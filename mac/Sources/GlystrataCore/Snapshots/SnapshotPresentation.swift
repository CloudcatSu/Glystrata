import Foundation

/// How snapshots and diff lines are worded on screen, kept apart from the views so it can be tested.
public enum SnapshotPresentation {
    /// "yyyy-MM-dd HH:mm:ss" in the viewer's time zone.
    public static func timestamp(_ utc: Date, timeZone: TimeZone = .current) -> String {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = timeZone
        let parts = calendar.dateComponents([.year, .month, .day, .hour, .minute, .second], from: utc)
        return String(
            format: "%04d-%02d-%02d %02d:%02d:%02d",
            parts.year ?? 0, parts.month ?? 0, parts.day ?? 0, parts.hour ?? 0, parts.minute ?? 0, parts.second ?? 0)
    }

    /// The timestamp and size shown in front of a snapshot's note. The size counts UTF-16 units,
    /// as the Windows build counts .NET string length.
    public static func header(for snapshot: SnapshotInfo, timeZone: TimeZone = .current) -> String {
        let count = snapshot.text.utf16.count.formatted(.number.grouping(.automatic).locale(Locale(identifier: "en_US")))
        return "\(timestamp(snapshot.createdUtc, timeZone: timeZone))  ·  \(count) chars"
    }

    /// A note on one line: the list row has a single-line field, so line breaks become spaces.
    public static func notePreview(_ note: String) -> String {
        note.replacingOccurrences(of: "\r\n", with: " ")
            .replacingOccurrences(of: "\r", with: " ")
            .replacingOccurrences(of: "\n", with: " ")
            .trimmingCharacters(in: .whitespacesAndNewlines)
    }

    /// What to write back after the note field was edited, or nil when nothing needs writing.
    /// The field shows `notePreview(stored)`, so an untouched field must not count as an edit —
    /// otherwise just clicking into and out of a multi-line note would flatten it on disk.
    public static func editedNote(stored: String, fieldText: String) -> String? {
        fieldText == notePreview(stored) ? nil : fieldText
    }

    public static func diffPrefix(_ kind: DiffLineKind) -> String {
        switch kind {
        case .added: return "+ "
        case .removed: return "- "
        case .unchanged: return "  "
        }
    }
}
