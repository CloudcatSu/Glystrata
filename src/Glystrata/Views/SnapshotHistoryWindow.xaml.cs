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
    private SnapshotInfo? _noteSnapshot;
    private string _noteOriginal = string.Empty;
    private bool _closingAfterNoteSave;

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
            // Save the note for the snapshot being left before loading the new one, otherwise
            // clicking straight to the next snapshot silently discards what was just typed.
            SaveNoteIfChanged();
            LoadNoteForSelection();
            if (!_suppressSelectionEvent && SelectedSnapshot is { } selected)
            {
                SelectedSnapshotChanged?.Invoke(this, selected);
            }
        };
        NoteBox.LostFocus += (_, _) => SaveNoteIfChanged();
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

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || _closingAfterNoteSave || !HasUnsavedNote())
        {
            return;
        }

        // Blocking on the save here would hang the app for good: it resumes on the UI thread to update
        // the row it just wrote, and the UI thread is precisely what a blocking wait would be holding.
        // So call the close off, save, and close again once the note is safely on disk.
        e.Cancel = true;
        _ = CloseAfterSavingNoteAsync();
    }

    private async Task CloseAfterSavingNoteAsync()
    {
        await SaveNoteIfChangedAsync();
        _closingAfterNoteSave = true;
        Close();
    }

    private bool HasUnsavedNote() =>
        _noteSnapshot is not null &&
        _view.Document.FilePath is not null &&
        !string.Equals(NoteBox.Text, _noteOriginal, StringComparison.Ordinal);

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
            LoadNoteForSelection();
            return;
        }

        try
        {
            var entries = await _snapshots.ListAsync(_view.Document.FilePath);
            foreach (var snapshot in entries)
            {
                SnapshotList.Items.Add(new ListBoxItem
                {
                    Content = FormatRow(snapshot),
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
            LoadNoteForSelection();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageDialogs.Inform(this, _localization, _localization.Get("snapshot.title"), exception.Message);
            UpdateButtons(false);
            DeleteAllButton.IsEnabled = false;
            LoadNoteForSelection();
        }
    }

    private static string FormatTimestamp(SnapshotInfo snapshot) =>
        snapshot.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);

    private static string FormatRow(SnapshotInfo snapshot)
    {
        var row = $"{FormatTimestamp(snapshot)}  ·  {snapshot.Text.Length:N0} chars";
        var notePreview = FormatNotePreview(snapshot.Note);
        return notePreview.Length == 0 ? row : $"{row}  ·  {notePreview}";
    }

    private static string FormatNotePreview(string note) =>
        string.IsNullOrEmpty(note) ? string.Empty : note.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Trim();

    private SnapshotInfo? SelectedSnapshot => (SnapshotList.SelectedItem as ListBoxItem)?.Tag as SnapshotInfo;

    private void LoadNoteForSelection()
    {
        if (SelectedSnapshot is { } snapshot)
        {
            _noteSnapshot = snapshot;
            _noteOriginal = snapshot.Note;
            NoteBox.Text = snapshot.Note;
            NoteBox.IsEnabled = true;
        }
        else
        {
            _noteSnapshot = null;
            _noteOriginal = string.Empty;
            NoteBox.Text = string.Empty;
            NoteBox.IsEnabled = false;
        }
    }

    private void SaveNoteIfChanged() => _ = SaveNoteIfChangedAsync();

    private async Task SaveNoteIfChangedAsync()
    {
        if (_noteSnapshot is not { } snapshot || _view.Document.FilePath is not { } path)
        {
            return;
        }

        var text = NoteBox.Text;
        if (string.Equals(text, _noteOriginal, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            if (await _snapshots.UpdateNoteAsync(path, snapshot.Id, text))
            {
                UpdateRowNote(snapshot.Id, text);

                // Only when the selection has not moved on while the write was in flight: otherwise
                // this would declare a different snapshot's edit already saved. Without it the box
                // still looks dirty after a successful save, so every later focus change rewrites the
                // same note and closing the window takes an extra round trip.
                if (_noteSnapshot?.Id == snapshot.Id)
                {
                    _noteSnapshot = _noteSnapshot with { Note = text };
                    _noteOriginal = text;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageDialogs.Inform(this, _localization, _localization.Get("snapshot.title"), exception.Message);
        }
    }

    // Patches just the affected row in place (content and the Tag it carries) instead of calling
    // RefreshAsync, which would reset the list's scroll position and selection.
    private void UpdateRowNote(Guid snapshotId, string note)
    {
        var item = SnapshotList.Items.OfType<ListBoxItem>()
            .FirstOrDefault(candidate => candidate.Tag is SnapshotInfo info && info.Id == snapshotId);
        if (item?.Tag is not SnapshotInfo snapshot)
        {
            return;
        }

        var updated = snapshot with { Note = note };
        item.Tag = updated;
        item.Content = FormatRow(updated);
    }

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
        NoteLabel.Text = _localization.Get("snapshot.note");
        NoteBox.ToolTip = _localization.Get("snapshot.notePlaceholder");
    }

    private void UpdateButtons(bool enabled)
    {
        CompareButton.IsEnabled = enabled;
        RestoreButton.IsEnabled = enabled;
        DeleteButton.IsEnabled = enabled;
    }
}
