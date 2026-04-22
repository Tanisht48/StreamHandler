using Microsoft.EntityFrameworkCore;
using StreamHandler.API.Data;
using StreamHandler.API.Models;

namespace StreamHandler.API.Services;

public class StreamService
{
    private readonly StreamDbContext _db;
    private readonly StreamFormatDetector _formatDetector;
    private readonly StreamHealthService _healthService;
    private readonly ILogger<StreamService> _logger;

    public StreamService(
        StreamDbContext db,
        StreamFormatDetector formatDetector,
        StreamHealthService healthService,
        ILogger<StreamService> logger)
    {
        _db = db;
        _formatDetector = formatDetector;
        _healthService = healthService;
        _logger = logger;
    }

    public async Task<Models.Stream> AddStreamAsync(AddStreamRequest request)
    {
        var stream = new Models.Stream
        {
            Url = request.Url,
            Name = string.IsNullOrWhiteSpace(request.Name) ? request.Url : request.Name,
            Format = _formatDetector.Detect(request.Url),
            Status = StreamStatus.Unknown,
            Tags = request.Tags ?? string.Empty,
            AddedAt = DateTime.UtcNow,
            LastCheckedAt = DateTime.UtcNow
        };

        _db.Streams.Add(stream);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Added stream '{Name}' ({Id}), Format={Format}", stream.Name, stream.Id, stream.Format);

        // Trigger an immediate health check in the background
        _ = Task.Run(async () =>
        {
            try { await _healthService.CheckStreamHealthAsync(stream.Id); }
            catch (Exception ex) { _logger.LogError(ex, "Immediate health check failed for {Id}", stream.Id); }
        });

        return stream;
    }

    public async Task<List<Models.Stream>> GetAllStreamsAsync()
    {
        return await _db.Streams
            .OrderBy(s => s.Status == StreamStatus.Live ? 0 :
                          s.Status == StreamStatus.Checking ? 1 :
                          s.Status == StreamStatus.Offline ? 2 : 3)
            .ThenBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<Models.Stream?> GetStreamByIdAsync(Guid id)
    {
        return await _db.Streams.FindAsync(id);
    }

    public async Task<bool> DeleteStreamAsync(Guid id)
    {
        var stream = await _db.Streams.FindAsync(id);
        if (stream is null) return false;

        _db.Streams.Remove(stream);
        await _db.SaveChangesAsync();
        return true;
    }
}
