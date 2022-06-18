using MimeKit;
using Microsoft.EntityFrameworkCore;

namespace SimpleMailArchiver.Data;

public static partial class ImportMessages
{
    public static async Task ImportFromFolder(string[] emlPaths, string importFolderRoot, ImportProgress progress)
	{
        Program.ImportRunning = true;
        Program.Logger.LogInformation("Start import from folder");


        var basepath_uri = new Uri(importFolderRoot);
        var token = progress.Ct;
        try
        {
            await Parallel.ForEachAsync(emlPaths, async (file, token) =>
            {
                using var context = await Program.ContextFactory.CreateDbContextAsync(progress.Ct).ConfigureAwait(false);

                progress.Ct.ThrowIfCancellationRequested();

                // get the relative path of the current email to the archive base path to know where to put the email in the archive.
                progress.CurrentFolder = Path.GetDirectoryName(basepath_uri.MakeRelativeUri(new Uri(file)).OriginalString)!;

                using var msg = MimeMessage.Load(file);
                bool saved = await Utils.SaveMessage(msg, progress.CurrentFolder, context, progress.Ct).ConfigureAwait(false);
                progress.ParsedMessageCount++;

                if (saved)
                    progress.ImportedMessageCount++;
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw;
        }
        finally
        {
            //await context.SaveChangesAsync(progress.Ct).ConfigureAwait(false);
            Program.ImportRunning = false;
            Program.Logger.LogInformation("Finished import from folder");
        }
    }
}
