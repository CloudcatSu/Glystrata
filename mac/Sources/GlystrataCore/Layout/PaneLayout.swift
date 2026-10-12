import Foundation

// Raw values are part of session.json.
public enum SplitOrientation: Int, Codable, Sendable {
    case horizontal = 0
    case vertical = 1
}

public indirect enum PaneLayoutNode: Equatable, Sendable {
    case editorPane(paneID: UUID)
    case split(id: UUID, orientation: SplitOrientation, ratio: Double, first: PaneLayoutNode, second: PaneLayoutNode)

    public var id: UUID {
        switch self {
        case .editorPane(let paneID):
            return paneID
        case .split(let id, _, _, _, _):
            return id
        }
    }
}

public enum PaneLayoutOperations {
    public static func firstPaneID(_ root: PaneLayoutNode) -> UUID {
        switch root {
        case .editorPane(let paneID):
            return paneID
        case .split(_, _, _, let first, _):
            return firstPaneID(first)
        }
    }

    public static func paneIDs(_ root: PaneLayoutNode) -> [UUID] {
        switch root {
        case .editorPane(let paneID):
            return [paneID]
        case .split(_, _, _, let first, let second):
            return paneIDs(first) + paneIDs(second)
        }
    }

    public static func collapseToSinglePane(_ root: PaneLayoutNode) -> PaneLayoutNode {
        .editorPane(paneID: firstPaneID(root))
    }

    /// Removes a pane and collapses the split that held it, so its sibling takes over the space.
    /// Returns nil when the pane removed was the whole layout.
    public static func removePane(_ root: PaneLayoutNode, paneID: UUID) -> (node: PaneLayoutNode?, removed: Bool) {
        switch root {
        case .editorPane(let id):
            return id == paneID ? (nil, true) : (root, false)
        case .split(let id, let orientation, let ratio, let first, let second):
            if containsPane(first, paneID: paneID) {
                let result = removePane(first, paneID: paneID)
                guard let remaining = result.node else {
                    return (second, result.removed)
                }
                return (.split(id: id, orientation: orientation, ratio: ratio, first: remaining, second: second), result.removed)
            }

            if containsPane(second, paneID: paneID) {
                let result = removePane(second, paneID: paneID)
                guard let remaining = result.node else {
                    return (first, result.removed)
                }
                return (.split(id: id, orientation: orientation, ratio: ratio, first: first, second: remaining), result.removed)
            }

            return (root, false)
        }
    }

    public static func containsPane(_ root: PaneLayoutNode, paneID: UUID) -> Bool {
        switch root {
        case .editorPane(let id):
            return id == paneID
        case .split(_, _, _, let first, let second):
            return containsPane(first, paneID: paneID) || containsPane(second, paneID: paneID)
        }
    }
}
