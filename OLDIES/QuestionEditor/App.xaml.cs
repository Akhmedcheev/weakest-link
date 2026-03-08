using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace WeakestLink.QuestionEditor;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = (Exception)args.ExceptionObject;
            File.WriteAllText("question_editor_crash.txt", $"{ex}\n{ex.StackTrace}");
            MessageBox.Show($"Ошибка: {ex.Message}\n\nПодробности в question_editor_crash.txt", "Сбой редактора");
        };
        DispatcherUnhandledException += (_, args) =>
        {
            File.WriteAllText("question_editor_crash.txt", $"{args.Exception}\n{args.Exception.StackTrace}");
            MessageBox.Show($"Ошибка: {args.Exception.Message}\n\nПодробности в question_editor_crash.txt", "Сбой редактора");
            args.Handled = true;
        };
        base.OnStartup(e);
    }
}
