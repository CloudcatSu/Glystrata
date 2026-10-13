import Foundation

/// Why a settings file could not be imported. Each case maps to one `settings.import.*` string.
public enum SettingsImportError: Error, Equatable, Sendable {
    case invalid
    case notSettings
    case tooNew

    public var localizationKey: String {
        switch self {
        case .invalid: return "settings.import.invalid"
        case .notSettings: return "settings.import.notSettings"
        case .tooNew: return "settings.import.tooNew"
        }
    }
}

/// The exported `.gss` settings file: the settings as indented camelCase JSON plus a `kind` marker.
/// The per-document snapshot sidecar uses the same extension, so `kind` is what tells them apart.
public enum SettingsFile {
    public static let kind = "glystrata-settings"
    public static let supportedSchemaVersion = 1

    public static func export(_ settings: AppSettings) throws -> Data {
        let encoder = JSONEncoder()
        var object = try JSONSerialization.jsonObject(with: encoder.encode(settings)) as? [String: Any] ?? [:]
        object["kind"] = kind
        return try JSONSerialization.data(withJSONObject: object, options: [.prettyPrinted, .sortedKeys])
    }

    public static func `import`(_ data: Data) throws(SettingsImportError) -> AppSettings {
        // JSON5 mode is what lets comments and trailing commas through, as the Windows build allows.
        guard let object = (try? JSONSerialization.jsonObject(with: data, options: [.json5Allowed])) as? [String: Any] else {
            throw .invalid
        }

        if object["snapshots"] != nil || object["notice"] != nil {
            throw .notSettings
        }
        guard object["kind"] != nil else {
            throw .notSettings
        }
        guard let fileKind = object["kind"] as? String else {
            throw .invalid
        }
        guard fileKind == kind else {
            throw .notSettings
        }
        if let version = object["schemaVersion"] {
            guard let number = version as? NSNumber, CFGetTypeID(number) != CFBooleanGetTypeID() else {
                throw .invalid
            }
            if number.intValue > supportedSchemaVersion {
                throw .tooNew
            }
        }

        // JSONDecoder rejects the comments the parse above allowed, so decode from the parsed object.
        guard let clean = try? JSONSerialization.data(withJSONObject: object),
              var settings = try? JSONDecoder().decode(AppSettings.self, from: clean) else {
            throw .invalid
        }
        settings.normalize()
        return settings
    }
}
