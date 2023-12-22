using Microsoft.EntityFrameworkCore;
using SimpleMailArchiver.Data;

namespace SimpleMailArchiver.Services.MessageImportService;

public partial class MessageImportService
{
    private readonly ILogger<MessageImportService> _logger;
    private readonly IDbContextFactory<ArchiveContext> _dbContextFactory;
    private readonly ApplicationContext _appContext;
    private readonly MailMessageHelperService _messageHelperService;
    public MessageImportService(IDbContextFactory<ArchiveContext> dbContextFactory, ApplicationContext appContext, MailMessageHelperService messageHelperService, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<MessageImportService>();
        _dbContextFactory = dbContextFactory;
        _appContext = appContext;
        _messageHelperService = messageHelperService;
    }
}