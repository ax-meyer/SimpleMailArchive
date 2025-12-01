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
    public async Task ImportFromServer(string accountFilename, ImportProgress progress)
    {
        _logger.LogInformation("Starting import from server on {AccountFilename}", accountFilename);
        appContext.ImportRunning = true;

        var account = appContext.Accounts.First(item => item.AccountFilename == accountFilename);

        using var client = new ImapClient();
        await client.ConnectAsync(account.ImapUrl, 993, SecureSocketOptions.SslOnConnect, cancellationToken: progress.Ct);
        await client.AuthenticateAsync(account.Username, account.Password, progress.Ct);

        await using var context = await dbContextFactory.CreateDbContextAsync(progress.Ct);
        var folders = await client.GetFoldersAsync(new FolderNamespace('/', ""), cancellationToken: progress.Ct);
        try
        {
            foreach (var folder in folders)
            {
                var folderOptions = account.FolderOptions?.FirstOrDefault(f => f.Name == folder.FullName);
                if (folderOptions is { Exclude: true }) continue;

                var archiveFolder = "";
                if (account.BasePathInArchive != null && account.BasePathInArchive.Trim().TrimEnd('/') != string.Empty)
                    archiveFolder = account.BasePathInArchive.TrimEnd('/') + "/";

                if (folderOptions is { NameInArchive: not null } && folderOptions.NameInArchive.Trim() != string.Empty)
                    archiveFolder += folderOptions.NameInArchive;
                else
                    archiveFolder += folder.FullName;

                progress.Ct.ThrowIfCancellationRequested();

                progress.CurrentFolder = folder.FullName;

                var folderAccess = await folder.OpenAsync(FolderAccess.ReadWrite, progress.Ct);
                var msgUids = await folder.SearchAsync(SearchQuery.All, progress.Ct);
                var messageSummaries = await folder.FetchAsync(msgUids, MessageSummaryItems.InternalDate | MessageSummaryItems.Headers, progress.Ct);
                var messageToDeleteIds = new List<UniqueId>();
                var messagesOnServer = new List<string>();

                var deleteAfterDays = account.DeleteAfterDays;
                if (folderOptions?.DeleteAfterDays is { } folderSpecificDeleteAfterDays)
                    deleteAfterDays = folderSpecificDeleteAfterDays;

                foreach (var messageSummary in messageSummaries)
                {
                    progress.Ct.ThrowIfCancellationRequested();

                    using var hmsg = new MimeMessage(messageSummary.Headers);
                    hmsg.Date = (DateTimeOffset)messageSummary.InternalDate!;
                    var headerMsg = await MailParser.Construct(hmsg, archiveFolder, progress.Ct);
                    progress.ParsedMessageCount++;

                    // mark message to be deleted if meets the deletion date.
                    // delete will only be executed if whole folder is processed successfully.
                    if (deleteAfterDays > 0 && Math.Abs((headerMsg.Date - DateTime.Now).TotalDays) > deleteAfterDays)
                        messageToDeleteIds.Add(messageSummary.UniqueId);
                    else
                        messagesOnServer.Add(headerMsg.Hash);

                    // check if message is already in archive
                    var existingMsg = await context.MailMessages
                        .FirstOrDefaultAsync(msg => msg.Hash == headerMsg.Hash, progress.Ct);
                    if (existingMsg != null)
                    {
                        // check if message is now in different folder on the server
                        // if yes, move in archive
                        if (existingMsg.Folder != archiveFolder)
                        {
                            var oldEmlPath = messageHelperService.GetEmlPath(existingMsg);
                            existingMsg.Folder = archiveFolder;
                            var newEmlPath = messageHelperService.GetEmlPath(existingMsg);
                            var dirName = Path.GetDirectoryName(newEmlPath);
                            Directory.CreateDirectory(dirName!);
                            File.Move(oldEmlPath, newEmlPath);
                            await context.SaveChangesAsync();
                        }

                        continue;
                    }

                    using var msg = await folder.GetMessageAsync(messageSummary.UniqueId, progress.Ct);
                    msg.Date = (DateTimeOffset)messageSummary.InternalDate;

                    var saved = await messageHelperService.SaveMessage(msg, progress.CurrentFolder, progress.Ct);
                    progress.ParsedMessageCount++;

                    if (saved)
                        progress.ImportedMessageCount++;
                }

                // delete messages marked for deletion.
                if (messageToDeleteIds.Count > 0)
                {
#if DEBUG
                    _logger.LogInformation("Debug mode, not deleting on server");
#else
                        await folder.AddFlagsAsync(messageToDeleteIds, MessageFlags.Deleted, true, progress.Ct);
                        await folder.ExpungeAsync(progress.Ct);
#endif
                    progress.RemoteMessagesDeletedCount += messageToDeleteIds.Count;
                }

                if (folderOptions is { SyncServerFolder: true })
                {
                    var msgsToDelete = context.MailMessages.Where(msg =>
                            messagesOnServer.All(onServer => msg.Hash != onServer) && msg.Folder == folder.FullName)
                        .ToArray();
                    if (msgsToDelete is { Length: > 0 })
                    {
                        var deletedMessages = new List<MailMessage>();
                        foreach (var msg in msgsToDelete)
                        {
                            try
                            {
                                var emlPath = messageHelperService.GetEmlPath(msg);
                                File.Delete(emlPath);
                                deletedMessages.Add(msg);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to delete message {MsgId}", msg.Id);
                            }
                        }

                        context.MailMessages.RemoveRange(deletedMessages);
                        await context.SaveChangesAsync();
                        progress.LocalMessagesDeletedCount += deletedMessages.Count;
                    }
                }
            }

            _logger.LogInformation("Finished import from server");
        }
        finally
        {
            await context.SaveChangesAsync(progress.Ct);
            appContext.ImportRunning = false;
        }
    }
}