namespace Glystrata.Views;

public partial class SnapshotHistoryWindow : Window
{
    private readonly DocumentViewState _view;
    private readonly ISnapshotService _snapshots;
    private readonly LocalizationService _localization;

    public SnapshotHistoryWindow(DocumentViewState view, ISnapshotService snapshots, LocalizationService localization)
    {
        InitializeComponent();
        _view = view;
        _snapshots = snapshots;
        _localization = localization;
        ApplyLocalization();
        SnapshotList.SelectionChanged += (_, _) => UpdateButtons(SelectedSnapshot is not null);
        _localization.LanguageChanged += Localization_LanguageChanged;
        RefreshAsync();
    }

    public event EventHandler<(SnapshotInfo Snapshot, RestoreMode Mode)>? RestoreRequested;

    public event EventHandler<SnapshotInfo>? CompareRequested;

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
            return;
        }

        try
        {
            var entries = await _snapshots.ListAsync(_view.Document.FilePath);
            foreach (var snapshot in entries)
            {
                var local = snapshot.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
                SnapshotList.Items.Add(new ListBoxItem
                {
                    Content = $"{local}  ·  {snapshot.Text.Length:N0} chars",
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
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show(this, exception.Message, _localization.Get("snapshot.title"), MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateButtons(false);
        }
    }

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

        var mode = MessageBox.Show(
            this,
            _localization.Get("snapshot.restoreMode"),
            _localization.Get("snapshot.title"),
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);
        if (mode == MessageBoxResult.Yes)
        {
            RestoreRequested?.Invoke(this, (snapshot, RestoreMode.ReplaceCurrent));
        }
        else if (mode == MessageBoxResult.No)
        {
            RestoreRequested?.Invoke(this, (snapshot, RestoreMode.SaveAsNewFile));
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_view.Document.FilePath is not { } path || SelectedSnapshot is not { } snapshot ||
            MessageBox.Show(this, _localization.Get("snapshot.delete"), _localization.Get("snapshot.title"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
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
            MessageBox.Show(this, exception.Message, _localization.Get("snapshot.title"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void DeleteAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (_view.Document.FilePath is not { } path ||
            MessageBox.Show(this, _localization.Get("snapshot.deleteAll"), _localization.Get("snapshot.title"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
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
            MessageBox.Show(this, exception.Message, _localization.Get("snapshot.title"), MessageBoxButton.OK, MessageBoxImage.Error);
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
        DeleteAllButton.IsEnabled = enabled;
    }
}
