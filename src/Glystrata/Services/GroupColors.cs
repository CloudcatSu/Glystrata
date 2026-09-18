namespace Glystrata.Services;

/// <summary>Turns a group's stored hex colour into the brushes the sidebar and the tab strip draw with.</summary>
public static class GroupColors
{
    /// <summary>The colour bar drawn beside a group and beside every tab belonging to it, or null when
    /// the group has no colour or the stored value no longer parses.</summary>
    public static SolidColorBrush? TryCreateBar(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        try
        {
            if (new BrushConverter().ConvertFromString(hex) is SolidColorBrush brush)
            {
                brush.Freeze();
                return brush;
            }
        }
        catch (FormatException)
        {
            // A hand-edited groups.json can hold anything; an unusable colour just means "no bar".
        }
        return null;
    }

    /// <summary>The background for the selected group's row: its colour mixed into the sidebar
    /// background. Blending rather than using the colour directly keeps the group name readable, and
    /// keeps one tint setting working for both the light and the dark theme.</summary>
    public static SolidColorBrush? TryCreateSelectionBackground(string? hex, double tint)
    {
        if (TryCreateBar(hex) is not { } accent || tint <= 0)
        {
            return null;
        }

        if (Application.Current.TryFindResource("SidebarBrush") is not SolidColorBrush background)
        {
            return null;
        }

        var blended = new SolidColorBrush(Blend(accent.Color, background.Color, Math.Clamp(tint, 0, 1)));
        blended.Freeze();
        return blended;
    }

    private static Color Blend(Color accent, Color background, double ratio) => Color.FromRgb(
        (byte)Math.Round(accent.R * ratio + background.R * (1 - ratio)),
        (byte)Math.Round(accent.G * ratio + background.G * (1 - ratio)),
        (byte)Math.Round(accent.B * ratio + background.B * (1 - ratio)));
}
