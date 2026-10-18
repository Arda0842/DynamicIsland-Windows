using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace DynamicIsland;

public partial class CrosshairWindow : Window
{
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020; // Click-through: mouse clicks pass directly into games
    private const int WS_EX_TOOLWINDOW = 0x00000080;  // Hidden from Alt+Tab
    private const int WS_EX_NOACTIVATE = 0x08000000;  // Never steals focus
    private const int WS_EX_LAYERED = 0x00080000;

    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    public int OffsetX { get; set; } = 0;
    public int OffsetY { get; set; } = 0;

    public CrosshairWindow()
    {
        InitializeComponent();
        Loaded += (s, e) => EnsureCenterPosition();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;

        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED);

        EnsureCenterPosition();
    }

    public void EnsureCenterPosition()
    {
        // 1. Get the actual DPI scale factor for this window
        double dpiScaleX = 1.0;
        double dpiScaleY = 1.0;
        try
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            if (dpi.DpiScaleX > 0) dpiScaleX = dpi.DpiScaleX;
            if (dpi.DpiScaleY > 0) dpiScaleY = dpi.DpiScaleY;
        }
        catch { }

        // 2. Primary monitor dimensions in physical device pixels
        double physicalScreenWidth = SystemParameters.PrimaryScreenWidth * dpiScaleX;
        double physicalScreenHeight = SystemParameters.PrimaryScreenHeight * dpiScaleY;

        double physicalWidth = Width * dpiScaleX;
        double physicalHeight = Height * dpiScaleY;

        // 3. Exact physical pixel position on the screen
        int physicalX = (int)Math.Round((physicalScreenWidth - physicalWidth) / 2.0) + OffsetX;
        int physicalY = (int)Math.Round((physicalScreenHeight - physicalHeight) / 2.0) + OffsetY;

        // 4. Also synchronize WPF logical Left and Top
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2.0 + (OffsetX / dpiScaleX);
        Top = (SystemParameters.PrimaryScreenHeight - Height) / 2.0 + (OffsetY / dpiScaleY);

        // 5. Win32 SetWindowPos with true physical screen coordinates and HWND_TOPMOST
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            SetWindowPos(hwnd, HWND_TOPMOST, physicalX, physicalY, (int)Math.Round(physicalWidth), (int)Math.Round(physicalHeight), SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }
    }

    public void ApplySettings(string style, double size, Color color, double opacity, int offX = 0, int offY = 0)
    {
        OffsetX = offX;
        OffsetY = offY;
        EnsureCenterPosition();

        var brush = new SolidColorBrush(color);

        CrossDotView.Visibility = style == "dot" ? Visibility.Visible : Visibility.Collapsed;
        CrossRingView.Visibility = style == "ring" ? Visibility.Visible : Visibility.Collapsed;
        CrossPlusView.Visibility = style == "plus" ? Visibility.Visible : Visibility.Collapsed;
        CrossSniperView.Visibility = style == "sniper" ? Visibility.Visible : Visibility.Collapsed;

        // Dot Style
        DotMain.Width = size;
        DotMain.Height = size;
        DotOutline.Width = size + 2;
        DotOutline.Height = size + 2;
        DotMain.Fill = brush;

        // Ring Style
        RingMain.Width = size * 3;
        RingMain.Height = size * 3;
        RingOutline.Width = size * 3 + 2;
        RingOutline.Height = size * 3 + 2;
        RingMain.Stroke = brush;
        RingCenterDot.Fill = brush;

        // Plus Style
        LineTop.Stroke = brush;
        LineBottom.Stroke = brush;
        LineLeft.Stroke = brush;
        LineRight.Stroke = brush;
        PlusCenterDot.Fill = brush;

        // Sniper Glow Style
        SniperMain.Width = Math.Max(2, size);
        SniperMain.Height = Math.Max(2, size);
        SniperOutline.Width = Math.Max(2, size) + 2;
        SniperOutline.Height = Math.Max(2, size) + 2;
        SniperGlow.Width = size * 3.5;
        SniperGlow.Height = size * 3.5;
        SniperMain.Fill = brush;
        SniperGlow.Fill = new SolidColorBrush(Color.FromArgb(64, color.R, color.G, color.B));

        Opacity = opacity;
    }
}
