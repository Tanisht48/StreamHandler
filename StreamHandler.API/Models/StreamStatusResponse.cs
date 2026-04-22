namespace StreamHandler.API.Models;

public class StreamStatusResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public StreamFormat Format { get; set; }
    public StreamStatus Status { get; set; }
    public DateTime LastCheckedAt { get; set; }
    public DateTime? LastSeenLiveAt { get; set; }
    public string Tags { get; set; } = string.Empty;
    public string? Codec { get; set; }
    public string? Resolution { get; set; }
    public int? BitrateBps { get; set; }
}

public class StreamEventResponse
{
    public Guid Id { get; set; }
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}
