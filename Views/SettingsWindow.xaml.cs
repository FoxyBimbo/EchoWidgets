using System.Windows;
using EchoUI.Models;
using EchoUI.Services;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace EchoUI.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly ExtensionManager _extManager;

    public SettingsWindow(AppSettings settings, ExtensionManager extManager)
    {
        InitializeComponent();
        _settings = settings;
        _extManager = extManager;
        LoadSettings();
    }

    private void LoadSettings()
    {
        TxtAccentColor.Text = _settings.AccentColor;
        LstExtensions.ItemsSource = _extManager.Extensions;
    }

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

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        _settings.AccentColor = TxtAccentColor.Text;
        _settings.Save();
        DialogResult = true;
        Close();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
