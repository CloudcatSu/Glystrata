import Foundation

// The on-disk shapes of groups.json, session.json and recent.json. Decoding is lenient throughout
// (missing fields take defaults) so a file from an older build, or one edited by hand, still loads.

public struct GroupsState: Codable, Equatable, Sendable {
    public var schemaVersion = 1
    public var groups: [GroupStateItem] = []

    public init() {}

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        schemaVersion = try container.decodeIfPresent(Int.self, forKey: .schemaVersion) ?? 1
        groups = try container.decodeIfPresent([GroupStateItem].self, forKey: .groups) ?? []
    }

    @MainActor
    public static func from(_ groups: [Group]) -> GroupsState {
        var state = GroupsState()
        state.groups = groups.map { group in
            GroupStateItem(
                id: group.id,
                name: group.name,
                color: group.color,
                items: group.items.map { GroupItemState(id: $0.id, path: $0.path, kind: $0.kind) })
        }
        return state
    }

    @MainActor
    public func apply(to manager: GroupManager) {
        manager.groups = groups.map { groupState in
            let group = Group(name: groupState.name, id: groupState.id)
            group.color = groupState.color
            group.items = groupState.items.map { GroupItem(path: $0.path, kind: $0.kind, id: $0.id) }
            return group
        }
    }
}

public struct GroupStateItem: Codable, Equatable, Sendable {
    public var id: UUID
    public var name: String
    public var color: String?
    public var items: [GroupItemState]

    public init(id: UUID, name: String, color: String?, items: [GroupItemState]) {
        self.id = id
        self.name = name
        self.color = color
        self.items = items
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(UUID.self, forKey: .id)
        name = try container.decodeIfPresent(String.self, forKey: .name) ?? ""
        color = try container.decodeIfPresent(String.self, forKey: .color)
        items = try container.decodeIfPresent([GroupItemState].self, forKey: .items) ?? []
    }
}

public struct GroupItemState: Codable, Equatable, Sendable {
    public var id: UUID
    public var path: String
    public var kind: GroupItemKind

    public init(id: UUID, path: String, kind: GroupItemKind) {
        self.id = id
        self.path = path
        self.kind = kind
    }
}

public struct SessionState: Codable, Equatable, Sendable {
    public var schemaVersion = 1
    public var rootNodeId = UUID(uuid: (0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0))
    public var nodes: [PaneNodeState] = []
    public var views: [SessionViewState] = []
    public var activeViewId: UUID?

    public init() {}

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let defaults = SessionState()
        schemaVersion = try container.decodeIfPresent(Int.self, forKey: .schemaVersion) ?? defaults.schemaVersion
        rootNodeId = try container.decodeIfPresent(UUID.self, forKey: .rootNodeId) ?? defaults.rootNodeId
        nodes = try container.decodeIfPresent([PaneNodeState].self, forKey: .nodes) ?? []
        views = try container.decodeIfPresent([SessionViewState].self, forKey: .views) ?? []
        activeViewId = try container.decodeIfPresent(UUID.self, forKey: .activeViewId)
    }
}

public struct PaneNodeState: Codable, Equatable, Sendable {
    public var id: UUID
    public var isSplit = false
    public var orientation = SplitOrientation.horizontal
    public var ratio = 0.5
    public var firstNodeId: UUID?
    public var secondNodeId: UUID?

    public init(id: UUID) {
        self.id = id
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(UUID.self, forKey: .id)
        isSplit = try container.decodeIfPresent(Bool.self, forKey: .isSplit) ?? false
        orientation = try container.decodeIfPresent(SplitOrientation.self, forKey: .orientation) ?? .horizontal
        ratio = try container.decodeIfPresent(Double.self, forKey: .ratio) ?? 0.5
        firstNodeId = try container.decodeIfPresent(UUID.self, forKey: .firstNodeId)
        secondNodeId = try container.decodeIfPresent(UUID.self, forKey: .secondNodeId)
    }
}

public struct SessionViewState: Codable, Equatable, Sendable {
    public var viewId: UUID
    public var path: String
    public var sourceGroupId: UUID?
    public var paneId: UUID
    public var caretOffset = 0
    public var horizontalOffset = 0.0
    public var verticalOffset = 0.0

    public init(viewId: UUID, path: String, sourceGroupId: UUID?, paneId: UUID) {
        self.viewId = viewId
        self.path = path
        self.sourceGroupId = sourceGroupId
        self.paneId = paneId
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        viewId = try container.decode(UUID.self, forKey: .viewId)
        path = try container.decodeIfPresent(String.self, forKey: .path) ?? ""
        sourceGroupId = try container.decodeIfPresent(UUID.self, forKey: .sourceGroupId)
        paneId = try container.decode(UUID.self, forKey: .paneId)
        caretOffset = try container.decodeIfPresent(Int.self, forKey: .caretOffset) ?? 0
        horizontalOffset = try container.decodeIfPresent(Double.self, forKey: .horizontalOffset) ?? 0
        verticalOffset = try container.decodeIfPresent(Double.self, forKey: .verticalOffset) ?? 0
    }
}

public struct RecentFilesState: Codable, Equatable, Sendable {
    public var schemaVersion = 1
    public var files: [RecentFileEntry] = []

    public init() {}

    // `"files": null` and null elements are both things a hand-edited or half-written recent.json
    // can contain, and neither may stop the editor from starting.
    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        schemaVersion = try container.decodeIfPresent(Int.self, forKey: .schemaVersion) ?? 1
        files = (try container.decodeIfPresent([RecentFileEntry?].self, forKey: .files) ?? []).compactMap { $0 }
    }
}

public struct RecentFileEntry: Codable, Equatable, Sendable {
    public var path: String
    public var lastOpenedUtc: Date

    public init(path: String, lastOpenedUtc: Date) {
        self.path = path
        self.lastOpenedUtc = lastOpenedUtc
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        path = try container.decodeIfPresent(String.self, forKey: .path) ?? ""
        lastOpenedUtc = try container.decodeIfPresent(Date.self, forKey: .lastOpenedUtc) ?? Date(timeIntervalSince1970: 0)
    }
}
