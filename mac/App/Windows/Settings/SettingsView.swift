import AppKit
import GlystrataCore
import SwiftUI
import UniformTypeIdentifiers

/// Preferences. Everything is edited in a `SettingsForm` and only reaches the app on Apply.
struct SettingsView: View {
    @State private var form: SettingsForm
    @State private var tab: Int

    @Environment(AppModel.self) private var model

    init(settings: AppSettings, systemKind: ThemeKind) {
        _form = State(initialValue: SettingsForm(settings: settings, systemKind: systemKind))
        #if DEBUG
        // Development aid: `-GlystrataSettingsTab 0...4` starts on that tab, for screenshots.
        _tab = State(initialValue: UserDefaults.standard.integer(forKey: "GlystrataSettingsTab"))
        #else
        _tab = State(initialValue: 0)
        #endif
    }

    var body: some View {
        let text = model.localization
        VStack(spacing: 0) {
            Picker("", selection: $tab) {
                Text(text.get("settings.general")).tag(0)
                Text(text.get("settings.colors")).tag(1)
                Text(text.get("settings.readerColors")).tag(2)
                Text(text.get("settings.groupColors")).tag(3)
                Text(text.get("settings.snapshots")).tag(4)
            }
            .pickerStyle(.segmented)
            .labelsHidden()
            .padding(.bottom, 12)

            Group {
                switch tab {
                case 1: EditorColorsTab(form: $form)
                case 2: ReaderColorsTab(form: $form)
                case 3: GroupColorsTab(form: $form)
                case 4: SnapshotSettingsTab(form: $form)
                default: GeneralSettingsTab(form: $form)
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)

            HStack(spacing: 6) {
                Button(text.get("settings.reset")) {
                    form.replace(with: AppSettings())
                }
                Button(text.get("settings.export")) {
                    exportSettings()
                }
                Button(text.get("settings.import")) {
                    importSettings()
                }
                Spacer()
                Button(text.get("dialog.cancel")) {
                    model.windows.close("settings")
                }
                .keyboardShortcut(.cancelAction)
                Button(text.get("settings.apply")) {
                    applyAndClose()
                }
                .keyboardShortcut(.defaultAction)
            }
            .padding(.top, 14)
        }
        .padding(18)
    }

    private func applyAndClose() {
        if let settings = validated() {
            model.applySettings(settings)
            model.windows.close("settings")
        }
    }

    /// The form's settings, or nil after telling the user which entry was wrong.
    private func validated() -> AppSettings? {
        let text = model.localization
        switch form.commit() {
        case .success(let settings):
            return settings
        case .failure(let error):
            let detail: String
            switch error {
            case .invalidInteger: detail = text.get("settings.error.invalidInteger")
            case .invalidNumber: detail = text.get("settings.error.invalidNumber")
            case .invalidColor(let value): detail = text.format("settings.error.invalidColor", value)
            }
            model.showError(text.get("settings.title"), detail: detail)
            return nil
        }
    }

    private static var settingsFileType: UTType {
        UTType(filenameExtension: "gss") ?? .json
    }

    private func exportSettings() {
        guard let settings = validated() else {
            return
        }

        let text = model.localization
        let panel = NSSavePanel()
        panel.title = text.get("settings.export")
        panel.nameFieldStringValue = "glystrata-settings.gss"
        panel.allowedContentTypes = [Self.settingsFileType]
        panel.canCreateDirectories = true
        guard panel.runModal() == .OK, let url = panel.url else {
            return
        }

        do {
            try SettingsFile.export(settings).write(to: url, options: .atomic)
        } catch {
            model.showError(text.get("settings.title"), detail: error.localizedDescription)
        }
    }

    private func importSettings() {
        let text = model.localization
        let panel = NSOpenPanel()
        panel.title = text.get("settings.import")
        panel.allowedContentTypes = [Self.settingsFileType]
        panel.canChooseDirectories = false
        panel.allowsMultipleSelection = false
        guard panel.runModal() == .OK, let url = panel.url else {
            return
        }

        let imported: AppSettings
        do {
            imported = try SettingsFile.import(Data(contentsOf: url))
        } catch let error as SettingsImportError {
            model.showError(text.get("settings.title"), detail: text.get(error.localizationKey))
            return
        } catch {
            model.showError(text.get("settings.title"), detail: error.localizedDescription)
            return
        }

        // Only the form changes; Cancel backs the import out.
        form.replace(with: imported)
        let alert = NSAlert()
        alert.messageText = text.get("settings.title")
        alert.informativeText = text.get("settings.import.success")
        alert.addButton(withTitle: text.get("dialog.ok"))
        alert.runModal()
    }
}
