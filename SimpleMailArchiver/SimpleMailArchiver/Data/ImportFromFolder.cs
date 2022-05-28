using MimeKit;

namespace SimpleMailArchiver.Data;

public static partial class ImportMessages
{
    public static async Task ImportFromFolder(string[] emlPaths, string importFolderRoot, ImportProgress progress)
	{
        using var context = Program.ContextFactory.CreateDbContext();

        var basepath_uri = new Uri(importFolderRoot);
        try
        {
            foreach (string file in emlPaths)
            {
                progress.Ct.ThrowIfCancellationRequested();

                // get the relative path of the current email to the archive base path to know where to put the email in the archive.
                progress.CurrentFolder = Path.GetDirectoryName(basepath_uri.MakeRelativeUri(new Uri(file)).OriginalString);

                using var msg = MimeMessage.Load(file);
                var mmsg = new MailMessage(msg, progress.CurrentFolder);
                progress.ParsedMessageCount++;

                if (context.MailMessages.Any(o => o.Hash == mmsg.Hash))
                    continue;

                progress.Ct.ThrowIfCancellationRequested();

                var dbTask = context.AddAsync(mmsg);
                var fileTask = msg.WriteToAsync(ParseMailMessage.MailSavePath(mmsg));
                await dbTask;
                await fileTask;
                progress.ImportedMessageCount++;
            }
        }
        finally
        {
            context.SaveChanges();
        }
    }
}
