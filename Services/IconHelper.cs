using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EchoUI.Services;

public static partial class IconHelper
{
    [LibraryImport("shell32.dll", EntryPoint = "ExtractIconW")]
    private static partial IntPtr ExtractIcon(IntPtr hInst, [MarshalAs(UnmanagedType.LPWStr)] string lpszExeFileName, int nIconIndex);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(IntPtr hIcon);

    public static ImageSource? GetIconForExe(string exePath)
    {
        if (!File.Exists(exePath))
            return null;

        IntPtr hIcon = IntPtr.Zero;
        try
        {
            hIcon = ExtractIcon(IntPtr.Zero, exePath, 0);
            if (hIcon == IntPtr.Zero || hIcon == (IntPtr)1)
                return null;

            var bmpSource = Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bmpSource.Freeze();
            return bmpSource;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hIcon != IntPtr.Zero && hIcon != (IntPtr)1)
                DestroyIcon(hIcon);
        }
    }
}
