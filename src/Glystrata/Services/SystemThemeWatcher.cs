using Microsoft.Win32;

namespace Glystrata.Services;

/// <summary>Reports the Windows app theme (Settings → Personalisation → Choose your mode).
/// The current value is read straight from the registry; changes arrive as a WM_SETTINGCHANGE
/// broadcast that <see cref="Glystrata.Controls.CustomTitleBar"/> forwards here, which keeps this
/// off Microsoft.Win32.SystemEvents — a whole extra dependency, and a thread-affinity problem,
/// just to learn about one setting.</summary>
public static class SystemThemeWatcher
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";

    /// <summary>Raised when Windows reports that the colour scheme changed. It does not say which way;
    /// call <see cref="Read"/> for the new value.</summary>
    public static event EventHandler? Changed;

    public static ThemeKind Read()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
            // The value is missing on installs that never had the dark app theme; light is the
            // safe reading there, and it is also what Windows itself falls back to.
            return key?.GetValue(AppsUseLightThemeValue) is int appsUseLightTheme && appsUseLightTheme == 0
                ? ThemeKind.Dark
                : ThemeKind.Light;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return ThemeKind.Light;
        }
    }

    /// <summary>Called from a window procedure that saw WM_SETTINGCHANGE with "ImmersiveColorSet".</summary>
    public static void NotifyColorSchemeChanged() => Changed?.Invoke(null, EventArgs.Empty);
}
