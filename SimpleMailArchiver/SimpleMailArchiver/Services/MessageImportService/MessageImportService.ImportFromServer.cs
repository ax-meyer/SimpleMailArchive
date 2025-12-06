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
        _logger.LogInformation("Starting import from server on {AccountFilename}", accountFilename);

        var account = appContext.Accounts.First(item => item.AccountFilename == accountFilename);

        using var client = new ImapClient();
        await client.ConnectAsync(account.ImapUrl, 993, SecureSocketOptions.SslOnConnect, ct);
        await client.AuthenticateAsync(account.Username, account.Password, ct);

        await using var context = await dbContextFactory.CreateDbContextAsync(ct);
        var folders = await client.GetFoldersAsync(new FolderNamespace('/', ""), cancellationToken: ct);
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

                ct.ThrowIfCancellationRequested();

                progress.Report(new ProgressData(CurrentFolder: folder.FullName));

                var folderAccess = await folder.OpenAsync(FolderAccess.ReadWrite, ct);
                var msgUids = await folder.SearchAsync(SearchQuery.All, ct);
                var messageSummaries = await folder.FetchAsync(msgUids,
                    MessageSummaryItems.InternalDate | MessageSummaryItems.Headers, ct);
                var messageToDeleteIds = new List<UniqueId>();
                var messagesOnServer = new List<string>();

                var deleteAfterDays = account.DeleteAfterDays;
                if (folderOptions?.DeleteAfterDays is { } folderSpecificDeleteAfterDays)
                    deleteAfterDays = folderSpecificDeleteAfterDays;

                foreach (var messageSummary in messageSummaries)
                {
                    ct.ThrowIfCancellationRequested();

                    using var hmsg = new MimeMessage(messageSummary.Headers);
                    hmsg.Date = (DateTimeOffset)messageSummary.InternalDate!;
                    var headerMsg = await MailParser.Construct(hmsg, archiveFolder, ct);
                    progress.Report(new ProgressData(ParsedMessageCount: progress.ParsedMessageCount + 1));

                    // mark message to be deleted if meets the deletion date.
                    // delete will only be executed if whole folder is processed successfully.
                    if (deleteAfterDays > 0 && Math.Abs((headerMsg.Date - DateTime.Now).TotalDays) > deleteAfterDays)
                        messageToDeleteIds.Add(messageSummary.UniqueId);
                    else
                        messagesOnServer.Add(headerMsg.Hash);

                    // check if message is already in archive
                    var existingMsg = await context.MailMessages
                        .FirstOrDefaultAsync(msg => msg.Hash == headerMsg.Hash, ct);
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
                            await context.SaveChangesAsync(CancellationToken.None);
                        }

                        continue;
                    }

                    using var msg = await folder.GetMessageAsync(messageSummary.UniqueId, ct);
                    msg.Date = (DateTimeOffset)messageSummary.InternalDate;

                    var saved = await messageHelperService.SaveMessage(msg, folder.FullName, ct);
                    progress.Report(new ProgressData(ParsedMessageCount: progress.ParsedMessageCount + 1));


                    if (saved)
                        progress.Report(new ProgressData(ImportedMessageCount: progress.ImportedMessageCount + 1));
                }

                // delete messages marked for deletion.
                if (messageToDeleteIds.Count > 0)
                {
#if DEBUG
                    _logger.LogInformation("Debug mode, not deleting on server");
#else
                        await folder.AddFlagsAsync(messageToDeleteIds, MessageFlags.Deleted, true, ct);
                        await folder.ExpungeAsync(ct);
#endif
                    progress.Report(new ProgressData(
                        RemoteMessagesDeletedCount: progress.RemoteMessagesDeletedCount + messageToDeleteIds.Count));
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

                        context.MailMessages.RemoveRange(deletedMessages);
                        await context.SaveChangesAsync(CancellationToken.None);
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
        }
    }
}