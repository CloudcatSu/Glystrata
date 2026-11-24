import GlystrataCore
import SwiftUI

struct SnapshotHistoryView: View {
    let history: SnapshotHistoryModel

    @Environment(AppModel.self) private var model
    @Environment(\.theme) private var theme

    var body: some View {
        let localization = model.localization
        let hasSelection = history.selected != nil
        VStack(spacing: 0) {
            ScrollViewReader { proxy in
                ScrollView {
                    LazyVStack(spacing: 0) {
                        if history.isLoaded && history.snapshots.isEmpty {
                            Text(localization.get("snapshot.none"))
                                .foregroundStyle(theme.secondaryText.color)
                                .frame(maxWidth: .infinity, alignment: .leading)
                                .padding(EdgeInsets(top: 7, leading: 8, bottom: 7, trailing: 8))
                        }
                        ForEach(history.snapshots) { snapshot in
                            SnapshotRow(history: history, snapshot: snapshot)
                                .id(snapshot.id)
                        }
                    }
                }
                .background(theme.surface.color)
                .overlay(Rectangle().strokeBorder(theme.border.color, lineWidth: 1))
                .onChange(of: history.selectedID) { _, id in
                    if let id {
                        proxy.scrollTo(id)
                    }
                }
            }

            HStack(spacing: 10) {
                Button(localization.get("snapshot.deleteAll")) {
                    Task { await history.deleteAll() }
                }
                .foregroundStyle(theme.diffRemovedForeground.color)
                .disabled(history.snapshots.isEmpty)

                Spacer()

                Button(localization.get("file.createSnapshot")) {
                    Task { await history.createSnapshot() }
                }
                Button(localization.get("snapshot.compare")) {
                    history.compareSelected()
                }
                .disabled(!hasSelection)
                Button(localization.get("snapshot.restore")) {
                    Task { await history.restoreSelected() }
                }
                .disabled(!hasSelection)
                Button(localization.get("snapshot.delete")) {
                    Task { await history.deleteSelected() }
                }
                .disabled(!hasSelection)
            }
            .padding(.top, 12)
        }
        .padding(16)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .task { await history.refresh() }
    }
}

/// One row: the timestamp and size as fixed text, then the note as a borderless field sitting
/// where the note is read, so there is no looking in one place and typing in another.
private struct SnapshotRow: View {
    let history: SnapshotHistoryModel
    let snapshot: SnapshotInfo

    @Environment(AppModel.self) private var model
    @Environment(\.theme) private var theme
    @FocusState private var isEditing: Bool

    var body: some View {
        let text = Binding(
            get: { history.draft(for: snapshot) },
            set: { history.drafts[snapshot.id] = $0 })
        let isSelected = history.selectedID == snapshot.id
        HStack(spacing: 0) {
            Text(SnapshotPresentation.header(for: snapshot) + "  ·  ")
                .lineLimit(1)
                .fixedSize()
            ZStack(alignment: .leading) {
                if text.wrappedValue.isEmpty {
                    // Fainter than ordinary secondary text: it repeats on every row and is only a prompt.
                    Text(model.localization.get("snapshot.notePlaceholder"))
                        .foregroundStyle(theme.secondaryText.color.opacity(0.55))
                        .allowsHitTesting(false)
                }
                TextField("", text: text)
                    .textFieldStyle(.plain)
                    .focused($isEditing)
                    .onSubmit { isEditing = false }
            }
        }
        .padding(EdgeInsets(top: 7, leading: 8, bottom: 7, trailing: 8))
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(isSelected ? theme.tabActive.color : Color.clear)
        .contentShape(Rectangle())
        .onTapGesture {
            history.selectedID = snapshot.id
        }
        .onChange(of: isEditing) { _, editing in
            // The buttons below act on the selection, so editing a note has to select its row.
            if editing {
                history.selectedID = snapshot.id
            } else {
                Task { await history.saveNote(of: snapshot.id) }
            }
        }
    }
}
