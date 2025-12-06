using MimeKit;
using SimpleMailArchiver.Data;

namespace SimpleMailArchiver.Services.MessageImportService;

public partial class MessageImportService
{
    public async Task ImportFromFolder(string importFolderRoot, string basePathInArchive, ImportProgress progress,
        CancellationToken ct)
    {
        _logger.LogInformation("Start import from folder");

        var emlPaths = Directory.GetFiles(importFolderRoot, "*.eml", SearchOption.AllDirectories);

        if (emlPaths.Length == 0)
        {
            progress.Report(new ProgressData($"No .eml files found for import in {importFolderRoot}"));
            return;
        }

        progress.Report(new ProgressData(TotalMessageCount: emlPaths.Length));

        var basepathUri = new Uri(importFolderRoot);
        var groupedByFolder = emlPaths.GroupBy(path =>
            Path.GetDirectoryName(basepathUri.MakeRelativeUri(new Uri(path)).OriginalString));
        try
        {
            foreach (var group in groupedByFolder)
            {
                await using var context = await dbContextFactory.CreateDbContextAsync(ct);

                ct.ThrowIfCancellationRequested();

                var pathInImportFolder = group.Key ?? throw new Exception("Could not determine import folder path");
                progress.Report(new ProgressData(CurrentFolder: pathInImportFolder));
                var pathInArchive = Path.Join(basePathInArchive, pathInImportFolder);
                await Parallel.ForEachAsync(group, ct, async (file, innerToken) =>
                {
                    ct.ThrowIfCancellationRequested();
                    using var msg = await MimeMessage.LoadAsync(file, innerToken);
                    var saved = await messageHelperService.SaveMessage(msg, pathInArchive, innerToken);
                    progress.Report(new ProgressData(ParsedMessageCount: progress.ParsedMessageCount + 1));

                    if (saved)
                        progress.Report(new ProgressData(ImportedMessageCount: progress.ImportedMessageCount + 1));
                });
            }
        }
        finally
        {
            //await context.SaveChangesAsync(progress.Ct);
            _logger.LogInformation("Finished import from folder");
        }
    }
}