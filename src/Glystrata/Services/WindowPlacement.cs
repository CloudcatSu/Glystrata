namespace Glystrata.Services;

/// <summary>
/// Where to put a window that is not owned by the main window (the reader, a floating editor), and
/// whether a remembered position is still on screen. Such a window no longer gets CenterOwner, and
/// centring it on its anchor would cover exactly what the user was looking at.
/// </summary>
internal static class WindowPlacement
{
    private const double Gap = 8;
    private const double Cascade = 40;

    /// <summary>Puts <paramref name="window"/> next to <paramref name="anchor"/> — to its right if the
    /// screen has room, else to its left, else cascaded over it — and sizes it to fit the screen.</summary>
    public static void PlaceBeside(Window window, Window anchor)
    {
        var screen = VirtualScreen();
        var width = Math.Min(window.Width, screen.Width);
        var height = Math.Min(window.Height, screen.Height);
        var anchorRect = anchor.WindowState == WindowState.Normal
            ? new Rect(anchor.Left, anchor.Top, anchor.ActualWidth, anchor.ActualHeight)
            : SystemParameters.WorkArea;

        double left;
        var top = anchorRect.Top;
        if (anchorRect.Right + Gap + width <= screen.Right)
        {
            left = anchorRect.Right + Gap;
        }
        else if (anchorRect.Left - Gap - width >= screen.Left)
        {
            left = anchorRect.Left - Gap - width;
        }
        else
        {
            left = anchorRect.Left + Cascade;
            top = anchorRect.Top + Cascade;
        }

        // A second reader opened from the same window would land exactly on the first and hide it.
        for (var step = 0; step < 10 && IsTaken(window, left, top); step++)
        {
            left += Cascade;
            top += Cascade;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Width = width;
        window.Height = height;
        window.Left = Math.Clamp(left, screen.Left, Math.Max(screen.Left, screen.Right - width));
        window.Top = Math.Clamp(top, screen.Top, Math.Max(screen.Top, screen.Bottom - height));
    }

    /// <summary>True when enough of the rectangle is on some screen to grab its title bar. A window
    /// restored from a session saved with a monitor that is no longer attached must not open off-screen.</summary>
    public static bool IsReachable(double left, double top, double width)
    {
        var screen = VirtualScreen();
        var titleBar = new Rect(left, top, Math.Max(width, 1), 32);
        var visible = Rect.Intersect(titleBar, screen);
        return !visible.IsEmpty && visible.Width >= 80 && visible.Height >= 16;
    }

    private static bool IsTaken(Window window, double left, double top) =>
        Application.Current.Windows.OfType<Window>().Any(other =>
            other != window
            && other.IsVisible
            && other.WindowState == WindowState.Normal
            && Math.Abs(other.Left - left) < 1
            && Math.Abs(other.Top - top) < 1);

    private static Rect VirtualScreen() => new(
        SystemParameters.VirtualScreenLeft,
        SystemParameters.VirtualScreenTop,
        SystemParameters.VirtualScreenWidth,
        SystemParameters.VirtualScreenHeight);
}
