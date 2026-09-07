using MDeditor.Core.Documents;

namespace MDeditor.Core.Snapshots;

public enum DiffLineKind
{
    Unchanged,
    Added,
    Removed
}

public sealed record DiffLine(DiffLineKind Kind, string Text, int? LeftLine, int? RightLine);

public sealed class TextDiffService
{
    public IReadOnlyList<DiffLine> Compare(string leftText, string rightText)
    {
        var left = SplitLines(leftText);
        var right = SplitLines(rightText);
        var table = new int[left.Length + 1, right.Length + 1];

        for (var leftIndex = left.Length - 1; leftIndex >= 0; leftIndex--)
        {
            for (var rightIndex = right.Length - 1; rightIndex >= 0; rightIndex--)
            {
                table[leftIndex, rightIndex] = string.Equals(left[leftIndex], right[rightIndex], StringComparison.Ordinal)
                    ? table[leftIndex + 1, rightIndex + 1] + 1
                    : Math.Max(table[leftIndex + 1, rightIndex], table[leftIndex, rightIndex + 1]);
            }
        }

        var result = new List<DiffLine>();
        var i = 0;
        var j = 0;
        var leftLine = 1;
        var rightLine = 1;
        while (i < left.Length && j < right.Length)
        {
            if (string.Equals(left[i], right[j], StringComparison.Ordinal))
            {
                result.Add(new DiffLine(DiffLineKind.Unchanged, left[i], leftLine++, rightLine++));
                i++;
                j++;
            }
            else if (table[i + 1, j] >= table[i, j + 1])
            {
                result.Add(new DiffLine(DiffLineKind.Removed, left[i++], leftLine++, null));
            }
            else
            {
                result.Add(new DiffLine(DiffLineKind.Added, right[j++], null, rightLine++));
            }
        }

        while (i < left.Length)
        {
            result.Add(new DiffLine(DiffLineKind.Removed, left[i++], leftLine++, null));
        }

        while (j < right.Length)
        {
            result.Add(new DiffLine(DiffLineKind.Added, right[j++], null, rightLine++));
        }

        return result;
    }

    private static string[] SplitLines(string text) =>
        FileCodec.NormalizeLineEndings(text).Split('\n');
}
