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

    private var selectedViewIDs: [UUID: UUID] = [:]
    @ObservationIgnored private let stateStore = JSONStateStore()
    private var theme = ThemeColors.light
    @ObservationIgnored private(set) lazy var textStores = DocumentTextStoreRegistry(style: editorStyle)

    init() {
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
        editorZoom = min(max((zoom * 10).rounded() / 10, 0.5), 2.0)
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

    // MARK: - File commands

    func newUntitled() {
        select(documents.createUntitled(paneID: activePaneID, sourceGroupID: selectedGroupID))
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
        for path in paths {
            do {
                let view = try documents.openView(path, paneID: activePaneID, sourceGroupID: selectedGroupID)
                select(view)
                recentFiles.add(path)
            } catch {
                showError(localization.get("error.open"), detail: error.localizedDescription)
            }
        }
    }

    @discardableResult
    func save(_ view: DocumentViewState) async -> Bool {
        guard !view.document.isUntitled else {
            return await saveAs(view)
        }

        let result = await documents.save(view.document)
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

        let result = await documents.saveAs(view.document, to: url.path)
        if result.success {
            recentFiles.add(url.path)
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
        }
        if selectedViewIDs[view.paneID] == view.viewID {
            selectedViewIDs[view.paneID] = nil
        }
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
    }

    func closeActivePane() {
        let result = PaneLayoutOperations.removePane(layout, paneID: activePaneID)
        guard result.removed, let remaining = result.node else {
            return
        }

        let target = PaneLayoutOperations.firstPaneID(remaining)
        var order = (documents.views.filter { $0.paneID == target }.map(\.tabOrder).max() ?? -1) + 1
        for view in documents.views.filter({ $0.paneID == activePaneID }).sorted(by: { $0.tabOrder < $1.tabOrder }) {
            view.paneID = target
            view.tabOrder = order
            order += 1
        }
        selectedViewIDs[activePaneID] = nil
        layout = remaining
        activePaneID = target
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
    }

    func setSplitRatio(_ splitID: UUID, _ ratio: Double) {
        layout = PaneLayoutOperations.settingRatio(layout, splitID: splitID, ratio: ratio)
    }

    // MARK: - Dialogs

    private enum UnsavedChoice {
        case save
        case discard
        case cancel
    }

    private func confirmUnsavedChanges() -> UnsavedChoice {
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

    private func showError(_ title: String, detail: String) {
        let alert = NSAlert()
        alert.alertStyle = .warning
        alert.messageText = title
        alert.informativeText = detail
        alert.addButton(withTitle: localization.get("dialog.ok"))
        alert.runModal()
    }
}
