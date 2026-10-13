import AppKit
import GlystrataCore
import SwiftUI

/// What a reader window shows: an open document, kept up to date as it is edited, or the
/// built-in Markdown guide.
enum ReaderSource {
    case document(DocumentViewState)
    case guide
}

struct ReaderView: View {
    let source: ReaderSource
    let windowID: String

    @Environment(AppModel.self) private var model
    @Environment(\.theme) private var theme
    @State private var content = NSAttributedString()
    @State private var zoom = ReaderView.initialZoom
    @State private var hasRendered = false

    var body: some View {
        let colors = ReaderColors(palette: palette, kind: theme.kind)
        VStack(spacing: 0) {
            ReaderTextView(
                content: content,
                background: colors.background,
                zoom: zoom,
                copyTitle: model.localization.get("edit.copy"),
                selectAllTitle: model.localization.get("edit.selectAll"),
                onZoom: { setZoom($0) })
            if case .document = source {
                zoomBar
            }
        }
        .background(Color(nsColor: colors.background))
        .task(id: RefreshKey(
            text: sourceText,
            typography: model.settings.previewTypography,
            palette: palette,
            kind: theme.kind,
            language: model.localization.language)
        ) {
            await refresh(colors: colors)
        }
        .onChange(of: title) { _, title in
            model.windows.setTitle(title, for: windowID)
        }
        // Escape closes the guide, as it does every dialog in the Windows build.
        .background {
            if case .guide = source {
                Button("") { model.windows.close(windowID) }
                    .keyboardShortcut(.cancelAction)
                    .opacity(0)
            }
        }
    }

    private var zoomBar: some View {
        VStack(spacing: 0) {
            theme.border.color.frame(height: 1)
            HStack(spacing: 0) {
                Spacer(minLength: 0)
                Slider(value: Binding(get: { zoom }, set: { setZoom($0) }), in: 50...200, step: 10)
                    .controlSize(.mini)
                    .frame(width: 110)
                    .help(model.localization.get("preview.zoom"))
                Text("\(Int(zoom.rounded()))%")
                    .foregroundStyle(theme.secondaryText.color)
                    .frame(width: 44, alignment: .trailing)
                    .padding(.leading, 6)
                    .help(model.localization.get("preview.zoom"))
                    .onTapGesture(count: 2) {
                        setZoom(100)
                    }
            }
            .padding(.horizontal, 10)
            .padding(.vertical, 4)
            .background(theme.surface.color)
        }
    }

    private static var initialZoom: Double {
        #if DEBUG
        // Development aid: `-GlystrataReaderZoom <percent>` opens readers already zoomed.
        let requested = UserDefaults.standard.double(forKey: "GlystrataReaderZoom")
        if requested > 0 {
            return requested
        }
        #endif
        return 100
    }

    private func setZoom(_ percent: Double) {
        zoom = min(max((percent / 10).rounded() * 10, 50), 200)
    }

    private var palette: ReaderColorPalette {
        theme.kind == .dark ? model.settings.darkReaderPalette : model.settings.lightReaderPalette
    }

    private var sourceText: String {
        switch source {
        case .document(let view):
            return view.document.text
        case .guide:
            return MarkdownGuideContent.text(for: model.localization.language)
        }
    }

    private var title: String {
        switch source {
        case .document(let view):
            return model.readerTitle(for: view)
        case .guide:
            return model.localization.get("help.markdownGuide")
        }
    }

    private struct RefreshKey: Equatable {
        let text: String
        let typography: PreviewTypography
        let palette: ReaderColorPalette
        let kind: ThemeKind
        let language: AppLanguage
    }

    private func refresh(colors: ReaderColors) async {
        // Wait for a pause in the typing; a newer change cancels this task and starts another.
        if hasRendered {
            try? await Task.sleep(for: .milliseconds(250))
            if Task.isCancelled {
                return
            }
        }

        let text = sourceText
        let directory: String
        if case .document(let view) = source, let path = view.document.filePath {
            directory = (path as NSString).deletingLastPathComponent
        } else {
            directory = FileManager.default.currentDirectoryPath
        }

        let parsed = await Task.detached(priority: .userInitiated) {
            MarkdownPreviewService().parse(text, sourceDirectory: directory)
        }.value
        if Task.isCancelled {
            return
        }

        let renderer = ReaderRenderer(
            typography: model.settings.previewTypography,
            colors: colors,
            missingImageLabel: model.localization.get("preview.missingImage"))
        content = renderer.render(parsed)
        hasRendered = true
    }
}

/// The read-only text view the rendered document is shown in.
private struct ReaderTextView: NSViewRepresentable {
    let content: NSAttributedString
    let background: NSColor
    let zoom: Double
    let copyTitle: String
    let selectAllTitle: String
    let onZoom: (Double) -> Void

    func makeCoordinator() -> Coordinator {
        Coordinator()
    }

    func makeNSView(context: Context) -> NSScrollView {
        // Built on a layout manager, as the editor is: text tables and text blocks, which the
        // renderer relies on, exist only in that text system.
        let storage = NSTextStorage()
        let layoutManager = NSLayoutManager()
        storage.addLayoutManager(layoutManager)
        let container = NSTextContainer(size: NSSize(width: 0, height: CGFloat.greatestFiniteMagnitude))
        container.widthTracksTextView = true
        container.lineFragmentPadding = 0
        layoutManager.addTextContainer(container)

        let textView = ReaderNSTextView(frame: .zero, textContainer: container)
        textView.isEditable = false
        textView.isSelectable = true
        textView.isRichText = true
        textView.textContainerInset = NSSize(width: 34, height: 26)
        textView.minSize = .zero
        textView.maxSize = NSSize(width: CGFloat.greatestFiniteMagnitude, height: CGFloat.greatestFiniteMagnitude)
        textView.isVerticallyResizable = true
        textView.isHorizontallyResizable = false
        textView.autoresizingMask = [.width]
        // The renderer has already coloured and underlined links; keep the system from repainting them.
        textView.linkTextAttributes = [.cursor: NSCursor.pointingHand]
        textView.delegate = context.coordinator

        let scrollView = NSScrollView()
        scrollView.hasVerticalScroller = true
        scrollView.hasHorizontalScroller = false
        scrollView.autohidesScrollers = true
        scrollView.allowsMagnification = true
        scrollView.minMagnification = 0.5
        scrollView.maxMagnification = 2
        scrollView.documentView = textView
        context.coordinator.attach(textView: textView, scrollView: scrollView, storage: storage)
        return scrollView
    }

    func updateNSView(_ scrollView: NSScrollView, context: Context) {
        let coordinator = context.coordinator
        coordinator.onZoom = onZoom
        guard let textView = coordinator.textView else {
            return
        }

        textView.onZoom = { [zoom] step in onZoom(zoom + step) }
        textView.copyTitle = copyTitle
        textView.selectAllTitle = selectAllTitle
        if textView.backgroundColor != background {
            textView.backgroundColor = background
            scrollView.backgroundColor = background
        }
        if coordinator.shown !== content {
            coordinator.shown = content
            coordinator.storage?.setAttributedString(content)
        }
        let magnification = zoom / 100
        if abs(scrollView.magnification - magnification) > 0.001 {
            scrollView.magnification = magnification
        }
    }

    static func dismantleNSView(_ scrollView: NSScrollView, coordinator: Coordinator) {
        NotificationCenter.default.removeObserver(coordinator)
    }

    @MainActor
    final class Coordinator: NSObject, NSTextViewDelegate {
        weak var textView: ReaderNSTextView?
        weak var scrollView: NSScrollView?
        var storage: NSTextStorage?
        var shown: NSAttributedString?
        var onZoom: ((Double) -> Void)?

        func attach(textView: ReaderNSTextView, scrollView: NSScrollView, storage: NSTextStorage) {
            self.textView = textView
            self.scrollView = scrollView
            self.storage = storage
            NotificationCenter.default.addObserver(
                self,
                selector: #selector(pinchEnded),
                name: NSScrollView.didEndLiveMagnifyNotification,
                object: scrollView)
        }

        /// A trackpad pinch magnifies the scroll view directly; report where it ended so the
        /// slider follows and the zoom settles on a 10% step like every other way of zooming.
        @objc private func pinchEnded() {
            if let scrollView {
                onZoom?(scrollView.magnification * 100)
            }
        }

        // Only web and mail links leave the app, the same rule as the Windows build.
        func textView(_ textView: NSTextView, clickedOnLink link: Any, at charIndex: Int) -> Bool {
            let text = (link as? String) ?? (link as? URL)?.absoluteString ?? ""
            if let url = URL(string: text), let scheme = url.scheme?.lowercased(), ["http", "https", "mailto"].contains(scheme) {
                NSWorkspace.shared.open(url)
            }
            return true
        }
    }
}

final class ReaderNSTextView: NSTextView {
    /// Called with a step of +10 or -10 percent.
    var onZoom: ((Double) -> Void)?
    var copyTitle = "Copy"
    var selectAllTitle = "Select All"

    private var scrollTravel: CGFloat = 0

    // The reader offers just these two, as on Windows, instead of the system's long text menu.
    override func menu(for event: NSEvent) -> NSMenu? {
        let menu = NSMenu()
        menu.addItem(withTitle: copyTitle, action: #selector(copy(_:)), keyEquivalent: "")
        menu.addItem(withTitle: selectAllTitle, action: #selector(selectAll(_:)), keyEquivalent: "")
        return menu
    }

    override func scrollWheel(with event: NSEvent) {
        guard event.modifierFlags.intersection(.deviceIndependentFlagsMask) == .command else {
            scrollTravel = 0
            super.scrollWheel(with: event)
            return
        }
        guard event.momentumPhase.isEmpty else {
            return
        }

        // Same direction as the editor: pushing away zooms in, whatever "natural scrolling" says.
        let delta = event.isDirectionInvertedFromDevice ? -event.scrollingDeltaY : event.scrollingDeltaY
        guard delta != 0 else {
            return
        }

        if event.hasPreciseScrollingDeltas {
            if event.phase == .began {
                scrollTravel = 0
            }
            scrollTravel += delta
            let steps = (scrollTravel / 40).rounded(.towardZero)
            if steps != 0 {
                scrollTravel -= steps * 40
                onZoom?(Double(steps) * 10)
            }
        } else {
            onZoom?(delta > 0 ? 10 : -10)
        }
    }
}
