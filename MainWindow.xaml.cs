using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using WpfImage = System.Windows.Controls.Image;
using WpfButton = System.Windows.Controls.Button;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfDataObject = System.Windows.DataObject;
using WpfDataFormats = System.Windows.DataFormats;
using WpfDragDropEffects = System.Windows.DragDropEffects;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DynamicIsland.Models;
using DynamicIsland.Services;
using Microsoft.Win32;

namespace DynamicIsland;

public partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const byte VK_VOLUME_MUTE = 0xAD;
    private const byte VK_VOLUME_DOWN = 0xAE;
    private const byte VK_VOLUME_UP = 0xAF;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOPMOST = 0x00000008;

    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private readonly MediaService _mediaService = new();
    private readonly StopwatchService _stopwatchService = new();
    private readonly HotKeyService _hotKeyService = new();
    private readonly GeminiService _geminiService = new();
    private readonly AudioRecorderService _audioRecorder = new();
    private readonly TranslateService _translateService;
    private string _translateSourceLang = "tr";
    private string _translateTargetLang = "en";

    private readonly SystemService _systemService = new();
    private readonly FileShelfService _fileShelfService = new();
    private readonly QuickAppsService _quickAppsService = new();

    private readonly DispatcherTimer _collapseDelayTimer = new();
    private readonly DispatcherTimer _eqAnimationTimer = new();
    private readonly DispatcherTimer _topmostHeartbeatTimer = new();
    private readonly DispatcherTimer _clockTimer = new();
    private readonly DispatcherTimer _rgbTimer = new();
    private readonly Random _random = new();

    private WinEventDelegate? _winEventDelegate;
    private IntPtr _winEventHook = IntPtr.Zero;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private System.Drawing.Icon? _trayIconInstance;

    private bool _isExpanded = false;
    private bool _isPinned = false;
    private bool _isHovering = false;
    private double _currentHue = 0.0;
    private string _currentTheme = "oled";
    private double _currentOpacity = 1.0;

    private string _keyToggle = "Alt+D";
    private string _keyMouse = "Alt+M";
    private string _keyVoice = "Alt+V";
    private string _keyPlay = "Alt+P";
    private string _keyMcAfk = "Alt+K";
    private string _keyCrosshair = "Alt+X";

    private readonly MinecraftAfkService _mcAfkService = new();
    private readonly Cs2GsiService _cs2GsiService = new();
    private CrosshairWindow? _crosshairWindow;
    private bool _isCrosshairEnabled = false;
    private string _crosshairStyle = "dot";
    private double _crosshairSize = 3.0;
    private string _crosshairColorHex = "#00FF66";
    private double _crosshairOpacity = 1.0;
    private int _crosshairOffsetX = 0;
    private int _crosshairOffsetY = 0;

    private readonly GifPlayerService _gifPlayer = new();
    private string _currentAnimeTheme = "none";
    private string _customImagePath = "";
    private bool _showMascot = true;
    private bool _showBgImage = false;
    private bool _mascotDance = true;
    private double _mascotBobY = 0.0;

    public enum IslandPosition
    {
        TopLeft,
        TopCenter,
        TopRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

    private IslandPosition _islandPosition = IslandPosition.TopCenter;

    private readonly DispatcherTimer _hoverExpandTimer = new();
    private const double MINI_WIDTH = 38;
    private const double MINI_HEIGHT = 38;
    private const double COMPACT_WIDTH = 280;
    private const double COMPACT_HEIGHT = 38;
    private const double EXPANDED_WIDTH = 410;
    private const double EXPANDED_HEIGHT = 224;

    private readonly string _configPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DynamicIsland",
        "config.json"
    );

    public MainWindow()
    {
        InitializeComponent();
        _translateService = new TranslateService(_geminiService);
        _mcAfkService.StateChanged += OnMcAfkStateChanged;

        LapItemsControl.ItemsSource = _stopwatchService.Laps;

        // Auto-collapse delay timer (generous 700ms grace period so it doesn't snap shut on cursor slip)
        _collapseDelayTimer.Interval = TimeSpan.FromMilliseconds(700);
        _collapseDelayTimer.Tick += (s, e) =>
        {
            _collapseDelayTimer.Stop();
            if (!_isHovering && !_isPinned && _isExpanded && !_audioRecorder.IsRecording && PanelIntro.Visibility != Visibility.Visible)
            {
                CollapseIsland();
            }
        };

        // Hover-expand timer (snappy 80ms debounce so it opens effortlessly)
        _hoverExpandTimer.Interval = TimeSpan.FromMilliseconds(80);
        _hoverExpandTimer.Tick += (s, e) =>
        {
            _hoverExpandTimer.Stop();
            if (_isHovering && !_isExpanded && !_audioRecorder.IsRecording && PanelIntro.Visibility != Visibility.Visible)
            {
                ExpandIsland();
            }
        };

        // Mini animated equalizer bars simulation
        _eqAnimationTimer.Interval = TimeSpan.FromMilliseconds(120);
        _eqAnimationTimer.Tick += EqAnimationTimer_Tick;

        // Fast Topmost heartbeat timer (runs every 350ms to enforce topmost even in borderless games)
        _topmostHeartbeatTimer.Interval = TimeSpan.FromMilliseconds(350);
        _topmostHeartbeatTimer.Tick += (s, e) => EnsureTopmost();
        _topmostHeartbeatTimer.Start();

        // 1-second clock HUD timer for compact mode
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += (s, e) => UpdateCompactHud();
        _clockTimer.Start();

        // RGB Chroma breathing animation timer
        _rgbTimer.Interval = TimeSpan.FromMilliseconds(35);
        _rgbTimer.Tick += RgbTimer_Tick;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;

        // WS_EX_TOOLWINDOW prevents minimizing when full-screen games gain focus and hides from Alt+Tab
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);

        // Set up foreground window hook: Whenever a game takes focus, stay on top!
        try
        {
            _winEventDelegate = WinEventCallback;
            _winEventHook = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _winEventDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
        }
        catch { }

        EnsureTopmost();
    }

    private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // When any game or window gains focus, immediately reinforce Topmost
        Dispatcher.BeginInvoke(new Action(EnsureTopmost));
    }

    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        // Handle resolution change when launching games (e.g. 4:3 stretched or resolution switch)
        Dispatcher.Invoke(() =>
        {
            ApplyWindowPosition();
            EnsureTopmost();
            _crosshairWindow?.EnsureCenterPosition();
        });
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _gifPlayer.RegisterTarget(ImgIslandBackground);
        _gifPlayer.RegisterTarget(ImgMascotPill);
        _gifPlayer.RegisterTarget(ImgMascotMini);

        LoadConfig();
        InitTrayIcon();
        UpdatePositionButtonsUi();

        // Hotkey registration
        try
        {
            _hotKeyService.Register(this, _keyToggle, _keyMouse, _keyVoice, _keyPlay, _keyMcAfk, _keyCrosshair);
            _hotKeyService.ToggleRequested += () => Dispatcher.Invoke(ToggleExpand);
            _hotKeyService.PlayPauseRequested += () => Dispatcher.Invoke(async () => await _mediaService.TogglePlayPauseAsync());
            _hotKeyService.VoiceRequested += () => Dispatcher.Invoke(async () => await ToggleVoiceRecordingAsync());
            _hotKeyService.MouseTeleportRequested += () => Dispatcher.Invoke(TeleportMouseToIsland);
            _hotKeyService.McAfkRequested += () => Dispatcher.Invoke(ToggleMcAfk);
            _hotKeyService.CrosshairRequested += () => Dispatcher.Invoke(ToggleCrosshair);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Hotkey error: {ex.Message}");
        }

        // Stopwatch events
        _stopwatchService.TimeUpdated += OnStopwatchTimeUpdated;
        _stopwatchService.CountdownCompleted += OnCountdownCompleted;

        // Media service events
        _mediaService.MediaChanged += OnMediaChanged;
        _mediaService.TimelineChanged += OnTimelineChanged;

        await _mediaService.InitializeAsync();

        // System Stats & DynamicWin features
        _systemService.StatsUpdated += OnSystemStatsUpdated;
        _systemService.Start();
        _fileShelfService.Items.CollectionChanged += (s, ev) => Dispatcher.Invoke(RenderFileShelfItems);
        _quickAppsService.Items.CollectionChanged += (s, ev) => Dispatcher.Invoke(RenderQuickApps);
        RenderFileShelfItems();
        RenderQuickApps();

        // If music is not playing on startup and intro is not showing, start as mini circle
        if (!_mediaService.CurrentInfo.IsPlaying && PanelIntro.Visibility != Visibility.Visible)
        {
            CompactView.Visibility = Visibility.Collapsed;
            MiniCircleView.Visibility = Visibility.Visible;
            Width = 84;
            Height = 56;
            ApplyWindowPosition();
            IslandBorder.Width = MINI_WIDTH;
            IslandBorder.Height = MINI_HEIGHT;
            IslandBorder.CornerRadius = new CornerRadius(18);
        }
        else if (!_isExpanded && PanelIntro.Visibility != Visibility.Visible)
        {
            MiniCircleView.Visibility = Visibility.Collapsed;
            CompactView.Visibility = Visibility.Visible;
            Width = 310;
            Height = 56;
            ApplyWindowPosition();
            IslandBorder.Width = COMPACT_WIDTH;
            IslandBorder.Height = COMPACT_HEIGHT;
            IslandBorder.CornerRadius = new CornerRadius(18);
        }

        // Check if intro should be shown on first run
        CheckFirstRunIntro();

        UpdateCrosshairUi();
        if (_isCrosshairEnabled)
        {
            EnableCrosshair();
        }

        // CS2 GSI Service events
        _cs2GsiService.GsiStatusChanged += status => Dispatcher.Invoke(() =>
        {
            if (TxtCs2Status != null) TxtCs2Status.Text = status;
            if (DotCs2Status != null) DotCs2Status.Fill = status.Contains("🟢") 
                ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94)) 
                : new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
        });

        _cs2GsiService.BombPlanted += () => Dispatcher.Invoke(OnCs2BombPlanted);
        _cs2GsiService.BombTick += (time, canWithout, canWith, kitsizDl, kitliDl) => Dispatcher.Invoke(() =>
        {
            UpdateCs2BombUi(time, canWithout, canWith, kitsizDl, kitliDl);
        });
        _cs2GsiService.BombDefused += () => Dispatcher.Invoke(OnCs2BombDefused);
        _cs2GsiService.BombExploded += () => Dispatcher.Invoke(OnCs2BombExploded);
        _cs2GsiService.BombReset += () => Dispatcher.Invoke(ResetCs2BombUi);

        _cs2GsiService.StartListener();

        // Check & Auto-install GSI config file if CS2 exists
        if (!Cs2GsiService.IsGsiConfigInstalled())
        {
            Cs2GsiService.EnsureGsiConfigInstalled(out _);
        }
        UpdateCs2CfgStatusUi();

        EnsureTopmost();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _crosshairWindow?.Close();
        _crosshairWindow = null;
        _gifPlayer.Dispose();
        _mcAfkService.Dispose();
        _cs2GsiService.Dispose();
        _hotKeyService.Dispose();
        _systemService.Dispose();
        _notifyIcon?.Dispose();
        _trayIconInstance?.Dispose();
        _topmostHeartbeatTimer.Stop();
        _clockTimer.Stop();
        _rgbTimer.Stop();

        if (_winEventHook != IntPtr.Zero)
        {
            UnhookWinEvent(_winEventHook);
        }

        SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
    }

    public void EnsureTopmost()
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                // Unconditionally re-assert topmost window position so games can never hide the island
                SetWindowPos(handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }

            if (!Topmost)
            {
                Topmost = true;
            }

            if (Visibility != Visibility.Visible)
            {
                Visibility = Visibility.Visible;
            }
        }
        catch { }
    }

    public void TeleportMouseToIsland()
    {
        if (!_isExpanded)
        {
            ExpandIsland();
        }
        EnsureTopmost();

        try
        {
            var screenPoint = IslandBorder.PointToScreen(new System.Windows.Point(IslandBorder.ActualWidth / 2, IslandBorder.ActualHeight / 2));
            SetCursorPos((int)screenPoint.X, (int)screenPoint.Y);
        }
        catch { }
    }

    private System.Drawing.Icon CreateDynamicIslandTrayIcon()
    {
        try
        {
            const int sz = 32;
            using var bmp = new System.Drawing.Bitmap(sz, sz, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.Clear(System.Drawing.Color.Transparent);

                // Dış kart: Yuvarlatılmış şık koyu kapsül
                const int r = 7;
                using var pathBg = new System.Drawing.Drawing2D.GraphicsPath();
                pathBg.AddArc(1, 1, r, r, 180, 90);
                pathBg.AddArc(30 - r, 1, r, r, 270, 90);
                pathBg.AddArc(30 - r, 30 - r, r, r, 0, 90);
                pathBg.AddArc(1, 30 - r, r, r, 90, 90);
                pathBg.CloseFigure();

                // Derin OLED siyah arka plan
                using (var brushBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 12, 12, 18)))
                    g.FillPath(brushBg, pathBg);

                // Parlayan Canlı Neon Camgöbeği (Cyan) çerçeve çizgisi
                using (var penBorder = new System.Drawing.Pen(System.Drawing.Color.FromArgb(255, 56, 189, 248), 1.5f))
                    g.DrawPath(penBorder, pathBg);

                // Ortadaki Apple Dinamik Ada siyah hapı
                using var pillPath = new System.Drawing.Drawing2D.GraphicsPath();
                pillPath.AddArc(4, 10, 10, 10, 90, 180);
                pillPath.AddArc(18, 10, 10, 10, 270, 180);
                pillPath.CloseFigure();

                using (var brushPill = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 0, 0, 0)))
                    g.FillPath(brushPill, pillPath);

                // Sol tarafta parlayan yeşil durum / kamera göstergesi (Apple Dynamic Island dot)
                using (var brushDot = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 34, 197, 94)))
                    g.FillEllipse(brushDot, 6, 13, 4, 4);

                // Sağ tarafta mini müzik ekolayzer dalgaları
                using (var bCyan = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 56, 189, 248)))
                    g.FillRectangle(bCyan, 16, 12, 2, 6);
                using (var bPink = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 244, 114, 182)))
                    g.FillRectangle(bPink, 19, 10, 2, 10);
                using (var bYellow = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 251, 191, 36)))
                    g.FillRectangle(bYellow, 22, 13, 2, 4);
            }

            // Pencere simgesini de senkronize et
            try
            {
                using var ms = new MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                var bmpImage = new BitmapImage();
                bmpImage.BeginInit();
                bmpImage.CacheOption = BitmapCacheOption.OnLoad;
                bmpImage.StreamSource = ms;
                bmpImage.EndInit();
                bmpImage.Freeze();
                this.Icon = bmpImage;
            }
            catch { }

            IntPtr hIcon = bmp.GetHicon();
            _trayIconInstance = System.Drawing.Icon.FromHandle(hIcon);
            return _trayIconInstance;
        }
        catch
        {
            return System.Drawing.SystemIcons.Application;
        }
    }

    private void InitTrayIcon()
    {
        try
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Text = "Dinamik Ada (Dynamic Island)",
                Icon = CreateDynamicIslandTrayIcon(),
                Visible = true
            };

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            contextMenu.Items.Add($"Genişlet / Daralt ({_keyToggle})", null, (s, e) => Dispatcher.Invoke(ToggleExpand));
            contextMenu.Items.Add($"Fareyi Adaya Işınla ({_keyMouse})", null, (s, e) => Dispatcher.Invoke(TeleportMouseToIsland));
            contextMenu.Items.Add($"Sesli Komut / Asistan ({_keyVoice})", null, (s, e) => Dispatcher.Invoke(async () => await ToggleVoiceRecordingAsync()));
            contextMenu.Items.Add($"⚔️ Minecraft AFK Başlat / Durdur ({_keyMcAfk})", null, (s, e) => Dispatcher.Invoke(ToggleMcAfk));
            contextMenu.Items.Add($"🎯 Ekran Ortası Crosshair ({_keyCrosshair})", null, (s, e) => Dispatcher.Invoke(ToggleCrosshair));
            
            var posMenu = new System.Windows.Forms.ToolStripMenuItem("📍 Konum (6 Köşe)");
            posMenu.DropDownItems.Add("↖️ Sol Üst", null, (s, e) => Dispatcher.Invoke(() => SetIslandPosition(IslandPosition.TopLeft)));
            posMenu.DropDownItems.Add("⬆️ Üst Orta", null, (s, e) => Dispatcher.Invoke(() => SetIslandPosition(IslandPosition.TopCenter)));
            posMenu.DropDownItems.Add("↗️ Sağ Üst", null, (s, e) => Dispatcher.Invoke(() => SetIslandPosition(IslandPosition.TopRight)));
            posMenu.DropDownItems.Add(new System.Windows.Forms.ToolStripSeparator());
            posMenu.DropDownItems.Add("↙️ Sol Alt", null, (s, e) => Dispatcher.Invoke(() => SetIslandPosition(IslandPosition.BottomLeft)));
            posMenu.DropDownItems.Add("⬇️ Alt Orta", null, (s, e) => Dispatcher.Invoke(() => SetIslandPosition(IslandPosition.BottomCenter)));
            posMenu.DropDownItems.Add("↘️ Sağ Alt", null, (s, e) => Dispatcher.Invoke(() => SetIslandPosition(IslandPosition.BottomRight)));
            contextMenu.Items.Add(posMenu);

            contextMenu.Items.Add("🎯 Üst Ortaya Sıfırla", null, (s, e) => Dispatcher.Invoke(ResetPosition));
            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            contextMenu.Items.Add("Çıkış", null, (s, e) => Dispatcher.Invoke(() =>
            {
                _notifyIcon.Visible = false;
                System.Windows.Application.Current.Shutdown();
            }));

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => Dispatcher.Invoke(ToggleExpand);
        }
        catch { }
    }

    public void SetIslandPosition(IslandPosition pos)
    {
        _islandPosition = pos;
        ApplyWindowPosition();
        SavePosition();
        UpdatePositionButtonsUi();
    }

    public void ResetPosition()
    {
        SetIslandPosition(IslandPosition.TopCenter);
    }

    public void ApplyWindowPosition()
    {
        double minLeft = SystemParameters.VirtualScreenLeft;
        double screenWidth = SystemParameters.VirtualScreenWidth;
        double screenRight = minLeft + screenWidth;
        double minTop = SystemParameters.VirtualScreenTop;
        double screenHeight = SystemParameters.VirtualScreenHeight;
        double screenBottom = minTop + screenHeight;

        switch (_islandPosition)
        {
            case IslandPosition.TopLeft:
                if (IslandBorder != null)
                {
                    IslandBorder.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                    IslandBorder.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                    IslandBorder.Margin = _isExpanded ? new Thickness(10, 6, 0, 0) : new Thickness(14, 6, 0, 0);
                }
                Left = minLeft;
                Top = minTop;
                break;

            case IslandPosition.TopCenter:
                if (IslandBorder != null)
                {
                    IslandBorder.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                    IslandBorder.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                    IslandBorder.Margin = new Thickness(0, 6, 0, 0);
                }
                Left = minLeft + (screenWidth - Width) / 2.0;
                Top = minTop;
                break;

            case IslandPosition.TopRight:
                if (IslandBorder != null)
                {
                    IslandBorder.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                    IslandBorder.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                    IslandBorder.Margin = _isExpanded ? new Thickness(0, 6, 10, 0) : new Thickness(0, 6, 14, 0);
                }
                Left = screenRight - Width;
                Top = minTop;
                break;

            case IslandPosition.BottomLeft:
                if (IslandBorder != null)
                {
                    IslandBorder.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                    IslandBorder.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
                    IslandBorder.Margin = _isExpanded ? new Thickness(10, 0, 0, 8) : new Thickness(14, 0, 0, 8);
                }
                Left = minLeft;
                Top = screenBottom - Height;
                break;

            case IslandPosition.BottomCenter:
                if (IslandBorder != null)
                {
                    IslandBorder.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                    IslandBorder.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
                    IslandBorder.Margin = new Thickness(0, 0, 0, 8);
                }
                Left = minLeft + (screenWidth - Width) / 2.0;
                Top = screenBottom - Height;
                break;

            case IslandPosition.BottomRight:
                if (IslandBorder != null)
                {
                    IslandBorder.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                    IslandBorder.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
                    IslandBorder.Margin = _isExpanded ? new Thickness(0, 0, 10, 8) : new Thickness(0, 0, 14, 8);
                }
                Left = screenRight - Width;
                Top = screenBottom - Height;
                break;
        }

        EnsureTopmost();
    }

    // --- Equalizer Animation ---
    private void EqAnimationTimer_Tick(object? sender, EventArgs e)
    {
        if (_mediaService.CurrentInfo.IsPlaying)
        {
            EqBar1.Height = _random.Next(3, 11);
            EqBar2.Height = _random.Next(6, 14);
            EqBar3.Height = _random.Next(4, 12);

            if (_mascotDance)
            {
                _mascotBobY = _mascotBobY == 0 ? -2.5 : 0;
                if (TransMascotBob != null) TransMascotBob.Y = _mascotBobY;
            }
        }
        else
        {
            EqBar1.Height = 3;
            EqBar2.Height = 3;
            EqBar3.Height = 3;
            if (TransMascotBob != null) TransMascotBob.Y = 0;
        }
    }

    private string FormatSongTitle(MediaInfo info)
    {
        if (string.IsNullOrWhiteSpace(info.Title) || info.Title == "Müzik Çalınmıyor")
        {
            return "Müzik Çalınmıyor";
        }

        if (string.IsNullOrWhiteSpace(info.Artist) || info.Artist.Contains("Spotify") || info.Artist == "Bilinmeyen Sanatçı")
        {
            return info.Title;
        }

        return $"{info.Title} • {info.Artist}";
    }

    // --- Live Compact HUD Clock & Status ---
    private void UpdateCompactHud()
    {
        if (_audioRecorder.IsRecording || _isExpanded) return;

        if (_cs2GsiService.IsBombPlanted)
        {
            TxtCompactTitle.Text = "💣 C4 KURULDU";
            return;
        }

        var info = _mediaService.CurrentInfo;
        if (string.IsNullOrWhiteSpace(info.Title) || info.Title == "Müzik Çalınmıyor")
        {
            TxtCompactTitle.Text = "Müzik Çalınmıyor";
            if (ImgMiniCover.Visibility != Visibility.Visible)
            {
                TxtMiniIcon.Text = "🎵";
            }
        }
        else
        {
            // Always show the song name even when paused, never wipe out with "Müzik Yok"
            var formatted = FormatSongTitle(info);
            if (TxtCompactTitle.Text != formatted)
            {
                TxtCompactTitle.Text = formatted;
            }
        }
    }

    // --- Media Service Handlers ---
    private void OnMediaChanged(MediaInfo info)
    {
        Dispatcher.Invoke(() =>
        {
            // Compact view updates
            if (!_audioRecorder.IsRecording)
            {
                TxtCompactTitle.Text = FormatSongTitle(info);
                TxtCompactTitle.ToolTip = string.IsNullOrWhiteSpace(info.Artist)
                    ? info.Title
                    : $"{info.Title}\n{info.Artist} ({info.SourceBadge})";
            }

            if (BtnCompactPlay != null)
            {
                BtnCompactPlay.Content = info.IsPlaying ? "❚❚" : "▶";
            }

            if (info.Thumbnail != null)
            {
                ImgMiniCover.Source = info.Thumbnail;
                ImgMiniCover.Visibility = Visibility.Visible;
                TxtMiniIcon.Visibility = Visibility.Collapsed;

                ImgLargeCover.Source = info.Thumbnail;
                ImgLargeCover.Visibility = Visibility.Visible;
                TxtLargeCoverPlaceholder.Visibility = Visibility.Collapsed;

                ImgDotCover.Source = info.Thumbnail;
                ImgDotCover.Visibility = Visibility.Visible;
                TxtDotIcon.Visibility = Visibility.Collapsed;
            }
            else
            {
                ImgMiniCover.Visibility = Visibility.Collapsed;
                TxtMiniIcon.Visibility = Visibility.Visible;
                TxtMiniIcon.Text = info.SourceIcon;

                ImgLargeCover.Visibility = Visibility.Collapsed;
                TxtLargeCoverPlaceholder.Visibility = Visibility.Visible;
                TxtLargeCoverPlaceholder.Text = info.SourceIcon;

                ImgDotCover.Visibility = Visibility.Collapsed;
                TxtDotIcon.Visibility = Visibility.Visible;
                TxtDotIcon.Text = info.SourceIcon;
            }

            // Expanded view updates (Matches TikTok NotchNook "Error / Paused" style)
            TxtExpandedTitle.Text = string.IsNullOrWhiteSpace(info.Title) ? "Müzik Çalınmıyor" : info.Title;
            TxtExpandedArtist.Text = info.IsPlaying 
                ? (string.IsNullOrWhiteSpace(info.Artist) ? "Oynatılıyor" : info.Artist) 
                : "Duraklatıldı (Paused)";
            TxtExpandedAppName.Text = info.SourceBadge;
            TxtExpandedAppIcon.Text = info.SourceIcon;
            BtnPlayPause.Content = info.IsPlaying ? "❚❚" : "▶";

            if (BadgeMediaSource != null)
            {
                if (info.SourceBadge.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
                {
                    BadgeMediaSource.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 215, 96));
                    BadgeMediaSource.Visibility = Visibility.Visible;
                }
                else if (info.SourceBadge.Contains("Brave", StringComparison.OrdinalIgnoreCase))
                {
                    BadgeMediaSource.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 84, 43));
                    BadgeMediaSource.Visibility = Visibility.Visible;
                }
                else if (info.SourceBadge.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
                {
                    BadgeMediaSource.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(66, 133, 244));
                    BadgeMediaSource.Visibility = Visibility.Visible;
                }
                else if (!string.IsNullOrEmpty(info.Title) && info.Title != "Müzik Çalınmıyor")
                {
                    BadgeMediaSource.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(38, 40, 52));
                    BadgeMediaSource.Visibility = Visibility.Visible;
                }
                else
                {
                    BadgeMediaSource.Visibility = Visibility.Collapsed;
                }
            }

            // Equalizer animation state
            if (info.IsPlaying && !_eqAnimationTimer.IsEnabled)
            {
                _eqAnimationTimer.Start();
            }
            else if (!info.IsPlaying && _eqAnimationTimer.IsEnabled)
            {
                _eqAnimationTimer.Stop();
                EqBar1.Height = 3;
                EqBar2.Height = 3;
                EqBar3.Height = 3;
            }

            // Automatic Mini Circle vs Compact Pill transition when collapsed
            if (!_isExpanded && !_audioRecorder.IsRecording && PanelIntro.Visibility != Visibility.Visible)
            {
                if (info.IsPlaying)
                {
                    MiniCircleView.Visibility = Visibility.Collapsed;
                    CompactView.Visibility = Visibility.Visible;
                    Width = 330;
                    Height = 56;
                    ApplyWindowPosition();
                    AnimateIsland(COMPACT_WIDTH, COMPACT_HEIGHT, new CornerRadius(19));
                }
                else
                {
                    CompactView.Visibility = Visibility.Collapsed;
                    MiniCircleView.Visibility = Visibility.Visible;
                    Width = 84;
                    Height = 56;
                    ApplyWindowPosition();
                    AnimateIsland(MINI_WIDTH, MINI_HEIGHT, new CornerRadius(19));
                }
            }
        });
    }

    private void OnTimelineChanged(TimeSpan pos, TimeSpan dur)
    {
        Dispatcher.Invoke(() =>
        {
            if (dur.TotalSeconds > 0)
            {
                PbMedia.Maximum = dur.TotalSeconds;
                PbMedia.Value = pos.TotalSeconds;
                if (TxtMediaPos != null) TxtMediaPos.Text = pos.ToString(@"m\:ss");
                if (TxtMediaDur != null) TxtMediaDur.Text = dur.ToString(@"m\:ss");
            }
        });
    }

    // --- Stopwatch & Timer Handlers ---
    private void OnStopwatchTimeUpdated(string formatted, bool isRunning)
    {
        Dispatcher.Invoke(() =>
        {
            TxtStopwatchDisplay.Text = formatted;
            TxtCountdownDisplay.Text = formatted;

            if (isRunning)
            {
                MiniTimerBadge.Visibility = Visibility.Visible;
                TxtMiniTimer.Text = formatted;
            }
            else if (formatted == "00:00.0")
            {
                MiniTimerBadge.Visibility = Visibility.Collapsed;
            }
        });
    }

    private void OnCountdownCompleted()
    {
        Dispatcher.Invoke(() =>
        {
            MiniTimerBadge.Visibility = Visibility.Visible;
            TxtMiniTimer.Text = "SÜRE DOLDU!";
            ExpandIsland();
            try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
        });
    }

    // --- Island Expand / Collapse Animations ---
    public void ToggleExpand()
    {
        if (_isExpanded)
            CollapseIsland();
        else
            ExpandIsland();
    }

    public void ExpandIsland()
    {
        if (_isExpanded) return;
        _isExpanded = true;
        _hoverExpandTimer.Stop();

        MiniCircleView.Visibility = Visibility.Collapsed;
        CompactView.Visibility = Visibility.Collapsed;

        // Smooth fade-in of expanded view
        ExpandedView.Opacity = 0;
        ExpandedView.Visibility = Visibility.Visible;

        Width = 450;
        Height = 256;
        ApplyWindowPosition();

        AnimateIsland(EXPANDED_WIDTH, EXPANDED_HEIGHT, new CornerRadius(24));

        var fadeIn = new DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(180),
            BeginTime = TimeSpan.FromMilliseconds(50),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        ExpandedView.BeginAnimation(OpacityProperty, fadeIn);

        EnsureTopmost();
    }

    private void AppleIndicatorBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            CollapseIsland();
        }
        else
        {
            try
            {
                DragMove();
            }
            catch { }
        }
    }

    public void CollapseIsland()
    {
        if (!_isExpanded) return;
        _isExpanded = false;
        _hoverExpandTimer.Stop();

        var fadeOut = new DoubleAnimation
        {
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(90),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        bool isPlayingOrBomb = _mediaService.CurrentInfo.IsPlaying || _cs2GsiService.IsBombPlanted;
        double targetW = isPlayingOrBomb ? (COMPACT_WIDTH + (_cs2GsiService.IsBombPlanted ? 60 : 0)) : MINI_WIDTH;
        double targetH = isPlayingOrBomb ? COMPACT_HEIGHT : MINI_HEIGHT;
        CornerRadius targetRad = new CornerRadius(19);

        fadeOut.Completed += (s, e) =>
        {
            ExpandedView.Visibility = Visibility.Collapsed;

            if (isPlayingOrBomb)
            {
                MiniCircleView.Visibility = Visibility.Collapsed;
                CompactView.Opacity = 0;
                CompactView.Visibility = Visibility.Visible;
                var compFade = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(120));
                CompactView.BeginAnimation(OpacityProperty, compFade);
            }
            else
            {
                CompactView.Visibility = Visibility.Collapsed;
                MiniCircleView.Opacity = 0;
                MiniCircleView.Visibility = Visibility.Visible;
                var miniFade = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(120));
                MiniCircleView.BeginAnimation(OpacityProperty, miniFade);
            }

            var postTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            postTimer.Tick += (st, et) =>
            {
                postTimer.Stop();
                if (!_isExpanded)
                {
                    Width = isPlayingOrBomb ? 330 : 84;
                    Height = 56;
                    ApplyWindowPosition();
                }
            };
            postTimer.Start();
        };

        ExpandedView.BeginAnimation(OpacityProperty, fadeOut);
        AnimateIsland(targetW, targetH, targetRad);
        EnsureTopmost();
    }

    private void BtnCompactExpand_Click(object sender, RoutedEventArgs e)
    {
        ExpandIsland();
    }

    private void AnimateIsland(double targetWidth, double targetHeight, CornerRadius targetRadius)
    {
        var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.18 };
        var duration = TimeSpan.FromMilliseconds(260);

        var widthAnim = new DoubleAnimation
        {
            To = targetWidth,
            Duration = duration,
            EasingFunction = ease
        };

        var heightAnim = new DoubleAnimation
        {
            To = targetHeight,
            Duration = duration,
            EasingFunction = ease
        };

        IslandBorder.BeginAnimation(WidthProperty, widthAnim);
        IslandBorder.BeginAnimation(HeightProperty, heightAnim);
        IslandBorder.CornerRadius = targetRadius;
    }

    // --- Mouse & Hover Interactions & Dragging ---
    private System.Windows.Point _dragStartPoint;
    private bool _isDragging = false;
    private DateTime _lastDragEndTime = DateTime.MinValue;

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent) return parent;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private static bool IsInteractiveControl(DependencyObject? dep)
    {
        return FindVisualParent<System.Windows.Controls.Button>(dep) != null ||
               FindVisualParent<System.Windows.Controls.RadioButton>(dep) != null ||
               FindVisualParent<System.Windows.Controls.TextBox>(dep) != null ||
               FindVisualParent<System.Windows.Controls.PasswordBox>(dep) != null ||
               FindVisualParent<System.Windows.Controls.Slider>(dep) != null;
    }

    private void IslandBorder_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isHovering = true;
        _collapseDelayTimer.Stop();

        // While bomb is planted, never auto-expand on hover so player's screen is not blocked during gameplay
        if (!_isExpanded && !_audioRecorder.IsRecording && PanelIntro.Visibility != Visibility.Visible && !_cs2GsiService.IsBombPlanted)
        {
            _hoverExpandTimer.Start();
        }
    }

    private void IslandBorder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isHovering = false;
        _hoverExpandTimer.Stop();

        if (_isExpanded && !_isPinned && !_audioRecorder.IsRecording && PanelIntro.Visibility != Visibility.Visible)
        {
            _collapseDelayTimer.Start();
        }
    }

    private void IslandBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep && IsInteractiveControl(dep))
        {
            return;
        }

        _dragStartPoint = e.GetPosition(this);
        _isDragging = false;
    }

    private void IslandBorder_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
        {
            if (e.OriginalSource is DependencyObject dep && IsInteractiveControl(dep))
            {
                return;
            }

            var currentPos = e.GetPosition(this);
            var diff = _dragStartPoint - currentPos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                _isDragging = true;
                try
                {
                    DragMove();
                }
                catch { }
                finally
                {
                    _lastDragEndTime = DateTime.UtcNow;
                    _isDragging = false;
                    OnDragFinished();
                }
            }
        }
    }

    private void IslandBorder_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _hoverExpandTimer.Stop();

        if ((DateTime.UtcNow - _lastDragEndTime).TotalMilliseconds < 250)
        {
            e.Handled = true;
            return;
        }

        if (e.OriginalSource is DependencyObject dep && IsInteractiveControl(dep))
        {
            return;
        }

        if (!_isExpanded)
        {
            ExpandIsland();
            e.Handled = true;
        }
    }

    private void IslandBorder_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep && IsInteractiveControl(dep))
        {
            return;
        }

        // Right-click toggles Play / Pause directly without expanding
        _ = _mediaService.TogglePlayPauseAsync();
        e.Handled = true;
    }

    private void IslandBorder_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Mouse wheel over the island adjusts master system volume (±4%)
        byte vk = e.Delta > 0 ? VK_VOLUME_UP : VK_VOLUME_DOWN;
        for (int i = 0; i < 2; i++)
        {
            keybd_event(vk, 0, 0, UIntPtr.Zero);
            keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
        e.Handled = true;
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.OriginalSource is DependencyObject dep && IsInteractiveControl(dep))
            {
                return;
            }
            try
            {
                DragMove();
            }
            catch { }
            finally
            {
                _lastDragEndTime = DateTime.UtcNow;
                _isDragging = false;
                OnDragFinished();
            }
        }
    }

    private void OnDragFinished()
    {
        double minLeft = SystemParameters.VirtualScreenLeft;
        double screenWidth = SystemParameters.VirtualScreenWidth;
        double screenRight = minLeft + screenWidth;
        double minTop = SystemParameters.VirtualScreenTop;
        double screenHeight = SystemParameters.VirtualScreenHeight;

        double currentCenterX = Left + Width / 2.0;
        double currentCenterY = Top + Height / 2.0;

        bool isTop = currentCenterY < (minTop + screenHeight / 2.0);

        double distToLeft = Left - minLeft;
        double distToRight = screenRight - (Left + Width);
        double distToCenter = Math.Abs(currentCenterX - (minLeft + screenWidth / 2.0));

        IslandPosition targetPos;
        if (distToRight < distToCenter && distToRight < distToLeft)
        {
            targetPos = isTop ? IslandPosition.TopRight : IslandPosition.BottomRight;
        }
        else if (distToLeft < distToCenter && distToLeft < distToRight)
        {
            targetPos = isTop ? IslandPosition.TopLeft : IslandPosition.BottomLeft;
        }
        else
        {
            targetPos = isTop ? IslandPosition.TopCenter : IslandPosition.BottomCenter;
        }

        SetIslandPosition(targetPos);
    }

    private void SavePosition()
    {
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            JsonObject obj = File.Exists(_configPath)
                ? (JsonNode.Parse(File.ReadAllText(_configPath)) as JsonObject ?? new JsonObject())
                : new JsonObject();

            obj["islandPosition"] = JsonValue.Create(_islandPosition.ToString());
            File.WriteAllText(_configPath, obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    // 6 Position Click Handlers
    private void BtnPosTopLeft_Click(object sender, RoutedEventArgs e) => SetIslandPosition(IslandPosition.TopLeft);
    private void BtnPosTopCenter_Click(object sender, RoutedEventArgs e) => SetIslandPosition(IslandPosition.TopCenter);
    private void BtnPosTopRight_Click(object sender, RoutedEventArgs e) => SetIslandPosition(IslandPosition.TopRight);
    private void BtnPosBottomLeft_Click(object sender, RoutedEventArgs e) => SetIslandPosition(IslandPosition.BottomLeft);
    private void BtnPosBottomCenter_Click(object sender, RoutedEventArgs e) => SetIslandPosition(IslandPosition.BottomCenter);
    private void BtnPosBottomRight_Click(object sender, RoutedEventArgs e) => SetIslandPosition(IslandPosition.BottomRight);

    private void UpdatePositionButtonsUi()
    {
        if (BtnPosTopLeft == null || BtnPosTopCenter == null || BtnPosTopRight == null ||
            BtnPosBottomLeft == null || BtnPosBottomCenter == null || BtnPosBottomRight == null)
            return;

        var normalBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 34, 38));
        var activeBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(2, 132, 199));
        var normalFg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 208));
        var activeFg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));

        System.Windows.Controls.Button[] buttons = { BtnPosTopLeft, BtnPosTopCenter, BtnPosTopRight, BtnPosBottomLeft, BtnPosBottomCenter, BtnPosBottomRight };
        IslandPosition[] positions = { IslandPosition.TopLeft, IslandPosition.TopCenter, IslandPosition.TopRight, IslandPosition.BottomLeft, IslandPosition.BottomCenter, IslandPosition.BottomRight };

        for (int i = 0; i < buttons.Length; i++)
        {
            bool isActive = positions[i] == _islandPosition;
            buttons[i].Background = isActive ? activeBg : normalBg;
            buttons[i].Foreground = isActive ? activeFg : normalFg;
            buttons[i].FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal;
        }

        if (TxtCurrentPosBadge != null)
        {
            TxtCurrentPosBadge.Text = _islandPosition switch
            {
                IslandPosition.TopLeft => "Sol Üst",
                IslandPosition.TopCenter => "Üst Orta",
                IslandPosition.TopRight => "Sağ Üst",
                IslandPosition.BottomLeft => "Sol Alt",
                IslandPosition.BottomCenter => "Alt Orta",
                IslandPosition.BottomRight => "Sağ Alt",
                _ => "Üst Orta"
            };
        }
    }

    // --- Button Actions ---
    private void BtnPin_Click(object sender, RoutedEventArgs e)
    {
        _isPinned = !_isPinned;
        BtnPin.Foreground = _isPinned
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248))
            : new SolidColorBrush(System.Windows.Media.Color.FromRgb(136, 136, 144));
    }

    private void BtnCollapse_Click(object sender, RoutedEventArgs e)
    {
        _isPinned = false;
        CollapseIsland();
    }

    private void BtnResetPosition_Click(object sender, RoutedEventArgs e)
    {
        ResetPosition();
    }

    // Media Controls
    private async void BtnPlayPause_Click(object sender, RoutedEventArgs e)
    {
        await _mediaService.TogglePlayPauseAsync();
    }

    private async void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        await _mediaService.SkipNextAsync();
    }

    private async void BtnPrev_Click(object sender, RoutedEventArgs e)
    {
        await _mediaService.SkipPreviousAsync();
    }

    // Tab Navigation
    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (PanelMedia == null || PanelStopwatch == null || PanelTimer == null || PanelMcAfk == null || PanelCrosshair == null || PanelCs2Bomb == null || PanelAi == null || PanelTranslate == null || PanelSettings == null) return;

        bool isGamingSubTab = sender == TabCs2Bomb || sender == TabMcAfk || sender == TabCrosshair ||
                              sender == TabStopwatch || sender == TabTimer || sender == TabAi || sender == TabTranslate;

        if (isGamingSubTab && TabGamingTools != null && TabGamingTools.IsChecked != true)
        {
            TabGamingTools.IsChecked = true;
        }

        if (sender == TabGamingTools && TabCs2Bomb != null &&
            TabCs2Bomb.IsChecked != true && TabMcAfk.IsChecked != true && TabCrosshair.IsChecked != true &&
            TabStopwatch.IsChecked != true && TabTimer.IsChecked != true && TabAi.IsChecked != true && TabTranslate.IsChecked != true)
        {
            TabCs2Bomb.IsChecked = true;
        }

        bool isGamingActive = TabGamingTools?.IsChecked == true;
        if (PanelGamingTools != null)
        {
            PanelGamingTools.Visibility = isGamingActive ? Visibility.Visible : Visibility.Collapsed;
        }

        PanelMedia.Visibility = TabMedia?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        if (PanelFileShelf != null && TabFileShelf != null)
            PanelFileShelf.Visibility = TabFileShelf.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        if (PanelQuickApps != null && TabQuickApps != null)
            PanelQuickApps.Visibility = TabQuickApps.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        if (PanelSystemStats != null && TabSystemStats != null)
        {
            PanelSystemStats.Visibility = TabSystemStats.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            if (TabSystemStats.IsChecked == true && TxtSystemDate != null)
            {
                TxtSystemDate.Text = DateTime.Now.ToString("dddd, d MMMM", new System.Globalization.CultureInfo("tr-TR"));
            }
        }

        // Sub-panels inside Gaming Tools
        PanelStopwatch.Visibility = isGamingActive && TabStopwatch?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelTimer.Visibility = isGamingActive && TabTimer?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelMcAfk.Visibility = isGamingActive && TabMcAfk?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelCrosshair.Visibility = isGamingActive && TabCrosshair?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelCs2Bomb.Visibility = isGamingActive && TabCs2Bomb?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelAi.Visibility = isGamingActive && TabAi?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelTranslate.Visibility = isGamingActive && TabTranslate?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelSettings.Visibility = TabSettings?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

        if (isGamingActive && TabCrosshair?.IsChecked == true)
        {
            UpdateCrosshairUi();
        }

        if (isGamingActive && TabCs2Bomb?.IsChecked == true)
        {
            UpdateCs2CfgStatusUi();
        }

        if (TabSettings?.IsChecked == true)
        {
            TbKeyToggle.Text = _keyToggle;
            TbKeyMouse.Text = _keyMouse;
            TbKeyVoice.Text = _keyVoice;
            TbKeyPlay.Text = _keyPlay;
            TbKeyMcAfk.Text = _keyMcAfk;
            TbKeyCrosshair.Text = _keyCrosshair;
            SldOpacity.Value = _currentOpacity;
            TxtOpacityVal.Text = $"{(int)(_currentOpacity * 100)}%";
            if (_geminiService.HasApiKey)
            {
                PbApiKeySettings.Password = _geminiService.ApiKey;
            }
            UpdatePositionButtonsUi();
        }
    }

    // Stopwatch Controls
    private void BtnSwStart_Click(object sender, RoutedEventArgs e)
    {
        if (_stopwatchService.IsRunning)
        {
            _stopwatchService.PauseStopwatch();
            BtnSwStart.Content = "▶ Başlat";
            BtnSwStart.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
        }
        else
        {
            _stopwatchService.StartStopwatch();
            BtnSwStart.Content = "⏸ Durdur";
            BtnSwStart.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11));
        }
    }

    private void BtnSwReset_Click(object sender, RoutedEventArgs e)
    {
        _stopwatchService.ResetStopwatch();
        BtnSwStart.Content = "▶ Başlat";
        BtnSwStart.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
    }

    private void BtnSwLap_Click(object sender, RoutedEventArgs e)
    {
        _stopwatchService.AddLap();
    }

    // Countdown Timer Controls
    private void BtnPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string minsStr && int.TryParse(minsStr, out var mins))
        {
            _stopwatchService.StartCountdown(TimeSpan.FromMinutes(mins));
        }
    }

    private void BtnTimerReset_Click(object sender, RoutedEventArgs e)
    {
        _stopwatchService.ResetCountdown();
    }

    // ==========================================================
    // MINECRAFT AFK MOB FARM CONTROLS
    // ==========================================================

    public void ToggleMcAfk()
    {
        _mcAfkService.Toggle();
    }

    private void BtnToggleMcAfk_Click(object sender, RoutedEventArgs e)
    {
        ToggleMcAfk();
    }

    private void ChkOnlyMinecraft_Changed(object sender, RoutedEventArgs e)
    {
        _mcAfkService.OnlyWhenMinecraftActive = ChkOnlyMinecraft.IsChecked == true;
    }

    private async void BtnTestHit_Click(object sender, RoutedEventArgs e)
    {
        await _mcAfkService.PerformHitAsync();
    }

    private async void BtnTestEat_Click(object sender, RoutedEventArgs e)
    {
        await _mcAfkService.PerformEatAsync();
    }

    private void OnMcAfkStateChanged(MinecraftAfkState state)
    {
        Dispatcher.Invoke(() =>
        {
            if (BtnToggleMcAfk == null) return;

            if (state.IsRunning)
            {
                TxtMcAfkBtnText.Text = "AFK DURDUR";
                TxtMcAfkBtnIcon.Text = "⏹";
                BtnToggleMcAfk.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 38, 38));

                if (state.IsEating)
                {
                    DotMcAfkStatus.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(236, 72, 153));
                    TxtMcAfkStatus.Text = "🍖 Yemek Yeniyor...";
                    TxtMcAfkStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 114, 182));
                    TxtMcAfkDetail.Text = "Sol eldeki yemek yeniyor (2.2 sn sağ tık)...";
                }
                else if (_mcAfkService.OnlyWhenMinecraftActive && !state.IsMinecraftFocused)
                {
                    DotMcAfkStatus.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11));
                    TxtMcAfkStatus.Text = "⏸️ Beklemede (MC Odak Dışı)";
                    TxtMcAfkStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 191, 36));
                    TxtMcAfkDetail.Text = "Minecraft penceresine geçince otomatik devam edecek";
                }
                else
                {
                    DotMcAfkStatus.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
                    TxtMcAfkStatus.Text = "🟢 AFK Aktif (10s Çift Vuruş & Yemek)";
                    TxtMcAfkStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 211, 153));
                    TxtMcAfkDetail.Text = $"Minecraft odakta • {state.TotalHits} zombi kesildi";
                }

                TxtMcNextHit.Text = $"{state.SecondsUntilNextHit}s";
                int eatMin = state.SecondsUntilNextEat / 60;
                int eatSec = state.SecondsUntilNextEat % 60;
                TxtMcNextEat.Text = $"{eatMin:D2}:{eatSec:D2}";
                TxtMcStats.Text = $"{state.TotalHits} Vuruş";
                TxtMcEats.Text = $"{state.TotalEats} Yemek";
                TxtMcElapsed.Text = $"{(int)state.ElapsedTime.TotalMinutes:D2}:{state.ElapsedTime.Seconds:D2}";

                if (MiniAfkBadge != null)
                {
                    MiniAfkBadge.Visibility = Visibility.Visible;
                    TxtMiniAfk.Text = state.IsEating ? "🍖 YE" : $"{state.SecondsUntilNextHit}s";
                }
            }
            else
            {
                TxtMcAfkBtnText.Text = "AFK BAŞLAT";
                TxtMcAfkBtnIcon.Text = "▶";
                BtnToggleMcAfk.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
                DotMcAfkStatus.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(107, 114, 128));
                TxtMcAfkStatus.Text = $"AFK Beklemede ({_keyMcAfk})";
                TxtMcAfkStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184));
                TxtMcAfkDetail.Text = "Farma geçip Alt+K ile başlatabilirsiniz";

                TxtMcNextHit.Text = $"{_mcAfkService.HitIntervalSeconds}s";
                int eatMin = _mcAfkService.EatIntervalSeconds / 60;
                int eatSec = _mcAfkService.EatIntervalSeconds % 60;
                TxtMcNextEat.Text = $"{eatMin:D2}:{eatSec:D2}";

                if (MiniAfkBadge != null)
                {
                    MiniAfkBadge.Visibility = Visibility.Collapsed;
                }
            }
        });
    }

    // ==========================================================
    // THEMES, OPACITY & LIVE ANIMATED RGB
    // ==========================================================

    private void BtnTheme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string themeKey)
        {
            ApplyTheme(themeKey);
            SaveConfigValue("theme", themeKey);
        }
    }

    public void ApplyTheme(string theme)
    {
        _currentTheme = theme.ToLowerInvariant();
        _rgbTimer.Stop();

        switch (_currentTheme)
        {
            case "spotify":
                IslandBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 215, 96));
                IslandShadow.Color = System.Windows.Media.Color.FromRgb(30, 215, 96);
                IslandShadow.Opacity = 0.55;
                IslandShadow.BlurRadius = 26;
                break;
            case "brave":
                IslandBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 84, 43));
                IslandShadow.Color = System.Windows.Media.Color.FromRgb(251, 84, 43);
                IslandShadow.Opacity = 0.55;
                IslandShadow.BlurRadius = 26;
                break;
            case "cyberpunk":
                IslandBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(192, 132, 252));
                IslandShadow.Color = System.Windows.Media.Color.FromRgb(168, 85, 247);
                IslandShadow.Opacity = 0.65;
                IslandShadow.BlurRadius = 28;
                break;
            case "sakura":
                IslandBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 114, 182));
                IslandShadow.Color = System.Windows.Media.Color.FromRgb(236, 72, 153);
                IslandShadow.Opacity = 0.65;
                IslandShadow.BlurRadius = 28;
                break;
            case "lofi":
                IslandBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(168, 85, 247));
                IslandShadow.Color = System.Windows.Media.Color.FromRgb(139, 92, 246);
                IslandShadow.Opacity = 0.65;
                IslandShadow.BlurRadius = 28;
                break;
            case "edgerunners":
                IslandBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 229, 255));
                IslandShadow.Color = System.Windows.Media.Color.FromRgb(255, 0, 127);
                IslandShadow.Opacity = 0.7;
                IslandShadow.BlurRadius = 30;
                break;
            case "cyan":
                IslandBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248));
                IslandShadow.Color = System.Windows.Media.Color.FromRgb(2, 132, 199);
                IslandShadow.Opacity = 0.6;
                IslandShadow.BlurRadius = 28;
                break;
            case "rgb":
                IslandShadow.Opacity = 0.7;
                IslandShadow.BlurRadius = 30;
                _rgbTimer.Start();
                break;
            case "oled":
            default:
                var specBrush = new LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 0),
                    EndPoint = new System.Windows.Point(0, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(System.Windows.Media.Color.FromArgb(60, 255, 255, 255), 0.0),
                        new GradientStop(System.Windows.Media.Color.FromArgb(20, 255, 255, 255), 0.25),
                        new GradientStop(System.Windows.Media.Color.FromArgb(8, 255, 255, 255), 1.0)
                    }
                };
                IslandBorder.BorderBrush = specBrush;
                IslandShadow.Color = System.Windows.Media.Color.FromRgb(0, 0, 0);
                IslandShadow.Opacity = 0.55;
                IslandShadow.BlurRadius = 28;
                break;
        }

        UpdateBackgroundBrush();
    }

    private void UpdateBackgroundBrush()
    {
        byte alpha = (byte)Math.Clamp(_currentOpacity * 255, 30, 255);
        IslandBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 12, 12, 14));
    }

    private void SldOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _currentOpacity = e.NewValue;
        if (TxtOpacityVal != null)
        {
            TxtOpacityVal.Text = $"{(int)(_currentOpacity * 100)}%";
        }
        UpdateBackgroundBrush();
        SaveConfigValue("opacity", _currentOpacity);
    }

    private void RgbTimer_Tick(object? sender, EventArgs e)
    {
        _currentHue = (_currentHue + 3.0) % 360.0;
        var rgbColor = HsvToRgb(_currentHue, 0.9, 1.0);
        IslandBorder.BorderBrush = new SolidColorBrush(rgbColor);
        IslandShadow.Color = rgbColor;
    }

    private static System.Windows.Media.Color HsvToRgb(double h, double s, double v)
    {
        int hi = Convert.ToInt32(Math.Floor(h / 60)) % 6;
        double f = h / 60 - Math.Floor(h / 60);

        v = v * 255;
        byte vVal = Convert.ToByte(v);
        byte p = Convert.ToByte(v * (1 - s));
        byte q = Convert.ToByte(v * (1 - f * s));
        byte t = Convert.ToByte(v * (1 - (1 - f) * s));

        return hi switch
        {
            0 => System.Windows.Media.Color.FromRgb(vVal, t, p),
            1 => System.Windows.Media.Color.FromRgb(q, vVal, p),
            2 => System.Windows.Media.Color.FromRgb(p, vVal, t),
            3 => System.Windows.Media.Color.FromRgb(p, q, vVal),
            4 => System.Windows.Media.Color.FromRgb(t, p, vVal),
            _ => System.Windows.Media.Color.FromRgb(vVal, p, q),
        };
    }

    // ==========================================================
    // CONFIG & HOTKEY SETTINGS
    // ==========================================================

    private void LoadConfig()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var node = JsonNode.Parse(json);
                if (node != null)
                {
                    var theme = node["theme"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(theme)) _currentTheme = theme;

                    if (node["opacity"] != null)
                    {
                        _currentOpacity = node["opacity"]!.GetValue<double>();
                    }

                    _keyToggle = node["keyToggle"]?.GetValue<string>() ?? "Alt+D";
                    _keyMouse = node["keyMouse"]?.GetValue<string>() ?? "Alt+M";
                    _keyVoice = node["keyVoice"]?.GetValue<string>() ?? "Alt+V";
                    _keyPlay = node["keyPlay"]?.GetValue<string>() ?? "Alt+P";
                    _keyMcAfk = node["keyMcAfk"]?.GetValue<string>() ?? "Alt+K";
                    _keyCrosshair = node["keyCrosshair"]?.GetValue<string>() ?? "Alt+X";

                    if (node["crosshairStyle"] != null) _crosshairStyle = node["crosshairStyle"]!.GetValue<string>() ?? "dot";
                    if (node["crosshairSize"] != null) _crosshairSize = node["crosshairSize"]!.GetValue<double>();
                    if (node["crosshairColor"] != null) _crosshairColorHex = node["crosshairColor"]!.GetValue<string>() ?? "#00FF66";
                    if (node["crosshairOpacity"] != null) _crosshairOpacity = node["crosshairOpacity"]!.GetValue<double>();
                    if (node["crosshairOffsetX"] != null) _crosshairOffsetX = node["crosshairOffsetX"]!.GetValue<int>();
                    if (node["crosshairOffsetY"] != null) _crosshairOffsetY = node["crosshairOffsetY"]!.GetValue<int>();
                    if (node["isCrosshairEnabled"] != null) _isCrosshairEnabled = node["isCrosshairEnabled"]!.GetValue<bool>();

                    if (node["islandPosition"] != null)
                    {
                        var posStr = node["islandPosition"]!.GetValue<string>();
                        if (Enum.TryParse<IslandPosition>(posStr, out var parsedPos))
                        {
                            _islandPosition = parsedPos;
                        }
                    }
                    else if (node["anchorMode"] != null)
                    {
                        var modeStr = node["anchorMode"]!.GetValue<string>();
                        if (modeStr == "Right") _islandPosition = IslandPosition.TopRight;
                        else if (modeStr == "Left") _islandPosition = IslandPosition.TopLeft;
                        else _islandPosition = IslandPosition.TopCenter;
                    }

                    if (node["animeTheme"] != null) _currentAnimeTheme = node["animeTheme"]!.GetValue<string>() ?? "none";
                    if (node["customImagePath"] != null) _customImagePath = node["customImagePath"]!.GetValue<string>() ?? "";
                    if (node["showMascot"] != null) _showMascot = node["showMascot"]!.GetValue<bool>();
                    if (node["showBgImage"] != null) _showBgImage = node["showBgImage"]!.GetValue<bool>();
                    if (node["mascotDance"] != null) _mascotDance = node["mascotDance"]!.GetValue<bool>();

                    ApplyWindowPosition();
                    UpdatePositionButtonsUi();
                    ApplyMascotTheme(_currentAnimeTheme, _customImagePath);
                }
            }
        }
        catch { }

        ApplyTheme(_currentTheme);
    }

    private void SaveConfigValue<T>(string key, T value)
    {
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            JsonObject obj;
            if (File.Exists(_configPath))
            {
                obj = JsonNode.Parse(File.ReadAllText(_configPath)) as JsonObject ?? new JsonObject();
            }
            else
            {
                obj = new JsonObject();
            }

            obj[key] = JsonValue.Create(value);
            File.WriteAllText(_configPath, obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private void BtnSaveHotkeys_Click(object sender, RoutedEventArgs e)
    {
        _keyToggle = string.IsNullOrWhiteSpace(TbKeyToggle.Text) ? "Alt+D" : TbKeyToggle.Text.Trim();
        _keyMouse = string.IsNullOrWhiteSpace(TbKeyMouse.Text) ? "Alt+M" : TbKeyMouse.Text.Trim();
        _keyVoice = string.IsNullOrWhiteSpace(TbKeyVoice.Text) ? "Alt+V" : TbKeyVoice.Text.Trim();
        _keyPlay = string.IsNullOrWhiteSpace(TbKeyPlay.Text) ? "Alt+P" : TbKeyPlay.Text.Trim();
        _keyMcAfk = string.IsNullOrWhiteSpace(TbKeyMcAfk.Text) ? "Alt+K" : TbKeyMcAfk.Text.Trim();
        _keyCrosshair = string.IsNullOrWhiteSpace(TbKeyCrosshair.Text) ? "Alt+X" : TbKeyCrosshair.Text.Trim();

        _hotKeyService.ReRegister(_keyToggle, _keyMouse, _keyVoice, _keyPlay, _keyMcAfk, _keyCrosshair);

        SaveConfigValue("keyToggle", _keyToggle);
        SaveConfigValue("keyMouse", _keyMouse);
        SaveConfigValue("keyVoice", _keyVoice);
        SaveConfigValue("keyPlay", _keyPlay);
        SaveConfigValue("keyMcAfk", _keyMcAfk);
        SaveConfigValue("keyCrosshair", _keyCrosshair);

        if (TxtMcHotkeyBadge != null)
        {
            TxtMcHotkeyBadge.Text = $"Kısayol: {_keyMcAfk}";
        }
        if (TxtCrossHotkeyBadge != null)
        {
            TxtCrossHotkeyBadge.Text = $"Kısayol: {_keyCrosshair}";
        }

        InitTrayIcon();
        UpdateCrosshairUi();
        System.Windows.MessageBox.Show($"Kısayol tuşları başarıyla güncellendi!\nAda: {_keyToggle} | Fare: {_keyMouse} | Ses: {_keyVoice} | Müzik: {_keyPlay} | MC Farm: {_keyMcAfk} | Cross: {_keyCrosshair}", "Dinamik Ada", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnSaveApiKeySettings_Click(object sender, RoutedEventArgs e)
    {
        var key = PbApiKeySettings.Password?.Trim();
        if (!string.IsNullOrEmpty(key))
        {
            _geminiService.ApiKey = key;
            System.Windows.MessageBox.Show("Gemini API Anahtarınız kaydedildi!", "Dinamik Ada", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // ==========================================================
    // ONBOARDING / INTRO WALKTHROUGH
    // ==========================================================

    private void CheckFirstRunIntro()
    {
        try
        {
            bool hasSeen = false;
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var node = JsonNode.Parse(json);
                hasSeen = node?["hasSeenIntro"]?.GetValue<bool>() ?? false;
            }

            if (!hasSeen)
            {
                ShowIntro();
            }
        }
        catch { }
    }

    private void ShowIntro()
    {
        ExpandIsland();
        PanelIntro.Visibility = Visibility.Visible;
    }

    private void BtnDismissIntro_Click(object sender, RoutedEventArgs e)
    {
        PanelIntro.Visibility = Visibility.Collapsed;
        SaveConfigValue("hasSeenIntro", true);
        TabMedia.IsChecked = true;
    }

    private void BtnShowIntro_Click(object sender, RoutedEventArgs e)
    {
        ShowIntro();
    }

    // ==========================================================
    // GEMINI AI & SESLİ KOMUT KONTROLLERİ
    // ==========================================================

    private async void BtnCompactMic_Click(object sender, RoutedEventArgs e)
    {
        await ToggleVoiceRecordingAsync();
    }

    private async void BtnMic_Click(object sender, RoutedEventArgs e)
    {
        await ToggleVoiceRecordingAsync();
    }

    private async System.Threading.Tasks.Task ToggleVoiceRecordingAsync()
    {
        if (!_audioRecorder.IsRecording)
        {
            // Start recording
            var started = _audioRecorder.StartRecording();
            if (started)
            {
                TxtCompactTitle.Text = "🎙️ Dinleniyor...";
                TxtMicIcon.Text = "🔴";
                TxtMicStatus.Text = "Dinleniyor... (Bitir)";
                TxtMicStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
                BtnMicToggle.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));

                // Switch to AI tab so user can see what's happening
                ExpandIsland();
                TabAi.IsChecked = true;
            }
        }
        else
        {
            // Stop recording & Process
            TxtMicIcon.Text = "⏳";
            TxtMicStatus.Text = "İşleniyor...";
            TxtMicStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11));
            TxtAiUserSpeech.Text = "🗣️ Ses kaydı çözümleniyor...";
            TxtAiReply.Text = "✨ Gemini yanıtı bekleniyor...";

            var bytes = await _audioRecorder.StopRecordingAsync();

            TxtMicIcon.Text = "🎙️";
            TxtMicStatus.Text = "Bas-Konuş (Alt+V)";
            TxtMicStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248));
            BtnMicToggle.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248));

            if (bytes != null && bytes.Length > 0)
            {
                var result = await _geminiService.ProcessAudioAsync(bytes);
                await HandleGeminiResultAsync(result);
            }
            else
            {
                TxtAiUserSpeech.Text = "🗣️ Ses algılanamadı.";
                TxtAiReply.Text = "✨ Lütfen mikrofona konuşup tekrar deneyin.";
                _ = _mediaService.RefreshMediaInfoAsync();
            }
        }
    }

    private async void BtnAiSend_Click(object sender, RoutedEventArgs e)
    {
        await SendAiTextMessageAsync();
    }

    private async void TxtAiInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await SendAiTextMessageAsync();
        }
    }

    private async System.Threading.Tasks.Task SendAiTextMessageAsync()
    {
        var text = TxtAiInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        TxtAiInput.Text = "";
        TxtAiUserSpeech.Text = $"🗣️ \"{text}\"";
        TxtAiReply.Text = "⏳ Düşünüyor...";

        var result = await _geminiService.ProcessTextAsync(text);
        await HandleGeminiResultAsync(result);
    }

    private async System.Threading.Tasks.Task HandleGeminiResultAsync(GeminiResult result)
    {
        if (!result.Success)
        {
            TxtAiReply.Text = $"⚠️ {result.Error}";
            if (!_geminiService.HasApiKey)
            {
                TabSettings.IsChecked = true;
            }
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.UserText))
        {
            TxtAiUserSpeech.Text = $"🗣️ \"{result.UserText}\"";
        }

        TxtAiReply.Text = $"✨ {result.ResponseMessage}";

        // Execute recognized Action
        switch (result.Action.ToUpperInvariant())
        {
            case "PLAY_MUSIC":
                var target = string.IsNullOrWhiteSpace(result.ActionArg) ? result.UserText : result.ActionArg;
                MusicActionService.PlayOrSearchMusic(target);
                break;

            case "VOLUME_UP":
                VolumeService.VolumeUp(6);
                break;

            case "VOLUME_DOWN":
                VolumeService.VolumeDown(6);
                break;

            case "VOLUME_MUTE":
                VolumeService.ToggleMute();
                break;

            case "NEXT_TRACK":
                await _mediaService.SkipNextAsync();
                break;

            case "PREV_TRACK":
                await _mediaService.SkipPreviousAsync();
                break;

            case "TOGGLE_PLAY":
                await _mediaService.TogglePlayPauseAsync();
                break;

            case "START_STOPWATCH":
                _stopwatchService.StartStopwatch();
                Tab_Checked(TabStopwatch, new RoutedEventArgs());
                break;

            case "PAUSE_STOPWATCH":
                _stopwatchService.PauseStopwatch();
                Tab_Checked(TabStopwatch, new RoutedEventArgs());
                break;

            case "RESET_STOPWATCH":
                _stopwatchService.ResetStopwatch();
                Tab_Checked(TabStopwatch, new RoutedEventArgs());
                break;

            case "START_TIMER":
                if (int.TryParse(result.ActionArg, out var minutes) && minutes > 0)
                {
                    _stopwatchService.StartCountdown(TimeSpan.FromMinutes(minutes));
                    Tab_Checked(TabTimer, new RoutedEventArgs());
                }
                break;
        }
    }

    // Quick Volume Shortcuts
    private void BtnVolUp_Click(object sender, RoutedEventArgs e)
    {
        VolumeService.VolumeUp(5);
    }

    private void BtnVolDown_Click(object sender, RoutedEventArgs e)
    {
        VolumeService.VolumeDown(5);
    }

    private void BtnVolMute_Click(object sender, RoutedEventArgs e)
    {
        VolumeService.ToggleMute();
    }

    // ==========================================================
    // 🌐 ÇEVİRİ (TRANSLATE - EN <-> TR) CONTROLS
    // ==========================================================

    private void BtnLangSwitch_Click(object sender, RoutedEventArgs e)
    {
        ToggleTranslateDirection();
    }

    private void BtnSwapDirection_Click(object sender, RoutedEventArgs e)
    {
        ToggleTranslateDirection();
    }

    private void ToggleTranslateDirection()
    {
        if (_translateSourceLang == "tr")
        {
            _translateSourceLang = "en";
            _translateTargetLang = "tr";
            BtnLangSwitch.Content = "🇬🇧 İngilizce ➔ 🇹🇷 Türkçe";
        }
        else
        {
            _translateSourceLang = "tr";
            _translateTargetLang = "en";
            BtnLangSwitch.Content = "🇹🇷 Türkçe ➔ 🇬🇧 İngilizce";
        }

        var curResult = TxtTranslateResult.Text;
        if (!string.IsNullOrWhiteSpace(curResult) && !curResult.StartsWith("Çevrilecek") && !curResult.StartsWith("⏳"))
        {
            TbTranslateInput.Text = curResult;
            _ = DoTranslateAsync();
        }
    }

    private async void BtnDoTranslate_Click(object sender, RoutedEventArgs e)
    {
        await DoTranslateAsync();
    }

    private async void TbTranslateInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            e.Handled = true;
            await DoTranslateAsync();
        }
    }

    private async void BtnPasteTranslate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (System.Windows.Clipboard.ContainsText())
            {
                var text = System.Windows.Clipboard.GetText()?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    TbTranslateInput.Text = text;
                    await DoTranslateAsync();
                }
            }
        }
        catch { }
    }

    private void BtnClearTranslate_Click(object sender, RoutedEventArgs e)
    {
        TbTranslateInput.Text = string.Empty;
        TxtTranslateResult.Text = "Çevrilecek metni yukarıya yazın veya yapıştırın...";
    }

    private async Task DoTranslateAsync()
    {
        var input = TbTranslateInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(input)) return;

        TxtTranslateResult.Text = "⏳ Çevriliyor...";
        BtnDoTranslate.IsEnabled = false;

        try
        {
            var result = await _translateService.TranslateAsync(input, _translateSourceLang, _translateTargetLang);
            TxtTranslateResult.Text = string.IsNullOrWhiteSpace(result) ? "⚠️ Çeviri yapılamadı." : result;
        }
        catch (Exception ex)
        {
            TxtTranslateResult.Text = $"⚠️ Hata: {ex.Message}";
        }
        finally
        {
            BtnDoTranslate.IsEnabled = true;
        }
    }

    private void BtnCopyResult_Click(object sender, RoutedEventArgs e)
    {
        var text = TxtTranslateResult.Text;
        if (!string.IsNullOrWhiteSpace(text) && !text.StartsWith("Çevrilecek") && !text.StartsWith("⏳"))
        {
            try
            {
                System.Windows.Clipboard.SetText(text);
                BtnCopyResult.Content = "✅ Kopyalandı";
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += (s, ev) =>
                {
                    timer.Stop();
                    BtnCopyResult.Content = "📋 Kopyala";
                };
                timer.Start();
            }
            catch { }
        }
    }

    private void BtnSpeakResult_Click(object sender, RoutedEventArgs e)
    {
        var text = TxtTranslateResult.Text;
        if (string.IsNullOrWhiteSpace(text) || text.StartsWith("Çevrilecek") || text.StartsWith("⏳")) return;

        Task.Run(() =>
        {
            try
            {
                var escaped = text.Replace("'", "''").Replace("\"", "`\"");
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -Command \"Add-Type -AssemblyName System.Speech; (New-Object System.Speech.Synthesis.SpeechSynthesizer).Speak('{escaped}')\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch { }
        });
    }

    private async void BtnQuickPhrase_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string phrase)
        {
            TbTranslateInput.Text = phrase;
            await DoTranslateAsync();
        }
    }

    // ==========================================================
    // GAMER CROSSHAIR / CENTER DOT SYSTEM
    // ==========================================================
    public void ToggleCrosshair()
    {
        if (_isCrosshairEnabled)
        {
            DisableCrosshair();
        }
        else
        {
            EnableCrosshair();
        }
    }

    public void EnableCrosshair()
    {
        _isCrosshairEnabled = true;
        if (_crosshairWindow == null)
        {
            _crosshairWindow = new CrosshairWindow();
        }
        ApplyCrosshairSettings();
        _crosshairWindow.Show();
        UpdateCrosshairUi();
        SaveConfigValue("isCrosshairEnabled", true);
    }

    public void DisableCrosshair()
    {
        _isCrosshairEnabled = false;
        if (_crosshairWindow != null)
        {
            _crosshairWindow.Hide();
        }
        UpdateCrosshairUi();
        SaveConfigValue("isCrosshairEnabled", false);
    }

    private void ApplyCrosshairSettings()
    {
        if (_crosshairWindow == null) return;
        System.Windows.Media.Color c;
        try
        {
            c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_crosshairColorHex);
        }
        catch
        {
            c = System.Windows.Media.Color.FromRgb(0x00, 0xFF, 0x66);
        }
        _crosshairWindow.ApplySettings(_crosshairStyle, _crosshairSize, c, _crosshairOpacity, _crosshairOffsetX, _crosshairOffsetY);
    }

    private void UpdateCrosshairUi()
    {
        if (BtnToggleCrosshair == null) return;

        if (_isCrosshairEnabled)
        {
            BtnToggleCrosshair.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDC, 0x26, 0x26)); // Red
            TxtCrossBtnText.Text = "CROSSHAIR KAPAT";
            DotCrossStatus.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81)); // Green active dot
            TxtCrossStatus.Text = $"Crosshair Aktif ({_keyCrosshair})";
            TxtCrossStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x34, 0xD3, 0x99));
            if (MiniCrossBadge != null) MiniCrossBadge.Visibility = Visibility.Visible;
        }
        else
        {
            BtnToggleCrosshair.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81)); // Green
            TxtCrossBtnText.Text = "CROSSHAIR AÇ";
            DotCrossStatus.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6B, 0x72, 0x80)); // Gray
            TxtCrossStatus.Text = $"Crosshair Kapalı ({_keyCrosshair})";
            TxtCrossStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8));
            if (MiniCrossBadge != null) MiniCrossBadge.Visibility = Visibility.Collapsed;
        }

        if (TxtCrossHotkeyBadge != null)
        {
            TxtCrossHotkeyBadge.Text = $"Kısayol: {_keyCrosshair}";
        }

        if (TxtOffX != null) TxtOffX.Text = $"X:{_crosshairOffsetX}";
        if (TxtOffY != null) TxtOffY.Text = $"Y:{_crosshairOffsetY}";

        if (SldCrossOpacity != null)
        {
            SldCrossOpacity.Value = _crosshairOpacity;
        }
        if (TxtCrossOpacityVal != null)
        {
            TxtCrossOpacityVal.Text = $"{(int)(_crosshairOpacity * 100)}%";
        }

        // Style button highlights
        UpdateStyleButtonsUi();

        // Size button highlights
        UpdateSizeButtonsUi();

        // Color button highlights
        UpdateColorButtonsUi();

        // Update preview in target simulator
        UpdateCrosshairPreview();
    }

    private void UpdateCrosshairPreview()
    {
        if (PrevCrossContainer == null) return;

        System.Windows.Media.Color c;
        try
        {
            c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_crosshairColorHex);
        }
        catch
        {
            c = System.Windows.Media.Color.FromRgb(0x00, 0xFF, 0x66);
        }
        var brush = new SolidColorBrush(c);

        PrevDotView.Visibility = _crosshairStyle == "dot" ? Visibility.Visible : Visibility.Collapsed;
        PrevRingView.Visibility = _crosshairStyle == "ring" ? Visibility.Visible : Visibility.Collapsed;
        PrevPlusView.Visibility = _crosshairStyle == "plus" ? Visibility.Visible : Visibility.Collapsed;
        PrevSniperView.Visibility = _crosshairStyle == "sniper" ? Visibility.Visible : Visibility.Collapsed;

        PrevDotMain.Width = _crosshairSize;
        PrevDotMain.Height = _crosshairSize;
        PrevDotOutline.Width = _crosshairSize + 2;
        PrevDotOutline.Height = _crosshairSize + 2;
        PrevDotMain.Fill = brush;

        PrevRingMain.Width = _crosshairSize * 3;
        PrevRingMain.Height = _crosshairSize * 3;
        PrevRingOutline.Width = _crosshairSize * 3 + 2;
        PrevRingOutline.Height = _crosshairSize * 3 + 2;
        PrevRingMain.Stroke = brush;
        PrevRingCenterDot.Fill = brush;

        PrevLineTop.Stroke = brush;
        PrevLineBottom.Stroke = brush;
        PrevLineLeft.Stroke = brush;
        PrevLineRight.Stroke = brush;
        PrevPlusCenterDot.Fill = brush;

        PrevSniperMain.Width = Math.Max(2, _crosshairSize);
        PrevSniperMain.Height = Math.Max(2, _crosshairSize);
        PrevSniperOutline.Width = Math.Max(2, _crosshairSize) + 2;
        PrevSniperOutline.Height = Math.Max(2, _crosshairSize) + 2;
        PrevSniperGlow.Width = _crosshairSize * 3.5;
        PrevSniperGlow.Height = _crosshairSize * 3.5;
        PrevSniperMain.Fill = brush;
        PrevSniperGlow.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(64, c.R, c.G, c.B));

        PrevCrossContainer.Opacity = _crosshairOpacity;
    }

    private void UpdateStyleButtonsUi()
    {
        if (BtnStyleDot == null) return;
        var activeBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2A, 0x38, 0x4F));
        var defBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x22, 0x22, 0x26));

        BtnStyleDot.Background = _crosshairStyle == "dot" ? activeBg : defBg;
        BtnStyleRing.Background = _crosshairStyle == "ring" ? activeBg : defBg;
        BtnStylePlus.Background = _crosshairStyle == "plus" ? activeBg : defBg;
        BtnStyleSniper.Background = _crosshairStyle == "sniper" ? activeBg : defBg;
    }

    private void UpdateSizeButtonsUi()
    {
        if (BtnSize2 == null) return;
        var activeBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2A, 0x38, 0x4F));
        var defBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x22, 0x22, 0x26));

        BtnSize2.Background = Math.Abs(_crosshairSize - 2) < 0.1 ? activeBg : defBg;
        BtnSize3.Background = Math.Abs(_crosshairSize - 3) < 0.1 ? activeBg : defBg;
        BtnSize4.Background = Math.Abs(_crosshairSize - 4) < 0.1 ? activeBg : defBg;
        BtnSize5.Background = Math.Abs(_crosshairSize - 5) < 0.1 ? activeBg : defBg;
        BtnSize6.Background = Math.Abs(_crosshairSize - 6) < 0.1 ? activeBg : defBg;
    }

    private void UpdateColorButtonsUi()
    {
        if (BtnColGreen == null) return;
        void SetBorder(System.Windows.Controls.Button btn, string hex)
        {
            if (string.Equals(_crosshairColorHex, hex, StringComparison.OrdinalIgnoreCase))
            {
                btn.BorderThickness = new Thickness(2);
                btn.BorderBrush = new SolidColorBrush(Colors.White);
            }
            else
            {
                btn.BorderThickness = new Thickness(0);
                btn.BorderBrush = null;
            }
        }

        SetBorder(BtnColGreen, "#00FF66");
        SetBorder(BtnColRed, "#FF3344");
        SetBorder(BtnColCyan, "#00E5FF");
        SetBorder(BtnColYellow, "#FFEA00");
        SetBorder(BtnColMagenta, "#C026D3");
        SetBorder(BtnColWhite, "#FFFFFF");
    }

    private void BtnToggleCrosshair_Click(object sender, RoutedEventArgs e)
    {
        ToggleCrosshair();
    }

    private void BtnCrossStyle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string style)
        {
            _crosshairStyle = style;
            ApplyCrosshairSettings();
            UpdateCrosshairUi();
            SaveConfigValue("crosshairStyle", _crosshairStyle);
        }
    }

    private void BtnCrossSize_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string szStr && double.TryParse(szStr, out var sz))
        {
            _crosshairSize = sz;
            ApplyCrosshairSettings();
            UpdateCrosshairUi();
            SaveConfigValue("crosshairSize", _crosshairSize);
        }
    }

    private void BtnCrossColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string hex)
        {
            _crosshairColorHex = hex;
            ApplyCrosshairSettings();
            UpdateCrosshairUi();
            SaveConfigValue("crosshairColor", _crosshairColorHex);
        }
    }

    private void SldCrossOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _crosshairOpacity = e.NewValue;
        if (TxtCrossOpacityVal != null)
        {
            TxtCrossOpacityVal.Text = $"{(int)(_crosshairOpacity * 100)}%";
        }
        ApplyCrosshairSettings();
        UpdateCrosshairPreview();
        SaveConfigValue("crosshairOpacity", _crosshairOpacity);
    }

    private void BtnOffXMinus_Click(object sender, RoutedEventArgs e)
    {
        _crosshairOffsetX--;
        ApplyCrosshairSettings();
        if (TxtOffX != null) TxtOffX.Text = $"X:{_crosshairOffsetX}";
        SaveConfigValue("crosshairOffsetX", _crosshairOffsetX);
    }

    private void BtnOffXPlus_Click(object sender, RoutedEventArgs e)
    {
        _crosshairOffsetX++;
        ApplyCrosshairSettings();
        if (TxtOffX != null) TxtOffX.Text = $"X:{_crosshairOffsetX}";
        SaveConfigValue("crosshairOffsetX", _crosshairOffsetX);
    }

    private void BtnOffYMinus_Click(object sender, RoutedEventArgs e)
    {
        _crosshairOffsetY--;
        ApplyCrosshairSettings();
        if (TxtOffY != null) TxtOffY.Text = $"Y:{_crosshairOffsetY}";
        SaveConfigValue("crosshairOffsetY", _crosshairOffsetY);
    }

    private void BtnOffYPlus_Click(object sender, RoutedEventArgs e)
    {
        _crosshairOffsetY++;
        ApplyCrosshairSettings();
        if (TxtOffY != null) TxtOffY.Text = $"Y:{_crosshairOffsetY}";
        SaveConfigValue("crosshairOffsetY", _crosshairOffsetY);
    }

    private void BtnResetCrossOffset_Click(object sender, RoutedEventArgs e)
    {
        _crosshairOffsetX = 0;
        _crosshairOffsetY = 0;
        ApplyCrosshairSettings();
        if (TxtOffX != null) TxtOffX.Text = "X:0";
        if (TxtOffY != null) TxtOffY.Text = "Y:0";
        SaveConfigValue("crosshairOffsetX", 0);
        SaveConfigValue("crosshairOffsetY", 0);
    }

    // =========================================================================
    // --- CS2 Bomb Explosion Timer & Defuse Assistant ---
    // =========================================================================
    private void OnCs2BombPlanted()
    {
        if (MiniBombBadge != null)
        {
            MiniBombBadge.Visibility = Visibility.Visible;
        }

        if (TxtCs2BombState != null)
        {
            TxtCs2BombState.Text = "💣 BOMBA KURULDU!";
        }

        if (_cs2GsiService.AutoExpandOnPlant)
        {
            ExpandIsland();
            if (TabCs2Bomb != null)
            {
                TabCs2Bomb.IsChecked = true;
                Tab_Checked(TabCs2Bomb, new RoutedEventArgs());
            }
        }
        else
        {
            // Keep island small and compact! Never block the screen during gameplay
            if (_isExpanded)
            {
                CollapseIsland();
            }
            else
            {
                MiniCircleView.Visibility = Visibility.Collapsed;
                CompactView.Visibility = Visibility.Visible;
                Width = 350;
                Height = 56;
                ApplyWindowPosition();
                AnimateIsland(COMPACT_WIDTH + 60, COMPACT_HEIGHT, new CornerRadius(18));
            }
        }
    }

    private void UpdateCs2BombUi(double time, bool canWithout, bool canWith, double kitsizDl, double kitliDl)
    {
        // 1. Update Compact View Pill Badge
        if (MiniBombBadge != null && MiniBombBadge.Visibility == Visibility.Visible)
        {
            if (TxtMiniBombTimer != null)
            {
                TxtMiniBombTimer.Text = $"{time:0.0}s";
                TxtMiniBombTimer.Foreground = time <= 5.0
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68))
                    : time <= 10.0
                        ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11))
                        : new SolidColorBrush(System.Windows.Media.Color.FromRgb(252, 165, 165));
            }

            if (BadgeMiniDefuse != null && TxtMiniDefuseState != null)
            {
                if (canWithout)
                {
                    BadgeMiniDefuse.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(6, 78, 59));
                    TxtMiniDefuseState.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 211, 153));
                    TxtMiniDefuseState.Text = $"KİTSİZ ({kitsizDl:0.0}s)";
                }
                else if (canWith)
                {
                    BadgeMiniDefuse.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(120, 53, 15));
                    TxtMiniDefuseState.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 191, 36));
                    TxtMiniDefuseState.Text = $"KİTLİ ({kitliDl:0.0}s)";
                }
                else
                {
                    BadgeMiniDefuse.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(127, 29, 29));
                    TxtMiniDefuseState.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 113, 113));
                    TxtMiniDefuseState.Text = "KAÇ!";
                }
            }
        }

        // 2. Update Expanded Tactical Panel
        if (PanelCs2Bomb != null)
        {
            if (TxtCs2BombTimer != null)
            {
                TxtCs2BombTimer.Text = $"{time:0.0}s";
                TxtCs2BombTimer.Foreground = time <= 5.0
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68))
                    : time <= 10.0
                        ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11))
                        : new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
            }

            if (PbCs2BombProgress != null)
            {
                PbCs2BombProgress.Value = Math.Clamp((time / _cs2GsiService.TotalBombTime) * 100.0, 0, 100);
            }

            // Kitsiz card
            if (TxtCs2KitsizStatus != null && TxtCs2KitsizDeadline != null && BorderCs2KitsizCard != null)
            {
                if (canWithout)
                {
                    TxtCs2KitsizStatus.Text = "✅ KİTSİZ ÇÖZEBİLİRSİN";
                    TxtCs2KitsizStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 211, 153));
                    TxtCs2KitsizDeadline.Text = $"{kitsizDl:0.0}s";
                    BorderCs2KitsizCard.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(5, 150, 105));
                    BorderCs2KitsizCard.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(6, 34, 23));
                }
                else
                {
                    TxtCs2KitsizStatus.Text = "❌ KİTSİZ ÇÖZÜLEMEZ";
                    TxtCs2KitsizStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
                    TxtCs2KitsizDeadline.Text = "0.0s";
                    BorderCs2KitsizCard.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 27, 27));
                    BorderCs2KitsizCard.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 13, 13));
                }
            }

            // Kitli card
            if (TxtCs2KitliStatus != null && TxtCs2KitliDeadline != null && BorderCs2KitliCard != null)
            {
                if (canWith)
                {
                    TxtCs2KitliStatus.Text = "✅ KİTLE ÇÖZEBİLİRSİN";
                    TxtCs2KitliStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248));
                    TxtCs2KitliDeadline.Text = $"{kitliDl:0.0}s";
                    BorderCs2KitliCard.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(2, 132, 199));
                    BorderCs2KitliCard.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(12, 31, 56));
                }
                else
                {
                    TxtCs2KitliStatus.Text = "💥 KAÇ! ÇÖZÜLEMEZ";
                    TxtCs2KitliStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
                    TxtCs2KitliDeadline.Text = "0.0s";
                    BorderCs2KitliCard.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(185, 28, 28));
                    BorderCs2KitliCard.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 10, 10));
                }
            }
        }
    }

    private void OnCs2BombDefused()
    {
        if (TxtMiniBombTimer != null) TxtMiniBombTimer.Text = "0.0s";
        if (TxtMiniDefuseState != null)
        {
            TxtMiniDefuseState.Text = "🏆 ÇÖZÜLDÜ";
            TxtMiniDefuseState.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 211, 153));
        }
        if (BadgeMiniDefuse != null)
        {
            BadgeMiniDefuse.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(6, 95, 70));
        }

        if (TxtCs2BombState != null) TxtCs2BombState.Text = "🏆 BOMBA ÇÖZÜLDÜ (DEFUSED)";
        if (TxtCs2BombTimer != null)
        {
            TxtCs2BombTimer.Text = "DEFUSED";
            TxtCs2BombTimer.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 211, 153));
        }
        if (PbCs2BombProgress != null) PbCs2BombProgress.Value = 0;
    }

    private void OnCs2BombExploded()
    {
        if (TxtMiniBombTimer != null) TxtMiniBombTimer.Text = "💥";
        if (TxtMiniDefuseState != null)
        {
            TxtMiniDefuseState.Text = "PATLADI!";
            TxtMiniDefuseState.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(252, 165, 165));
        }
        if (BadgeMiniDefuse != null)
        {
            BadgeMiniDefuse.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 27, 27));
        }

        if (TxtCs2BombState != null) TxtCs2BombState.Text = "💥 BOMBA PATLADI!";
        if (TxtCs2BombTimer != null)
        {
            TxtCs2BombTimer.Text = "BOOM!";
            TxtCs2BombTimer.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
        }
        if (PbCs2BombProgress != null) PbCs2BombProgress.Value = 0;
    }

    private void ResetCs2BombUi()
    {
        if (MiniBombBadge != null) MiniBombBadge.Visibility = Visibility.Collapsed;
        if (TxtCs2BombTimer != null)
        {
            TxtCs2BombTimer.Text = "40.0s";
            TxtCs2BombTimer.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
        }
        if (TxtCs2BombState != null) TxtCs2BombState.Text = "BEKLENİYOR";
        if (PbCs2BombProgress != null) PbCs2BombProgress.Value = 100;
        if (BtnCs2TestSim != null) BtnCs2TestSim.Content = "🧪 40s Test Et";

        if (TxtCs2KitsizStatus != null)
        {
            TxtCs2KitsizStatus.Text = "✅ KİTSİZ ÇÖZEBİLİRSİN";
            TxtCs2KitsizStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 211, 153));
        }
        if (TxtCs2KitsizDeadline != null) TxtCs2KitsizDeadline.Text = "30.0s";
        if (BorderCs2KitsizCard != null)
        {
            BorderCs2KitsizCard.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(5, 150, 105));
            BorderCs2KitsizCard.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(6, 34, 23));
        }

        if (TxtCs2KitliStatus != null)
        {
            TxtCs2KitliStatus.Text = "✅ KİTLE ÇÖZEBİLİRSİN";
            TxtCs2KitliStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248));
        }
        if (TxtCs2KitliDeadline != null) TxtCs2KitliDeadline.Text = "35.0s";
        if (BorderCs2KitliCard != null)
        {
            BorderCs2KitliCard.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(2, 132, 199));
            BorderCs2KitliCard.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(12, 31, 56));
        }

        if (!_isExpanded && !_mediaService.CurrentInfo.IsPlaying && MiniCircleView.Visibility != Visibility.Visible)
        {
            CollapseIsland();
        }
    }

    private void BtnCs2InstallGsi_Click(object sender, RoutedEventArgs e)
    {
        bool success = Cs2GsiService.EnsureGsiConfigInstalled(out var msg);
        if (TxtCs2CfgStatus != null)
        {
            TxtCs2CfgStatus.Text = (success ? "✅ " : "⚠️ ") + msg;
            TxtCs2CfgStatus.Foreground = success 
                ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 211, 153))
                : new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 113, 113));
        }
        System.Windows.MessageBox.Show(msg, "CS2 Game State Kurulumu", MessageBoxButton.OK, success ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void BtnCs2TestSim_Click(object sender, RoutedEventArgs e)
    {
        if (_cs2GsiService.IsBombPlanted)
        {
            _cs2GsiService.StopTestSimulation();
            if (BtnCs2TestSim != null) BtnCs2TestSim.Content = "🧪 40s Test Et";
        }
        else
        {
            _cs2GsiService.StartTestSimulation();
            if (BtnCs2TestSim != null) BtnCs2TestSim.Content = "⏹️ Testi Durdur";
        }
    }

    private void ChkCs2Options_Changed(object sender, RoutedEventArgs e)
    {
        if (ChkCs2AutoExpand != null)
        {
            _cs2GsiService.AutoExpandOnPlant = ChkCs2AutoExpand.IsChecked == true;
        }
        if (ChkCs2Audio != null)
        {
            _cs2GsiService.EnableAudioAlerts = ChkCs2Audio.IsChecked == true;
        }
    }

    private void UpdateCs2CfgStatusUi()
    {
        if (TxtCs2CfgStatus == null) return;
        bool installed = Cs2GsiService.IsGsiConfigInstalled();
        TxtCs2CfgStatus.Text = installed 
            ? "🛡️ CS2 CFG Kurulu (VAC Güvenli • 0 Bellek Okuma)" 
            : "⚠️ CFG Kurulmadı (Yukarıdaki 'CFG Kur' butonuna basın)";
        TxtCs2CfgStatus.Foreground = installed 
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 211, 153)) 
            : new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11));
    }

    // ==========================================================
    // 🎨 ANIME MASCOT & CUSTOM PHOTO/GIF THEME CONTROLS
    // ==========================================================

    private void ApplyMascotTheme(string theme, string? customPath = null)
    {
        _currentAnimeTheme = theme;
        if (!string.IsNullOrEmpty(customPath)) _customImagePath = customPath;

        if (ChkShowMascot != null) ChkShowMascot.IsChecked = _showMascot;
        if (ChkShowBgImage != null) ChkShowBgImage.IsChecked = _showBgImage;
        if (ChkMascotDance != null) ChkMascotDance.IsChecked = _mascotDance;

        if (_currentAnimeTheme == "none")
        {
            _gifPlayer.Stop();
            if (BorderMascotPill != null) BorderMascotPill.Visibility = Visibility.Collapsed;
            if (BorderMascotMini != null) BorderMascotMini.Visibility = Visibility.Collapsed;
            if (ImgIslandBackground != null) ImgIslandBackground.Visibility = Visibility.Collapsed;
            if (TxtAnimeThemeStatus != null) TxtAnimeThemeStatus.Text = "Seçili: Yok";
            return;
        }

        if (_currentAnimeTheme == "custom" && !string.IsNullOrWhiteSpace(_customImagePath) && File.Exists(_customImagePath))
        {
            _gifPlayer.Load(_customImagePath);

            if (TxtMascotPillEmoji != null) TxtMascotPillEmoji.Visibility = Visibility.Collapsed;
            if (TxtMascotMiniEmoji != null) TxtMascotMiniEmoji.Visibility = Visibility.Collapsed;
            if (ImgMascotPill != null) ImgMascotPill.Visibility = Visibility.Visible;
            if (ImgMascotMini != null) ImgMascotMini.Visibility = Visibility.Visible;

            if (BorderMascotPill != null) BorderMascotPill.Visibility = _showMascot ? Visibility.Visible : Visibility.Collapsed;
            if (BorderMascotMini != null) BorderMascotMini.Visibility = _showMascot ? Visibility.Visible : Visibility.Collapsed;
            if (ImgIslandBackground != null) ImgIslandBackground.Visibility = _showBgImage ? Visibility.Visible : Visibility.Collapsed;

            if (TxtAnimeThemeStatus != null)
            {
                TxtAnimeThemeStatus.Text = $"Seçili: {Path.GetFileName(_customImagePath)}";
            }
            return;
        }

        // Built-in Anime Themes
        _gifPlayer.Stop();
        if (ImgMascotPill != null) ImgMascotPill.Visibility = Visibility.Collapsed;
        if (ImgMascotMini != null) ImgMascotMini.Visibility = Visibility.Collapsed;
        if (ImgIslandBackground != null) ImgIslandBackground.Visibility = Visibility.Collapsed;
        if (TxtMascotPillEmoji != null) TxtMascotPillEmoji.Visibility = Visibility.Visible;
        if (TxtMascotMiniEmoji != null) TxtMascotMiniEmoji.Visibility = Visibility.Visible;

        if (BorderMascotPill != null) BorderMascotPill.Visibility = _showMascot ? Visibility.Visible : Visibility.Collapsed;
        if (BorderMascotMini != null) BorderMascotMini.Visibility = _showMascot ? Visibility.Visible : Visibility.Collapsed;

        switch (_currentAnimeTheme)
        {
            case "neko":
                if (TxtMascotPillEmoji != null) TxtMascotPillEmoji.Text = "🐱";
                if (TxtMascotMiniEmoji != null) TxtMascotMiniEmoji.Text = "🐱";
                if (BorderMascotPill != null) BorderMascotPill.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 114, 182));
                ApplyTheme("sakura");
                if (TxtAnimeThemeStatus != null) TxtAnimeThemeStatus.Text = "🌸 Chibi Kedi";
                break;

            case "lofi":
                if (TxtMascotPillEmoji != null) TxtMascotPillEmoji.Text = "🎧";
                if (TxtMascotMiniEmoji != null) TxtMascotMiniEmoji.Text = "🎧";
                if (BorderMascotPill != null) BorderMascotPill.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(168, 85, 247));
                ApplyTheme("lofi");
                if (TxtAnimeThemeStatus != null) TxtAnimeThemeStatus.Text = "🎧 Lo-Fi Kız";
                break;

            case "cyber":
                if (TxtMascotPillEmoji != null) TxtMascotPillEmoji.Text = "⚡";
                if (TxtMascotMiniEmoji != null) TxtMascotMiniEmoji.Text = "⚡";
                if (BorderMascotPill != null) BorderMascotPill.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 229, 255));
                ApplyTheme("edgerunners");
                if (TxtAnimeThemeStatus != null) TxtAnimeThemeStatus.Text = "⚡ Cyberpunk";
                break;
        }
    }

    private void BtnChooseCustomImage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Dinamik Ada İçin Anime Resmi veya GIF Seç",
            Filter = "Resim ve GIF Dosyaları (*.gif;*.png;*.jpg;*.jpeg;*.webp)|*.gif;*.png;*.jpg;*.jpeg;*.webp|Tüm Dosyalar (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            _customImagePath = dlg.FileName;
            _currentAnimeTheme = "custom";
            _showMascot = true;
            ApplyMascotTheme("custom", _customImagePath);

            SaveConfigValue("customImagePath", _customImagePath);
            SaveConfigValue("animeTheme", "custom");
            SaveConfigValue("showMascot", _showMascot);
        }
    }

    private void BtnMascotPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string tag)
        {
            _currentAnimeTheme = tag;
            _showMascot = true;
            ApplyMascotTheme(tag);

            SaveConfigValue("animeTheme", _currentAnimeTheme);
            SaveConfigValue("showMascot", _showMascot);
        }
    }

    private void BtnClearMascot_Click(object sender, RoutedEventArgs e)
    {
        _currentAnimeTheme = "none";
        ApplyMascotTheme("none");
        SaveConfigValue("animeTheme", "none");
    }

    private void ChkMascotOptions_Changed(object sender, RoutedEventArgs e)
    {
        if (ChkShowMascot != null) _showMascot = ChkShowMascot.IsChecked == true;
        if (ChkShowBgImage != null) _showBgImage = ChkShowBgImage.IsChecked == true;
        if (ChkMascotDance != null) _mascotDance = ChkMascotDance.IsChecked == true;

        if (BorderMascotPill != null)
        {
            BorderMascotPill.Visibility = (_showMascot && _currentAnimeTheme != "none") ? Visibility.Visible : Visibility.Collapsed;
        }
        if (BorderMascotMini != null)
        {
            BorderMascotMini.Visibility = (_showMascot && _currentAnimeTheme != "none") ? Visibility.Visible : Visibility.Collapsed;
        }
        if (ImgIslandBackground != null)
        {
            ImgIslandBackground.Visibility = (_showBgImage && _currentAnimeTheme == "custom") ? Visibility.Visible : Visibility.Collapsed;
        }

        SaveConfigValue("showMascot", _showMascot);
        SaveConfigValue("showBgImage", _showBgImage);
        SaveConfigValue("mascotDance", _mascotDance);
    }

    // ========================================================
    // DYNAMICWIN / NOTCHNOOK FEATURES: FILE SHELF, QUICK DOCK, SYSTEM STATS
    // ========================================================

    private void Island_DragEnter(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(WpfDataFormats.FileDrop))
        {
            e.Effects = WpfDragDropEffects.Copy;
            if (!_isExpanded)
            {
                ExpandIsland();
            }
            if (TabFileShelf != null)
            {
                TabFileShelf.IsChecked = true;
                Tab_Checked(TabFileShelf, new RoutedEventArgs());
            }
            if (TxtFileShelfDropNotice != null)
            {
                TxtFileShelfDropNotice.Text = "📥 Bırakın, dosya hemen rafa eklensin!";
            }
            e.Handled = true;
        }
    }

    private void Island_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(WpfDataFormats.FileDrop))
        {
            e.Effects = WpfDragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void Island_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        if (TxtFileShelfDropNotice != null)
        {
            TxtFileShelfDropNotice.Text = "Dosyaları sürükleyip buraya bırakın";
        }
    }

    private void Island_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (TxtFileShelfDropNotice != null)
        {
            TxtFileShelfDropNotice.Text = "Dosyaları sürükleyip buraya bırakın";
        }

        if (e.Data.GetDataPresent(WpfDataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(WpfDataFormats.FileDrop);
            if (files != null)
            {
                foreach (var file in files)
                {
                    _fileShelfService.AddFile(file);
                }
            }
            e.Handled = true;
        }
    }

    private void RenderFileShelfItems()
    {
        if (PanelFileShelfList == null) return;
        PanelFileShelfList.Children.Clear();

        if (TxtFileShelfEmpty != null)
        {
            TxtFileShelfEmpty.Visibility = _fileShelfService.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        foreach (var item in _fileShelfService.Items)
        {
            var card = new Border
            {
                Width = 115,
                Height = 85,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 26, 38)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 46, 64)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(6),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = $"{item.FileName}\n{item.FilePath}\n(Tutun ve Discord/WhatsApp'a sürükleyin)"
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Top row: File Icon + Remove "x"
            var topGrid = new Grid();
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            if (item.Icon != null)
            {
                var img = new WpfImage
                {
                    Source = item.Icon,
                    Width = 18,
                    Height = 18,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 4, 0)
                };
                Grid.SetColumn(img, 0);
                topGrid.Children.Add(img);
            }
            else
            {
                var icoTxt = new TextBlock
                {
                    Text = item.IsDirectory ? "📁" : "📄",
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 4, 0)
                };
                Grid.SetColumn(icoTxt, 0);
                topGrid.Children.Add(icoTxt);
            }

            var btnRemove = new WpfButton
            {
                Content = "✕",
                FontSize = 9,
                Width = 16,
                Height = 16,
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(150, 150, 160)),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Rafdan Kaldır"
            };
            btnRemove.Click += (s, ev) => _fileShelfService.RemoveFile(item);
            Grid.SetColumn(btnRemove, 2);
            topGrid.Children.Add(btnRemove);

            Grid.SetRow(topGrid, 0);
            grid.Children.Add(topGrid);

            // Middle row: File name & size
            var middleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 0, 2) };
            var tbName = new TextBlock
            {
                Text = item.FileName,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = System.Windows.Media.Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var tbSize = new TextBlock
            {
                Text = item.FileSizeFormatted,
                FontSize = 8.5,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(120, 120, 140))
            };
            middleStack.Children.Add(tbName);
            middleStack.Children.Add(tbSize);
            Grid.SetRow(middleStack, 1);
            grid.Children.Add(middleStack);

            // Bottom row: "▶ Aç" and "📁"
            var bottomStack = new StackPanel { Orientation = WpfOrientation.Horizontal, HorizontalAlignment = WpfHorizontalAlignment.Center };
            var btnOpen = new WpfButton
            {
                Content = "▶ Aç",
                FontSize = 8.5,
                Padding = new Thickness(5, 1, 5, 1),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(36, 36, 52)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 3, 0)
            };
            btnOpen.Click += (s, ev) =>
            {
                try { Process.Start(new ProcessStartInfo { FileName = item.FilePath, UseShellExecute = true }); } catch { }
            };
            var btnExplore = new WpfButton
            {
                Content = "📁",
                FontSize = 8.5,
                Padding = new Thickness(4, 1, 4, 1),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(36, 36, 52)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Klasörde Göster"
            };
            btnExplore.Click += (s, ev) =>
            {
                try { Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"/select,\"{item.FilePath}\"", UseShellExecute = true }); } catch { }
            };
            bottomStack.Children.Add(btnOpen);
            bottomStack.Children.Add(btnExplore);
            Grid.SetRow(bottomStack, 2);
            grid.Children.Add(bottomStack);

            card.Child = grid;

            // DRAG OUT: Enables dragging this file directly out to Discord, browser, or folders!
            System.Windows.Point startPoint = new System.Windows.Point();
            card.PreviewMouseLeftButtonDown += (s, ev) => startPoint = ev.GetPosition(null);
            card.PreviewMouseMove += (s, ev) =>
            {
                if (ev.LeftButton == MouseButtonState.Pressed)
                {
                    System.Windows.Point currentPoint = ev.GetPosition(null);
                    System.Windows.Vector diff = startPoint - currentPoint;
                    if (Math.Abs(diff.X) > 5 || Math.Abs(diff.Y) > 5)
                    {
                        var data = new WpfDataObject(WpfDataFormats.FileDrop, new[] { item.FilePath });
                        DragDrop.DoDragDrop(card, data, WpfDragDropEffects.Copy);
                    }
                }
            };

            PanelFileShelfList.Children.Add(card);
        }
    }

    private void RenderQuickApps()
    {
        if (PanelQuickAppsList == null) return;
        PanelQuickAppsList.Children.Clear();

        foreach (var item in _quickAppsService.Items)
        {
            var btnCard = new Border
            {
                Width = 62,
                Height = 68,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 24, 34)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 58)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 7, 0),
                Padding = new Thickness(4),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = $"{item.Name} uygulamasını başlat"
            };

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = WpfHorizontalAlignment.Center };

            if (item.CustomIcon != null)
            {
                var img = new WpfImage
                {
                    Source = item.CustomIcon,
                    Width = 24,
                    Height = 24,
                    Margin = new Thickness(0, 0, 0, 4),
                    HorizontalAlignment = WpfHorizontalAlignment.Center
                };
                stack.Children.Add(img);
            }
            else
            {
                var tbEmoji = new TextBlock
                {
                    Text = item.IconEmoji,
                    FontSize = 20,
                    Margin = new Thickness(0, 0, 0, 4),
                    HorizontalAlignment = WpfHorizontalAlignment.Center
                };
                stack.Children.Add(tbEmoji);
            }

            var tbTitle = new TextBlock
            {
                Text = item.Name,
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(210, 210, 225)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                MaxWidth = 54
            };
            stack.Children.Add(tbTitle);

            btnCard.Child = stack;

            // Hover effect
            btnCard.MouseEnter += (s, ev) =>
            {
                btnCard.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(36, 36, 54));
                btnCard.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248));
            };
            btnCard.MouseLeave += (s, ev) =>
            {
                btnCard.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 24, 34));
                btnCard.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 58));
            };

            btnCard.MouseLeftButtonUp += (s, ev) => _quickAppsService.Launch(item);

            if (item.IsCustom)
            {
                var menu = new System.Windows.Controls.ContextMenu();
                var miRemove = new System.Windows.Controls.MenuItem { Header = "Kısayolu Kaldır" };
                miRemove.Click += (s, ev) => _quickAppsService.RemoveCustomApp(item);
                menu.Items.Add(miRemove);
                btnCard.ContextMenu = menu;
            }

            PanelQuickAppsList.Children.Add(btnCard);
        }
    }

    private void OnSystemStatsUpdated(SystemStatsInfo info)
    {
        Dispatcher.Invoke(() =>
        {
            if (TxtCpuUsage != null) TxtCpuUsage.Text = $"%{info.CpuPercent:0}";
            if (PbCpuUsage != null) PbCpuUsage.Value = info.CpuPercent;
            if (TxtCpuDetails != null)
            {
                TxtCpuDetails.Text = info.CpuPercent > 70 ? "🔥 Yüksek Yük" : (info.CpuPercent > 30 ? "⚡ Orta Yük" : "💤 Düşük Yük");
            }

            if (TxtRamUsage != null) TxtRamUsage.Text = $"%{info.RamPercent}";
            if (PbRamUsage != null) PbRamUsage.Value = info.RamPercent;
            if (TxtRamDetails != null) TxtRamDetails.Text = $"{info.RamUsedGb:F1} / {info.RamTotalGb:F1} GB";

            if (TxtBatteryPercent != null)
            {
                if (!info.HasBattery)
                {
                    if (TxtPowerIcon != null) TxtPowerIcon.Text = "⚡";
                    TxtBatteryPercent.Text = "Prizde / Masaüstü";
                }
                else
                {
                    if (TxtPowerIcon != null) TxtPowerIcon.Text = info.IsCharging ? "⚡" : "🔋";
                    TxtBatteryPercent.Text = $"%{info.BatteryPercent} {(info.IsCharging ? "(Şarjda)" : "")}";
                }
            }
        });
    }

    private void BtnAddFileShelf_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Dinamik Ada Dosya Rafına Ekle",
            Multiselect = true
        };
        if (ofd.ShowDialog() == true)
        {
            foreach (var f in ofd.FileNames)
            {
                _fileShelfService.AddFile(f);
            }
        }
    }

    private void BtnClearFileShelf_Click(object sender, RoutedEventArgs e)
    {
        _fileShelfService.ClearAll();
    }

    private void BtnAddCustomApp_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Kısayol Programı Seç (.exe)",
            Filter = "Uygulamalar (*.exe;*.lnk)|*.exe;*.lnk|Tüm Dosyalar (*.*)|*.*"
        };
        if (ofd.ShowDialog() == true)
        {
            _quickAppsService.AddCustomApp(ofd.FileName);
        }
    }

    private void BtnOpenTaskMgr_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = "taskmgr.exe", UseShellExecute = true });
        }
        catch { }
    }
}