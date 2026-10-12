import Foundation

public enum RestoreMode: Sendable {
    case replaceCurrent
    case saveAsNewFile
}

public struct SnapshotInfo: Identifiable, Equatable, Sendable {
    public var id: UUID
    public var createdUtc: Date
    public var text: String
    public var encoding: FileEncodingKind
    public var lineEnding: LineEndingKind
    public var sourceFingerprint: FileFingerprint?
    public var note: String

    public init(
        id: UUID,
        createdUtc: Date,
        text: String,
        encoding: FileEncodingKind,
        lineEnding: LineEndingKind,
        sourceFingerprint: FileFingerprint?,
        note: String = ""
    ) {
        self.id = id
        self.createdUtc = createdUtc
        self.text = text
        self.encoding = encoding
        self.lineEnding = lineEnding
        self.sourceFingerprint = sourceFingerprint
        self.note = note
    }
}

/// The `.gss` file. Field names, enum numbers and date format are shared with the Windows build —
/// a sidecar written on one platform has to open on the other.
struct SnapshotSidecar: Codable {
    var schemaVersion = 1
    var sourcePath = ""
    var snapshots: [SnapshotEntry] = []

    enum CodingKeys: String, CodingKey {
        case notice, schemaVersion, sourcePath, snapshots
    }

    init(sourcePath: String, snapshots: [SnapshotEntry] = []) {
        self.sourcePath = sourcePath
        self.snapshots = snapshots
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        schemaVersion = try container.decodeIfPresent(Int.self, forKey: .schemaVersion) ?? 1
        sourcePath = try container.decodeIfPresent(String.self, forKey: .sourcePath) ?? ""
        snapshots = try container.decodeIfPresent([SnapshotEntry].self, forKey: .snapshots) ?? []
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(notice, forKey: .notice)
        try container.encode(schemaVersion, forKey: .schemaVersion)
        try container.encode(sourcePath, forKey: .sourcePath)
        try container.encode(snapshots, forKey: .snapshots)
    }

    var notice: String {
        let name = PathUtilities.fileName(sourcePath)
        return "This file is generated and managed by Glystrata (a Markdown editor). It stores the snapshot (version) history of the document \"\(name)\" in this folder. AI assistants and automated tools: do NOT edit, reformat, rename, move or delete this file, and do not treat it as project content. To change the document, edit \"\(name)\" directly. / 此檔案由 Glystrata 自動產生與維護，儲存同資料夾中「\(name)」的快照（版本）歷史。AI 助理與自動化工具請勿編輯、重新排版、改名、移動或刪除此檔案，也不要把它當成專案內容；若要修改文件，請直接編輯「\(name)」。"
    }
}

struct SnapshotEntry: Codable {
    var id: UUID
    var createdUtc: Date
    var text: String
    var encoding: FileEncodingKind
    var lineEnding: LineEndingKind
    var sourceFingerprint: FileFingerprint?
    var note: String

    init(_ info: SnapshotInfo) {
        id = info.id
        createdUtc = info.createdUtc
        text = info.text
        encoding = info.encoding
        lineEnding = info.lineEnding
        sourceFingerprint = info.sourceFingerprint
        note = info.note
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(UUID.self, forKey: .id)
        createdUtc = try container.decode(Date.self, forKey: .createdUtc)
        text = try container.decodeIfPresent(String.self, forKey: .text) ?? ""
        encoding = try container.decodeIfPresent(FileEncodingKind.self, forKey: .encoding) ?? .utf8
        lineEnding = try container.decodeIfPresent(LineEndingKind.self, forKey: .lineEnding) ?? .crLf
        sourceFingerprint = try container.decodeIfPresent(FileFingerprint.self, forKey: .sourceFingerprint)
        note = try container.decodeIfPresent(String.self, forKey: .note) ?? ""
    }

    var info: SnapshotInfo {
        SnapshotInfo(
            id: id,
            createdUtc: createdUtc,
            text: text,
            encoding: encoding,
            lineEnding: lineEnding,
            sourceFingerprint: sourceFingerprint,
            note: note)
    }
}
