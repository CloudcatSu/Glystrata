import AppKit
import GlystrataCore

/// The reader windows, one per tab, and the Markdown guide, which is drawn by the same renderer.
extension AppModel {
    func isReaderOpen(for view: DocumentViewState) -> Bool {
        openReaderViewIDs.contains(view.viewID)
    }

    func toggleReader(for view: DocumentViewState) {
        guard !SyntaxHighlighter.isYAMLFile(view.document.filePath) else {
            return
        }

        let id = Self.readerWindowID(view)
        if windows.isOpen(id) {
            windows.close(id)
            return
        }

        let viewID = view.viewID
        openReaderViewIDs.insert(viewID)
        windows.show(
            id: id,
            title: readerTitle(for: view),
            size: NSSize(width: 760, height: 820),
            minSize: NSSize(width: 420, height: 320),
            model: self,
            onClose: { [weak self] in self?.openReaderViewIDs.remove(viewID) }
        ) {
            ReaderView(source: .document(view), windowID: id)
        }
    }

    /// A tab's reader goes away with the tab.
    func closeReader(for view: DocumentViewState) {
        windows.close(Self.readerWindowID(view))
    }

    func showMarkdownGuide() {
        let id = "markdownGuide"
        windows.show(id: id, title: localization.get("help.markdownGuide"), size: NSSize(width: 640, height: 680), model: self) {
            ReaderView(source: .guide, windowID: id)
        }
    }

    func readerTitle(for view: DocumentViewState) -> String {
        let name = view.document.filePath.map { ($0 as NSString).lastPathComponent } ?? localization.get("document.untitled")
        return "\(name) — \(localization.get("preview.reader"))"
    }

    private static func readerWindowID(_ view: DocumentViewState) -> String {
        "reader-\(view.viewID.uuidString)"
    }
}
