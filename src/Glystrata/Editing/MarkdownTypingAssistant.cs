using ICSharpCode.AvalonEdit;

namespace Glystrata.Editing;

/// <summary>Live typing conveniences for the markdown source editor, inspired by Heptabase's typed
/// shortcuts (see Help → Markdown 語法說明) but scoped to what still leaves valid, portable Markdown
/// behind: block markers like "# " or "- " already ARE their own final syntax in a plain-text markdown
/// file, so there is nothing to convert there. What's actually new is list/checkbox continuation on
/// Enter, and inline symbol auto-replacement for arrows/ellipsis/em dash.</summary>
public static class MarkdownTypingAssistant
{
    private static readonly Regex ListMarkerPattern = new(
        @"^(?<indent>[ \t]*)(?<marker>[-*+]|\d+\.)(?<checkbox>\s\[[ xX]\])?(?<rest>\s.*)?$",
        RegexOptions.Compiled);

    private static readonly (string Trigger, string Replacement)[] SymbolReplacements =
    {
        ("<->", "↔"),
        ("->", "→"),
        ("=>", "⇒"),
        (">=", "≥"),
        ("<=", "≤"),
        ("...", "…"),
        ("--", "—")
    };

    private sealed record AutoReplace(int Offset, string Original, string Replacement);

    public static void Attach(TextEditor editor)
    {
        AutoReplace? lastReplace = null;

        editor.TextArea.TextEntered += (_, e) =>
        {
            if (e.Text is "\n" or "\r")
            {
                lastReplace = null;
                ContinueList(editor);
                return;
            }

            lastReplace = TryAutoReplaceSymbol(editor);
        };

        editor.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Back || lastReplace is not { } replace || editor.CaretOffset != replace.Offset + replace.Replacement.Length)
            {
                lastReplace = null;
                return;
            }

            editor.Document.Replace(replace.Offset, replace.Replacement.Length, replace.Original);
            editor.CaretOffset = replace.Offset + replace.Original.Length;
            lastReplace = null;
            e.Handled = true;
        };
    }

    private static void ContinueList(TextEditor editor)
    {
        var document = editor.Document;
        var caretLine = document.GetLineByOffset(editor.CaretOffset);
        if (caretLine.PreviousLine is not { } previousLine)
        {
            return;
        }

        var previousText = document.GetText(previousLine.Offset, previousLine.Length);
        var match = ListMarkerPattern.Match(previousText);
        if (!match.Success)
        {
            return;
        }

        var rest = match.Groups["rest"].Success ? match.Groups["rest"].Value.Trim() : string.Empty;
        if (rest.Length == 0)
        {
            // An empty list item + Enter clears the marker instead of repeating it (common editor convention).
            document.Remove(previousLine.Offset, previousLine.Length);
            return;
        }

        var indent = match.Groups["indent"].Value;
        var marker = match.Groups["marker"].Value;
        var nextMarker = int.TryParse(marker.TrimEnd('.'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? $"{number + 1}."
            : marker;
        var checkbox = match.Groups["checkbox"].Success ? " [ ]" : string.Empty;

        document.Insert(editor.CaretOffset, $"{indent}{nextMarker}{checkbox} ");
    }

    private static AutoReplace? TryAutoReplaceSymbol(TextEditor editor)
    {
        var document = editor.Document;
        var caretOffset = editor.CaretOffset;

        foreach (var (trigger, replacement) in SymbolReplacements)
        {
            if (caretOffset < trigger.Length)
            {
                continue;
            }

            var start = caretOffset - trigger.Length;
            if (document.GetText(start, trigger.Length) != trigger)
            {
                continue;
            }

            if (trigger == "--" && LooksLikeStructuralDashes(document, start))
            {
                // Leave "---" dividers and "| --- |" table separator rows alone while they're still being typed.
                continue;
            }

            document.Replace(start, trigger.Length, replacement);
            editor.CaretOffset = start + replacement.Length;
            return new AutoReplace(start, trigger, replacement);
        }

        return null;
    }

    private static bool LooksLikeStructuralDashes(ICSharpCode.AvalonEdit.Document.TextDocument document, int offset)
    {
        var line = document.GetLineByOffset(offset);
        var lineSoFar = document.GetText(line.Offset, offset - line.Offset);
        return lineSoFar.All(ch => ch is '-' or ':' or '|' or ' ');
    }
}
