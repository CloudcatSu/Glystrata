namespace MDeditor.Core.Markdown;

public enum MarkdownFormatCommand
{
    Paragraph,
    Heading1,
    Heading2,
    Heading3,
    Heading4,
    Heading5,
    Heading6,
    Bold,
    Italic,
    Strikethrough,
    InlineCode,
    Link,
    UnorderedList,
    OrderedList,
    Quote,
    CodeBlock
}

public sealed record MarkdownEditResult(string Text, int SelectionStart, int SelectionLength);

public sealed class MarkdownFormattingService
{
    public MarkdownEditResult Apply(
        string text,
        int selectionStart,
        int selectionLength,
        MarkdownFormatCommand command,
        string? linkUrl = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var start = Math.Clamp(selectionStart, 0, text.Length);
        var length = Math.Clamp(selectionLength, 0, text.Length - start);

        return command switch
        {
            MarkdownFormatCommand.Bold => ApplyInline(text, start, length, "**", "bold"),
            MarkdownFormatCommand.Italic => ApplyInline(text, start, length, "*", "italic"),
            MarkdownFormatCommand.Strikethrough => ApplyInline(text, start, length, "~~", "strikethrough"),
            MarkdownFormatCommand.InlineCode => ApplyInline(text, start, length, "`", "code"),
            MarkdownFormatCommand.Link => ApplyLink(text, start, length, linkUrl),
            MarkdownFormatCommand.Paragraph or
            MarkdownFormatCommand.Heading1 or
            MarkdownFormatCommand.Heading2 or
            MarkdownFormatCommand.Heading3 or
            MarkdownFormatCommand.Heading4 or
            MarkdownFormatCommand.Heading5 or
            MarkdownFormatCommand.Heading6 => ApplyHeading(text, start, length, command),
            MarkdownFormatCommand.UnorderedList => ApplyListPrefix(text, start, length, "- ", IsUnorderedList, StripUnorderedList),
            MarkdownFormatCommand.OrderedList => ApplyListPrefix(text, start, length, "1. ", IsOrderedList, StripOrderedList),
            MarkdownFormatCommand.Quote => ApplyListPrefix(text, start, length, "> ", IsQuote, StripQuote),
            MarkdownFormatCommand.CodeBlock => ApplyCodeBlock(text, start, length),
            _ => new MarkdownEditResult(text, start, length)
        };
    }

    private static MarkdownEditResult ApplyInline(
        string text,
        int start,
        int length,
        string marker,
        string placeholder)
    {
        var selected = text.Substring(start, length);
        if (length == 0)
        {
            var replacement = marker + placeholder + marker;
            return Replace(text, start, 0, replacement, start + marker.Length, placeholder.Length);
        }

        var wrapped = marker + selected + marker;
        return Replace(text, start, length, wrapped, start + marker.Length, length);
    }

    private static MarkdownEditResult ApplyLink(string text, int start, int length, string? linkUrl)
    {
        var url = string.IsNullOrWhiteSpace(linkUrl) ? "https://" : linkUrl.Trim();
        var selected = length == 0 ? "link text" : text.Substring(start, length);
        var replacement = $"[{selected}]({url})";
        return Replace(text, start, length, replacement, start + 1, selected.Length);
    }

    private static MarkdownEditResult ApplyHeading(string text, int start, int length, MarkdownFormatCommand command)
    {
        var (blockStart, blockLength) = GetLineRange(text, start, length);
        var block = text.Substring(blockStart, blockLength);
        var lines = block.Split('\n', StringSplitOptions.None);
        var headingLevel = GetHeadingLevel(command);
        var transformed = string.Join('\n', lines.Select(line =>
        {
            var content = StripHeading(line);
            return headingLevel == 0 ? content : new string('#', headingLevel) + " " + content;
        }));

        if (length == 0)
        {
            var line = transformed;
            var prefixLength = headingLevel == 0 ? 0 : headingLevel + 1;
            if (block.Length == 0)
            {
                var placeholder = headingLevel == 0 ? "text" : "heading";
                var replacement = (headingLevel == 0 ? string.Empty : new string('#', headingLevel) + " ") + placeholder;
                return Replace(text, blockStart, blockLength, replacement, blockStart + prefixLength, placeholder.Length);
            }

            var oldLine = block.Split('\n', StringSplitOptions.None)[0];
            var oldPrefixLength = GetHeadingPrefixLength(oldLine);
            var column = Math.Clamp(start - blockStart, 0, oldLine.Length);
            var contentColumn = Math.Clamp(column - oldPrefixLength, 0, Math.Max(0, oldLine.Length - oldPrefixLength));
            var caret = Math.Min(line.Length, prefixLength + contentColumn);
            return Replace(text, blockStart, blockLength, transformed, blockStart + caret, 0);
        }

        return Replace(text, blockStart, blockLength, transformed, blockStart, transformed.Length);
    }

    private static MarkdownEditResult ApplyListPrefix(
        string text,
        int start,
        int length,
        string prefix,
        Func<string, bool> hasPrefix,
        Func<string, string> stripPrefix)
    {
        var (blockStart, blockLength) = GetLineRange(text, start, length);
        var block = text.Substring(blockStart, blockLength);
        var lines = block.Split('\n', StringSplitOptions.None);
        var remove = lines.Length > 0 && lines.All(hasPrefix);
        var transformed = string.Join('\n', lines.Select(line =>
        {
            if (remove)
            {
                return stripPrefix(line);
            }

            return prefix + line;
        }));
        if (length == 0)
        {
            var caret = Math.Min(transformed.Length, Math.Max(0, start - blockStart) + (remove ? -prefix.Length : prefix.Length));
            return Replace(text, blockStart, blockLength, transformed, blockStart + Math.Max(0, caret), 0);
        }

        return Replace(text, blockStart, blockLength, transformed, blockStart, transformed.Length);
    }

    private static MarkdownEditResult ApplyCodeBlock(string text, int start, int length)
    {
        var (blockStart, blockLength) = GetLineRange(text, start, length);
        var block = text.Substring(blockStart, blockLength);
        var lines = block.Split('\n', StringSplitOptions.None);
        var isWrapped = lines.Length >= 2 &&
                        lines[0].Trim() == "```" &&
                        lines[^1].Trim() == "```";
        if (isWrapped)
        {
            var inner = string.Join('\n', lines.Skip(1).Take(lines.Length - 2));
            return Replace(text, blockStart, blockLength, inner, blockStart, length == 0 ? 0 : inner.Length);
        }

        var wrapped = "```\n" + block + "\n```";
        var selectedStart = blockStart + 4;
        return Replace(text, blockStart, blockLength, wrapped, selectedStart, block.Length);
    }

    private static (int Start, int Length) GetLineRange(string text, int start, int length)
    {
        var blockStart = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;
        var selectionEnd = start + length;
        var blockEnd = selectionEnd >= text.Length ? text.Length : text.IndexOf('\n', selectionEnd);
        if (blockEnd < 0)
        {
            blockEnd = text.Length;
        }
        if (length > 0 && selectionEnd > start && selectionEnd <= text.Length && text[selectionEnd - 1] == '\n')
        {
            blockEnd--;
        }

        return (blockStart, Math.Max(0, blockEnd - blockStart));
    }

    private static MarkdownEditResult Replace(
        string text,
        int start,
        int length,
        string replacement,
        int selectionStart,
        int selectionLength)
    {
        var result = text[..start] + replacement + text[(start + length)..];
        return new MarkdownEditResult(
            result,
            Math.Clamp(selectionStart, 0, result.Length),
            Math.Clamp(selectionLength, 0, result.Length - Math.Clamp(selectionStart, 0, result.Length)));
    }

    private static int GetHeadingLevel(MarkdownFormatCommand command) => command switch
    {
        MarkdownFormatCommand.Heading1 => 1,
        MarkdownFormatCommand.Heading2 => 2,
        MarkdownFormatCommand.Heading3 => 3,
        MarkdownFormatCommand.Heading4 => 4,
        MarkdownFormatCommand.Heading5 => 5,
        MarkdownFormatCommand.Heading6 => 6,
        _ => 0
    };

    private static string StripHeading(string line)
    {
        var index = 0;
        while (index < line.Length && line[index] == '#')
        {
            index++;
        }
        if (index > 0 && index < line.Length && line[index] == ' ')
        {
            index++;
        }
        return index == 0 ? line : line[index..];
    }

    private static int GetHeadingPrefixLength(string line)
    {
        var content = StripHeading(line);
        return line.Length - content.Length;
    }

    private static bool IsUnorderedList(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("- ", StringComparison.Ordinal) ||
               trimmed.StartsWith("* ", StringComparison.Ordinal) ||
               trimmed.StartsWith("+ ", StringComparison.Ordinal);
    }

    private static string StripUnorderedList(string line)
    {
        var leadingLength = line.Length - line.TrimStart().Length;
        var trimmed = line[leadingLength..];
        return IsUnorderedList(line) ? line[..leadingLength] + trimmed[2..] : line;
    }

    private static bool IsOrderedList(string line)
    {
        var trimmed = line.TrimStart();
        var index = 0;
        while (index < trimmed.Length && char.IsDigit(trimmed[index]))
        {
            index++;
        }
        return index > 0 && index + 1 < trimmed.Length && (trimmed[index] == '.' || trimmed[index] == ')') && char.IsWhiteSpace(trimmed[index + 1]);
    }

    private static string StripOrderedList(string line)
    {
        var leadingLength = line.Length - line.TrimStart().Length;
        var trimmed = line[leadingLength..];
        var index = 0;
        while (index < trimmed.Length && char.IsDigit(trimmed[index]))
        {
            index++;
        }
        if (!IsOrderedList(line))
        {
            return line;
        }
        index++;
        while (index < trimmed.Length && char.IsWhiteSpace(trimmed[index]))
        {
            index++;
        }
        return line[..leadingLength] + trimmed[index..];
    }

    private static bool IsQuote(string line) => line.TrimStart().StartsWith("> ", StringComparison.Ordinal) || line.TrimStart() == ">";

    private static string StripQuote(string line)
    {
        var leadingLength = line.Length - line.TrimStart().Length;
        var trimmed = line[leadingLength..];
        if (trimmed.StartsWith("> ", StringComparison.Ordinal))
        {
            return line[..leadingLength] + trimmed[2..];
        }
        return trimmed == ">" ? line[..leadingLength] : line;
    }
}
