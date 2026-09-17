using Glystrata.Controls;
using Glystrata.Dialogs;

namespace Glystrata.Views;

public partial class SnapshotHistoryWindow : Window
{
    private readonly DocumentViewState _view;
    private readonly ISnapshotService _snapshots;
    private readonly LocalizationService _localization;
    private readonly int _maxSnapshots;
    private bool _suppressSelectionEvent;

    public SnapshotHistoryWindow(DocumentViewState view, ISnapshotService snapshots, LocalizationService localization, int maxSnapshots)
    {
        InitializeComponent();
        CustomTitleBar.Attach(this, localization);
        DialogKeys.AttachEscapeToClose(this);
        _view = view;
        _snapshots = snapshots;
        _localization = localization;
        _maxSnapshots = maxSnapshots;
        ApplyLocalization();
        SnapshotList.SelectionChanged += (_, _) =>
        {
            UpdateButtons(SelectedSnapshot is not null);
            if (!_suppressSelectionEvent && SelectedSnapshot is { } selected)
            {
                SelectedSnapshotChanged?.Invoke(this, selected);
            }
        };
        _localization.LanguageChanged += Localization_LanguageChanged;
        RefreshAsync();
    }

    public event EventHandler<(SnapshotInfo Snapshot, RestoreMode Mode)>? RestoreRequested;

    public event EventHandler<SnapshotInfo>? CompareRequested;

    /// <summary>Raised when the user picks a different snapshot, so an open compare window can follow along.</summary>
    public event EventHandler<SnapshotInfo>? SelectedSnapshotChanged;

    public void SelectSnapshot(Guid snapshotId)
    {
        var item = SnapshotList.Items.OfType<ListBoxItem>()
            .FirstOrDefault(candidate => candidate.Tag is SnapshotInfo info && info.Id == snapshotId);
        if (item is null || ReferenceEquals(item, SnapshotList.SelectedItem))
        {
            return;
        }

        _suppressSelectionEvent = true;
        try
        {
            SnapshotList.SelectedItem = item;
            SnapshotList.ScrollIntoView(item);
        }
        finally
        {
            _suppressSelectionEvent = false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _localization.LanguageChanged -= Localization_LanguageChanged;
        base.OnClosed(e);
    }

    public async void RefreshAsync()
    {
        SnapshotList.Items.Clear();
        if (_view.Document.FilePath is null)
        {
            SnapshotList.Items.Add(new ListBoxItem { Content = _localization.Get("snapshot.none"), IsEnabled = false });
            UpdateButtons(false);
            DeleteAllButton.IsEnabled = false;
            return;
        }

        try
        {
            var entries = await _snapshots.ListAsync(_view.Document.FilePath);
            foreach (var snapshot in entries)
            {
                SnapshotList.Items.Add(new ListBoxItem
                {
                    Content = $"{FormatTimestamp(snapshot)}  ·  {snapshot.Text.Length:N0} chars",
                    Tag = snapshot,
                    Padding = new Thickness(8, 7, 8, 7)
                });
            }

            if (entries.Count == 0)
            {
                SnapshotList.Items.Add(new ListBoxItem { Content = _localization.Get("snapshot.none"), IsEnabled = false });
            }
            else
            {
                SnapshotList.SelectedIndex = 0;
            }
            UpdateButtons(SelectedSnapshot is not null);
            DeleteAllButton.IsEnabled = entries.Count > 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageDialogs.Inform(this, _localization, _localization.Get("snapshot.title"), exception.Message);
            UpdateButtons(false);
            DeleteAllButton.IsEnabled = false;
        }
    }

    private static string FormatTimestamp(SnapshotInfo snapshot) =>
        snapshot.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);

    private SnapshotInfo? SelectedSnapshot => (SnapshotList.SelectedItem as ListBoxItem)?.Tag as SnapshotInfo;

    private void CompareButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSnapshot is { } snapshot)
        {
            CompareRequested?.Invoke(this, snapshot);
        }
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSnapshot is not { } snapshot)
        {
            return;
        }

        var choice = MessageDialogs.Show(
            this,
            _localization,
            _localization.Get("snapshot.restore"),
            string.Format(CultureInfo.CurrentCulture, _localization.Get("snapshot.restoreTo"), FormatTimestamp(snapshot)),
            _localization.Get("snapshot.replaceCurrent"),
            _localization.Get("snapshot.saveAsNew"),
            _localization.Get("dialog.cancel"));
        if (choice == 0)
        {
            RestoreRequested?.Invoke(this, (snapshot, RestoreMode.ReplaceCurrent));
        }
        else if (choice == 1)
        {
            RestoreRequested?.Invoke(this, (snapshot, RestoreMode.SaveAsNewFile));
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_view.Document.FilePath is not { } path || SelectedSnapshot is not { } snapshot ||
            !MessageDialogs.Confirm(
                this,
                _localization,
                _localization.Get("snapshot.delete"),
                string.Format(CultureInfo.CurrentCulture, _localization.Get("snapshot.deleteConfirm"), FormatTimestamp(snapshot)),
                _localization.Get("dialog.delete")))
        {
            return;
        }

        try
        {
            await _snapshots.DeleteAsync(path, snapshot.Id);
            RefreshAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageDialogs.Inform(this, _localization, _localization.Get("snapshot.title"), exception.Message);
        }
    }

    private async void CreateSnapshotButton_Click(object sender, RoutedEventArgs e)
    {
        if (_view.Document.FilePath is null)
        {
            MessageDialogs.Inform(this, _localization, _localization.Get("snapshot.title"), _localization.Get("dialog.noFile"));
            return;
        }

        try
        {
            var snapshot = await _snapshots.CreateAsync(_view.Document, _maxSnapshots);
            if (snapshot is null)
            {
                MessageDialogs.Inform(this, _localization, _localization.Get("snapshot.title"), _localization.Get("snapshot.unchanged"));
                return;
            }
            RefreshAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageDialogs.Inform(this, _localization, _localization.Get("snapshot.title"), exception.Message);
        }
    }

    private async void DeleteAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (_view.Document.FilePath is not { } path ||
            !MessageDialogs.Confirm(
                this,
                _localization,
                _localization.Get("snapshot.deleteAll"),
                _localization.Get("snapshot.deleteAllConfirm"),
                _localization.Get("dialog.delete")))
        {
            return;
        }

        try
        {
            await _snapshots.DeleteAllAsync(path);
            RefreshAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageDialogs.Inform(this, _localization, _localization.Get("snapshot.title"), exception.Message);
        }
    }

    private void Localization_LanguageChanged(object? sender, EventArgs e)
    {
        ApplyLocalization();
        RefreshAsync();
    }

    private void ApplyLocalization()
    {
        Title = _localization.Get("snapshot.title");
        CreateSnapshotButton.Content = _localization.Get("file.createSnapshot");
        CompareButton.Content = _localization.Get("snapshot.compare");
        RestoreButton.Content = _localization.Get("snapshot.restore");
        DeleteButton.Content = _localization.Get("snapshot.delete");
        DeleteAllButton.Content = _localization.Get("snapshot.deleteAll");
    }

    private void UpdateButtons(bool enabled)
    {
        CompareButton.IsEnabled = enabled;
        RestoreButton.IsEnabled = enabled;
        DeleteButton.IsEnabled = enabled;
    }
}
