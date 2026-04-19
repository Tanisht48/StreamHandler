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
}
