using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Windows;
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

    private DesktopFolderWidget? _folderWidget;

    public static WidgetDockManager DockManager { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        _settings = AppSettings.Load();
        _extManager = new ExtensionManager();
        _scriptEngine = new ScriptEngine();

        var api = new TaskbarScriptApi(AddNotification, (_, _) => { });
        _scriptEngine.ExposeApi("echo", api);

        // ── System tray icon ────────────────────────────────
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "EchoUI",
            Visible = true
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Desktop Folder Widget", null, (_, _) => ToggleFolderWidget());
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
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        DockManager.RestoreAll();
    }

    // ── Folder Widget ───────────────────────────────────────
    private void ToggleFolderWidget()
    {
        if (_folderWidget is { IsLoaded: true })
        {
            DockManager.Undock("DesktopFolder");
            _folderWidget.Close();
            _folderWidget = null;
        }
        else
        {
            _folderWidget = new DesktopFolderWidget();
            _folderWidget.Closed += (_, _) =>
            {
                DockManager.Undock("DesktopFolder");
                _folderWidget = null;
            };
            _folderWidget.Show();

            foreach (var widget in _extManager.GetWidgets())
            {
                var code = _extManager.ReadScript(widget);
                _scriptEngine.Run(code, widget.ScriptType);
            }
        }
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
        win.ShowDialog();
    }

    // ── Exit ────────────────────────────────────────────────
    private void ExitApp()
    {
        DockManager.RestoreAll();
        Application.Current.Shutdown();
    }
}