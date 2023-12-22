using Microsoft.EntityFrameworkCore;
using SimpleMailArchiver.Data;

namespace SimpleMailArchiver.Services;

public class CleanupService
{
    private readonly IDbContextFactory<ArchiveContext> _dbContextFactory;
    private readonly ILogger<CleanupService> _logger;
    private readonly MailMessageHelperService _messageHelperService;

    public CleanupService(IDbContextFactory<ArchiveContext> dbContextFactory, MailMessageHelperService messageHelperService, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<CleanupService>();
        _dbContextFactory = dbContextFactory;
        _messageHelperService = messageHelperService;
    }

    /// <summary>
    /// WARNING: NOT TESTED YET
    /// </summary>
    /// <returns></returns>
    private async Task RecalculateHashes(CancellationToken token = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(token);
        int count = 0;
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
        await using var context = await _dbContextFactory.CreateDbContextAsync(token);
        var duplicates = context.MailMessages.GroupBy(i => i.Hash)
            .Where(x => x.Count() > 1)
            .Select(val => val.Key);

        foreach (var dupHash in duplicates)
        {
            var msgsEqHash = context.MailMessages.Where(m => m.Hash == dupHash);
            var refMsg = msgsEqHash.First();
            bool start = true;
            foreach (var msg in msgsEqHash)
            {
                if (start)
                {
                    start = false;
                    continue;
                }

                if (refMsg.Equals(msg))
                {
                    File.Delete(_messageHelperService.GetEmlPath(msg));
                    context.MailMessages.Remove(msg);
                }
            }
        }

        await context.SaveChangesAsync(token);
    }
}