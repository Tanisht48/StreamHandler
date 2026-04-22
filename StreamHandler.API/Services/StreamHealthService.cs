using System.Diagnostics;
using System.Text.Json;
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

    private static readonly Uri _placeholder = new("http://localhost");

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

        var previousStatus = stream.Status;

        stream.Status = StreamStatus.Checking;
        stream.LastCheckedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await PushStreamUpdateAsync(stream);

        StreamStatus newStatus;
        StreamMetadata? metadata = null;

        switch (stream.Format)
        {
            case StreamFormat.HTTP or StreamFormat.HLS:
                newStatus = await CheckHttpWithRetryAsync(stream.Url);
                break;
            case StreamFormat.RTSP or StreamFormat.YouTube:
                (newStatus, metadata) = await CheckFfprobeWithRetryAsync(stream.Url);
                break;
            default:
                newStatus = StreamStatus.Unknown;
                break;
        }

        stream.Status = newStatus;
        stream.LastCheckedAt = DateTime.UtcNow;

        if (newStatus == StreamStatus.Live)
            stream.LastSeenLiveAt = DateTime.UtcNow;

        if (metadata is not null)
        {
            stream.Codec = metadata.Codec;
            stream.Resolution = metadata.Resolution;
            stream.BitrateBps = metadata.BitrateBps;
        }

        // Log status change event
        if (previousStatus != StreamStatus.Checking && previousStatus != newStatus)
        {
            _db.StreamEvents.Add(new StreamEvent
            {
                StreamId = stream.Id,
                OldStatus = previousStatus,
                NewStatus = newStatus,
                OccurredAt = DateTime.UtcNow
            });

            // Push alert when stream goes offline from live
            if (previousStatus == StreamStatus.Live && newStatus == StreamStatus.Offline)
                await PushAlertAsync(stream, "went offline");
            else if (previousStatus == StreamStatus.Offline && newStatus == StreamStatus.Live)
                await PushAlertAsync(stream, "is back online");
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Stream '{Name}' ({StreamId}): {Status}", stream.Name, streamId, newStatus);

        await PushStreamUpdateAsync(stream);
        await PushSummaryUpdateAsync();
    }

    // ── HTTP / HLS with retry ──────────────────────────────────────────────
    private async Task<StreamStatus> CheckHttpWithRetryAsync(string url)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var result = await CheckHttpAsync(url);
            if (result == StreamStatus.Live) return StreamStatus.Live;
            if (attempt < maxAttempts)
                await Task.Delay(TimeSpan.FromSeconds(2));
        }
        return StreamStatus.Offline;
    }

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
            _logger.LogDebug("HTTP check failed for {Url}: {Message}", url, ex.Message);
            return StreamStatus.Offline;
        }
    }

    // ── RTSP / YouTube via ffprobe with retry + metadata ──────────────────
    private async Task<(StreamStatus, StreamMetadata?)> CheckFfprobeWithRetryAsync(string url)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var (status, meta) = await CheckFfprobeAsync(url);
            if (status == StreamStatus.Live) return (StreamStatus.Live, meta);
            if (status == StreamStatus.Unknown) return (StreamStatus.Unknown, null);
            if (attempt < maxAttempts)
                await Task.Delay(TimeSpan.FromSeconds(2));
        }
        return (StreamStatus.Offline, null);
    }

    private async Task<(StreamStatus, StreamMetadata?)> CheckFfprobeAsync(string url)
    {
        // Sanitize: must be a valid absolute URI with http/https/rtsp scheme
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
            parsed.Scheme is not ("http" or "https" or "rtsp"))
        {
            _logger.LogWarning("Rejecting unsafe URL for ffprobe: {Url}", url);
            return (StreamStatus.Offline, null);
        }

        var ffprobePath = _configuration.GetValue<string>("StreamHealth:FfprobePath") ?? "ffprobe";
        var timeoutSeconds = _configuration.GetValue<int>("StreamHealth:HttpTimeoutSeconds", 5);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds + 2));

            var psi = new ProcessStartInfo
            {
                FileName               = ffprobePath,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };

            // Pass URL as a separate argument to avoid shell injection
            psi.ArgumentList.Add("-v");
            psi.ArgumentList.Add("quiet");
            psi.ArgumentList.Add("-print_format");
            psi.ArgumentList.Add("json");
            psi.ArgumentList.Add("-show_streams");
            psi.ArgumentList.Add("-analyzeduration");
            psi.ArgumentList.Add("2000000");
            psi.ArgumentList.Add("-probesize");
            psi.ArgumentList.Add("1000000");
            psi.ArgumentList.Add(url);

            using var process = Process.Start(psi);
            if (process is null) return (StreamStatus.Unknown, null);

            var stdout = await process.StandardOutput.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);

            if (process.ExitCode != 0)
            {
                _logger.LogDebug("ffprobe exit {Code} for {Url}", process.ExitCode, url);
                return (StreamStatus.Offline, null);
            }

            var meta = ParseFfprobeMetadata(stdout);
            return (StreamStatus.Live, meta);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("ffprobe timed out for {Url}", url);
            return (StreamStatus.Offline, null);
        }
        catch (Exception ex) when (ex.Message.Contains("No such file") || ex.Message.Contains("cannot find"))
        {
            _logger.LogWarning("ffprobe not found at '{Path}'. Set StreamHealth:FfprobePath in config.", ffprobePath);
            return (StreamStatus.Unknown, null);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("ffprobe check failed for {Url}: {Message}", url, ex.Message);
            return (StreamStatus.Offline, null);
        }
    }

    private static StreamMetadata? ParseFfprobeMetadata(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("streams", out var streams)) return null;

            string? codec = null;
            string? resolution = null;
            int? bitrate = null;

            foreach (var s in streams.EnumerateArray())
            {
                if (codec is null && s.TryGetProperty("codec_name", out var cn))
                    codec = cn.GetString();

                if (resolution is null &&
                    s.TryGetProperty("width", out var w) &&
                    s.TryGetProperty("height", out var h))
                    resolution = $"{w.GetInt32()}x{h.GetInt32()}";

                if (bitrate is null && s.TryGetProperty("bit_rate", out var br) &&
                    int.TryParse(br.GetString(), out var brVal))
                    bitrate = brVal;
            }

            return new StreamMetadata { Codec = codec, Resolution = resolution, BitrateBps = bitrate };
        }
        catch
        {
            return null;
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
            stream.LastSeenLiveAt,
            stream.Tags,
            stream.Codec,
            stream.Resolution,
            stream.BitrateBps
        });

    private Task PushAlertAsync(Models.Stream stream, string message) =>
        _hub.Clients.All.SendAsync("StreamAlert", new
        {
            stream.Id,
            stream.Name,
            Message = $"'{stream.Name}' {message}",
            OccurredAt = DateTime.UtcNow
        });

    private async Task PushSummaryUpdateAsync()
    {
        var streams = await _db.Streams.ToListAsync();
        await _hub.Clients.All.SendAsync("StatusSummaryUpdated", new
        {
            TotalStreams          = streams.Count,
            Live                 = streams.Count(s => s.Status == StreamStatus.Live),
            Offline              = streams.Count(s => s.Status == StreamStatus.Offline),
            Checking             = streams.Count(s => s.Status == StreamStatus.Checking),
            Unknown              = streams.Count(s => s.Status == StreamStatus.Unknown),
            LastHealthCheckRanAt = streams.Count > 0 ? streams.Max(s => s.LastCheckedAt) : (DateTime?)null
        });
    }

    private sealed record StreamMetadata
    {
        public string? Codec { get; init; }
        public string? Resolution { get; init; }
        public int? BitrateBps { get; init; }
    }
}
