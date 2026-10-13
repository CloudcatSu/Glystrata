import Foundation
import Markdown

/// Parses a document for the reader.
///
/// The Windows build configures Markdig with pipe tables, task lists, extra emphasis, bare-URL
/// links and YAML front matter, with raw HTML and Setext headings turned off. The parser used here
/// (cmark-gfm, through swift-markdown) has no switches for most of that, so the same result is
/// reached by adjusting the source before parsing and the tree after it.
public struct MarkdownPreviewService: Sendable {
    public init() {}

    public func parse(_ markdown: String, sourceDirectory: String) -> MarkdownPreviewDocument {
        let directory = PathUtilities.fullPath(sourceDirectory)
        let body = Self.stripFrontMatter(FileCodec.normalizeLineEndings(markdown))
        let source = Self.neutralizeHTML(body)
        let document = Document(parsing: source, options: Self.parseOptions)
        let converter = ASTConverter(lines: source.split(separator: "\n", omittingEmptySubsequences: false))
        let blocks = converter.blocks(of: document)

        var sources: [String] = []
        Self.collectImageSources(blocks, into: &sources)
        return MarkdownPreviewDocument(
            blocks: blocks,
            sourceDirectory: directory,
            resources: sources.map { Self.resolveResource($0, baseDirectory: directory) })
    }

    // Smart punctuation would turn "--" into a dash and straight quotes into curly ones; Markdig leaves them alone.
    private static let parseOptions: ParseOptions = [.disableSmartOpts]

    // MARK: - Source adjustments

    /// A leading block fenced by `---` lines is metadata, not content.
    static func stripFrontMatter(_ text: String) -> String {
        let lines = text.split(separator: "\n", omittingEmptySubsequences: false)
        guard let first = lines.first, first.trimmingCharacters(in: .whitespaces) == "---" else {
            return text
        }

        guard let closing = lines.dropFirst().firstIndex(where: {
            let trimmed = $0.trimmingCharacters(in: .whitespaces)
            return trimmed == "---" || trimmed == "..."
        }) else {
            return text
        }

        return lines[(closing + 1)...].joined(separator: "\n")
    }

    /// Makes HTML show up as the text the user typed. Each tag the parser recognises gets its "<"
    /// escaped and the source is parsed again, because un-HTML-ing a block exposes the tags inside
    /// it; a handful of rounds settles any real document.
    static func neutralizeHTML(_ source: String) -> String {
        var bytes = Array(source.utf8)
        for _ in 0..<8 {
            let document = Document(parsing: String(decoding: bytes, as: UTF8.self), options: parseOptions)
            var collector = HTMLCollector()
            collector.visit(document)
            guard !collector.starts.isEmpty else {
                break
            }

            var lineStarts = [0]
            for (offset, byte) in bytes.enumerated() where byte == 0x0A {
                lineStarts.append(offset + 1)
            }

            let offsets = collector.starts.compactMap { location -> Int? in
                guard lineStarts.indices.contains(location.line - 1) else {
                    return nil
                }
                var offset = lineStarts[location.line - 1] + location.column - 1
                while offset < bytes.count, bytes[offset] != 0x3C, bytes[offset] != 0x0A {
                    offset += 1
                }
                return offset < bytes.count && bytes[offset] == 0x3C ? offset : nil
            }

            guard !offsets.isEmpty else {
                break
            }

            for offset in Set(offsets).sorted(by: >) {
                bytes.insert(0x5C, at: offset)
            }
        }
        return String(decoding: bytes, as: UTF8.self)
    }

    private struct HTMLCollector: MarkupWalker {
        var starts: [SourceLocation] = []

        mutating func visitHTMLBlock(_ html: HTMLBlock) {
            if let start = html.range?.lowerBound {
                starts.append(start)
            }
        }

        mutating func visitInlineHTML(_ inlineHTML: InlineHTML) {
            if let start = inlineHTML.range?.lowerBound {
                starts.append(start)
            }
        }
    }

    // MARK: - Tree conversion

    private struct ASTConverter {
        let lines: [Substring]

        func blocks(of markup: Markup) -> [MarkdownBlock] {
            markup.children.flatMap(block)
        }

        func inlines(of markup: Markup) -> [MarkdownInline] {
            MarkdownInlineExtras.apply(markup.children.flatMap(inline))
        }

        private func block(_ node: Markup) -> [MarkdownBlock] {
            switch node {
            case let heading as Heading:
                let content = inlines(of: heading)
                guard let underline = setextUnderline(of: heading) else {
                    return [.heading(level: heading.level, inlines: content)]
                }

                // With Setext headings off, a "---" under a paragraph is a rule and anything else
                // ("===", or too few dashes) is just more of the paragraph.
                if underline.first == "-" && underline.count >= 3 {
                    return [.paragraph(content), .thematicBreak]
                }
                return [.paragraph(content + [.softBreak, .text(underline)])]
            case let paragraph as Paragraph:
                return [.paragraph(inlines(of: paragraph))]
            case let quote as BlockQuote:
                return [.blockQuote(blocks(of: quote))]
            case let code as CodeBlock:
                let language = code.language?.split(separator: " ").first.map(String.init)
                let text = code.code.hasSuffix("\n") ? String(code.code.dropLast()) : code.code
                return [.codeBlock(language: language, code: text)]
            case let list as UnorderedList:
                return [.list(MarkdownList(isOrdered: false, start: 1, items: items(of: list), isTight: isTight(list)))]
            case let list as OrderedList:
                return [.list(MarkdownList(isOrdered: true, start: Int(list.startIndex), items: items(of: list), isTight: isTight(list)))]
            case let table as Table:
                return [.table(MarkdownTable(
                    alignments: table.columnAlignments.map(alignment),
                    header: table.head.cells.map { inlines(of: $0) },
                    rows: table.body.rows.map { row in row.cells.map { inlines(of: $0) } }))]
            case is ThematicBreak:
                return [.thematicBreak]
            case let html as HTMLBlock:
                // Only reached if escaping did not settle; show the raw text rather than drop it.
                return [.paragraph([.text(html.rawHTML.trimmingCharacters(in: .newlines))])]
            default:
                return blocks(of: node)
            }
        }

        /// The parser does not say whether a list is tight, so this looks at the source: a blank
        /// line right before an item, or before a later block inside an item, makes the list loose.
        private func isTight(_ list: Markup) -> Bool {
            func followsBlankLine(_ node: Markup) -> Bool {
                guard let line = node.range?.lowerBound.line, line >= 2, line - 2 < lines.count else {
                    return false
                }
                // Inside a quote the "blank" line still carries its ">" markers.
                return lines[line - 2].allSatisfy { $0.isWhitespace || $0 == ">" }
            }

            for (index, item) in list.children.enumerated() {
                if index > 0 && followsBlankLine(item) {
                    return false
                }
                for (childIndex, child) in item.children.enumerated() where childIndex > 0 && followsBlankLine(child) {
                    return false
                }
            }
            return true
        }

        private func items(of list: Markup) -> [MarkdownListItem] {
            list.children.compactMap { child in
                guard let item = child as? ListItem else {
                    return nil
                }
                return MarkdownListItem(isChecked: item.checkbox.map { $0 == .checked }, blocks: blocks(of: item))
            }
        }

        private func alignment(_ alignment: Table.ColumnAlignment?) -> MarkdownColumnAlignment? {
            switch alignment {
            case .left: return .left
            case .center: return .center
            case .right: return .right
            case nil: return nil
            }
        }

        private func inline(_ node: Markup) -> [MarkdownInline] {
            switch node {
            case let text as Markdown.Text:
                return [.text(text.string)]
            case let strong as Strong:
                return [.strong(strong.children.flatMap(inline))]
            case let emphasis as Emphasis:
                return [.emphasis(emphasis.children.flatMap(inline))]
            case let strike as Strikethrough:
                // One tilde is Markdig's subscript, which the Windows reader shows as plain text;
                // only the doubled form is strikethrough there.
                let children = strike.children.flatMap(inline)
                let isDoubled = sourceText(at: strike.range?.lowerBound)?.hasPrefix("~~") ?? true
                return isDoubled ? [.strikethrough(children)] : children
            case let code as InlineCode:
                return [.code(code.code)]
            case let link as Markdown.Link:
                return [.link(destination: link.destination ?? "", children: link.children.flatMap(inline))]
            case let image as Markdown.Image:
                return [.image(source: image.source ?? "", alt: image.plainText)]
            case is LineBreak:
                return [.lineBreak]
            case is SoftBreak:
                return [.softBreak]
            case let html as InlineHTML:
                return [.text(html.rawHTML)]
            default:
                return node.children.flatMap(inline)
            }
        }

        /// The rest of the source line starting at `location`, or nil when the parser gave no usable position.
        private func sourceText(at location: SourceLocation?) -> Substring? {
            guard let location, lines.indices.contains(location.line - 1) else {
                return nil
            }

            let line = lines[location.line - 1]
            let start = line.utf8.index(line.utf8.startIndex, offsetBy: location.column - 1, limitedBy: line.utf8.endIndex)
            return start.map { line[$0...] }
        }

        /// The "===" or "---" line under a Setext heading, or nil for a "#" heading.
        private func setextUnderline(of heading: Heading) -> String? {
            guard let range = heading.range, let start = sourceText(at: range.lowerBound), !start.hasPrefix("#") else {
                return nil
            }

            // The reported end is the underline itself, or the line after it when a blank line follows,
            // so look backwards from there for the last line that is nothing but "=" or "-".
            for lineNumber in stride(from: range.upperBound.line, to: range.lowerBound.line, by: -1)
            where lines.indices.contains(lineNumber - 1) {
                // Inside a quote or list the underline carries the container's prefix.
                let candidate = lines[lineNumber - 1]
                    .drop { $0 == " " || $0 == ">" }
                    .trimmingCharacters(in: .whitespaces)
                if !candidate.isEmpty, candidate.allSatisfy({ $0 == "=" }) || candidate.allSatisfy({ $0 == "-" }) {
                    return candidate
                }
            }

            return nil
        }
    }

    // MARK: - Images

    private static func collectImageSources(_ blocks: [MarkdownBlock], into sources: inout [String]) {
        for block in blocks {
            switch block {
            case .heading(_, let inlines), .paragraph(let inlines):
                collectImageSources(inlines, into: &sources)
            case .blockQuote(let children):
                collectImageSources(children, into: &sources)
            case .list(let list):
                for item in list.items {
                    collectImageSources(item.blocks, into: &sources)
                }
            case .table(let table):
                for cell in table.header + table.rows.flatMap({ $0 }) {
                    collectImageSources(cell, into: &sources)
                }
            case .codeBlock, .thematicBreak:
                break
            }
        }
    }

    private static func collectImageSources(_ inlines: [MarkdownInline], into sources: inout [String]) {
        for inline in inlines {
            switch inline {
            case .image(let source, _):
                sources.append(source)
            case .strong(let children), .emphasis(let children), .strikethrough(let children), .link(_, let children):
                collectImageSources(children, into: &sources)
            case .text, .code, .lineBreak, .softBreak:
                break
            }
        }
    }

    /// Only images inside the document's own folder are shown: opening a file must not be able to
    /// pull in pictures from elsewhere on disk or from the network.
    static func resolveResource(_ source: String, baseDirectory: String) -> PreviewResource {
        if let url = URL(string: source), let scheme = url.scheme {
            guard scheme.lowercased() == "file" else {
                return PreviewResource(source: source, resolvedPath: nil, isAvailable: false, errorMessage: "預覽只載入本機相對圖片。")
            }
            return fileResource(source, path: url.path, baseDirectory: baseDirectory)
        }

        let cleanSource = source.prefix { $0 != "#" && $0 != "?" }
        if cleanSource.trimmingCharacters(in: .whitespaces).isEmpty {
            return PreviewResource(source: source, resolvedPath: nil, isAvailable: false, errorMessage: "圖片路徑是空的。")
        }

        let path = cleanSource.hasPrefix("/")
            ? String(cleanSource)
            : (baseDirectory as NSString).appendingPathComponent(String(cleanSource))
        return fileResource(source, path: PathUtilities.fullPath(path), baseDirectory: baseDirectory)
    }

    private static func fileResource(_ source: String, path: String, baseDirectory: String) -> PreviewResource {
        let base = PathUtilities.canonicalKey(baseDirectory)
        let candidate = PathUtilities.canonicalKey(path)
        guard candidate == base || candidate.hasPrefix(base.hasSuffix("/") ? base : base + "/") else {
            return PreviewResource(source: source, resolvedPath: nil, isAvailable: false, errorMessage: "圖片路徑必須位於文件目錄內。")
        }

        return PathUtilities.fileExists(path)
            ? PreviewResource(source: source, resolvedPath: path, isAvailable: true, errorMessage: nil)
            : PreviewResource(source: source, resolvedPath: path, isAvailable: false, errorMessage: "找不到圖片檔案。")
    }
}
