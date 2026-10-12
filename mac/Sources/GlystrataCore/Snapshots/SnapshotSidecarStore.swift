import Foundation

/// Reads and writes the snapshot sidecar that sits beside each document.
///
/// An actor so that every read-change-write of a sidecar runs to completion before the next one
/// starts. Without that, the auto-snapshot timer racing a manual delete or a note edit would both
/// start from the same list, and whichever wrote last would silently drop the other's change.
public actor SnapshotSidecarStore {
    public private(set) var hideSidecarFiles: Bool

    public init(hideSidecarFiles: Bool = false) {
        self.hideSidecarFiles = hideSidecarFiles
    }

    public func setHideSidecarFiles(_ hidden: Bool) {
        hideSidecarFiles = hidden
    }

    // Content is plain JSON; the dedicated extension lets the system give it its own icon and
    // association without touching every other .json file.
    public static func sidecarPath(for sourcePath: String) -> String {
        siblingPath(of: sourcePath) { "\($0).gss" }
    }

    // Names used by earlier Windows builds. A sidecar created by one of them may arrive here through
    // a synced folder, so they are still recognised and renamed to the current scheme on first use.
    private static func legacyPaths(for sourcePath: String) -> [String] {
        [
            siblingPath(of: sourcePath) { "\($0).glystrata-snapshots.json" },
            siblingPath(of: sourcePath) { ".\($0).glystrata-snapshots.json" },
            siblingPath(of: sourcePath) { ".\($0).mdeditor-snapshots.json" }
        ]
    }

    private static func siblingPath(of sourcePath: String, name: (String) -> String) -> String {
        let fullPath = PathUtilities.fullPath(sourcePath)
        let directory = PathUtilities.directory(fullPath)
        return (directory as NSString).appendingPathComponent(name(PathUtilities.fileName(fullPath)))
    }

    private static func migrateLegacySidecar(_ sourcePath: String, to sidecarPath: String) {
        if let legacyPath = legacyPaths(for: sourcePath).first(where: PathUtilities.fileExists) {
            moveIfExists(from: legacyPath, to: sidecarPath)
        }
    }

    /// Proactively migrates a document's sidecar to the current naming, without requiring its
    /// history to be read first. Cheap (a file existence check plus, at most, a rename); safe to sweep
    /// across a whole workspace at startup so older sidecars don't sit under a stale name until their
    /// snapshot history happens to be opened.
    public static func migrateIfNeeded(_ sourcePath: String) {
        let sidecarPath = sidecarPath(for: sourcePath)
        if !PathUtilities.fileExists(sidecarPath) {
            migrateLegacySidecar(sourcePath, to: sidecarPath)
        }
    }

    /// Newest first.
    public func read(_ sourcePath: String) throws -> [SnapshotInfo] {
        try readSidecar(sourcePath).snapshots
            .map(\.info)
            .sorted { $0.createdUtc > $1.createdUtc }
    }

    public func write(_ sourcePath: String, snapshots: [SnapshotInfo]) throws {
        try writeCore(PathUtilities.fullPath(sourcePath), snapshots: snapshots)
    }

    /// Reads the sidecar, lets `body` change the list (newest first), and writes it back as one
    /// uninterrupted step. Everything that edits an existing sidecar has to come through here.
    /// Emptying the list removes the sidecar file rather than leaving an empty one behind.
    /// - Parameter body: Returns whether it changed anything, plus a value to hand back to the caller.
    public func mutate<Value: Sendable>(
        _ sourcePath: String,
        _ body: @Sendable (inout [SnapshotInfo]) -> (changed: Bool, value: Value)
    ) throws -> Value {
        let fullPath = PathUtilities.fullPath(sourcePath)
        var snapshots = try read(fullPath)
        let result = body(&snapshots)
        guard result.changed else {
            return result.value
        }

        if snapshots.isEmpty {
            try deleteAllCore(fullPath)
        } else {
            try writeCore(fullPath, snapshots: snapshots)
        }

        return result.value
    }

    public func deleteAll(_ sourcePath: String) throws {
        try deleteAllCore(sourcePath)
    }

    /// Sets or clears the hidden flag Finder honours. The flag is local to this Mac: cloud-sync
    /// services do not carry it to other devices.
    public static func applyVisibility(_ sourcePath: String, hidden: Bool) {
        var url = URL(fileURLWithPath: sidecarPath(for: sourcePath))
        guard let isHidden = try? url.resourceValues(forKeys: [.isHiddenKey]).isHidden, isHidden != hidden else {
            return
        }

        var values = URLResourceValues()
        values.isHidden = hidden
        try? url.setResourceValues(values)
    }

    /// Moves a document's sidecar (current name and, if still unmigrated, any older naming) to follow a rename on disk.
    public static func renameSidecar(from oldSourcePath: String, to newSourcePath: String) {
        moveIfExists(from: sidecarPath(for: oldSourcePath), to: sidecarPath(for: newSourcePath))
        for (oldPath, newPath) in zip(legacyPaths(for: oldSourcePath), legacyPaths(for: newSourcePath)) {
            moveIfExists(from: oldPath, to: newPath)
        }
    }

    private static func moveIfExists(from oldPath: String, to newPath: String) {
        guard PathUtilities.fileExists(oldPath), !PathUtilities.fileExists(newPath) else {
            return
        }

        // On failure the sidecar stays under its old name; it simply won't be found until the user retries.
        try? FileManager.default.moveItem(atPath: oldPath, toPath: newPath)
    }

    private func writeCore(_ fullPath: String, snapshots: [SnapshotInfo]) throws {
        let sidecar = SnapshotSidecar(
            sourcePath: fullPath,
            snapshots: snapshots.sorted { $0.createdUtc > $1.createdUtc }.map(SnapshotEntry.init))
        let data = try CoreJSON.makeEncoder().encode(sidecar)
        try AtomicFileWriter.writeText(String(decoding: data, as: UTF8.self), to: Self.sidecarPath(for: fullPath))
        Self.applyVisibility(fullPath, hidden: hideSidecarFiles)
    }

    private func deleteAllCore(_ sourcePath: String) throws {
        for path in [Self.sidecarPath(for: sourcePath)] + Self.legacyPaths(for: sourcePath)
        where PathUtilities.fileExists(path) {
            try FileManager.default.removeItem(atPath: path)
        }
    }

    private func readSidecar(_ sourcePath: String) throws -> SnapshotSidecar {
        let sidecarPath = Self.sidecarPath(for: sourcePath)
        if !PathUtilities.fileExists(sidecarPath) {
            Self.migrateLegacySidecar(sourcePath, to: sidecarPath)
        }

        // Migration may have failed (e.g. the old file is locked); read it in place rather than
        // reporting an empty history.
        let readPath = PathUtilities.fileExists(sidecarPath)
            ? sidecarPath
            : Self.legacyPaths(for: sourcePath).first(where: PathUtilities.fileExists) ?? sidecarPath
        guard PathUtilities.fileExists(readPath) else {
            return SnapshotSidecar(sourcePath: PathUtilities.fullPath(sourcePath))
        }

        Self.applyVisibility(sourcePath, hidden: hideSidecarFiles)

        let data = try Data(contentsOf: URL(fileURLWithPath: readPath))
        do {
            return try CoreJSON.makeDecoder().decode(SnapshotSidecar.self, from: data)
        } catch {
            throw GlystrataError.corruptSnapshotSidecar
        }
    }
}
