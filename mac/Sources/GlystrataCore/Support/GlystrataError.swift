import Foundation

public enum GlystrataError: LocalizedError, Equatable, Sendable {
    case unsupportedTextEncoding(String)
    case fileNotFound(path: String)
    case readOnlyFile
    case untitledNeedsSaveAs
    case corruptSnapshotSidecar
    case missingDirectory(String)

    public var errorDescription: String? {
        switch self {
        case .unsupportedTextEncoding(let message):
            return message
        case .fileNotFound:
            return "找不到指定檔案。"
        case .readOnlyFile:
            return "檔案是唯讀檔案。"
        case .untitledNeedsSaveAs:
            return "未命名文件必須先使用另存新檔指定路徑。"
        case .corruptSnapshotSidecar:
            return "快照檔案格式損壞。"
        case .missingDirectory(let message):
            return message
        }
    }
}
