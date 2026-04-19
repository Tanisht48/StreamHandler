using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StreamHandler.API.Data;
using StreamHandler.API.Hubs;
using StreamHandler.API.Models;

namespace StreamHandler.API.Services;

public class StreamHealthService
{
    private readonly StreamDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<StreamHealthService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHubContext<StreamStatusHub> _hub;

    public StreamHealthService(
        StreamDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<StreamHealthService> logger,
        IConfiguration configuration,
        IHubContext<StreamStatusHub> hub)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
        _hub = hub;
    }

    public async Task CheckStreamHealthAsync(Guid streamId)
    {
        var stream = await _db.Streams.FindAsync(streamId);
        if (stream is null)
        {
            _logger.LogWarning("Stream {StreamId} not found during health check.", streamId);
            return;
        }

        stream.Status = StreamStatus.Checking;
        stream.LastCheckedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await PushStreamUpdateAsync(stream);

        var newStatus = stream.Format switch
        {
            StreamFormat.HTTP or StreamFormat.HLS => await CheckHttpAsync(stream.Url),
            StreamFormat.RTSP                     => await CheckFfprobeAsync(stream.Url),
            StreamFormat.YouTube                  => await CheckFfprobeAsync(stream.Url),
            _                                     => StreamStatus.Unknown
        };

        stream.Status = newStatus;
        stream.LastCheckedAt = DateTime.UtcNow;

        if (newStatus == StreamStatus.Live)
            stream.LastSeenLiveAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Stream '{Name}' ({StreamId}): {Status}", stream.Name, streamId, newStatus);

        await PushStreamUpdateAsync(stream);
        await PushSummaryUpdateAsync();
    }

    // ── HTTP / HLS ─────────────────────────────────────────────────────────
    private async Task<StreamStatus> CheckHttpAsync(string url)
    {
        var timeoutSeconds = _configuration.GetValue<int>("StreamHealth:HttpTimeoutSeconds", 5);

        try
        {
            var client = _httpClientFactory.CreateClient("HealthCheck");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            return response.IsSuccessStatusCode ? StreamStatus.Live : StreamStatus.Offline;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("HTTP health check failed for {Url}: {Message}", url, ex.Message);
            return StreamStatus.Offline;
        }
    }

    // ── RTSP / YouTube via ffprobe ─────────────────────────────────────────
    private async Task<StreamStatus> CheckFfprobeAsync(string url)
    {
        var ffprobePath = _configuration.GetValue<string>("StreamHealth:FfprobePath") ?? "ffprobe";
        var timeoutSeconds = _configuration.GetValue<int>("StreamHealth:HttpTimeoutSeconds", 5);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds + 2));

            var psi = new ProcessStartInfo
            {
                FileName               = ffprobePath,
                Arguments              = $"-v quiet -print_format json -show_streams -analyzeduration 2000000 -probesize 1000000 \"{url}\"",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };

            using var process = Process.Start(psi);
            if (process is null) return StreamStatus.Unknown;

            await process.WaitForExitAsync(cts.Token);

            if (process.ExitCode == 0)
            {
                _logger.LogDebug("ffprobe succeeded for {Url}", url);
                return StreamStatus.Live;
            }

            _logger.LogDebug("ffprobe returned exit code {Code} for {Url}", process.ExitCode, url);
            return StreamStatus.Offline;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("ffprobe timed out for {Url}", url);
            return StreamStatus.Offline;
        }
        catch (Exception ex) when (ex.Message.Contains("No such file") || ex.Message.Contains("cannot find"))
        {
            _logger.LogWarning("ffprobe not found at '{Path}'. Set StreamHealth:FfprobePath in config. Marking as Unknown.", ffprobePath);
            return StreamStatus.Unknown;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("ffprobe check failed for {Url}: {Message}", url, ex.Message);
            return StreamStatus.Offline;
        }
    }

    // ── SignalR push helpers ───────────────────────────────────────────────
    private Task PushStreamUpdateAsync(Models.Stream stream) =>
        _hub.Clients.All.SendAsync("StreamStatusUpdated", new
        {
            stream.Id,
            stream.Name,
            stream.Url,
            Format        = stream.Format.ToString(),
            Status        = stream.Status.ToString(),
            stream.LastCheckedAt,
            stream.LastSeenLiveAt
        });

    private async Task PushSummaryUpdateAsync()
    {
        var streams = await _db.Streams.ToListAsync();
        await _hub.Clients.All.SendAsync("StatusSummaryUpdated", new
        {
            TotalStreams        = streams.Count,
            Live               = streams.Count(s => s.Status == StreamStatus.Live),
            Offline            = streams.Count(s => s.Status == StreamStatus.Offline),
            Checking           = streams.Count(s => s.Status == StreamStatus.Checking),
            Unknown            = streams.Count(s => s.Status == StreamStatus.Unknown),
            LastHealthCheckRanAt = streams.Count > 0 ? streams.Max(s => s.LastCheckedAt) : (DateTime?)null
        });
    }
}
