namespace SimpleMailArchiver.Data;

public record ProgressData(
    string? InfoMessage = null,
    string? CurrentFolder = null,
    int? TotalMessageCount = null,
    int? ParsedMessageCount = null,
    int? ImportedMessageCount = null,
    int? LocalMessagesDeletedCount = null,
    int? RemoteMessagesDeletedCount = null);

public interface IImportProgress
{
    int TotalMessageCount { get; }
    int ParsedMessageCount { get; }
    int ImportedMessageCount { get; }
    int LocalMessagesDeletedCount { get; }
    int RemoteMessagesDeletedCount { get; }

    string? CurrentFolder { get; }

    string? InfoMessage { get; }
    event EventHandler? ProgressUpdated;
}

public partial class ImportProgress(ILoggerFactory loggerFactory) : IImportProgress, IProgress<ProgressData>
{
    private readonly ILogger<ImportProgress> _logger = loggerFactory.CreateLogger<ImportProgress>();

    public event EventHandler? ProgressUpdated;
    public int TotalMessageCount { get; private set; }
    public int ParsedMessageCount { get; private set; }
    public int ImportedMessageCount { get; private set; }
    public int LocalMessagesDeletedCount { get; private set; }
    public int RemoteMessagesDeletedCount { get; private set; }

    public string? CurrentFolder { get; private set; }

    public string? InfoMessage { get; private set; }

    public void Report(ProgressData value)
    {
        if (value.InfoMessage is not null) InfoMessage = value.InfoMessage;
        if (value.CurrentFolder is not null) CurrentFolder = value.CurrentFolder;
        if (value.TotalMessageCount is not null) TotalMessageCount = value.TotalMessageCount.Value;
        if (value.ParsedMessageCount is not null) ParsedMessageCount = value.ParsedMessageCount.Value;
        if (value.ImportedMessageCount is not null) ImportedMessageCount = value.ImportedMessageCount.Value;
        if (value.LocalMessagesDeletedCount is not null)
            LocalMessagesDeletedCount = value.LocalMessagesDeletedCount.Value;
        if (value.RemoteMessagesDeletedCount is not null)
            RemoteMessagesDeletedCount = value.RemoteMessagesDeletedCount.Value;

        PublishUpdate();
    }

    public void Reset()
    {
        InfoMessage = "Waiting for import to start";
        CurrentFolder = null;
        TotalMessageCount = 0;
        ParsedMessageCount = 0;
        ImportedMessageCount = 0;
        LocalMessagesDeletedCount = 0;
        RemoteMessagesDeletedCount = 0;
        PublishUpdate();
    }

    private void PublishUpdate()
    {
        LogUpdate(InfoMessage ?? string.Empty, CurrentFolder ?? string.Empty, TotalMessageCount, ParsedMessageCount,
            ImportedMessageCount, LocalMessagesDeletedCount, RemoteMessagesDeletedCount);
        ProgressUpdated?.Invoke(this, EventArgs.Empty);
    }

    [LoggerMessage(LogLevel.Debug,
        "{infoMessage} | {currentFolder} | Total: {totalMessageCount}, Parsed: {parsedMessageCount}, Imported: {importedMessageCount}, Local Deleted: {localMessagesDeletedCount}, Remote Deleted: {remoteMessagesDeletedCount}")]
    partial void LogUpdate(string infoMessage, string currentFolder, int totalMessageCount, int parsedMessageCount,
        int importedMessageCount, int localMessagesDeletedCount, int remoteMessagesDeletedCount);
}