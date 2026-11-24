import AppKit
import GlystrataCore

/// The colours the reader draws with: the user's reader palette, plus the surface and muted tones
/// the Windows renderer keeps fixed per theme.
struct ReaderColors {
    let text: NSColor
    let muted: NSColor
    let surface: NSColor
    let heading: NSColor
    let link: NSColor
    let quote: NSColor
    let code: NSColor
    let background: NSColor
    let missingImage = NSColor(hex: "#CD5C5C") ?? .systemRed

    init(palette: ReaderColorPalette, kind: ThemeKind) {
        let isDark = kind == .dark
        let defaultText = isDark ? "#E6EAF0" : "#24292F"
        let defaultMuted = isDark ? "#A6ADB8" : "#68707C"
        func color(_ key: String, _ fallback: String) -> NSColor {
            NSColor(hex: palette.get(key, fallback: fallback)) ?? .gray
        }

        text = color("text", defaultText)
        muted = NSColor(hex: defaultMuted) ?? .gray
        surface = NSColor(hex: isDark ? "#292E36" : "#F1F3F5") ?? .gray
        heading = color("heading", defaultText)
        link = color("link", isDark ? "#79C0FF" : "#0969DA")
        quote = color("quote", defaultMuted)
        code = color("code", defaultText)
        background = NSColor(hex: isDark ? "#202329" : "#FFFFFF") ?? .textBackgroundColor
    }
}

/// Turns a parsed Markdown document into one attributed string for a text view. A single text
/// view, rather than a view per block, is what lets the reader select and copy across the whole
/// document. Sizes and spacing follow the Windows build's FlowDocument renderer.
@MainActor
struct ReaderRenderer {
    let typography: PreviewTypography
    let colors: ReaderColors
    let missingImageLabel: String

    static let bodySize: CGFloat = 15
    static let codeSize: CGFloat = 13
    /// A plain list's margin plus padding, and equally a task marker's slot plus its gap, so the
    /// text of plain and task lists starts at the same place.
    static let listIndent: CGFloat = 22
    static let markerGap: CGFloat = 7

    func render(_ document: MarkdownPreviewDocument) -> NSAttributedString {
        var builder = Builder(renderer: self, resources: document.resources)
        builder.append(document.blocks, in: Context(foreground: colors.text))
        return builder.finish()
    }

    // MARK: - Fonts

    private var bodyFont: NSFont { .systemFont(ofSize: Self.bodySize) }

    private var codeFont: NSFont { .monospacedSystemFont(ofSize: Self.codeSize, weight: .regular) }

    private var bodyLineHeight: CGFloat { Self.bodySize * typography.lineSpacing }

    private func headingSize(_ level: Int) -> CGFloat {
        switch level {
        case 1: return typography.h1Size
        case 2: return typography.h2Size
        case 3: return typography.h3Size
        case 4: return typography.h4Size
        case 5: return typography.h5Size
        default: return typography.h6Size
        }
    }

    // MARK: - Building

    /// Where a block is being drawn: the enclosing quote or table cell, how far in its content
    /// starts, and the colour text takes when nothing more specific applies.
    private struct Context {
        var blocks: [NSTextBlock] = []
        var indent: CGFloat = 0
        var foreground: NSColor
    }

    @MainActor
    private struct Builder {
        let renderer: ReaderRenderer
        let resources: [PreviewResource]
        let output = NSMutableAttributedString()
        /// The bottom margin of whatever was drawn last. Adjacent margins collapse to the larger
        /// one, as they do in a FlowDocument, so it is applied by the next block rather than here.
        var pendingBottom: CGFloat = 0

        private var typography: PreviewTypography { renderer.typography }
        private var colors: ReaderColors { renderer.colors }

        func finish() -> NSAttributedString {
            // Drop the last paragraph break, which would otherwise show as an empty line at the end.
            if output.length > 0 {
                output.deleteCharacters(in: NSRange(location: output.length - 1, length: 1))
            }
            return output
        }

        mutating func append(_ blocks: [MarkdownBlock], in context: Context) {
            for block in blocks {
                append(block, in: context)
            }
        }

        mutating func append(_ block: MarkdownBlock, in context: Context) {
            switch block {
            case .heading(let level, let inlines):
                let size = renderer.headingSize(level)
                let style = paragraphStyle(in: context, lineHeight: size * typography.lineSpacing)
                let font = NSFont.systemFont(ofSize: size, weight: .semibold)
                appendParagraph(
                    inline(inlines, InlineStyle(font: font, color: colors.heading)),
                    style: style,
                    top: typography.headingSpacing,
                    bottom: typography.paragraphSpacing)

            case .paragraph(let inlines):
                appendParagraph(
                    inline(inlines, InlineStyle(font: renderer.bodyFont, color: context.foreground)),
                    style: paragraphStyle(in: context, lineHeight: renderer.bodyLineHeight),
                    top: 0,
                    bottom: typography.paragraphSpacing)

            case .blockQuote(let children):
                let quote = Self.containerBlock()
                quote.setWidth(context.indent + 12, type: .absoluteValueType, for: .margin, edge: .minX)
                quote.setWidth(3, type: .absoluteValueType, for: .border, edge: .minX)
                quote.setBorderColor(colors.muted, for: .minX)
                quote.setWidth(12, type: .absoluteValueType, for: .padding, edge: .minX)
                quote.setWidth(2, type: .absoluteValueType, for: .padding, edge: .minY)
                quote.setWidth(2, type: .absoluteValueType, for: .padding, edge: .maxY)
                appendContainer(quote, top: 0, bottom: typography.paragraphSpacing) { builder in
                    builder.append(children, in: Context(blocks: context.blocks + [quote], indent: 0, foreground: builder.colors.quote))
                }

            case .codeBlock(_, let code):
                let box = Self.containerBlock()
                box.backgroundColor = colors.surface
                box.setWidth(context.indent, type: .absoluteValueType, for: .margin, edge: .minX)
                box.setWidth(12, type: .absoluteValueType, for: .padding)
                appendContainer(box, top: 4, bottom: typography.paragraphSpacing) { builder in
                    let style = NSMutableParagraphStyle()
                    style.textBlocks = context.blocks + [box]
                    style.minimumLineHeight = 19
                    // Real line breaks rather than line separators, so copied code pastes as code.
                    let text = NSAttributedString(string: code, attributes: [
                        .font: builder.renderer.codeFont,
                        .foregroundColor: builder.colors.code,
                        .paragraphStyle: style
                    ])
                    builder.output.append(text)
                    builder.output.append(NSAttributedString(string: "\n", attributes: [.paragraphStyle: style, .font: builder.renderer.codeFont]))
                }

            case .list(let list):
                append(list, in: context)

            case .table(let table):
                append(table, in: context)

            case .thematicBreak:
                let attachment = NSTextAttachment()
                attachment.attachmentCell = RuleCell(color: colors.muted)
                let style = paragraphStyle(in: context, lineHeight: 0)
                appendParagraph(NSAttributedString(attachment: attachment), style: style, top: 4, bottom: typography.paragraphSpacing)
            }
        }

        /// A box that paragraphs can share, for quotes and code. It is a table of one cell: a
        /// plain text block is laid out for its first paragraph only and never draws its border.
        private static func containerBlock() -> NSTextBlock {
            let table = NSTextTable()
            table.numberOfColumns = 1
            table.setContentWidth(100, type: .percentageValueType)
            return NSTextTableBlock(table: table, startingRow: 0, rowSpan: 1, startingColumn: 0, columnSpan: 1)
        }

        private func paragraphStyle(in context: Context, lineHeight: CGFloat) -> NSMutableParagraphStyle {
            let style = NSMutableParagraphStyle()
            style.textBlocks = context.blocks
            style.firstLineHeadIndent = context.indent
            style.headIndent = context.indent
            // A minimum, not a fixed height: a line holding an image has to be able to grow.
            style.minimumLineHeight = lineHeight
            return style
        }

        private mutating func appendParagraph(_ content: NSAttributedString, style: NSMutableParagraphStyle, top: CGFloat, bottom: CGFloat) {
            style.paragraphSpacingBefore = max(pendingBottom, top)
            let paragraph = NSMutableAttributedString(attributedString: content)
            paragraph.append(NSAttributedString(string: "\n", attributes: [.font: renderer.bodyFont]))
            paragraph.addAttribute(.paragraphStyle, value: style, range: NSRange(location: 0, length: paragraph.length))
            output.append(paragraph)
            pendingBottom = bottom
        }

        /// Draws `body` inside a box (a quote or code), whose own margins take the place of
        /// paragraph spacing above and below.
        private mutating func appendContainer(_ block: NSTextBlock, top: CGFloat, bottom: CGFloat, body: (inout Builder) -> Void) {
            block.setWidth(max(pendingBottom, top), type: .absoluteValueType, for: .margin, edge: .minY)
            block.setWidth(bottom, type: .absoluteValueType, for: .margin, edge: .maxY)
            pendingBottom = 0
            body(&self)
            // The block's bottom margin is already in the layout, so nothing is left to carry over.
            pendingBottom = 0
        }

        // MARK: Lists

        private mutating func append(_ list: MarkdownList, in context: Context) {
            let isTaskList = list.items.contains { $0.isChecked != nil }
            // An unordered task list hangs its checkboxes where bullets would be; an ordered one
            // keeps its numbers and puts the checkbox after them, one slot further in.
            let markerColumn = context.indent + (isTaskList && !list.isOrdered ? 0 : ReaderRenderer.listIndent)
            let textColumn = context.indent + ReaderRenderer.listIndent + (isTaskList && list.isOrdered ? ReaderRenderer.listIndent : 0)
            let itemBottom = list.isTight ? 4 : typography.paragraphSpacing

            for (index, item) in list.items.enumerated() {
                let marker = NSMutableAttributedString()
                let markerAttributes: [NSAttributedString.Key: Any] = [.font: renderer.bodyFont, .foregroundColor: context.foreground]
                var tabs: [NSTextTab] = []
                func slot(_ kind: MarkerCell.Kind) -> NSAttributedString {
                    let attachment = NSTextAttachment()
                    attachment.attachmentCell = MarkerCell(kind: kind, colors: colors, capHeight: renderer.bodyFont.capHeight)
                    return NSAttributedString(attachment: attachment)
                }

                if list.isOrdered || !isTaskList {
                    // Right-aligned, so "9." and "10." end at the same place.
                    tabs.append(NSTextTab(textAlignment: .right, location: markerColumn - ReaderRenderer.markerGap))
                    tabs.append(NSTextTab(textAlignment: .left, location: markerColumn))
                    marker.append(NSAttributedString(string: "\t", attributes: markerAttributes))
                    // The bullet is drawn rather than typed, so it matches the one in task lists.
                    marker.append(list.isOrdered
                        ? NSAttributedString(string: "\(list.start + index).", attributes: markerAttributes)
                        : slot(.bullet))
                    marker.append(NSAttributedString(string: "\t", attributes: markerAttributes))
                }
                if isTaskList {
                    tabs.append(NSTextTab(textAlignment: .left, location: textColumn))
                    marker.append(slot(item.isChecked.map { .checkbox(isChecked: $0) } ?? (list.isOrdered ? .empty : .bullet)))
                    marker.append(NSAttributedString(string: "\t", attributes: markerAttributes))
                }

                let style = paragraphStyle(in: context, lineHeight: renderer.bodyLineHeight)
                style.firstLineHeadIndent = context.indent
                style.headIndent = textColumn
                style.tabStops = tabs

                var blocks = item.blocks
                if case .paragraph(let inlines)? = blocks.first {
                    blocks.removeFirst()
                    marker.append(inline(inlines, InlineStyle(font: renderer.bodyFont, color: context.foreground)))
                }
                appendParagraph(marker, style: style, top: 0, bottom: itemBottom)

                // Whatever else the item holds (nested lists, further paragraphs, code) lines up
                // under the item's text.
                var inner = context
                inner.indent = textColumn
                for block in blocks {
                    append(block, in: inner)
                    if case .list = block {
                        continue
                    }
                    pendingBottom = max(pendingBottom, itemBottom)
                }
            }
            pendingBottom = max(pendingBottom, typography.paragraphSpacing)
        }

        // MARK: Tables

        private mutating func append(_ table: MarkdownTable, in context: Context) {
            let columnCount = max(([table.header.count] + table.rows.map(\.count)).max() ?? 1, 1)
            let textTable = NSTextTable()
            textTable.numberOfColumns = columnCount
            textTable.layoutAlgorithm = .fixedLayoutAlgorithm
            textTable.hidesEmptyCells = false
            textTable.setContentWidth(100, type: .percentageValueType)

            let gapAbove = max(pendingBottom, 2)
            let rows = [table.header] + table.rows
            for (rowIndex, row) in rows.enumerated() {
                for column in 0..<columnCount {
                    let cell = NSTextTableBlock(table: textTable, startingRow: rowIndex, rowSpan: 1, startingColumn: column, columnSpan: 1)
                    cell.setContentWidth(100 / CGFloat(columnCount), type: .percentageValueType)
                    // Each cell draws its right and bottom edge, and the first row and column the
                    // outer top and left, so shared edges are one line thick. Letting the table
                    // collapse borders instead would also discard the margin above it.
                    cell.setBorderColor(colors.surface)
                    cell.setWidth(1, type: .absoluteValueType, for: .border, edge: .maxX)
                    cell.setWidth(1, type: .absoluteValueType, for: .border, edge: .maxY)
                    cell.setWidth(column == 0 ? 1 : 0, type: .absoluteValueType, for: .border, edge: .minX)
                    cell.setWidth(rowIndex == 0 ? 1 : 0, type: .absoluteValueType, for: .border, edge: .minY)
                    cell.setWidth(7, type: .absoluteValueType, for: .padding, edge: .minX)
                    cell.setWidth(7, type: .absoluteValueType, for: .padding, edge: .maxX)
                    cell.setWidth(4, type: .absoluteValueType, for: .padding, edge: .minY)
                    cell.setWidth(4, type: .absoluteValueType, for: .padding, edge: .maxY)
                    if rowIndex == 0 {
                        cell.backgroundColor = colors.surface
                        cell.setWidth(gapAbove, type: .absoluteValueType, for: .margin, edge: .minY)
                    }
                    if column == 0 {
                        cell.setWidth(context.indent, type: .absoluteValueType, for: .margin, edge: .minX)
                    }

                    let style = NSMutableParagraphStyle()
                    style.textBlocks = context.blocks + [cell]
                    style.minimumLineHeight = renderer.bodyLineHeight
                    if column < table.alignments.count, let alignment = table.alignments[column] {
                        switch alignment {
                        case .left: style.alignment = .left
                        case .center: style.alignment = .center
                        case .right: style.alignment = .right
                        }
                    }

                    let content = NSMutableAttributedString(attributedString: column < row.count
                        ? inline(row[column], InlineStyle(font: renderer.bodyFont, color: context.foreground))
                        : NSAttributedString())
                    content.append(NSAttributedString(string: "\n", attributes: [.font: renderer.bodyFont]))
                    content.addAttribute(.paragraphStyle, value: style, range: NSRange(location: 0, length: content.length))
                    output.append(content)
                }
            }
            pendingBottom = typography.paragraphSpacing
        }

        // MARK: Inlines

        private struct InlineStyle {
            var font: NSFont
            var color: NSColor
            var isItalic = false
            var isStruck = false
            var link: String?
        }

        private func inline(_ nodes: [MarkdownInline], _ style: InlineStyle) -> NSAttributedString {
            let result = NSMutableAttributedString()
            for node in nodes {
                switch node {
                case .text(let text):
                    result.append(run(text, style))
                case .strong(let children):
                    var bold = style
                    bold.font = NSFontManager.shared.convert(style.font, toHaveTrait: .boldFontMask)
                    result.append(inline(children, bold))
                case .emphasis(let children):
                    var italic = style
                    italic.font = NSFontManager.shared.convert(style.font, toHaveTrait: .italicFontMask)
                    italic.isItalic = true
                    result.append(inline(children, italic))
                case .strikethrough(let children):
                    var struck = style
                    struck.isStruck = true
                    result.append(inline(children, struck))
                case .code(let code):
                    var attributes = attributes(for: style)
                    attributes[.font] = renderer.codeFont
                    attributes[.foregroundColor] = colors.code
                    attributes[.backgroundColor] = colors.surface
                    result.append(NSAttributedString(string: code, attributes: attributes))
                case .link(let destination, let children):
                    var linked = style
                    linked.color = colors.link
                    linked.link = destination
                    result.append(inline(children, linked))
                case .image(let source, let alt):
                    result.append(image(source: source, alt: alt, style))
                case .lineBreak, .softBreak:
                    // A line separator: a new line inside the same paragraph. The Windows reader
                    // shows a single source line break as a line break too.
                    result.append(NSAttributedString(string: "\u{2028}", attributes: attributes(for: style)))
                }
            }
            return result
        }

        private func attributes(for style: InlineStyle) -> [NSAttributedString.Key: Any] {
            var attributes: [NSAttributedString.Key: Any] = [.font: style.font, .foregroundColor: style.color]
            if style.isStruck {
                attributes[.strikethroughStyle] = NSUnderlineStyle.single.rawValue
            }
            if let link = style.link, !link.trimmingCharacters(in: .whitespaces).isEmpty {
                attributes[.underlineStyle] = NSUnderlineStyle.single.rawValue
                attributes[.toolTip] = link
                // Kept as a string: the text view's delegate decides which schemes may be opened.
                attributes[.link] = link
            }
            return attributes
        }

        private func run(_ text: String, _ style: InlineStyle) -> NSAttributedString {
            let result = NSMutableAttributedString(string: text, attributes: attributes(for: style))
            guard style.isItalic else {
                return result
            }

            // Chinese, Japanese and Korean fonts have no italic cut, so emphasis there would not
            // show at all; slanting those characters is what the Windows text engine does too.
            let string = text as NSString
            var location = 0
            while location < string.length {
                let range = string.rangeOfComposedCharacterSequence(at: location)
                if let scalar = string.substring(with: range).unicodeScalars.first, Self.lacksItalic(scalar) {
                    result.addAttribute(.obliqueness, value: 0.2, range: range)
                }
                location = NSMaxRange(range)
            }
            return result
        }

        private static func lacksItalic(_ scalar: Unicode.Scalar) -> Bool {
            switch scalar.value {
            case 0x2E80...0x9FFF, 0xAC00...0xD7AF, 0xF900...0xFAFF, 0xFF00...0xFFEF, 0x20000...0x3FFFF:
                return true
            default:
                return false
            }
        }

        private func image(source: String, alt: String, _ style: InlineStyle) -> NSAttributedString {
            guard let resource = resources.first(where: { $0.source == source }),
                  resource.isAvailable,
                  let path = resource.resolvedPath,
                  let image = NSImage(contentsOfFile: path),
                  image.size.width > 0 else {
                var attributes = attributes(for: style)
                attributes[.foregroundColor] = colors.missingImage
                return NSAttributedString(string: "[\(renderer.missingImageLabel): \(source)]", attributes: attributes)
            }

            let attachment = NSTextAttachment()
            attachment.attachmentCell = ImageCell(image: image)
            let result = NSMutableAttributedString(attachment: attachment)
            if !alt.isEmpty {
                result.addAttribute(.toolTip, value: alt, range: NSRange(location: 0, length: result.length))
            }
            return result
        }
    }
}

// MARK: - Attachments

/// A horizontal rule as wide as the line it sits on.
private final class RuleCell: NSTextAttachmentCell {
    private let color: NSColor

    init(color: NSColor) {
        self.color = color
        super.init()
    }

    @available(*, unavailable)
    required init(coder: NSCoder) {
        fatalError("not used")
    }

    override func cellFrame(for textContainer: NSTextContainer, proposedLineFragment lineFrag: NSRect, glyphPosition position: NSPoint, characterIndex charIndex: Int) -> NSRect {
        NSRect(x: 0, y: 0, width: max(lineFrag.width - position.x, 1), height: 1)
    }

    override func draw(withFrame cellFrame: NSRect, in controlView: NSView?) {
        color.setFill()
        cellFrame.fill()
    }
}

/// An image kept within 720 points and within the line, whichever is narrower.
private final class ImageCell: NSTextAttachmentCell {
    private nonisolated static let maxWidth: CGFloat = 720
    private let picture: NSImage
    private let naturalSize: NSSize

    init(image: NSImage) {
        picture = image
        // AppKit sizes an image at 72 units to the inch, WPF at 96; scaling keeps a picture the
        // same size relative to the text as in the Windows reader.
        naturalSize = NSSize(width: image.size.width * 96 / 72, height: image.size.height * 96 / 72)
        super.init()
    }

    @available(*, unavailable)
    required init(coder: NSCoder) {
        fatalError("not used")
    }

    override func cellFrame(for textContainer: NSTextContainer, proposedLineFragment lineFrag: NSRect, glyphPosition position: NSPoint, characterIndex charIndex: Int) -> NSRect {
        let available = max(lineFrag.width - position.x, 1)
        let width = min(naturalSize.width, Self.maxWidth, available)
        return NSRect(x: 0, y: 0, width: width, height: width * naturalSize.height / naturalSize.width)
    }

    override func draw(withFrame cellFrame: NSRect, in controlView: NSView?) {
        picture.draw(in: cellFrame, from: .zero, operation: .sourceOver, fraction: 1, respectFlipped: true, hints: [.interpolation: NSImageInterpolation.high.rawValue])
    }
}

/// The fixed slot in front of a task-list item's text: a checkbox, a bullet, or nothing.
private final class MarkerCell: NSTextAttachmentCell {
    enum Kind {
        case checkbox(isChecked: Bool)
        case bullet
        case empty
    }

    private nonisolated static let size: CGFloat = 14
    private let kind: Kind
    private let colors: ReaderColors
    private let capHeight: CGFloat

    init(kind: Kind, colors: ReaderColors, capHeight: CGFloat) {
        self.kind = kind
        self.colors = colors
        self.capHeight = capHeight
        super.init()
    }

    @available(*, unavailable)
    required init(coder: NSCoder) {
        fatalError("not used")
    }

    override func cellSize() -> NSSize {
        NSSize(width: Self.size, height: Self.size)
    }

    // Centres the slot on the capital letters beside it rather than sitting it on the baseline.
    override func cellBaselineOffset() -> NSPoint {
        NSPoint(x: 0, y: (capHeight - Self.size) / 2)
    }

    override func draw(withFrame cellFrame: NSRect, in controlView: NSView?) {
        switch kind {
        case .empty:
            break
        case .bullet:
            colors.text.setFill()
            NSBezierPath(ovalIn: NSRect(x: cellFrame.midX - 2.75, y: cellFrame.midY - 2.75, width: 5.5, height: 5.5)).fill()
        case .checkbox(let isChecked):
            let box = NSBezierPath(roundedRect: cellFrame.insetBy(dx: 0.7, dy: 0.7), xRadius: 3, yRadius: 3)
            box.lineWidth = 1.4
            if isChecked {
                colors.link.setFill()
                box.fill()
                colors.link.setStroke()
                box.stroke()
                drawTick(in: cellFrame, flipped: controlView?.isFlipped ?? true)
            } else {
                colors.muted.setStroke()
                box.stroke()
            }
        }
    }

    private func drawTick(in frame: NSRect, flipped: Bool) {
        // The Windows build's path, drawn in a 14-point box with the y axis pointing down.
        let points = [NSPoint(x: 2.6, y: 6.6), NSPoint(x: 5.4, y: 9.4), NSPoint(x: 10.4, y: 3.6)]
        let tick = NSBezierPath()
        for (index, point) in points.enumerated() {
            let y = flipped ? frame.minY + point.y : frame.maxY - point.y
            let target = NSPoint(x: frame.minX + point.x, y: y)
            if index == 0 {
                tick.move(to: target)
            } else {
                tick.line(to: target)
            }
        }
        tick.lineWidth = 1.7
        tick.lineCapStyle = .round
        tick.lineJoinStyle = .round
        NSColor.white.setStroke()
        tick.stroke()
    }
}
