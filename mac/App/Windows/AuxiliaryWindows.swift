import AppKit
import GlystrataCore
import SwiftUI

/// Opens and keeps track of the windows other than the main one (settings, find and replace,
/// snapshot history, readers, the Markdown guide). Each is a plain AppKit window around SwiftUI
/// content, keyed by an identifier so asking for one that is already open brings it forward.
@MainActor
final class AuxiliaryWindows: NSObject, NSWindowDelegate {
    private var windows: [String: NSWindow] = [:]
    private var closeHandlers: [String: () -> Void] = [:]

    /// In the order they were opened.
    var all: [NSWindow] {
        windows.values.sorted { $0.windowNumber < $1.windowNumber }
    }

    func isOpen(_ id: String) -> Bool {
        windows[id] != nil
    }

    func window(_ id: String) -> NSWindow? {
        windows[id]
    }

    /// Shows the window `id`, creating it from `content` the first time. `onClose` runs once,
    /// when the window goes away for any reason.
    func show<Content: View>(
        id: String,
        title: String,
        size: NSSize,
        minSize: NSSize? = nil,
        isResizable: Bool = true,
        isFloating: Bool = false,
        model: AppModel,
        onClose: (() -> Void)? = nil,
        @ViewBuilder content: () -> Content
    ) {
        if let existing = windows[id] {
            existing.title = title
            existing.makeKeyAndOrderFront(nil)
            return
        }

        var style: NSWindow.StyleMask = [.titled, .closable, .miniaturizable]
        if isResizable {
            style.insert(.resizable)
        }
        let window = NSWindow(contentRect: NSRect(origin: .zero, size: size), styleMask: style, backing: .buffered, defer: false)
        window.title = title
        window.isReleasedWhenClosed = false
        let root = content()
        window.contentViewController = NSHostingController(rootView: ThemedRoot { root }.environment(model))
        window.setContentSize(size)
        if let minSize {
            window.contentMinSize = minSize
        }
        if isFloating {
            window.level = .floating
            window.hidesOnDeactivate = true
        }
        window.delegate = self
        window.center()
        windows[id] = window
        closeHandlers[id] = onClose
        window.makeKeyAndOrderFront(nil)
    }

    func setTitle(_ title: String, for id: String) {
        windows[id]?.title = title
    }

    func close(_ id: String) {
        windows[id]?.close()
    }

    func closeAll(withPrefix prefix: String = "") {
        for (id, window) in windows where id.hasPrefix(prefix) {
            window.close()
        }
    }

    func windowWillClose(_ notification: Notification) {
        guard let closing = notification.object as? NSWindow,
              let id = windows.first(where: { $0.value === closing })?.key else {
            return
        }

        windows[id] = nil
        closeHandlers.removeValue(forKey: id)?()
    }
}

/// Gives a window's content the app's colours, text style and light/dark choice, the same way
/// the main window gets them.
struct ThemedRoot<Content: View>: View {
    @ViewBuilder let content: () -> Content

    @Environment(AppModel.self) private var model
    @Environment(\.colorScheme) private var colorScheme

    var body: some View {
        let theme = ThemeColors.resolve(model.settings.theme, system: colorScheme)
        content()
            .environment(\.theme, theme)
            .font(.system(size: 12))
            .foregroundStyle(theme.primaryText.color)
            .background(theme.windowBackground.color)
            .preferredColorScheme(model.settings.theme.colorScheme)
    }
}

extension ThemePreference {
    /// Nil follows the system.
    var colorScheme: ColorScheme? {
        switch self {
        case .light: return .light
        case .dark: return .dark
        case .system: return nil
        }
    }
}
