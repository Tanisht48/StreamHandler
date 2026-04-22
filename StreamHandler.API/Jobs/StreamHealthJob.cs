using Hangfire;
using StreamHandler.API.Data;
using StreamHandler.API.Services;

namespace StreamHandler.API.Jobs;

public class StreamHealthJob
{
    private readonly StreamDbContext _db;
    private readonly StreamHealthService _healthService;
    private readonly ILogger<StreamHealthJob> _logger;

    public StreamHealthJob(
        StreamDbContext db,
        StreamHealthService healthService,
        ILogger<StreamHealthJob> logger)
    {
        _db = db;
        _healthService = healthService;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(IJobCancellationToken cancellationToken)
    {
        _logger.LogInformation("StreamHealthJob started at {Time}", DateTime.UtcNow);

        var streamIds = _db.Streams.Select(s => s.Id).ToList();
        _logger.LogInformation("Checking health for {Count} stream(s) in parallel.", streamIds.Count);

        // Run all checks in parallel, capped at 10 concurrent
        var semaphore = new SemaphoreSlim(10);
        var tasks = streamIds.Select(async id =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            await semaphore.WaitAsync();
            try
            {
                await _healthService.CheckStreamHealthAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed for stream {StreamId}", id);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        _logger.LogInformation("StreamHealthJob completed at {Time}", DateTime.UtcNow);
    }
}
