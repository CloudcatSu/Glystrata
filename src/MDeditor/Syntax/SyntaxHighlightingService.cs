using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace MDeditor.Syntax;

public sealed class SyntaxHighlightingService
{
    public SyntaxColorizingTransformer CreateTransformer(DocumentSession document, EditorColorPalette palette)
    {
        return new SyntaxColorizingTransformer(document, palette);
    }
}

public sealed class SyntaxColorizingTransformer : DocumentColorizingTransformer
{
    private static readonly Regex HeadingRegex = new(@"^\s{0,3}#{1,6}(?:\s+.*)?$", RegexOptions.Compiled);
    private static readonly Regex QuoteRegex = new(@"^\s*>\s?.*$", RegexOptions.Compiled);
    private static readonly Regex ListRegex = new(@"^\s*(?:[-*+]\s+|\d+[.)]\s+).*$", RegexOptions.Compiled);
    private static readonly Regex FenceRegex = new(@"^\s*(`{3,}|~{3,}).*$", RegexOptions.Compiled);
    private static readonly Regex LinkRegex = new(@"!?(?:\[[^\]]+\]\([^\)]*\)|\[[^\]]+\]\[[^\]]*\])", RegexOptions.Compiled);
    private static readonly Regex InlineCodeRegex = new("`[^`\\r\\n]+`", RegexOptions.Compiled);
    private static readonly Regex EmphasisRegex = new(@"(?:\*\*|__|\*|_)[^\r\n]+?(?:\*\*|__|\*|_)", RegexOptions.Compiled);
    private static readonly Regex YamlKeyRegex = new(@"^\s*[-?]?\s*([A-Za-z_][A-Za-z0-9_.-]*)(?=\s*:)", RegexOptions.Compiled);
    private static readonly Regex YamlValueRegex = new(@"(?<=:\s?)(?!$).+$", RegexOptions.Compiled);
    private static readonly Regex CommentRegex = new(@"(?:^|\s)(#.*)$", RegexOptions.Compiled);

    private readonly DocumentSession _document;
    private EditorColorPalette _palette;
    private int _lineOffset;

    public SyntaxColorizingTransformer(DocumentSession document, EditorColorPalette palette)
    {
        _document = document;
        _palette = palette;
    }

    public void SetPalette(EditorColorPalette palette)
    {
        _palette = palette;
        CurrentContext?.TextView.Redraw();
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        var text = CurrentContext.Document.GetText(line.Offset, line.Length);
        _lineOffset = line.Offset;
        if (text.Length == 0)
        {
            return;
        }

        var isYamlFile = IsYamlFile(_document.FilePath);
        var isFrontMatter = IsFrontMatterLine(line.LineNumber, text);

        if (isFrontMatter)
        {
            Colorize(0, text.Length, "front-matter");
        }

        if (HeadingRegex.IsMatch(text))
        {
            Colorize(0, text.Length, "heading");
            return;
        }

        if (FenceRegex.IsMatch(text))
        {
            Colorize(0, text.Length, "code");
            return;
        }

        if (isYamlFile || IsLikelyYamlFrontMatter(line.LineNumber))
        {
            foreach (Match match in YamlKeyRegex.Matches(text))
            {
                Colorize(match.Groups[1].Index, match.Groups[1].Length, "yaml-key");
            }

            foreach (Match match in YamlValueRegex.Matches(text))
            {
                Colorize(match.Index, match.Length, "yaml-value");
            }

            foreach (Match match in CommentRegex.Matches(text))
            {
                Colorize(match.Groups[1].Index, match.Groups[1].Length, "yaml-comment");
            }
        }

        if (QuoteRegex.IsMatch(text))
        {
            Colorize(0, text.Length, "quote");
        }
        else if (ListRegex.IsMatch(text))
        {
            var marker = Regex.Match(text, @"^\s*(?:[-*+]|\d+[.)])");
            Colorize(marker.Index, marker.Length, "list");
        }

        foreach (Match match in LinkRegex.Matches(text))
        {
            Colorize(match.Index, match.Length, "link");
        }

        foreach (Match match in InlineCodeRegex.Matches(text))
        {
            Colorize(match.Index, match.Length, "code");
        }

        foreach (Match match in EmphasisRegex.Matches(text))
        {
            Colorize(match.Index, match.Length, "emphasis");
        }
    }

    private void Colorize(int localStart, int length, string category)
    {
        if (length <= 0 || localStart < 0)
        {
            return;
        }

        var start = _lineOffset + localStart;
        var end = Math.Min(start + length, CurrentContext.Document.TextLength);
        if (end <= start)
        {
            return;
        }

        var brush = CreateBrush(category);
        ChangeLinePart(start, end, element => element.TextRunProperties.SetForegroundBrush(brush));
    }

    private Brush CreateBrush(string category)
    {
        var fallback = category switch
        {
            "heading" => "#0969DA",
            "emphasis" => "#8250DF",
            "link" => "#0969DA",
            "list" => "#57606A",
            "quote" => "#6E7781",
            "code" => "#953800",
            "yaml-key" => "#0550AE",
            "yaml-value" => "#0A3069",
            "yaml-comment" => "#6E7781",
            "front-matter" => "#8250DF",
            _ => "#1F2328"
        };

        var value = _palette.Get(category, fallback);
        try
        {
            var brush = (Brush?)new BrushConverter().ConvertFromString(value) ?? new SolidColorBrush(Colors.Gray);
            brush.Freeze();
            return brush;
        }
        catch (FormatException)
        {
            return new SolidColorBrush(Colors.Gray);
        }
    }

    private bool IsFrontMatterLine(int lineNumber, string text)
    {
        if (lineNumber > 30)
        {
            return false;
        }

        var firstLine = CurrentContext.Document.GetLineByNumber(1);
        if (CurrentContext.Document.GetText(firstLine.Offset, firstLine.Length).Trim() != "---")
        {
            return false;
        }

        if (text.Trim() == "---" && lineNumber != 1)
        {
            return true;
        }

        return lineNumber > 1 && !HasClosedFrontMatter(lineNumber);
    }

    private bool IsLikelyYamlFrontMatter(int lineNumber)
    {
        return lineNumber <= 30 && CurrentContext.Document.GetLineByNumber(1) is { } firstLine &&
               CurrentContext.Document.GetText(firstLine.Offset, firstLine.Length).Trim() == "---" &&
               !HasClosedFrontMatter(lineNumber);
    }

    private bool HasClosedFrontMatter(int currentLine)
    {
        for (var number = 2; number <= currentLine; number++)
        {
            var candidate = CurrentContext.Document.GetLineByNumber(number);
            if (CurrentContext.Document.GetText(candidate.Offset, candidate.Length).Trim() == "---")
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsYamlFile(string? path)
    {
        var extension = Path.GetExtension(path ?? string.Empty);
        return extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase);
    }
}
