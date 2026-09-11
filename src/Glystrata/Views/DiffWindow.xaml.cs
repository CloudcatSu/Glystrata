using Glystrata.Controls;

namespace Glystrata.Views;

public partial class DiffWindow : Window
{
    private readonly LocalizationService _localization;
    private readonly TextDiffService? _diff;
    private readonly Func<string>? _getCurrentText;

    public DiffWindow(
        string currentText,
        string snapshotText,
        DateTime snapshotUtc,
        TextDiffService diff,
        LocalizationService localization)
    {
        InitializeComponent();
        CustomTitleBar.Attach(this, localization);
        _localization = localization;
        RenderDiff(diff.Compare(snapshotText, currentText));
        SetTitle(snapshotUtc);
        ApplyBackground();
    }

    public DiffWindow(
        IReadOnlyList<SnapshotInfo> snapshots,
        SnapshotInfo initial,
        Func<string> getCurrentText,
        TextDiffService diff,
        LocalizationService localization)
    {
        InitializeComponent();
        CustomTitleBar.Attach(this, localization);
        _localization = localization;
        _diff = diff;
        _getCurrentText = getCurrentText;
        SelectorPanel.Visibility = Visibility.Visible;
        SelectorLabel.Text = _localization.Get("snapshot.compareWith");
        foreach (var snapshot in snapshots)
        {
            var local = snapshot.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
            SnapshotSelector.Items.Add(new ComboBoxItem
            {
                Content = $"{local}  ·  {snapshot.Text.Length:N0} chars",
                Tag = snapshot
            });
        }
        ApplyBackground();
        SnapshotSelector.SelectedItem = SnapshotSelector.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(item => Equals(item.Tag, initial))
            ?? SnapshotSelector.Items.Cast<ComboBoxItem>().FirstOrDefault();
    }

    private void SnapshotSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SnapshotSelector.SelectedItem is not ComboBoxItem { Tag: SnapshotInfo snapshot } || _diff is null || _getCurrentText is null)
        {
            return;
        }
        RenderDiff(_diff.Compare(snapshot.Text, _getCurrentText()));
        SetTitle(snapshot.CreatedUtc);
    }

    private void SetTitle(DateTime snapshotUtc)
    {
        var time = snapshotUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
        Title = $"{_localization.Get("dialog.compare")} — {time}";
    }

    private void ApplyBackground()
    {
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
