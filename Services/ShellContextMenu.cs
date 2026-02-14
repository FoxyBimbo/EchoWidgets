using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace EchoUI.Services;

/// <summary>
/// Opens the default Windows shell context menu for a file or folder.
/// </summary>
public static partial class ShellContextMenu
{
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x, y;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out POINT lpPoint);

    /// <summary>
    /// Shows the Windows Explorer right-click context menu for the given path
    /// at the current cursor position.
    /// </summary>
    public static void Show(string path)
    {
        try
        {
            // Use PowerShell with the Shell.Application COM object to show
            // the native context menu. This is the most reliable cross-version
            // approach without importing full COM shell interfaces.
            // Fallback: open the "properties" dialog via shell verb.
            if (!File.Exists(path) && !Directory.Exists(path)) return;

            // Use the undocumented but widely-supported approach: launch
            // explorer.exe with /select to highlight the item, which also
            // makes the standard context menu available.
            // For an immediate context menu, we invoke the shell "properties"
            // as a quick reliable option, but a better UX is to build a
            // WPF context menu with common shell actions.
            ShowManagedContextMenu(path);
        }
        catch { }
    }

    private static void ShowManagedContextMenu(string path)
    {
        GetCursorPos(out var pt);

        bool isDir = Directory.Exists(path);
        string name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name)) name = path;

        var menu = new System.Windows.Controls.ContextMenu
        {
            Style = null,
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF2A2A3E")),
            Foreground = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF555577"))
        };

        var open = new System.Windows.Controls.MenuItem { Header = "Open" };
        open.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch { }
        };
        menu.Items.Add(open);

        if (!isDir)
        {
            var openWith = new System.Windows.Controls.MenuItem { Header = "Open with…" };
            openWith.Click += (_, _) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "rundll32.exe",
                        Arguments = $"shell32.dll,OpenAs_RunDLL \"{path}\"",
                        UseShellExecute = false
                    });
                }
                catch { }
            };
            menu.Items.Add(openWith);
        }

        menu.Items.Add(new System.Windows.Controls.Separator());

        var copyPath = new System.Windows.Controls.MenuItem { Header = "Copy path" };
        copyPath.Click += (_, _) =>
        {
            try { System.Windows.Clipboard.SetText(path); }
            catch { }
        };
        menu.Items.Add(copyPath);

        var showInExplorer = new System.Windows.Controls.MenuItem { Header = "Show in Explorer" };
        showInExplorer.Click += (_, _) =>
        {
            try
            {
                if (isDir)
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = false });
                else
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = false });
            }
            catch { }
        };
        menu.Items.Add(showInExplorer);

        menu.Items.Add(new System.Windows.Controls.Separator());

        if (!isDir)
        {
            var delete = new System.Windows.Controls.MenuItem { Header = "Delete" };
            delete.Click += (_, _) =>
            {
                var result = System.Windows.MessageBox.Show(
                    $"Send \"{name}\" to the Recycle Bin?",
                    "Delete", System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);
                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    try
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                            path,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    }
                    catch { }
                }
            };
            menu.Items.Add(delete);
        }
        else
        {
            var delete = new System.Windows.Controls.MenuItem { Header = "Delete" };
            delete.Click += (_, _) =>
            {
                var result = System.Windows.MessageBox.Show(
                    $"Send folder \"{name}\" to the Recycle Bin?",
                    "Delete", System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);
                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    try
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                            path,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    }
                    catch { }
                }
            };
            menu.Items.Add(delete);
        }

        var props = new System.Windows.Controls.MenuItem { Header = "Properties" };
        props.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/properties,\"{path}\"",
                    UseShellExecute = false
                });
            }
            catch { }
        };
        menu.Items.Add(props);

        menu.IsOpen = true;
    }
}
