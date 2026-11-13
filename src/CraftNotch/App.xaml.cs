using System;
using System.IO;
using System.Windows;

namespace CraftNotch;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            LogCrash("AppDomain", args.ExceptionObject as Exception);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            LogCrash("Dispatcher", args.Exception);
            args.Handled = true;
        };

        var win = new MainWindow();
        win.Show();
    }

    private static void LogCrash(string source, Exception? ex)
    {
        string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
        File.AppendAllText(logPath, $"[{DateTime.Now}] [{source}] {ex?.ToString()}\n\n");
    }
}
