import AppKit
import GlystrataCore
import SwiftUI

/// What the status bar needs to know about the caret.
struct EditorSelection: Equatable {
    var line = 1
    var column = 1
    var selectedText: String?
}

/// One tab's editor: an NSTextView with its own layout over the document's shared text storage, so
/// the same document can be open in several panes with independent carets and scroll positions.
struct EditorView: NSViewRepresentable {
    let viewState: DocumentViewState
    let store: DocumentTextStore
    let style: EditorStyle
    let zoom: Double
    let onSelectionChange: (EditorSelection) -> Void
    let onFocus: () -> Void
    /// Asks for a new zoom factor; the model rounds and limits it.
    let onZoom: (Double) -> Void
    /// Hands the text view over (and takes it back, with nil) so toolbar buttons and commands can reach it.
    let onEditorChange: (EditorTextView?) -> Void

    func makeCoordinator() -> Coordinator {
        Coordinator(viewState: viewState, store: store, onSelectionChange: onSelectionChange, onFocus: onFocus, onEditorChange: onEditorChange)
    }

    func makeNSView(context: Context) -> NSScrollView {
        let layoutManager = NSLayoutManager()
        store.storage.addLayoutManager(layoutManager)
        let container = NSTextContainer(size: NSSize(width: 0, height: CGFloat.greatestFiniteMagnitude))
        container.widthTracksTextView = true
        layoutManager.addTextContainer(container)

        let textView = EditorTextView(frame: .zero, textContainer: container)
        textView.onFocus = onFocus
        textView.onZoom = onZoom
        textView.zoom = zoom
        textView.delegate = context.coordinator
        textView.isRichText = false
        textView.allowsUndo = true
        textView.usesFindBar = false
        textView.isAutomaticQuoteSubstitutionEnabled = false
        textView.isAutomaticDashSubstitutionEnabled = false
        textView.isAutomaticTextReplacementEnabled = false
        textView.isAutomaticSpellingCorrectionEnabled = false
        textView.isAutomaticLinkDetectionEnabled = false
        textView.isContinuousSpellCheckingEnabled = false
        textView.isGrammarCheckingEnabled = false
        textView.textContainerInset = NSSize(width: 14, height: 10)
        textView.minSize = .zero
        textView.maxSize = NSSize(width: CGFloat.greatestFiniteMagnitude, height: CGFloat.greatestFiniteMagnitude)
        textView.isVerticallyResizable = true
        textView.isHorizontallyResizable = false
        textView.autoresizingMask = [.width]

        let scrollView = NSScrollView()
        scrollView.hasVerticalScroller = true
        scrollView.hasHorizontalScroller = false
        scrollView.autohidesScrollers = true
        scrollView.documentView = textView
        scrollView.contentView.postsBoundsChangedNotifications = true

        let ruler = LineNumberRulerView(textView: textView, scrollView: scrollView)
        scrollView.verticalRulerView = ruler
        scrollView.hasVerticalRuler = true
        scrollView.rulersVisible = true

        context.coordinator.attach(textView: textView, scrollView: scrollView, layoutManager: layoutManager, ruler: ruler)
        applyStyle(to: context.coordinator)
        context.coordinator.restorePosition()
        return scrollView
    }

    func updateNSView(_ scrollView: NSScrollView, context: Context) {
        context.coordinator.textView?.zoom = zoom
        applyStyle(to: context.coordinator)
    }

    static func dismantleNSView(_ scrollView: NSScrollView, coordinator: Coordinator) {
        coordinator.detach()
    }

    private func applyStyle(to coordinator: Coordinator) {
        guard let textView = coordinator.textView, coordinator.appliedStyle != style else {
            return
        }

        coordinator.appliedStyle = style
        textView.backgroundColor = style.background.nsColor
        textView.insertionPointColor = style.foreground.nsColor
        textView.typingAttributes = store.typingAttributes
        textView.font = style.font
        coordinator.scrollView?.backgroundColor = style.background.nsColor
        coordinator.ruler?.backgroundColor = style.background.nsColor
        coordinator.ruler?.textColor = style.gutterForeground.nsColor
        coordinator.ruler?.font = .monospacedDigitSystemFont(ofSize: max(9, style.fontSize - 2), weight: .regular)
    }

    @MainActor
    final class Coordinator: NSObject, NSTextViewDelegate {
        private let viewState: DocumentViewState
        private let store: DocumentTextStore
        private let onSelectionChange: (EditorSelection) -> Void
        private let onFocus: () -> Void
        private let onEditorChange: (EditorTextView?) -> Void
        private var layoutManager: NSLayoutManager?
        private(set) weak var textView: EditorTextView?
        private(set) weak var scrollView: NSScrollView?
        private(set) weak var ruler: LineNumberRulerView?
        var appliedStyle: EditorStyle?

        init(
            viewState: DocumentViewState,
            store: DocumentTextStore,
            onSelectionChange: @escaping (EditorSelection) -> Void,
            onFocus: @escaping () -> Void,
            onEditorChange: @escaping (EditorTextView?) -> Void
        ) {
            self.viewState = viewState
            self.store = store
            self.onSelectionChange = onSelectionChange
            self.onFocus = onFocus
            self.onEditorChange = onEditorChange
        }

        func attach(textView: EditorTextView, scrollView: NSScrollView, layoutManager: NSLayoutManager, ruler: LineNumberRulerView) {
            self.textView = textView
            self.scrollView = scrollView
            self.layoutManager = layoutManager
            self.ruler = ruler
            NotificationCenter.default.addObserver(
                self,
                selector: #selector(scrolled),
                name: NSView.boundsDidChangeNotification,
                object: scrollView.contentView)
            onEditorChange(textView)
        }

        func detach() {
            onEditorChange(nil)
            NotificationCenter.default.removeObserver(self)
            if let layoutManager {
                store.storage.removeLayoutManager(layoutManager)
            }
            layoutManager = nil
        }

        /// Puts the caret and scroll position back where this tab last had them.
        func restorePosition() {
            guard let textView, let scrollView else {
                return
            }

            let offset = min(max(viewState.caretOffset, 0), textView.string.utf16.count)
            textView.setSelectedRange(NSRange(location: offset, length: 0))
            let vertical = viewState.verticalOffset
            DispatchQueue.main.async {
                textView.layoutManager?.ensureLayout(for: textView.textContainer!)
                // Keep the horizontal origin the scroll view chose: it is offset by the width of the
                // line-number gutter, and scrolling to 0 would slide the text underneath it.
                let clipView = scrollView.contentView
                clipView.scroll(to: NSPoint(x: clipView.bounds.origin.x, y: vertical))
                scrollView.reflectScrolledClipView(scrollView.contentView)
                textView.window?.makeFirstResponder(textView)
            }
        }

        @objc private func scrolled() {
            guard let scrollView else {
                return
            }
            viewState.verticalOffset = scrollView.contentView.bounds.origin.y
            viewState.horizontalOffset = scrollView.contentView.bounds.origin.x
        }

        func undoManager(for view: NSTextView) -> UndoManager? {
            store.undoManager
        }

        func textViewDidChangeSelection(_ notification: Notification) {
            guard let textView else {
                return
            }

            let range = textView.selectedRange()
            viewState.caretOffset = range.location
            let text = textView.string as NSString
            let lineRange = text.lineRange(for: NSRange(location: min(range.location, text.length), length: 0))
            var line = 1
            var scan = 0
            while scan < lineRange.location {
                var end = 0
                text.getLineStart(nil, end: &end, contentsEnd: nil, for: NSRange(location: scan, length: 0))
                scan = end
                line += 1
            }

            onSelectionChange(EditorSelection(
                line: line,
                column: range.location - lineRange.location + 1,
                selectedText: range.length > 0 ? text.substring(with: range) : nil))
        }
    }
}

/// The editor's text view. It tells its pane when it gains focus, so commands act on the pane the
/// user is typing in, turns ⌘-scroll and the trackpad pinch into zoom requests, and applies the
/// Markdown typing conveniences and formatting commands.
final class EditorTextView: NSTextView {
    static let zoomStep = 0.1

    var onFocus: (() -> Void)?
    var onZoom: ((Double) -> Void)?
    var zoom = 1.0

    private var scrollTravel: CGFloat = 0
    private var pinchStartZoom = 1.0
    private var pinchTotal: CGFloat = 0
    /// The symbol swap Backspace would take back; forgotten as soon as the caret goes anywhere else.
    private var lastSymbolSwap: SymbolReplacement?
    private var isApplyingAssistance = false

    // MARK: - Typing conveniences

    override func insertText(_ string: Any, replacementRange: NSRange) {
        super.insertText(string, replacementRange: replacementRange)
        guard !isApplyingAssistance, !hasMarkedText(), selectedRange().length == 0 else {
            return
        }

        if let swap = MarkdownTypingAssistant.symbolReplacement(in: self.string, caret: selectedRange().location) {
            apply(swap.edit)
            lastSymbolSwap = swap
        }
    }

    override func insertNewline(_ sender: Any?) {
        super.insertNewline(sender)
        guard selectedRange().length == 0,
              let edit = MarkdownTypingAssistant.continuation(afterLineBreakIn: string, caret: selectedRange().location) else {
            return
        }
        apply(edit)
    }

    override func deleteBackward(_ sender: Any?) {
        guard let swap = lastSymbolSwap else {
            super.deleteBackward(sender)
            return
        }

        lastSymbolSwap = nil
        let swapped = swap.reverting.range
        // An edit made in another pane may have moved things since; then this is a plain Backspace.
        guard NSMaxRange(swapped) <= (string as NSString).length,
              (string as NSString).substring(with: swapped) == swap.edit.replacement else {
            super.deleteBackward(sender)
            return
        }
        apply(swap.reverting)
    }

    override func setSelectedRanges(_ ranges: [NSValue], affinity: NSSelectionAffinity, stillSelecting: Bool) {
        super.setSelectedRanges(ranges, affinity: affinity, stillSelecting: stillSelecting)
        if let swap = lastSymbolSwap, !isApplyingAssistance, selectedRange() != NSRange(location: swap.edit.caret, length: 0) {
            lastSymbolSwap = nil
        }
    }

    /// Goes through the text view's own editing path, so the change can be undone and reaches
    /// every pane showing the document.
    private func apply(_ edit: TextEdit, selectionLength: Int = 0) {
        isApplyingAssistance = true
        defer { isApplyingAssistance = false }
        guard shouldChangeText(in: edit.range, replacementString: edit.replacement) else {
            return
        }

        // Attributed, because text put into an empty document has no neighbour to take its font from.
        textStorage?.replaceCharacters(in: edit.range, with: NSAttributedString(string: edit.replacement, attributes: typingAttributes))
        didChangeText()
        setSelectedRange(NSRange(location: edit.caret, length: selectionLength))
        scrollRangeToVisible(selectedRange())
    }

    // MARK: - Formatting

    /// Runs a formatting-toolbar command on the selection. False when it left the text as it was.
    @discardableResult
    func applyFormatting(_ command: MarkdownFormatCommand, linkURL: String? = nil) -> Bool {
        let selection = selectedRange()
        let result = MarkdownFormattingService().apply(
            string,
            selectionStart: selection.location,
            selectionLength: selection.length,
            command: command,
            linkURL: linkURL)
        window?.makeFirstResponder(self)

        // Only the part that differs is replaced, so undo restores just that and carets in other
        // panes are not thrown to the end of the document.
        let before = string as NSString
        let after = result.text as NSString
        var prefix = 0
        while prefix < before.length, prefix < after.length, before.character(at: prefix) == after.character(at: prefix) {
            prefix += 1
        }
        var suffix = 0
        while suffix < before.length - prefix, suffix < after.length - prefix,
              before.character(at: before.length - 1 - suffix) == after.character(at: after.length - 1 - suffix) {
            suffix += 1
        }
        guard prefix < before.length || prefix < after.length else {
            return false
        }

        apply(
            TextEdit(
                range: NSRange(location: prefix, length: before.length - prefix - suffix),
                replacement: after.substring(with: NSRange(location: prefix, length: after.length - prefix - suffix)),
                caret: result.selectionStart),
            selectionLength: result.selectionLength)
        return true
    }

    // MARK: - Focus and zoom

    override func becomeFirstResponder() -> Bool {
        let accepted = super.becomeFirstResponder()
        if accepted {
            onFocus?()
        }
        return accepted
    }

    override func scrollWheel(with event: NSEvent) {
        guard event.modifierFlags.intersection(.deviceIndependentFlagsMask) == .command else {
            scrollTravel = 0
            super.scrollWheel(with: event)
            return
        }

        // The glide after the fingers lift would keep zooming with nobody steering it.
        guard event.momentumPhase.isEmpty else {
            return
        }

        // Follow the hand rather than the "natural scrolling" setting: pushing the wheel or the
        // fingers away zooms in, the same direction as Ctrl-wheel in the Windows build.
        let delta = event.isDirectionInvertedFromDevice ? -event.scrollingDeltaY : event.scrollingDeltaY
        guard delta != 0 else {
            return
        }

        if event.hasPreciseScrollingDeltas {
            // A trackpad reports many small movements; one step per 40 points keeps it controllable.
            if event.phase == .began {
                scrollTravel = 0
            }
            scrollTravel += delta
            let steps = (scrollTravel / 40).rounded(.towardZero)
            if steps != 0 {
                scrollTravel -= steps * 40
                onZoom?(zoom + Double(steps) * Self.zoomStep)
            }
        } else {
            onZoom?(zoom + (delta > 0 ? Self.zoomStep : -Self.zoomStep))
        }
    }

    override func magnify(with event: NSEvent) {
        if event.phase == .began {
            pinchStartZoom = zoom
            pinchTotal = 0
        }
        pinchTotal += event.magnification
        onZoom?(pinchStartZoom * (1 + Double(pinchTotal)))
    }
}
