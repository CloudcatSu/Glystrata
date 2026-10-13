import Foundation

enum AtomicFileWriter {
    static func writeText(_ content: String, to path: String) throws {
        let directory = PathUtilities.directory(path)
        guard !directory.isEmpty else {
            throw GlystrataError.missingDirectory("設定檔路徑缺少目錄。")
        }

        try FileManager.default.createDirectory(atPath: directory, withIntermediateDirectories: true)
        try Data(content.utf8).write(to: URL(fileURLWithPath: path), options: .atomic)
    }
}

/// Settings, groups, the open-tabs session and the recent-files list, each as one JSON file under
/// `~/Library/Application Support/Glystrata`.
public struct JSONStateStore: Sendable {
    public let baseDirectory: String

    public init(baseDirectory: String? = nil) {
        self.baseDirectory = baseDirectory ?? URL.applicationSupportDirectory.appending(path: "Glystrata").path
    }

    public func loadSettings() async -> AppSettings {
        load("settings.json", fallback: AppSettings())
    }

    // Synchronous so startup can apply the real theme/language before the window is first shown.
    public func loadSettingsForStartup() -> AppSettings {
        load("settings.json", fallback: AppSettings(), keepCorruptCopy: false)
    }

    public func saveSettings(_ settings: AppSettings) async throws {
        var normalized = settings
        normalized.normalize()
        try save(normalized, as: "settings.json")
    }

    public func loadGroups() async -> GroupsState {
        load("groups.json", fallback: GroupsState())
    }

    public func saveGroups(_ groups: GroupsState) async throws {
        try save(groups, as: "groups.json")
    }

    public func loadSession() async -> SessionState {
        load("session.json", fallback: SessionState())
    }

    public func saveSession(_ session: SessionState) async throws {
        try save(session, as: "session.json")
    }

    // Kept separate from settings.json on purpose: recent files are per-machine working state,
    // and must not ride along in an exported settings file (that would leak file paths).
    public func loadRecentFiles() async -> RecentFilesState {
        load("recent.json", fallback: RecentFilesState())
    }

    public func saveRecentFiles(_ state: RecentFilesState) async throws {
        try save(state, as: "recent.json")
    }

    /// Writes all four files in one go without suspending, so it can run while the app is quitting.
    /// Each file is attempted on its own: one that cannot be written does not hold back the others.
    public func saveEverything(settings: AppSettings, groups: GroupsState, session: SessionState, recentFiles: RecentFilesState) {
        var normalized = settings
        normalized.normalize()
        try? save(normalized, as: "settings.json")
        try? save(groups, as: "groups.json")
        try? save(session, as: "session.json")
        try? save(recentFiles, as: "recent.json")
    }

    private func path(for fileName: String) -> String {
        (baseDirectory as NSString).appendingPathComponent(fileName)
    }

    private func load<T: Decodable>(_ fileName: String, fallback: T, keepCorruptCopy: Bool = true) -> T {
        let path = path(for: fileName)
        guard let data = FileManager.default.contents(atPath: path) else {
            return fallback
        }

        do {
            return try CoreJSON.makeDecoder().decode(T.self, from: data)
        } catch {
            if keepCorruptCopy {
                // Move the unreadable file aside so the next save does not overwrite the only copy of
                // whatever the user had. The safe defaults remain usable even if the move fails.
                let backup = "\(path).corrupt-\(Self.timestamp()).json"
                try? FileManager.default.removeItem(atPath: backup)
                try? FileManager.default.moveItem(atPath: path, toPath: backup)
            }
            return fallback
        }
    }

    private func save<T: Encodable>(_ value: T, as fileName: String) throws {
        let data = try CoreJSON.makeEncoder().encode(value)
        try AtomicFileWriter.writeText(String(decoding: data, as: UTF8.self), to: path(for: fileName))
    }

    private static func timestamp() -> String {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(secondsFromGMT: 0)
        formatter.dateFormat = "yyyyMMddHHmmss"
        return formatter.string(from: Date())
    }
}
