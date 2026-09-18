using System.Runtime.InteropServices;

namespace Glystrata.Services;

/// <summary>
/// Raising the already-running window when a second copy of the app is launched.
///
/// Windows only lets the process that currently owns the foreground hand it to someone else; every
/// other SetForegroundWindow call is refused and downgraded to a flashing taskbar button. The running
/// instance therefore cannot raise itself — it is in the background by definition, so its own
/// Activate() only ever makes the taskbar blink. The process that *can* is the new one, which Explorer
/// granted foreground rights to when the user double-clicked. So the second instance raises the first
/// instance's window itself, synchronously, before it exits, rather than asking over the pipe and
/// racing its own shutdown.
/// </summary>
internal static class WindowActivation
{
    private const int SwRestore = 9;

    /// <summary>
    /// Restores and raises the window of another running instance of this app. Called from the second
    /// instance while it still holds foreground rights.
    /// </summary>
    public static void RaiseOtherInstance()
    {
        Process current;
        Process[] others;
        try
        {
            current = Process.GetCurrentProcess();
            others = Process.GetProcessesByName(current.ProcessName);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            return;
        }

        using (current)
        {
            foreach (var other in others)
            {
                using (other)
                {
                    try
                    {
                        if (other.Id == current.Id)
                        {
                            continue;
                        }

                        // Backstop for the window we cannot reach yet: an instance still starting up has
                        // no MainWindowHandle, so hand it our foreground rights instead and let its own
                        // BringToFront do the work once the window exists.
                        AllowSetForegroundWindow((uint)other.Id);

                        var handle = other.MainWindowHandle;
                        if (handle != IntPtr.Zero)
                        {
                            RestoreAndRaise(handle);
                        }
                    }
                    catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
                    {
                        // 該執行個體在列舉後結束了，換下一個。
                    }
                }
            }
        }
    }

    /// <summary>Un-minimizes <paramref name="handle"/> if needed and puts it in front.</summary>
    public static void RestoreAndRaise(IntPtr handle)
    {
        // Guarded by IsIconic because SW_RESTORE on a maximized window un-maximizes it: restoring is
        // only ever meant to undo the minimize, never to resize a window the user left maximized.
        if (IsIconic(handle))
        {
            ShowWindow(handle, SwRestore);
        }

        SetForegroundWindow(handle);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint dwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
