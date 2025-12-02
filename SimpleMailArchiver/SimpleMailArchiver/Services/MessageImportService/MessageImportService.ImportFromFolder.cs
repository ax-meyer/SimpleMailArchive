using MimeKit;
using SimpleMailArchiver.Data;

namespace SimpleMailArchiver.Services.MessageImportService;

public partial class MessageImportService
{
    public async Task ImportFromFolder(string[] emlPaths, string importFolderRoot, ImportExecutor executor)
	{
        appContext.ImportRunning = true;
        _logger.LogInformation("Start import from folder");


        var basepathUri = new Uri(importFolderRoot);
        try
        {
            await Parallel.ForEachAsync(emlPaths, executor.Ct, async (file, innerToken) =>
            {
                await using var context = await dbContextFactory.CreateDbContextAsync(innerToken);

                innerToken.ThrowIfCancellationRequested();

                // get the relative path of the current email to the archive base path to know where to put the email in the archive.
                executor.CurrentFolder = Path.GetDirectoryName(basepathUri.MakeRelativeUri(new Uri(file)).OriginalString)!;

                using var msg = await MimeMessage.LoadAsync(file, innerToken);
                var saved = await messageHelperService.SaveMessage(msg, executor.CurrentFolder, innerToken);
                executor.ParsedMessageCount++;

                if (saved)
                    executor.ImportedMessageCount++;
            });
        }
        finally
        {
            //await context.SaveChangesAsync(progress.Ct);
            appContext.ImportRunning = false;
            _logger.LogInformation("Finished import from folder");
        }
    }
}
