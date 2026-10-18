using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;
using DynamicIsland.Models;

namespace DynamicIsland.Services;

public class MediaService
{
    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;

    public event Action<MediaInfo>? MediaChanged;
    public event Action<TimeSpan, TimeSpan>? TimelineChanged;

    public MediaInfo CurrentInfo { get; private set; } = new MediaInfo();

    public async Task InitializeAsync()
    {
        try
        {
            _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_sessionManager != null)
            {
                _sessionManager.CurrentSessionChanged += SessionManager_CurrentSessionChanged;
                UpdateCurrentSession(_sessionManager.GetCurrentSession());
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MediaService init error: {ex.Message}");
        }
    }

    private void SessionManager_CurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args)
    {
        UpdateCurrentSession(sender.GetCurrentSession());
    }

    private void UpdateCurrentSession(GlobalSystemMediaTransportControlsSession? session)
    {
        if (_currentSession != null)
        {
            try
            {
                _currentSession.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
                _currentSession.TimelinePropertiesChanged -= Session_TimelinePropertiesChanged;
            }
            catch { }
        }

        _currentSession = session;

        if (_currentSession != null)
        {
            _currentSession.MediaPropertiesChanged += Session_MediaPropertiesChanged;
            _currentSession.PlaybackInfoChanged += Session_PlaybackInfoChanged;
            _currentSession.TimelinePropertiesChanged += Session_TimelinePropertiesChanged;
            _ = RefreshMediaInfoAsync();
        }
        else
        {
            CurrentInfo = new MediaInfo
            {
                Title = "Müzik Çalınmıyor",
                Artist = "Spotify veya Brave ile müzik açın",
                IsPlaying = false
            };
            MediaChanged?.Invoke(CurrentInfo);
        }
    }

    private void Session_MediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args)
    {
        _ = RefreshMediaInfoAsync();
    }

    private void Session_PlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args)
    {
        _ = RefreshMediaInfoAsync();
    }

    private void Session_TimelinePropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        TimelinePropertiesChangedEventArgs args)
    {
        try
        {
            var timeline = sender.GetTimelineProperties();
            CurrentInfo.Position = timeline.Position;
            CurrentInfo.Duration = timeline.EndTime - timeline.StartTime;
            TimelineChanged?.Invoke(CurrentInfo.Position, CurrentInfo.Duration);
        }
        catch { }
    }

    public async Task RefreshMediaInfoAsync()
    {
        if (_currentSession == null)
        {
            CurrentInfo = new MediaInfo();
            MediaChanged?.Invoke(CurrentInfo);
            return;
        }

        try
        {
            var mediaProps = await _currentSession.TryGetMediaPropertiesAsync();
            var playbackInfo = _currentSession.GetPlaybackInfo();
            var timeline = _currentSession.GetTimelineProperties();

            var appId = _currentSession.SourceAppUserModelId ?? "";
            var title = mediaProps?.Title;
            var artist = mediaProps?.Artist;

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(artist))
            {
                title = "Müzik Çalınmıyor";
                artist = "Spotify veya Brave ile müzik açın";
            }

            var isPlaying = playbackInfo?.PlaybackStatus ==
                            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            BitmapSource? thumbnail = null;
            if (mediaProps?.Thumbnail != null)
            {
                try
                {
                    using IRandomAccessStreamWithContentType stream = await mediaProps.Thumbnail.OpenReadAsync();
                    using Stream netStream = stream.AsStreamForRead();
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = netStream;
                    bmp.EndInit();
                    bmp.Freeze();
                    thumbnail = bmp;
                }
                catch
                {
                    // Thumbnail read failure fallback
                }
            }

            var info = new MediaInfo
            {
                Title = string.IsNullOrWhiteSpace(title) ? "Bilinmeyen Parça" : title,
                Artist = string.IsNullOrWhiteSpace(artist) ? "Bilinmeyen Sanatçı" : artist,
                AppId = appId,
                AppName = ParseAppName(appId),
                Thumbnail = thumbnail,
                IsPlaying = isPlaying,
                Position = timeline.Position,
                Duration = timeline.EndTime - timeline.StartTime
            };

            CurrentInfo = info;
            MediaChanged?.Invoke(info);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Refresh error: {ex.Message}");
        }
    }

    private string ParseAppName(string appId)
    {
        if (string.IsNullOrEmpty(appId)) return "Medya";
        var lower = appId.ToLowerInvariant();
        if (lower.Contains("spotify")) return "Spotify";
        if (lower.Contains("brave")) return "Brave";
        if (lower.Contains("chrome")) return "Chrome";
        if (lower.Contains("edge")) return "Edge";
        if (lower.Contains("firefox")) return "Firefox";
        if (lower.Contains("opera")) return "Opera";
        if (lower.Contains("zune") || lower.Contains("wmplayer") || lower.Contains("media player") || lower.Contains("groove")) return "Medya";

        var parts = appId.Split('!', '\\', '/');
        var name = parts[^1].Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
        if (name.Contains('.')) name = name.Split('.')[^1];
        return name.Length > 8 ? "Medya" : name;
    }

    public async Task<bool> TogglePlayPauseAsync()
    {
        if (_currentSession == null) return false;
        try
        {
            return await _currentSession.TryTogglePlayPauseAsync();
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SkipNextAsync()
    {
        if (_currentSession == null) return false;
        try
        {
            return await _currentSession.TrySkipNextAsync();
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SkipPreviousAsync()
    {
        if (_currentSession == null) return false;
        try
        {
            return await _currentSession.TrySkipPreviousAsync();
        }
        catch
        {
            return false;
        }
    }
}
