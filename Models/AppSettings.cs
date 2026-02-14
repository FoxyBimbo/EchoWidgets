using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EchoUI.Models;

public class AppSettings
{
    public bool AutoHideTaskbar { get; set; }
    public bool ShowSeconds { get; set; }
    public bool Use24HourClock { get; set; }
    public string TaskbarPosition { get; set; } = "Bottom";
    public double TaskbarOpacity { get; set; } = 0.95;
    public string AccentColor { get; set; } = "#FF3A86FF";
    public List<string> PinnedAppPaths { get; set; } = [];
    public List<string> EnabledExtensions { get; set; } = [];

    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EchoUI");

    private static readonly string SettingsPath =
        Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public void Save()
    {
        Directory.CreateDirectory(SettingsDir);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public static AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }
}
