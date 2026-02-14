using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace EchoUI.Services;

public static partial class ShellInterop
{
    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial IntPtr FindWindowW([MarshalAs(UnmanagedType.LPWStr)] string? lpClassName,
                                              [MarshalAs(UnmanagedType.LPWStr)] string? lpWindowName);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial IntPtr FindWindowExW(IntPtr hwndParent, IntPtr hwndChildAfter,
                                                [MarshalAs(UnmanagedType.LPWStr)] string? lpszClass,
                                                [MarshalAs(UnmanagedType.LPWStr)] string? lpszWindow);

    [LibraryImport("user32.dll")]
    private static partial IntPtr SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfoW(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int nIndex);

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfoW(IntPtr hMonitor, ref MONITORINFO lpmi);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight,
                                           [MarshalAs(UnmanagedType.Bool)] bool bRepaint);

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const uint SPI_GETWORKAREA = 0x0030;
    private const uint SPI_SETWORKAREA = 0x002F;
    private const uint SPIF_SENDCHANGE = 0x0002;
    private const uint MONITOR_DEFAULTTOPRIMARY = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private static RECT _originalWorkArea;
    private static bool _workAreaModified;

    // ── Windows Taskbar Hide / Show ─────────────────────────
    public static void HideWindowsTaskbar()
    {
        var hwnd = FindWindowW("Shell_TrayWnd", null);
        if (hwnd != IntPtr.Zero)
            ShowWindow(hwnd, SW_HIDE);

        var hwnd2 = FindWindowW("Shell_SecondaryTrayWnd", null);
        if (hwnd2 != IntPtr.Zero)
            ShowWindow(hwnd2, SW_HIDE);
    }

    public static void ShowWindowsTaskbar()
    {
        var hwnd = FindWindowW("Shell_TrayWnd", null);
        if (hwnd != IntPtr.Zero)
            ShowWindow(hwnd, SW_SHOW);

        var hwnd2 = FindWindowW("Shell_SecondaryTrayWnd", null);
        if (hwnd2 != IntPtr.Zero)
            ShowWindow(hwnd2, SW_SHOW);
    }

    // ── Work Area Reservation ───────────────────────────────
    // Shrinks the desktop work area so that maximized windows
    // leave room for the EchoUI bar at the bottom. Uses real
    // screen pixels (not WPF DIPs) to avoid DPI-scaling issues.

    public static void ReserveWorkArea(int barHeightPx)
    {
        // Save the current work area so we can restore it later
        var current = new RECT();
        SystemParametersInfoW(SPI_GETWORKAREA, 0, ref current, 0);
        _originalWorkArea = current;

        int screenW = GetSystemMetrics(SM_CXSCREEN);
        int screenH = GetSystemMetrics(SM_CYSCREEN);

        var reserved = new RECT
        {
            left = 0,
            top = 0,
            right = screenW,
            bottom = screenH - barHeightPx
        };

        SystemParametersInfoW(SPI_SETWORKAREA, 0, ref reserved, SPIF_SENDCHANGE);
        _workAreaModified = true;
    }

    public static void RestoreWorkArea()
    {
        if (!_workAreaModified) return;
        SystemParametersInfoW(SPI_SETWORKAREA, 0, ref _originalWorkArea, SPIF_SENDCHANGE);
        _workAreaModified = false;
    }

    // ── Position the EchoUI window using raw pixels ─────────
    // WPF positions use DIPs; we bypass that by calling
    // MoveWindow directly with pixel coordinates so the bar
    // sits flush at the true bottom of the screen.

    public static void PositionBarAtBottom(Window window, int barHeightPx)
    {
        var helper = new WindowInteropHelper(window);
        var hwnd = helper.Handle;
        if (hwnd == IntPtr.Zero) return;

        int screenW = GetSystemMetrics(SM_CXSCREEN);
        int screenH = GetSystemMetrics(SM_CYSCREEN);

        MoveWindow(hwnd, 0, screenH - barHeightPx, screenW, barHeightPx, true);
    }

    // ── DPI helper ──────────────────────────────────────────
    // Returns the pixel height that corresponds to the desired
    // WPF height, accounting for the system DPI scale factor.

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForSystem();

    public static int DipToPixel(double dip)
    {
        double dpi = GetDpiForSystem();
        return (int)Math.Round(dip * dpi / 96.0);
    }

    public static void OpenStartMenu()
    {
        try
        {
            // Simulate Win key press via keybd_event
            keybd_event(0x5B, 0, 0, UIntPtr.Zero); // VK_LWIN down
            keybd_event(0x5B, 0, 2, UIntPtr.Zero); // VK_LWIN up
        }
        catch { }
    }

    [LibraryImport("user32.dll")]
    private static partial void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    public static void ToggleSystemTray()
    {
        try
        {
            // Find the system tray overflow window and click it
            var tray = FindWindowW("Shell_TrayWnd", null);
            if (tray != IntPtr.Zero)
            {
                var notify = FindWindowExW(tray, IntPtr.Zero, "TrayNotifyWnd", null);
                if (notify != IntPtr.Zero)
                {
                    var chevron = FindWindowExW(notify, IntPtr.Zero, "Button", null);
                    if (chevron != IntPtr.Zero)
                    {
                        SetForegroundWindow(chevron);
                        SendMessageW(chevron, 0x0201, IntPtr.Zero, IntPtr.Zero); // WM_LBUTTONDOWN
                        SendMessageW(chevron, 0x0202, IntPtr.Zero, IntPtr.Zero); // WM_LBUTTONUP
                        return;
                    }
                }
            }
            // Fallback: open notification area icons settings
            Process.Start(new ProcessStartInfo("ms-settings:taskbar") { UseShellExecute = true });
        }
        catch { }
    }

    public static void ToggleNotificationCenter()
    {
        try
        {
            // Win+N opens Windows notification center
            keybd_event(0x5B, 0, 0, UIntPtr.Zero);
            keybd_event(0x4E, 0, 0, UIntPtr.Zero); // 'N'
            keybd_event(0x4E, 0, 2, UIntPtr.Zero);
            keybd_event(0x5B, 0, 2, UIntPtr.Zero);
        }
        catch { }
    }

    public static void LaunchApp(string exePath)
    {
        try
        {
            Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
        }
        catch { }
    }
}
