import Foundation

/// One replacement in a text buffer and where the caret goes afterwards. Offsets are UTF-16 code
/// units, the unit NSTextView uses.
public struct TextEdit: Equatable, Sendable {
    public var range: NSRange
    public var replacement: String
    public var caret: Int

    public init(range: NSRange, replacement: String, caret: Int) {
        self.range = range
        self.replacement = replacement
        self.caret = caret
    }
}

/// A symbol swap that Backspace can take back while the caret still sits right after it.
public struct SymbolReplacement: Equatable, Sendable {
    public var edit: TextEdit
    public var original: String

    /// Puts the typed characters back.
    public var reverting: TextEdit {
        let replaced = NSRange(location: edit.range.location, length: (edit.replacement as NSString).length)
        return TextEdit(range: replaced, replacement: original, caret: edit.range.location + (original as NSString).length)
    }
}

/// Typing conveniences for Markdown source that still leave plain, portable Markdown behind:
/// lists and checkboxes continue on Return, and a few typed sequences become arrows, an ellipsis
/// or an em dash.
public enum MarkdownTypingAssistant {
    private static let listMarker = try! NSRegularExpression(
        pattern: #"^([ \t]*)([-*+]|\d+[.)])(\s\[[ xX]\])?(\s.*)?$"#)

    private static let symbols: [(trigger: String, replacement: String)] = [
        ("<->", "↔"),
        ("->", "→"),
        ("=>", "⇒"),
        (">=", "≥"),
        ("<=", "≤"),
        ("...", "…"),
        ("--", "—")
    ]

    /// What to do once a line break has been typed and the caret sits on the new line: repeat the
    /// list marker of the line above, or clear that line when its item was left empty.
    public static func continuation(afterLineBreakIn text: String, caret: Int) -> TextEdit? {
        let source = text as NSString
        guard caret > 0, caret <= source.length else {
            return nil
        }

        let caretLine = source.lineRange(for: NSRange(location: caret, length: 0))
        guard caretLine.location > 0 else {
            return nil
        }

        var previousStart = 0
        var previousContentsEnd = 0
        source.getLineStart(&previousStart, end: nil, contentsEnd: &previousContentsEnd, for: NSRange(location: caretLine.location - 1, length: 0))
        let previousRange = NSRange(location: previousStart, length: previousContentsEnd - previousStart)
        let previous = source.substring(with: previousRange)
        guard let match = listMarker.firstMatch(in: previous, range: NSRange(location: 0, length: (previous as NSString).length)) else {
            return nil
        }

        func group(_ index: Int) -> String? {
            let range = match.range(at: index)
            return range.location == NSNotFound ? nil : (previous as NSString).substring(with: range)
        }

        let rest = (group(4) ?? "").trimmingCharacters(in: .whitespaces)
        if rest.isEmpty {
            // Return on an empty item ends the list instead of adding another empty item.
            return TextEdit(range: previousRange, replacement: "", caret: caret - previousRange.length)
        }

        let marker = group(2) ?? ""
        let nextMarker = Int(marker.dropLast()).map { "\($0 + 1)\(marker.suffix(1))" } ?? marker
        let continuation = "\(group(1) ?? "")\(nextMarker)\(group(3) == nil ? "" : " [ ]") "

        // Whatever indentation the new line already carries is replaced, not added to; otherwise
        // every Return inside a nested list would nest one level deeper.
        var leading = 0
        while caretLine.location + leading < source.length,
              let scalar = UnicodeScalar(source.character(at: caretLine.location + leading)),
              scalar == " " || scalar == "\t" {
            leading += 1
        }
        return TextEdit(
            range: NSRange(location: caretLine.location, length: leading),
            replacement: continuation,
            caret: caretLine.location + (continuation as NSString).length)
    }

    /// The symbol swap for whatever was just typed in front of the caret, if any.
    public static func symbolReplacement(in text: String, caret: Int) -> SymbolReplacement? {
        let source = text as NSString
        guard caret <= source.length else {
            return nil
        }

        for (trigger, replacement) in symbols {
            let length = (trigger as NSString).length
            guard caret >= length else {
                continue
            }

            let range = NSRange(location: caret - length, length: length)
            guard source.substring(with: range) == trigger else {
                continue
            }

            // "---" dividers and "| --- |" table rows are left alone while they are being typed.
            if trigger == "--", looksLikeStructuralDashes(source, before: range.location) {
                continue
            }

            let edit = TextEdit(range: range, replacement: replacement, caret: range.location + (replacement as NSString).length)
            return SymbolReplacement(edit: edit, original: trigger)
        }
        return nil
    }

    private static func looksLikeStructuralDashes(_ source: NSString, before offset: Int) -> Bool {
        let lineStart = source.lineRange(for: NSRange(location: offset, length: 0)).location
        let soFar = source.substring(with: NSRange(location: lineStart, length: offset - lineStart))
        return soFar.allSatisfy { "-:| ".contains($0) }
    }
}
