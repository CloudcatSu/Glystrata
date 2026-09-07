namespace MDeditor.Core.Persistence;

public enum AppLanguage
{
    TraditionalChinese,
    English
}

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
    public ThemeKind Theme { get; set; } = ThemeKind.Light;
    public EditorColorPalette LightEditorPalette { get; set; } = EditorColorPalette.CreateLightDefault();
    public EditorColorPalette DarkEditorPalette { get; set; } = EditorColorPalette.CreateDarkDefault();
    public PreviewTypography PreviewTypography { get; set; } = new();
    public int SnapshotIntervalMinutes { get; set; } = 5;
    public int MaxSnapshotsPerFile { get; set; } = 20;
    public CharacterCountMode CharacterCountMode { get; set; } = CharacterCountMode.IncludeWhitespace;
    public bool ShowFormattingToolbar { get; set; }

    public void Normalize()
    {
        SnapshotIntervalMinutes = Math.Clamp(SnapshotIntervalMinutes, 1, 120);
        MaxSnapshotsPerFile = Math.Clamp(MaxSnapshotsPerFile, 1, 200);
        PreviewTypography ??= new PreviewTypography();
        LightEditorPalette ??= EditorColorPalette.CreateLightDefault();
        DarkEditorPalette ??= EditorColorPalette.CreateDarkDefault();
        if (!Enum.IsDefined(CharacterCountMode))
        {
            CharacterCountMode = CharacterCountMode.IncludeWhitespace;
        }
    }
}
