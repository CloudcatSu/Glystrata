import AppKit
import GlystrataCore
import Observation

/// What one document's snapshot history window and its compare window share, so that picking a
/// snapshot in either one moves the other.
@MainActor
@Observable
final class SnapshotHistoryModel {
    let view: DocumentViewState
    private(set) var snapshots: [SnapshotInfo] = []
    var selectedID: UUID?
    /// Raised whenever Compare is pressed, so an open compare window re-reads the document's current text.
    private(set) var compareRequests = 0
    /// What the note fields currently show, by snapshot. A missing entry means untouched.
    var drafts: [UUID: String] = [:]
    /// False until the first read finishes, so the list does not flash "no snapshots" while loading.
    private(set) var isLoaded = false
    /// A failed read leaves the list empty and every button off, as in the Windows build.
    private(set) var loadFailed = false

    @ObservationIgnored private unowned let app: AppModel

    init(app: AppModel, view: DocumentViewState) {
        self.app = app
        self.view = view
    }

    var selected: SnapshotInfo? {
        snapshots.first { $0.id == selectedID }
    }

    var windowID: String { "snapshots-\(view.document.sessionID.uuidString)" }
    var diffWindowID: String { "snapshotDiff-\(view.document.sessionID.uuidString)" }

    func refresh() async {
        guard let path = view.document.filePath else {
            return
        }

        do {
            snapshots = try await app.snapshots.list(path)
            loadFailed = false
        } catch {
            snapshots = []
            loadFailed = true
            app.showError(app.localization.get("snapshot.title"), detail: error.localizedDescription)
        }
        drafts = [:]
        selectedID = snapshots.first?.id
        isLoaded = true
    }

    func draft(for snapshot: SnapshotInfo) -> String {
        drafts[snapshot.id] ?? SnapshotPresentation.notePreview(snapshot.note)
    }

    func saveNote(of snapshotID: UUID) async {
        guard let path = view.document.filePath,
              let snapshot = snapshots.first(where: { $0.id == snapshotID }),
              let fieldText = drafts[snapshotID],
              let note = SnapshotPresentation.editedNote(stored: snapshot.note, fieldText: fieldText),
              note != snapshot.note else {
            return
        }

        do {
            if try await app.snapshots.updateNote(path, snapshotID: snapshotID, note: note),
               let index = snapshots.firstIndex(where: { $0.id == snapshotID }) {
                // The row now carries what is on disk, so it no longer counts as edited.
                snapshots[index].note = note
            }
        } catch {
            app.showError(app.localization.get("snapshot.title"), detail: error.localizedDescription)
        }
    }

    func saveAllNotes() async {
        for snapshot in snapshots {
            await saveNote(of: snapshot.id)
        }
    }

    func createSnapshot() async {
        let title = app.localization.get("snapshot.title")
        do {
            let created = try await app.snapshots.create(for: view.document, maxSnapshots: app.settings.maxSnapshotsPerFile)
            if created == nil {
                app.showError(title, detail: app.localization.get("snapshot.unchanged"))
                return
            }
            await refresh()
        } catch {
            app.showError(title, detail: error.localizedDescription)
        }
    }

    func compareSelected() {
        guard selected != nil else {
            return
        }

        compareRequests += 1
        app.showSnapshotDiff(self)
    }

    func restoreSelected() async {
        guard let snapshot = selected else {
            return
        }

        let localization = app.localization
        let alert = NSAlert()
        alert.messageText = localization.get("snapshot.restore")
        alert.informativeText = localization.format("snapshot.restoreTo", SnapshotPresentation.timestamp(snapshot.createdUtc))
        alert.addButton(withTitle: localization.get("snapshot.replaceCurrent"))
        alert.addButton(withTitle: localization.get("snapshot.saveAsNew"))
        alert.addButton(withTitle: localization.get("dialog.cancel")).keyEquivalent = "\u{1b}"
        switch alert.runModal() {
        case .alertFirstButtonReturn:
            await app.restoreSnapshot(snapshot, mode: .replaceCurrent, in: view)
        case .alertSecondButtonReturn:
            await app.restoreSnapshot(snapshot, mode: .saveAsNewFile, in: view)
        default:
            break
        }
    }

    func deleteSelected() async {
        guard let path = view.document.filePath, let snapshot = selected else {
            return
        }

        let localization = app.localization
        guard app.confirm(
            title: localization.get("snapshot.delete"),
            message: localization.format("snapshot.deleteConfirm", SnapshotPresentation.timestamp(snapshot.createdUtc)),
            confirmTitle: localization.get("dialog.delete")) else {
            return
        }

        do {
            try await app.snapshots.delete(path, snapshotID: snapshot.id)
            await refresh()
        } catch {
            app.showError(localization.get("snapshot.title"), detail: error.localizedDescription)
        }
    }

    func deleteAll() async {
        guard let path = view.document.filePath else {
            return
        }

        let localization = app.localization
        guard app.confirm(
            title: localization.get("snapshot.deleteAll"),
            message: localization.get("snapshot.deleteAllConfirm"),
            confirmTitle: localization.get("dialog.delete")) else {
            return
        }

        do {
            try await app.snapshots.deleteAll(path)
            await refresh()
        } catch {
            app.showError(localization.get("snapshot.title"), detail: error.localizedDescription)
        }
    }
}
