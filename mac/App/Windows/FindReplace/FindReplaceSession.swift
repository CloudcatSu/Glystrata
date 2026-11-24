import AppKit
import GlystrataCore
import Observation

/// What the Find and Replace window holds between uses: the two text boxes, the status line and
/// the match last visited. One exists because only one window does; reopening the window while it
/// is already up has to reach the same boxes.
@MainActor
@Observable
final class FindReplaceSession {
    static let shared = FindReplaceSession()

    enum Field {
        case find
        case replace
    }

    var query = "" {
        didSet {
            if query != oldValue {
                invalidate()
            }
        }
    }
    var replacement = ""
    var status = ""
    /// Each request moves focus into `field` and selects its text; the token makes a repeat of the
    /// same request still count as a change.
    private(set) var focusRequest = (field: Field.find, token: 0)

    /// Where the last Find, Next or Previous landed. Replace uses it only while it still matches
    /// the live text, since edits made in between would leave a stale offset pointing at the wrong place.
    @ObservationIgnored private var current: NSRange?

    func seed(selectedText: String?, focusReplace: Bool) {
        if let selectedText, !selectedText.isEmpty {
            query = selectedText
        }
        invalidate()
        focusRequest = (focusReplace ? .replace : .find, focusRequest.token + 1)
    }

    /// A fresh window starts with empty boxes, as the Windows one does.
    func reset() {
        query = ""
        replacement = ""
        invalidate()
    }

    private func invalidate() {
        current = nil
        status = ""
    }

    func navigate(forward: Bool, model: AppModel) {
        guard let editor = model.editor(for: model.activeView) else {
            return
        }

        let found = TextSearch.matches(in: editor.string, query: query)
        guard !found.isEmpty else {
            current = nil
            status = query.isEmpty ? "" : model.localization.get("findReplace.noMatches")
            return
        }

        let selection = editor.selectedRange()
        let anchor = forward && selection.length > 0 ? NSMaxRange(selection) : selection.location
        visit(TextSearch.matchIndex(in: found, anchor: anchor, forward: forward), of: found, editor: editor, model: model)
    }

    func replaceCurrent(model: AppModel) {
        guard let editor = model.editor(for: model.activeView), !query.isEmpty else {
            return
        }

        if current.map({ !TextSearch.matches(in: editor.string, query: query).contains($0) }) ?? true {
            navigate(forward: true, model: model)
        }
        guard let target = current else {
            return
        }

        let edit = TextSearch.replacing(target, with: replacement, in: editor.string)
        editor.replace(edit)

        let found = TextSearch.matches(in: editor.string, query: query)
        guard !found.isEmpty else {
            current = nil
            status = model.localization.get("findReplace.noMatches")
            return
        }
        visit(TextSearch.matchIndex(in: found, anchor: edit.caret, forward: true), of: found, editor: editor, model: model)
    }

    func replaceAll(model: AppModel) {
        guard let editor = model.editor(for: model.activeView), !query.isEmpty else {
            return
        }

        let result = TextSearch.replacingAll(in: editor.string, query: query, with: replacement)
        if let result {
            editor.replace(result.edit)
        }
        invalidate()
        status = model.localization.format("findReplace.replacedCount", result?.count ?? 0)
    }

    private func visit(_ index: Int?, of found: [NSRange], editor: EditorTextView, model: AppModel) {
        guard let index else {
            return
        }

        current = found[index]
        editor.selectAndReveal(found[index])
        status = model.localization.format("findReplace.matchCount", index + 1, found.count)
    }
}
