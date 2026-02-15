using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EchoUI.Models;
using EchoUI.Services;
using ColorDialog = System.Windows.Forms.ColorDialog;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace EchoUI.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly ExtensionManager? _extManager;
    private readonly string? _widgetIdOverride;
    private readonly Action? _widgetSettingsApplied;
    private bool _loading = true;

    public SettingsWindow(AppSettings settings, ExtensionManager? extManager, string? widgetIdOverride = null, Action? widgetSettingsApplied = null)
    {
        _settings = settings;
        _extManager = extManager;
        _widgetIdOverride = widgetIdOverride;
        _widgetSettingsApplied = widgetSettingsApplied;
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        _loading = true;
        TxtAccentColor.Text = _settings.AccentColor;
        if (_extManager is not null)
            LstExtensions.ItemsSource = _extManager.Extensions;

        // Theme mode
        CmbThemeMode.SelectedIndex = _settings.ThemeMode switch
        {
            "Dark" => 1,
            "Light" => 2,
            "Custom" => 3,
            _ => 0 // Auto
        };
        LoadCustomThemeFields();

        ConfigureSectionVisibility();
        if (_widgetIdOverride is not null)
        {
            LoadWidgetSettings(_widgetIdOverride);
        }
        else
        {
            LoadWidgetSettings("DesktopFolder");
        }
        _loading = false;
    }

    private void ConfigureSectionVisibility()
    {
        if (_widgetIdOverride is not null)
        {
            PanelThemeSettings.Visibility = Visibility.Collapsed;
            PanelGeneralSettings.Visibility = Visibility.Collapsed;
            PanelExtensions.Visibility = Visibility.Collapsed;
            PanelWidgetSettings.Visibility = Visibility.Visible;
            PanelWidgetSelector.Visibility = Visibility.Collapsed;
            return;
        }

        PanelWidgetSettings.Visibility = Visibility.Collapsed;
        PanelThemeSettings.Visibility = Visibility.Visible;
        PanelGeneralSettings.Visibility = Visibility.Visible;
        PanelExtensions.Visibility = _extManager is null ? Visibility.Collapsed : Visibility.Visible;
    }

    // ── Theme mode ──────────────────────────────────────────
    private void CmbThemeMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        PanelCustomTheme.Visibility = CmbThemeMode.SelectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
    }

    private string SelectedThemeMode => CmbThemeMode.SelectedIndex switch
    {
        1 => "Dark",
        2 => "Light",
        3 => "Custom",
        _ => "Auto"
    };

    private void LoadCustomThemeFields()
    {
        var c = _settings.CustomTheme ?? ThemeColors.Dark;
        TxtCustomWindowBg.Text = c.WindowBackground;
        TxtCustomControlBg.Text = c.ControlBackground;
        TxtCustomForeground.Text = c.Foreground;
        TxtCustomMuted.Text = c.MutedForeground;
        TxtCustomBorder.Text = c.Border;
        TxtCustomAccent.Text = c.Accent;
        TxtCustomSecondary.Text = c.SecondaryButton;
        TxtCustomDropdownBg.Text = c.DropdownBackground;
        TxtCustomDropdownHover.Text = c.DropdownItemHover;
        UpdateSwatches();
        PanelCustomTheme.Visibility = _settings.ThemeMode == "Custom" ? Visibility.Visible : Visibility.Collapsed;
    }

    private ThemeColors ReadCustomThemeFields() => new()
    {
        WindowBackground = TxtCustomWindowBg.Text.Trim(),
        ControlBackground = TxtCustomControlBg.Text.Trim(),
        Foreground = TxtCustomForeground.Text.Trim(),
        MutedForeground = TxtCustomMuted.Text.Trim(),
        Border = TxtCustomBorder.Text.Trim(),
        Accent = TxtCustomAccent.Text.Trim(),
        SecondaryButton = TxtCustomSecondary.Text.Trim(),
        DropdownBackground = TxtCustomDropdownBg.Text.Trim(),
        DropdownItemHover = TxtCustomDropdownHover.Text.Trim()
    };

    private void UpdateSwatches()
    {
        SetSwatch(SwatchWindowBg, TxtCustomWindowBg.Text);
        SetSwatch(SwatchControlBg, TxtCustomControlBg.Text);
        SetSwatch(SwatchForeground, TxtCustomForeground.Text);
        SetSwatch(SwatchMuted, TxtCustomMuted.Text);
        SetSwatch(SwatchBorder, TxtCustomBorder.Text);
        SetSwatch(SwatchAccent, TxtCustomAccent.Text);
        SetSwatch(SwatchSecondary, TxtCustomSecondary.Text);
        SetSwatch(SwatchDropdownBg, TxtCustomDropdownBg.Text);
        SetSwatch(SwatchDropdownHover, TxtCustomDropdownHover.Text);
    }

    private static void SetSwatch(Border swatch, string hex)
    {
        try { swatch.Background = new SolidColorBrush(ThemeHelper.ParseColor(hex)); }
        catch { swatch.Background = System.Windows.Media.Brushes.Transparent; }
    }

    private void BtnPickColors_Click(object sender, RoutedEventArgs e)
    {
        var fields = new (System.Windows.Controls.TextBox Txt, string Label)[]
        {
            (TxtCustomWindowBg, "Window Background"),
            (TxtCustomControlBg, "Control Background"),
            (TxtCustomForeground, "Foreground"),
            (TxtCustomMuted, "Muted Text"),
            (TxtCustomBorder, "Border"),
            (TxtCustomAccent, "Accent"),
            (TxtCustomSecondary, "Secondary Button"),
            (TxtCustomDropdownBg, "Dropdown Background"),
            (TxtCustomDropdownHover, "Dropdown Hover"),
        };

        foreach (var (txt, label) in fields)
        {
            var dlg = new ColorDialog { FullOpen = true };
            try
            {
                var c = ThemeHelper.ParseColor(txt.Text.Trim());
                dlg.Color = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
            }
            catch { }

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var sc = dlg.Color;
                txt.Text = $"#{sc.A:X2}{sc.R:X2}{sc.G:X2}{sc.B:X2}";
            }
            else
            {
                break; // user cancelled — stop prompting
            }
        }
        UpdateSwatches();
    }

    // ── Extension import ────────────────────────────────────
    private void BtnImportPlugin_Click(object sender, RoutedEventArgs e) => ImportExtension(ExtensionKind.Plugin);
    private void BtnImportWidget_Click(object sender, RoutedEventArgs e) => ImportExtension(ExtensionKind.Widget);

    private void ImportExtension(ExtensionKind kind)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Script files (*.js;*.lua)|*.js;*.lua",
            Title = $"Import {kind}"
        };
        if (dlg.ShowDialog() == true)
        {
            _extManager.ImportExtension(dlg.FileName, kind);
            LstExtensions.ItemsSource = null;
            LstExtensions.ItemsSource = _extManager.Extensions;
        }
    }

    // ── Show folders ────────────────────────────────────────
    private void BtnShowPluginFolder_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_extManager.PluginsFolderPath}\"") { UseShellExecute = false }); }
        catch { }
    }

    private void BtnShowWidgetFolder_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_extManager.WidgetsFolderPath}\"") { UseShellExecute = false }); }
        catch { }
    }

    // ── Widget settings ─────────────────────────────────────
    private string SelectedWidgetId =>
        CmbWidgetSelect.SelectedItem is ComboBoxItem item && item.Tag is string id ? id : "DesktopFolder";

    private void CmbWidgetSelect_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (_widgetIdOverride is not null) return;
        LoadWidgetSettings(SelectedWidgetId);
    }

    private void LoadWidgetSettings(string widgetId)
    {
        _loading = true;
        var ws = _settings.GetWidgetSettings(widgetId);

        SliderOpacity.Value = ws.Opacity;
        TxtOpacityValue.Text = $"{(int)(ws.Opacity * 100)}%";
        ChkTopmost.IsChecked = ws.Topmost;

        // Per-widget custom colors
        ChkWidgetCustomColors.IsChecked = ws.CustomColors is not null;
        LoadWidgetColorFields(ws);

        // Widget-type-specific panels
        var kind = widgetId.Contains('_') ? widgetId[..widgetId.LastIndexOf('_')] : widgetId;
        PanelDesktopFolderSettings.Visibility = kind == "DesktopFolder" ? Visibility.Visible : Visibility.Collapsed;

        if (kind == "DesktopFolder")
        {
            ws.Custom.TryGetValue("DefaultSort", out var sort);
            CmbDefaultSort.SelectedIndex = sort switch
            {
                "DateModified" => 1,
                "Size" => 2,
                "Type" => 3,
                _ => 0
            };

            ws.Custom.TryGetValue("DefaultFolder", out var folder);
            TxtDefaultFolder.Text = string.IsNullOrEmpty(folder)
                ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
                : folder;
        }

        _loading = false;
    }

    private void LoadWidgetColorFields(WidgetSettings ws)
    {
        var c = ws.CustomColors ?? ThemeHelper.ResolveColors(_settings);
        TxtWcWindowBg.Text = c.WindowBackground;
        TxtWcControlBg.Text = c.ControlBackground;
        TxtWcForeground.Text = c.Foreground;
        TxtWcAccent.Text = c.Accent;
        TxtWcBorder.Text = c.Border;
        TxtWcMuted.Text = c.MutedForeground;
        PanelWidgetColors.Visibility = ws.CustomColors is not null ? Visibility.Visible : Visibility.Collapsed;
    }

    private ThemeColors? ReadWidgetColorFields()
    {
        if (ChkWidgetCustomColors.IsChecked != true) return null;

        var global = ThemeHelper.ResolveColors(_settings);
        return new ThemeColors
        {
            WindowBackground = TxtWcWindowBg.Text.Trim(),
            ControlBackground = TxtWcControlBg.Text.Trim(),
            Foreground = TxtWcForeground.Text.Trim(),
            Accent = TxtWcAccent.Text.Trim(),
            Border = TxtWcBorder.Text.Trim(),
            MutedForeground = TxtWcMuted.Text.Trim(),
            SecondaryButton = global.SecondaryButton,
            DropdownBackground = TxtWcControlBg.Text.Trim(),
            DropdownItemHover = global.DropdownItemHover
        };
    }

    private void ChkWidgetCustomColors_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        PanelWidgetColors.Visibility = ChkWidgetCustomColors.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SliderOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading || TxtOpacityValue is null) return;
        TxtOpacityValue.Text = $"{(int)(SliderOpacity.Value * 100)}%";
    }

    private void ChkTopmost_Changed(object sender, RoutedEventArgs e)
    {
        // Just tracks state — saved on Save click
    }

    private void SaveWidgetSettings()
    {
        var widgetId = SelectedWidgetId;
        var ws = _settings.GetWidgetSettings(widgetId);
        var kind = widgetId.Contains('_') ? widgetId[..widgetId.LastIndexOf('_')] : widgetId;

        ws.Opacity = SliderOpacity.Value;
        ws.Topmost = ChkTopmost.IsChecked == true;
        ws.CustomColors = ReadWidgetColorFields();

        if (kind == "DesktopFolder")
        {
            ws.Custom["DefaultSort"] = CmbDefaultSort.SelectedIndex switch
            {
                1 => "DateModified",
                2 => "Size",
                3 => "Type",
                _ => "Name"
            };

            var folder = TxtDefaultFolder.Text.Trim();
            if (!string.IsNullOrEmpty(folder))
                ws.Custom["DefaultFolder"] = folder;
        }
    }

    // ── Save / Close ────────────────────────────────────────
    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        ApplySettings();
        DialogResult = true;
        Close();
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        ApplySettings();
    }

    private void ApplySettings()
    {
        if (_widgetIdOverride is null)
        {
            _settings.AccentColor = TxtAccentColor.Text;
            _settings.ThemeMode = SelectedThemeMode;

            if (SelectedThemeMode == "Custom")
                _settings.CustomTheme = ReadCustomThemeFields();
        }

        if (PanelWidgetSettings.Visibility == Visibility.Visible)
            SaveWidgetSettings();

        if (_widgetIdOverride is null)
        {
            var colors = ThemeHelper.ResolveColors(_settings);
            ThemeHelper.ApplyToApp(colors);
        }

        _settings.Save();
        _widgetSettingsApplied?.Invoke();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
