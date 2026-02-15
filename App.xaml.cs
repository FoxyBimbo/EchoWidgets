using EchoUI.Models;
using EchoUI.Services;

namespace EchoUI
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            base.OnStartup(e);

            var settings = AppSettings.Load();
            var colors = ThemeHelper.ResolveColors(settings);
            ThemeHelper.ApplyToApp(colors);

            SessionEnding += (_, _) => EchoUI.MainWindow.DockManager.RestoreAll();
            Exit += (_, _) => EchoUI.MainWindow.DockManager.RestoreAll();
            AppDomain.CurrentDomain.ProcessExit += (_, _) => EchoUI.MainWindow.DockManager.RestoreAll();
        }
    }
}
