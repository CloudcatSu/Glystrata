import Foundation
import Testing
@testable import GlystrataCore

@MainActor
@Suite struct SnapshotTests {
    @Test func snapshotLifecycle() async throws {
        let root = try TemporaryDirectory()
        let path = try root.write("第一版", to: "snapshot.md")
        let documents = DocumentManager()
        let document = try documents.open(path)
        let snapshots = SnapshotService()

        #expect(try await snapshots.create(for: document, maxSnapshots: 2) != nil)
        // Nothing changed since the last snapshot, so there is nothing new to keep.
        #expect(try await snapshots.create(for: document, maxSnapshots: 2) == nil)
        document.updateText("第二版")
        #expect(try await snapshots.create(for: document, maxSnapshots: 2) != nil)
        document.updateText("第三版")
        #expect(try await snapshots.create(for: document, maxSnapshots: 2) != nil)

        let sidecar = SnapshotSidecarStore.sidecarPath(for: path)
        #expect(sidecar == root.file("snapshot.md.gss"))
        #expect(FileManager.default.fileExists(atPath: sidecar))
        #expect(!isHidden(sidecar))

        // A sidecar hidden by hand goes back to visible on the next write while the setting is off.
        try setHidden(sidecar, true)
        document.updateText("第四版")
        #expect(try await snapshots.create(for: document, maxSnapshots: 2) != nil)
        #expect(!isHidden(sidecar))

        let content = try readText(sidecar)
        let noticeIndex = try #require(content.range(of: "\"notice\""))
        let snapshotsIndex = try #require(content.range(of: "\"snapshots\""))
        #expect(noticeIndex.lowerBound < snapshotsIndex.lowerBound)
        #expect(content.contains("snapshot.md"))
        #expect(content.contains("第四版"))

        let listed = try await snapshots.list(path)
        #expect(listed.count == 2)
        #expect(listed.map(\.text) == ["第四版", "第三版"])
        #expect(listed[0].note == "")

        #expect(try await snapshots.updateNote(path, snapshotID: listed[0].id, note: "測試註解"))
        #expect(try await snapshots.get(path, snapshotID: listed[0].id)?.note == "測試註解")
        #expect(try await !snapshots.updateNote(path, snapshotID: UUID(), note: "不存在"))

        try await snapshots.delete(path, snapshotID: listed[0].id)
        #expect(try await snapshots.list(path).count == 1)
        try await snapshots.deleteAll(path)
        #expect(!FileManager.default.fileExists(atPath: sidecar))
    }

    @Test(arguments: [
        ("/a/b/note.md.gss", "/a/b/note.md"),
        ("/a/b/config.yaml.GSS", "/a/b/config.yaml"),
        ("/a/b/筆記.md.gss", "/a/b/筆記.md")
    ])
    func sidecarNamesLeadBackToTheirDocument(_ sidecar: String, _ document: String) {
        #expect(SnapshotSidecarStore.sourcePath(forSidecar: sidecar) == document)
        #expect(SnapshotSidecarStore.sidecarPath(for: document).lowercased() == sidecar.lowercased())
    }

    @Test(arguments: ["/a/b/note.md", "/a/b/.gss", "/a/b/note.gssx", ""])
    func otherNamesAreNotSidecars(_ path: String) {
        #expect(SnapshotSidecarStore.sourcePath(forSidecar: path) == nil)
    }

    @Test func deletingTheLastSnapshotRemovesTheSidecar() async throws {
        let root = try TemporaryDirectory()
        let path = try root.write("only", to: "single.md")
        let document = try DocumentManager().open(path)
        let snapshots = SnapshotService()
        let created = try #require(try await snapshots.create(for: document, maxSnapshots: 5))
        try await snapshots.delete(path, snapshotID: created.id)
        #expect(!FileManager.default.fileExists(atPath: SnapshotSidecarStore.sidecarPath(for: path)))
    }

    @Test func emptyDocumentWithNoHistoryIsNotSnapshotted() async throws {
        let root = try TemporaryDirectory()
        let path = try root.write("", to: "empty.md")
        let document = try DocumentManager().open(path)
        #expect(try await SnapshotService().create(for: document, maxSnapshots: 5) == nil)
        #expect(!FileManager.default.fileExists(atPath: SnapshotSidecarStore.sidecarPath(for: path)))
    }

    @Test func hiddenSettingHidesAndUnhidesTheSidecar() async throws {
        let root = try TemporaryDirectory()
        let path = try root.write("第一版", to: "snapshot-hidden.md")
        let document = try DocumentManager().open(path)
        let snapshots = SnapshotService(store: SnapshotSidecarStore(hideSidecarFiles: true))
        #expect(try await snapshots.create(for: document, maxSnapshots: 2) != nil)

        let sidecar = SnapshotSidecarStore.sidecarPath(for: path)
        #expect(FileManager.default.fileExists(atPath: sidecar))
        #expect(isHidden(sidecar))

        SnapshotSidecarStore.applyVisibility(path, hidden: false)
        #expect(!isHidden(sidecar))
    }

    // A sidecar created by a build from before the project was renamed from MDeditor to Glystrata
    // uses the old ".mdeditor-snapshots.json" suffix and must still be picked up.
    @Test func legacySidecarIsMigrated() async throws {
        let root = try TemporaryDirectory()
        let path = try root.write("舊版內容", to: "snapshot-legacy.md")
        let legacySidecar = try root.write("""
            {
              "schemaVersion": 1,
              "sourcePath": "snapshot-legacy.md",
              "snapshots": [
                { "id": "11111111-1111-1111-1111-111111111111", "createdUtc": "2026-01-01T00:00:00Z", "text": "舊版內容", "encoding": 0, "lineEnding": 1 }
              ]
            }
            """, to: ".snapshot-legacy.md.mdeditor-snapshots.json")

        let snapshots = SnapshotService()
        let migrated = try await snapshots.list(path)
        #expect(migrated.count == 1)
        #expect(migrated[0].note == "")
        #expect(migrated[0].lineEnding == .lf)
        #expect(FileManager.default.fileExists(atPath: SnapshotSidecarStore.sidecarPath(for: path)))
        #expect(!FileManager.default.fileExists(atPath: legacySidecar))
    }

    // Shaped like what System.Text.Json writes on Windows: a Windows source path, lowercase GUIDs,
    // enums as numbers and seven-digit fractional seconds. Hand-built, not captured from a real file.
    @Test func sidecarWrittenByTheWindowsBuildOpensAndSurvivesARewrite() async throws {
        let root = try TemporaryDirectory()
        let path = try root.write("新內容", to: "筆記.md")
        try root.write(#"""
            {
              "notice": "This file is generated and managed by Glystrata…",
              "schemaVersion": 1,
              "sourcePath": "C:\\Users\\cloud\\Documents\\筆記.md",
              "snapshots": [
                {
                  "id": "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
                  "createdUtc": "2026-10-09T17:10:38.1234567Z",
                  "text": "第二版\n內容",
                  "encoding": 1,
                  "lineEnding": 0,
                  "sourceFingerprint": { "length": 18, "lastWriteTimeUtc": "2026-10-09T17:10:30.7654321Z" },
                  "note": "交稿前"
                },
                {
                  "id": "9c5b94b1-35ad-49bb-b118-8e8fc24abf80",
                  "createdUtc": "2026-10-08T09:00:00Z",
                  "text": "第一版",
                  "encoding": 1,
                  "lineEnding": 0,
                  "sourceFingerprint": null,
                  "note": ""
                }
              ]
            }
            """#, to: "筆記.md.gss")

        let snapshots = SnapshotService()
        let listed = try await snapshots.list(path)
        #expect(listed.map(\.text) == ["第二版\n內容", "第一版"])
        #expect(listed[0].id == UUID(uuidString: "3F2504E0-4F89-41D3-9A0C-0305E82C3301"))
        #expect(listed[0].encoding == .utf8WithBom)
        #expect(listed[0].lineEnding == .crLf)
        #expect(listed[0].note == "交稿前")
        #expect(listed[0].sourceFingerprint?.length == 18)
        #expect(listed[1].sourceFingerprint == nil)

        // Editing the file here must keep every snapshot the other platform wrote.
        #expect(try await snapshots.updateNote(path, snapshotID: listed[1].id, note: "初稿"))
        let rewritten = try await snapshots.list(path)
        #expect(rewritten.map(\.id) == listed.map(\.id))
        #expect(rewritten.map(\.text) == listed.map(\.text))
        #expect(rewritten[1].note == "初稿")
        #expect(abs(rewritten[0].createdUtc.timeIntervalSince(listed[0].createdUtc)) < 0.001)
    }

    // A real round trip: the Mac build wrote three snapshots, the Windows build (master 38350c4)
    // opened the file, added one and changed a note, and wrote this back. Afterwards the source path
    // was changed to one without a user name and the notice cut short; the rest is as written,
    // CRLF line endings and no final newline included.
    @Test func sidecarRewrittenByTheWindowsBuildOpensHere() async throws {
        let root = try TemporaryDirectory()
        let path = try root.write("# 反向驗證用的筆記\n\n第一版內容。\n", to: "筆記.md")
        let written = #"""
            {
              "notice": "This file is generated and managed by Glystrata (a Markdown editor).",
              "schemaVersion": 1,
              "sourcePath": "C:\\GlystrataDemo\\筆記.md",
              "snapshots": [
                {
                  "id": "55a480a2-0987-475f-ad8d-994ab2ae83f7",
                  "createdUtc": "2026-10-13T17:58:57.6515273Z",
                  "text": "# 反向驗證用的筆記\n\n第一版內容。\n",
                  "encoding": 0,
                  "lineEnding": 1,
                  "sourceFingerprint": {
                    "length": 181,
                    "lastWriteTimeUtc": "2026-10-13T17:57:01.0664108Z"
                  },
                  "note": ""
                },
                {
                  "id": "92ac2807-1040-4b0e-8789-80382f88e64e",
                  "createdUtc": "2026-10-13T17:54:40.276Z",
                  "text": "Second version: added on the Mac. 第二版加了這一行。\n\n# 反向驗證用的筆記\n\n第一版內容。\nThird version: one more line at the end. 第三版在結尾多一行。",
                  "encoding": 0,
                  "lineEnding": 1,
                  "sourceFingerprint": {
                    "length": 181,
                    "lastWriteTimeUtc": "2026-10-13T17:54:36.815Z"
                  },
                  "note": ""
                },
                {
                  "id": "3a265fc3-5bdf-4490-894e-f525a2909be7",
                  "createdUtc": "2026-10-13T17:54:23.669Z",
                  "text": "Second version: added on the Mac. 第二版加了這一行。\n\n# 反向驗證用的筆記\n\n第一版內容。\n",
                  "encoding": 0,
                  "lineEnding": 1,
                  "sourceFingerprint": {
                    "length": 110,
                    "lastWriteTimeUtc": "2026-10-13T17:54:21.296Z"
                  },
                  "note": "Windows 改過的備註：第二版"
                },
                {
                  "id": "c731503e-cd02-4518-a57c-e0f03939b935",
                  "createdUtc": "2026-10-13T17:53:46.603Z",
                  "text": "# 反向驗證用的筆記\n\n第一版內容。\n",
                  "encoding": 0,
                  "lineEnding": 1,
                  "sourceFingerprint": {
                    "length": 47,
                    "lastWriteTimeUtc": "2026-10-13T17:53:31.238Z"
                  },
                  "note": ""
                }
              ]
            }
            """#
        try root.write(written.replacingOccurrences(of: "\n", with: "\r\n"), to: "筆記.md.gss")

        let snapshots = SnapshotService()
        let listed = try await snapshots.list(path)
        #expect(listed.map(\.id) == [
            "55A480A2-0987-475F-AD8D-994AB2AE83F7", "92AC2807-1040-4B0E-8789-80382F88E64E",
            "3A265FC3-5BDF-4490-894E-F525A2909BE7", "C731503E-CD02-4518-A57C-E0F03939B935"
        ].map { UUID(uuidString: $0) })
        #expect(listed.map(\.note) == ["", "", "Windows 改過的備註：第二版", ""])
        #expect(listed.map { $0.text.count } == [19, 115, 64, 19])
        #expect(listed.map(\.sourceFingerprint?.length) == [181, 181, 110, 47])
        // Seven fractional digits from Windows beside three from the Mac, newest first.
        #expect(listed[0].createdUtc.timeIntervalSince(listed[1].createdUtc) > 257.3)
        #expect(listed[0].createdUtc.timeIntervalSince(listed[1].createdUtc) < 257.4)
        #expect(listed[3].text == "# 反向驗證用的筆記\n\n第一版內容。\n")

        #expect(try await snapshots.updateNote(path, snapshotID: listed[0].id, note: "Mac 再改一次"))
        let rewritten = try await snapshots.list(path)
        #expect(rewritten.map(\.id) == listed.map(\.id))
        #expect(rewritten.map(\.text) == listed.map(\.text))
        #expect(rewritten.map(\.note) == ["Mac 再改一次", "", "Windows 改過的備註：第二版", ""])
    }

    @Test func corruptSidecarIsReportedNotSilentlyReplaced() async throws {
        let root = try TemporaryDirectory()
        let path = try root.write("x", to: "broken.md")
        try root.write("{ not json", to: "broken.md.gss")
        let snapshots = SnapshotService()
        await #expect(throws: GlystrataError.corruptSnapshotSidecar) {
            try await snapshots.list(path)
        }
        // The unreadable history is left on disk for the user to recover, not overwritten.
        let document = try DocumentManager().open(path)
        await #expect(throws: GlystrataError.corruptSnapshotSidecar) {
            try await snapshots.create(for: document, maxSnapshots: 5)
        }
        #expect(try readText(root.file("broken.md.gss")) == "{ not json")
    }

    @Test func sidecarFollowsARename() async throws {
        let root = try TemporaryDirectory()
        let oldPath = try root.write("內容", to: "old.md")
        let document = try DocumentManager().open(oldPath)
        let snapshots = SnapshotService()
        #expect(try await snapshots.create(for: document, maxSnapshots: 5) != nil)

        let newPath = root.file("new.md")
        try FileManager.default.moveItem(atPath: oldPath, toPath: newPath)
        SnapshotSidecarStore.renameSidecar(from: oldPath, to: newPath)
        #expect(try await snapshots.list(newPath).count == 1)
        #expect(!FileManager.default.fileExists(atPath: root.file("old.md.gss")))
    }
}
