import Foundation
import Testing
@testable import GlystrataCore

@Suite struct SnapshotPresentationTests {
    @Test func timestampUsesTheGivenTimeZone() throws {
        let utc = Date(timeIntervalSince1970: 1_791_000_000) // 2026-10-03 04:00:00 UTC
        let taipei = try #require(TimeZone(identifier: "Asia/Taipei"))
        let utcZone = try #require(TimeZone(identifier: "UTC"))
        #expect(SnapshotPresentation.timestamp(utc, timeZone: utcZone) == "2026-10-03 04:00:00")
        #expect(SnapshotPresentation.timestamp(utc, timeZone: taipei) == "2026-10-03 12:00:00")
    }

    @Test func headerCountsUtf16UnitsWithGrouping() throws {
        let zone = try #require(TimeZone(identifier: "UTC"))
        let snapshot = SnapshotInfo(
            id: UUID(), createdUtc: Date(timeIntervalSince1970: 0), text: String(repeating: "a", count: 1234),
            encoding: .utf8, lineEnding: .lf, sourceFingerprint: nil)
        #expect(SnapshotPresentation.header(for: snapshot, timeZone: zone) == "1970-01-01 00:00:00  ·  1,234 chars")
    }

    @Test func multiLineNoteIsOnlyRewrittenWhenTheFieldWasEdited() {
        let stored = "第一行\r\n第二行\n第三行"
        #expect(SnapshotPresentation.notePreview(stored) == "第一行 第二行 第三行")
        // Untouched field: the stored note keeps its line breaks.
        #expect(SnapshotPresentation.editedNote(stored: stored, fieldText: "第一行 第二行 第三行") == nil)
        #expect(SnapshotPresentation.editedNote(stored: stored, fieldText: "改過了") == "改過了")
        #expect(SnapshotPresentation.editedNote(stored: "", fieldText: "新註解") == "新註解")
        #expect(SnapshotPresentation.editedNote(stored: "舊", fieldText: "") == "")
    }

    @Test func diffPrefixes() {
        #expect(SnapshotPresentation.diffPrefix(.added) == "+ ")
        #expect(SnapshotPresentation.diffPrefix(.removed) == "- ")
        #expect(SnapshotPresentation.diffPrefix(.unchanged) == "  ")
    }
}
