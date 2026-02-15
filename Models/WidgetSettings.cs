namespace EchoUI.Models;

/// <summary>
/// Per-widget settings with generic properties shared by all widgets
/// and a dictionary for widget-specific custom values.
/// </summary>
public class WidgetSettings
{
    /// <summary>Widget kind — used to create the correct window type.</summary>
    public string Kind { get; set; } = "DesktopFolder";

    /// <summary>Window opacity (0.0 – 1.0).</summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>Whether the widget stays on top of other applications.</summary>
    public bool Topmost { get; set; } = true;

    /// <summary>Widget-specific key/value settings.</summary>
    public Dictionary<string, string> Custom { get; set; } = [];

    /// <summary>Shortcut entries for ShortcutPanel widgets.</summary>
    public List<ShortcutItem> Shortcuts { get; set; } = [];

    /// <summary>
    /// Optional per-widget custom color override. When non-null the widget
    /// uses these colors instead of the global theme.
    /// </summary>
    public ThemeColors? CustomColors { get; set; }
}
