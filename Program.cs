using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace FastSnip;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, @"Local\FastSnip", out bool isFirst);
        if (!isFirst)
        {
            // Launching again while running opens the running instance's settings.
            HotkeyWindow.RequestShowSettings();
            return;
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Report(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Report(e.ExceptionObject as Exception);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayContext());
    }

    static void Report(Exception? ex)
    {
        if (ex == null) return;
        string log = Path.Combine(AppInfo.DataDir, "error.log");
        try
        {
            File.AppendAllText(log, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] v{AppInfo.Version}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Logging is best effort; never let it take the app down.
        }
        MessageBox.Show(string.Format(L.ErrorMessage, log), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
