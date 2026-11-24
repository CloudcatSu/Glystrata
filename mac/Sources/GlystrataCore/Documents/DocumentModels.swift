import Foundation
import Observation

// Raw values are part of the on-disk format (snapshot sidecars store them as numbers).
public enum FileEncodingKind: Int, Codable, Sendable, CaseIterable {
    case utf8 = 0
    case utf8WithBom = 1
    case utf16LittleEndian = 2
}

public enum LineEndingKind: Int, Codable, Sendable, CaseIterable {
    case crLf = 0
    case lf = 1
    case cr = 2
}

public struct FileFingerprint: Codable, Equatable, Sendable {
    public var length: Int64
    public var lastWriteTimeUtc: Date

    public init(length: Int64, lastWriteTimeUtc: Date) {
        self.length = length
        self.lastWriteTimeUtc = lastWriteTimeUtc
    }

    public static func read(path: String) throws -> FileFingerprint {
        let attributes = try FileManager.default.attributesOfItem(atPath: path)
        let length = (attributes[.size] as? NSNumber)?.int64Value ?? 0
        let modified = attributes[.modificationDate] as? Date ?? Date(timeIntervalSince1970: 0)
        return FileFingerprint(length: length, lastWriteTimeUtc: modified)
    }
}

public struct DecodedText: Equatable, Sendable {
    public var text: String
    public var encoding: FileEncodingKind
    public var lineEnding: LineEndingKind
    public var fingerprint: FileFingerprint
}

public struct SaveResult: Equatable, Sendable {
    public var success: Bool
    public var errorMessage: String?
    public var fingerprint: FileFingerprint?

    public static func failed(_ message: String) -> SaveResult {
        SaveResult(success: false, errorMessage: message, fingerprint: nil)
    }

    public static func succeeded(_ fingerprint: FileFingerprint) -> SaveResult {
        SaveResult(success: true, errorMessage: nil, fingerprint: fingerprint)
    }
}

/// One open document's content and save state, shared by every tab showing it.
///
/// The text storage the editor types into (and its undo stack) lives in the app layer; it reports
/// edits here through `updateText`, and watches `bufferGeneration` for the cases where the whole
/// buffer is swapped underneath it.
@MainActor
@Observable
public final class DocumentSession: Identifiable {
    public let sessionID = UUID()
    public private(set) var filePath: String?
    public private(set) var documentKey: String
    public private(set) var text: String
    public private(set) var encoding: FileEncodingKind
    public private(set) var lineEnding: LineEndingKind
    public private(set) var isModified = false
    public private(set) var lastSavedFingerprint: FileFingerprint?
    public private(set) var lastError: String?

    /// Bumped whenever the content is replaced wholesale (reload from disk, snapshot restore) rather
    /// than edited, so the editor knows to reload its storage and drop its undo history.
    public private(set) var bufferGeneration = 0

    public var lastSnapshotUtc: Date?
    @ObservationIgnored public var lastSnapshotText: String?
    @ObservationIgnored private var lastSavedText: String

    public init(
        filePath: String?,
        text: String,
        encoding: FileEncodingKind,
        lineEnding: LineEndingKind,
        fingerprint: FileFingerprint? = nil
    ) {
        let fullPath = filePath.map(PathUtilities.fullPath)
        self.filePath = fullPath
        self.documentKey = fullPath.map(PathUtilities.canonicalKey) ?? "untitled:\(sessionID.uuidString.lowercased())"
        self.text = text
        self.encoding = encoding
        self.lineEnding = lineEnding
        self.lastSavedFingerprint = fingerprint
        self.lastSavedText = text
    }

    public nonisolated var id: UUID { sessionID }

    public var isUntitled: Bool { filePath == nil }

    /// Called by the editor after every edit.
    public func updateText(_ newText: String) {
        text = newText
        isModified = !newText.ordinalEquals(lastSavedText)
        lastError = nil
    }

    public func applyLoadedContent(
        _ text: String,
        encoding: FileEncodingKind,
        lineEnding: LineEndingKind,
        fingerprint: FileFingerprint?
    ) {
        replaceBuffer(text)
        self.encoding = encoding
        self.lineEnding = lineEnding
        lastSavedFingerprint = fingerprint
        lastSavedText = text
        isModified = false
        lastError = nil
    }

    public func applyRestoredContent(_ text: String, encoding: FileEncodingKind, lineEnding: LineEndingKind) {
        replaceBuffer(text)
        self.encoding = encoding
        self.lineEnding = lineEnding
        isModified = true
        lastError = nil
    }

    /// `savedText` is what actually went to disk. Typing can continue while a save is in flight, so
    /// comparing against the current text would wrongly clear the modified flag for those edits.
    public func markSaved(_ fingerprint: FileFingerprint, savedText: String) {
        lastSavedFingerprint = fingerprint
        lastSavedText = savedText
        isModified = !text.ordinalEquals(savedText)
        lastError = nil
    }

    public func setSaveError(_ message: String) {
        lastError = message
    }

    public func setFilePath(_ path: String) {
        let fullPath = PathUtilities.fullPath(path)
        filePath = fullPath
        documentKey = PathUtilities.canonicalKey(fullPath)
    }

    public func setEncodingAndLineEnding(_ encoding: FileEncodingKind, _ lineEnding: LineEndingKind) {
        self.encoding = encoding
        self.lineEnding = lineEnding
    }

    private func replaceBuffer(_ newText: String) {
        text = newText
        bufferGeneration += 1
    }
}

/// One tab: a view onto a document, with its own caret and scroll position.
@MainActor
@Observable
public final class DocumentViewState: Identifiable {
    public let viewID = UUID()
    public let document: DocumentSession
    public var sourceGroupID: UUID?
    public var paneID: UUID

    /// Position among the other views sharing this pane; drives tab display order. Lower sorts first.
    public var tabOrder = 0

    // Written on every caret move and scroll tick and only read back when the session is saved, so
    // they stay out of observation rather than re-rendering the window as the user scrolls.
    @ObservationIgnored public var caretOffset = 0
    @ObservationIgnored public var horizontalOffset = 0.0
    @ObservationIgnored public var verticalOffset = 0.0

    public init(document: DocumentSession, sourceGroupID: UUID?, paneID: UUID) {
        self.document = document
        self.sourceGroupID = sourceGroupID
        self.paneID = paneID
    }

    public nonisolated var id: UUID { viewID }
}
