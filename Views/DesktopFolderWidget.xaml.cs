using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EchoUI.Models;
using EchoUI.Services;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace EchoUI.Views;

public partial class DesktopFolderWidget : Window
{
    private readonly string _widgetId;
    private string _folderPath;
    private DockEdge _currentEdge = DockEdge.None;
    private bool _isDocked;
    private SortMode _sortMode = SortMode.Name;
    private bool _sortDescending;
    private readonly WidgetSettings _widgetSettings;

    // ── Drag state ───────────────────────────────────────────
    private System.Windows.Point _dragStartPoint;
    private string? _dragItemPath;
    private bool _didDrag;
    private bool _dragIsLeftButton;

    // ── Resize state ────────────────────────────────────────
    private bool _isResizing;
    private System.Windows.Point _resizeStart;
    private int _resizeOriginalThickness;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x, y; }

    public string WidgetId => _widgetId;

    public DesktopFolderWidget() : this("DesktopFolder", new WidgetSettings { Topmost = false }) { }

    public DesktopFolderWidget(string widgetId, WidgetSettings settings)
    {
        InitializeComponent();
        _widgetId = widgetId;
        _widgetSettings = settings;

        // Apply per-widget custom color overrides
        ThemeHelper.ApplyToElement(this, settings.CustomColors);

        // Apply generic settings
        Topmost = settings.Topmost;
        Opacity = settings.Opacity;

        // Apply custom settings
        _folderPath = settings.Custom.TryGetValue("DefaultFolder", out var folder) && !string.IsNullOrEmpty(folder)
            ? folder
            : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        if (settings.Custom.TryGetValue("DefaultSort", out var sort))
        {
            _sortMode = sort switch
            {
                "DateModified" => SortMode.DateModified,
                "Size" => SortMode.Size,
                "Type" => SortMode.Type,
                _ => SortMode.Name
            };
        }

        TxtPath.Text = _folderPath;
        CmbSort.SelectedIndex = (int)_sortMode;
        LoadFolder();

        Closed += (_, _) => ReleaseResources();
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

        RootBorder.SetResourceReference(Border.BackgroundProperty, "WindowBackgroundBrush");
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

        RootBorder.SetResourceReference(Border.BackgroundProperty, "WindowBackgroundSemiBrush");
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
        BtnDockLeft.SetResourceReference(Button.BackgroundProperty, edge == DockEdge.Left ? "AccentBrush" : "ControlBackgroundBrush");
        BtnDockRight.SetResourceReference(Button.BackgroundProperty, edge == DockEdge.Right ? "AccentBrush" : "ControlBackgroundBrush");
        BtnDockTop.SetResourceReference(Button.BackgroundProperty, edge == DockEdge.Top ? "AccentBrush" : "ControlBackgroundBrush");
        BtnDockBottom.SetResourceReference(Button.BackgroundProperty, edge == DockEdge.Bottom ? "AccentBrush" : "ControlBackgroundBrush");
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
                var di = new DirectoryInfo(dir);
                items.Add(new FolderItem
                {
                    DisplayName = di.Name,
                    FullPath = dir,
                    IconImage = IconHelper.GetIconForPath(dir),
                    IsDirectory = true,
                    Modified = di.LastWriteTime,
                    Extension = ""
                });
            }

            foreach (var file in Directory.GetFiles(_folderPath))
            {
                var fi = new FileInfo(file);
                items.Add(new FolderItem
                {
                    DisplayName = fi.Name,
                    FullPath = file,
                    IconImage = IconHelper.GetIconForPath(file),
                    IsDirectory = false,
                    Size = fi.Length,
                    Modified = fi.LastWriteTime,
                    Extension = fi.Extension.ToLowerInvariant()
                });
            }
        }
        catch { }

        items = SortItems(items);

        TxtFolderName.Text = Path.GetFileName(_folderPath);
        if (string.IsNullOrEmpty(TxtFolderName.Text))
            TxtFolderName.Text = _folderPath;

        FileGrid.ItemsSource = items;
    }

    private List<FolderItem> SortItems(List<FolderItem> items)
    {
        // Directories always come first, then apply the selected sort
        IEnumerable<FolderItem> dirs = items.Where(i => i.IsDirectory);
        IEnumerable<FolderItem> files = items.Where(i => !i.IsDirectory);

        dirs = _sortMode switch
        {
            SortMode.Name => _sortDescending
                ? dirs.OrderByDescending(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
                : dirs.OrderBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase),
            SortMode.DateModified => _sortDescending
                ? dirs.OrderByDescending(i => i.Modified)
                : dirs.OrderBy(i => i.Modified),
            _ => dirs.OrderBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
        };

        files = _sortMode switch
        {
            SortMode.Name => _sortDescending
                ? files.OrderByDescending(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
                : files.OrderBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase),
            SortMode.DateModified => _sortDescending
                ? files.OrderByDescending(i => i.Modified)
                : files.OrderBy(i => i.Modified),
            SortMode.Size => _sortDescending
                ? files.OrderByDescending(i => i.Size)
                : files.OrderBy(i => i.Size),
            SortMode.Type => _sortDescending
                ? files.OrderByDescending(i => i.Extension).ThenBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
                : files.OrderBy(i => i.Extension).ThenBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase),
            _ => files.OrderBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
        };

        return [.. dirs, .. files];
    }

    private void CmbSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbSort.SelectedIndex < 0) return;
        _sortMode = (SortMode)CmbSort.SelectedIndex;
        LoadFolder();
    }

    private void BtnSortDir_Click(object sender, RoutedEventArgs e)
    {
        _sortDescending = !_sortDescending;
        BtnSortDir.Content = _sortDescending ? "↓" : "↑";
        BtnSortDir.ToolTip = _sortDescending ? "Descending" : "Ascending";
        LoadFolder();
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
        if (_didDrag)
        {
            _didDrag = false;
            return;
        }

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

    private void FileItem_PreviewLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
        {
            _dragStartPoint = e.GetPosition(this);
            _dragItemPath = path;
            _didDrag = false;
            _dragIsLeftButton = true;
        }
    }

    private void FileItem_PreviewRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
        {
            _dragStartPoint = e.GetPosition(this);
            _dragItemPath = path;
            _didDrag = false;
            _dragIsLeftButton = false;
        }
    }

    private void FileItem_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_dragItemPath is null)
            return;

        bool buttonHeld = _dragIsLeftButton
            ? e.LeftButton == MouseButtonState.Pressed
            : e.RightButton == MouseButtonState.Pressed;
        if (!buttonHeld)
            return;

        var pos = e.GetPosition(this);
        var diff = pos - _dragStartPoint;

        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            _didDrag = true;
            var path = _dragItemPath;
            _dragItemPath = null;
            var data = new System.Windows.DataObject(System.Windows.DataFormats.FileDrop, new[] { path });
            DragDrop.DoDragDrop(this, data, System.Windows.DragDropEffects.Copy | System.Windows.DragDropEffects.Move | System.Windows.DragDropEffects.Link);
        }
    }

    private void FileItem_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (_didDrag)
        {
            _didDrag = false;
            _dragItemPath = null;
            e.Handled = true;
            return;
        }

        _dragItemPath = null;

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

    private void ReleaseResources()
    {
        // Clear icon image references from all items
        if (FileGrid.ItemsSource is IList<FolderItem> items)
        {
            foreach (var item in items)
                item.IconImage = null;
        }
        FileGrid.ItemsSource = null;

        // Release the drop shadow effect (holds unmanaged render resources)
        RootBorder.Effect = null;
        RootBorder.Child = null;
        Content = null;
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
    public ImageSource? IconImage { get; set; }
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTime Modified { get; set; }
    public string Extension { get; set; } = string.Empty;
}

public enum SortMode
{
    Name,
    DateModified,
    Size,
    Type
}
