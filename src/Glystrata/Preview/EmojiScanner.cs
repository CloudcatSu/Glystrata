using System.Globalization;

namespace Glystrata.Preview;

/// <summary>
/// Finds emoji sequences in text and maps them to Twemoji file names (code points in lowercase hex,
/// joined with '-'), following Twemoji's own rule for variation selectors.
/// </summary>
internal static class EmojiScanner
{
    private const int ZeroWidthJoiner = 0x200D;
    private const int VariationSelector16 = 0xFE0F;
    private const int CombiningKeycap = 0x20E3;

    /// <summary>Length in UTF-16 units of the emoji sequence starting at <paramref name="index"/>, or 0.</summary>
    public static int MatchSequence(string text, int index)
    {
        if (!TryReadCodePoint(text, index, out var first, out var firstLength))
        {
            return 0;
        }

        // Digits, '#' and '*' are only emoji when a keycap follows them.
        if (IsKeycapBase(first))
        {
            var probe = index + firstLength;
            var length = firstLength;
            if (probe < text.Length && TryReadCodePoint(text, probe, out var next, out var nextLength) && next == VariationSelector16)
            {
                probe += nextLength;
                length += nextLength;
            }
            if (probe < text.Length && TryReadCodePoint(text, probe, out var keycap, out var keycapLength) && keycap == CombiningKeycap)
            {
                return length + keycapLength;
            }
            return 0;
        }

        if (!IsEmojiStart(first))
        {
            return 0;
        }

        var total = firstLength;
        var position = index + firstLength;
        while (position < text.Length && TryReadCodePoint(text, position, out var codePoint, out var codePointLength))
        {
            if (codePoint == VariationSelector16 || codePoint == CombiningKeycap || IsSkinTone(codePoint) || IsTagCharacter(codePoint))
            {
                total += codePointLength;
                position += codePointLength;
                continue;
            }

            if (codePoint == ZeroWidthJoiner)
            {
                // Only keep the joiner when another emoji follows it.
                var after = position + codePointLength;
                if (after < text.Length && TryReadCodePoint(text, after, out var joined, out var joinedLength) && IsEmojiStart(joined))
                {
                    total += codePointLength + joinedLength;
                    position = after + joinedLength;
                    continue;
                }
                break;
            }

            if (IsRegionalIndicator(first) && IsRegionalIndicator(codePoint) && total == firstLength)
            {
                total += codePointLength;
                position += codePointLength;
                continue;
            }

            break;
        }

        return total;
    }

    /// <summary>File-name keys to try, most specific first.</summary>
    public static IEnumerable<string> CandidateKeys(string sequence)
    {
        var codePoints = ToCodePoints(sequence);
        var hasJoiner = codePoints.Contains(ZeroWidthJoiner);
        // Twemoji keeps U+FE0F only in sequences that contain a zero-width joiner.
        var primary = hasJoiner ? codePoints : codePoints.Where(codePoint => codePoint != VariationSelector16).ToList();
        yield return Format(primary);

        var stripped = codePoints.Where(codePoint => codePoint != VariationSelector16).ToList();
        if (stripped.Count != primary.Count)
        {
            yield return Format(stripped);
        }

        var withoutSkinTone = stripped.Where(codePoint => !IsSkinTone(codePoint)).ToList();
        if (withoutSkinTone.Count != stripped.Count && withoutSkinTone.Count > 0)
        {
            yield return Format(withoutSkinTone);
        }

        if (codePoints.Count > 1)
        {
            yield return Format(new[] { codePoints[0] });
        }
    }

    private static string Format(IEnumerable<int> codePoints) =>
        string.Join("-", codePoints.Select(codePoint => codePoint.ToString("x", CultureInfo.InvariantCulture)));

    private static List<int> ToCodePoints(string sequence)
    {
        var result = new List<int>(sequence.Length);
        var index = 0;
        while (TryReadCodePoint(sequence, index, out var codePoint, out var length))
        {
            result.Add(codePoint);
            index += length;
        }
        return result;
    }

    private static bool TryReadCodePoint(string text, int index, out int codePoint, out int length)
    {
        codePoint = 0;
        length = 0;
        if (index < 0 || index >= text.Length)
        {
            return false;
        }

        if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            codePoint = char.ConvertToUtf32(text[index], text[index + 1]);
            length = 2;
            return true;
        }

        codePoint = text[index];
        length = 1;
        return true;
    }

    private static bool IsKeycapBase(int codePoint) =>
        codePoint == '#' || codePoint == '*' || (codePoint >= '0' && codePoint <= '9');

    private static bool IsSkinTone(int codePoint) => codePoint is >= 0x1F3FB and <= 0x1F3FF;

    private static bool IsTagCharacter(int codePoint) => codePoint is >= 0xE0020 and <= 0xE007F;

    private static bool IsRegionalIndicator(int codePoint) => codePoint is >= 0x1F1E6 and <= 0x1F1FF;

    private static bool IsEmojiStart(int codePoint) => codePoint switch
    {
        >= 0x1F000 and <= 0x1FAFF => true,
        >= 0x2600 and <= 0x27BF => true,
        >= 0x2B00 and <= 0x2BFF => true,
        0x00A9 or 0x00AE or 0x203C or 0x2049 or 0x2122 or 0x2139 => true,
        >= 0x2194 and <= 0x21AA => true,
        >= 0x231A and <= 0x231B => true,
        >= 0x23E9 and <= 0x23FA => true,
        0x24C2 => true,
        >= 0x25AA and <= 0x25FE => true,
        0x2934 or 0x2935 or 0x3030 or 0x303D or 0x3297 or 0x3299 => true,
        _ => false
    };
}
