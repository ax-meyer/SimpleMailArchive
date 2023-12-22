namespace SimpleMailArchiver.Data;

public class ImportProgress
{
    private readonly ILogger<ImportProgress> _logger;

    public ImportProgress(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ImportProgress>();
    }

    private string? _currentFolder;

    public int TotalMessageCount { get; set; }
    public int ParsedMessageCount { get; set; }
    public int ImportedMessageCount { get; set; }
    public int LocalMessagesDeletedCount { get; set; }
    public int RemoteMessagesDeletedCount { get; set; }

    public string? CurrentFolder
    {
        get => _currentFolder;
        set
        {
            _currentFolder = value;
            _logger.LogInformation("Processing folder {CurrentFolder}", _currentFolder);
        }
    }

    public string? InfoMessage { get; set; }
    public CancellationToken Ct { get; set; }
}