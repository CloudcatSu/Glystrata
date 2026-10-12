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
    let onSelectionChange: (EditorSelection) -> Void
    let onFocus: () -> Void

    func makeCoordinator() -> Coordinator {
        Coordinator(viewState: viewState, store: store, onSelectionChange: onSelectionChange, onFocus: onFocus)
    }

    func makeNSView(context: Context) -> NSScrollView {
        let layoutManager = NSLayoutManager()
        store.storage.addLayoutManager(layoutManager)
        let container = NSTextContainer(size: NSSize(width: 0, height: CGFloat.greatestFiniteMagnitude))
        container.widthTracksTextView = true
        layoutManager.addTextContainer(container)

        let textView = FocusReportingTextView(frame: .zero, textContainer: container)
        textView.onFocus = onFocus
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
        private var layoutManager: NSLayoutManager?
        private(set) weak var textView: NSTextView?
        private(set) weak var scrollView: NSScrollView?
        private(set) weak var ruler: LineNumberRulerView?
        var appliedStyle: EditorStyle?

        init(
            viewState: DocumentViewState,
            store: DocumentTextStore,
            onSelectionChange: @escaping (EditorSelection) -> Void,
            onFocus: @escaping () -> Void
        ) {
            self.viewState = viewState
            self.store = store
            self.onSelectionChange = onSelectionChange
            self.onFocus = onFocus
        }

        func attach(textView: NSTextView, scrollView: NSScrollView, layoutManager: NSLayoutManager, ruler: LineNumberRulerView) {
            self.textView = textView
            self.scrollView = scrollView
            self.layoutManager = layoutManager
            self.ruler = ruler
            NotificationCenter.default.addObserver(
                self,
                selector: #selector(scrolled),
                name: NSView.boundsDidChangeNotification,
                object: scrollView.contentView)
        }

        func detach() {
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

/// Tells its pane when it gains focus, so commands act on the pane the user is typing in.
private final class FocusReportingTextView: NSTextView {
    var onFocus: (() -> Void)?

    override func becomeFirstResponder() -> Bool {
        let accepted = super.becomeFirstResponder()
        if accepted {
            onFocus?()
        }
        return accepted
    }
}
