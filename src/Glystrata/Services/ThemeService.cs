namespace Glystrata.Services;

public sealed class ThemeService
{
    private ResourceDictionary? _activeDictionary;
    private ThemePreference _preference = ThemePreference.System;

    public ThemeService()
    {
        SystemThemeWatcher.Changed += SystemThemeWatcher_Changed;
    }

    /// <summary>What the user picked; may be <see cref="ThemePreference.System"/>.</summary>
    public ThemePreference Preference => _preference;

    /// <summary>The theme actually applied right now. Everything that picks a palette or a colour
    /// reads this, never the preference.</summary>
    public ThemeKind CurrentTheme { get; private set; } = ThemeKind.Light;

    public event EventHandler? ThemeChanged;

    /// <summary>Resolves a preference to the theme it means at this moment. Safe to call from
    /// anywhere that only has <see cref="AppSettings"/> and no ThemeService instance.</summary>
    public static ThemeKind Resolve(ThemePreference preference) => preference switch
    {
        ThemePreference.Light => ThemeKind.Light,
        ThemePreference.Dark => ThemeKind.Dark,
        _ => SystemThemeWatcher.Read()
    };

    public void Apply(ThemePreference preference)
    {
        _preference = preference;
        ApplyResolved(Resolve(preference));
    }

    private void ApplyResolved(ThemeKind theme)
    {
        var dictionaryName = theme == ThemeKind.Dark ? "Dark.xaml" : "Light.xaml";
        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"Resources/Themes/{dictionaryName}", UriKind.Relative)
        };

        var resources = Application.Current.Resources;
        if (_activeDictionary is not null)
        {
            resources.MergedDictionaries.Remove(_activeDictionary);
        }

        resources.MergedDictionaries.Add(dictionary);
        _activeDictionary = dictionary;
        CurrentTheme = theme;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SystemThemeWatcher_Changed(object? sender, EventArgs e)
    {
        // Windows broadcasts the colour-scheme change to every top-level window, and it fires for
        // accent-colour changes too, so re-applying unconditionally would rebuild the whole shell
        // for nothing.
        if (_preference != ThemePreference.System)
        {
            return;
        }

        var resolved = SystemThemeWatcher.Read();
        if (resolved != CurrentTheme)
        {
            ApplyResolved(resolved);
        }
    }
}
