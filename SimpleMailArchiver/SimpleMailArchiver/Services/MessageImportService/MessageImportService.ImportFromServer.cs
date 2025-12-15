using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using SimpleMailArchiver.Data;

namespace SimpleMailArchiver.Services.MessageImportService;

public partial class MessageImportService
{
    public async Task ImportFromServer(string accountFilename, ImportProgress progress, CancellationToken ct)
    {
        _logger.BeginScope(nameof(ImportFromServer));
        _logger.LogInformation("Starting import from server on {AccountFilename}", accountFilename);

        var account = appContext.Accounts.First(item => item.AccountFilename == accountFilename);
        _logger.LogDebug("Account loaded: {Username} on {ImapUrl}", account.Username, account.ImapUrl);

        using var client = new ImapClient();
        _logger.LogDebug("Connecting to IMAP server {ImapUrl}:993", account.ImapUrl);
        await client.ConnectAsync(account.ImapUrl, 993, SecureSocketOptions.SslOnConnect, ct);
        _logger.LogDebug("Connected to IMAP server successfully");

        await client.AuthenticateAsync(account.Username, account.Password, ct);
        _logger.LogDebug("Authenticated as {Username}", account.Username);

        await using var context = await dbContextFactory.CreateDbContextAsync(ct);
        var folders = await client.GetFoldersAsync(new FolderNamespace('/', ""), cancellationToken: ct);
        _logger.LogDebug("Retrieved {FolderCount} folders from server", folders.Count);

        try
        {
            foreach (var folder in folders)
            {
                var folderOptions = account.FolderOptions?.FirstOrDefault(f => f.Name == folder.FullName);
                if (folderOptions is { Exclude: true })
                {
                    _logger.LogInformation("Skipping excluded folder {Folder}", folder.FullName);
                    continue;
                }

                _logger.LogInformation("Processing folder {Folder}", folder.FullName);

                var archiveFolder = "";
                if (account.BasePathInArchive != null && account.BasePathInArchive.Trim().TrimEnd('/') != string.Empty)
                    archiveFolder = account.BasePathInArchive.TrimEnd('/') + "/";

                if (folderOptions is { NameInArchive: not null } && folderOptions.NameInArchive.Trim() != string.Empty)
                    archiveFolder += folderOptions.NameInArchive;
                else
                    archiveFolder += folder.FullName;

                _logger.LogDebug("Archive folder path: {ArchiveFolder}", archiveFolder);

                ct.ThrowIfCancellationRequested();

                progress.Report(new ProgressData(InfoMessage: "Import running", CurrentFolder: folder.FullName));

                var folderAccess = await folder.OpenAsync(FolderAccess.ReadWrite, ct);

                var msgUids = await folder.SearchAsync(SearchQuery.All, ct);
                _logger.LogDebug("Found {MessageCount} messages in folder {Folder}", msgUids.Count, folder.FullName);

                var messageSummaries = await folder.FetchAsync(msgUids,
                    MessageSummaryItems.InternalDate | MessageSummaryItems.Headers, ct);
                var messageToDeleteIds = new List<UniqueId>();
                var messagesOnServer = new List<string>();

                var deleteAfterDays = account.DeleteAfterDays;
                if (folderOptions?.DeleteAfterDays is { } folderSpecificDeleteAfterDays)
                {
                    deleteAfterDays = folderSpecificDeleteAfterDays;
                    _logger.LogDebug("Using folder-specific delete policy: {DeleteAfterDays} days", deleteAfterDays);
                }
                else
                {
                    _logger.LogDebug("Using account-level delete policy: {DeleteAfterDays} days", deleteAfterDays);
                }

                _logger.LogInformation("Processing {MessageCount} messages in folder {Folder}", messageSummaries.Count,
                    folder.FullName);

                int processedCount = 0;
                foreach (var messageSummary in messageSummaries)
                {
                    ct.ThrowIfCancellationRequested();

                    _logger.LogDebug("Processing message UID {Uid} with InternalDate {Date}", messageSummary.UniqueId,
                        messageSummary.InternalDate);

                    using var hmsg = new MimeMessage(messageSummary.Headers);
                    hmsg.Date = (DateTimeOffset)messageSummary.InternalDate!;
                    var headerMsg = await MailParser.Construct(hmsg, archiveFolder, ct);
                    _logger.LogDebug("Parsed message header: Subject={Subject}, From={From}, Hash={Hash}",
                        hmsg.Subject ?? "<no subject>", hmsg.From.ToString(), headerMsg.Hash);

                    progress.Report(new ProgressData(ParsedMessageCount: progress.ParsedMessageCount + 1));

                    // mark message to be deleted if meets the deletion date.
                    // delete will only be executed if whole folder is processed successfully.
                    if (deleteAfterDays > 0 && Math.Abs((headerMsg.Date - DateTime.Now).TotalDays) > deleteAfterDays)
                    {
                        _logger.LogDebug(
                            "Message UID {Uid} marked for deletion (age: {DaysOld} days, threshold: {DeleteAfterDays})",
                            messageSummary.UniqueId,
                            Math.Abs((headerMsg.Date - DateTime.Now).TotalDays),
                            deleteAfterDays);
                        messageToDeleteIds.Add(messageSummary.UniqueId);
                    }
                    else
                        messagesOnServer.Add(headerMsg.Hash);

                    // check if message is already in archive
                    var existingMsg = await context.MailMessages
                        .FirstOrDefaultAsync(msg => msg.Hash == headerMsg.Hash, ct);
                    if (existingMsg != null)
                    {
                        _logger.LogDebug("Message UID {Uid} already exists in archive with ID {MsgId}",
                            messageSummary.UniqueId, existingMsg.Id);
                        var existingPath = messageHelperService.GetEmlPath(existingMsg);
                        if (!File.Exists(existingPath))
                        {
                            _logger.LogError("Did not find eml file for existing message {MsgId} at path {Path}, will try to redownload",
                                existingMsg.Id, existingPath);
                        }
                        else
                        {
                            // check if message is now in different folder on the server
                            // if yes, move in archive
                            if (existingMsg.Folder != archiveFolder)
                            {
                                _logger.LogDebug("Moving message {MsgId} from folder '{OldFolder}' to '{NewFolder}'",
                                    existingMsg.Id, existingMsg.Folder, archiveFolder);

                                var oldEmlPath = messageHelperService.GetEmlPath(existingMsg);
                                existingMsg.Folder = archiveFolder;
                                var newEmlPath = messageHelperService.GetEmlPath(existingMsg);
                                var dirName = Path.GetDirectoryName(newEmlPath);
                                Directory.CreateDirectory(dirName!);
                                File.Move(oldEmlPath, newEmlPath);
                                await context.SaveChangesAsync(CancellationToken.None);

                                _logger.LogDebug("Message moved successfully from {OldPath} to {NewPath}", oldEmlPath,
                                    newEmlPath);
                            }

                            continue;
                        }
                    }

                    _logger.LogDebug("Downloading full message UID {Uid} from server", messageSummary.UniqueId);
                    using var msg = await folder.GetMessageAsync(messageSummary.UniqueId, ct);
                    msg.Date = (DateTimeOffset)messageSummary.InternalDate;

                    var saved = await messageHelperService.SaveMessage(msg, folder.FullName, ct);
                    progress.Report(new ProgressData(ParsedMessageCount: progress.ParsedMessageCount + 1));

                    if (saved)
                    {
                        _logger.LogDebug("Message UID {Uid} successfully saved to archive", messageSummary.UniqueId);
                        progress.Report(new ProgressData(ImportedMessageCount: progress.ImportedMessageCount + 1));
                    }
                    else
                    {
                        _logger.LogError(
                            "Message UID {Uid} was not saved - should not happen since duplicate is checked before",
                            messageSummary.UniqueId);
                    }

                    processedCount++;
                }

                _logger.LogInformation("Completed processing {ProcessedCount} messages in folder {Folder}",
                    processedCount, folder.FullName);

                // delete messages marked for deletion.
                if (messageToDeleteIds.Count > 0)
                {
#if DEBUG
                    _logger.LogInformation("Debug mode active, not deleting {DeleteCount} messages on server",
                        messageToDeleteIds.Count);
#else
                        _logger.LogInformation("Deleting {DeleteCount} messages from server folder {Folder}", messageToDeleteIds.Count, folder.FullName);
                        foreach (var id in messageToDeleteIds)
                            _logger.LogDebug("Deleting message with UID {Uid}", id);
                        
                        await folder.AddFlagsAsync(messageToDeleteIds, MessageFlags.Deleted, true, ct);
                        await folder.ExpungeAsync(ct);
                        _logger.LogDebug("Deletion and expunge completed for folder {Folder}", folder.FullName);
#endif
                    progress.Report(new ProgressData(
                        RemoteMessagesDeletedCount: progress.RemoteMessagesDeletedCount + messageToDeleteIds.Count));
                }

                if (folderOptions is { SyncServerFolder: true })
                {
                    _logger.LogDebug("Syncing folder {Folder} - checking for messages to delete locally",
                        folder.FullName);

                    var msgsToDelete = context.MailMessages.Where(msg =>
                            messagesOnServer.All(onServer => msg.Hash != onServer) && msg.Folder == folder.FullName)
                        .ToArray();

                    _logger.LogDebug("Found {DeleteCount} messages in archive not present on server",
                        msgsToDelete.Length);

                    if (msgsToDelete is { Length: > 0 })
                    {
                        var deletedMessages = new List<MailMessage>();
                        foreach (var msg in msgsToDelete)
                            try
                            {
                                _logger.LogDebug("Deleting local message {MsgId} (Hash: {Hash})", msg.Id, msg.Hash);
                                var emlPath = messageHelperService.GetEmlPath(msg);
                                File.Delete(emlPath);
                                deletedMessages.Add(msg);
                                _logger.LogDebug("Deleted file {FilePath}", emlPath);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to delete message {MsgId}", msg.Id);
                            }

                        context.MailMessages.RemoveRange(deletedMessages);
                        await context.SaveChangesAsync(CancellationToken.None);
                        _logger.LogInformation("Removed {DeleteCount} messages from database", deletedMessages.Count);

                        progress.Report(new ProgressData(
                            LocalMessagesDeletedCount: progress.LocalMessagesDeletedCount + deletedMessages.Count));
                    }
                }
            }

            _logger.LogInformation("Finished import from server");
        }
        finally
        {
            await context.SaveChangesAsync(ct);
            _logger.LogDebug("Final SaveChangesAsync completed");
        }
    }
}