import AppKit
import GlystrataCore

extension AppModel {
    func openFindReplace(focusReplace: Bool) {
        guard let view = activeView else {
            return
        }

        let selection = editor(for: view).flatMap { editor -> String? in
            let range = editor.selectedRange()
            return range.length > 0 ? (editor.string as NSString).substring(with: range) : nil
        }
        FindReplaceSession.shared.seed(selectedText: selection, focusReplace: focusReplace)
        windows.show(
            id: "findReplace",
            title: localization.get("findReplace.title"),
            size: NSSize(width: 420, height: 146),
            isResizable: false,
            isFloating: true,
            model: self,
            onClose: { FindReplaceSession.shared.reset() }
        ) {
            FindReplaceView()
        }
    }
}
