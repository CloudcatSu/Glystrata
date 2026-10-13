import Foundation

/// Finding and replacing text for the Find and Replace window. Offsets are UTF-16 code units, the
/// unit NSTextView uses.
public enum TextSearch {
    /// Every non-overlapping match, in order. Case is ignored unless asked for, as in the Windows build.
    public static func matches(in text: String, query: String, caseSensitive: Bool = false) -> [NSRange] {
        guard !query.isEmpty else {
            return []
        }

        let source = text as NSString
        let options: NSString.CompareOptions = caseSensitive ? [] : [.caseInsensitive]
        var found: [NSRange] = []
        var index = 0
        while index < source.length {
            let range = source.range(of: query, options: options, range: NSRange(location: index, length: source.length - index))
            guard range.location != NSNotFound else {
                break
            }
            // A query made only of ignorable characters can match nothing; step over it rather than loop.
            guard range.length > 0 else {
                index = range.location + 1
                continue
            }
            found.append(range)
            index = NSMaxRange(range)
        }
        return found
    }

    /// Which match to visit next from `anchor`, wrapping around at either end. Nil when there are none.
    public static func matchIndex(in matches: [NSRange], anchor: Int, forward: Bool) -> Int? {
        guard !matches.isEmpty else {
            return nil
        }

        if forward {
            return matches.firstIndex { $0.location >= anchor } ?? 0
        }
        return matches.lastIndex { $0.location < anchor } ?? matches.count - 1
    }

    /// The edit that puts `replacement` over `range`; the caret lands right after it.
    public static func replacing(_ range: NSRange, with replacement: String, in text: String) -> TextEdit {
        TextEdit(range: range, replacement: replacement, caret: range.location + (replacement as NSString).length)
    }

    /// One edit that rewrites every match, spanning only from the first to the last so undo and
    /// other panes' carets are left alone elsewhere. Nil when nothing matches.
    public static func replacingAll(
        in text: String, query: String, with replacement: String, caseSensitive: Bool = false
    ) -> (edit: TextEdit, count: Int)? {
        let found = matches(in: text, query: query, caseSensitive: caseSensitive)
        guard let first = found.first, let last = found.last else {
            return nil
        }

        let source = text as NSString
        var result = ""
        var cursor = first.location
        for match in found {
            result += source.substring(with: NSRange(location: cursor, length: match.location - cursor))
            result += replacement
            cursor = NSMaxRange(match)
        }
        let span = NSRange(location: first.location, length: NSMaxRange(last) - first.location)
        return (TextEdit(range: span, replacement: result, caret: first.location + (result as NSString).length), found.count)
    }
}
