import AppKit
import GlystrataCore
import SwiftUI

/// The editor windows tabs can be moved out into. Each shows one pane that sits outside the main
/// window's layout. Its tabs are ordinary views on their documents, so what is typed in one shows
/// in every other view of the same file, and saving, snapshots and the reader work as anywhere else.
@MainActor
final class FloatingEditorWindows: NSObject, NSWindowDelegate {
    private var windows: [UUID: NSWindow] = [:]
    /// The size a window had before it was zoomed or made full screen, which is what is kept between
    /// runs, as Windows keeps the restored size of a maximized window.
    private var normalFrames: [UUID: NSRect] = [:]
    private weak var model: AppModel?

    /// In the order they were opened.
    var paneIDs: [UUID] {
        windows.sorted { $0.value.windowNumber < $1.value.windowNumber }.map(\.key)
    }

    var all: [NSWindow] {
        paneIDs.compactMap { windows[$0] }
    }

    func window(for paneID: UUID) -> NSWindow? {
        windows[paneID]
    }

    func contains(_ window: NSWindow) -> Bool {
        paneID(of: window) != nil
    }

    func paneID(of window: NSWindow) -> UUID? {
        windows.first { $0.value === window }?.key
    }

    func savedFrame(for paneID: UUID) -> NSRect? {
        guard let window = windows[paneID] else {
            return nil
        }
        if window.isZoomed || window.styleMask.contains(.fullScreen), let normal = normalFrames[paneID] {
            return normal
        }
        return window.frame
    }

    /// `frame` is where a restored window was; a new one goes beside `anchor`.
    func open(paneID: UUID, frame: NSRect?, beside anchor: NSWindow?, makeKey: Bool, model: AppModel) {
        self.model = model
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 720, height: 640),
            styleMask: [.titled, .closable, .miniaturizable, .resizable],
            backing: .buffered,
            defer: false)
        window.isReleasedWhenClosed = false
        let hosting = NSHostingController(rootView: ThemedRoot { FloatingEditorView(paneID: paneID) }.environment(model))
        // The window keeps the size it is given or restored to, not the content's own idea of one.
        hosting.sizingOptions = []
        window.contentViewController = hosting
        window.contentMinSize = NSSize(width: 420, height: 300)
        window.delegate = self
        windows[paneID] = window
        if let frame {
            window.setFrame(frame, display: false)
        } else {
            window.setContentSize(NSSize(width: 720, height: 640))
            AuxiliaryWindows.place(window, beside: anchor)
        }
        if makeKey {
            window.makeKeyAndOrderFront(nil)
        } else {
            window.orderFront(nil)
        }
    }

    /// Takes the window away without asking anything; the caller has dealt with its tabs.
    func close(_ paneID: UUID) {
        guard let window = windows.removeValue(forKey: paneID) else {
            return
        }
        normalFrames[paneID] = nil
        window.delegate = nil
        window.close()
    }

    /// The close button closes the window's tabs, each through the usual unsaved-changes question;
    /// the window goes once the last of them has.
    func windowShouldClose(_ sender: NSWindow) -> Bool {
        guard let paneID = paneID(of: sender), let model else {
            return true
        }
        Task { await model.closeFloatingWindow(paneID) }
        return false
    }

    func windowDidMove(_ notification: Notification) {
        model?.scheduleSessionSave()
    }

    func windowDidEndLiveResize(_ notification: Notification) {
        model?.scheduleSessionSave()
    }

    func windowShouldZoom(_ window: NSWindow, toFrame newFrame: NSRect) -> Bool {
        if !window.isZoomed, let paneID = paneID(of: window) {
            normalFrames[paneID] = window.frame
        }
        return true
    }

    func windowWillEnterFullScreen(_ notification: Notification) {
        if let window = notification.object as? NSWindow, !window.isZoomed, let paneID = paneID(of: window) {
            normalFrames[paneID] = window.frame
        }
    }
}

struct FloatingEditorView: View {
    let paneID: UUID

    @Environment(AppModel.self) private var model
    @Environment(\.theme) private var theme

    var body: some View {
        let view = model.selectedView(in: paneID)
        VStack(spacing: 0) {
            PaneView(paneID: paneID)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .background(theme.windowBackground.color)
            theme.border.color.frame(height: 1)
            StatusBarView(paneID: paneID)
        }
        .onChange(of: TitleState(view: view, model: model), initial: true) { _, state in
            guard let window = model.floatingWindows.window(for: paneID) else {
                return
            }
            window.title = state.name
            window.isDocumentEdited = state.isModified
            window.representedURL = state.path.map { URL(fileURLWithPath: $0) }
        }
    }

    private struct TitleState: Equatable {
        let name: String
        let path: String?
        let isModified: Bool

        @MainActor
        init(view: DocumentViewState?, model: AppModel) {
            path = view?.document.filePath
            name = path.map { ($0 as NSString).lastPathComponent } ?? model.localization.get("document.untitled")
            isModified = view?.document.isModified ?? false
        }
    }
}
