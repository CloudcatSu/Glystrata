using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace MDeditor;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        var window = new MainWindow(e.Args);
        MainWindow = window;
        window.Show();
    }

    private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);
        if (e.Exception is not Win32Exception { NativeErrorCode: 1816 })
        {
            return;
        }

        e.Handled = true;
        MessageBox.Show(
            "Windows 桌面資源暫時不足（1816）。目前編輯內容仍保留，請關閉不使用的預覽或設定視窗後繼續。\n\nWindows desktop resources are temporarily exhausted (1816). Your current edits are still in memory; close unused preview or settings windows and continue.",
            "MDeditor",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private static void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            LogException(exception);
        }
    }

    private static void LogException(Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MDeditor");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "errors.log");
            File.AppendAllText(
                path,
                $"[{DateTimeOffset.Now:O}] {exception}\r\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch
        {
            // 錯誤記錄失敗時不應再產生第二個例外。
        }
    }
}
