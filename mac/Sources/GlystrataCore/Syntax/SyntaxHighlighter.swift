import Foundation

/// A stretch of one line to draw in a palette colour. `range` is in UTF-16 units from the start of
/// the line.
public struct SyntaxSpan: Equatable, Sendable {
    public var range: NSRange
    public var category: String
}

/// Where a document's YAML front matter sits, worked out once from its first lines.
public struct FrontMatterContext: Equatable, Sendable {
    /// Front matter is only looked for this far down, so a stray "---" deep in a long document
    /// cannot recolour everything above it.
    public static let lineLimit = 30

    public var opensOnFirstLine: Bool

    /// Line number (1-based) of the closing "---", if there is one within the limit.
    public var closingLine: Int?

    public init(opensOnFirstLine: Bool, closingLine: Int?) {
        self.opensOnFirstLine = opensOnFirstLine
        self.closingLine = closingLine
    }

    public init(leadingLines: some Sequence<Substring>) {
        var opens = false
        var closing: Int?
        for (index, line) in leadingLines.prefix(Self.lineLimit).enumerated() {
            let isFence = line.trimmingCharacters(in: .whitespaces) == "---"
            if index == 0 {
                opens = isFence
                if !opens {
                    break
                }
            } else if isFence {
                closing = index + 1
                break
            }
        }
        opensOnFirstLine = opens
        closingLine = closing
    }

    /// Whether the line is a fence or lies between the fences. An unclosed block counts up to the
    /// limit, so the colours already apply while it is being typed.
    public func contains(line: Int) -> Bool {
        guard opensOnFirstLine, line <= Self.lineLimit else {
            return false
        }
        return closingLine.map { line <= $0 } ?? true
    }

    /// Whether the line is YAML content: inside the block, not one of its fences.
    public func isContent(line: Int) -> Bool {
        contains(line: line) && line > 1 && line != closingLine
    }
}

/// Line-by-line colouring for Markdown and YAML. Later spans paint over earlier ones.
public enum SyntaxHighlighter {
    public static let fallbackColors: [String: String] = [
        "heading": "#0969DA",
        "emphasis": "#8250DF",
        "link": "#0969DA",
        "list": "#57606A",
        "quote": "#6E7781",
        "code": "#953800",
        "yaml-key": "#0550AE",
        "yaml-value": "#0A3069",
        "yaml-comment": "#6E7781",
        "front-matter": "#8250DF"
    ]

    public static func isYAMLFile(_ path: String?) -> Bool {
        let fileExtension = ((path ?? "") as NSString).pathExtension.lowercased()
        return fileExtension == "yml" || fileExtension == "yaml"
    }

    public static func spans(
        forLine text: String,
        lineNumber: Int,
        isYAMLFile: Bool,
        frontMatter: FrontMatterContext
    ) -> [SyntaxSpan] {
        let length = text.utf16Length
        guard length > 0 else {
            return []
        }

        let whole = NSRange(location: 0, length: length)
        var spans: [SyntaxSpan] = []
        func add(_ range: NSRange, _ category: String) {
            if range.location != NSNotFound, range.length > 0 {
                spans.append(SyntaxSpan(range: range, category: category))
            }
        }

        if frontMatter.contains(line: lineNumber) {
            add(whole, "front-matter")
        }

        let isYAML = isYAMLFile || frontMatter.isContent(line: lineNumber)

        // In YAML a leading "#" starts a comment, not a heading.
        if !isYAML, heading.firstMatch(in: text, range: whole) != nil {
            add(whole, "heading")
            return spans
        }

        if fence.firstMatch(in: text, range: whole) != nil {
            add(whole, "code")
            return spans
        }

        if isYAML {
            for match in yamlKey.matches(in: text, range: whole) {
                add(match.range(at: 1), "yaml-key")
            }
            for match in yamlValue.matches(in: text, range: whole) {
                add(match.range, "yaml-value")
            }
            for match in comment.matches(in: text, range: whole) {
                add(match.range(at: 1), "yaml-comment")
            }
        }

        if quote.firstMatch(in: text, range: whole) != nil {
            add(whole, "quote")
        } else if list.firstMatch(in: text, range: whole) != nil, let marker = listMarker.firstMatch(in: text, range: whole) {
            add(marker.range, "list")
        }

        for match in link.matches(in: text, range: whole) {
            add(match.range, "link")
        }
        for match in inlineCode.matches(in: text, range: whole) {
            add(match.range, "code")
        }
        for match in emphasis.matches(in: text, range: whole) {
            add(match.range, "emphasis")
        }

        return spans
    }

    private static func regex(_ pattern: String) -> NSRegularExpression {
        // The patterns are fixed and known to compile.
        try! NSRegularExpression(pattern: pattern)
    }

    private static let heading = regex(#"^\s{0,3}#{1,6}(?:\s+.*)?$"#)
    private static let quote = regex(#"^\s*>\s?.*$"#)
    private static let list = regex(#"^\s*(?:[-*+]\s+|\d+[.)]\s+).*$"#)
    private static let listMarker = regex(#"^\s*(?:[-*+]|\d+[.)])"#)
    private static let fence = regex(#"^\s*(`{3,}|~{3,}).*$"#)
    private static let link = regex(#"!?(?:\[[^\]]+\]\([^\)]*\)|\[[^\]]+\]\[[^\]]*\])"#)
    private static let inlineCode = regex(#"`[^`\r\n]+`"#)
    private static let emphasis = regex(#"(?:\*\*|__|\*|_)[^\r\n]+?(?:\*\*|__|\*|_)"#)
    private static let yamlKey = regex(#"^\s*[-?]?\s*([A-Za-z_][A-Za-z0-9_.-]*)(?=\s*:)"#)
    private static let yamlValue = regex(#"(?<=:\s?)(?!$).+$"#)
    private static let comment = regex(#"(?:^|\s)(#.*)$"#)
}

private extension NSRegularExpression {
    func firstMatch(in text: String, range: NSRange) -> NSTextCheckingResult? {
        firstMatch(in: text, options: [], range: range)
    }

    func matches(in text: String, range: NSRange) -> [NSTextCheckingResult] {
        matches(in: text, options: [], range: range)
    }
}
