using System.IO;
using EchoUI.Models;

namespace EchoUI.Services;

public class ExtensionManager
{
    private readonly HashSet<string> _enabledExtensions = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _respectEnabledList;
    private static readonly string ExtensionsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EchoUI", "Extensions");

    private static readonly string PluginsDir = Path.Combine(ExtensionsDir, "Plugins");
    private static readonly string WidgetsDir = Path.Combine(ExtensionsDir, "Widgets");

    public string PluginsFolderPath => PluginsDir;
    public string WidgetsFolderPath => WidgetsDir;

    public List<ExtensionInfo> Extensions { get; } = [];

    public ExtensionManager(AppSettings? settings = null)
    {
        if (settings is not null)
        {
            _respectEnabledList = settings.HasConfiguredExtensions;
            foreach (var name in settings.EnabledExtensions)
                _enabledExtensions.Add(name);
        }

        Directory.CreateDirectory(PluginsDir);
        Directory.CreateDirectory(WidgetsDir);
        EnsureSampleExtensions();
        Scan();
    }

    public void Scan()
    {
        Extensions.Clear();
        ScanDirectory(PluginsDir, ExtensionKind.Plugin);
        ScanDirectory(WidgetsDir, ExtensionKind.Widget);
        ApplyEnabledFlags();
    }

    private void ScanDirectory(string dir, ExtensionKind kind)
    {
        foreach (var file in Directory.GetFiles(dir, "*.js").Concat(Directory.GetFiles(dir, "*.lua")))
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            Extensions.Add(new ExtensionInfo
            {
                Name = Path.GetFileNameWithoutExtension(file),
                FilePath = file,
                ScriptType = ext == ".lua" ? ScriptType.Lua : ScriptType.JavaScript,
                Kind = kind,
                IsEnabled = true,
                Description = kind == ExtensionKind.Plugin ? "Plugin script" : "Widget script"
            });
        }
    }

    public void UpdateEnabledExtensions(IEnumerable<string> enabledNames)
    {
        _enabledExtensions.Clear();
        foreach (var name in enabledNames)
            _enabledExtensions.Add(name);

        ApplyEnabledFlags();
    }

    private void ApplyEnabledFlags()
    {
        if (!_respectEnabledList)
            return;

        foreach (var ext in Extensions)
            ext.IsEnabled = _enabledExtensions.Contains(ext.Name);
    }

    public void ImportExtension(string sourcePath, ExtensionKind kind)
    {
        var destDir = kind == ExtensionKind.Plugin ? PluginsDir : WidgetsDir;
        var destPath = Path.Combine(destDir, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, destPath, overwrite: true);
        Scan();
    }

    public List<ExtensionInfo> GetPlugins() =>
        Extensions.Where(e => e.Kind == ExtensionKind.Plugin && e.IsEnabled).ToList();

    public List<ExtensionInfo> GetWidgets() =>
        Extensions.Where(e => e.Kind == ExtensionKind.Widget && e.IsEnabled).ToList();

    public string ReadScript(ExtensionInfo ext) => File.ReadAllText(ext.FilePath);

    private void EnsureSampleExtensions()
    {
        var samplePlugin = Path.Combine(PluginsDir, "ColorChanger.js");
        if (!File.Exists(samplePlugin))
        {
            File.WriteAllText(samplePlugin, """
                // ColorChanger plugin – cycles the taskbar clock foreground color
                var colors = ["#FF5733", "#33FF57", "#3357FF", "#F0E68C", "#DDA0DD"];
                var pick = colors[Math.floor(Math.random() * colors.length)];
                echo.setForegroundColor("ClockText", pick);
                echo.notify("ColorChanger", "Clock color changed to " + pick);
                """);
        }

        var legacyDesktopFolder = Path.Combine(WidgetsDir, "DesktopFolder.js");
        var sampleFolder = Path.Combine(WidgetsDir, "Folder.js");
        if (File.Exists(legacyDesktopFolder) && !File.Exists(sampleFolder))
            File.Move(legacyDesktopFolder, sampleFolder);

        if (!File.Exists(sampleFolder))
        {
            File.WriteAllText(sampleFolder, """
                // Folder widget – built-in, handled natively.
                // This marker file tells EchoUI to show the Folder widget.
                echo.notify("Folder", "Folder widget is active.");
                """);
        }

        var sampleShortcutPanel = Path.Combine(WidgetsDir, "ShortcutPanel.js");
        if (!File.Exists(sampleShortcutPanel))
        {
            File.WriteAllText(sampleShortcutPanel, """
                // ShortcutPanel widget – built-in, handled natively.
                // This marker file tells EchoUI to show the Shortcut Panel widget.
                echo.notify("ShortcutPanel", "Shortcut panel widget is active.");
                """);
        }

        var sampleFullScreenShell = Path.Combine(WidgetsDir, "FullScreenShell.js");
        if (!File.Exists(sampleFullScreenShell))
        {
            File.WriteAllText(sampleFullScreenShell, """
                // FullScreenShell widget – built-in, handled natively.
                // This marker file tells EchoUI to show the Full Screen Shell widget.
                echo.notify("FullScreenShell", "Full screen shell widget is active.");
                """);
        }
    }
}
