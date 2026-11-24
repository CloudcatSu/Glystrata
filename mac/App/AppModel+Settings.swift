import AppKit
import GlystrataCore

extension AppModel {
    func openSettings() {
        // "Follow the system" is read from the app, not the window: the window's own appearance
        // is already overridden by the theme that is currently chosen.
        let systemKind: ThemeKind = NSApp.effectiveAppearance.bestMatch(from: [.darkAqua, .aqua]) == .darkAqua ? .dark : .light
        windows.show(
            id: "settings",
            title: localization.get("settings.title"),
            size: NSSize(width: 720, height: 660),
            minSize: NSSize(width: 620, height: 520),
            model: self
        ) {
            SettingsView(settings: settings, systemKind: systemKind)
        }
    }

    /// Takes the settings the Preferences window validated, applies them everywhere and queues the save.
    func applySettings(_ newSettings: AppSettings) {
        var new = newSettings
        new.normalize()
        let hideSnapshotFilesChanged = new.hideSnapshotFiles != settings.hideSnapshotFiles
        // The groups are redrawn from their own colour, so remapping here is all it takes for the
        // sidebar and tabs to show the edited swatches.
        groups.remapColors(oldPalette: settings.groupColors, newPalette: new.groupColors)
        settings = new
        let hide = new.hideSnapshotFiles
        let store = snapshots.store
        Task { await store.setHideSidecarFiles(hide) }
        if hideSnapshotFilesChanged {
            sweepSnapshotVisibility()
        }
        localization.apply(new.language)
        // The theme itself follows `settings.theme` in the main window; a new palette under an
        // unchanged theme would otherwise never reach the text views.
        textStores.apply(editorStyle)
        applyDockIcon()
        scheduleSessionSave()
    }

    /// The icon in the app's own file is the flat one, and macOS gives an app no way to choose
    /// between icons; what a running app can do is hand the Dock a picture. So the glass finish
    /// shows only while the app runs, and only there.
    func applyDockIcon() {
        guard settings.useGlassIcon else {
            NSApp.applicationIconImage = nil
            return
        }
        NSApp.applicationIconImage = NSImage(named: Self.systemIconsAreDark ? "IconGlassDark" : "IconGlassLight")
    }

    /// Whether the icons around ours are the dark ones. No API says; this is the preference the
    /// system keeps its icon style in, and without it the icons are the light ones.
    private static var systemIconsAreDark: Bool {
        let style = UserDefaults.standard.string(forKey: "AppleIconAppearanceTheme") ?? ""
        if style.hasSuffix("Automatic") {
            return NSApp.effectiveAppearance.bestMatch(from: [.darkAqua, .aqua]) == .darkAqua
        }
        return style.hasSuffix("Dark")
    }
}
