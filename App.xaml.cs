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
            var ex = e.Exception;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[{DateTime.Now}] CRASH: {ex.Message}");
            sb.AppendLine(ex.StackTrace);
            var inner = ex.InnerException;
            while (inner != null)
            {
                sb.AppendLine($"--- Inner: {inner.GetType().FullName}: {inner.Message}");
                sb.AppendLine(inner.StackTrace);
                inner = inner.InnerException;
            }
            sb.AppendLine();
            System.IO.File.AppendAllText("error_log.txt", sb.ToString());

            string display = $"КРИТИЧЕСКАЯ ОШИБКА:\n{ex.Message}";
            if (ex.InnerException != null)
                display += $"\n\nInner: {ex.InnerException.Message}";
            display += $"\n\nСтек:\n{ex.StackTrace}";

            WeakestLink.Views.DarkMessageBox.Show(display,
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
