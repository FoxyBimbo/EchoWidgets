using System.Windows.Media;

namespace EchoUI.Models;

public class PinnedApp
{
    public string Name { get; set; } = string.Empty;
    public string ExePath { get; set; } = string.Empty;
    public ImageSource? Icon { get; set; }
}
