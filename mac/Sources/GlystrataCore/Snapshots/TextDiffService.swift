import Foundation

public enum DiffLineKind: Sendable {
    case unchanged
    case added
    case removed
}

public struct DiffLine: Equatable, Sendable {
    public var kind: DiffLineKind
    public var text: String
    public var leftLine: Int?
    public var rightLine: Int?
}

public struct TextDiffService: Sendable {
    public init() {}

    public func compare(_ leftText: String, _ rightText: String) -> [DiffLine] {
        let left = Self.splitLines(leftText)
        let right = Self.splitLines(rightText)

        // Lines are compared by their bytes, not with `==`, so a line whose only change is Unicode
        // composition still shows up as changed.
        var ids: [[UInt8]: Int32] = [:]
        func identify(_ line: String) -> Int32 {
            let key = Array(line.utf8)
            if let id = ids[key] {
                return id
            }
            let id = Int32(ids.count)
            ids[key] = id
            return id
        }
        let leftIDs = left.map(identify)
        let rightIDs = right.map(identify)

        // Longest-common-subsequence table, filled from the bottom-right.
        let columns = right.count + 1
        var table = [Int32](repeating: 0, count: (left.count + 1) * columns)
        for leftIndex in stride(from: left.count - 1, through: 0, by: -1) {
            for rightIndex in stride(from: right.count - 1, through: 0, by: -1) {
                table[leftIndex * columns + rightIndex] = leftIDs[leftIndex] == rightIDs[rightIndex]
                    ? table[(leftIndex + 1) * columns + rightIndex + 1] + 1
                    : max(table[(leftIndex + 1) * columns + rightIndex], table[leftIndex * columns + rightIndex + 1])
            }
        }

        var result: [DiffLine] = []
        var i = 0
        var j = 0
        while i < left.count && j < right.count {
            if leftIDs[i] == rightIDs[j] {
                result.append(DiffLine(kind: .unchanged, text: left[i], leftLine: i + 1, rightLine: j + 1))
                i += 1
                j += 1
            } else if table[(i + 1) * columns + j] >= table[i * columns + j + 1] {
                result.append(DiffLine(kind: .removed, text: left[i], leftLine: i + 1, rightLine: nil))
                i += 1
            } else {
                result.append(DiffLine(kind: .added, text: right[j], leftLine: nil, rightLine: j + 1))
                j += 1
            }
        }

        while i < left.count {
            result.append(DiffLine(kind: .removed, text: left[i], leftLine: i + 1, rightLine: nil))
            i += 1
        }

        while j < right.count {
            result.append(DiffLine(kind: .added, text: right[j], leftLine: nil, rightLine: j + 1))
            j += 1
        }

        return result
    }

    private static func splitLines(_ text: String) -> [String] {
        FileCodec.normalizeLineEndings(text).components(separatedBy: "\n")
    }
}
