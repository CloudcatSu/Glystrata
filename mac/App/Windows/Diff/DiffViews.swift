import GlystrataCore
import SwiftUI

/// The diff colours of the Windows build's Light.xaml and Dark.xaml (Diff*Brush), which the shared
/// theme does not carry.
extension ThemeColors {
    var diffAddedBackground: ThemeColor { ThemeColor(hex: kind == .dark ? "#20382B" : "#E8F5EC") }
    var diffAddedForeground: ThemeColor { ThemeColor(hex: kind == .dark ? "#9AD9AD" : "#1F6B3B") }
    var diffRemovedBackground: ThemeColor { ThemeColor(hex: kind == .dark ? "#432C31" : "#FBEAEC") }
    var diffRemovedForeground: ThemeColor { ThemeColor(hex: kind == .dark ? "#F2A8B0" : "#8A3440") }
    var diffUnchangedForeground: ThemeColor { ThemeColor(hex: kind == .dark ? "#B5BDC8" : "#4E5968") }
}

/// The lines of a comparison, one per row: added lines in green with "+ ", removed in red with
/// "- ", the rest in grey.
struct DiffTextView: View {
    let rows: [DiffLine]

    @Environment(\.theme) private var theme

    var body: some View {
        ScrollView {
            LazyVStack(spacing: 0) {
                ForEach(Array(rows.enumerated()), id: \.offset) { _, row in
                    Text(SnapshotPresentation.diffPrefix(row.kind) + row.text)
                        .font(.system(size: 13, design: .monospaced))
                        .foregroundStyle(foreground(row.kind))
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(EdgeInsets(top: 2, leading: 6, bottom: 2, trailing: 6))
                        .background(background(row.kind))
                }
            }
            .textSelection(.enabled)
        }
        .background(theme.surface.color)
        .overlay(Rectangle().strokeBorder(theme.border.color, lineWidth: 1))
    }

    private func foreground(_ kind: DiffLineKind) -> Color {
        switch kind {
        case .added: return theme.diffAddedForeground.color
        case .removed: return theme.diffRemovedForeground.color
        case .unchanged: return theme.diffUnchangedForeground.color
        }
    }

    private func background(_ kind: DiffLineKind) -> Color {
        switch kind {
        case .added: return theme.diffAddedBackground.color
        case .removed: return theme.diffRemovedBackground.color
        case .unchanged: return .clear
        }
    }
}

/// A comparison of two fixed texts, e.g. the local content against a file changed on disk.
struct FixedDiffView: View {
    let rows: [DiffLine]

    var body: some View {
        DiffTextView(rows: rows)
            .padding(12)
    }
}

/// A snapshot of one document compared with its current content, with a picker for the snapshot.
struct SnapshotDiffView: View {
    let history: SnapshotHistoryModel

    @Environment(AppModel.self) private var model
    @State private var rows: [DiffLine] = []

    var body: some View {
        @Bindable var history = history
        let localization = model.localization
        VStack(alignment: .leading, spacing: 8) {
            HStack(spacing: 8) {
                Text(localization.get("snapshot.compareWith"))
                Picker("", selection: $history.selectedID) {
                    ForEach(history.snapshots) { snapshot in
                        Text(SnapshotPresentation.header(for: snapshot)).tag(Optional(snapshot.id))
                    }
                }
                .labelsHidden()
                .frame(width: 320)
                Spacer()
            }
            DiffTextView(rows: rows)
        }
        .padding(12)
        .task(id: Trigger(id: history.selectedID, requests: history.compareRequests)) {
            guard let snapshot = history.selected else {
                return
            }

            // The snapshot is the older side, the current text the newer one.
            let old = snapshot.text
            let new = history.view.document.text
            rows = await Task.detached { TextDiffService().compare(old, new) }.value
            model.windows.setTitle(
                "\(localization.get("dialog.compare")) — \(SnapshotPresentation.timestamp(snapshot.createdUtc))",
                for: history.diffWindowID)
        }
    }

    private struct Trigger: Equatable {
        let id: UUID?
        let requests: Int
    }
}
