namespace StreamHandler.API.Models;

public enum StreamFormat
{
    Unknown,
    HLS,
    RTSP,
    YouTube,
    HTTP
}

public enum StreamStatus
{
    Unknown,
    Live,
    Offline,
    Checking
}

public class Stream
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Url { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public StreamFormat Format { get; set; } = StreamFormat.Unknown;
    public StreamStatus Status { get; set; } = StreamStatus.Unknown;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastCheckedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenLiveAt { get; set; }

    // Tags (comma-separated)
    public string Tags { get; set; } = string.Empty;

    // Metadata from ffprobe
    public string? Codec { get; set; }
    public string? Resolution { get; set; }
    public int? BitrateBps { get; set; }

    public ICollection<StreamEvent> Events { get; set; } = [];
}

public class StreamEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StreamId { get; set; }
    public StreamStatus OldStatus { get; set; }
    public StreamStatus NewStatus { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public Stream Stream { get; set; } = null!;
}
