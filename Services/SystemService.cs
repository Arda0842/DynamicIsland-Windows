using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace DynamicIsland.Services;

public class SystemStatsInfo
{
    public double CpuPercent { get; set; }
    public double RamUsedGb { get; set; }
    public double RamTotalGb { get; set; }
    public double RamPercent { get; set; }
    public int BatteryPercent { get; set; }
    public bool IsCharging { get; set; }
    public bool HasBattery { get; set; }
}

public class SystemService : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
        public ulong Value => ((ulong)dwHighDateTime << 32) | dwLowDateTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    private readonly DispatcherTimer _timer = new();
    private ulong _prevIdle = 0;
    private ulong _prevKernel = 0;
    private ulong _prevUser = 0;
    private bool _firstSample = true;

    public event Action<SystemStatsInfo>? StatsUpdated;

    public SystemService()
    {
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += Timer_Tick;
    }

    public void Start()
    {
        UpdateStats();
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        UpdateStats();
    }

    public SystemStatsInfo GetCurrentStats()
    {
        var info = new SystemStatsInfo();

        // 1. CPU Usage
        if (GetSystemTimes(out var idle, out var kernel, out var user))
        {
            ulong currentIdle = idle.Value;
            ulong currentKernel = kernel.Value;
            ulong currentUser = user.Value;

            if (!_firstSample)
            {
                ulong idleDiff = currentIdle - _prevIdle;
                ulong kernelDiff = currentKernel - _prevKernel;
                ulong userDiff = currentUser - _prevUser;
                ulong totalDiff = kernelDiff + userDiff;

                if (totalDiff > 0)
                {
                    double cpu = (1.0 - ((double)idleDiff / totalDiff)) * 100.0;
                    info.CpuPercent = Math.Clamp(Math.Round(cpu, 1), 0.0, 100.0);
                }
            }
            else
            {
                _firstSample = false;
                info.CpuPercent = 0.0;
            }

            _prevIdle = currentIdle;
            _prevKernel = currentKernel;
            _prevUser = currentUser;
        }

        // 2. RAM Usage
        var memStatus = new MEMORYSTATUSEX();
        if (GlobalMemoryStatusEx(memStatus))
        {
            double totalGb = Math.Round((double)memStatus.ullTotalPhys / (1024 * 1024 * 1024), 1);
            double availGb = Math.Round((double)memStatus.ullAvailPhys / (1024 * 1024 * 1024), 1);
            double usedGb = Math.Round(totalGb - availGb, 1);

            info.RamTotalGb = totalGb;
            info.RamUsedGb = usedGb;
            info.RamPercent = memStatus.dwMemoryLoad;
        }

        // 3. Battery Status
        try
        {
            var power = System.Windows.Forms.SystemInformation.PowerStatus;
            info.BatteryPercent = (int)Math.Round(power.BatteryLifePercent * 100);
            info.IsCharging = power.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online;
            info.HasBattery = power.BatteryChargeStatus != System.Windows.Forms.BatteryChargeStatus.NoSystemBattery;
        }
        catch
        {
            info.HasBattery = false;
            info.IsCharging = true;
        }

        return info;
    }

    private void UpdateStats()
    {
        var stats = GetCurrentStats();
        StatsUpdated?.Invoke(stats);
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
