import Foundation
import Observation

/// Interface text in the two supported languages, switchable while the app runs and independent of
/// the system language.
@MainActor
@Observable
public final class LocalizationService {
    public private(set) var language: AppLanguage

    public init(language: AppLanguage = .traditionalChinese) {
        self.language = language
    }

    /// Returns the key itself when there is no text for it, so a missing entry is visible on screen.
    public func get(_ key: String) -> String {
        Self.text(key, language: language) ?? key
    }

    public func apply(_ language: AppLanguage) {
        if self.language != language {
            self.language = language
        }
    }

    public nonisolated static func text(_ key: String, language: AppLanguage) -> String? {
        switch language {
        case .traditionalChinese:
            return macTraditionalChinese[key] ?? LocalizationTable.traditionalChinese[key]
        case .english:
            return macEnglish[key] ?? LocalizationTable.english[key]
        }
    }

    // Strings that name Windows keys or Windows file handling. Kept apart from the generated table
    // so that table can be regenerated from the Windows source without losing these.
    private nonisolated static let macTraditionalChinese: [String: String] = [
        "editor.empty": "按 ⌘N 新增檔案，或按 ⌘O 開啟檔案",
        "settings.hideSnapshotFiles.help": "快照存放在文件旁的「檔名.gss」。勾選後會設為隱藏檔，Finder 預設看不到；取消勾選則恢復為一般檔案。設定會套用到之後寫入的快照檔，程式啟動及變更此設定時，也會一併轉換開啟中文件與群組內檔案的既有快照檔。移動或重新命名文件時，請把快照檔一起移動／改名，否則會找不到快照紀錄。隱藏狀態只在這台 Mac 有效，透過雲端同步到其他裝置後不會保留。"
    ]

    private nonisolated static let macEnglish: [String: String] = [
        "editor.empty": "Press ⌘N to create a file, or ⌘O to open one",
        "settings.hideSnapshotFiles.help": "Snapshots are stored next to the document as \"<file name>.gss\". When checked, the file is hidden so Finder doesn't show it by default; when unchecked it is a normal file. The setting applies to snapshot files written from now on, and existing snapshot files of open documents and files in groups are converted on startup and whenever this setting changes. When you move or rename a document, move or rename its snapshot file with it, or the snapshot history will not be found. The hidden state is local to this Mac and is not kept when the file syncs to other devices."
    ]
}
