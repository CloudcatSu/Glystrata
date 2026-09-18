namespace Glystrata.Core.Persistence;

public enum AppLanguage
{
    TraditionalChinese,
    English
}

/// <summary>What the user picked in Preferences. The numeric values are pinned: settings.json stores
/// enums as numbers, so a 0 or 1 written by a build that predates "follow the system" has to keep
/// meaning Light and Dark, or an existing install would silently switch theme on upgrade.</summary>
public enum ThemePreference
{
    Light = 0,
    Dark = 1,
    System = 2
}

/// <summary>The theme actually in effect, once <see cref="ThemePreference.System"/> has been resolved
/// against Windows. This is what picks a palette or a resource dictionary.</summary>
public enum ThemeKind
{
    Light,
    Dark
}

public enum CharacterCountMode
{
    IncludeWhitespace,
    ExcludeWhitespace,
    ExcludeLineBreaks
}

public sealed class EditorColorPalette
{
    public Dictionary<string, string> Colors { get; set; } = new(StringComparer.Ordinal)
    {
        ["plain"] = "#1F2328",
        ["heading"] = "#0969DA",
        ["emphasis"] = "#8250DF",
        ["link"] = "#0969DA",
        ["list"] = "#57606A",
        ["quote"] = "#6E7781",
        ["code"] = "#953800",
        ["yaml-key"] = "#0550AE",
        ["yaml-value"] = "#0A3069",
        ["yaml-comment"] = "#6E7781",
        ["front-matter"] = "#8250DF"
    };

    public static EditorColorPalette CreateLightDefault() => new();

    public static EditorColorPalette CreateDarkDefault() => new()
    {
        Colors = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["plain"] = "#E6EDF3",
            ["heading"] = "#79C0FF",
            ["emphasis"] = "#D2A8FF",
            ["link"] = "#79C0FF",
            ["list"] = "#8B949E",
            ["quote"] = "#8B949E",
            ["code"] = "#FFA657",
            ["yaml-key"] = "#A5D6FF",
            ["yaml-value"] = "#C9D1D9",
            ["yaml-comment"] = "#8B949E",
            ["front-matter"] = "#D2A8FF"
        }
    };

    public string Get(string key, string fallback) =>
        Colors.TryGetValue(key, out var value) ? value : fallback;

    public EditorColorPalette Clone() => new()
    {
        Colors = new Dictionary<string, string>(Colors, StringComparer.Ordinal)
    };
}

public sealed class ReaderColorPalette
{
    public Dictionary<string, string> Colors { get; set; } = new(StringComparer.Ordinal)
    {
        ["text"] = "#24292F",
        ["heading"] = "#24292F",
        ["link"] = "#0969DA",
        ["quote"] = "#68707C",
        ["code"] = "#24292F"
    };

    // Kept monochrome by default (aside from links, which stay accent-colored so they still read as
    // clickable) so the reader looks the same out of the box; only an explicit choice in Settings
    // introduces color.
    public static ReaderColorPalette CreateLightDefault() => new();

    public static ReaderColorPalette CreateDarkDefault() => new()
    {
        Colors = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["text"] = "#E6EAF0",
            ["heading"] = "#E6EAF0",
            ["link"] = "#79C0FF",
            ["quote"] = "#A6ADB8",
            ["code"] = "#E6EAF0"
        }
    };

    public string Get(string key, string fallback) =>
        Colors.TryGetValue(key, out var value) ? value : fallback;

    public ReaderColorPalette Clone() => new()
    {
        Colors = new Dictionary<string, string>(Colors, StringComparer.Ordinal)
    };
}

public sealed class PreviewTypography
{
    public double H1Size { get; set; } = 32;
    public double H2Size { get; set; } = 26;
    public double H3Size { get; set; } = 22;
    public double H4Size { get; set; } = 18;
    public double H5Size { get; set; } = 16;
    public double H6Size { get; set; } = 14;
    public double LineSpacing { get; set; } = 1.35;
    public double ParagraphSpacing { get; set; } = 10;
    public double HeadingSpacing { get; set; } = 14;
}

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public AppLanguage Language { get; set; } = AppLanguage.TraditionalChinese;
    public ThemePreference Theme { get; set; } = ThemePreference.System;
    public EditorColorPalette LightEditorPalette { get; set; } = EditorColorPalette.CreateLightDefault();
    public EditorColorPalette DarkEditorPalette { get; set; } = EditorColorPalette.CreateDarkDefault();
    public ReaderColorPalette LightReaderPalette { get; set; } = ReaderColorPalette.CreateLightDefault();
    public ReaderColorPalette DarkReaderPalette { get; set; } = ReaderColorPalette.CreateDarkDefault();
    public PreviewTypography PreviewTypography { get; set; } = new();
    public int SnapshotIntervalMinutes { get; set; } = 5;
    public int MaxSnapshotsPerFile { get; set; } = 20;
    public CharacterCountMode CharacterCountMode { get; set; } = CharacterCountMode.IncludeWhitespace;
    public bool ShowFormattingToolbar { get; set; }
    public bool HideSnapshotFiles { get; set; }

    public void Normalize()
    {
        SnapshotIntervalMinutes = Math.Clamp(SnapshotIntervalMinutes, 1, 120);
        MaxSnapshotsPerFile = Math.Clamp(MaxSnapshotsPerFile, 1, 200);
        PreviewTypography ??= new PreviewTypography();
        LightEditorPalette ??= EditorColorPalette.CreateLightDefault();
        DarkEditorPalette ??= EditorColorPalette.CreateDarkDefault();
        LightReaderPalette ??= ReaderColorPalette.CreateLightDefault();
        DarkReaderPalette ??= ReaderColorPalette.CreateDarkDefault();
        if (!Enum.IsDefined(CharacterCountMode))
        {
            CharacterCountMode = CharacterCountMode.IncludeWhitespace;
        }
        if (!Enum.IsDefined(Theme))
        {
            Theme = ThemePreference.System;
        }
    }
}
