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
        model.openExternal(paths: urls.filter(\.isFileURL).map(\.path))
    }

    func applicationDidFinishLaunching(_ notification: Notification) {
        Task {
            await model.loadWorkspace()
            #if DEBUG
            if let name = UserDefaults.standard.string(forKey: "GlystrataOpenWindow") {
                // Files named on the command line arrive a moment after launch; let them open first.
                try? await Task.sleep(for: .seconds(1))
                model.openWindowForDebugging(name)
            }
            #endif
        }
        #if DEBUG
        saveSnapshotIfRequested()
        #endif
    }

    // Closing the window leaves the app and its open documents running, as Mac apps do; the Dock
    // icon brings the window back. Unsaved work is asked about here, when the app really quits.
    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        guard model.hasUnsavedDocuments else {
            model.finishQuitting()
            return .terminateNow
        }

        Task {
            let proceed = await model.resolveUnsavedDocuments()
            if proceed {
                model.finishQuitting()
            }
            NSApp.reply(toApplicationShouldTerminate: proceed)
        }
        return .terminateLater
    }

    #if DEBUG
    /// Development aid: `-GlystrataSnapshotPath <file.png>` saves a picture of the main window a few
    /// seconds after launch (`-GlystrataSnapshotDelay <seconds>`) and quits. Other open windows are
    /// saved beside it as `<file>-aux1.png`, `-aux2.png`… The windows draw themselves, so no
    /// screen-recording permission is involved.
    private func saveSnapshotIfRequested() {
        guard let path = UserDefaults.standard.string(forKey: "GlystrataSnapshotPath") else {
            return
        }

        let delay = UserDefaults.standard.double(forKey: "GlystrataSnapshotDelay")
        DispatchQueue.main.asyncAfter(deadline: .now() + (delay > 0 ? delay : 3)) { [model] in
            let auxiliary = model.windows.all
            let main = NSApp.windows.first { window in
                window.isVisible && window.contentView != nil && !auxiliary.contains { $0 === window } && window.styleMask.contains(.titled)
            }
            let stem = (path as NSString).deletingPathExtension
            var targets: [(NSWindow, String)] = main.map { [($0, path)] } ?? []
            targets += auxiliary.enumerated().map { ($1, "\(stem)-aux\($0 + 1).png") }
            for (window, file) in targets {
                if let view = window.contentView?.superview, let bitmap = view.bitmapImageRepForCachingDisplay(in: view.bounds) {
                    view.cacheDisplay(in: view.bounds, to: bitmap)
                    try? bitmap.representation(using: .png, properties: [:])?.write(to: URL(fileURLWithPath: file))
                }
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
        CommandGroup(replacing: .appInfo) {
            item(.helpAbout) { model.showAbout() }
        }

        CommandGroup(replacing: .appSettings) {
            item(.settingsOpen) { model.openSettings() }
        }

        CommandGroup(replacing: .newItem) {
            item(.fileNew) { model.newUntitled() }
            item(.fileOpen) { model.openFiles() }
            Menu(model.localization.get("file.recent")) {
                let files = model.recentFiles.files
                if files.isEmpty {
                    Button(model.localization.get("file.recent.empty")) {}.disabled(true)
                } else {
                    ForEach(files, id: \.path) { entry in
                        Button((entry.path as NSString).lastPathComponent) {
                            model.open(paths: [entry.path])
                        }
                        .help(entry.path)
                        .disabled(!FileManager.default.fileExists(atPath: entry.path))
                    }
                    Divider()
                    Button(model.localization.get("file.recent.clear")) {
                        model.recentFiles.clear()
                        model.scheduleSessionSave()
                    }
                }
            }
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
            Divider()
            item(.fileSnapshotHistory, enabled: model.activeView != nil) {
                if let view = model.activeView {
                    model.openSnapshotHistory(for: view)
                }
            }
            item(.fileCreateSnapshot, enabled: model.activeView?.document.filePath != nil) {
                if let view = model.activeView {
                    Task { await model.createSnapshotNow(view) }
                }
            }
        }

        CommandGroup(replacing: .textEditing) {
            item(.editFindReplace, enabled: model.activeView != nil) { model.openFindReplace(focusReplace: false) }
            Button(model.localization.get("findReplace.replace")) { model.openFindReplace(focusReplace: true) }
                .keyboardShortcut(AppCommands.definition(.editReplace).shortcut)
                .disabled(model.activeView == nil)
        }

        CommandGroup(before: .toolbar) {
            item(.viewSplitHorizontal, enabled: model.activeView != nil) { model.splitActivePane(.horizontal) }
            item(.viewSplitVertical, enabled: model.activeView != nil) { model.splitActivePane(.vertical) }
            item(.viewClosePane) { model.closeActivePane() }
            item(.viewResetLayout) { model.collapseToSinglePane() }
            item(.viewNewView) { model.openNewView() }
            Button(model.localization.get("preview.reader")) {
                if let view = model.activeView {
                    model.toggleReader(for: view)
                }
            }
            .keyboardShortcut(AppCommands.definition(.viewToggleReader).shortcut)
            .disabled(model.activeView.map { SyntaxHighlighter.isYAMLFile($0.document.filePath) } ?? true)
            Divider()
            item(.viewZoomIn, enabled: model.editorZoom < 2) { model.setEditorZoom(model.editorZoom + EditorTextView.zoomStep) }
            item(.viewZoomOut, enabled: model.editorZoom > 0.5) { model.setEditorZoom(model.editorZoom - EditorTextView.zoomStep) }
            item(.viewZoomReset, enabled: model.editorZoom != 1) { model.setEditorZoom(1) }
            Divider()
        }

        groupMenu

        CommandGroup(replacing: .help) {
            item(.helpMarkdownGuide) { model.showMarkdownGuide() }
        }
    }

    // The top-level title of a menu this app adds itself; the system's own menus follow the system language.
    @CommandsBuilder private var groupMenu: some Commands {
        CommandMenu(model.localization.get("menu.group")) {
            item(.groupNew) { model.newGroup() }
            item(.groupOpenFile) {
                if let groupID = model.currentGroupID, let group = model.groups.find(groupID) {
                    model.openFiles(into: group)
                }
            }
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
