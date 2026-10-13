import GlystrataCore
import SwiftUI

/// One editor pane: a row of tabs over the selected tab's editor.
struct PaneView: View {
    let paneID: UUID

    @Environment(AppModel.self) private var model
    @Environment(\.theme) private var theme

    var body: some View {
        let views = model.views(in: paneID)
        let selected = model.selectedView(in: paneID)
        VStack(spacing: 0) {
            if !views.isEmpty {
                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(spacing: 0) {
                        ForEach(views) { view in
                            TabItemView(view: view, isSelected: view.viewID == selected?.viewID)
                        }
                    }
                }
                .background(theme.tabBackground.color)
            }

            if let selected {
                if model.settings.showFormattingToolbar {
                    FormattingToolbar(view: selected)
                }
                EditorView(
                    viewState: selected,
                    store: model.textStores.store(for: selected.document),
                    style: model.editorStyle,
                    zoom: model.editorZoom,
                    onSelectionChange: { selection in
                        if model.activePaneID == paneID {
                            model.selection = selection
                        }
                    },
                    onFocus: {
                        // Focus changes arrive while AppKit is mid-update; let that finish first.
                        DispatchQueue.main.async {
                            model.activate(paneID: paneID)
                        }
                    },
                    onZoom: { model.setEditorZoom($0) },
                    onEditorChange: { model.setEditor($0, for: selected.viewID) },
                    onDropFiles: { model.openIntoCurrentGroup(paths: $0) })
                    .id(selected.viewID)
            } else {
                Text(model.localization.get("editor.empty"))
                    .foregroundStyle(theme.secondaryText.color)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                    .background(theme.editorBackground.color)
                    .contentShape(Rectangle())
                    .onTapGesture {
                        model.activate(paneID: paneID)
                    }
            }
        }
    }
}

struct TabItemView: View {
    let view: DocumentViewState
    let isSelected: Bool

    @Environment(AppModel.self) private var model
    @Environment(\.theme) private var theme
    @State private var isHovered = false
    @State private var isTargeted = false

    var body: some View {
        HStack(spacing: 0) {
            if let color = groupColor {
                RoundedRectangle(cornerRadius: 1.5)
                    .fill(color)
                    .frame(width: 3, height: 14)
                    .padding(.trailing, 6)
            }

            Text(model.title(for: view))
                .lineLimit(1)
                .truncationMode(.tail)
                .frame(maxWidth: 220, alignment: .leading)
                .fixedSize(horizontal: true, vertical: false)
                .help(view.document.filePath ?? model.localization.get("document.untitled"))

            TabGlyphButton(help: model.localization.get(isReaderAvailable ? "toolbar.preview" : "toolbar.previewUnavailable")) {
                model.toggleReader(for: view)
            } label: {
                Image("Eye")
                    .renderingMode(.template)
                    .resizable()
                    .interpolation(.high)
                    .frame(width: 15, height: 15)
                    .foregroundStyle(model.isReaderOpen(for: view) ? theme.accent.color : theme.secondaryText.color)
                    .opacity(model.isReaderOpen(for: view) ? 1 : 0.62)
            }
            .disabled(!isReaderAvailable)

            TabGlyphButton(help: model.localization.get("dialog.close")) {
                Task { await model.close(view) }
            } label: {
                Text("×").font(.system(size: 16))
            }
        }
        .padding(.leading, 12)
        .padding(.trailing, 10)
        .padding(.vertical, 5)
        .foregroundStyle(isSelected ? theme.primaryText.color : theme.tabForeground.color)
        .background(background)
        .overlay(alignment: .bottom) {
            (isSelected ? theme.tabActiveBorder.color : Color.clear).frame(height: 2)
        }
        .clipShape(UnevenRoundedRectangle(topLeadingRadius: 2, topTrailingRadius: 2))
        .contentShape(Rectangle())
        .overlay { dropIndicator }
        .onHover { isHovered = $0 }
        .onTapGesture {
            model.select(view)
        }
        .contextMenu { menu }
        .onDrag {
            model.dragging = .tab(view.viewID)
            return DragItem.tab(view.viewID).itemProvider
        }
        .onDrop(of: [DragItem.pasteboardType], isTargeted: $isTargeted) { _ in
            guard case .tab(let draggedID)? = model.dragging else {
                return false
            }
            model.dragging = nil
            model.moveTab(draggedID, onto: view)
            return true
        }
    }

    /// A bar on the side the dragged tab will land: after this tab when it comes from the left,
    /// before it when it comes from the right.
    @ViewBuilder private var dropIndicator: some View {
        if isTargeted, case .tab(let draggedID)? = model.dragging, draggedID != view.viewID,
           let dragged = model.documents.findView(draggedID), dragged.paneID == view.paneID {
            theme.accent.color
                .frame(width: 2)
                .frame(maxWidth: .infinity, alignment: dragged.tabOrder < view.tabOrder ? .trailing : .leading)
        }
    }

    @ViewBuilder private var menu: some View {
        let hasFile = view.document.filePath != nil
        Button(model.localization.get("dialog.close")) {
            Task { await model.close(view) }
        }
        Button(model.localization.get("preview.reader")) {
            model.toggleReader(for: view)
        }
        .disabled(!isReaderAvailable)
        Button(model.localization.get("tab.showInFolder")) {
            model.showInFinder(view)
        }
        .disabled(!hasFile)
        Menu(model.localization.get("tab.openInNewPane")) {
            Button(model.localization.get("tab.openInNewPane.leftRight")) {
                model.openInNewPane(view, orientation: .horizontal)
            }
            Button(model.localization.get("tab.openInNewPane.topBottom")) {
                model.openInNewPane(view, orientation: .vertical)
            }
        }
        Menu(model.localization.get("group.moveTo")) {
            ForEach(model.groups.groups) { group in
                Toggle(group.name, isOn: Binding(
                    get: { view.sourceGroupID == group.id },
                    set: { _ in model.assign(view, to: group) }))
            }
        }
        Button(model.localization.get("file.snapshotHistory")) {
            model.openSnapshotHistory(for: view)
        }
        Button(model.localization.get("file.createSnapshot")) {
            Task { await model.createSnapshotNow(view) }
        }
        .disabled(!hasFile)
        Button(model.localization.get("tab.rename")) {
            model.renameDocument(view)
        }
    }

    private var background: Color {
        switch (isSelected, isHovered) {
        case (true, true): return theme.tabActiveHover.color
        case (true, false): return theme.tabActive.color
        case (false, true): return theme.tabHover.color
        case (false, false): return theme.tabBackground.color
        }
    }

    private var isReaderAvailable: Bool {
        !SyntaxHighlighter.isYAMLFile(view.document.filePath)
    }

    private var groupColor: Color? {
        guard let groupID = view.sourceGroupID, let hex = model.groups.find(groupID)?.color else {
            return nil
        }
        return NSColor(hex: hex).map { Color(nsColor: $0) }
    }
}

/// The small eye and close buttons inside a tab.
struct TabGlyphButton<Label: View>: View {
    let help: String
    let action: () -> Void
    @ViewBuilder let label: () -> Label

    @Environment(\.theme) private var theme
    @State private var isHovered = false

    var body: some View {
        Button(action: action) {
            label()
                .foregroundStyle(theme.secondaryText.color)
                .padding(.horizontal, 4)
                .frame(minHeight: 20)
                .contentShape(Rectangle())
        }
        .buttonStyle(HoverButtonStyle(isHovered: isHovered))
        .onHover { isHovered = $0 }
        .padding(.leading, 5)
        .help(help)
    }
}

/// The optional strip of Markdown formatting buttons above the editor.
struct FormattingToolbar: View {
    let view: DocumentViewState

    @Environment(AppModel.self) private var model
    @Environment(\.theme) private var theme

    private static let buttons: [(label: String, key: String, command: MarkdownFormatCommand)] = [
        ("¶", "format.paragraph", .paragraph),
        ("H1", "format.heading1", .heading1),
        ("H2", "format.heading2", .heading2),
        ("H3", "format.heading3", .heading3),
        ("H4", "format.heading4", .heading4),
        ("H5", "format.heading5", .heading5),
        ("H6", "format.heading6", .heading6),
        ("B", "format.bold", .bold),
        ("I", "format.italic", .italic),
        ("S", "format.strikethrough", .strikethrough),
        ("`", "format.inlineCode", .inlineCode),
        ("↗", "format.link", .link),
        ("•", "format.unorderedList", .unorderedList),
        ("1.", "format.orderedList", .orderedList),
        ("❯", "format.quote", .quote),
        ("```", "format.codeBlock", .codeBlock)
    ]

    var body: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 2) {
                ForEach(Self.buttons, id: \.key) { button in
                    FormatButton(
                        label: button.label,
                        help: model.localization.get(button.key),
                        isEmphasized: button.command == .bold || button.command == .heading1
                    ) {
                        model.applyFormatting(button.command, to: view)
                    }
                }
            }
            .padding(.horizontal, 6)
            .padding(.vertical, 2)
        }
        .background(theme.surface.color)
        .overlay(alignment: .bottom) {
            theme.border.color.frame(height: 1)
        }
    }
}

private struct FormatButton: View {
    let label: String
    let help: String
    let isEmphasized: Bool
    let action: () -> Void

    @Environment(\.theme) private var theme
    @State private var isHovered = false

    var body: some View {
        Button(action: action) {
            Text(label)
                .fontWeight(isEmphasized ? .semibold : .regular)
                .foregroundStyle(theme.primaryText.color)
                .padding(.horizontal, 5)
                .frame(minWidth: label.count > 2 ? 38 : 30, minHeight: 27)
                .contentShape(Rectangle())
        }
        .buttonStyle(HoverButtonStyle(isHovered: isHovered))
        .onHover { isHovered = $0 }
        .help(help)
    }
}
