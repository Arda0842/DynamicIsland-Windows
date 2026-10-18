using System.Runtime.InteropServices;

namespace DynamicIsland.Services;

public static class VolumeService
{
    private const byte VK_VOLUME_MUTE = 0xAD;
    private const byte VK_VOLUME_DOWN = 0xAE;
    private const byte VK_VOLUME_UP = 0xAF;

    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

    private static void PressKey(byte key)
    {
        keybd_event(key, 0, KEYEVENTF_EXTENDEDKEY, 0);
        keybd_event(key, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
    }

    public static void VolumeUp(int steps = 5)
    {
        for (int i = 0; i < steps; i++)
        {
            PressKey(VK_VOLUME_UP);
        }
    }

    public static void VolumeDown(int steps = 5)
    {
        for (int i = 0; i < steps; i++)
        {
            PressKey(VK_VOLUME_DOWN);
        }
    }

    public static void ToggleMute()
    {
        PressKey(VK_VOLUME_MUTE);
    }
}
