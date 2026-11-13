using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using System.Windows.Threading;
using CraftNotch.Models;
using Windows.System.Power;

namespace CraftNotch.Services;

public class SystemMonitorService
{
    private readonly DispatcherTimer _timer;
    private ulong _prevIdleTime;
    private ulong _prevKernelTime;
    private ulong _prevUserTime;

    private bool _prevCapsLock;
    private bool _prevNumLock;
    private int _pingCounter;

    public int CpuPercentage { get; private set; }
    public int RamPercentage { get; private set; }
    public int BatteryPercentage { get; private set; } = 100;
    public bool IsCharging { get; private set; }
    public bool HasBattery { get; private set; }
    public int PingMs { get; private set; } = 24;
    public string PingStatus { get; private set; } = "Good";

    public event Action<int, int>? PerformanceUpdated;
    public event Action<int, bool>? BatteryUpdated;
    public event Action<string, bool>? LockKeyToggled; // keyName, isOn
    public event Action<int, string>? PingUpdated;

    public SystemMonitorService()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (s, e) => PollSystem();
        InitCpuTimes();
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void InitCpuTimes()
    {
        if (Win32Helper.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            _prevIdleTime = ToUInt64(idle);
            _prevKernelTime = ToUInt64(kernel);
            _prevUserTime = ToUInt64(user);
        }
        _prevCapsLock = (Win32Helper.GetKeyState(Win32Helper.VK_CAPITAL) & 1) != 0;
        _prevNumLock = (Win32Helper.GetKeyState(Win32Helper.VK_NUMLOCK) & 1) != 0;
    }

    private void PollSystem()
    {
        // 1. CPU Usage
        if (Win32Helper.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            ulong idleTime = ToUInt64(idle);
            ulong kernelTime = ToUInt64(kernel);
            ulong userTime = ToUInt64(user);

            ulong usr = userTime - _prevUserTime;
            ulong ker = kernelTime - _prevKernelTime;
            ulong idl = idleTime - _prevIdleTime;

            ulong sys = ker + usr;
            if (sys > 0)
            {
                CpuPercentage = (int)Math.Clamp((sys - idl) * 100 / sys, 0, 100);
            }

            _prevIdleTime = idleTime;
            _prevKernelTime = kernelTime;
            _prevUserTime = userTime;
        }

        // 2. RAM Usage
        var memStatus = new Win32Helper.MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32Helper.MEMORYSTATUSEX>() };
        if (Win32Helper.GlobalMemoryStatusEx(ref memStatus))
        {
            RamPercentage = (int)memStatus.dwMemoryLoad;
        }

        PerformanceUpdated?.Invoke(CpuPercentage, RamPercentage);

        // 3. Battery & Power
        try
        {
            var batteryStatus = PowerManager.BatteryStatus;
            HasBattery = batteryStatus != BatteryStatus.NotPresent;
            if (HasBattery)
            {
                BatteryPercentage = PowerManager.RemainingChargePercent;
                IsCharging = PowerManager.PowerSupplyStatus != PowerSupplyStatus.NotPresent && 
                             (batteryStatus == BatteryStatus.Charging || PowerManager.PowerSupplyStatus == PowerSupplyStatus.Adequate);
                BatteryUpdated?.Invoke(BatteryPercentage, IsCharging);
            }
        }
        catch { }

        // 4. Lock Keys
        bool caps = (Win32Helper.GetKeyState(Win32Helper.VK_CAPITAL) & 1) != 0;
        if (caps != _prevCapsLock)
        {
            _prevCapsLock = caps;
            LockKeyToggled?.Invoke("Caps Lock", caps);
        }

        bool num = (Win32Helper.GetKeyState(Win32Helper.VK_NUMLOCK) & 1) != 0;
        if (num != _prevNumLock)
        {
            _prevNumLock = num;
            LockKeyToggled?.Invoke("Num Lock", num);
        }

        // 5. Ping & Network latency check (every 5 seconds)
        if (++_pingCounter % 5 == 0)
        {
            _ = CheckPingAsync();
        }
    }

    private async Task CheckPingAsync()
    {
        try
        {
            using var pinger = new Ping();
            var reply = await pinger.SendPingAsync("1.1.1.1", 1200);
            if (reply.Status == IPStatus.Success)
            {
                PingMs = (int)reply.RoundtripTime;
                PingStatus = PingMs < 60 ? "Good" : (PingMs < 140 ? "Fair" : "Poor");
            }
            else
            {
                PingStatus = "Offline";
            }
        }
        catch
        {
            PingStatus = "Offline";
        }

        PingUpdated?.Invoke(PingMs, PingStatus);
    }

    private static ulong ToUInt64(FILETIME ft)
    {
        return ((ulong)(uint)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
    }
}
