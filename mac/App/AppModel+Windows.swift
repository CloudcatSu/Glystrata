import AppKit
import GlystrataCore

/// The windows other than the main one. Each of these is an entry point the menus, toolbar and tab
/// menu already call; the windows themselves are built in the next stage.
extension AppModel {
    func openSettings() {
        notYetAvailable("settings.open")
    }

    func toggleReader(for view: DocumentViewState) {
        notYetAvailable("preview.reader")
    }

    func openSnapshotHistory(for view: DocumentViewState) {
        notYetAvailable("file.snapshotHistory")
    }

    func openFindReplace(focusReplace: Bool) {
        notYetAvailable("edit.findReplace")
    }

    func showMarkdownGuide() {
        notYetAvailable("help.markdownGuide")
    }

    func showAbout() {
        let version = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? ""
        let alert = NSAlert()
        alert.messageText = localization.get("help.about")
        alert.informativeText = "Glystrata \(version)\n\(localization.get("about.message"))"
        alert.addButton(withTitle: localization.get("dialog.ok"))
        alert.runModal()
    }

    private func notYetAvailable(_ key: String) {
        statusOverride = "\(localization.get(key)) — Mac 版尚未完成"
    }
}
