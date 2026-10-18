using System.Windows.Media.Imaging;

namespace DynamicIsland.Models;

public class MediaInfo
{
    public string Title { get; set; } = "Müzik Çalınmıyor";
    public string Artist { get; set; } = "Spotify veya Brave ile müzik açın";
    public string AppName { get; set; } = "Medya";
    public string AppId { get; set; } = "";
    public BitmapSource? Thumbnail { get; set; }
    public bool IsPlaying { get; set; } = false;
    public TimeSpan Position { get; set; } = TimeSpan.Zero;
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;

    public string SourceBadge
    {
        get
        {
            var lower = (AppId + " " + AppName).ToLowerInvariant();
            if (lower.Contains("spotify")) return "Spotify";
            if (lower.Contains("brave")) return "Brave";
            if (lower.Contains("chrome")) return "Chrome";
            if (lower.Contains("edge")) return "Edge";
            if (lower.Contains("firefox")) return "Firefox";
            if (lower.Contains("opera")) return "Opera";
            if (lower.Contains("zune") || lower.Contains("media player") || lower.Contains("groove")) return "Medya";
            return "Medya";
        }
    }

    public string SourceIcon
    {
        get
        {
            var lower = (AppId + " " + AppName).ToLowerInvariant();
            if (lower.Contains("spotify")) return "🟢";
            if (lower.Contains("brave")) return "🦁";
            if (lower.Contains("chrome")) return "🔴";
            if (lower.Contains("edge")) return "🌐";
            return "🎵";
        }
    }
}
