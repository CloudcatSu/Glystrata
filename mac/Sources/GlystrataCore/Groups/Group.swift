import Foundation
import Observation

// Raw values are part of groups.json.
public enum GroupItemKind: Int, Codable, Sendable {
    case file = 0
    case folder = 1
}

@MainActor
@Observable
public final class GroupItem: Identifiable {
    public let id: UUID
    public private(set) var path: String
    public let kind: GroupItemKind

    public init(path: String, kind: GroupItemKind, id: UUID? = nil) {
        self.id = id ?? UUID()
        self.path = PathUtilities.fullPath(path)
        self.kind = kind
    }

    public func setPath(_ path: String) {
        self.path = PathUtilities.fullPath(path)
    }

    public var exists: Bool {
        kind == .folder ? PathUtilities.directoryExists(path) : PathUtilities.fileExists(path)
    }

    public var displayName: String {
        PathUtilities.fileName(path)
    }
}

@MainActor
@Observable
public final class Group: Identifiable {
    public let id: UUID
    public var name: String

    /// Hex colour shown as a bar beside the group and its documents' tabs, or nil for none.
    /// The value is stored here rather than as an index into the settings palette so that editing or
    /// removing a palette entry can never leave a group pointing at something that is gone.
    public var color: String?

    public var items: [GroupItem] = []

    public init(name: String, id: UUID? = nil) {
        self.id = id ?? UUID()
        let trimmed = name.trimmingCharacters(in: .whitespacesAndNewlines)
        self.name = trimmed.isEmpty ? "未命名群組" : trimmed
    }
}

public struct FileSystemEntry: Equatable, Sendable {
    public var path: String
    public var name: String
    public var kind: GroupItemKind
}
