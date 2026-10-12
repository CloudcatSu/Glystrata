import Foundation

public enum AppCommandID: Sendable, CaseIterable {
    case fileNew
    case fileOpen
    case fileSave
    case fileSaveAs
    case fileSnapshotHistory
    case fileCreateSnapshot
    case settingsOpen
    case editFindReplace
    case editReplace
    case viewSplitHorizontal
    case viewSplitVertical
    case viewClosePane
    case viewResetLayout
    case viewNewView
    case viewToggleReader
    case viewCloseTab
    case groupNew
    case groupOpenFile
    case helpMarkdownGuide
    case helpAbout
}

public struct KeyShortcut: Hashable, Sendable {
    public enum Modifier: Hashable, Sendable {
        case command
        case shift
        case option
        case control
    }

    public var key: Character
    public var modifiers: Set<Modifier>

    public init(_ key: Character, _ modifiers: Set<Modifier> = [.command]) {
        self.key = key
        self.modifiers = modifiers
    }

    /// As macOS menus print it, e.g. "⌥⌘S".
    public var displayText: String {
        let symbols: [(Modifier, String)] = [(.control, "⌃"), (.option, "⌥"), (.shift, "⇧"), (.command, "⌘")]
        return symbols.filter { modifiers.contains($0.0) }.map(\.1).joined() + String(key).uppercased()
    }
}

/// One row of the command table: what the menu says and what key runs it. Keeping both on a single
/// line is the point — a menu label and a key handler kept in two separate lists can drift apart
/// without anything noticing.
public struct AppCommandDefinition: Sendable {
    public var id: AppCommandID

    /// Menu label, or nil for a command with no menu entry of its own text.
    public var localizationKey: String?
    public var shortcut: KeyShortcut?

    public init(_ id: AppCommandID, _ localizationKey: String?, _ shortcut: KeyShortcut? = nil) {
        self.id = id
        self.localizationKey = localizationKey
        self.shortcut = shortcut
    }
}

public enum AppCommands {
    // Undo, redo and quit are absent on purpose: macOS supplies those menu items and their keys.
    public static let all: [AppCommandDefinition] = [
        .init(.fileNew, "file.new", KeyShortcut("n")),
        .init(.fileOpen, "file.open", KeyShortcut("o")),
        .init(.fileSave, "file.save", KeyShortcut("s")),
        .init(.fileSaveAs, "file.saveAs", KeyShortcut("s", [.command, .shift])),
        .init(.fileSnapshotHistory, "file.snapshotHistory", KeyShortcut("h", [.command, .shift])),
        .init(.fileCreateSnapshot, "file.createSnapshot", KeyShortcut("s", [.command, .option])),
        .init(.settingsOpen, "settings.open", KeyShortcut(",")),

        .init(.editFindReplace, "edit.findReplace", KeyShortcut("f")),
        // ⌘H belongs to the system ("Hide"); ⌥⌘F is where Mac apps put find-and-replace.
        .init(.editReplace, nil, KeyShortcut("f", [.command, .option])),

        .init(.viewSplitHorizontal, "view.splitHorizontal", KeyShortcut("\\")),
        .init(.viewSplitVertical, "view.splitVertical", KeyShortcut("\\", [.command, .option])),
        .init(.viewClosePane, "view.closePane", KeyShortcut("w", [.command, .shift])),
        .init(.viewResetLayout, "view.resetLayout", KeyShortcut("1", [.command, .option])),
        .init(.viewNewView, "view.newView", KeyShortcut("n", [.command, .option])),
        .init(.viewToggleReader, nil, KeyShortcut("r", [.command, .shift])),
        .init(.viewCloseTab, nil, KeyShortcut("w")),

        // ⇧⌘G is "find previous" in Mac text views; ⇧⌘N follows Finder's "new folder".
        .init(.groupNew, "group.new", KeyShortcut("n", [.command, .shift])),
        .init(.groupOpenFile, "group.openFile"),

        .init(.helpMarkdownGuide, "help.markdownGuide"),
        .init(.helpAbout, "help.about")
    ]

    public static func definition(_ id: AppCommandID) -> AppCommandDefinition {
        all.first { $0.id == id } ?? AppCommandDefinition(id, nil)
    }

    /// Pairs of commands that claim the same key.
    public static func conflicts(in definitions: [AppCommandDefinition] = all) -> [(AppCommandID, AppCommandID)] {
        var seen: [KeyShortcut: AppCommandID] = [:]
        var result: [(AppCommandID, AppCommandID)] = []
        for definition in definitions {
            guard let shortcut = definition.shortcut else {
                continue
            }
            if let existing = seen[shortcut] {
                result.append((existing, definition.id))
            } else {
                seen[shortcut] = definition.id
            }
        }
        return result
    }
}
