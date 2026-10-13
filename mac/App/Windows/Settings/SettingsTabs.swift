import GlystrataCore
import SwiftUI

private extension Binding where Value == [String: String] {
    /// One text field of the form, addressed by its key.
    func entry(_ key: String) -> Binding<String> {
        Binding<String>(
            get: { wrappedValue[key] ?? "" },
            set: { wrappedValue[key] = $0 })
    }
}

struct GeneralSettingsTab: View {
    @Binding var form: SettingsForm
    @Environment(AppModel.self) private var model

    var body: some View {
        let text = model.localization
        SettingsPage {
            SettingsRow(label: text.get("settings.language")) {
                Picker("", selection: $form.settings.language) {
                    Text(text.get("settings.language.zh")).tag(AppLanguage.traditionalChinese)
                    Text(text.get("settings.language.en")).tag(AppLanguage.english)
                }
                .labelsHidden()
                .frame(width: 220, alignment: .leading)
            }
            SettingsRow(label: text.get("settings.theme")) {
                Picker("", selection: Binding(get: { form.settings.theme }, set: { form.setTheme($0) })) {
                    Text(text.get("settings.theme.system")).tag(ThemePreference.system)
                    Text(text.get("settings.light")).tag(ThemePreference.light)
                    Text(text.get("settings.dark")).tag(ThemePreference.dark)
                }
                .labelsHidden()
                .frame(width: 220, alignment: .leading)
            }
            SettingsRow(label: text.get("settings.showFormattingToolbar")) {
                Toggle("", isOn: $form.settings.showFormattingToolbar)
                    .labelsHidden()
            }
            SettingsRow(label: text.get("settings.characterCountMode")) {
                Picker("", selection: $form.settings.characterCountMode) {
                    Text(text.get("status.countMode.includeWhitespace")).tag(CharacterCountMode.includeWhitespace)
                    Text(text.get("status.countMode.excludeWhitespace")).tag(CharacterCountMode.excludeWhitespace)
                    Text(text.get("status.countMode.excludeLineBreaks")).tag(CharacterCountMode.excludeLineBreaks)
                }
                .labelsHidden()
                .frame(width: 220, alignment: .leading)
            }
        }
    }
}

/// Says which theme's palette the colour tabs are editing.
private struct PaletteScopeNote: View {
    let form: SettingsForm
    @Environment(AppModel.self) private var model

    var body: some View {
        let text = model.localization
        var themeName = text.get(form.editingTheme == .dark ? "settings.dark" : "settings.light")
        if form.settings.theme == .system {
            themeName = text.format("settings.theme.systemSuffix", themeName)
        }
        return SettingsNote(text: text.format("settings.paletteScope", themeName))
    }
}

struct EditorColorsTab: View {
    @Binding var form: SettingsForm
    @Environment(AppModel.self) private var model

    var body: some View {
        SettingsPage {
            PaletteScopeNote(form: form)
            SettingsNote(text: "HEX")
            ForEach(SettingsForm.editorColorKeys, id: \.self) { key in
                ColorRow(label: model.localization.get("settings.color.\(key)"), hex: $form.editorColors.entry(key))
            }
        }
    }
}

struct ReaderColorsTab: View {
    @Binding var form: SettingsForm
    @Environment(AppModel.self) private var model

    private static let typographyRows = [
        ("h1", "settings.heading.h1"), ("h2", "settings.heading.h2"), ("h3", "settings.heading.h3"),
        ("h4", "settings.heading.h4"), ("h5", "settings.heading.h5"), ("h6", "settings.heading.h6"),
        ("line", "settings.lineSpacing"), ("paragraph", "settings.paragraphSpacing"), ("heading", "settings.headingSpacing")
    ]

    var body: some View {
        let text = model.localization
        SettingsPage {
            PaletteScopeNote(form: form)
            SettingsNote(text: text.get("settings.readerColors.help"))
            ForEach(SettingsForm.readerColorKeys, id: \.self) { key in
                ColorRow(label: text.get("settings.readerColor.\(key)"), hex: $form.readerColors.entry(key))
            }
            Text(text.get("settings.preview"))
                .fontWeight(.semibold)
                .padding(EdgeInsets(top: 14, leading: 0, bottom: 6, trailing: 0))
            ForEach(Self.typographyRows, id: \.0) { key, label in
                SettingsRow(label: text.get(label)) {
                    TextField("", text: $form.typography.entry(key))
                        .textFieldStyle(.roundedBorder)
                        .frame(width: 90)
                }
            }
        }
    }
}

struct GroupColorsTab: View {
    @Binding var form: SettingsForm
    @Environment(AppModel.self) private var model

    var body: some View {
        let text = model.localization
        SettingsPage {
            SettingsNote(text: text.get("settings.groupColors.help"))
            // Rows are identified by position, so the binding must survive an index that has just
            // been removed while SwiftUI still holds the old row.
            ForEach(form.groupColors.indices, id: \.self) { index in
                HStack(spacing: 6) {
                    ColorEntry(hex: Binding(
                        get: { form.groupColors.indices.contains(index) ? form.groupColors[index] : "" },
                        set: { if form.groupColors.indices.contains(index) { form.groupColors[index] = $0 } }))
                    Button {
                        form.removeGroupColor(at: index)
                    } label: {
                        Image(systemName: "xmark")
                            .frame(width: 18)
                    }
                    .disabled(form.groupColors.count <= 1)
                    .help(text.get("settings.groupColors.remove"))
                }
                .padding(.vertical, 2)
            }
            Button(text.get("settings.groupColors.add")) {
                form.addGroupColor()
            }
            .padding(EdgeInsets(top: 6, leading: 0, bottom: 16, trailing: 0))
            SettingsRow(label: text.get("settings.groupSelectionTint")) {
                Slider(value: $form.tintPercent, in: 0...50, step: 1)
                    .frame(width: 220, alignment: .leading)
                Text("\(Int(form.tintPercent))%")
                    .monospacedDigit()
                    .padding(.leading, 8)
            }
            SettingsHelp(text: text.get("settings.groupSelectionTint.help"))
        }
    }
}

struct SnapshotSettingsTab: View {
    @Binding var form: SettingsForm
    @Environment(AppModel.self) private var model

    var body: some View {
        let text = model.localization
        SettingsPage {
            SettingsRow(label: text.get("settings.snapshotInterval")) {
                TextField("", text: $form.snapshotInterval)
                    .textFieldStyle(.roundedBorder)
                    .frame(width: 90)
            }
            SettingsHelp(text: text.get("settings.snapshotInterval.help"))
            SettingsRow(label: text.get("settings.maxSnapshots")) {
                TextField("", text: $form.maxSnapshots)
                    .textFieldStyle(.roundedBorder)
                    .frame(width: 90)
            }
            SettingsHelp(text: text.get("settings.maxSnapshots.help"))
            SettingsRow(label: text.get("settings.hideSnapshotFiles")) {
                Toggle("", isOn: $form.settings.hideSnapshotFiles)
                    .labelsHidden()
            }
            SettingsHelp(text: text.get("settings.hideSnapshotFiles.help"))
        }
    }
}
