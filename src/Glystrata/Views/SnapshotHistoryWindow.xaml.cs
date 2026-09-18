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

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || _closingAfterNoteSave)
        {
            return;
        }

        var dirty = DirtyRows().ToArray();
        if (dirty.Length == 0)
        {
            return;
        }

        // Blocking on the save here would hang the app for good: it resumes on the UI thread to update
        // the row it just wrote, and the UI thread is precisely what a blocking wait would be holding.
        // So call the close off, save, and close again once the notes are safely on disk.
        e.Cancel = true;
        _ = CloseAfterSavingNotesAsync(dirty);
    }

    private async Task CloseAfterSavingNotesAsync(IEnumerable<ListBoxItem> dirty)
    {
        foreach (var item in dirty)
        {
            await SaveRowNoteAsync(item);
        }

        _closingAfterNoteSave = true;
        Close();
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
                SnapshotList.Items.Add(CreateSnapshotRow(snapshot));
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

    private static string FormatNotePreview(string note) =>
        string.IsNullOrEmpty(note) ? string.Empty : note.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Trim();

    /// <summary>
    /// One row: the timestamp and size as fixed text, then the note as a borderless text box sitting
    /// exactly where the note is read. Editing it anywhere else means looking in one place and typing
    /// in another.
    /// </summary>
    private ListBoxItem CreateSnapshotRow(SnapshotInfo snapshot)
    {
        var item = new ListBoxItem
        {
            Tag = snapshot,
            Padding = new Thickness(8, 7, 8, 7),
            // Set here rather than left to the ListBox: the row has to span the full width or the
            // note's text box only covers the few characters already in it, and there is nothing to
            // click on to start writing one.
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };

        var header = new TextBlock
        {
            Text = $"{FormatTimestamp(snapshot)}  ·  {snapshot.Text.Length:N0} chars  ·  ",
            VerticalAlignment = VerticalAlignment.Center
        };

        var hint = new TextBlock
        {
            Text = _localization.Get("snapshot.notePlaceholder"),
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Visibility = snapshot.Note.Length == 0 ? Visibility.Visible : Visibility.Collapsed
        };
        hint.SetResourceReference(ForegroundProperty, "SecondaryTextBrush");

        var editor = new TextBox
        {
            Text = FormatNotePreview(snapshot.Note),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            AcceptsReturn = false
        };
        editor.SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
        editor.TextChanged += (_, _) =>
            hint.Visibility = editor.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Clicking the note puts the caret in the text box and never reaches the row, so the row would
        // stay unselected while its note is being edited — and the buttons below act on the selection.
        editor.GotFocus += (_, _) => item.IsSelected = true;
        editor.LostFocus += (_, _) => SaveRowNote(item);
        editor.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Enter)
            {
                return;
            }

            // Committing on Enter, and swallowing it, so it cannot reach the dialog's buttons.
            args.Handled = true;
            SnapshotList.Focus();
        };

        var noteArea = new Grid();
        noteArea.Children.Add(hint);
        noteArea.Children.Add(editor);

        var row = new DockPanel();
        DockPanel.SetDock(header, Dock.Left);
        row.Children.Add(header);
        row.Children.Add(noteArea);
        item.Content = row;
        return item;
    }

    private TextBox? FindRowEditor(ListBoxItem item) =>
        (item.Content as DockPanel)?.Children.OfType<Grid>().FirstOrDefault()?.Children.OfType<TextBox>().FirstOrDefault();

    private SnapshotInfo? SelectedSnapshot => (SnapshotList.SelectedItem as ListBoxItem)?.Tag as SnapshotInfo;

    private void SaveRowNote(ListBoxItem item) => _ = SaveRowNoteAsync(item);

    private async Task SaveRowNoteAsync(ListBoxItem item)
    {
        if (item.Tag is not SnapshotInfo snapshot ||
            _view.Document.FilePath is not { } path ||
            FindRowEditor(item) is not { } editor)
        {
            return;
        }

        var text = editor.Text;
        if (string.Equals(text, snapshot.Note, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            if (await _snapshots.UpdateNoteAsync(path, snapshot.Id, text))
            {
                // The row now carries what is on disk, so nothing here reports itself dirty again and
                // closing the window does not re-save a note that never changed.
                item.Tag = snapshot with { Note = text };
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageDialogs.Inform(this, _localization, _localization.Get("snapshot.title"), exception.Message);
        }
    }

    private IEnumerable<ListBoxItem> DirtyRows() =>
        SnapshotList.Items.OfType<ListBoxItem>()
            .Where(item => item.Tag is SnapshotInfo snapshot &&
                FindRowEditor(item) is { } editor &&
                !string.Equals(editor.Text, snapshot.Note, StringComparison.Ordinal));

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
