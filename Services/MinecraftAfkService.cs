using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace DynamicIsland.Services;

public class MinecraftAfkState
{
    public bool IsRunning { get; set; }
    public bool IsEating { get; set; }
    public bool IsMinecraftFocused { get; set; }
    public int SecondsUntilNextHit { get; set; }
    public int SecondsUntilNextEat { get; set; }
    public int TotalHits { get; set; }
    public int TotalEats { get; set; }
    public TimeSpan ElapsedTime { get; set; }
}

public class MinecraftAfkService : IDisposable
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

    private readonly DispatcherTimer _ticker = new();
    private DateTime _startTime = DateTime.MinValue;

    public int HitIntervalSeconds { get; set; } = 10; // 10 saniyede bir
    public int HitsPerCycle { get; set; } = 2; // Her döngüde 2 vuruş
    public int DelayBetweenHitsMs { get; set; } = 625; // İki vuruş arası kılıç bekleme süresi (tam hasar için)
    public int EatIntervalSeconds { get; set; } = 600; // 10 dakikada (600 sn) 1 yemek yeme
    public int EatHoldDurationMs { get; set; } = 2200; // 2.2 saniye sağ tık basılı tutma
    public bool OnlyWhenMinecraftActive { get; set; } = true;

    public bool IsRunning { get; private set; } = false;
    public bool IsEating { get; private set; } = false;
    public int SecondsUntilNextHit { get; private set; } = 10;
    public int SecondsUntilNextEat { get; private set; } = 600;
    public int TotalHits { get; private set; } = 0;
    public int TotalEats { get; private set; } = 0;

    public event Action<MinecraftAfkState>? StateChanged;
    public event Action? HitExecuted;
    public event Action? EatExecuted;

    public MinecraftAfkService()
    {
        _ticker.Interval = TimeSpan.FromSeconds(1);
        _ticker.Tick += Ticker_Tick;
    }

    public void Start()
    {
        if (IsRunning) return;
        IsRunning = true;
        _startTime = DateTime.UtcNow;
        SecondsUntilNextHit = HitIntervalSeconds;
        SecondsUntilNextEat = EatIntervalSeconds;
        _ticker.Start();
        NotifyState();
    }

    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;
        IsEating = false;
        _ticker.Stop();
        
        // Safety: Release any held mouse buttons
        mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        NotifyState();
    }

    public void Toggle()
    {
        if (IsRunning) Stop();
        else Start();
    }

    public static bool IsMinecraftWindowActive()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;
            var sb = new StringBuilder(256);
            if (GetWindowText(hwnd, sb, 256) > 0)
            {
                var title = sb.ToString().ToLowerInvariant();
                return title.Contains("minecraft") || 
                       title.Contains("lunar client") || 
                       title.Contains("badlion") || 
                       title.Contains("feather") || 
                       title.Contains("prism launcher");
            }
        }
        catch { }
        return false;
    }

    private async void Ticker_Tick(object? sender, EventArgs e)
    {
        if (!IsRunning) return;

        bool mcActive = IsMinecraftWindowActive();

        // If OnlyWhenMinecraftActive is enabled and MC is not active, wait
        if (OnlyWhenMinecraftActive && !mcActive)
        {
            NotifyState();
            return;
        }

        if (IsEating)
        {
            NotifyState();
            return;
        }

        // Count down
        SecondsUntilNextEat--;
        SecondsUntilNextHit--;

        // Check Eat interval (every 10 minutes)
        if (SecondsUntilNextEat <= 0)
        {
            SecondsUntilNextEat = EatIntervalSeconds;
            await PerformEatAsync();
        }
        else if (SecondsUntilNextHit <= 0)
        {
            SecondsUntilNextHit = HitIntervalSeconds;
            await PerformHitsSequenceAsync(HitsPerCycle, DelayBetweenHitsMs);
        }

        NotifyState();
    }

    public async Task PerformHitsSequenceAsync(int hitCount = 2, int delayMs = 625)
    {
        for (int i = 0; i < hitCount; i++)
        {
            try
            {
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                await Task.Delay(45);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);

                TotalHits++;
                HitExecuted?.Invoke();
            }
            catch { }

            if (i < hitCount - 1)
            {
                await Task.Delay(delayMs);
            }
        }
    }

    public async Task PerformHitAsync()
    {
        await PerformHitsSequenceAsync(HitsPerCycle, DelayBetweenHitsMs);
    }

    public async Task PerformEatAsync()
    {
        if (IsEating) return;
        IsEating = true;
        NotifyState();

        try
        {
            mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, UIntPtr.Zero);
            await Task.Delay(EatHoldDurationMs);
            mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);

            TotalEats++;
            EatExecuted?.Invoke();
        }
        catch { }
        finally
        {
            mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
            IsEating = false;
            NotifyState();
        }
    }

    public void ResetStats()
    {
        TotalHits = 0;
        TotalEats = 0;
        _startTime = IsRunning ? DateTime.UtcNow : DateTime.MinValue;
        NotifyState();
    }

    public void NotifyState()
    {
        var elapsed = IsRunning && _startTime != DateTime.MinValue 
            ? DateTime.UtcNow - _startTime 
            : TimeSpan.Zero;

        StateChanged?.Invoke(new MinecraftAfkState
        {
            IsRunning = IsRunning,
            IsEating = IsEating,
            IsMinecraftFocused = IsMinecraftWindowActive(),
            SecondsUntilNextHit = Math.Max(0, SecondsUntilNextHit),
            SecondsUntilNextEat = Math.Max(0, SecondsUntilNextEat),
            TotalHits = TotalHits,
            TotalEats = TotalEats,
            ElapsedTime = elapsed
        });
    }

    public void Dispose()
    {
        Stop();
    }
}
