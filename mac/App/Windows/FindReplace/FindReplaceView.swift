import AppKit
import SwiftUI

/// The content of the Find and Replace window. It always searches the tab that is active when a
/// button is pressed, so switching tabs while it is open retargets it.
struct FindReplaceView: View {
    @Environment(AppModel.self) private var model
    @Environment(\.theme) private var theme

    @State private var session = FindReplaceSession.shared
    @FocusState private var focused: FindReplaceSession.Field?

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            fieldRow("findReplace.find", text: $session.query, field: .find)
                .padding(.bottom, 8)
                .onSubmit {
                    session.navigate(forward: !(NSApp.currentEvent?.modifierFlags.contains(.shift) ?? false), model: model)
                }
            fieldRow("findReplace.replaceWith", text: $session.replacement, field: .replace)
                .padding(.bottom, 8)
                .onSubmit { session.replaceCurrent(model: model) }
            Text(session.status)
                .foregroundStyle(theme.secondaryText.color)
                .frame(maxWidth: .infinity, minHeight: 18, alignment: .leading)
                .padding(.bottom, 8)
            HStack(spacing: 5) {
                Button(model.localization.get("findReplace.findButton")) { session.navigate(forward: true, model: model) }
                Button(model.localization.get("findReplace.previous")) { session.navigate(forward: false, model: model) }
                Button(model.localization.get("findReplace.next")) { session.navigate(forward: true, model: model) }
                Spacer(minLength: 0)
                Button(model.localization.get("findReplace.replace")) { session.replaceCurrent(model: model) }
                Button(model.localization.get("findReplace.replaceAll")) { session.replaceAll(model: model) }
            }
        }
        .padding(16)
        .frame(width: 420)
        .background {
            // Esc closes the window from either box, like the Windows one.
            Button("") { model.windows.close("findReplace") }
                .keyboardShortcut(.cancelAction)
                .opacity(0)
        }
        .onAppear { applyFocusRequest() }
        .onChange(of: session.focusRequest.token) { applyFocusRequest() }
    }

    private func fieldRow(_ labelKey: String, text: Binding<String>, field: FindReplaceSession.Field) -> some View {
        HStack(spacing: 0) {
            Text(model.localization.get(labelKey))
                .frame(width: 90, alignment: .leading)
            TextField("", text: text)
                .textFieldStyle(.plain)
                .focused($focused, equals: field)
                .padding(.horizontal, 6)
                .padding(.vertical, 4)
                .background(theme.surface.color)
                .overlay(RoundedRectangle(cornerRadius: 3).stroke(theme.border.color))
        }
    }

    private func applyFocusRequest() {
        focused = session.focusRequest.field
        // The window may not be key yet when this runs; select only inside a text box, never the document.
        Task {
            try? await Task.sleep(for: .milliseconds(100))
            if let field = NSApp.keyWindow?.firstResponder as? NSTextView, field.isFieldEditor {
                field.selectAll(nil)
            }
        }
    }
}
