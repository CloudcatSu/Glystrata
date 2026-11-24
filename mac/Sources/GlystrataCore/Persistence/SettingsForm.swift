import Foundation

/// Why `SettingsForm.commit` refused. Each case maps to one `settings.error.*` string.
public enum SettingsFormError: Error, Equatable, Sendable {
    case invalidInteger
    case invalidNumber
    case invalidColor(String)
}

public enum HexColor {
    /// "#RRGGBB" in upper case, or nil when `text` is not a colour. Accepts the short "#RGB" form and
    /// the "#AARRGGBB" / "#ARGB" forms (alpha dropped), which are the hex forms the Windows build's
    /// colour parser accepts.
    public static func normalize(_ text: String?) -> String? {
        guard let text else {
            return nil
        }

        let trimmed = text.trimmingCharacters(in: .whitespaces)
        guard trimmed.hasPrefix("#") else {
            return nil
        }

        var digits = Array(trimmed.dropFirst().uppercased())
        guard digits.allSatisfy({ $0.isASCII && $0.isHexDigit }) else {
            return nil
        }

        switch digits.count {
        case 3, 4:
            digits = digits.flatMap { [$0, $0] }
            fallthrough
        case 6, 8:
            return "#" + String(digits.suffix(6))
        default:
            return nil
        }
    }
}

/// The Preferences window's working copy: the text the user has typed, kept apart from the live
/// settings until `commit` validates it. The colour tabs edit whichever palette the chosen theme
/// resolves to, so that is tracked here too.
public struct SettingsForm: Equatable, Sendable {
    public static let editorColorKeys = ["plain", "heading", "emphasis", "link", "list", "quote", "code", "yaml-key", "yaml-value", "yaml-comment", "front-matter"]
    public static let readerColorKeys = ["text", "heading", "link", "quote", "code"]
    public static let typographyKeys = ["h1", "h2", "h3", "h4", "h5", "h6", "line", "paragraph", "heading"]
    public static let newGroupColor = "#3B6FF5"

    /// The fields that are not text-backed (language, theme, toggles…) are edited in place here.
    public var settings: AppSettings
    public var editorColors: [String: String] = [:]
    public var readerColors: [String: String] = [:]
    public var typography: [String: String] = [:]
    public var groupColors: [String] = []
    public var snapshotInterval = ""
    public var maxSnapshots = ""
    /// 0...50; AppSettings stores the same value as a 0...0.5 fraction.
    public var tintPercent = 0.0

    /// What "follow the system" currently resolves to.
    public var systemKind: ThemeKind

    public init(settings: AppSettings, systemKind: ThemeKind) {
        var normalized = settings
        normalized.normalize()
        self.settings = normalized
        self.systemKind = systemKind
        loadFields()
    }

    public var editingTheme: ThemeKind {
        Self.resolve(settings.theme, system: systemKind)
    }

    public static func resolve(_ preference: ThemePreference, system: ThemeKind) -> ThemeKind {
        switch preference {
        case .light: return .light
        case .dark: return .dark
        case .system: return system
        }
    }

    /// Switches theme. Colours typed so far belong to the palette that was being edited, so they are
    /// stored before the fields show the other one.
    public mutating func setTheme(_ preference: ThemePreference) {
        guard preference != settings.theme else {
            return
        }

        let previous = editingTheme
        settings.theme = preference
        if editingTheme != previous {
            storeValidColors(for: previous)
            loadColors()
        }
    }

    /// Replaces everything with `settings`, as Reset and Import do.
    public mutating func replace(with settings: AppSettings) {
        var normalized = settings
        normalized.normalize()
        self.settings = normalized
        loadFields()
    }

    public mutating func addGroupColor() {
        groupColors.append(Self.newGroupColor)
    }

    /// A palette that hits zero entries would be refilled with the defaults by `normalize`, which
    /// would look like the edit was ignored, so the last colour cannot be removed.
    public mutating func removeGroupColor(at index: Int) {
        guard groupColors.count > 1, groupColors.indices.contains(index) else {
            return
        }
        groupColors.remove(at: index)
    }

    /// Checks everything in the order the Windows window does and returns the resulting settings.
    public func commit() -> Result<AppSettings, SettingsFormError> {
        var result = settings
        guard let interval = Int(snapshotInterval.trimmingCharacters(in: .whitespaces)),
              let maximum = Int(maxSnapshots.trimmingCharacters(in: .whitespaces)) else {
            return .failure(.invalidInteger)
        }

        result.snapshotIntervalMinutes = interval
        result.maxSnapshotsPerFile = maximum

        var values: [String: Double] = [:]
        for key in Self.typographyKeys {
            guard let value = Double(typography[key, default: ""].trimmingCharacters(in: .whitespaces)), value.isFinite, value > 0 else {
                return .failure(.invalidNumber)
            }
            values[key] = value
        }
        result.previewTypography.h1Size = values["h1"] ?? 0
        result.previewTypography.h2Size = values["h2"] ?? 0
        result.previewTypography.h3Size = values["h3"] ?? 0
        result.previewTypography.h4Size = values["h4"] ?? 0
        result.previewTypography.h5Size = values["h5"] ?? 0
        result.previewTypography.h6Size = values["h6"] ?? 0
        result.previewTypography.lineSpacing = values["line"] ?? 0
        result.previewTypography.paragraphSpacing = values["paragraph"] ?? 0
        result.previewTypography.headingSpacing = values["heading"] ?? 0

        var editor: [String: String] = [:]
        for key in Self.editorColorKeys {
            guard let color = HexColor.normalize(editorColors[key]) else {
                return .failure(.invalidColor(editorColors[key] ?? ""))
            }
            editor[key] = color
        }
        var reader: [String: String] = [:]
        for key in Self.readerColorKeys {
            guard let color = HexColor.normalize(readerColors[key]) else {
                return .failure(.invalidColor(readerColors[key] ?? ""))
            }
            reader[key] = color
        }
        var groups: [String] = []
        for text in groupColors {
            guard let color = HexColor.normalize(text) else {
                return .failure(.invalidColor(text))
            }
            groups.append(color)
        }

        if editingTheme == .dark {
            result.darkEditorPalette.colors.merge(editor) { _, new in new }
            result.darkReaderPalette.colors.merge(reader) { _, new in new }
        } else {
            result.lightEditorPalette.colors.merge(editor) { _, new in new }
            result.lightReaderPalette.colors.merge(reader) { _, new in new }
        }
        result.groupColors = groups
        result.groupSelectionTint = tintPercent / 100
        result.normalize()
        return .success(result)
    }

    private mutating func loadFields() {
        snapshotInterval = String(settings.snapshotIntervalMinutes)
        maxSnapshots = String(settings.maxSnapshotsPerFile)
        let t = settings.previewTypography
        typography = [
            "h1": Self.text(t.h1Size), "h2": Self.text(t.h2Size), "h3": Self.text(t.h3Size),
            "h4": Self.text(t.h4Size), "h5": Self.text(t.h5Size), "h6": Self.text(t.h6Size),
            "line": Self.text(t.lineSpacing), "paragraph": Self.text(t.paragraphSpacing), "heading": Self.text(t.headingSpacing)
        ]
        loadColors()
        groupColors = settings.groupColors
        tintPercent = (settings.groupSelectionTint * 100).rounded()
    }

    private mutating func loadColors() {
        let dark = editingTheme == .dark
        let editor = dark ? settings.darkEditorPalette : settings.lightEditorPalette
        let reader = dark ? settings.darkReaderPalette : settings.lightReaderPalette
        editorColors = Dictionary(uniqueKeysWithValues: Self.editorColorKeys.map { ($0, editor.get($0, fallback: "#808080")) })
        readerColors = Dictionary(uniqueKeysWithValues: Self.readerColorKeys.map { ($0, reader.get($0, fallback: "#808080")) })
    }

    /// Entries that are not valid colours yet are left out, so a half-typed value does not
    /// overwrite a good one just because the theme changed.
    private mutating func storeValidColors(for theme: ThemeKind) {
        for (key, text) in editorColors {
            if let color = HexColor.normalize(text) {
                if theme == .dark { settings.darkEditorPalette.colors[key] = color } else { settings.lightEditorPalette.colors[key] = color }
            }
        }
        for (key, text) in readerColors {
            if let color = HexColor.normalize(text) {
                if theme == .dark { settings.darkReaderPalette.colors[key] = color } else { settings.lightReaderPalette.colors[key] = color }
            }
        }
    }

    /// 32.0 shows as "32", 1.35 as "1.35".
    private static func text(_ value: Double) -> String {
        value == value.rounded() && abs(value) < 1e15 ? String(Int(value)) : String(value)
    }
}
