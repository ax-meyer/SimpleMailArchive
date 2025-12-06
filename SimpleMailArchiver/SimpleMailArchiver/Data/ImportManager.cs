using System.Diagnostics;

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
    event EventHandler? ProgressUpdated;
    int TotalMessageCount { get; }
    int ParsedMessageCount { get; }
    int ImportedMessageCount { get; }
    int LocalMessagesDeletedCount { get; }
    int RemoteMessagesDeletedCount { get; }

    string? CurrentFolder { get; }

    string? InfoMessage { get; }
}

public partial class ImportProgress(ILoggerFactory loggerFactory) : IImportProgress, IProgress<ProgressData>
{
    private readonly ILogger<ImportProgress> _logger = loggerFactory.CreateLogger<ImportProgress>();

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

    public event EventHandler? ProgressUpdated;
    public int TotalMessageCount { get; private set; }
    public int ParsedMessageCount { get; private set; }
    public int ImportedMessageCount { get; private set; }
    public int LocalMessagesDeletedCount { get; private set; }
    public int RemoteMessagesDeletedCount { get; private set; }

    public string? CurrentFolder { get; private set; }

    public string? InfoMessage { get; private set; }

    private void PublishUpdate()
    {
        LogUpdate(InfoMessage ?? string.Empty, CurrentFolder ?? string.Empty, TotalMessageCount, ParsedMessageCount,
            ImportedMessageCount, LocalMessagesDeletedCount, RemoteMessagesDeletedCount);
        ProgressUpdated?.Invoke(this, EventArgs.Empty);
    }

    [LoggerMessage(LogLevel.Information,
        "{infoMessage} | {currentFolder} | Total: {totalMessageCount}, Parsed: {parsedMessageCount}, Imported: {importedMessageCount}, Local Deleted: {localMessagesDeletedCount}, Remote Deleted: {remoteMessagesDeletedCount}")]
    partial void LogUpdate(string infoMessage, string currentFolder, int totalMessageCount, int parsedMessageCount,
        int importedMessageCount, int localMessagesDeletedCount, int remoteMessagesDeletedCount);
}

public class ImportManager(ILoggerFactory loggerFactory) : IAsyncDisposable
{
    private readonly ILogger<ImportManager> _logger = loggerFactory.CreateLogger<ImportManager>();
    private Task? _currentImportTask;

    public Task CancelAsync() => Cts == null ? Task.CompletedTask : Cts.CancelAsync();

    public bool IsRunning { get; private set; }
    public IImportProgress CurrentProgress => _currentProgress;

    private readonly ImportProgress _currentProgress = new(loggerFactory);
    private const string Fmt = @"hh\:mm\:ss";
    private readonly Stopwatch _watch = new();

    public void Start(Func<ImportProgress, CancellationToken, Task> importAction)
    {
        if (IsRunning) return;

        if (!_semaphore.Wait(TimeSpan.Zero)) return;
        IsRunning = true;
        Cts = new CancellationTokenSource();
        _currentImportTask = Task.Run(async () =>
        {
            try
            {
                _watch.Start();
                _currentProgress.Reset();
                _logger.LogInformation("Import operation started");
                await importAction(_currentProgress, Cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Import operation was cancelled");
                _currentProgress.Report(
                    new ProgressData(InfoMessage: $"Import was cancelled after {_watch.Elapsed.ToString(Fmt)}"));
            }
            catch (InvalidDataException ex)
            {
                _logger.LogInformation("Import failed due to invalid data: {Message}", ex.Message);
                _currentProgress.Report(new ProgressData(InfoMessage: "Import failed: Internal error: Hash Mismatch"));
            }
            catch (ArgumentException ex)
            {
                _currentProgress.Report(new ProgressData(InfoMessage: $"Import failed: {ex.Message}"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during the import operation");
                _currentProgress.Report(
                    new ProgressData(InfoMessage: $"Import failed with unexpected Error: {ex.Message}"));
            }
            finally
            {
                IsRunning = false;
                _semaphore.Release();
                Cts?.Dispose();
                Cts = null;
                _watch.Reset();
            }
        }, Cts.Token);
    }

    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private CancellationTokenSource? Cts { get; set; }


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
                await _currentImportTask;
                _currentImportTask = null;
            }
            catch (OperationCanceledException)
            {
            }
        }

        Cts?.Dispose();
    }
}