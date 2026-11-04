using ICSharpCode.AvalonEdit;
using Glystrata.Controls;

namespace Glystrata.Views;

public partial class FindReplaceWindow : Window
{
    private readonly LocalizationService _localization;
    private readonly Func<DocumentViewState?> _getView;
    private readonly Func<TextEditor?> _getEditor;
    // Offset and length of each match. The length is the matched text's, which a culture-aware
    // comparison can make different from the query's (ignorable characters, expansions).
    private List<(int Offset, int Length)> _matches = new();
    private int _matchIndex = -1;

    public FindReplaceWindow(Window owner, LocalizationService localization, Func<DocumentViewState?> getView, Func<TextEditor?> getEditor)
    {
        InitializeComponent();
        Owner = owner;
        CustomTitleBar.Attach(this, localization);
        _localization = localization;
        _getView = getView;
        _getEditor = getEditor;
        ApplyLocalization();
        FindBox.TextChanged += (_, _) => InvalidateMatches();
        FindBox.KeyDown += FindBox_KeyDown;
        ReplaceBox.KeyDown += ReplaceBox_KeyDown;
        Loaded += (_, _) => FindBox.Focus();
    }

    public void Seed(string? initialQuery, bool focusReplace)
    {
        if (!string.IsNullOrEmpty(initialQuery))
        {
            FindBox.Text = initialQuery;
        }
        InvalidateMatches();
        if (focusReplace)
        {
            ReplaceBox.Focus();
            ReplaceBox.SelectAll();
        }
        else
        {
            FindBox.Focus();
            FindBox.SelectAll();
        }
    }

    private void ApplyLocalization()
    {
        Title = _localization.Get("findReplace.title");
        FindLabel.Text = _localization.Get("findReplace.find");
        ReplaceLabel.Text = _localization.Get("findReplace.replaceWith");
        FindButton.Content = _localization.Get("findReplace.findButton");
        PreviousButton.Content = _localization.Get("findReplace.previous");
        NextButton.Content = _localization.Get("findReplace.next");
        ReplaceButton.Content = _localization.Get("findReplace.replace");
        ReplaceAllButton.Content = _localization.Get("findReplace.replaceAll");
    }

    private void FindBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Navigate(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void ReplaceBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ReplaceCurrent();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void FindButton_Click(object sender, RoutedEventArgs e) => Navigate(1);

    private void PreviousButton_Click(object sender, RoutedEventArgs e) => Navigate(-1);

    private void NextButton_Click(object sender, RoutedEventArgs e) => Navigate(1);

    private void ReplaceButton_Click(object sender, RoutedEventArgs e) => ReplaceCurrent();

    private void ReplaceAllButton_Click(object sender, RoutedEventArgs e) => ReplaceAll();

    private void InvalidateMatches()
    {
        _matches.Clear();
        _matchIndex = -1;
        StatusText.Text = string.Empty;
    }

    /// <summary>Recomputes match offsets against the live document text; must be called before every navigate/replace since edits shift offsets.</summary>
    private void RecomputeMatches(DocumentViewState view)
    {
        var query = FindBox.Text;
        _matches.Clear();
        _matchIndex = -1;
        if (string.IsNullOrEmpty(query))
        {
            return;
        }

        var text = view.Document.Text;
        var index = 0;
        while (TryFindNext(text, query, index, out var found, out var length))
        {
            _matches.Add((found, length));
            index = found + length;
        }
    }

    private static bool TryFindNext(string text, string query, int startIndex, out int offset, out int length)
    {
        offset = -1;
        length = 0;
        if (startIndex >= text.Length)
        {
            return false;
        }

        var found = CultureInfo.CurrentCulture.CompareInfo.IndexOf(
            text.AsSpan(startIndex), query, CompareOptions.IgnoreCase, out length);
        // A query made only of ignorable characters matches nothing at every position.
        if (found < 0 || length == 0)
        {
            return false;
        }

        offset = startIndex + found;
        return true;
    }

    private void Navigate(int direction)
    {
        if (_getView() is not { } view || _getEditor() is not { } editor)
        {
            return;
        }

        RecomputeMatches(view);
        if (_matches.Count == 0)
        {
            StatusText.Text = string.IsNullOrEmpty(FindBox.Text) ? string.Empty : _localization.Get("findReplace.noMatches");
            return;
        }

        var hasSelection = editor.SelectionLength > 0;
        var anchor = direction >= 0
            ? (hasSelection ? editor.SelectionStart + editor.SelectionLength : editor.CaretOffset)
            : (hasSelection ? editor.SelectionStart : editor.CaretOffset);
        _matchIndex = direction >= 0
            ? _matches.FindIndex(match => match.Offset >= anchor)
            : _matches.FindLastIndex(match => match.Offset < anchor);
        if (_matchIndex < 0)
        {
            _matchIndex = direction >= 0 ? 0 : _matches.Count - 1;
        }

        SelectMatch(editor);
        UpdateStatus();
    }

    private void SelectMatch(TextEditor editor)
    {
        var (offset, length) = _matches[_matchIndex];
        editor.Select(offset, length);
        editor.ScrollToLine(editor.Document.GetLineByOffset(offset).LineNumber);
        editor.Focus();
    }

    private void UpdateStatus()
    {
        StatusText.Text = _matches.Count == 0
            ? _localization.Get("findReplace.noMatches")
            : string.Format(CultureInfo.CurrentCulture, _localization.Get("findReplace.matchCount"), _matchIndex + 1, _matches.Count);
    }

    private void ReplaceCurrent()
    {
        if (_getView() is not { } view || _getEditor() is not { } editor || string.IsNullOrEmpty(FindBox.Text))
        {
            return;
        }

        // The window is modeless, so the document may have been edited since the last search and the
        // remembered offsets can point anywhere. Only a match found in the text as it is now, and
        // still selected in the editor, gets replaced; otherwise move to the next one first.
        RecomputeMatches(view);
        _matchIndex = _matches.FindIndex(match =>
            match.Offset == editor.SelectionStart && match.Length == editor.SelectionLength);
        if (_matchIndex < 0)
        {
            Navigate(1);
        }
        if (_matchIndex < 0 || _matchIndex >= _matches.Count)
        {
            return;
        }

        var (offset, length) = _matches[_matchIndex];
        var replacement = ReplaceBox.Text;
        view.Document.TextDocument.Replace(offset, length, replacement);
        var caretAfter = offset + replacement.Length;

        RecomputeMatches(view);
        if (_matches.Count == 0)
        {
            editor.CaretOffset = Math.Min(caretAfter, view.Document.Text.Length);
            StatusText.Text = _localization.Get("findReplace.noMatches");
            return;
        }

        _matchIndex = _matches.FindIndex(candidate => candidate.Offset >= caretAfter);
        if (_matchIndex < 0)
        {
            _matchIndex = 0;
        }
        editor.CaretOffset = caretAfter;
        SelectMatch(editor);
        UpdateStatus();
    }

    private void ReplaceAll()
    {
        if (_getView() is not { } view || _getEditor() is not { } editor)
        {
            return;
        }

        var query = FindBox.Text;
        if (string.IsNullOrEmpty(query))
        {
            return;
        }

        var replacement = ReplaceBox.Text;
        var text = view.Document.Text;
        var builder = new StringBuilder();
        var count = 0;
        var index = 0;
        while (TryFindNext(text, query, index, out var found, out var length))
        {
            builder.Append(text, index, found - index);
            builder.Append(replacement);
            index = found + length;
            count++;
        }
        builder.Append(text, index, text.Length - index);

        if (count > 0)
        {
            view.Document.TextDocument.Text = builder.ToString();
        }

        InvalidateMatches();
        StatusText.Text = string.Format(CultureInfo.CurrentCulture, _localization.Get("findReplace.replacedCount"), count);
        editor.Focus();
    }
}
