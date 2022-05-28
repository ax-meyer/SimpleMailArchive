using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;

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

                    var folderAccess = await folder.OpenAsync(FolderAccess.ReadOnly, progress.Ct);
                    var msgUids = await folder.SearchAsync(SearchQuery.All, progress.Ct);
                    var messageSummaries = await folder.FetchAsync(msgUids, MessageSummaryItems.InternalDate | MessageSummaryItems.Headers, progress.Ct);

                    foreach (var messageSummary in messageSummaries)
                    {
                        progress.Ct.ThrowIfCancellationRequested();

                        using var hmsg = new MimeMessage(messageSummary.Headers)
                        {
                            Date = (DateTimeOffset)messageSummary.InternalDate
                        };
                        var headerMsg = new MailMessage(hmsg, folder.ToString());
                        progress.ParsedMessageCount++;

                        if (context.MailMessages.Any(o => o.Hash == headerMsg.Hash))
                            continue;

                        using var msg = await folder.GetMessageAsync(messageSummary.UniqueId, progress.Ct);
                        msg.Date = (DateTimeOffset)messageSummary.InternalDate;
                        var mmsg = new MailMessage(msg, folder.ToString());
                        if (headerMsg.Hash != mmsg.Hash)
                            throw new InvalidDataException("hash mismatch");

                        progress.Ct.ThrowIfCancellationRequested();

                        // don't pass CancellationToken to those two awaits - makes sure that state consitend even in case of cancellation.
                        var addDbTask = context.AddAsync(mmsg);
                        var WriteToDiskTask = msg.WriteToAsync(ParseMailMessage.MailSavePath(mmsg));
                        await addDbTask;
                        await WriteToDiskTask;
                        progress.ImportedMessageCount++;
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

