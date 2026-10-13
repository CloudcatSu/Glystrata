import AppKit
import GlystrataCore

/// Shared pieces for the windows other than the main one. Each window's own entry points live
/// in its own file: AppModel+Settings, +FindReplace, +Snapshots, +Reader.
extension AppModel {
    func showAbout() {
        let version = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? ""
        let alert = NSAlert()
        alert.messageText = localization.get("help.about")
        alert.informativeText = "Glystrata \(version)\n\(localization.get("about.message"))"
        alert.addButton(withTitle: localization.get("dialog.ok"))
        alert.runModal()
    }

    /// Says in the status bar that a window has not been built for the Mac yet.
    func notYetAvailable(_ key: String) {
        statusOverride = "\(localization.get(key)) — Mac 版尚未完成"
    }

    #if DEBUG
    /// Development aid: `-GlystrataOpenWindow settings|find|replace|snapshots|reader|guide` opens
    /// that window at launch, so it can be photographed with `-GlystrataSnapshotPath`.
    /// `-GlystrataWindowHeight <points>` makes it that tall, to fit a long page in one picture.
    func openWindowForDebugging(_ name: String) {
        defer {
            let height = UserDefaults.standard.double(forKey: "GlystrataWindowHeight")
            if height > 0 {
                for window in windows.all {
                    window.setContentSize(NSSize(width: window.contentLayoutRect.width, height: height))
                }
            }
        }
        switch name {
        case "settings":
            openSettings()
        case "find":
            openFindReplace(focusReplace: false)
        case "replace":
            openFindReplace(focusReplace: true)
        case "snapshots":
            if let view = activeView {
                openSnapshotHistory(for: view)
            }
        case "reader":
            if let view = activeView {
                toggleReader(for: view)
            }
        case "guide":
            showMarkdownGuide()
        default:
            break
        }
    }
    #endif
}
