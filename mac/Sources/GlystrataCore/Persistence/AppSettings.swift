import Foundation

// Every enum here is stored in settings.json as its number, so the raw values are pinned: a file
// written by an older build has to keep meaning what it meant.

public enum AppLanguage: Int, Codable, Sendable, CaseIterable {
    case traditionalChinese = 0
    case english = 1
}

/// What the user picked in Preferences.
public enum ThemePreference: Int, Codable, Sendable, CaseIterable {
    case light = 0
    case dark = 1
    case system = 2
}

/// The theme actually in effect, once `ThemePreference.system` has been resolved against macOS.
public enum ThemeKind: Sendable {
    case light
    case dark
}

public enum CharacterCountMode: Int, Codable, Sendable, CaseIterable {
    case includeWhitespace = 0
    case excludeWhitespace = 1
    case excludeLineBreaks = 2
}

public struct EditorColorPalette: Codable, Equatable, Sendable {
    public var colors: [String: String]

    public init(colors: [String: String]) {
        self.colors = colors
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        colors = try container.decodeIfPresent([String: String].self, forKey: .colors) ?? Self.lightDefault.colors
    }

    public static let lightDefault = EditorColorPalette(colors: [
        "plain": "#1F2328",
        "heading": "#0969DA",
        "emphasis": "#8250DF",
        "link": "#0969DA",
        "list": "#57606A",
        "quote": "#6E7781",
        "code": "#953800",
        "yaml-key": "#0550AE",
        "yaml-value": "#0A3069",
        "yaml-comment": "#6E7781",
        "front-matter": "#8250DF"
    ])

    public static let darkDefault = EditorColorPalette(colors: [
        "plain": "#E6EDF3",
        "heading": "#79C0FF",
        "emphasis": "#D2A8FF",
        "link": "#79C0FF",
        "list": "#8B949E",
        "quote": "#8B949E",
        "code": "#FFA657",
        "yaml-key": "#A5D6FF",
        "yaml-value": "#C9D1D9",
        "yaml-comment": "#8B949E",
        "front-matter": "#D2A8FF"
    ])

    public func get(_ key: String, fallback: String) -> String {
        colors[key] ?? fallback
    }
}

public struct ReaderColorPalette: Codable, Equatable, Sendable {
    public var colors: [String: String]

    public init(colors: [String: String]) {
        self.colors = colors
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        colors = try container.decodeIfPresent([String: String].self, forKey: .colors) ?? Self.lightDefault.colors
    }

    // Kept monochrome by default (aside from links, which stay accent-colored so they still read as
    // clickable) so the reader looks the same out of the box; only an explicit choice in Settings
    // introduces color.
    public static let lightDefault = ReaderColorPalette(colors: [
        "text": "#24292F",
        "heading": "#24292F",
        "link": "#0969DA",
        "quote": "#68707C",
        "code": "#24292F"
    ])

    public static let darkDefault = ReaderColorPalette(colors: [
        "text": "#E6EAF0",
        "heading": "#E6EAF0",
        "link": "#79C0FF",
        "quote": "#A6ADB8",
        "code": "#E6EAF0"
    ])

    public func get(_ key: String, fallback: String) -> String {
        colors[key] ?? fallback
    }
}

public struct PreviewTypography: Codable, Equatable, Sendable {
    public var h1Size = 32.0
    public var h2Size = 26.0
    public var h3Size = 22.0
    public var h4Size = 18.0
    public var h5Size = 16.0
    public var h6Size = 14.0
    public var lineSpacing = 1.35
    public var paragraphSpacing = 10.0
    public var headingSpacing = 14.0

    public init() {}

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let defaults = PreviewTypography()
        h1Size = try container.decodeIfPresent(Double.self, forKey: .h1Size) ?? defaults.h1Size
        h2Size = try container.decodeIfPresent(Double.self, forKey: .h2Size) ?? defaults.h2Size
        h3Size = try container.decodeIfPresent(Double.self, forKey: .h3Size) ?? defaults.h3Size
        h4Size = try container.decodeIfPresent(Double.self, forKey: .h4Size) ?? defaults.h4Size
        h5Size = try container.decodeIfPresent(Double.self, forKey: .h5Size) ?? defaults.h5Size
        h6Size = try container.decodeIfPresent(Double.self, forKey: .h6Size) ?? defaults.h6Size
        lineSpacing = try container.decodeIfPresent(Double.self, forKey: .lineSpacing) ?? defaults.lineSpacing
        paragraphSpacing = try container.decodeIfPresent(Double.self, forKey: .paragraphSpacing) ?? defaults.paragraphSpacing
        headingSpacing = try container.decodeIfPresent(Double.self, forKey: .headingSpacing) ?? defaults.headingSpacing
    }
}

/// The colours offered when assigning a colour to a group. A group stores the hex value
/// itself, not a reference into this list, so editing an entry here does not repaint groups that
/// already picked the old value — that was the deliberate trade for never having a group point at a
/// swatch the user deleted.
public enum GroupColorPalette {
    public static let defaults = ["#3B6FF5", "#2DA44E", "#D97706", "#8250DF", "#CF222E"]
}

public struct AppSettings: Codable, Equatable, Sendable {
    public var schemaVersion = 1
    public var language = AppLanguage.traditionalChinese
    public var theme = ThemePreference.system
    public var lightEditorPalette = EditorColorPalette.lightDefault
    public var darkEditorPalette = EditorColorPalette.darkDefault
    public var lightReaderPalette = ReaderColorPalette.lightDefault
    public var darkReaderPalette = ReaderColorPalette.darkDefault
    public var previewTypography = PreviewTypography()
    public var snapshotIntervalMinutes = 5
    public var maxSnapshotsPerFile = 20
    public var characterCountMode = CharacterCountMode.includeWhitespace
    public var showFormattingToolbar = false
    public var hideSnapshotFiles = false
    public var groupColors = GroupColorPalette.defaults

    /// How much of a group's colour is mixed into the sidebar background when that group is
    /// selected. 0 leaves the selection uncoloured; the ceiling keeps the group name readable.
    public var groupSelectionTint = 0.14

    /// Mac only: draw the Dock icon with a glass finish while the app runs. The Windows build
    /// neither writes nor reads the key.
    public var useGlassIcon = false

    public init() {}

    /// Every field is optional on disk: a file from an older build simply lacks the newer ones, and
    /// an enum value this build does not know falls back to its default instead of failing the load.
    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let defaults = AppSettings()
        schemaVersion = try container.decodeIfPresent(Int.self, forKey: .schemaVersion) ?? defaults.schemaVersion
        language = try container.decodeIfPresent(Int.self, forKey: .language).flatMap { AppLanguage(rawValue: $0) } ?? defaults.language
        theme = try container.decodeIfPresent(Int.self, forKey: .theme).flatMap { ThemePreference(rawValue: $0) } ?? defaults.theme
        lightEditorPalette = try container.decodeIfPresent(EditorColorPalette.self, forKey: .lightEditorPalette) ?? defaults.lightEditorPalette
        darkEditorPalette = try container.decodeIfPresent(EditorColorPalette.self, forKey: .darkEditorPalette) ?? defaults.darkEditorPalette
        lightReaderPalette = try container.decodeIfPresent(ReaderColorPalette.self, forKey: .lightReaderPalette) ?? defaults.lightReaderPalette
        darkReaderPalette = try container.decodeIfPresent(ReaderColorPalette.self, forKey: .darkReaderPalette) ?? defaults.darkReaderPalette
        previewTypography = try container.decodeIfPresent(PreviewTypography.self, forKey: .previewTypography) ?? defaults.previewTypography
        snapshotIntervalMinutes = try container.decodeIfPresent(Int.self, forKey: .snapshotIntervalMinutes) ?? defaults.snapshotIntervalMinutes
        maxSnapshotsPerFile = try container.decodeIfPresent(Int.self, forKey: .maxSnapshotsPerFile) ?? defaults.maxSnapshotsPerFile
        characterCountMode = try container.decodeIfPresent(Int.self, forKey: .characterCountMode).flatMap { CharacterCountMode(rawValue: $0) } ?? defaults.characterCountMode
        showFormattingToolbar = try container.decodeIfPresent(Bool.self, forKey: .showFormattingToolbar) ?? defaults.showFormattingToolbar
        hideSnapshotFiles = try container.decodeIfPresent(Bool.self, forKey: .hideSnapshotFiles) ?? defaults.hideSnapshotFiles
        groupColors = try container.decodeIfPresent([String].self, forKey: .groupColors) ?? defaults.groupColors
        groupSelectionTint = try container.decodeIfPresent(Double.self, forKey: .groupSelectionTint) ?? defaults.groupSelectionTint
        useGlassIcon = try container.decodeIfPresent(Bool.self, forKey: .useGlassIcon) ?? defaults.useGlassIcon
        normalize()
    }

    public mutating func normalize() {
        snapshotIntervalMinutes = min(max(snapshotIntervalMinutes, 1), 120)
        maxSnapshotsPerFile = min(max(maxSnapshotsPerFile, 1), 200)
        groupSelectionTint = min(max(groupSelectionTint, 0), 0.5)
        // An empty palette would leave the colour menu with nothing to offer and no way back.
        if groupColors.isEmpty {
            groupColors = GroupColorPalette.defaults
        }
    }
}
