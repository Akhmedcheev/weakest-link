using System.Configuration;
using System.Data;
using System.Windows;

namespace WeakestLink;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        this.DispatcherUnhandledException += (s, e) =>
        {
            string logMsg = $"[{DateTime.Now}] CRASH: {e.Exception.Message}\n{e.Exception.StackTrace}\n";
            System.IO.File.AppendAllText("error_log.txt", logMsg);

            MessageBox.Show(
                $"КРИТИЧЕСКАЯ ОШИБКА:\n{e.Exception.Message}\n\nСтек:\n{e.Exception.StackTrace}",
                "Чёрный ящик", MessageBoxButton.OK, MessageBoxImage.Error);

            e.Handled = true;
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) => {
            var ex = (Exception)args.ExceptionObject;
            System.IO.File.WriteAllText("crash_report.txt", $"FATAL UNHANDLED EXCEPTION:\n{ex.Message}\n{ex.StackTrace}");
        };
        base.OnStartup(e);
    }
}
