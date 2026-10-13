import AppKit
import GlystrataCore
import UniformTypeIdentifiers

/// What is being dragged inside the window. The pasteboard only carries a marker; the drop targets
/// read the real item from here, which also lets them choose a drop indicator while hovering.
enum DragItem: Equatable {
    case group(UUID)
    case tab(UUID)

    /// A private type, so text views do not take the drag for text and paste a UUID into a document.
    static let pasteboardType = UTType(exportedAs: "com.firewatersmithy.glystrata.drag-item")

    var itemProvider: NSItemProvider {
        let provider = NSItemProvider()
        provider.registerDataRepresentation(forTypeIdentifier: Self.pasteboardType.identifier, visibility: .ownProcess) { completion in
            completion(Data(), nil)
            return nil
        }
        return provider
    }
}

/// Groups, and the things a tab's menu does to its document.
extension AppModel {
    // MARK: - Groups

    func selectGroup(_ groupID: UUID?) {
        selectedGroupID = groupID
        statusOverride = nil
    }

    func newGroup() {
        let title = localization.get("group.new")
        guard let name = promptForText(title: title), !name.trimmingCharacters(in: .whitespaces).isEmpty else {
            return
        }

        groups.createGroup(name)
        scheduleSessionSave()
    }

    func renameGroup(_ group: Group) {
        let title = localization.get("group.rename")
        guard let name = promptForText(title: title, initial: group.name),
              !name.trimmingCharacters(in: .whitespaces).isEmpty,
              groups.renameGroup(group.id, name: name) else {
            return
        }
        scheduleSessionSave()
    }

    func deleteGroup(_ group: Group) {
        guard confirm(title: localization.get("group.delete"), message: group.name, confirmTitle: localization.get("dialog.delete")) else {
            return
        }

        // The group's tabs stay open; they simply no longer belong to a group.
        for view in documents.views where view.sourceGroupID == group.id {
            view.sourceGroupID = nil
        }
        groups.deleteGroup(group.id)
        if selectedGroupID == group.id {
            selectedGroupID = nil
        }
        if groups.groups.isEmpty {
            groups.createGroup(localization.get("sidebar.groups"))
        }
        scheduleSessionSave()
    }

    func setColor(_ color: String?, for group: Group) {
        if groups.setGroupColor(group.id, color: color) {
            scheduleSessionSave()
        }
    }

    func openFiles(into group: Group) {
        let panel = NSOpenPanel()
        panel.allowsMultipleSelection = true
        panel.canChooseDirectories = false
        panel.message = localization.get("group.openFile")
        guard panel.runModal() == .OK else {
            return
        }
        add(paths: panel.urls.map(\.path), to: group)
    }

    /// Files arriving from the open dialog, Finder or a drop join the group being looked at and
    /// are added to its list, the way the Windows build files what its open dialog returns.
    func openIntoCurrentGroup(paths: [String]) {
        var isDirectory: ObjCBool = false
        let files = paths.filter { FileManager.default.fileExists(atPath: $0, isDirectory: &isDirectory) && !isDirectory.boolValue }
        guard !files.isEmpty else {
            return
        }

        if let groupID = currentGroupID, let group = groups.find(groupID) {
            add(paths: files, to: group)
        } else {
            open(paths: files, sourceGroupID: nil)
        }
    }

    private func add(paths: [String], to group: Group) {
        for path in paths {
            groups.addPath(group.id, path: path, kind: .file)
        }
        open(paths: paths, sourceGroupID: group.id)
        scheduleSessionSave()
    }

    func assign(_ view: DocumentViewState, to group: Group) {
        let previous = view.sourceGroupID
        guard previous != group.id else {
            return
        }

        view.sourceGroupID = group.id
        if let path = view.document.filePath {
            if let previous {
                groups.removePath(previous, path: path)
            }
            groups.addPath(group.id, path: path, kind: .file)
        }
        scheduleSessionSave()
    }

    /// Finishes a drag that ended on a group's row: a tab joins the group, another group takes its place.
    func drop(_ item: DragItem, on group: Group) {
        switch item {
        case .tab(let viewID):
            if let view = documents.findView(viewID) {
                assign(view, to: group)
            }
        case .group(let draggedID):
            guard draggedID != group.id,
                  let from = groups.groups.firstIndex(where: { $0.id == draggedID }),
                  let to = groups.groups.firstIndex(where: { $0.id == group.id }) else {
                return
            }
            groups.moveGroup(from: from, to: to)
            scheduleSessionSave()
        }
    }

    // MARK: - Tabs

    /// Finishes a tab drag that ended on another tab of the same pane: the dragged tab takes the
    /// target's place, landing after it when coming from the left and before it from the right.
    func moveTab(_ draggedID: UUID, onto target: DocumentViewState) {
        guard draggedID != target.viewID, let dragged = documents.findView(draggedID), dragged.paneID == target.paneID else {
            return
        }

        var order = documents.views.filter { $0.paneID == target.paneID }.sorted { $0.tabOrder < $1.tabOrder }.map(\.viewID)
        guard let from = order.firstIndex(of: draggedID), let to = order.firstIndex(of: target.viewID) else {
            return
        }

        order.remove(at: from)
        order.insert(draggedID, at: to)
        documents.reorderViews(paneID: target.paneID, orderedViewIDs: order)
        select(dragged)
    }

    func openInNewPane(_ view: DocumentViewState, orientation: SplitOrientation) {
        guard PaneLayoutOperations.containsPane(layout, paneID: view.paneID) else {
            return
        }

        let newPaneID = UUID()
        layout = PaneLayoutOperations.replacePaneWithSplit(layout, paneID: view.paneID, orientation: orientation, newPaneID: newPaneID)
        select(documents.createView(view.document, sourceGroupID: view.sourceGroupID, paneID: newPaneID))
    }

    func showInFinder(_ view: DocumentViewState) {
        if let path = view.document.filePath {
            NSWorkspace.shared.activateFileViewerSelecting([URL(fileURLWithPath: path)])
        }
    }

    func createSnapshotNow(_ view: DocumentViewState) async {
        guard view.document.filePath != nil else {
            showError(localization.get("snapshot.title"), detail: localization.get("dialog.noFile"))
            return
        }

        do {
            let snapshot = try await snapshots.create(for: view.document, maxSnapshots: settings.maxSnapshotsPerFile)
            statusOverride = localization.get(snapshot == nil ? "snapshot.unchanged" : "snapshot.created")
        } catch {
            showError(localization.get("snapshot.title"), detail: error.localizedDescription)
        }
    }

    /// Renames the file on disk and takes its snapshot history and group entries along.
    func renameDocument(_ view: DocumentViewState) {
        let title = localization.get("tab.rename")
        guard let oldPath = view.document.filePath else {
            showError(title, detail: localization.get("dialog.noFile"))
            return
        }

        let oldName = (oldPath as NSString).lastPathComponent
        guard let entered = promptForText(title: title, message: localization.get("tab.renameLabel"), initial: oldName) else {
            return
        }

        let newName = entered.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !newName.isEmpty, newName != oldName else {
            return
        }
        guard !newName.contains("/"), !newName.contains(":"), newName != ".", newName != ".." else {
            showError(title, detail: localization.get("tab.renameInvalid"))
            return
        }

        let newPath = ((oldPath as NSString).deletingLastPathComponent as NSString).appendingPathComponent(newName)
        // On the usual case-insensitive Mac volume "Notes.md" and "notes.md" are one file, so a
        // change of capitals alone must not be mistaken for a clash with an existing file.
        let onlyCaseDiffers = oldPath.compare(newPath, options: .caseInsensitive) == .orderedSame
        guard onlyCaseDiffers || !FileManager.default.fileExists(atPath: newPath) else {
            showError(title, detail: localization.get("tab.renameConflict"))
            return
        }

        do {
            try FileManager.default.moveItem(atPath: oldPath, toPath: newPath)
        } catch {
            showError(title, detail: error.localizedDescription)
            return
        }

        SnapshotSidecarStore.renameSidecar(from: oldPath, to: newPath)
        documents.renamePath(view.document, to: newPath)
        groups.renamePath(from: oldPath, to: newPath)
        recentFiles.remove(oldPath)
        recentFiles.add(newPath)
        attach(view.document)
        scheduleSessionSave()
    }

    // MARK: - Dialogs

    func confirm(title: String, message: String, confirmTitle: String) -> Bool {
        let alert = NSAlert()
        alert.messageText = title
        alert.informativeText = message
        alert.addButton(withTitle: confirmTitle)
        alert.addButton(withTitle: localization.get("dialog.cancel"))
        return alert.runModal() == .alertFirstButtonReturn
    }
}
