using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfImage = System.Windows.Controls.Image;

namespace DynamicIsland.Services;

public class GifPlayerService : IDisposable
{
    private readonly DispatcherTimer _timer = new();
    private readonly List<BitmapFrame> _frames = new();
    private readonly List<WpfImage> _targetImages = new();
    private int _currentFrame = 0;

    public bool IsPlaying => _timer.IsEnabled;
    public string? CurrentFilePath { get; private set; }

    public GifPlayerService()
    {
        _timer.Interval = TimeSpan.FromMilliseconds(100);
        _timer.Tick += (s, e) => NextFrame();
    }

    public void RegisterTarget(WpfImage image)
    {
        if (!_targetImages.Contains(image))
        {
            _targetImages.Add(image);
        }
    }

    public void UnregisterTarget(WpfImage image)
    {
        _targetImages.Remove(image);
    }

    public bool Load(string filePath)
    {
        Stop();
        _frames.Clear();
        CurrentFilePath = filePath;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            SetAllTargets(null);
            return false;
        }

        try
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".gif")
            {
                using var fs = File.OpenRead(filePath);
                var decoder = new GifBitmapDecoder(fs, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count > 0)
                {
                    foreach (var f in decoder.Frames)
                    {
                        f.Freeze();
                        _frames.Add(f);
                    }

                    _currentFrame = 0;
                    SetAllTargets(_frames[0]);

                    if (_frames.Count > 1)
                    {
                        _timer.Start();
                    }
                    return true;
                }
            }

            // Standard static image (.png, .jpg, .webp, .bmp)
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(filePath, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();

            SetAllTargets(bmp);
            return true;
        }
        catch
        {
            SetAllTargets(null);
            return false;
        }
    }

    private void NextFrame()
    {
        if (_frames.Count == 0) return;
        _currentFrame = (_currentFrame + 1) % _frames.Count;
        SetAllTargets(_frames[_currentFrame]);
    }

    private void SetAllTargets(BitmapSource? source)
    {
        foreach (var img in _targetImages)
        {
            img.Source = source;
        }
    }

    public void Stop()
    {
        _timer.Stop();
    }

    public void Resume()
    {
        if (_frames.Count > 1) _timer.Start();
    }

    public void Dispose()
    {
        Stop();
        _frames.Clear();
        _targetImages.Clear();
    }
}
