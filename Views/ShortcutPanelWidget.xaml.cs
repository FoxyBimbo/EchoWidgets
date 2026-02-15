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

namespace EchoUI.Views;

public partial class ShortcutPanelWidget : Window
{
    private readonly string _widgetId;
    private readonly WidgetSettings _widgetSettings;
    private bool _isCollapsed;
    private bool _titleDragging;
    private System.Windows.Point _titleDragStart;

    public string WidgetId => _widgetId;

    public ShortcutPanelWidget(string widgetId, WidgetSettings settings)
    {
        InitializeComponent();
        _widgetId = widgetId;
        _widgetSettings = settings;

        // Apply per-widget custom color overrides
        ThemeHelper.ApplyToElement(this, settings.CustomColors);

        Topmost = settings.Topmost;
        Opacity = settings.Opacity;

        settings.Custom.TryGetValue("Title", out var title);
        TxtTitle.Text = string.IsNullOrEmpty(title) ? "Shortcuts" : title;

        LoadShortcuts();

        Closed += (_, _) => ReleaseResources();
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
            DragMove();
        }
    }

    private void TitleBar_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_titleDragging)
        {
            _titleDragging = false;
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
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }
}
