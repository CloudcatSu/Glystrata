import Foundation

public final class SnapshotService: Sendable {
    public let store: SnapshotSidecarStore

    public init(store: SnapshotSidecarStore = SnapshotSidecarStore()) {
        self.store = store
    }

    public func list(_ sourcePath: String) async throws -> [SnapshotInfo] {
        try await store.read(sourcePath)
    }

    /// Returns nil when no snapshot was taken: the document is untitled, it is empty with no history
    /// yet, or nothing changed since the last snapshot.
    @MainActor
    public func create(for document: DocumentSession, maxSnapshots: Int) async throws -> SnapshotInfo? {
        guard let path = document.filePath else {
            return nil
        }

        let limit = min(max(maxSnapshots, 1), 200)
        let text = document.text
        let candidate = SnapshotInfo(
            id: UUID(),
            createdUtc: Date(),
            text: text,
            encoding: document.encoding,
            lineEnding: document.lineEnding,
            sourceFingerprint: document.lastSavedFingerprint)

        // Decided inside the mutation rather than before it: both "the document is empty and there is
        // no history yet" and "nothing changed since the last snapshot" have to be judged against the
        // list we are about to write, not against one read before someone else's write landed.
        let created = try await store.mutate(path) { snapshots -> (changed: Bool, value: Bool) in
            if snapshots.isEmpty && text.isEmpty {
                return (false, false)
            }
            if let newest = snapshots.first, newest.text.ordinalEquals(text) {
                return (false, false)
            }

            snapshots.insert(candidate, at: 0)
            if snapshots.count > limit {
                snapshots.removeLast(snapshots.count - limit)
            }
            return (true, true)
        }

        guard created else {
            return nil
        }

        document.lastSnapshotUtc = candidate.createdUtc
        document.lastSnapshotText = text
        return candidate
    }

    public func delete(_ sourcePath: String, snapshotID: UUID) async throws {
        _ = try await store.mutate(sourcePath) { snapshots -> (changed: Bool, value: Bool) in
            let before = snapshots.count
            snapshots.removeAll { $0.id == snapshotID }
            let changed = snapshots.count < before
            return (changed, changed)
        }
    }

    @discardableResult
    public func updateNote(_ sourcePath: String, snapshotID: UUID, note: String) async throws -> Bool {
        try await store.mutate(sourcePath) { snapshots -> (changed: Bool, value: Bool) in
            guard let index = snapshots.firstIndex(where: { $0.id == snapshotID }) else {
                return (false, false)
            }

            snapshots[index].note = note
            return (true, true)
        }
    }

    public func deleteAll(_ sourcePath: String) async throws {
        try await store.deleteAll(sourcePath)
    }

    public func get(_ sourcePath: String, snapshotID: UUID) async throws -> SnapshotInfo? {
        try await store.read(sourcePath).first { $0.id == snapshotID }
    }
}
