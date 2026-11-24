import AppKit
import GlystrataCore

/// How editor text is drawn: font size follows the zoom, colours follow the theme and the user's palette.
struct EditorStyle: Equatable {
    static let baseFontSize = 14.0

    var fontSize: CGFloat
    var foreground: ThemeColor
    var background: ThemeColor
    var gutterForeground: ThemeColor
    var palette: EditorColorPalette

    var font: NSFont { .monospacedSystemFont(ofSize: fontSize, weight: .regular) }

    func color(for category: String) -> NSColor {
        let fallback = SyntaxHighlighter.fallbackColors[category] ?? foreground.hex
        return NSColor(hex: palette.get(category, fallback: fallback)) ?? .gray
    }
}

/// The text storage and undo history of one open document, shared by every tab showing it, so an
/// edit in one pane appears in the others and undo works across them.
@MainActor
final class DocumentTextStore: NSObject, NSTextStorageDelegate {
    let storage = NSTextStorage()
    let undoManager = UndoManager()

    private let document: DocumentSession
    private let onEdit: (DocumentSession) -> Void
    private var style: EditorStyle
    private var loadedGeneration: Int
    private var isReloading = false

    init(document: DocumentSession, style: EditorStyle, onEdit: @escaping (DocumentSession) -> Void) {
        self.document = document
        self.onEdit = onEdit
        self.style = style
        loadedGeneration = document.bufferGeneration
        super.init()
        storage.delegate = self
        reload()
    }

    var typingAttributes: [NSAttributedString.Key: Any] {
        [.font: style.font, .foregroundColor: style.foreground.nsColor]
    }

    func apply(_ newStyle: EditorStyle) {
        guard newStyle != style else {
            return
        }

        style = newStyle
        restyleEverything()
    }

    /// Picks up content the model replaced wholesale (a reload from disk, a restored snapshot).
    func syncWithDocument() {
        if document.bufferGeneration != loadedGeneration {
            loadedGeneration = document.bufferGeneration
            reload()
        }
    }

    private func reload() {
        isReloading = true
        storage.setAttributedString(NSAttributedString(string: document.text, attributes: typingAttributes))
        isReloading = false
        undoManager.removeAllActions()
        highlight(NSRange(location: 0, length: storage.length), resetFirst: false)
    }

    private func restyleEverything() {
        let whole = NSRange(location: 0, length: storage.length)
        storage.beginEditing()
        storage.setAttributes(typingAttributes, range: whole)
        highlight(whole, resetFirst: false)
        storage.endEditing()
    }

    nonisolated func textStorage(
        _ textStorage: NSTextStorage,
        didProcessEditing editedMask: NSTextStorageEditActions,
        range editedRange: NSRange,
        changeInLength delta: Int
    ) {
        guard editedMask.contains(.editedCharacters) else {
            return
        }

        MainActor.assumeIsolated {
            guard !isReloading else {
                return
            }

            highlight(editedRange, resetFirst: true)
            document.updateText(storage.string)
            onEdit(document)
        }
    }

    /// Recolours the lines touching `range`. An edit near the top also recolours the whole front
    /// matter area, because typing a "---" there changes what every line below it means.
    private func highlight(_ range: NSRange, resetFirst: Bool) {
        let text = storage.string as NSString
        guard text.length > 0 else {
            return
        }

        var leadingStarts: [Int] = []
        var leadingLines: [Substring] = []
        var frontLimit = 0
        while frontLimit < text.length, leadingStarts.count < FrontMatterContext.lineLimit {
            var end = 0
            var contentsEnd = 0
            text.getLineStart(nil, end: &end, contentsEnd: &contentsEnd, for: NSRange(location: frontLimit, length: 0))
            leadingStarts.append(frontLimit)
            leadingLines.append(Substring(text.substring(with: NSRange(location: frontLimit, length: contentsEnd - frontLimit))))
            frontLimit = end
        }

        let frontMatter = FrontMatterContext(leadingLines: leadingLines)
        let isYAML = SyntaxHighlighter.isYAMLFile(document.filePath)
        let clamped = NSRange(location: min(range.location, text.length), length: min(range.length, text.length - min(range.location, text.length)))
        var target = text.lineRange(for: clamped)
        if target.location < frontLimit {
            target = NSUnionRange(target, NSRange(location: 0, length: frontLimit))
        }

        if resetFirst {
            storage.addAttribute(.foregroundColor, value: style.foreground.nsColor, range: target)
        }

        var location = target.location
        while location < NSMaxRange(target) {
            var end = 0
            var contentsEnd = 0
            text.getLineStart(nil, end: &end, contentsEnd: &contentsEnd, for: NSRange(location: location, length: 0))
            // Past the front matter area the exact line number no longer matters.
            let lineNumber = leadingStarts.firstIndex(of: location).map { $0 + 1 } ?? FrontMatterContext.lineLimit + 1
            let line = text.substring(with: NSRange(location: location, length: contentsEnd - location))
            for span in SyntaxHighlighter.spans(forLine: line, lineNumber: lineNumber, isYAMLFile: isYAML, frontMatter: frontMatter) {
                storage.addAttribute(
                    .foregroundColor,
                    value: style.color(for: span.category),
                    range: NSRange(location: location + span.range.location, length: span.range.length))
            }
            location = end
        }
    }
}

/// Hands out the one text store per open document.
@MainActor
final class DocumentTextStoreRegistry {
    private var stores: [UUID: DocumentTextStore] = [:]
    private(set) var style: EditorStyle
    private let onEdit: (DocumentSession) -> Void

    init(style: EditorStyle, onEdit: @escaping (DocumentSession) -> Void) {
        self.style = style
        self.onEdit = onEdit
    }

    func store(for document: DocumentSession) -> DocumentTextStore {
        if let existing = stores[document.sessionID] {
            existing.syncWithDocument()
            return existing
        }

        let store = DocumentTextStore(document: document, style: style, onEdit: onEdit)
        stores[document.sessionID] = store
        return store
    }

    func apply(_ newStyle: EditorStyle) {
        style = newStyle
        for store in stores.values {
            store.apply(newStyle)
        }
    }

    /// Shows content the model replaced wholesale, without waiting for the pane to redraw.
    func sync(_ document: DocumentSession) {
        stores[document.sessionID]?.syncWithDocument()
    }

    func discard(_ sessionID: UUID) {
        stores.removeValue(forKey: sessionID)
    }
}
