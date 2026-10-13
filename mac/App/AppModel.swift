import AppKit
import GlystrataCore
import Observation
import SwiftUI

/// Everything the main window shows and the commands act on.
@MainActor
@Observable
final class AppModel {
    var settings: AppSettings
    let localization: LocalizationService
    let documents = DocumentManager()
    let groups = GroupManager()
    let recentFiles = RecentFilesService()

    var layout: PaneLayoutNode
    var activePaneID: UUID
    /// nil shows every tab; otherwise only the tabs opened from that group.
    var selectedGroupID: UUID?
    var editorZoom = 1.0
    var selection = EditorSelection()
    /// Replaces the saved/modified wording in the status bar until the next edit, e.g. after an auto-save.
    var statusOverride: String?

    var selectedViewIDs: [UUID: UUID] = [:]
    @ObservationIgnored let stateStore: JSONStateStore
    private var theme = ThemeColors.light
    @ObservationIgnored private(set) lazy var textStores = DocumentTextStoreRegistry(style: editorStyle) { [weak self] document in
        self?.documentEdited(document)
    }
    @ObservationIgnored let snapshots: SnapshotService

    /// The live text view of each tab on screen, for commands that act on the text itself.
    @ObservationIgnored private var editors: [UUID: EditorTextView] = [:]

    /// The group or tab being dragged within the window right now; see `DragItem`.
    @ObservationIgnored var dragging: DragItem?

    // Background housekeeping; see AppModel+Workspace.swift.
    @ObservationIgnored var isWorkspaceLoaded = false
    @ObservationIgnored var isQuitting = false
    @ObservationIgnored var pathsWaitingForWorkspace: [String] = []
    @ObservationIgnored var sessionSaveTask: Task<Void, Never>?
    @ObservationIgnored var autoSaveTasks: [UUID: Task<Void, Never>] = [:]
    @ObservationIgnored var autoSaveFailures = Set<UUID>()
    @ObservationIgnored var lastChanged: [UUID: Date] = [:]
    @ObservationIgnored var savesInFlight: [UUID: Int] = [:]
    @ObservationIgnored var knownExternalFingerprints: [UUID: FileFingerprint] = [:]
    @ObservationIgnored var isCheckingExternalChanges = false
    @ObservationIgnored var housekeepingTimers: [Timer] = []

    init() {
        #if DEBUG
        // Development aid: `-GlystrataStateDirectory <folder>` keeps a test run's settings and
        // session away from the real ones.
        stateStore = JSONStateStore(baseDirectory: UserDefaults.standard.string(forKey: "GlystrataStateDirectory"))
        #else
        stateStore = JSONStateStore()
        #endif
        var loaded = stateStore.loadSettingsForStartup()
        #if DEBUG
        // Development aid for snapshots: `-GlystrataTheme light|dark` overrides the saved theme for this run.
        switch UserDefaults.standard.string(forKey: "GlystrataTheme") {
        case "light": loaded.theme = .light
        case "dark": loaded.theme = .dark
        default: break
        }
        #endif
        settings = loaded
        localization = LocalizationService(language: loaded.language)
        snapshots = SnapshotService(store: SnapshotSidecarStore(hideSidecarFiles: loaded.hideSnapshotFiles))
        let paneID = UUID()
        layout = .editorPane(paneID: paneID)
        activePaneID = paneID
    }

    // MARK: - Appearance

    var editorStyle: EditorStyle {
        EditorStyle(
            fontSize: EditorStyle.baseFontSize * editorZoom,
            foreground: theme.editorForeground,
            background: theme.editorBackground,
            gutterForeground: theme.secondaryText,
            palette: theme.kind == .dark ? settings.darkEditorPalette : settings.lightEditorPalette)
    }

    func applyTheme(_ theme: ThemeColors) {
        if self.theme != theme {
            self.theme = theme
        }
        textStores.apply(editorStyle)
    }

    func setEditorZoom(_ zoom: Double) {
        let limited = min(max((zoom * 10).rounded() / 10, 0.5), 2.0)
        guard limited != editorZoom else {
            return
        }
        editorZoom = limited
        textStores.apply(editorStyle)
    }

    // MARK: - Tabs

    func views(in paneID: UUID) -> [DocumentViewState] {
        documents.views
            .filter { $0.paneID == paneID && isVisible($0) }
            .sorted { $0.tabOrder < $1.tabOrder }
    }

    func isVisible(_ view: DocumentViewState) -> Bool {
        selectedGroupID == nil || view.sourceGroupID == selectedGroupID
    }

    func selectedView(in paneID: UUID) -> DocumentViewState? {
        let candidates = views(in: paneID)
        if let id = selectedViewIDs[paneID], let match = candidates.first(where: { $0.viewID == id }) {
            return match
        }
        return candidates.last
    }

    var activeView: DocumentViewState? { selectedView(in: activePaneID) }

    func select(_ view: DocumentViewState) {
        selectedViewIDs[view.paneID] = view.viewID
        activePaneID = view.paneID
        statusOverride = nil
        scheduleSessionSave()
    }

    func activate(paneID: UUID) {
        if activePaneID != paneID {
            activePaneID = paneID
        }
    }

    func title(for view: DocumentViewState) -> String {
        let name = view.document.filePath.map { ($0 as NSString).lastPathComponent } ?? localization.get("document.untitled")
        return view.document.isModified ? "\(name) •" : name
    }

    // MARK: - Editors

    func setEditor(_ editor: EditorTextView?, for viewID: UUID) {
        editors[viewID] = editor
    }

    func editor(for view: DocumentViewState?) -> EditorTextView? {
        view.flatMap { editors[$0.viewID] }
    }

    func applyFormatting(_ command: MarkdownFormatCommand, to view: DocumentViewState) {
        guard let editor = editor(for: view) else {
            return
        }

        if command == .link {
            guard let url = promptForText(title: localization.get("format.link"), message: localization.get("format.linkUrl"), initial: "https://") else {
                return
            }
            editor.applyFormatting(command, linkURL: url)
        } else {
            editor.applyFormatting(command)
        }
    }

    // MARK: - File commands

    func newUntitled() {
        select(documents.createUntitled(paneID: paneForNewTabs, sourceGroupID: selectedGroupID))
    }

    /// A second tab on the active document, in the same pane.
    func openNewView() {
        guard let active = activeView else {
            newUntitled()
            return
        }
        select(documents.createView(active.document, sourceGroupID: active.sourceGroupID, paneID: active.paneID))
    }

    /// The active pane, or the first one when the active pane has gone from the layout.
    var paneForNewTabs: UUID {
        PaneLayoutOperations.containsPane(layout, paneID: activePaneID) ? activePaneID : PaneLayoutOperations.firstPaneID(layout)
    }

    func openFiles() {
        let panel = NSOpenPanel()
        panel.allowsMultipleSelection = true
        panel.canChooseDirectories = false
        guard panel.runModal() == .OK else {
            return
        }
        open(paths: panel.urls.map(\.path))
    }

    func open(paths: [String]) {
        open(paths: paths, sourceGroupID: selectedGroupID)
    }

    func open(paths: [String], sourceGroupID: UUID?) {
        for path in paths {
            do {
                let view = try documents.openView(path, paneID: paneForNewTabs, sourceGroupID: sourceGroupID)
                attach(view.document)
                select(view)
                recentFiles.add(path)
            } catch {
                // A path that will not open is not worth offering again; this is also what drops a
                // file picked from the recent list after it was deleted from disk.
                recentFiles.remove(path)
                scheduleSessionSave()
                showError(localization.get("error.open"), detail: error.localizedDescription)
            }
        }
    }

    @discardableResult
    func save(_ view: DocumentViewState) async -> Bool {
        guard !view.document.isUntitled else {
            return await saveAs(view)
        }

        let result = await performSave(view.document)
        if !result.success {
            showError(localization.get("error.save"), detail: result.errorMessage ?? "")
        }
        return result.success
    }

    @discardableResult
    func saveAs(_ view: DocumentViewState) async -> Bool {
        let panel = NSSavePanel()
        panel.canCreateDirectories = true
        if let path = view.document.filePath {
            panel.directoryURL = URL(fileURLWithPath: path).deletingLastPathComponent()
            panel.nameFieldStringValue = (path as NSString).lastPathComponent
        } else {
            panel.nameFieldStringValue = "\(localization.get("document.untitled")).md"
        }

        guard panel.runModal() == .OK, let url = panel.url else {
            return false
        }

        let result = await performSave(view.document, to: url.path)
        if result.success {
            recentFiles.add(url.path)
            attach(view.document)
            scheduleSessionSave()
        } else {
            showError(localization.get("error.save"), detail: result.errorMessage ?? "")
        }
        return result.success
    }

    /// Closes a tab, first offering to save when this is the last tab showing unsaved changes.
    func close(_ view: DocumentViewState) async {
        let isLastView = !documents.views.contains { $0.document === view.document && $0.viewID != view.viewID }
        if view.document.isModified && isLastView {
            switch confirmUnsavedChanges() {
            case .save:
                guard await save(view) else {
                    return
                }
            case .discard:
                break
            case .cancel:
                return
            }
        }

        let document = view.document
        documents.closeView(view.viewID)
        if !documents.documents.contains(where: { $0 === document }) {
            textStores.discard(document.sessionID)
            detach(document)
        }
        if selectedViewIDs[view.paneID] == view.viewID {
            selectedViewIDs[view.paneID] = nil
        }
        // A pane left with no tabs at all closes with its last tab.
        if !documents.views.contains(where: { $0.paneID == view.paneID }) {
            removePane(view.paneID)
        }
        scheduleSessionSave()
    }

    // MARK: - Panes

    func splitActivePane(_ orientation: SplitOrientation) {
        let paneID = PaneLayoutOperations.containsPane(layout, paneID: activePaneID)
            ? activePaneID
            : PaneLayoutOperations.firstPaneID(layout)
        let paneViews = views(in: paneID)
        guard let last = paneViews.last else {
            return
        }

        let newPaneID = UUID()
        layout = PaneLayoutOperations.replacePaneWithSplit(layout, paneID: paneID, orientation: orientation, newPaneID: newPaneID)
        if paneViews.count == 1 {
            // A lone tab has nothing to hand over, so the new pane gets a second view of the same file instead.
            select(documents.createView(last.document, sourceGroupID: last.sourceGroupID, paneID: newPaneID))
        } else {
            last.paneID = newPaneID
            select(last)
        }
        scheduleSessionSave()
    }

    func closeActivePane() {
        removePane(activePaneID)
    }

    /// Takes a pane out of the layout, handing its tabs to the first pane that remains.
    func removePane(_ paneID: UUID) {
        let result = PaneLayoutOperations.removePane(layout, paneID: paneID)
        guard result.removed, let remaining = result.node else {
            return
        }

        let target = PaneLayoutOperations.firstPaneID(remaining)
        var order = (documents.views.filter { $0.paneID == target }.map(\.tabOrder).max() ?? -1) + 1
        for view in documents.views.filter({ $0.paneID == paneID }).sorted(by: { $0.tabOrder < $1.tabOrder }) {
            view.paneID = target
            view.tabOrder = order
            order += 1
        }
        selectedViewIDs[paneID] = nil
        layout = remaining
        if activePaneID == paneID || !PaneLayoutOperations.containsPane(remaining, paneID: activePaneID) {
            activePaneID = target
        }
        scheduleSessionSave()
    }

    func collapseToSinglePane() {
        guard case .split = layout else {
            return
        }

        let target = PaneLayoutOperations.firstPaneID(layout)
        var order = 0
        for paneID in PaneLayoutOperations.paneIDs(layout) {
            for view in documents.views.filter({ $0.paneID == paneID }).sorted(by: { $0.tabOrder < $1.tabOrder }) {
                view.paneID = target
                view.tabOrder = order
                order += 1
            }
        }
        let keep = activeView?.viewID
        layout = PaneLayoutOperations.collapseToSinglePane(layout)
        selectedViewIDs = keep.map { [target: $0] } ?? [:]
        activePaneID = target
        scheduleSessionSave()
    }

    func setSplitRatio(_ splitID: UUID, _ ratio: Double) {
        layout = PaneLayoutOperations.settingRatio(layout, splitID: splitID, ratio: ratio)
        scheduleSessionSave()
    }

    // MARK: - Dialogs

    enum UnsavedChoice {
        case save
        case discard
        case cancel
    }

    func confirmUnsavedChanges() -> UnsavedChoice {
        let alert = NSAlert()
        alert.messageText = localization.get("dialog.unsavedTitle")
        alert.informativeText = localization.get("dialog.unsavedMessage")
        alert.addButton(withTitle: localization.get("file.save"))
        alert.addButton(withTitle: localization.get("dialog.cancel"))
        alert.addButton(withTitle: localization.get("dialog.dontSave"))
        switch alert.runModal() {
        case .alertFirstButtonReturn:
            return .save
        case .alertThirdButtonReturn:
            return .discard
        default:
            return .cancel
        }
    }

    /// Asks for one line of text. Nil when the user cancels.
    func promptForText(title: String, message: String, initial: String = "") -> String? {
        let alert = NSAlert()
        alert.messageText = title
        alert.informativeText = message
        let field = NSTextField(string: initial)
        field.frame = NSRect(x: 0, y: 0, width: 260, height: 24)
        alert.accessoryView = field
        alert.window.initialFirstResponder = field
        alert.addButton(withTitle: localization.get("dialog.ok"))
        alert.addButton(withTitle: localization.get("dialog.cancel"))
        return alert.runModal() == .alertFirstButtonReturn ? field.stringValue : nil
    }

    func showError(_ title: String, detail: String) {
        let alert = NSAlert()
        alert.alertStyle = .warning
        alert.messageText = title
        alert.informativeText = detail
        alert.addButton(withTitle: localization.get("dialog.ok"))
        alert.runModal()
    }
}
