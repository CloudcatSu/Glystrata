namespace Glystrata.Views;

public partial class DiffWindow : Window
{
    private readonly LocalizationService _localization;

    public DiffWindow(
        string currentText,
        string snapshotText,
        DateTime snapshotUtc,
        TextDiffService diff,
        LocalizationService localization)
    {
        InitializeComponent();
        _localization = localization;
        var rows = diff.Compare(snapshotText, currentText);
        foreach (var row in rows)
        {
            var prefix = row.Kind switch
            {
                DiffLineKind.Added => "+ ",
                DiffLineKind.Removed => "- ",
                _ => "  "
            };
            var text = new TextBlock
            {
                Text = prefix + row.Text,
                Padding = new Thickness(6, 2, 6, 2),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var foregroundKey = row.Kind switch
            {
                DiffLineKind.Added => "DiffAddedForegroundBrush",
                DiffLineKind.Removed => "DiffRemovedForegroundBrush",
                _ => "DiffUnchangedForegroundBrush"
            };
            var backgroundKey = row.Kind switch
            {
                DiffLineKind.Added => "DiffAddedBackgroundBrush",
                DiffLineKind.Removed => "DiffRemovedBackgroundBrush",
                _ => "DiffUnchangedBackgroundBrush"
            };
            text.SetResourceReference(TextBlock.ForegroundProperty, foregroundKey);
            text.SetResourceReference(TextBlock.BackgroundProperty, backgroundKey);
            DiffList.Items.Add(text);
        }
        var time = snapshotUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
        Title = $"{_localization.Get("dialog.compare")} — {time}";
        SetResourceReference(Window.BackgroundProperty, "WindowBackgroundBrush");
        RootGrid.SetResourceReference(Panel.BackgroundProperty, "WindowBackgroundBrush");
    }
}
