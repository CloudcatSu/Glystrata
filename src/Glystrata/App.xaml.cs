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
    private const int InstanceConnectTimeoutMilliseconds = 15000;

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
            if (!ForwardToRunningInstance(e.Args))
            {
                OfferToReplaceStuckInstance(e.Args);
            }

            Shutdown();
            return;
        }

        SnapshotFileAssociation.EnsureRegistered();

        var window = new MainWindow(e.Args);
        MainWindow = window;
        StartInstancePipeServer(window);
        window.Show();
    }

    /// <returns>False when the instance holding the mutex never answered on its pipe.</returns>
    private static bool ForwardToRunningInstance(string[] args)
    {
        // Before the pipe, and before this process exits: we are the one Windows currently trusts with
        // the foreground, so raising the other window is our job, not its own.
        WindowActivation.RaiseOtherInstance();

        try
        {
            using var client = new NamedPipeClientStream(".", InstancePipeName, PipeDirection.Out);
            // The pipe only opens once the other instance has built its window, and a cold start can
            // take several seconds, so a short wait would mistake a slow start for a dead one.
            client.Connect(InstanceConnectTimeoutMilliseconds);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            foreach (var arg in args)
            {
                writer.WriteLine(arg);
            }

            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 連線中斷時放棄轉發；該執行個體仍在，使用者可從它的視窗手動開啟檔案。
            return true;
        }
    }

    /// <summary>
    /// An instance that holds the mutex but never opened its pipe is stuck in start-up: it has no
    /// window, and until it is ended every later launch would forward to nothing and exit, which
    /// looks exactly like the app refusing to start. Say so, and offer to end it and start again.
    /// Instances that do have a window are left alone — they may hold unsaved work.
    /// </summary>
    private static void OfferToReplaceStuckInstance(string[] args)
    {
        var answer = MessageBox.Show(
            "Glystrata 已經在執行，但沒有回應，也沒有顯示視窗。\n要結束它並重新啟動嗎？\n\nGlystrata is already running but is not responding and shows no window.\nEnd it and start again?",
            "Glystrata",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            using var current = Process.GetCurrentProcess();
            foreach (var other in Process.GetProcessesByName(current.ProcessName))
            {
                using (other)
                {
                    if (other.Id == current.Id || other.MainWindowHandle != IntPtr.Zero)
                    {
                        continue;
                    }

                    other.Kill();
                    other.WaitForExit(5000);
                }
            }

            if (Environment.ProcessPath is { } path)
            {
                var startInfo = new ProcessStartInfo(path) { UseShellExecute = false };
                foreach (var arg in args)
                {
                    startInfo.ArgumentList.Add(arg);
                }

                Process.Start(startInfo)?.Dispose();
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            LogException(exception);
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

                    // Always surface the window, even when the second instance had no files to hand
                    // over: it has already decided to exit, so if nothing comes forward the user sees
                    // their double-click do nothing at all.
                    await Dispatcher.InvokeAsync(() =>
                    {
                        window.BringToFront();
                        if (paths.Count > 0)
                        {
                            window.OpenExternalPaths(paths);
                        }
                    });
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
