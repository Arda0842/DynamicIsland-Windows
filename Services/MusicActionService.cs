using System;
using System.Diagnostics;

namespace DynamicIsland.Services;

public static class MusicActionService
{
    public static void PlayOrSearchMusic(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;

        var cleanQuery = query.Trim();
        var encoded = Uri.EscapeDataString(cleanQuery);

        // Try Spotify protocol first
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = $"spotify:search:{encoded}",
                UseShellExecute = true
            };
            Process.Start(psi);
            return;
        }
        catch { }

        // Fallback: Open YouTube search in default browser (Brave, Chrome, etc.)
        try
        {
            var ytUrl = $"https://www.youtube.com/results?search_query={encoded}";
            Process.Start(new ProcessStartInfo
            {
                FileName = ytUrl,
                UseShellExecute = true
            });
        }
        catch { }
    }
}
