using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using EchoUI.Models;
using EchoUI.Services;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace EchoUI.Views;

public partial class DesktopFolderWidget : Window
{
    private const string WidgetId = "DesktopFolder";
    private string _folderPath;
    private DockEdge _currentEdge = DockEdge.None;
    private bool _isDocked;

    // ── Resize state ────────────────────────────────────────
    private bool _isResizing;
    private System.Windows.Point _resizeStart;
    private int _resizeOriginalThickness;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x, y; }

    public DesktopFolderWidget()
    {
        InitializeComponent();
        _folderPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        TxtPath.Text = _folderPath;
        LoadFolder();
    }

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        var screen = Screen.PrimaryScreen!.WorkingArea;
        Left = (screen.Width - Width) / 2;
        Top = (screen.Height - Height) / 2;
    }

    // ── Dock / Undock ───────────────────────────────────────
    private void BtnDock_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string edgeName &&
            Enum.TryParse<DockEdge>(edgeName, out var edge))
        {
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

        RootBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1E1E2E"));
        RootBorder.CornerRadius = new CornerRadius(0);
        RootBorder.Margin = new Thickness(0);
        RootBorder.Effect = null;

        double thickness = edge is DockEdge.Left or DockEdge.Right ? 320 : 200;

        MainWindow.DockManager.Dock(WidgetId, this, edge, thickness);
        HighlightActiveEdge(edge);
        ShowResizeGrip(edge);
    }

    private void Undock()
    {
        _isDocked = false;
        _currentEdge = DockEdge.None;
        MainWindow.DockManager.Undock(WidgetId);

        RootBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DD1E1E2E"));
        RootBorder.CornerRadius = new CornerRadius(14);
        RootBorder.Margin = new Thickness(8);
        RootBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius = 16, ShadowDepth = 2, Opacity = 0.5, Color = Colors.Black
        };

        Width = 320;
        Height = 420;
        var screen = Screen.PrimaryScreen!.WorkingArea;
        Left = (screen.Width - Width) / 2;
        Top = (screen.Height - Height) / 2;
        HighlightActiveEdge(DockEdge.None);
        ResizeGrip.Visibility = Visibility.Collapsed;
    }

    // ── Resize grip positioning ─────────────────────────────
    private void ShowResizeGrip(DockEdge edge)
    {
        ResizeGrip.Visibility = Visibility.Visible;

        // Place the grip on the inner edge (the side facing the desktop)
        switch (edge)
        {
            case DockEdge.Left:
                ResizeGrip.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                ResizeGrip.VerticalAlignment = VerticalAlignment.Stretch;
                ResizeGrip.Width = 6;
                ResizeGrip.Height = double.NaN;
                ResizeGrip.Cursor = System.Windows.Input.Cursors.SizeWE;
                break;
            case DockEdge.Right:
                ResizeGrip.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                ResizeGrip.VerticalAlignment = VerticalAlignment.Stretch;
                ResizeGrip.Width = 6;
                ResizeGrip.Height = double.NaN;
                ResizeGrip.Cursor = System.Windows.Input.Cursors.SizeWE;
                break;
            case DockEdge.Top:
                ResizeGrip.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                ResizeGrip.VerticalAlignment = VerticalAlignment.Bottom;
                ResizeGrip.Width = double.NaN;
                ResizeGrip.Height = 6;
                ResizeGrip.Cursor = System.Windows.Input.Cursors.SizeNS;
                break;
            case DockEdge.Bottom:
                ResizeGrip.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                ResizeGrip.VerticalAlignment = VerticalAlignment.Top;
                ResizeGrip.Width = double.NaN;
                ResizeGrip.Height = 6;
                ResizeGrip.Cursor = System.Windows.Input.Cursors.SizeNS;
                break;
        }
    }

    // ── Resize drag handlers ────────────────────────────────
    private void ResizeGrip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isDocked || e.LeftButton != MouseButtonState.Pressed) return;
        _isResizing = true;
        GetCursorPos(out var pt);
        _resizeStart = new System.Windows.Point(pt.x, pt.y);
        _resizeOriginalThickness = MainWindow.DockManager.DipToPixel(
            _currentEdge is DockEdge.Left or DockEdge.Right ? 320 : 200);
        // Use actual current window size as the baseline
        if (_currentEdge is DockEdge.Left or DockEdge.Right)
            _resizeOriginalThickness = (int)ActualWidth;
        else
            _resizeOriginalThickness = (int)ActualHeight;
        // Convert to pixels for DPI
        _resizeOriginalThickness = MainWindow.DockManager.DipToPixel(_resizeOriginalThickness);

        ((Border)sender).CaptureMouse();
    }

    private void ResizeGrip_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isResizing) return;

        GetCursorPos(out var pt);
        int dx = pt.x - (int)_resizeStart.X;
        int dy = pt.y - (int)_resizeStart.Y;

        int newThickness = _currentEdge switch
        {
            DockEdge.Left => _resizeOriginalThickness + dx,
            DockEdge.Right => _resizeOriginalThickness - dx,
            DockEdge.Top => _resizeOriginalThickness + dy,
            DockEdge.Bottom => _resizeOriginalThickness - dy,
            _ => _resizeOriginalThickness
        };

        if (newThickness < 150) newThickness = 150;

        MainWindow.DockManager.Resize(WidgetId, newThickness);
    }

    private void ResizeGrip_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isResizing = false;
        ((Border)sender).ReleaseMouseCapture();
    }

    // ── Edge highlighting ───────────────────────────────────
    private void HighlightActiveEdge(DockEdge edge)
    {
        var active = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3A86FF"));
        var inactive = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2A2A3E"));
        BtnDockLeft.Background = edge == DockEdge.Left ? active : inactive;
        BtnDockRight.Background = edge == DockEdge.Right ? active : inactive;
        BtnDockTop.Background = edge == DockEdge.Top ? active : inactive;
        BtnDockBottom.Background = edge == DockEdge.Bottom ? active : inactive;
    }

    // ── Folder browsing ─────────────────────────────────────
    private void LoadFolder()
    {
        if (!Directory.Exists(_folderPath))
            return;

        var items = new List<FolderItem>();

        try
        {
            foreach (var dir in Directory.GetDirectories(_folderPath))
            {
                items.Add(new FolderItem
                {
                    DisplayName = Path.GetFileName(dir),
                    FullPath = dir,
                    Icon = "📁"
                });
            }

            foreach (var file in Directory.GetFiles(_folderPath))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                items.Add(new FolderItem
                {
                    DisplayName = Path.GetFileName(file),
                    FullPath = file,
                    Icon = ext switch
                    {
                        ".txt" or ".log" or ".md" => "📄",
                        ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "🖼️",
                        ".mp3" or ".wav" or ".flac" => "🎵",
                        ".mp4" or ".avi" or ".mkv" => "🎬",
                        ".exe" or ".msi" => "⚙️",
                        ".zip" or ".rar" or ".7z" => "📦",
                        ".pdf" => "📕",
                        ".lnk" => "🔗",
                        _ => "📄"
                    }
                });
            }
        }
        catch { }

        TxtFolderName.Text = Path.GetFileName(_folderPath);
        if (string.IsNullOrEmpty(TxtFolderName.Text))
            TxtFolderName.Text = _folderPath;

        FileGrid.ItemsSource = items;
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select a folder to display",
            SelectedPath = _folderPath
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _folderPath = dialog.SelectedPath;
            TxtPath.Text = _folderPath;
            LoadFolder();
        }
    }

    private void FileItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
        {
            if (Directory.Exists(path))
            {
                _folderPath = path;
                TxtPath.Text = _folderPath;
                LoadFolder();
            }
            else if (File.Exists(path))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
                catch { }
            }
        }
    }

    private void FileItem_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
        {
            e.Handled = true;
            ShellContextMenu.Show(path);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        MainWindow.DockManager.Undock(WidgetId);
        Close();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isDocked && e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }
}

public class FolderItem
{
    public string DisplayName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string Icon { get; set; } = "📄";
}
