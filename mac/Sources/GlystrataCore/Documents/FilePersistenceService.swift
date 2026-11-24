import Foundation

public struct FilePersistenceService: Sendable {
    private let codec: FileCodec

    public init(codec: FileCodec = FileCodec()) {
        self.codec = codec
    }

    public func save(
        text: String,
        to path: String,
        encoding: FileEncodingKind,
        lineEnding: LineEndingKind
    ) async throws -> FileFingerprint {
        let fullPath = PathUtilities.fullPath(path)
        let fileManager = FileManager.default
        let exists = PathUtilities.fileExists(fullPath)
        if exists && !fileManager.isWritableFile(atPath: fullPath) {
            throw GlystrataError.readOnlyFile
        }

        let data = codec.encode(text, encoding: encoding, lineEnding: lineEnding)
        let target = URL(fileURLWithPath: fullPath)
        let temporary = URL(fileURLWithPath: "\(fullPath).glystrata-write-\(UUID().uuidString.lowercased()).tmp")
        defer { try? fileManager.removeItem(at: temporary) }

        try data.write(to: temporary, options: .withoutOverwriting)
        if exists {
            // Swaps the content in while keeping the original file's identity and metadata (Finder
            // tags, permissions), which a plain write-and-rename would drop.
            _ = try fileManager.replaceItemAt(target, withItemAt: temporary)
        } else {
            try fileManager.moveItem(at: temporary, to: target)
        }

        return try FileFingerprint.read(path: fullPath)
    }
}
