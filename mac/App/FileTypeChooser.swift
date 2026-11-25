import AppKit
import GlystrataCore
import UniformTypeIdentifiers

/// The file-type pop-up of the Windows open and save dialogs, which the Mac panels do not have on
/// their own: a menu under the panel that narrows what it lists, or which extension a save gets.
@MainActor
final class FileTypeChooser: NSObject {
    struct Choice {
        let titleKey: String
        /// Empty means every file.
        let extensions: [String]
    }

    static let documents = [
        Choice(titleKey: "dialog.fileType.markdownAndText", extensions: ["md", "markdown", "mdown", "yaml", "yml", "txt"]),
        Choice(titleKey: "dialog.fileType.all", extensions: [])
    ]

    static let saving = [
        Choice(titleKey: "dialog.fileType.markdown", extensions: ["md"]),
        Choice(titleKey: "dialog.fileType.yaml", extensions: ["yaml", "yml"]),
        Choice(titleKey: "dialog.fileType.all", extensions: [])
    ]

    private let panel: NSSavePanel
    private let choices: [Choice]

    /// Puts the pop-up under `panel` with the first choice selected. Keep the chooser alive while
    /// the panel runs; the panel holds only its view.
    init(panel: NSSavePanel, choices: [Choice], localization: LocalizationService) {
        self.panel = panel
        self.choices = choices
        super.init()

        let popUp = NSPopUpButton(frame: .zero, pullsDown: false)
        popUp.addItems(withTitles: choices.map { localization.get($0.titleKey) })
        popUp.target = self
        popUp.action = #selector(choose(_:))
        let row = NSStackView(views: [NSTextField(labelWithString: localization.get("dialog.fileType")), popUp])
        row.edgeInsets = NSEdgeInsets(top: 8, left: 12, bottom: 8, right: 12)
        panel.accessoryView = row
        (panel as? NSOpenPanel)?.isAccessoryViewDisclosed = true
        // A name typed with another extension is kept (after the panel asks), as on Windows.
        panel.allowsOtherFileTypes = true
        apply(0)
    }

    @objc private func choose(_ sender: NSPopUpButton) {
        apply(sender.indexOfSelectedItem)
    }

    private func apply(_ index: Int) {
        let extensions = choices[index].extensions
        panel.allowedContentTypes = extensions.compactMap { UTType(filenameExtension: $0) }
        // The system would name a YAML file ".yml"; Windows uses the first extension of the choice.
        if !(panel is NSOpenPanel), let first = extensions.first {
            let name = panel.nameFieldStringValue as NSString
            if !extensions.contains(name.pathExtension.lowercased()) {
                panel.nameFieldStringValue = (name.deletingPathExtension as NSString).appendingPathExtension(first) ?? name as String
            }
        }
    }
}
