using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EchoUI.Models;
using EchoUI.Services;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using Button = System.Windows.Controls.Button;

namespace EchoUI.Views;

public partial class ShortcutPanelWidget : Window
{
    private readonly string _widgetId;
    private readonly WidgetSettings _widgetSettings;
    private readonly AppSettings _appSettings;
    private bool _isCollapsed;
    private bool _titleDragging;
    private System.Windows.Point _titleDragStart;
    private bool _isDocked;
    private DockEdge _currentEdge = DockEdge.None;
    private const int AutoDockThreshold = 2;

    public string WidgetId => _widgetId;

    public ShortcutPanelWidget(string widgetId, WidgetSettings settings, AppSettings appSettings)
    {
        InitializeComponent();
        _widgetId = widgetId;
        _widgetSettings = settings;
        _appSettings = appSettings;

        ApplyWidgetSettingsFromModel();

        Closed += (_, _) => ReleaseResources();
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
        Opacity = ws.Opacity;

        ws.Custom.TryGetValue("Title", out var title);
        TxtTitle.Text = string.IsNullOrEmpty(title) ? "Shortcuts" : title;
        LoadShortcuts();
    }

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
        Left = (screen.Width - Width) / 2;
        Top = (screen.Height - Height) / 2;
    }

    // ── Load / Refresh ──────────────────────────────────────
    private void LoadShortcuts()
    {
        foreach (var s in _widgetSettings.Shortcuts)
            s.IconImage = ResolveIcon(s);

        ShortcutList.ItemsSource = null;
        ShortcutList.ItemsSource = _widgetSettings.Shortcuts;
    }

    private static ImageSource? ResolveIcon(ShortcutItem s)
    {
        if (!string.IsNullOrEmpty(s.CustomIconPath) && File.Exists(s.CustomIconPath))
            return IconHelper.GetIconForPath(s.CustomIconPath);

        if (!string.IsNullOrEmpty(s.TargetPath))
        {
            if (File.Exists(s.TargetPath) || Directory.Exists(s.TargetPath))
                return IconHelper.GetIconForPath(s.TargetPath);
        }

        return null;
    }

    // ── Title: drag to move, click to collapse, double-click to edit ──
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            TitleEditPanel.Visibility = Visibility.Visible;
            TxtTitleEdit.Text = TxtTitle.Text;
            TxtTitleEdit.Focus();
            TxtTitleEdit.SelectAll();
            e.Handled = true;
            return;
        }

        _titleDragging = false;
        _titleDragStart = e.GetPosition(this);
        e.Handled = true;
    }

    private void TitleBar_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(this);
        var diff = pos - _titleDragStart;

        if (!_titleDragging &&
            (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
             Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
        {
            _titleDragging = true;
            if (_isDocked)
                Undock(true);
            DragMove();
            TryAutoDockFromPosition();
        }
    }

    private void TitleBar_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_titleDragging)
        {
            _titleDragging = false;
            TryAutoDockFromPosition();
            return;
        }

        // Simple click+release → toggle collapse/expand
        _isCollapsed = !_isCollapsed;
        ContentPanel.Visibility = _isCollapsed ? Visibility.Collapsed : Visibility.Visible;

        if (_isCollapsed)
        {
            SizeToContent = SizeToContent.Height;
            MinHeight = 0;
        }
        else
        {
            SizeToContent = SizeToContent.Manual;
            Height = 320;
        }
    }

    // ── Title editing ───────────────────────────────────────
    private void BtnTitleSave_Click(object sender, RoutedEventArgs e)
    {
        var newTitle = TxtTitleEdit.Text.Trim();
        if (!string.IsNullOrEmpty(newTitle))
        {
            TxtTitle.Text = newTitle;
            _widgetSettings.Custom["Title"] = newTitle;
        }
        TitleEditPanel.Visibility = Visibility.Collapsed;
    }

    // ── Add shortcut ────────────────────────────────────────
    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ShortcutEditDialog { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _widgetSettings.Shortcuts.Add(dlg.Result);
            LoadShortcuts();
        }
    }

    // ── Dock / Undock ───────────────────────────────────────
    private void BtnDock_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string edgeName &&
            Enum.TryParse<DockEdge>(edgeName, out var edge))
        {
            if (_isDocked && edge == _currentEdge)
                Undock();
            else
                DockTo(edge);
        }
    }

    private void BtnUndock_Click(object sender, RoutedEventArgs e)
    {
        Undock();
    }

    private void DockTo(DockEdge edge)
    {
        _currentEdge = edge;
        _isDocked = true;

        RootBorder.SetResourceReference(Border.BackgroundProperty, "WindowBackgroundBrush");
        RootBorder.CornerRadius = new CornerRadius(0);
        RootBorder.Margin = new Thickness(0);
        RootBorder.Effect = null;

        double thickness = edge is DockEdge.Left or DockEdge.Right ? 260 : 180;

        MainWindow.DockManager.Dock(WidgetId, this, edge, thickness);
        HighlightActiveEdge(edge);
    }

    private void Undock(bool preservePosition = false)
    {
        var currentLeft = Left;
        var currentTop = Top;
        _isDocked = false;
        _currentEdge = DockEdge.None;
        MainWindow.DockManager.Undock(WidgetId);

        RootBorder.SetResourceReference(Border.BackgroundProperty, "WindowBackgroundSemiBrush");
        RootBorder.CornerRadius = new CornerRadius(14);
        RootBorder.Margin = new Thickness(8);
        RootBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius = 16, ShadowDepth = 2, Opacity = 0.5, Color = Colors.Black
        };

        Width = 260;
        Height = 320;
        if (preservePosition)
        {
            Left = currentLeft;
            Top = currentTop;
        }
        else
        {
            var screen = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
            Left = (screen.Width - Width) / 2;
            Top = (screen.Height - Height) / 2;
        }
        HighlightActiveEdge(DockEdge.None);
    }

    private void HighlightActiveEdge(DockEdge edge)
    {
        BtnDockLeft.SetResourceReference(Button.BackgroundProperty, edge == DockEdge.Left ? "AccentBrush" : "ControlBackgroundBrush");
        BtnDockRight.SetResourceReference(Button.BackgroundProperty, edge == DockEdge.Right ? "AccentBrush" : "ControlBackgroundBrush");
        BtnDockTop.SetResourceReference(Button.BackgroundProperty, edge == DockEdge.Top ? "AccentBrush" : "ControlBackgroundBrush");
        BtnDockBottom.SetResourceReference(Button.BackgroundProperty, edge == DockEdge.Bottom ? "AccentBrush" : "ControlBackgroundBrush");
    }

    private DockEdge GetAutoDockEdgeFromCursor()
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
        var cursor = System.Windows.Forms.Cursor.Position;

        int leftDistance = Math.Abs(cursor.X - screen.Left);
        int rightDistance = Math.Abs(screen.Right - cursor.X);
        int topDistance = Math.Abs(cursor.Y - screen.Top);
        int bottomDistance = Math.Abs(screen.Bottom - cursor.Y);

        int min = Math.Min(Math.Min(leftDistance, rightDistance), Math.Min(topDistance, bottomDistance));
        if (min > AutoDockThreshold)
            return DockEdge.None;

        if (min == leftDistance) return DockEdge.Left;
        if (min == rightDistance) return DockEdge.Right;
        if (min == topDistance) return DockEdge.Top;
        return DockEdge.Bottom;
    }

    private void TryAutoDockFromPosition()
    {
        if (_isDocked)
            return;

        var edge = GetAutoDockEdgeFromCursor();
        if (edge != DockEdge.None)
            DockTo(edge);
    }

    // ── Launch shortcut (single click) ──────────────────────
    private void Shortcut_Click(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 1) return;
        if (sender is FrameworkElement fe && fe.Tag is ShortcutItem s)
        {
            e.Handled = true;
            LaunchShortcut(s);
        }
    }

    private static void LaunchShortcut(ShortcutItem s)
    {
        if (string.IsNullOrWhiteSpace(s.TargetPath)) return;
        try
        {
            var psi = new ProcessStartInfo(s.TargetPath) { UseShellExecute = true };
            if (!string.IsNullOrWhiteSpace(s.Arguments))
                psi.Arguments = s.Arguments;
            Process.Start(psi);
        }
        catch { }
    }

    // ── Right-click or ⋮ menu ───────────────────────────────
    private void Shortcut_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ShortcutItem s)
        {
            e.Handled = true;
            ShowShortcutContextMenu(s, fe);
        }
    }

    private void BtnShortcutMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ShortcutItem s)
            ShowShortcutContextMenu(s, fe);
    }

    private void ShowShortcutContextMenu(ShortcutItem shortcut, FrameworkElement anchor)
    {
        var menu = new ContextMenu();

        var edit = new MenuItem { Header = "Edit…" };
        edit.Click += (_, _) =>
        {
            var dlg = new ShortcutEditDialog(shortcut) { Owner = this };
            if (dlg.ShowDialog() == true)
                LoadShortcuts();
        };
        menu.Items.Add(edit);

        var del = new MenuItem { Header = "Delete" };
        del.Click += (_, _) =>
        {
            _widgetSettings.Shortcuts.Remove(shortcut);
            LoadShortcuts();
        };
        menu.Items.Add(del);

        menu.PlacementTarget = anchor;
        menu.IsOpen = true;
    }

    // ── Close / cleanup ─────────────────────────────────────
    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        MainWindow.DockManager.Undock(_widgetId);
        Close();
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_appSettings, null, _widgetId, _widgetSettings, ApplyWidgetSettingsFromModel)
        {
            Owner = this
        };
        win.ShowDialog();
    }

    private void ReleaseResources()
    {
        foreach (var s in _widgetSettings.Shortcuts)
            s.IconImage = null;
        ShortcutList.ItemsSource = null;
        RootBorder.Effect = null;
        RootBorder.Child = null;
        Content = null;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        if (_isDocked)
            Undock(true);

        DragMove();
        TryAutoDockFromPosition();
    }
}
