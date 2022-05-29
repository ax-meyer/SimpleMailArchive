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
		public static async Task ImportFromServer(int accountId, ImportProgress progress)
        {
            var account = Program.Config.Accounts.First(item => item.ID == accountId);

            using var client = new ImapClient();
            await client.ConnectAsync(account.ImapUrl, 993, SecureSocketOptions.SslOnConnect, cancellationToken: progress.Ct);
            await client.AuthenticateAsync(account.Username, account.Password, progress.Ct);
            using var context = await Program.ContextFactory.CreateDbContextAsync(progress.Ct);

            var folders = await client.GetFoldersAsync(new FolderNamespace('/', ""), cancellationToken: progress.Ct);
            try
            {
                foreach (var folder in folders)
                {
                    if (account.IgnoreFolders.Contains(folder.ToString()))
                        continue;
                    progress.Ct.ThrowIfCancellationRequested();

                    progress.CurrentFolder = folder.ToString();

                    var folderAccess = await folder.OpenAsync(FolderAccess.ReadWrite, progress.Ct);
                    var msgUids = await folder.SearchAsync(SearchQuery.All, progress.Ct);
                    var messageSummaries = await folder.FetchAsync(msgUids, MessageSummaryItems.InternalDate | MessageSummaryItems.Headers, progress.Ct);
                    var messageToDeleteIds = new List<UniqueId>();

                    foreach (var messageSummary in messageSummaries)
                    {
                        progress.Ct.ThrowIfCancellationRequested();

                        using var hmsg = new MimeMessage(messageSummary.Headers)
                        {
                            Date = (DateTimeOffset)messageSummary.InternalDate
                        };
                        var headerMsg = await MailMessage.Construct(hmsg, folder.ToString(), progress.Ct);
                        progress.ParsedMessageCount++;

                        // mark message to be deleted if meets the deletion date.
                        // delete will only be executed if whole folder is processed successfully.
                        if (account.DeleteAfterDays > 0 && Math.Abs((headerMsg.Date - DateTime.Now).TotalDays) > account.DeleteAfterDays)
                            messageToDeleteIds.Add(messageSummary.UniqueId);

                        // check if message is already in archive
                        var existingMsg = await context.MailMessages.FirstOrDefaultAsync(msg => msg.Hash == headerMsg.Hash, progress.Ct);
                        if (existingMsg != null)
                        {
                            // check if message is now in different folder on the server
                            // if yes, move in archive
                            if (existingMsg.Folder != folder.ToString())
                            {
                                var oldEmlPath = existingMsg.EmlPath;
                                existingMsg.Folder = folder.ToString();
                                var newEmlPath = existingMsg.EmlPath;
                                var dirName = Path.GetDirectoryName(newEmlPath);
                                Directory.CreateDirectory(dirName);
                                File.Move(oldEmlPath, newEmlPath);
                            }
                            continue;
                        }
                                      
                        using var msg = await folder.GetMessageAsync(messageSummary.UniqueId, progress.Ct);
                        msg.Date = (DateTimeOffset)messageSummary.InternalDate;
                        var mmsg = await MailMessage.Construct(msg, folder.ToString(), progress.Ct);

                        progress.Ct.ThrowIfCancellationRequested();

                        // don't pass CancellationToken to those two awaits - makes sure that state consitend even in case of cancellation.
                        var addDbTask = context.AddAsync(mmsg);
                        var WriteToDiskTask = msg.WriteToAsync(ParseMailMessage.MailSavePath(mmsg));
                        await addDbTask;
                        await WriteToDiskTask;
                        progress.ImportedMessageCount++;
                    }

                    // delete messages marked for deletion.
                    if (messageToDeleteIds.Count > 0)
                    {
                        //await folder.AddFlagsAsync(messageToDeleteIds, MessageFlags.Deleted, true, progress.Ct);
                        //await folder.ExpungeAsync(progress.Ct);
                    }
                }
            }
            finally
            {
                await context.SaveChangesAsync();
            }
        }
	}
}

