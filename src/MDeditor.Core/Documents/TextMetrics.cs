using System.Globalization;
using MDeditor.Core.Persistence;

namespace MDeditor.Core.Documents;

public static class TextMetrics
{
    public static int CountCharacters(string? text, CharacterCountMode mode)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var count = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            if (mode == CharacterCountMode.ExcludeWhitespace && element.All(char.IsWhiteSpace))
            {
                continue;
            }

            if (mode == CharacterCountMode.ExcludeLineBreaks && element.All(IsLineBreak))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private static bool IsLineBreak(char value) => value is '\r' or '\n' or '\u2028' or '\u2029';
}
