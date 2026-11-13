using System;
using System.Windows;
using System.Windows.Threading;

namespace Scout.Services;

public class OcclusionService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private bool _isCurrentlyFullscreen;

    public bool IsFullscreenActive => _isCurrentlyFullscreen;
    public bool Enabled { get; set; } = true;

    public event EventHandler<bool>? FullscreenChanged;

    public OcclusionService()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(800)
        };
        _timer.Tick += Timer_Tick;
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!Enabled) return;

        bool isFs = CheckIfForegroundIsFullscreen();
        if (isFs != _isCurrentlyFullscreen)
        {
            _isCurrentlyFullscreen = isFs;
            FullscreenChanged?.Invoke(this, isFs);
        }
    }

    private bool CheckIfForegroundIsFullscreen()
    {
        try
        {
            var hWnd = Win32Helper.GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return false;

            // Ignore Desktop and Shell windows
            if (hWnd == Win32Helper.GetDesktopWindow() || hWnd == Win32Helper.GetShellWindow())
            {
                return false;
            }

            if (Win32Helper.GetWindowRect(hWnd, out var rect))
            {
                int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
                int screenHeight = (int)SystemParameters.PrimaryScreenHeight;

                int windowWidth = rect.Right - rect.Left;
                int windowHeight = rect.Bottom - rect.Top;

                // Fullscreen window covers screen width and height
                if (rect.Left <= 0 && rect.Top <= 0 &&
                    windowWidth >= screenWidth && windowHeight >= screenHeight)
                {
                    return true;
                }
            }
        }
        catch { }

        return false;
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
