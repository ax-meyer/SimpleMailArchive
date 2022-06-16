using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;
using Microsoft.EntityFrameworkCore;

namespace SimpleMailArchiver.Data
{
    public static partial class ImportMessages
	{
		public static async Task ImportFromServer(string accountFilename, ImportProgress progress)
        {
            Program.Logger.LogInformation($"Starting import from server on {accountFilename}");
            Program.ImportRunning = true;

            var account = Program.Config.Accounts.First(item => item.AccountFilename == accountFilename);

            using var client = new ImapClient();
            await client.ConnectAsync(account.ImapUrl, 993, SecureSocketOptions.SslOnConnect, cancellationToken: progress.Ct);
            await client.AuthenticateAsync(account.Username, account.Password, progress.Ct);
            using var context = await Program.ContextFactory.CreateDbContextAsync(progress.Ct);

            var folders = await client.GetFoldersAsync(new FolderNamespace('/', ""), cancellationToken: progress.Ct);
            try
            {
                foreach (var folder in folders)
                {
                    FolderOptions? folderOptions = account.FolderOptions.Where(f => f.Name == folder.FullName).FirstOrDefault();
                    if (folderOptions != null && folderOptions.Exclude)
                        continue;

                    string archiveFolder = "";
                    if (account.BasePathInArchive != null && account.BasePathInArchive.Trim().TrimEnd('/') != string.Empty)
                        archiveFolder = account.BasePathInArchive.TrimEnd('/') + "/";

                    if (folderOptions != null && folderOptions.NameInArchive != null && folderOptions.NameInArchive?.Trim() != string.Empty)
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

                    int deleteAfterDays = account.DeleteAfterDays;
                    if (folderOptions != null && folderOptions.DeleteAfterDays != null)
                        deleteAfterDays = (int)folderOptions.DeleteAfterDays;

                    foreach (var messageSummary in messageSummaries)
                    {
                        progress.Ct.ThrowIfCancellationRequested();

                        using var hmsg = new MimeMessage(messageSummary.Headers)
                        {
                            Date = (DateTimeOffset)messageSummary.InternalDate!
                        };
                        var headerMsg = await MailMessage.Construct(hmsg, archiveFolder, progress.Ct);
                        progress.ParsedMessageCount++;

                        // mark message to be deleted if meets the deletion date.
                        // delete will only be executed if whole folder is processed successfully.
                        if (deleteAfterDays > 0 && Math.Abs((headerMsg.Date - DateTime.Now).TotalDays) > deleteAfterDays)
                            messageToDeleteIds.Add(messageSummary.UniqueId);
                        else
                            messagesOnServer.Add(headerMsg.Hash);

                        // check if message is already in archive
                        var existingMsg = await context.MailMessages.FirstOrDefaultAsync(msg => msg.Hash == headerMsg.Hash, progress.Ct);
                        if (existingMsg != null)
                        {
                            // check if message is now in different folder on the server
                            // if yes, move in archive
                            if (existingMsg.Folder != archiveFolder)
                            {
                                var oldEmlPath = existingMsg.EmlPath;
                                existingMsg.Folder = archiveFolder;
                                var newEmlPath = existingMsg.EmlPath;
                                var dirName = Path.GetDirectoryName(newEmlPath);
                                Directory.CreateDirectory(dirName!);
                                File.Move(oldEmlPath, newEmlPath);
                            }
                            continue;
                        }
                                      
                        using var msg = await folder.GetMessageAsync(messageSummary.UniqueId, progress.Ct);
                        msg.Date = (DateTimeOffset)messageSummary.InternalDate;
                        var mmsg = await MailMessage.Construct(msg, archiveFolder, progress.Ct);

                        progress.Ct.ThrowIfCancellationRequested();

                        // don't pass CancellationToken to those two awaits - makes sure that state consitend even in case of cancellation.
                        var addDbTask = context.AddAsync(mmsg);
                        var WriteToDiskTask = msg.WriteToAsync(ParseMailMessage.MailSavePath(mmsg));
                        await addDbTask;
                        await WriteToDiskTask;
                        await context.SaveChangesAsync(progress.Ct);
                        progress.ImportedMessageCount++;
                    }

                    // delete messages marked for deletion.
                    if (messageToDeleteIds.Count > 0)
                    {
                        //await folder.AddFlagsAsync(messageToDeleteIds, MessageFlags.Deleted, true, progress.Ct);
                        //await folder.ExpungeAsync(progress.Ct);
                        progress.RemoteMessagesDeletedCount += messageToDeleteIds.Count;
                    }

                    if (folderOptions != null && folderOptions.SyncServerFolder)
                    {
                        var msgsToDelete = context.MailMessages.Where(msg => !messagesOnServer.Any(onServer => msg.Hash == onServer) && msg.Folder == folder.FullName).ToArray();
                        if (msgsToDelete != null && msgsToDelete.Length > 0)
                        {
                            foreach (var emlPath in msgsToDelete.Select(msg => msg.EmlPath))
                                File.Delete(emlPath);
                            context.MailMessages.RemoveRange(msgsToDelete);
                            progress.LocalMessagesDeletedCount += msgsToDelete.Length;
                        }
                    }
                }
            }
            finally
            {
                await context.SaveChangesAsync(progress.Ct);
                Program.ImportRunning = false;
                Program.Logger.LogInformation("Finished import from server");
            }
        }
	}
}

