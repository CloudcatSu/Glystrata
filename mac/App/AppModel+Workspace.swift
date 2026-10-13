import AppKit
import GlystrataCore

/// What happens around the documents without the user asking: restoring the last session, saving
/// it again as it changes, auto-save, timed snapshots, and noticing files changed by other programs.
extension AppModel {
    // MARK: - Startup

    func loadWorkspace() async {
        (await stateStore.loadGroups()).apply(to: groups)
        if groups.groups.isEmpty {
            groups.createGroup(localization.get("sidebar.groups"))
        }
        recentFiles.load(from: await stateStore.loadRecentFiles())

        let session = await stateStore.loadSession()
        if let restored = session.layout() {
            layout = restored
        }
        let firstPane = PaneLayoutOperations.firstPaneID(layout)
        // Anything opened in the moment before the session arrived belongs to a pane that no longer exists.
        for view in documents.views where !PaneLayoutOperations.containsPane(layout, paneID: view.paneID) {
            view.paneID = firstPane
        }
        activePaneID = firstPane
        restoreViews(session)
        pruneEmptyPanes()

        isWorkspaceLoaded = true
        startHousekeeping()
        let waiting = pathsWaitingForWorkspace
        pathsWaitingForWorkspace = []
        openExternal(paths: waiting)
        sweepSnapshotVisibility()
    }

    private func restoreViews(_ session: SessionState) {
        let paneIDs = Set(PaneLayoutOperations.paneIDs(layout))
        var seenPaths = Set<String>()
        var restoredIDs: [UUID: UUID] = [:]
        for saved in session.views {
            guard !saved.path.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
                  FileManager.default.fileExists(atPath: saved.path) else {
                continue
            }

            let paneID = paneIDs.contains(saved.paneId) ? saved.paneId : PaneLayoutOperations.firstPaneID(layout)
            let isSecondView = !seenPaths.insert(DocumentManager.canonicalizePath(saved.path)).inserted
            // A file that has since become unreadable is skipped; the rest of the session still restores.
            guard let view = try? documents.openView(saved.path, paneID: paneID, sourceGroupID: saved.sourceGroupId, newView: isSecondView) else {
                continue
            }

            view.caretOffset = saved.caretOffset
            view.horizontalOffset = saved.horizontalOffset
            view.verticalOffset = saved.verticalOffset
            attach(view.document)
            restoredIDs[saved.viewId] = view.viewID
            // Restored documents never pass through `open`, so without this the files actually open
            // would be the ones missing from the recent list.
            recentFiles.addIfMissing(saved.path, lastOpenedUtc: Date())
        }

        if let activeID = session.activeViewId, let restored = restoredIDs[activeID], let view = documents.findView(restored) {
            selectedViewIDs[view.paneID] = view.viewID
            activePaneID = view.paneID
        }
    }

    private func pruneEmptyPanes() {
        while case .split = layout,
              let empty = PaneLayoutOperations.paneIDs(layout).first(where: { paneID in !documents.views.contains { $0.paneID == paneID } }) {
            let before = layout
            removePane(empty)
            if layout == before {
                return
            }
        }
    }

    /// Files handed over by Finder join the group being looked at. The Windows build stops there
    /// when a second copy of the app passes its arguments along; here they also go on the group's
    /// list, the same as files from the open dialog.
    func openExternal(paths: [String]) {
        guard isWorkspaceLoaded else {
            pathsWaitingForWorkspace += paths
            return
        }

        var seen = Set<String>()
        let valid = paths
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty && seen.insert(DocumentManager.canonicalizePath($0)).inserted && FileManager.default.fileExists(atPath: $0) }
        guard !valid.isEmpty else {
            return
        }

        openIntoCurrentGroup(paths: valid)
    }

    /// The group new arrivals belong to: the selected one, else the active tab's, else the first.
    var currentGroupID: UUID? {
        if let selectedGroupID, groups.find(selectedGroupID) != nil {
            return selectedGroupID
        }
        if let groupID = activeView?.sourceGroupID, groups.find(groupID) != nil {
            return groupID
        }
        return groups.groups.first?.id
    }

    // MARK: - Session

    func scheduleSessionSave() {
        guard isWorkspaceLoaded, !isQuitting else {
            return
        }

        sessionSaveTask?.cancel()
        sessionSaveTask = Task { [weak self] in
            try? await Task.sleep(for: .milliseconds(700))
            guard !Task.isCancelled, let self else {
                return
            }
            await self.saveWorkspace()
        }
    }

    func saveWorkspace() async {
        let write = workspaceWriter()
        await Task.detached(priority: .utility) { write() }.value
    }

    /// Reads everything worth keeping here on the main actor and returns the writing as a separate
    /// step. A failed write of working state must not take the editor down, so it reports nothing.
    private func workspaceWriter() -> @Sendable () -> Void {
        let settings = settings
        let groupsState = GroupsState.from(groups.groups)
        let session = makeSessionState()
        let recent = recentFiles.toState()
        let store = stateStore
        return { store.saveEverything(settings: settings, groups: groupsState, session: session, recentFiles: recent) }
    }

    private func makeSessionState() -> SessionState {
        var state = SessionState()
        state.setLayout(layout)
        state.activeViewId = activeView?.viewID
        let paneOrder = PaneLayoutOperations.paneIDs(layout)
        state.views = documents.views
            .filter { $0.document.filePath != nil }
            .sorted {
                let left = paneOrder.firstIndex(of: $0.paneID) ?? 0
                let right = paneOrder.firstIndex(of: $1.paneID) ?? 0
                return left == right ? $0.tabOrder < $1.tabOrder : left < right
            }
            .map { view in
                var saved = SessionViewState(
                    viewId: view.viewID,
                    path: view.document.filePath ?? "",
                    sourceGroupId: view.sourceGroupID,
                    paneId: view.paneID)
                saved.caretOffset = view.caretOffset
                saved.horizontalOffset = view.horizontalOffset
                saved.verticalOffset = view.verticalOffset
                return saved
            }
        return state
    }

    // MARK: - Documents coming and going

    func attach(_ document: DocumentSession) {
        if let path = document.filePath {
            SnapshotSidecarStore.applyVisibility(path, hidden: settings.hideSnapshotFiles)
        }
    }

    func detach(_ document: DocumentSession) {
        let id = document.sessionID
        autoSaveTasks.removeValue(forKey: id)?.cancel()
        autoSaveFailures.remove(id)
        lastChanged[id] = nil
        knownExternalFingerprints[id] = nil
        savesInFlight[id] = nil
    }

    func sweepSnapshotVisibility() {
        let hidden = settings.hideSnapshotFiles
        var seen = Set<String>()
        let paths = (groups.groups.flatMap(\.items).filter { $0.kind == .file }.map(\.path) + documents.documents.compactMap(\.filePath))
            .filter { seen.insert(DocumentManager.canonicalizePath($0)).inserted }
        Task.detached(priority: .utility) {
            for path in paths {
                SnapshotSidecarStore.migrateIfNeeded(path)
                SnapshotSidecarStore.applyVisibility(path, hidden: hidden)
            }
        }
    }

    // MARK: - Saving

    /// Every save goes through here so the external-change check can tell the app's own writes
    /// from someone else's.
    func performSave(_ document: DocumentSession, to newPath: String? = nil) async -> SaveResult {
        let id = document.sessionID
        savesInFlight[id, default: 0] += 1
        let result: SaveResult
        if let newPath {
            result = await documents.saveAs(document, to: newPath)
        } else {
            result = await documents.save(document)
        }
        if let count = savesInFlight[id] {
            savesInFlight[id] = count > 1 ? count - 1 : nil
        }
        if result.success {
            knownExternalFingerprints[id] = nil
            autoSaveFailures.remove(id)
            missingFiles.remove(id)
        }
        return result
    }

    func documentEdited(_ document: DocumentSession) {
        guard !isQuitting else {
            return
        }

        lastChanged[document.sessionID] = Date()
        if statusOverride != nil {
            statusOverride = nil
        }
        scheduleAutoSave(document)
        scheduleSessionSave()
    }

    private func scheduleAutoSave(_ document: DocumentSession) {
        guard document.filePath != nil else {
            return
        }

        let id = document.sessionID
        autoSaveTasks[id]?.cancel()
        autoSaveTasks[id] = Task { [weak self] in
            try? await Task.sleep(for: .seconds(1))
            guard !Task.isCancelled, let self else {
                return
            }
            self.autoSaveTasks[id] = nil
            guard document.isModified, document.filePath != nil else {
                return
            }
            // Not underneath a question: its answer may be to throw these edits away.
            guard NSApp.modalWindow == nil else {
                self.scheduleAutoSave(document)
                return
            }
            // The timer that looks for outside changes may not have come round yet, and writing
            // now would replace the other program's version without anyone having seen it.
            if let choice = self.resolveExternalChange(of: document), choice != .keepLocal {
                return
            }

            let result = await self.performSave(document)
            if result.success {
                if self.activeView?.document === document {
                    self.statusOverride = self.localization.get("status.autoSaved")
                }
                self.scheduleSessionSave()
            } else if self.autoSaveFailures.insert(id).inserted {
                // Said once, not after every keystroke; the tab keeps its unsaved mark meanwhile.
                self.showError(self.localization.get("error.save"), detail: result.errorMessage ?? "")
            }
        }
    }

    // MARK: - Timers

    private func startHousekeeping() {
        let snapshotTimer = Timer.scheduledTimer(withTimeInterval: 30, repeats: true) { [weak self] _ in
            Task { @MainActor in await self?.takeDueSnapshots() }
        }
        snapshotTimer.tolerance = 5
        let externalTimer = Timer.scheduledTimer(withTimeInterval: 2, repeats: true) { [weak self] _ in
            Task { @MainActor in self?.checkExternalChanges() }
        }
        externalTimer.tolerance = 0.5
        housekeepingTimers = [snapshotTimer, externalTimer]

        NotificationCenter.default.addObserver(forName: NSApplication.didBecomeActiveNotification, object: nil, queue: .main) { [weak self] _ in
            Task { @MainActor in self?.checkExternalChanges() }
        }
    }

    private func takeDueSnapshots() async {
        let now = Date()
        let interval = TimeInterval(settings.snapshotIntervalMinutes * 60)
        for document in documents.documents {
            guard document.filePath != nil,
                  let changedAt = lastChanged[document.sessionID],
                  now.timeIntervalSince(changedAt) >= interval,
                  document.lastSnapshotText.map({ !$0.utf16.elementsEqual(document.text.utf16) }) ?? true else {
                continue
            }
            // A snapshot that cannot be written must not interrupt the editing.
            _ = try? await snapshots.create(for: document, maxSnapshots: settings.maxSnapshotsPerFile)
        }
    }

    // MARK: - Files changed by other programs

    /// Compares each open file with what the app last read or wrote. A timer rather than a file
    /// watcher: programs that save by replacing the file leave a watcher holding the old one.
    func checkExternalChanges() {
        guard isWorkspaceLoaded, !isQuitting, !isCheckingExternalChanges, NSApp.modalWindow == nil else {
            return
        }

        isCheckingExternalChanges = true
        defer { isCheckingExternalChanges = false }

        for document in documents.documents {
            resolveExternalChange(of: document)
        }

        // A missing file gets a note in the status bar rather than a question: there is nothing
        // to choose, since the next save puts it back.
        let missing = Set(documents.documents.filter { document in
            document.filePath.map { !FileManager.default.fileExists(atPath: $0) } ?? false
        }.map(\.sessionID))
        if missing != missingFiles {
            missingFiles = missing
        }
    }

    /// Asks what to do about `document`'s file having changed on disk, and does it. Nil when the
    /// file is as the app left it, or the user has already answered for this version of it.
    @discardableResult
    private func resolveExternalChange(of document: DocumentSession) -> ExternalChangeChoice? {
        let id = document.sessionID
        guard let path = document.filePath,
              savesInFlight[id] == nil,
              let current = try? FileFingerprint.read(path: path),
              current != document.lastSavedFingerprint,
              current != knownExternalFingerprints[id] else {
            return nil
        }

        knownExternalFingerprints[id] = current
        if let view = documents.views.first(where: { $0.document === document && isVisible($0) }) {
            select(view)
        }
        var choice = askAboutExternalChange(of: path)
        // Reloading throws the local edits away; that needs its own yes.
        if choice == .reload, document.isModified, !confirm(
            title: localization.get("dialog.externalChangeTitle"),
            message: localization.get("dialog.reloadDiscardMessage"),
            confirmTitle: localization.get("dialog.reload")) {
            choice = .keepLocal
        }
        switch choice {
        case .reload:
            reload(document)
        case .compare:
            compareWithDisk(document)
        case .keepLocal:
            break
        }
        return choice
    }

    private enum ExternalChangeChoice {
        case reload
        case compare
        case keepLocal
    }

    private func askAboutExternalChange(of path: String) -> ExternalChangeChoice {
        let alert = NSAlert()
        alert.messageText = localization.get("dialog.externalChangeTitle")
        alert.informativeText = "\((path as NSString).lastPathComponent)\n\n\(localization.get("dialog.externalChangeMessage"))"
        alert.addButton(withTitle: localization.get("dialog.reload"))
        alert.addButton(withTitle: localization.get("dialog.compare"))
        // Escape and Return-less dismissal both land here, so doing nothing is the safe default.
        alert.addButton(withTitle: localization.get("dialog.keepLocal")).keyEquivalent = "\u{1b}"
        switch alert.runModal() {
        case .alertFirstButtonReturn:
            return .reload
        case .alertSecondButtonReturn:
            return .compare
        default:
            return .keepLocal
        }
    }

    func reload(_ document: DocumentSession) {
        guard let path = document.filePath else {
            return
        }

        do {
            let decoded = try FileCodec().read(path: path)
            document.applyLoadedContent(decoded.text, encoding: decoded.encoding, lineEnding: decoded.lineEnding, fingerprint: decoded.fingerprint)
            lastChanged[document.sessionID] = nil
            autoSaveTasks.removeValue(forKey: document.sessionID)?.cancel()
            textStores.sync(document)
        } catch {
            showError(localization.get("error.open"), detail: error.localizedDescription)
        }
    }

    // MARK: - Quitting

    var hasUnsavedDocuments: Bool {
        documents.documents.contains(where: \.isModified)
    }

    /// Asks about each unsaved document in turn. False means the user backed out of quitting.
    func resolveUnsavedDocuments() async -> Bool {
        for document in documents.documents where document.isModified {
            guard let view = documents.views.first(where: { $0.document === document }) else {
                continue
            }
            // Bring the document in question to the front, so the question has a subject.
            if !isVisible(view) {
                selectedGroupID = nil
            }
            select(view)

            switch confirmUnsavedChanges() {
            case .save:
                guard await save(view), !document.isModified else {
                    return false
                }
            case .discard:
                break
            case .cancel:
                return false
            }
        }
        return true
    }

    /// Stops the background work and writes the session. Does not suspend: by now the app is on
    /// its way out and nothing may be left waiting for a later turn of the run loop.
    func finishQuitting() {
        isQuitting = true
        sessionSaveTask?.cancel()
        autoSaveTasks.values.forEach { $0.cancel() }
        housekeepingTimers.forEach { $0.invalidate() }
        workspaceWriter()()
    }
}
