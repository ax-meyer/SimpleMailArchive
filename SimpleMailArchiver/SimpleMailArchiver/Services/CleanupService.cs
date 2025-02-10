using Microsoft.EntityFrameworkCore;
using SimpleMailArchiver.Data;

namespace SimpleMailArchiver.Services;

public class CleanupService(
    IDbContextFactory<ArchiveContext> dbContextFactory,
    MailMessageHelperService messageHelperService,
    ILoggerFactory loggerFactory)
{
    private readonly ILogger<CleanupService> _logger = loggerFactory.CreateLogger<CleanupService>();

    /// <summary>
    /// WARNING: NOT TESTED YET
    /// </summary>
    /// <returns></returns>
    private async Task RecalculateHashes(CancellationToken token = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(token);
        var count = 0;
        foreach (var message in context.MailMessages)
        {
            message.Hash = await MailMessageHelperService.CreateMailHash(message, token);
            if (count++ % 100 == 0)
                await context.SaveChangesAsync(token);
        }
    }

    /// <summary>
    /// WARNING: NOT TESTED YET
    /// </summary>
    /// <returns></returns>
    private async Task RemoveDuplicates(CancellationToken token = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(token);
        var duplicates = context.MailMessages.GroupBy(i => i.Hash)
            .Where(x => x.Count() > 1)
            .Select(val => val.Key);

        foreach (var dupHash in duplicates)
        {
            var msgsEqHash = context.MailMessages.Where(m => m.Hash == dupHash);
            var refMsg = msgsEqHash.First();
            var start = true;
            foreach (var msg in msgsEqHash)
            {
                if (start)
                {
                    start = false;
                    continue;
                }

                if (refMsg.Equals(msg))
                {
                    File.Delete(messageHelperService.GetEmlPath(msg));
                    context.MailMessages.Remove(msg);
                }
            }
        }

        await context.SaveChangesAsync(token);
    }
}