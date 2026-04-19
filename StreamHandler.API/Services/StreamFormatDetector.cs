using StreamHandler.API.Models;

namespace StreamHandler.API.Services;

public class StreamFormatDetector
{
    public StreamFormat Detect(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return StreamFormat.Unknown;

        var lower = url.ToLowerInvariant();

        if (lower.Contains("m3u8") || lower.Contains("hls"))
            return StreamFormat.HLS;

        if (lower.StartsWith("rtsp://"))
            return StreamFormat.RTSP;

        if (lower.Contains("youtube.com") || lower.Contains("youtu.be"))
            return StreamFormat.YouTube;

        if (lower.StartsWith("http://") || lower.StartsWith("https://"))
            return StreamFormat.HTTP;

        return StreamFormat.Unknown;
    }
}
