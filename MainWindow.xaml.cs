using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using EchoUI.Models;
using EchoUI.Services;
using EchoUI.Views;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;

namespace EchoUI;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly ExtensionManager _extManager;
    private readonly ScriptEngine _scriptEngine;
    private readonly ObservableCollection<AppNotification> _notifications = [];
    private readonly System.Windows.Forms.NotifyIcon _trayIcon;
    private readonly FullscreenWatcher _fullscreenWatcher;

    /// <summary>All open widget windows keyed by their unique instance ID.</summary>
    private readonly Dictionary<string, Window> _widgets = [];

    public static WidgetDockManager DockManager { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        _settings = AppSettings.Load();
        _extManager = new ExtensionManager();
        _scriptEngine = new ScriptEngine();

        var api = new TaskbarScriptApi(AddNotification, (_, _) => { });
        _scriptEngine.ExposeApi("echo", api);

        // ── Fullscreen watcher ──────────────────────────────
        _fullscreenWatcher = new FullscreenWatcher();
        _fullscreenWatcher.FullscreenEntered += () =>
            Dispatcher.Invoke(() => DockManager.SetFullscreenMode(true));
        _fullscreenWatcher.FullscreenExited += () =>
            Dispatcher.Invoke(() => DockManager.SetFullscreenMode(false));
        _fullscreenWatcher.Start();

        // ── System tray icon ────────────────────────────────
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = new Icon(Application.GetResourceStream(new Uri("pack://application:,,,/AppIcon.ico"))!.Stream),
            Text = "EchoUI",
            Visible = true
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("New Desktop Folder Widget", null, (_, _) => SpawnWidget("DesktopFolder"));
        menu.Items.Add("New Shortcut Panel Widget", null, (_, _) => SpawnWidget("ShortcutPanel"));
        menu.Items.Add("Run Plugins", null, (_, _) => RunPlugins());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Notifications", null, (_, _) => ShowNotifications());
        menu.Items.Add("Settings", null, (_, _) => OpenSettings());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => OpenSettings();

        Closed += MainWindow_Closed;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _fullscreenWatcher.Dispose();
        CloseAllWidgets();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        DockManager.RestoreAll();
    }

    // ── Widget management ───────────────────────────────────

    private void SpawnWidget(string kind)
    {
        var (id, ws) = _settings.CreateWidgetInstance(kind);

        Window widget = kind switch
        {
            "ShortcutPanel" => new ShortcutPanelWidget(id, ws),
            _ => new DesktopFolderWidget(id, ws)
        };

        _widgets[id] = widget;
        widget.Closed += OnWidgetClosed;
        DockManager.TrackFloatingWidget(widget);
        widget.Show();

        if (kind == "DesktopFolder")
        {
            foreach (var ext in _extManager.GetWidgets())
            {
                var code = _extManager.ReadScript(ext);
                _scriptEngine.Run(code, ext.ScriptType);
            }
        }
    }

    private void OnWidgetClosed(object? sender, EventArgs e)
    {
        if (sender is not Window win) return;

        string? id = sender switch
        {
            DesktopFolderWidget dfw => dfw.WidgetId,
            ShortcutPanelWidget spw => spw.WidgetId,
            _ => null
        };
        if (id is null) return;

        DockManager.Undock(id);
        DockManager.UntrackFloatingWidget(win);
        win.Closed -= OnWidgetClosed;
        _widgets.Remove(id);

        // Save shortcut panel state before removing the settings reference
        if (sender is ShortcutPanelWidget)
            _settings.Save();

        GC.Collect(2, GCCollectionMode.Aggressive, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Aggressive, true, true);
    }

    private void CloseAllWidgets()
    {
        foreach (var (id, win) in _widgets.ToList())
        {
            DockManager.Undock(id);
            DockManager.UntrackFloatingWidget(win);
            win.Closed -= OnWidgetClosed;
            win.Close();
        }
        _widgets.Clear();
        _settings.Save();

        GC.Collect(2, GCCollectionMode.Aggressive, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Aggressive, true, true);
    }

    // ── Plugins ─────────────────────────────────────────────
    private void RunPlugins()
    {
        foreach (var plugin in _extManager.GetPlugins())
        {
            var code = _extManager.ReadScript(plugin);
            var result = _scriptEngine.Run(code, plugin.ScriptType);
            if (!result.Success)
                AddNotification($"Plugin Error: {plugin.Name}", result.Error ?? "Unknown error");
        }
    }

    // ── Notifications ───────────────────────────────────────
    private void ShowNotifications()
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
        var panel = new NotificationPanel(_notifications)
        {
            Left = screen.Right - 350,
            Top = screen.Bottom - 410
        };
        panel.Show();
    }

    private void AddNotification(string title, string message)
    {
        Dispatcher.Invoke(() =>
        {
            _notifications.Insert(0, new AppNotification
            {
                Title = title,
                Message = message,
                Timestamp = DateTime.Now
            });
        });
    }

    // ── Settings ────────────────────────────────────────────
    private void OpenSettings()
    {
        var win = new SettingsWindow(_settings, _extManager);
        if (win.ShowDialog() == true)
        {
            ApplyWidgetSettings();
        }
    }

    private void ApplyWidgetSettings()
    {
        foreach (var (id, win) in _widgets)
        {
            var ws = _settings.GetWidgetSettings(id);
            win.Topmost = ws.Topmost;
            win.Opacity = ws.Opacity;
            ThemeHelper.ApplyToElement(win, ws.CustomColors);
        }
    }

    // ── Exit ────────────────────────────────────────────────
    private void ExitApp()
    {
        DockManager.RestoreAll();
        Application.Current.Shutdown();
    }
}