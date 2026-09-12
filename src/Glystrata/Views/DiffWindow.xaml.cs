using Glystrata.Controls;

namespace Glystrata.Views;

public partial class DiffWindow : Window
{
    private readonly LocalizationService _localization;
    private readonly TextDiffService? _diff;
    private readonly Func<string>? _getCurrentText;
    private bool _suppressSelectionEvent;

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

    /// <summary>Raised when the user compares a different snapshot, so the history window can follow along.</summary>
    public event EventHandler<SnapshotInfo>? SnapshotSelected;

    /// <returns><c>false</c> when this window does not know the snapshot, e.g. it was created after the window opened.</returns>
    public bool SelectSnapshot(Guid snapshotId)
    {
        var item = SnapshotSelector.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(candidate => candidate.Tag is SnapshotInfo info && info.Id == snapshotId);
        if (item is null)
        {
            return false;
        }
        if (ReferenceEquals(item, SnapshotSelector.SelectedItem))
        {
            return true;
        }

        _suppressSelectionEvent = true;
        try
        {
            SnapshotSelector.SelectedItem = item;
        }
        finally
        {
            _suppressSelectionEvent = false;
        }
        return true;
    }

    private void SnapshotSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SnapshotSelector.SelectedItem is not ComboBoxItem { Tag: SnapshotInfo snapshot } || _diff is null || _getCurrentText is null)
        {
            return;
        }
        RenderDiff(_diff.Compare(snapshot.Text, _getCurrentText()));
        SetTitle(snapshot.CreatedUtc);
        if (!_suppressSelectionEvent)
        {
            SnapshotSelected?.Invoke(this, snapshot);
        }
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
