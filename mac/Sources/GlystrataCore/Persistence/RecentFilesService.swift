import Foundation
import Observation

/// In-memory list of recently opened files, newest first. Owns ordering, de-duplication
/// (by canonical path) and the cap on entry count. Persistence is a separate concern: callers pair
/// this with `RecentFilesState` via `load(from:)` / `toState()`, the same way `GroupsState` pairs
/// with `GroupManager`.
@MainActor
@Observable
public final class RecentFilesService {
    public static let maxEntries = 10

    public private(set) var files: [RecentFileEntry] = []

    public init() {}

    public func add(_ path: String) {
        guard !Self.isBlank(path) else {
            return
        }

        let canonical = PathUtilities.canonicalKey(path)
        files.removeAll { PathUtilities.canonicalKey($0.path) == canonical }
        files.insert(RecentFileEntry(path: path, lastOpenedUtc: Date()), at: 0)
        if files.count > Self.maxEntries {
            files.removeLast(files.count - Self.maxEntries)
        }
    }

    /// Records a file as the oldest entry rather than the newest, and only if it is not listed
    /// already. Used to seed the list from the documents a restored session reopened: they belong in
    /// the list, but treating every restart as "you just opened all of these" would push out the files
    /// you closed yesterday, which are the ones a recent list is actually for.
    public func addIfMissing(_ path: String, lastOpenedUtc: Date) {
        guard !Self.isBlank(path), files.count < Self.maxEntries else {
            return
        }

        let canonical = PathUtilities.canonicalKey(path)
        guard !files.contains(where: { PathUtilities.canonicalKey($0.path) == canonical }) else {
            return
        }

        files.append(RecentFileEntry(path: path, lastOpenedUtc: lastOpenedUtc))
    }

    @discardableResult
    public func remove(_ path: String) -> Bool {
        guard !Self.isBlank(path) else {
            return false
        }

        let canonical = PathUtilities.canonicalKey(path)
        let before = files.count
        files.removeAll { PathUtilities.canonicalKey($0.path) == canonical }
        return files.count < before
    }

    public func clear() {
        files.removeAll()
    }

    /// Replaces the in-memory list from a loaded state, tolerating a corrupt-ish file: entries with
    /// an empty or whitespace path are dropped, duplicates (by canonical path) are dropped keeping
    /// the first occurrence, and the result is truncated to `maxEntries`. Whether a path still exists
    /// on disk is deliberately not checked here - that is a question for the moment the menu opens,
    /// not load time.
    public func load(from state: RecentFilesState) {
        var loaded: [RecentFileEntry] = []
        var seen = Set<String>()
        for entry in state.files where !Self.isBlank(entry.path) {
            guard seen.insert(PathUtilities.canonicalKey(entry.path)).inserted else {
                continue
            }

            loaded.append(entry)
            if loaded.count >= Self.maxEntries {
                break
            }
        }
        files = loaded
    }

    public func toState() -> RecentFilesState {
        var state = RecentFilesState()
        state.files = files
        return state
    }

    private static func isBlank(_ path: String) -> Bool {
        path.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }
}
