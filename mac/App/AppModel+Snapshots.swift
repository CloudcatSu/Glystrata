import AppKit
import GlystrataCore

extension AppModel {
    // MARK: - Snapshot history

    /// Returns the model behind the window, or nil when the document has no file to keep snapshots of.
    @discardableResult
    func openSnapshotHistory(for view: DocumentViewState) -> SnapshotHistoryModel? {
        guard view.document.filePath != nil else {
            showError(localization.get("snapshot.title"), detail: localization.get("dialog.noFile"))
            return nil
        }

        let history = SnapshotHistoryModel(app: self, view: view)
        windows.show(
            id: history.windowID,
            title: localization.get("snapshot.title"),
            size: NSSize(width: 720, height: 560),
            minSize: NSSize(width: 560, height: 360),
            model: self,
            // Notes are written as each field is left; this catches the one still being typed in.
            onClose: { Task { await history.saveAllNotes() } }
        ) {
            SnapshotHistoryView(history: history)
        }
        return history
    }

    /// Replaces the document's content with the snapshot's, or writes it out as a new file.
    func restoreSnapshot(_ snapshot: SnapshotInfo, mode: RestoreMode, in view: DocumentViewState) async {
        switch mode {
        case .replaceCurrent:
            let document = view.document
            document.applyRestoredContent(snapshot.text, encoding: snapshot.encoding, lineEnding: snapshot.lineEnding)
            lastChanged[document.sessionID] = Date()
            textStores.sync(document)
            scheduleSessionSave()

        case .saveAsNewFile:
            let panel = NSSavePanel()
            panel.canCreateDirectories = true
            panel.title = localization.get("snapshot.saveAsNew")
            panel.nameFieldStringValue = "restored.md"
            guard panel.runModal() == .OK, let url = panel.url else {
                return
            }

            let newView = documents.createUntitled(paneID: paneForNewTabs, sourceGroupID: selectedGroupID)
            let document = newView.document
            document.applyRestoredContent(snapshot.text, encoding: snapshot.encoding, lineEnding: snapshot.lineEnding)
            let result = await performSave(document, to: url.path)
            guard result.success else {
                documents.closeView(newView.viewID)
                textStores.discard(document.sessionID)
                detach(document)
                showError(localization.get("error.save"), detail: result.errorMessage ?? localization.get("error.save"))
                return
            }

            attach(document)
            select(newView)
            scheduleSessionSave()
        }
    }

    // MARK: - Comparing

    /// Opens a comparison of two fixed texts; `old` is the left side of the diff, `new` the right.
    /// Any number of these can be open at once.
    func showDiff(old: String, new: String, date: Date) {
        let rows = TextDiffService().compare(old, new)
        windows.show(
            id: "diff-\(UUID().uuidString)",
            title: diffTitle(date),
            size: NSSize(width: 980, height: 720),
            minSize: NSSize(width: 640, height: 400),
            model: self
        ) {
            FixedDiffView(rows: rows)
        }
    }

    /// One comparison window per document, following whichever snapshot is selected in the history.
    func showSnapshotDiff(_ history: SnapshotHistoryModel) {
        windows.show(
            id: history.diffWindowID,
            title: history.selected.map { diffTitle($0.createdUtc) } ?? localization.get("dialog.compare"),
            size: NSSize(width: 980, height: 720),
            minSize: NSSize(width: 640, height: 400),
            model: self
        ) {
            SnapshotDiffView(history: history)
        }
    }

    /// The local content against what another program left on disk: local on the removed side.
    func compareWithDisk(_ document: DocumentSession) {
        guard let path = document.filePath else {
            return
        }

        do {
            let decoded = try FileCodec().read(path: path)
            showDiff(old: document.text, new: decoded.text, date: Date())
        } catch {
            showError(localization.get("error.open"), detail: error.localizedDescription)
        }
    }

    private func diffTitle(_ date: Date) -> String {
        "\(localization.get("dialog.compare")) — \(SnapshotPresentation.timestamp(date))"
    }
}
