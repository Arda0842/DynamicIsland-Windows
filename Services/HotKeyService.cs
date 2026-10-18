using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace DynamicIsland.Services;

public class HotKeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    private const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private HwndSource? _source;
    private IntPtr _windowHandle;

    public const int HOTKEY_ID_TOGGLE = 9001;
    public const int HOTKEY_ID_PLAYPAUSE = 9002;
    public const int HOTKEY_ID_VOICE = 9003;
    public const int HOTKEY_ID_MOUSE = 9004;
    public const int HOTKEY_ID_MC_AFK = 9005;
    public const int HOTKEY_ID_CROSSHAIR = 9006;

    public event Action? ToggleRequested;
    public event Action? PlayPauseRequested;
    public event Action? VoiceRequested;
    public event Action? MouseTeleportRequested;
    public event Action? McAfkRequested;
    public event Action? CrosshairRequested;

    public void Register(Window window, string toggle = "Alt+D", string mouse = "Alt+M", string voice = "Alt+V", string play = "Alt+P", string mcAfk = "Alt+K", string crosshair = "Alt+X")
    {
        var helper = new WindowInteropHelper(window);
        _windowHandle = helper.Handle;
        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(HwndHook);

        ReRegister(toggle, mouse, voice, play, mcAfk, crosshair);
    }

    public void ReRegister(string toggle, string mouse, string voice, string play, string mcAfk = "Alt+K", string crosshair = "Alt+X")
    {
        if (_windowHandle == IntPtr.Zero) return;

        // Unregister previous
        UnregisterHotKey(_windowHandle, HOTKEY_ID_TOGGLE);
        UnregisterHotKey(_windowHandle, HOTKEY_ID_PLAYPAUSE);
        UnregisterHotKey(_windowHandle, HOTKEY_ID_VOICE);
        UnregisterHotKey(_windowHandle, HOTKEY_ID_MOUSE);
        UnregisterHotKey(_windowHandle, HOTKEY_ID_MC_AFK);
        UnregisterHotKey(_windowHandle, HOTKEY_ID_CROSSHAIR);

        // Register new
        RegisterSingleKey(HOTKEY_ID_TOGGLE, toggle, 0x44); // default D
        RegisterSingleKey(HOTKEY_ID_MOUSE, mouse, 0x4D);   // default M
        RegisterSingleKey(HOTKEY_ID_VOICE, voice, 0x56);   // default V
        RegisterSingleKey(HOTKEY_ID_PLAYPAUSE, play, 0x50);// default P
        RegisterSingleKey(HOTKEY_ID_MC_AFK, mcAfk, 0x4B);  // default K
        RegisterSingleKey(HOTKEY_ID_CROSSHAIR, crosshair, 0x58); // default X
    }

    private void RegisterSingleKey(int id, string shortcutStr, uint fallbackVk)
    {
        var (mod, vk) = ParseShortcut(shortcutStr, fallbackVk);
        RegisterHotKey(_windowHandle, id, mod | MOD_NOREPEAT, vk);
    }

    public static (uint Modifiers, uint VirtualKey) ParseShortcut(string shortcut, uint defaultVk)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
        {
            return (MOD_ALT, defaultVk);
        }

        uint mod = 0;
        var parts = shortcut.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string keyPart = "";

        foreach (var part in parts)
        {
            var p = part.ToLowerInvariant();
            if (p == "alt") mod |= MOD_ALT;
            else if (p == "ctrl" || p == "control") mod |= MOD_CONTROL;
            else if (p == "shift") mod |= MOD_SHIFT;
            else keyPart = part.ToUpperInvariant();
        }

        if (mod == 0) mod = MOD_ALT; // default modifier

        uint vk = defaultVk;
        if (!string.IsNullOrEmpty(keyPart))
        {
            if (Enum.TryParse<Key>(keyPart, true, out var wpfKey))
            {
                var converted = (uint)KeyInterop.VirtualKeyFromKey(wpfKey);
                if (converted != 0) vk = converted;
            }
            else if (keyPart.Length == 1)
            {
                vk = (uint)keyPart[0];
            }
        }

        return (mod, vk);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == HOTKEY_ID_TOGGLE)
            {
                ToggleRequested?.Invoke();
                handled = true;
            }
            else if (id == HOTKEY_ID_PLAYPAUSE)
            {
                PlayPauseRequested?.Invoke();
                handled = true;
            }
            else if (id == HOTKEY_ID_VOICE)
            {
                VoiceRequested?.Invoke();
                handled = true;
            }
            else if (id == HOTKEY_ID_MOUSE)
            {
                MouseTeleportRequested?.Invoke();
                handled = true;
            }
            else if (id == HOTKEY_ID_MC_AFK)
            {
                McAfkRequested?.Invoke();
                handled = true;
            }
            else if (id == HOTKEY_ID_CROSSHAIR)
            {
                CrosshairRequested?.Invoke();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_windowHandle, HOTKEY_ID_TOGGLE);
            UnregisterHotKey(_windowHandle, HOTKEY_ID_PLAYPAUSE);
            UnregisterHotKey(_windowHandle, HOTKEY_ID_VOICE);
            UnregisterHotKey(_windowHandle, HOTKEY_ID_MOUSE);
            UnregisterHotKey(_windowHandle, HOTKEY_ID_MC_AFK);
            UnregisterHotKey(_windowHandle, HOTKEY_ID_CROSSHAIR);
        }
        _source?.RemoveHook(HwndHook);
    }
}
