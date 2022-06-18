using MimeKit;
using Microsoft.EntityFrameworkCore;

namespace SimpleMailArchiver.Data;

public static partial class ImportMessages
{
    public static async Task ImportFromFolder(string[] emlPaths, string importFolderRoot, ImportProgress progress)
	{
        Program.ImportRunning = true;
        Program.Logger.LogInformation("Start import from folder");

        using var context = await Program.ContextFactory.CreateDbContextAsync(progress.Ct).ConfigureAwait(false);

        var basepath_uri = new Uri(importFolderRoot);
        try
        {
            foreach (string file in emlPaths)
            {
                progress.Ct.ThrowIfCancellationRequested();

                // get the relative path of the current email to the archive base path to know where to put the email in the archive.
                progress.CurrentFolder = Path.GetDirectoryName(basepath_uri.MakeRelativeUri(new Uri(file)).OriginalString)!;

                using var msg = MimeMessage.Load(file);
                var mmsg = await MailMessage.Construct(msg, progress.CurrentFolder, progress.Ct).ConfigureAwait(false);
                progress.ParsedMessageCount++;

                if (await context.MailMessages.AnyAsync(o => o.Hash == mmsg.Hash, progress.Ct).ConfigureAwait(false))
                    continue;

                progress.Ct.ThrowIfCancellationRequested();

                var dbTask = context.AddAsync(mmsg);
                var fileTask = msg.WriteToAsync(ParseMailMessage.MailSavePath(mmsg));
                await dbTask.ConfigureAwait(false);
                await fileTask.ConfigureAwait(false);
                await context.SaveChangesAsync(progress.Ct).ConfigureAwait(false);
                progress.ImportedMessageCount++;
            }
        }
        finally
        {
            await context.SaveChangesAsync(progress.Ct).ConfigureAwait(false);
            Program.ImportRunning = false;
            Program.Logger.LogInformation("Finished import from folder");
        }
    }
}
