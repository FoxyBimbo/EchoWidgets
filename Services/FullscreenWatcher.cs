using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace EchoUI.Services;

/// <summary>
/// Polls the foreground window to detect when a fullscreen application
/// (game, video, etc.) is active, and raises events so widgets can hide/show.
/// </summary>
public partial class FullscreenWatcher : IDisposable
{
    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int nIndex);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowLongW(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(IntPtr hWnd);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const uint WS_POPUP = 0x80000000;
    private const uint WS_CAPTION = 0x00C00000;
    private const uint WS_EX_TOPMOST = 0x00000008;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }

    private readonly DispatcherTimer _timer;
    private bool _isFullscreen;
    private bool _disposed;

    public event Action? FullscreenEntered;
    public event Action? FullscreenExited;

    public bool IsFullscreen => _isFullscreen;

    public FullscreenWatcher()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _timer.Tick += OnTick;
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void OnTick(object? sender, EventArgs e)
    {
        bool fullscreen = DetectFullscreen();

        if (fullscreen && !_isFullscreen)
        {
            _isFullscreen = true;
            FullscreenEntered?.Invoke();
        }
        else if (!fullscreen && _isFullscreen)
        {
            _isFullscreen = false;
            FullscreenExited?.Invoke();
        }
    }

    private static bool DetectFullscreen()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;
        if (!IsWindowVisible(hwnd)) return false;

        // Ignore our own process
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == (uint)Environment.ProcessId) return false;

        // Ignore tool windows and desktop
        uint exStyle = GetWindowLongW(hwnd, GWL_EXSTYLE);
        if ((exStyle & WS_EX_TOOLWINDOW) != 0) return false;

        // Check if the window covers the entire screen
        if (!GetWindowRect(hwnd, out var rc)) return false;
        int screenW = GetSystemMetrics(SM_CXSCREEN);
        int screenH = GetSystemMetrics(SM_CYSCREEN);

        bool coversScreen = rc.left <= 0 && rc.top <= 0
                         && rc.right >= screenW && rc.bottom >= screenH;

        if (!coversScreen) return false;

        // A maximized window with a caption (regular app) is not "fullscreen"
        // for our purposes — only borderless/popup fullscreen windows count.
        uint style = GetWindowLongW(hwnd, GWL_STYLE);
        bool hasCaption = (style & WS_CAPTION) == WS_CAPTION;

        return !hasCaption;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        GC.SuppressFinalize(this);
    }
}
