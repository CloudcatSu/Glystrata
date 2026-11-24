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

    /// Puts a new pane beside an existing one, sharing its space equally.
    public static func replacePaneWithSplit(
        _ root: PaneLayoutNode,
        paneID: UUID,
        orientation: SplitOrientation,
        newPaneID: UUID
    ) -> PaneLayoutNode {
        switch root {
        case .editorPane(let id):
            guard id == paneID else {
                return root
            }
            return .split(id: UUID(), orientation: orientation, ratio: 0.5, first: root, second: .editorPane(paneID: newPaneID))
        case .split(let id, let splitOrientation, let ratio, let first, let second):
            return .split(
                id: id,
                orientation: splitOrientation,
                ratio: ratio,
                first: replacePaneWithSplit(first, paneID: paneID, orientation: orientation, newPaneID: newPaneID),
                second: replacePaneWithSplit(second, paneID: paneID, orientation: orientation, newPaneID: newPaneID))
        }
    }

    /// Changes how a split divides its space; the ratio is the first side's share.
    public static func settingRatio(_ root: PaneLayoutNode, splitID: UUID, ratio: Double) -> PaneLayoutNode {
        guard case .split(let id, let orientation, let oldRatio, let first, let second) = root else {
            return root
        }
        return .split(
            id: id,
            orientation: orientation,
            ratio: id == splitID ? min(max(ratio, 0.1), 0.9) : oldRatio,
            first: settingRatio(first, splitID: splitID, ratio: ratio),
            second: settingRatio(second, splitID: splitID, ratio: ratio))
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
