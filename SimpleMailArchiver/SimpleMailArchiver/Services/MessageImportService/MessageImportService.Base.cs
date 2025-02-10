using Microsoft.EntityFrameworkCore;
using SimpleMailArchiver.Data;

namespace SimpleMailArchiver.Services.MessageImportService;

public partial class MessageImportService(
    IDbContextFactory<ArchiveContext> dbContextFactory,
    ApplicationContext appContext,
    MailMessageHelperService messageHelperService,
    ILoggerFactory loggerFactory)
{
    private readonly ILogger<MessageImportService> _logger = loggerFactory.CreateLogger<MessageImportService>();
}