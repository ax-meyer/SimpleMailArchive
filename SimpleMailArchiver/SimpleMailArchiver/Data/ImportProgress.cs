namespace SimpleMailArchiver.Data;

public class ImportProgress(ILoggerFactory loggerFactory)
{
    private readonly ILogger<ImportProgress> _logger = loggerFactory.CreateLogger<ImportProgress>();

    public int TotalMessageCount { get; set; }
    public int ParsedMessageCount { get; set; }
    public int ImportedMessageCount { get; set; }
    public int LocalMessagesDeletedCount { get; set; }
    public int RemoteMessagesDeletedCount { get; set; }

    public string? CurrentFolder
    {
        get;
        set
        {
            field = value;
            _logger.LogInformation("Processing folder {CurrentFolder}", field);
        }
    }

    public string? InfoMessage { get; set; }
    public CancellationToken Ct { get; set; }
}