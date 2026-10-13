import AppKit
import GlystrataCore
import SwiftUI

@main
struct GlystrataApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var delegate

    var body: some Scene {
        let model = delegate.model
        Window("Glystrata", id: "main") {
            MainWindowView()
                .environment(model)
                .frame(minWidth: 760, minHeight: 500)
        }
        .defaultSize(width: 1240, height: 760)
        .commands {
            AppMenuCommands(model: model)
        }
    }
}

/// Owns the model so that files handed over by Finder (double-click, "Open With", a drop on the
/// Dock icon) have somewhere to go even before the window exists.
@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    let model = AppModel()

    func application(_ application: NSApplication, open urls: [URL]) {
        model.open(paths: urls.filter(\.isFileURL).map(\.path))
    }

    #if DEBUG
    /// Development aid: `-GlystrataSnapshotPath <file.png>` saves a picture of the main window a few
    /// seconds after launch and quits. The window draws itself, so no screen-recording permission
    /// is involved; used for comparing the layout against the Windows build.
    func applicationDidFinishLaunching(_ notification: Notification) {
        guard let path = UserDefaults.standard.string(forKey: "GlystrataSnapshotPath") else {
            return
        }

        let delay = UserDefaults.standard.double(forKey: "GlystrataSnapshotDelay")
        DispatchQueue.main.asyncAfter(deadline: .now() + (delay > 0 ? delay : 3)) {
            if let view = NSApp.windows.first(where: { $0.isVisible && $0.contentView != nil })?.contentView?.superview,
               let bitmap = view.bitmapImageRepForCachingDisplay(in: view.bounds) {
                view.cacheDisplay(in: view.bounds, to: bitmap)
                try? bitmap.representation(using: .png, properties: [:])?.write(to: URL(fileURLWithPath: path))
            }
            NSApp.terminate(nil)
        }
    }
    #endif
}

/// The app's entries in the menu bar. Titles and keys both come from the shared command table.
struct AppMenuCommands: Commands {
    let model: AppModel

    var body: some Commands {
        CommandGroup(replacing: .newItem) {
            item(.fileNew) { model.newUntitled() }
            item(.fileOpen) { model.openFiles() }
        }

        CommandGroup(replacing: .saveItem) {
            item(.fileSave, enabled: model.activeView != nil) {
                if let view = model.activeView {
                    Task { await model.save(view) }
                }
            }
            item(.fileSaveAs, enabled: model.activeView != nil) {
                if let view = model.activeView {
                    Task { await model.saveAs(view) }
                }
            }
            Divider()
            Button(model.localization.get("dialog.close")) {
                if let view = model.activeView {
                    Task { await model.close(view) }
                }
            }
            .keyboardShortcut(AppCommands.definition(.viewCloseTab).shortcut)
            .disabled(model.activeView == nil)
        }

        CommandGroup(before: .toolbar) {
            item(.viewSplitHorizontal, enabled: model.activeView != nil) { model.splitActivePane(.horizontal) }
            item(.viewSplitVertical, enabled: model.activeView != nil) { model.splitActivePane(.vertical) }
            item(.viewClosePane) { model.closeActivePane() }
            item(.viewResetLayout) { model.collapseToSinglePane() }
            Divider()
            item(.viewZoomIn, enabled: model.editorZoom < 2) { model.setEditorZoom(model.editorZoom + EditorTextView.zoomStep) }
            item(.viewZoomOut, enabled: model.editorZoom > 0.5) { model.setEditorZoom(model.editorZoom - EditorTextView.zoomStep) }
            item(.viewZoomReset, enabled: model.editorZoom != 1) { model.setEditorZoom(1) }
            Divider()
        }
    }

    private func item(_ id: AppCommandID, enabled: Bool = true, action: @escaping () -> Void) -> some View {
        let definition = AppCommands.definition(id)
        return Button(model.localization.get(definition.localizationKey ?? ""), action: action)
            .keyboardShortcut(definition.shortcut)
            .disabled(!enabled)
    }
}

extension View {
    /// Applies a command-table shortcut, or none when the command has no key.
    @ViewBuilder
    func keyboardShortcut(_ shortcut: KeyShortcut?) -> some View {
        if let shortcut {
            keyboardShortcut(KeyEquivalent(shortcut.key), modifiers: shortcut.eventModifiers)
        } else {
            self
        }
    }
}

extension KeyShortcut {
    var eventModifiers: EventModifiers {
        var result: EventModifiers = []
        if modifiers.contains(.command) { result.insert(.command) }
        if modifiers.contains(.shift) { result.insert(.shift) }
        if modifiers.contains(.option) { result.insert(.option) }
        if modifiers.contains(.control) { result.insert(.control) }
        return result
    }
}
