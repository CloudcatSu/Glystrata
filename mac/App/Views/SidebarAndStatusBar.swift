import AppKit
import GlystrataCore
import SwiftUI

struct SidebarView: View {
    @Environment(AppModel.self) private var model
    @Environment(\.theme) private var theme

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack {
                Text(model.localization.get("sidebar.groups"))
                    .font(.system(size: 16, weight: .semibold))
                Spacer()
                SidebarAddButton(help: model.localization.get("group.new")) {
                    model.newGroup()
                }
            }
            .padding(EdgeInsets(top: 10, leading: 12, bottom: 8, trailing: 10))

            theme.border.color.frame(height: 1)
                .padding(.bottom, 6)

            SidebarRow(
                title: model.localization.get("sidebar.allDocuments"),
                barColor: nil,
                isGroup: false,
                isSemibold: false,
                selectionColor: model.selectedGroupID == nil ? theme.tabActive.color : nil
            ) {
                model.selectGroup(nil)
            }
            .padding(.bottom, 6)

            ScrollView {
                VStack(alignment: .leading, spacing: 0) {
                    ForEach(model.groups.groups) { group in
                        GroupRow(group: group)
                    }
                }
            }

            Spacer(minLength: 0)
        }
        .frame(maxHeight: .infinity)
        .background(theme.sidebar.color)
        .overlay(alignment: .trailing) {
            theme.border.color.frame(width: 1)
        }
    }
}

private struct SidebarAddButton: View {
    let help: String
    let action: () -> Void

    @Environment(\.theme) private var theme
    @State private var isHovered = false

    var body: some View {
        Button(action: action) {
            PlusGlyph()
                .stroke(theme.secondaryText.color, lineWidth: 1)
                .frame(width: 9, height: 9)
                .frame(width: 26, height: 24)
                .contentShape(Rectangle())
        }
        .buttonStyle(HoverButtonStyle(isHovered: isHovered))
        .onHover { isHovered = $0 }
        .help(help)
    }
}

/// One group in the sidebar: click to filter the tabs, right-click for its menu, drag to reorder,
/// and a drop target for tabs and other groups.
private struct GroupRow: View {
    let group: GlystrataCore.Group

    @Environment(AppModel.self) private var model
    @Environment(\.theme) private var theme
    @State private var isTargeted = false

    var body: some View {
        let isSelected = model.selectedGroupID == group.id
        VStack(spacing: 0) {
            // Inset rule separating each group from the row above it.
            theme.border.color.frame(height: 1).padding(.horizontal, 9)
            SidebarRow(
                title: group.name,
                barColor: NSColor(hex: group.color ?? "").map { Color(nsColor: $0) },
                isGroup: true,
                isSemibold: true,
                selectionColor: isSelected ? selectionColor : nil
            ) {
                model.selectGroup(group.id)
            }
        }
        .overlay { dropIndicator }
        .contextMenu { menu }
        .onDrag {
            model.dragging = .group(group.id)
            return DragItem.group(group.id).itemProvider
        }
        .onDrop(of: [DragItem.pasteboardType], isTargeted: $isTargeted) { _ in
            guard let item = model.dragging else {
                return false
            }
            model.dragging = nil
            model.drop(item, on: group)
            return true
        }
    }

    /// The group's own colour mixed into the sidebar background, so the name stays readable in
    /// both themes; a group without a colour uses the same highlight as "All documents".
    private var selectionColor: Color {
        let tint = min(max(model.settings.groupSelectionTint, 0), 1)
        guard tint > 0, let accent = NSColor(hex: group.color ?? ""), let blended = accent.blended(withFraction: 1 - tint, of: theme.sidebar.nsColor) else {
            return theme.tabActive.color
        }
        return Color(nsColor: blended)
    }

    @ViewBuilder private var dropIndicator: some View {
        if isTargeted, let item = model.dragging {
            switch item {
            case .tab(let viewID):
                if model.documents.findView(viewID)?.sourceGroupID != group.id {
                    RoundedRectangle(cornerRadius: 4)
                        .strokeBorder(theme.accent.color, lineWidth: 2)
                        .padding(.horizontal, 8)
                }
            case .group(let draggedID):
                if draggedID != group.id,
                   let from = model.groups.groups.firstIndex(where: { $0.id == draggedID }),
                   let to = model.groups.groups.firstIndex(where: { $0.id == group.id }) {
                    // The dragged group takes this one's place, so the bar sits on the side it will land.
                    theme.accent.color
                        .frame(height: 2)
                        .padding(.horizontal, 8)
                        .frame(maxHeight: .infinity, alignment: from < to ? .bottom : .top)
                }
            }
        }
    }

    @ViewBuilder private var menu: some View {
        Button(model.localization.get("group.openFile")) {
            model.openFiles(into: group)
        }
        Menu(model.localization.get("group.color")) {
            ForEach(model.settings.groupColors, id: \.self) { color in
                // The swatches have no names, so the value itself is the label.
                Toggle(isOn: Binding(
                    get: { group.color?.caseInsensitiveCompare(color) == .orderedSame },
                    set: { _ in model.setColor(color, for: group) })
                ) {
                    Label {
                        Text(color)
                    } icon: {
                        Image(nsImage: .swatch(NSColor(hex: color) ?? .gray))
                    }
                }
            }
            Divider()
            Toggle(model.localization.get("group.color.none"), isOn: Binding(
                get: { group.color == nil },
                set: { _ in model.setColor(nil, for: group) }))
            Button(model.localization.get("group.color.manage")) {
                model.openSettings()
            }
        }
        Button(model.localization.get("group.rename")) {
            model.renameGroup(group)
        }
        Button(model.localization.get("group.delete")) {
            model.deleteGroup(group)
        }
    }
}

extension NSImage {
    /// A small rounded square of one colour, for menus.
    static func swatch(_ color: NSColor) -> NSImage {
        NSImage(size: NSSize(width: 12, height: 12), flipped: false) { rect in
            color.setFill()
            NSBezierPath(roundedRect: rect, xRadius: 2, yRadius: 2).fill()
            return true
        }
    }
}

struct SidebarRow: View {
    let title: String
    let barColor: Color?
    /// A group row has the measurements of the Windows build's tree rows: taller, indented past
    /// the column that build keeps for an expander, and highlighted with square corners. It keeps
    /// the bar's space even without a colour, so group names stay aligned with each other.
    let isGroup: Bool
    let isSemibold: Bool
    /// Nil when the row is not selected.
    let selectionColor: Color?
    let action: () -> Void

    @Environment(\.theme) private var theme
    @State private var isHovered = false

    var body: some View {
        HStack(spacing: 0) {
            if isGroup {
                RoundedRectangle(cornerRadius: 1.5)
                    .fill(barColor ?? .clear)
                    .frame(width: 3, height: 15)
                    .padding(.leading, 1)
                    .padding(.trailing, 7)
            }
            Text(title)
                .fontWeight(isSemibold ? .semibold : .regular)
                .lineLimit(1)
            Spacer(minLength: 0)
        }
        .padding(.leading, isGroup ? 21 : 10)
        .padding(.trailing, 10)
        .frame(height: isGroup ? 34 : 30)
        .background(
            RoundedRectangle(cornerRadius: isGroup ? 0 : 4)
                .fill(selectionColor ?? (isHovered ? theme.buttonHover.color : .clear)))
        .contentShape(Rectangle())
        .onHover { isHovered = $0 }
        .onTapGesture(perform: action)
        .padding(.horizontal, isGroup ? 9 : 8)
    }
}

struct StatusBarView: View {
    @Environment(AppModel.self) private var model
    @Environment(\.theme) private var theme

    var body: some View {
        let view = model.activeView
        HStack(spacing: 0) {
            // The folder path takes whatever the right-hand group leaves, trimming when narrow.
            Text(view?.document.filePath.map { ($0 as NSString).deletingLastPathComponent } ?? "")
                .lineLimit(1)
                .truncationMode(.tail)
                .help(view?.document.filePath ?? "")
                .padding(.leading, 8)
                .padding(.trailing, 20)
            Spacer(minLength: 0)

            Text(documentInfo(view)).padding(.horizontal, 8)
            separator
            Text(characterCount(view))
                .padding(.horizontal, 8)
                .help(model.localization.get(countModeKey))
            separator
            if view != nil {
                Text(position).padding(.horizontal, 8)
            }
            separator
            Slider(
                value: Binding(get: { model.editorZoom * 100 }, set: { model.setEditorZoom($0 / 100) }),
                in: 50...200,
                step: 10)
                .controlSize(.mini)
                .frame(width: 110)
                .help(model.localization.get("status.zoom"))
            Text("\(Int((model.editorZoom * 100).rounded()))%")
                .frame(width: 44, alignment: .trailing)
                .padding(.leading, 6)
                .padding(.trailing, 10)
                .help(model.localization.get("status.zoom"))
                .onTapGesture(count: 2) {
                    model.setEditorZoom(1)
                }
        }
        .lineLimit(1)
        .foregroundStyle(theme.secondaryText.color)
        .frame(height: 26)
        .background(theme.surface.color)
    }

    private var separator: some View {
        theme.border.color.frame(width: 1, height: 14)
    }

    private func documentInfo(_ view: DocumentViewState?) -> String {
        guard let document = view?.document else {
            return model.statusOverride ?? model.localization.get("status.noFile")
        }

        let state: String
        if model.missingFiles.contains(document.sessionID) {
            state = model.localization.get("status.fileMissing")
        } else if document.isModified {
            state = model.localization.get("status.modified")
        } else {
            state = model.statusOverride ?? model.localization.get("status.saved")
        }
        return "\(state) · \(encodingName(document.encoding)) · \(lineEndingName(document.lineEnding))"
    }

    private func characterCount(_ view: DocumentViewState?) -> String {
        let label = model.localization.get("status.characters")
        guard let document = view?.document else {
            return "\(label) —"
        }

        let total = TextMetrics.countCharacters(document.text, mode: model.settings.characterCountMode)
        if let selected = model.selection.selectedText {
            let count = TextMetrics.countCharacters(selected, mode: model.settings.characterCountMode)
            return "\(label) \(total) · \(model.localization.get("status.selectedCharacters")) \(count)"
        }
        return "\(label) \(total)"
    }

    private var position: String {
        model.localization.format("status.position", model.selection.line, model.selection.column)
    }

    private var countModeKey: String {
        switch model.settings.characterCountMode {
        case .includeWhitespace: return "status.countMode.includeWhitespace"
        case .excludeWhitespace: return "status.countMode.excludeWhitespace"
        case .excludeLineBreaks: return "status.countMode.excludeLineBreaks"
        }
    }

    // The Windows status bar prints its enum names as they are; these are the same spellings.
    private func encodingName(_ encoding: FileEncodingKind) -> String {
        switch encoding {
        case .utf8: return "Utf8"
        case .utf8WithBom: return "Utf8WithBom"
        case .utf16LittleEndian: return "Utf16LittleEndian"
        }
    }

    private func lineEndingName(_ lineEnding: LineEndingKind) -> String {
        switch lineEnding {
        case .crLf: return "CrLf"
        case .lf: return "Lf"
        case .cr: return "Cr"
        }
    }
}
