using System.Diagnostics.CodeAnalysis;

namespace SimpleMailArchiver.Data;


public record ProgressData(string? InfoMessage,
    string? CurrentFolder,
    int TotalMessageCount,
    int ParsedMessageCount,
    int ImportedMessageCount,
    int LocalMessagesDeletedCount,
    int RemoteMessagesDeletedCount);

public interface IImportProgress : IProgress<ProgressData>
{
    int TotalMessageCount { get; }
    int ParsedMessageCount { get; }
    int ImportedMessageCount { get; }
    int LocalMessagesDeletedCount { get; }
    int RemoteMessagesDeletedCount { get; }

    string? CurrentFolder { get; }

    string? InfoMessage { get; }
}
public partial class ImportProgress(ILoggerFactory loggerFactory) : IProgress<ProgressData>, IImportProgress
{
    private readonly ILogger<ImportProgress> _logger = loggerFactory.CreateLogger<ImportProgress>();
    public void Report(ProgressData value)
    {
        if (value.InfoMessage is not null) InfoMessage = value.InfoMessage;
        if (value.CurrentFolder is not null) CurrentFolder = value.CurrentFolder;
        if (value.TotalMessageCount >= 0) TotalMessageCount = value.TotalMessageCount;
        if (value.ParsedMessageCount >= 0) ParsedMessageCount = value.ParsedMessageCount;
        if (value.ImportedMessageCount >= 0) ImportedMessageCount = value.ImportedMessageCount;
        if (value.LocalMessagesDeletedCount >= 0) LocalMessagesDeletedCount = value.LocalMessagesDeletedCount;
        if (value.RemoteMessagesDeletedCount >= 0) RemoteMessagesDeletedCount = value.RemoteMessagesDeletedCount;

        LogUpdate(InfoMessage ?? string.Empty, CurrentFolder ?? string.Empty, TotalMessageCount, ParsedMessageCount, ImportedMessageCount, LocalMessagesDeletedCount, RemoteMessagesDeletedCount);
    }

    public int TotalMessageCount { get; private set; }
    public int ParsedMessageCount { get; private set; }
    public int ImportedMessageCount { get; private set; }
    public int LocalMessagesDeletedCount { get; private set; }
    public int RemoteMessagesDeletedCount { get; private set; }

    public string? CurrentFolder { get; private set; }

    public string? InfoMessage { get; private set; }
    
    [LoggerMessage(LogLevel.Information, "{infoMessage} | {currentFolder} | Total: {totalMessageCount}, Parsed: {parsedMessageCount}, Imported: {importedMessageCount}, Local Deleted: {localMessagesDeletedCount}, Remote Deleted: {remoteMessagesDeletedCount}")]
    partial void LogUpdate(string infoMessage, string currentFolder, int totalMessageCount, int parsedMessageCount, int importedMessageCount, int localMessagesDeletedCount, int remoteMessagesDeletedCount);
}

public class ImportExecutor(ILoggerFactory loggerFactory) : IAsyncDisposable
{
    private readonly ILogger<ImportExecutor> _logger = loggerFactory.CreateLogger<ImportExecutor>();
    private Task? _currentImportTask;

    public bool IsRunning { get; private set; }
    public IImportProgress? CurrentProgress { get; private set; }
    
    public void Start(Func<ImportProgress, CancellationToken, Task> importAction)
    {
        if (IsRunning) return;
        
        IsRunning = true;
        Cts = new CancellationTokenSource();
        _currentImportTask = Task.Run(async () =>
        {
            var progress = new ImportProgress(loggerFactory);
            CurrentProgress = progress;
            try
            {
                await importAction(progress, Ct!.Value);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Import operation was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during the import operation");
            }
            finally
            {
                IsRunning = false;
                Cts?.Dispose();
                Cts = null;
            }
        }, Ct);
    }
    

    [NotNullIfNotNull(nameof(Cts))]
    public CancellationToken? Ct => Cts?.Token;
    public CancellationTokenSource? Cts { get; set; }
    

    public async ValueTask DisposeAsync()
    {
        if (Cts is not null)
        {
            await Cts.CancelAsync();
        }

        if (_currentImportTask is not null)
        {
            try
            {
                await  _currentImportTask;
                _currentImportTask = null;
            }
            catch (OperationCanceledException)
            {
            }
        }
        Cts?.Dispose();
    }
}