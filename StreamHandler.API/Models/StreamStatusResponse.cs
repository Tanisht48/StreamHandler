namespace StreamHandler.API.Models;

public class StreamStatusResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public StreamFormat Format { get; set; }
    public StreamStatus Status { get; set; }
    public DateTime LastCheckedAt { get; set; }
}
