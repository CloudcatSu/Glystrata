import Foundation
import Observation

@MainActor
@Observable
public final class DocumentManager {
    private var documentsByKey: [String: DocumentSession] = [:]
    private var viewsByID: [UUID: DocumentViewState] = [:]

    // The newest save queued for each document. A save waits for the one before it, so overlapping
    // saves of the same file land in the order they were asked for.
    @ObservationIgnored private var saveTails: [UUID: Task<SaveResult, Never>] = [:]
    @ObservationIgnored private let codec: FileCodec
    @ObservationIgnored private let persistence: FilePersistenceService

    public init(codec: FileCodec = FileCodec(), persistence: FilePersistenceService? = nil) {
        self.codec = codec
        self.persistence = persistence ?? FilePersistenceService(codec: codec)
    }

    public var documents: [DocumentSession] { Array(documentsByKey.values) }

    public var views: [DocumentViewState] { Array(viewsByID.values) }

    public func open(_ path: String) throws -> DocumentSession {
        let fullPath = PathUtilities.fullPath(path)
        let key = PathUtilities.canonicalKey(fullPath)
        if let existing = documentsByKey[key] {
            return existing
        }

        guard PathUtilities.fileExists(fullPath) else {
            throw GlystrataError.fileNotFound(path: fullPath)
        }

        let decoded = try codec.read(path: fullPath)
        let document = DocumentSession(
            filePath: fullPath,
            text: decoded.text,
            encoding: decoded.encoding,
            lineEnding: decoded.lineEnding,
            fingerprint: decoded.fingerprint)
        documentsByKey[key] = document
        return document
    }

    public func openView(
        _ path: String,
        paneID: UUID,
        sourceGroupID: UUID? = nil,
        newView: Bool = false
    ) throws -> DocumentViewState {
        let document = try open(path)
        if !newView, let existing = viewsByID.values.first(where: { $0.document === document }) {
            existing.sourceGroupID = sourceGroupID ?? existing.sourceGroupID
            return existing
        }

        return createView(document, sourceGroupID: sourceGroupID, paneID: paneID)
    }

    public func createUntitled(paneID: UUID, sourceGroupID: UUID? = nil) -> DocumentViewState {
        let document = DocumentSession(filePath: nil, text: "", encoding: .utf8, lineEnding: .lf)
        documentsByKey[document.documentKey] = document
        return createView(document, sourceGroupID: sourceGroupID, paneID: paneID)
    }

    public func createView(_ document: DocumentSession, sourceGroupID: UUID?, paneID: UUID) -> DocumentViewState {
        let nextOrder = (viewsByID.values.filter { $0.paneID == paneID }.map(\.tabOrder).max() ?? -1) + 1
        let view = DocumentViewState(document: document, sourceGroupID: sourceGroupID, paneID: paneID)
        view.tabOrder = nextOrder
        viewsByID[view.viewID] = view
        return view
    }

    /// Applies a new tab order within a pane after a drag-drop reorder.
    public func reorderViews(paneID: UUID, orderedViewIDs: [UUID]) {
        for (index, viewID) in orderedViewIDs.enumerated() {
            if let view = viewsByID[viewID], view.paneID == paneID {
                view.tabOrder = index
            }
        }
    }

    public func closeView(_ viewID: UUID) {
        guard let view = viewsByID.removeValue(forKey: viewID) else {
            return
        }

        if !viewsByID.values.contains(where: { $0.document === view.document }) {
            documentsByKey.removeValue(forKey: view.document.documentKey)
            saveTails.removeValue(forKey: view.document.sessionID)
        }
    }

    public func save(_ document: DocumentSession) async -> SaveResult {
        guard document.filePath != nil else {
            return .failed("未命名文件必須先另存新檔。")
        }

        return await enqueueSave(document, to: nil)
    }

    public func saveAs(_ document: DocumentSession, to path: String) async -> SaveResult {
        await enqueueSave(document, to: path)
    }

    /// Repoints a document at a path whose file was already moved on disk (e.g. a rename), without re-saving its content.
    public func renamePath(_ document: DocumentSession, to newPath: String) {
        rekey(document, to: newPath)
    }

    public func findView(_ viewID: UUID) -> DocumentViewState? {
        viewsByID[viewID]
    }

    public func findDocument(_ path: String) -> DocumentSession? {
        documentsByKey[PathUtilities.canonicalKey(path)]
    }

    public nonisolated static func canonicalizePath(_ path: String) -> String {
        PathUtilities.canonicalKey(path)
    }

    /// `newPath` is nil for a plain save. For Save As the document only moves to the new path once
    /// the write has succeeded, so a failed Save As leaves it exactly where it was.
    private func enqueueSave(_ document: DocumentSession, to newPath: String?) async -> SaveResult {
        let previous = saveTails[document.sessionID]
        let task = Task { @MainActor () -> SaveResult in
            _ = await previous?.value
            guard let path = newPath ?? document.filePath else {
                return .failed("未命名文件必須先另存新檔。")
            }

            let text = document.text
            do {
                let fingerprint = try await persistence.save(
                    text: text,
                    to: path,
                    encoding: document.encoding,
                    lineEnding: document.lineEnding)
                if newPath != nil {
                    rekey(document, to: path)
                }
                document.markSaved(fingerprint, savedText: text)
                return .succeeded(fingerprint)
            } catch {
                let message = "儲存失敗：\(error.localizedDescription)"
                document.setSaveError(message)
                return .failed(message)
            }
        }
        saveTails[document.sessionID] = task
        return await task.value
    }

    private func rekey(_ document: DocumentSession, to path: String) {
        let oldKey = document.documentKey
        document.setFilePath(path)
        if documentsByKey[oldKey] === document {
            documentsByKey.removeValue(forKey: oldKey)
        }
        documentsByKey[document.documentKey] = document
    }
}
