using System.Diagnostics;

namespace SimpleMailArchiver.Data;

public class ImportManager(ILoggerFactory loggerFactory) : IAsyncDisposable
{
    private const string Fmt = @"hh\:mm\:ss";

    private readonly ImportProgress _currentProgress = new(loggerFactory);
    private readonly ILogger<ImportManager> _logger = loggerFactory.CreateLogger<ImportManager>();

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly Stopwatch _watch = new();
    private Task? _currentImportTask;

    public bool IsRunning { get; private set; }
    public IImportProgress CurrentProgress => _currentProgress;

    private CancellationTokenSource? Cts { get; set; }


    public async ValueTask DisposeAsync()
    {
        if (Cts is not null) await Cts.CancelAsync();

        if (_currentImportTask is not null)
            try
            {
                await _currentImportTask;
                _currentImportTask = null;
            }
            catch (OperationCanceledException)
            {
            }

        Cts?.Dispose();
    }

    public Task CancelAsync()
    {
        return Cts == null ? Task.CompletedTask : Cts.CancelAsync();
    }

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
                    new ProgressData($"Import was cancelled after {_watch.Elapsed.ToString(Fmt)}"));
            }
            catch (InvalidDataException ex)
            {
                _logger.LogInformation("Import failed due to invalid data: {Message}", ex.Message);
                _currentProgress.Report(new ProgressData("Import failed: Internal error: Hash Mismatch"));
            }
            catch (ArgumentException ex)
            {
                _currentProgress.Report(new ProgressData($"Import failed: {ex.Message}"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during the import operation");
                _currentProgress.Report(
                    new ProgressData($"Import failed with unexpected Error: {ex.Message}"));
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
}