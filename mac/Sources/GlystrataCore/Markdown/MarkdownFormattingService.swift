import Foundation

public enum MarkdownFormatCommand: Sendable, CaseIterable {
    case paragraph
    case heading1
    case heading2
    case heading3
    case heading4
    case heading5
    case heading6
    case bold
    case italic
    case strikethrough
    case inlineCode
    case link
    case unorderedList
    case orderedList
    case quote
    case codeBlock
}

/// Offsets are UTF-16 code units, the same unit NSTextView selections use.
public struct MarkdownEditResult: Equatable, Sendable {
    public var text: String
    public var selectionStart: Int
    public var selectionLength: Int
}

public struct MarkdownFormattingService: Sendable {
    public init() {}

    public func apply(
        _ text: String,
        selectionStart: Int,
        selectionLength: Int,
        command: MarkdownFormatCommand,
        linkURL: String? = nil
    ) -> MarkdownEditResult {
        let source = text as NSString
        let start = min(max(selectionStart, 0), source.length)
        let length = min(max(selectionLength, 0), source.length - start)

        switch command {
        case .bold:
            return Self.applyInline(source, start, length, marker: "**", placeholder: "bold")
        case .italic:
            return Self.applyInline(source, start, length, marker: "*", placeholder: "italic")
        case .strikethrough:
            return Self.applyInline(source, start, length, marker: "~~", placeholder: "strikethrough")
        case .inlineCode:
            return Self.applyInline(source, start, length, marker: "`", placeholder: "code")
        case .link:
            return Self.applyLink(source, start, length, linkURL: linkURL)
        case .paragraph, .heading1, .heading2, .heading3, .heading4, .heading5, .heading6:
            return Self.applyHeading(source, start, length, level: Self.headingLevel(command))
        case .unorderedList:
            return Self.applyLinePrefix(source, start, length, prefix: "- ", hasPrefix: Self.isUnorderedList, strip: Self.stripUnorderedList)
        case .orderedList:
            return Self.applyLinePrefix(source, start, length, prefix: "1. ", hasPrefix: Self.isOrderedList, strip: Self.stripOrderedList)
        case .quote:
            return Self.applyLinePrefix(source, start, length, prefix: "> ", hasPrefix: Self.isQuote, strip: Self.stripQuote)
        case .codeBlock:
            return Self.applyCodeBlock(source, start, length)
        }
    }

    private static func applyInline(
        _ source: NSString,
        _ start: Int,
        _ length: Int,
        marker: String,
        placeholder: String
    ) -> MarkdownEditResult {
        if length == 0 {
            return replace(source, start, 0, with: marker + placeholder + marker,
                           selectionStart: start + marker.utf16Length, selectionLength: placeholder.utf16Length)
        }

        let selected = source.substring(with: NSRange(location: start, length: length))
        return replace(source, start, length, with: marker + selected + marker,
                       selectionStart: start + marker.utf16Length, selectionLength: length)
    }

    private static func applyLink(_ source: NSString, _ start: Int, _ length: Int, linkURL: String?) -> MarkdownEditResult {
        let trimmedURL = linkURL?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        let url = trimmedURL.isEmpty ? "https://" : trimmedURL
        let selected = length == 0 ? "link text" : source.substring(with: NSRange(location: start, length: length))
        return replace(source, start, length, with: "[\(selected)](\(url))",
                       selectionStart: start + 1, selectionLength: selected.utf16Length)
    }

    private static func applyHeading(_ source: NSString, _ start: Int, _ length: Int, level: Int) -> MarkdownEditResult {
        let (blockStart, blockLength) = lineRange(source, start, length)
        let block = source.substring(with: NSRange(location: blockStart, length: blockLength))
        let lines = block.components(separatedBy: "\n")
        let prefix = level == 0 ? "" : String(repeating: "#", count: level) + " "
        let transformed = lines.map { prefix + stripHeading($0) }.joined(separator: "\n")

        guard length == 0 else {
            return replace(source, blockStart, blockLength, with: transformed,
                           selectionStart: blockStart, selectionLength: transformed.utf16Length)
        }

        if block.isEmpty {
            let placeholder = level == 0 ? "text" : "heading"
            return replace(source, blockStart, blockLength, with: prefix + placeholder,
                           selectionStart: blockStart + prefix.utf16Length, selectionLength: placeholder.utf16Length)
        }

        // Keep the caret on the same character of the line's content as the prefix changes length.
        let oldLine = lines[0]
        let oldPrefixLength = headingPrefixLength(oldLine)
        let column = min(max(start - blockStart, 0), oldLine.utf16Length)
        let contentColumn = min(max(column - oldPrefixLength, 0), max(0, oldLine.utf16Length - oldPrefixLength))
        let caret = min(transformed.utf16Length, prefix.utf16Length + contentColumn)
        return replace(source, blockStart, blockLength, with: transformed,
                       selectionStart: blockStart + caret, selectionLength: 0)
    }

    private static func applyLinePrefix(
        _ source: NSString,
        _ start: Int,
        _ length: Int,
        prefix: String,
        hasPrefix: (String) -> Bool,
        strip: (String) -> String
    ) -> MarkdownEditResult {
        let (blockStart, blockLength) = lineRange(source, start, length)
        let block = source.substring(with: NSRange(location: blockStart, length: blockLength))
        let lines = block.components(separatedBy: "\n")
        let remove = lines.allSatisfy(hasPrefix)
        let transformed = lines.map { remove ? strip($0) : prefix + $0 }.joined(separator: "\n")

        if length == 0 {
            let shift = remove ? -prefix.utf16Length : prefix.utf16Length
            let caret = min(transformed.utf16Length, max(0, start - blockStart) + shift)
            return replace(source, blockStart, blockLength, with: transformed,
                           selectionStart: blockStart + max(0, caret), selectionLength: 0)
        }

        return replace(source, blockStart, blockLength, with: transformed,
                       selectionStart: blockStart, selectionLength: transformed.utf16Length)
    }

    private static func applyCodeBlock(_ source: NSString, _ start: Int, _ length: Int) -> MarkdownEditResult {
        let (blockStart, blockLength) = lineRange(source, start, length)
        let block = source.substring(with: NSRange(location: blockStart, length: blockLength))
        let lines = block.components(separatedBy: "\n")
        let isFence: (String) -> Bool = { $0.trimmingCharacters(in: .whitespacesAndNewlines) == "```" }
        if lines.count >= 2, isFence(lines[0]), isFence(lines[lines.count - 1]) {
            let inner = lines[1..<(lines.count - 1)].joined(separator: "\n")
            return replace(source, blockStart, blockLength, with: inner,
                           selectionStart: blockStart, selectionLength: length == 0 ? 0 : inner.utf16Length)
        }

        return replace(source, blockStart, blockLength, with: "```\n" + block + "\n```",
                       selectionStart: blockStart + 4, selectionLength: block.utf16Length)
    }

    /// The whole lines the selection touches, without the final line break.
    private static func lineRange(_ source: NSString, _ start: Int, _ length: Int) -> (start: Int, length: Int) {
        let newline = "\n"
        var blockStart = 0
        if start > 0 {
            let found = source.range(of: newline, options: [.backwards, .literal], range: NSRange(location: 0, length: start))
            blockStart = found.location == NSNotFound ? 0 : found.location + 1
        }

        let selectionEnd = start + length
        var blockEnd = source.length
        if selectionEnd < source.length {
            let found = source.range(of: newline, options: .literal,
                                     range: NSRange(location: selectionEnd, length: source.length - selectionEnd))
            if found.location != NSNotFound {
                blockEnd = found.location
            }
        }

        // A selection that ends just past a line break was meant to stop at that line, not to pull
        // the following one in.
        if length > 0, source.character(at: selectionEnd - 1) == 0x0A {
            blockEnd -= 1
        }

        return (blockStart, max(0, blockEnd - blockStart))
    }

    private static func replace(
        _ source: NSString,
        _ start: Int,
        _ length: Int,
        with replacement: String,
        selectionStart: Int,
        selectionLength: Int
    ) -> MarkdownEditResult {
        let result = source.replacingCharacters(in: NSRange(location: start, length: length), with: replacement)
        let resultLength = result.utf16Length
        let clampedStart = min(max(selectionStart, 0), resultLength)
        return MarkdownEditResult(
            text: result,
            selectionStart: clampedStart,
            selectionLength: min(max(selectionLength, 0), resultLength - clampedStart))
    }

    private static func headingLevel(_ command: MarkdownFormatCommand) -> Int {
        switch command {
        case .heading1: return 1
        case .heading2: return 2
        case .heading3: return 3
        case .heading4: return 4
        case .heading5: return 5
        case .heading6: return 6
        default: return 0
        }
    }

    /// Length of the leading run of "#" plus the single space after it, if there is one.
    private static func headingPrefixLength(_ line: String) -> Int {
        var count = 0
        var iterator = line.utf16.makeIterator()
        var next = iterator.next()
        while next == 0x23 {
            count += 1
            next = iterator.next()
        }
        if count > 0 && next == 0x20 {
            count += 1
        }
        return count
    }

    private static func stripHeading(_ line: String) -> String {
        let prefixLength = headingPrefixLength(line)
        return prefixLength == 0 ? line : (line as NSString).substring(from: prefixLength)
    }

    private static func splitIndent(_ line: String) -> (indent: String, content: String) {
        let scalars = line.unicodeScalars
        let index = scalars.firstIndex { !$0.properties.isWhitespace } ?? scalars.endIndex
        return (String(scalars[..<index]), String(scalars[index...]))
    }

    private static func isUnorderedList(_ line: String) -> Bool {
        let content = splitIndent(line).content
        return content.hasPrefix("- ") || content.hasPrefix("* ") || content.hasPrefix("+ ")
    }

    private static func stripUnorderedList(_ line: String) -> String {
        guard isUnorderedList(line) else {
            return line
        }

        let (indent, content) = splitIndent(line)
        return indent + String(content.unicodeScalars.dropFirst(2))
    }

    /// Scalars up to and including the "." or ")" of an ordered-list marker, or nil if the line does not start with one.
    private static func orderedMarkerLength(_ content: String) -> Int? {
        let scalars = Array(content.unicodeScalars)
        var index = 0
        while index < scalars.count, scalars[index].properties.generalCategory == .decimalNumber {
            index += 1
        }

        guard index > 0, index + 1 < scalars.count,
              scalars[index] == "." || scalars[index] == ")",
              scalars[index + 1].properties.isWhitespace else {
            return nil
        }

        return index + 1
    }

    private static func isOrderedList(_ line: String) -> Bool {
        orderedMarkerLength(splitIndent(line).content) != nil
    }

    private static func stripOrderedList(_ line: String) -> String {
        let (indent, content) = splitIndent(line)
        guard let markerLength = orderedMarkerLength(content) else {
            return line
        }

        let rest = content.unicodeScalars.dropFirst(markerLength).drop { $0.properties.isWhitespace }
        return indent + String(rest)
    }

    private static func isQuote(_ line: String) -> Bool {
        let content = splitIndent(line).content
        return content.hasPrefix("> ") || content == ">"
    }

    private static func stripQuote(_ line: String) -> String {
        let (indent, content) = splitIndent(line)
        if content.hasPrefix("> ") {
            return indent + String(content.unicodeScalars.dropFirst(2))
        }
        return content == ">" ? indent : line
    }
}
