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
        RenderDiff(diff.Compare(snapshotText, currentText));
        var time = snapshotUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
        Title = $"{_localization.Get("dialog.compare")} — {time}";
        SetResourceReference(Window.BackgroundProperty, "WindowBackgroundBrush");
        RootGrid.SetResourceReference(Panel.BackgroundProperty, "WindowBackgroundBrush");
    }

    private void RenderDiff(IReadOnlyList<DiffLine> rows)
    {
        var document = new FlowDocument();
        foreach (var row in rows)
        {
            var prefix = row.Kind switch
            {
                DiffLineKind.Added => "+ ",
                DiffLineKind.Removed => "- ",
                _ => "  "
            };
            var paragraph = new Paragraph(new Run(prefix + row.Text))
            {
                Margin = new Thickness(0),
                Padding = new Thickness(6, 2, 6, 2)
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
            paragraph.SetResourceReference(TextElement.ForegroundProperty, foregroundKey);
            paragraph.SetResourceReference(TextElement.BackgroundProperty, backgroundKey);
            document.Blocks.Add(paragraph);
        }
        DiffText.Document = document;
    }
}
