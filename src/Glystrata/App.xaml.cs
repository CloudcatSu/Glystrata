using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Glystrata;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private const string InstanceMutexName = "Glystrata-SingleInstance-3F1E2C7A";
    private const string InstancePipeName = "Glystrata-SingleInstance-Pipe-3F1E2C7A";

    // Held for the app's lifetime: releasing it (even implicitly via GC) would let a second
    // launch believe no instance is running.
    private Mutex? _instanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        _instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            // Another Glystrata window is already open (e.g. the user double-clicked a document
            // in Explorer while the app was running). Hand it the path instead of opening a second window.
            ForwardToRunningInstance(e.Args);
            Shutdown();
            return;
        }

        SnapshotFileAssociation.EnsureRegistered();

        var window = new MainWindow(e.Args);
        MainWindow = window;
        StartInstancePipeServer(window);
        window.Show();
    }

    private static void ForwardToRunningInstance(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", InstancePipeName, PipeDirection.Out);
            client.Connect(2000);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            foreach (var arg in args)
            {
                writer.WriteLine(arg);
            }
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or UnauthorizedAccessException)
        {
            // 無法連線到既有執行個體時放棄轉發；使用者仍可從該視窗手動開啟檔案。
        }
    }

    private void StartInstancePipeServer(MainWindow window)
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        InstancePipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync();
                    using var reader = new StreamReader(server);
                    var paths = new List<string>();
                    string? line;
                    while ((line = await reader.ReadLineAsync()) is not null)
                    {
                        paths.Add(line);
                    }

                    if (paths.Count > 0)
                    {
                        await Dispatcher.InvokeAsync(() => window.OpenExternalPaths(paths));
                    }
                }
                catch (Exception exception) when (exception is IOException or ObjectDisposedException)
                {
                    // 連線意外中斷時繼續等待下一次轉發請求。
                }
            }
        });
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
            "Glystrata",
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
                "Glystrata");
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
