using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using EchoUI.Models;
using EchoUI.Services;

namespace EchoUI.Views;

public partial class FullScreenShellWidget : Window, INotifyPropertyChanged
{
    private readonly string _widgetId;
    private readonly WidgetSettings _widgetSettings;
    private readonly AppSettings _appSettings;
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _windowTimer;
    private string _currentTime = string.Empty;
    private string _currentDate = string.Empty;
    private DateTime _selectedCalendarDate = DateTime.Today;
    private readonly LowLevelKeyboardProc _keyboardProc;
    private IntPtr _keyboardHook = IntPtr.Zero;
    private bool _defaultTopmost;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> OpenWindowTitles { get; } = [];
    public ObservableCollection<BrowserLink> BrowserLinks { get; } = [];

    public string CurrentTime
    {
        get => _currentTime;
        private set
        {
            if (_currentTime == value) return;
            _currentTime = value;
            OnPropertyChanged();
        }
    }

    public string CurrentDate
    {
        get => _currentDate;
        private set
        {
            if (_currentDate == value) return;
            _currentDate = value;
            OnPropertyChanged();
        }
    }

    public DateTime SelectedCalendarDate
    {
        get => _selectedCalendarDate;
        set
        {
            if (_selectedCalendarDate == value) return;
            _selectedCalendarDate = value;
            OnPropertyChanged();
        }
    }

    public string WidgetId => _widgetId;

    public FullScreenShellWidget() : this("FullScreenShell", new WidgetSettings { Topmost = false }, new AppSettings()) { }

    public FullScreenShellWidget(string widgetId, WidgetSettings settings, AppSettings appSettings)
    {
        InitializeComponent();
        _widgetId = widgetId;
        _widgetSettings = settings;
        _appSettings = appSettings;
        DataContext = this;
        _keyboardProc = KeyboardHookCallback;

        ApplyWidgetSettingsFromModel();
        ConfigureBrowserLinks();
        UpdateClock();
        UpdateOpenWindows();

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();

        _windowTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _windowTimer.Tick += (_, _) => UpdateOpenWindows();
        _windowTimer.Start();

        Closed += Window_Closed;
    }

    private WidgetSettings SyncWidgetSettings()
    {
        var ws = _appSettings.GetWidgetSettings(_widgetId);
        _widgetSettings.Kind = ws.Kind;
        _widgetSettings.Opacity = ws.Opacity;
        _widgetSettings.Topmost = ws.Topmost;
        _widgetSettings.Custom = ws.Custom;
        _widgetSettings.Shortcuts = ws.Shortcuts;
        _widgetSettings.ViewMode = ws.ViewMode;
        _widgetSettings.CustomColors = ws.CustomColors;
        return ws;
    }

    private void ApplyWidgetSettingsFromModel()
    {
        var ws = SyncWidgetSettings();
        ThemeHelper.ApplyToElement(this, ws.CustomColors);
        Topmost = ws.Topmost;
        _defaultTopmost = ws.Topmost;
        Opacity = ws.Opacity;
    }

    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        Topmost = true;
    }

    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        Topmost = _defaultTopmost;
    }

    private void ConfigureBrowserLinks()
    {
        BrowserLinks.Clear();
        BrowserLinks.Add(new BrowserLink("Open Browser", "https://www.bing.com", "🌐"));
        BrowserLinks.Add(new BrowserLink("EchoUI Home", "https://github.com/FoxyBimbo/EchoWidgets", "🏠"));
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        var timeFormat = _appSettings.Use24HourClock ? "HH:mm" : "hh:mm";
        if (_appSettings.ShowSeconds)
            timeFormat += ":ss";

        CurrentTime = now.ToString(timeFormat);
        CurrentDate = now.ToString("ddd, MMM dd");
    }

    private void UpdateOpenWindows()
    {
        var titles = Process.GetProcesses()
            .Select(p => p.MainWindowTitle)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .OrderBy(t => t, StringComparer.CurrentCultureIgnoreCase)
            .Take(10)
            .ToList();

        OpenWindowTitles.Clear();
        foreach (var title in titles)
            OpenWindowTitles.Add(title);
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _clockTimer.Stop();
        _windowTimer.Stop();
        UnhookKeyboard();
    }

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        WindowState = WindowState.Maximized;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Focus();
        HookKeyboard();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is System.Windows.Input.Key.LWin or System.Windows.Input.Key.RWin)
        {
            CloseTransientPanels();
            StartMenuPopup.IsOpen = true;
            e.Handled = true;
        }
    }

    private void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        CloseTransientPanels();
        StartMenuPopup.IsOpen = !StartMenuPopup.IsOpen;
    }

    private void BtnTray_Click(object sender, RoutedEventArgs e)
    {
        CloseTransientPanels();
        TrayPopup.IsOpen = !TrayPopup.IsOpen;
    }

    private void BtnDate_Click(object sender, RoutedEventArgs e)
    {
        CloseTransientPanels();
        CalendarPopup.IsOpen = !CalendarPopup.IsOpen;
    }

    private void BtnTime_Click(object sender, RoutedEventArgs e)
    {
        CloseTransientPanels();
        ClockPopup.IsOpen = !ClockPopup.IsOpen;
    }

    private void BtnAppSettings_Click(object sender, RoutedEventArgs e)
    {
        StartMenuPopup.IsOpen = false;
        if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
            mainWindow.OpenSettingsFromWidget();
    }

    private void BtnSystemSettings_Click(object sender, RoutedEventArgs e)
    {
        StartMenuPopup.IsOpen = false;
        Process.Start(new ProcessStartInfo("ms-settings:") { UseShellExecute = true });
    }

    private void BtnCloseShell_Click(object sender, RoutedEventArgs e)
    {
        StartMenuPopup.IsOpen = false;
        Close();
    }

    private void BtnWordProcessor_Click(object sender, RoutedEventArgs e)
    {
        var window = new WordProcessorWindow { Owner = this };
        window.Show();
    }

    private void BtnSheetsEditor_Click(object sender, RoutedEventArgs e)
    {
        var window = new SheetsEditorWindow { Owner = this };
        window.Show();
    }

    private void BrowserLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is string target && !string.IsNullOrWhiteSpace(target))
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    }

    private void CloseTransientPanels()
    {
        StartMenuPopup.IsOpen = false;
        TrayPopup.IsOpen = false;
        CalendarPopup.IsOpen = false;
        ClockPopup.IsOpen = false;
    }

    private void HookKeyboard()
    {
        if (_keyboardHook != IntPtr.Zero)
            return;

        using var module = Process.GetCurrentProcess().MainModule;
        var moduleHandle = GetModuleHandle(module?.ModuleName);
        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
    }

    private void UnhookKeyboard()
    {
        if (_keyboardHook == IntPtr.Zero)
            return;

        UnhookWindowsHookEx(_keyboardHook);
        _keyboardHook = IntPtr.Zero;
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsActive && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            var vkCode = Marshal.ReadInt32(lParam);
            if (vkCode is VK_LWIN or VK_RWIN)
            {
                Dispatcher.Invoke(() =>
                {
                    CloseTransientPanels();
                    StartMenuPopup.IsOpen = true;
                });
                return (IntPtr)1;
            }
        }

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public sealed class BrowserLink
    {
        public BrowserLink(string name, string target, string iconGlyph)
        {
            Name = name;
            Target = target;
            IconGlyph = iconGlyph;
        }

        public string Name { get; }
        public string Target { get; }
        public string IconGlyph { get; }
    }
}
