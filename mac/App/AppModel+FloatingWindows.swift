import AppKit
import GlystrataCore

/// Tabs moved out of the main window into windows of their own, and back again.
extension AppModel {
    func isFloating(_ view: DocumentViewState) -> Bool {
        floatingPanes.contains(view.paneID)
    }

    /// The main window, found by being none of the others. Nil while it is closed.
    var mainWindow: NSWindow? {
        NSApp.windows.first { window in
            (window.isVisible || window.isMiniaturized)
                && !(window is NSPanel)
                && window.styleMask.contains(.titled)
                && !windows.all.contains { $0 === window }
                && !floatingWindows.contains(window)
        }
    }

    /// The window a tab is shown in, which is what a window opened for it goes beside.
    func window(showing view: DocumentViewState) -> NSWindow? {
        floatingWindows.window(for: view.paneID) ?? mainWindow
    }

    func openInFloatingWindow(_ view: DocumentViewState) {
        if let window = floatingWindows.window(for: view.paneID) {
            window.makeKeyAndOrderFront(nil)
            return
        }

        let anchor = mainWindow
        let oldPane = view.paneID
        let paneID = UUID()
        floatingPanes.insert(paneID)
        view.paneID = paneID
        view.tabOrder = 0
        if selectedViewIDs[oldPane] == view.viewID {
            selectedViewIDs[oldPane] = nil
        }
        // The same rule as closing a tab: a pane left with nothing in it goes.
        if !documents.views.contains(where: { $0.paneID == oldPane }) {
            removePane(oldPane)
        }
        floatingWindows.open(paneID: paneID, frame: nil, beside: anchor, makeKey: true, model: self)
        select(view)
    }

    /// Puts a floating window's tab back in the main window's active pane, closing the window
    /// when it was the last tab there.
    func moveToMainWindow(_ view: DocumentViewState) {
        guard isFloating(view) else {
            return
        }

        let floatingPane = view.paneID
        let target = paneForNewTabs
        view.paneID = target
        view.tabOrder = (documents.views.filter { $0.paneID == target && $0 !== view }.map(\.tabOrder).max() ?? -1) + 1
        if selectedViewIDs[floatingPane] == view.viewID {
            selectedViewIDs[floatingPane] = nil
        }
        if !documents.views.contains(where: { $0.paneID == floatingPane }) {
            removePane(floatingPane)
        }
        // Coming back into a group the main window is not showing would make it vanish instead.
        if !isVisible(view) {
            selectGroup(view.sourceGroupID)
        }
        select(view)
        showMainWindow()
    }

    /// Brings the main window forward, reopening it if it was closed, so a tab just put there is seen.
    func showMainWindow() {
        if let mainWindow {
            mainWindow.makeKeyAndOrderFront(nil)
        } else {
            reopenMainWindow?()
        }
    }

    /// What the window's close button does: closes each of its tabs as the tab's own close button
    /// would, and stops at the first one the user keeps.
    func closeFloatingWindow(_ paneID: UUID) async {
        for view in documents.views.filter({ $0.paneID == paneID }) {
            await close(view)
            if documents.findView(view.viewID) != nil {
                return
            }
        }
        if floatingPanes.contains(paneID) {
            removeFloatingPane(paneID)
        }
    }

    /// Closes a floating window without asking anything. Tabs still in it go back to the main
    /// window, as a closed pane's tabs go to the pane that remains.
    func removeFloatingPane(_ paneID: UUID) {
        let target = paneForNewTabs
        var order = (documents.views.filter { $0.paneID == target }.map(\.tabOrder).max() ?? -1) + 1
        for view in documents.views.filter({ $0.paneID == paneID }).sorted(by: { $0.tabOrder < $1.tabOrder }) {
            view.paneID = target
            view.tabOrder = order
            order += 1
        }
        floatingPanes.remove(paneID)
        selectedViewIDs[paneID] = nil
        selections[paneID] = nil
        floatingWindows.close(paneID)
        if activePaneID == paneID {
            activePaneID = target
        }
        scheduleSessionSave()
    }

    /// Keeps commands acting on the window in front: a floating window's pane while it is key, and
    /// the main window's own active pane once that is key again.
    func watchWindowFocus() {
        NotificationCenter.default.addObserver(forName: NSWindow.didBecomeKeyNotification, object: nil, queue: .main) { [weak self] notification in
            let window = notification.object as? NSWindow
            Task { @MainActor in
                guard let self, let window else {
                    return
                }
                if let paneID = self.floatingWindows.paneID(of: window) {
                    self.activate(paneID: paneID)
                } else if window === self.mainWindow {
                    self.activate(paneID: self.paneForNewTabs)
                }
            }
        }
    }

    // MARK: - Session

    func floatingWindowStates() -> [FloatingWindowState] {
        let top = NSScreen.screens.first?.frame.maxY ?? 0
        return floatingWindows.paneIDs.compactMap { paneID in
            guard let frame = floatingWindows.savedFrame(for: paneID) else {
                return nil
            }
            // Untitled tabs are not kept between runs, so a window holding only those is not either.
            let viewIDs = views(in: paneID).filter { $0.document.filePath != nil }.map(\.viewID)
            guard !viewIDs.isEmpty else {
                return nil
            }
            return FloatingWindowState(x: frame.minX, y: top - frame.maxY, width: frame.width, height: frame.height, viewIds: viewIDs)
        }
    }

    /// Reopens the floating windows of the last session. `restoredIDs` maps the view ids saved
    /// then to the views just reopened.
    func restoreFloatingWindows(_ states: [FloatingWindowState], restoredIDs: [UUID: UUID]) {
        var claimed = Set<UUID>()
        for state in states {
            let views = state.viewIds
                .compactMap { restoredIDs[$0] }
                .filter { claimed.insert($0).inserted }
                .compactMap { documents.findView($0) }
            guard !views.isEmpty else {
                continue
            }

            let paneID = UUID()
            floatingPanes.insert(paneID)
            for (order, view) in views.enumerated() {
                view.paneID = paneID
                view.tabOrder = order
            }
            floatingWindows.open(paneID: paneID, frame: Self.onScreenFrame(state), beside: nil, makeKey: false, model: self)
        }
    }

    /// The saved frame in this Mac's coordinates, or nil when it would not be meaningfully on any
    /// screen now (a display since unplugged, or a frame written on another computer).
    private static func onScreenFrame(_ state: FloatingWindowState) -> NSRect? {
        guard state.width >= 200, state.height >= 150, let top = NSScreen.screens.first?.frame.maxY else {
            return nil
        }

        let frame = NSRect(x: state.x, y: top - state.y - state.height, width: state.width, height: state.height)
        let visible = NSScreen.screens.contains { screen in
            let overlap = screen.visibleFrame.intersection(frame)
            return overlap.width >= 120 && overlap.height >= 80
        }
        return visible ? frame : nil
    }
}
