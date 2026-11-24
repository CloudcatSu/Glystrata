import Foundation
import Observation

@MainActor
@Observable
public final class GroupManager {
    public var groups: [Group] = []

    public init() {}

    @discardableResult
    public func createGroup(_ name: String) -> Group {
        let group = Group(name: name)
        groups.append(group)
        return group
    }

    @discardableResult
    public func renameGroup(_ groupID: UUID, name: String) -> Bool {
        let trimmed = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard let group = find(groupID), !trimmed.isEmpty else {
            return false
        }

        group.name = trimmed
        return true
    }

    /// Sets a group's colour bar, or clears it when `color` is nil or blank.
    @discardableResult
    public func setGroupColor(_ groupID: UUID, color: String?) -> Bool {
        guard let group = find(groupID) else {
            return false
        }

        let trimmed = color?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        group.color = trimmed.isEmpty ? nil : trimmed
        return true
    }

    /// Carries an edit of the colour palette over to the groups using it. A group stores the
    /// colour itself rather than a palette index, so without this a changed swatch would leave every
    /// group that picked it on the old colour. Entries are matched by position, since the palette
    /// editor can only change, append or remove swatches, never reorder them; groups on a removed
    /// swatch or a colour that was never in the palette keep what they have.
    /// - Returns: Whether any group changed.
    @discardableResult
    public func remapColors(oldPalette: [String], newPalette: [String]) -> Bool {
        var changed = false
        for group in groups {
            guard let color = group.color else {
                continue
            }

            for index in 0..<min(oldPalette.count, newPalette.count)
            where color.caseInsensitiveCompare(oldPalette[index]) == .orderedSame {
                if color.caseInsensitiveCompare(newPalette[index]) != .orderedSame {
                    group.color = newPalette[index]
                    changed = true
                }
                break
            }
        }
        return changed
    }

    @discardableResult
    public func deleteGroup(_ groupID: UUID) -> Bool {
        guard let index = groups.firstIndex(where: { $0.id == groupID }) else {
            return false
        }

        groups.remove(at: index)
        return true
    }

    @discardableResult
    public func addPath(_ groupID: UUID, path: String, kind: GroupItemKind? = nil) -> GroupItem? {
        guard let group = find(groupID), !path.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            return nil
        }

        let fullPath = PathUtilities.fullPath(path)
        guard let itemKind = kind ?? Self.inferKind(fullPath) else {
            return nil
        }

        if let existing = group.items.first(where: { Self.pathsEqual($0.path, fullPath) }) {
            return existing
        }

        let item = GroupItem(path: fullPath, kind: itemKind)
        group.items.append(item)
        return item
    }

    @discardableResult
    public func removePath(_ groupID: UUID, path: String) -> Bool {
        guard let group = find(groupID),
              let index = group.items.firstIndex(where: { Self.pathsEqual($0.path, path) }) else {
            return false
        }

        group.items.remove(at: index)
        return true
    }

    public func renamePath(from oldPath: String, to newPath: String) {
        for group in groups {
            for item in group.items where item.kind == .file && Self.pathsEqual(item.path, oldPath) {
                item.setPath(newPath)
            }
        }
    }

    @discardableResult
    public func movePath(from sourceGroupID: UUID, to targetGroupID: UUID, path: String) -> Bool {
        guard let source = find(sourceGroupID), let target = find(targetGroupID), source.id != target.id,
              let sourceIndex = source.items.firstIndex(where: { Self.pathsEqual($0.path, path) }) else {
            return false
        }

        let sourceItem = source.items[sourceIndex]
        if !target.items.contains(where: { Self.pathsEqual($0.path, sourceItem.path) }) {
            target.items.append(GroupItem(path: sourceItem.path, kind: sourceItem.kind, id: sourceItem.id))
        }

        source.items.remove(at: sourceIndex)
        return true
    }

    public func find(_ groupID: UUID) -> Group? {
        groups.first { $0.id == groupID }
    }

    public func moveGroup(from oldIndex: Int, to newIndex: Int) {
        guard groups.indices.contains(oldIndex), groups.indices.contains(newIndex), oldIndex != newIndex else {
            return
        }

        let group = groups.remove(at: oldIndex)
        groups.insert(group, at: newIndex)
    }

    public nonisolated func enumerateFolder(_ path: String) -> [FileSystemEntry] {
        guard PathUtilities.directoryExists(path) else {
            return []
        }

        let keys: [URLResourceKey] = [.isDirectoryKey]
        guard let contents = try? FileManager.default.contentsOfDirectory(
            at: URL(fileURLWithPath: path),
            includingPropertiesForKeys: keys,
            options: [.skipsHiddenFiles]) else {
            return []
        }

        return contents
            .compactMap { url -> FileSystemEntry? in
                let isDirectory = (try? url.resourceValues(forKeys: Set(keys)).isDirectory) ?? false
                let name = url.lastPathComponent
                if !isDirectory && Self.isSnapshotSidecar(name) {
                    return nil
                }
                return FileSystemEntry(path: url.path, name: name, kind: isDirectory ? .folder : .file)
            }
            .sorted { left, right in
                if left.kind != right.kind {
                    return left.kind == .folder
                }
                return left.name.localizedCaseInsensitiveCompare(right.name) == .orderedAscending
            }
    }

    private nonisolated static func inferKind(_ path: String) -> GroupItemKind? {
        if PathUtilities.directoryExists(path) {
            return .folder
        }

        if PathUtilities.fileExists(path) {
            return .file
        }

        return nil
    }

    private nonisolated static func pathsEqual(_ left: String, _ right: String) -> Bool {
        PathUtilities.canonicalKey(left) == PathUtilities.canonicalKey(right)
    }

    private nonisolated static func isSnapshotSidecar(_ name: String) -> Bool {
        let lowered = name.lowercased()
        return lowered.hasSuffix(".gss")
            || lowered.hasSuffix(".glystrata-snapshots.json")
            || lowered.hasSuffix(".mdeditor-snapshots.json")
    }
}
