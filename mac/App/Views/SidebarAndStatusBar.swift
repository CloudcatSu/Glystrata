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
            }
            .padding(EdgeInsets(top: 10, leading: 12, bottom: 8, trailing: 10))

            theme.border.color.frame(height: 1)
                .padding(.bottom, 6)

            SidebarRow(title: model.localization.get("sidebar.allDocuments"), color: nil, isSelected: model.selectedGroupID == nil) {
                model.selectedGroupID = nil
            }
            .padding(.bottom, 6)

            ScrollView {
                VStack(alignment: .leading, spacing: 0) {
                    ForEach(model.groups.groups) { group in
                        SidebarRow(
                            title: group.name,
                            color: group.color.flatMap { NSColor(hex: $0) }.map { Color(nsColor: $0) },
                            isSelected: model.selectedGroupID == group.id
                        ) {
                            model.selectedGroupID = group.id
                        }
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

struct SidebarRow: View {
    let title: String
    let color: Color?
    let isSelected: Bool
    let action: () -> Void

    @Environment(\.theme) private var theme
    @State private var isHovered = false

    var body: some View {
        Button(action: action) {
            HStack(spacing: 6) {
                if let color {
                    RoundedRectangle(cornerRadius: 1.5).fill(color).frame(width: 3, height: 14)
                }
                Text(title).lineLimit(1)
                Spacer(minLength: 0)
            }
            .padding(.horizontal, 10)
            .frame(height: 30)
            .background(
                RoundedRectangle(cornerRadius: 4)
                    .fill(isSelected ? theme.tabActive.color : (isHovered ? theme.buttonHover.color : .clear)))
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .onHover { isHovered = $0 }
        .padding(.horizontal, 8)
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

        let state = document.isModified
            ? model.localization.get("status.modified")
            : (model.statusOverride ?? model.localization.get("status.saved"))
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
