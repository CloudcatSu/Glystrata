import Foundation
import Testing
@testable import GlystrataCore

@MainActor
@Suite struct GroupTests {
    @Test func groupsHoldLinksNotFiles() throws {
        let root = try TemporaryDirectory()
        let file = try root.write("# group", to: "group.md")
        let directory = root.file("group-folder")
        try FileManager.default.createDirectory(atPath: directory, withIntermediateDirectories: true)

        let manager = GroupManager()
        let first = manager.createGroup("第一群組")
        let second = manager.createGroup("第二群組")
        manager.addPath(first.id, path: file, kind: .file)
        manager.addPath(first.id, path: directory, kind: .folder)
        manager.addPath(second.id, path: file, kind: .file)
        manager.addPath(first.id, path: file, kind: .file)

        #expect(first.items.count == 2)
        #expect(second.items.count == 1)
        #expect(manager.movePath(from: first.id, to: second.id, path: file))
        #expect(first.items.count == 1)
        #expect(second.items.count == 1)
        #expect(FileManager.default.fileExists(atPath: file))
    }

    @Test func kindIsInferredAndMissingPathsAreRejected() throws {
        let root = try TemporaryDirectory()
        let file = try root.write("x", to: "a.md")
        let manager = GroupManager()
        let group = manager.createGroup("  ")
        #expect(group.name == "未命名群組")
        #expect(manager.addPath(group.id, path: file)?.kind == .file)
        #expect(manager.addPath(group.id, path: root.path)?.kind == .folder)
        #expect(manager.addPath(group.id, path: root.file("nope.md")) == nil)
    }

    @Test func colourIsStoredClearedAndPersisted() {
        let manager = GroupManager()
        let first = manager.createGroup("第一群組")
        let second = manager.createGroup("第二群組")

        #expect(first.color == nil)
        #expect(manager.setGroupColor(first.id, color: "#2DA44E"))
        #expect(first.color == "#2DA44E")
        #expect(manager.setGroupColor(first.id, color: nil))
        #expect(first.color == nil)
        // Whitespace has to clear the colour rather than be stored: a blank bar would still take layout space.
        manager.setGroupColor(first.id, color: "   ")
        #expect(first.color == nil)

        manager.setGroupColor(second.id, color: "#CF222E")
        let restored = GroupManager()
        GroupsState.from(manager.groups).apply(to: restored)
        #expect(restored.find(second.id)?.color == "#CF222E")
        #expect(restored.find(first.id) != nil)
        #expect(restored.find(first.id)?.color == nil)
    }

    @Test func paletteEditsCarryOverByPosition() {
        let manager = GroupManager()
        let onFirst = manager.createGroup("first")
        let onSecond = manager.createGroup("second")
        let onRemoved = manager.createGroup("removed")
        let offPalette = manager.createGroup("custom")
        let uncoloured = manager.createGroup("none")
        manager.setGroupColor(onFirst.id, color: "#3b6ff5")
        manager.setGroupColor(onSecond.id, color: "#2DA44E")
        manager.setGroupColor(onRemoved.id, color: "#CF222E")
        manager.setGroupColor(offPalette.id, color: "#123456")

        // Swapping two swatches must not chain: the group on the first one ends on the second colour, not back where it started.
        #expect(manager.remapColors(oldPalette: ["#3B6FF5", "#2DA44E", "#CF222E"], newPalette: ["#2DA44E", "#3B6FF5"]))
        #expect(onFirst.color == "#2DA44E")
        #expect(onSecond.color == "#3B6FF5")
        #expect(onRemoved.color == "#CF222E")
        #expect(offPalette.color == "#123456")
        #expect(uncoloured.color == nil)
        #expect(!manager.remapColors(oldPalette: ["#2DA44E"], newPalette: ["#2DA44E"]))
    }

    @Test func folderListingHidesHiddenFilesAndSnapshotSidecars() throws {
        let root = try TemporaryDirectory()
        try root.write("", to: "b.md")
        try root.write("", to: "A.md")
        try root.write("", to: "A.md.gss")
        try root.write("", to: ".dotfile")
        let flagged = try root.write("", to: "flagged.md")
        try setHidden(flagged, true)
        try FileManager.default.createDirectory(atPath: root.file("zeta"), withIntermediateDirectories: true)

        let entries = GroupManager().enumerateFolder(root.path)
        #expect(entries.map(\.name) == ["zeta", "A.md", "b.md"])
        #expect(entries.first?.kind == .folder)
    }
}

@Suite struct PersistenceTests {
    @Test func settingsRoundTrip() async throws {
        let root = try TemporaryDirectory()
        let store = JSONStateStore(baseDirectory: root.file("state"))
        var settings = AppSettings()
        settings.language = .english
        settings.snapshotIntervalMinutes = 8
        settings.maxSnapshotsPerFile = 12
        settings.characterCountMode = .excludeLineBreaks
        settings.showFormattingToolbar = true
        settings.useGlassIcon = true
        try await store.saveSettings(settings)

        let loaded = await store.loadSettings()
        #expect(loaded == settings)
        #expect(store.loadSettingsForStartup() == settings)
    }

    @Test func corruptSettingsFallBackAndKeepADiagnosticCopy() async throws {
        let root = try TemporaryDirectory()
        let store = JSONStateStore(baseDirectory: root.path)
        try root.write("{ broken json", to: "settings.json")

        let fallback = await store.loadSettings()
        #expect(fallback.language == .traditionalChinese)
        let copies = try FileManager.default.contentsOfDirectory(atPath: root.path)
            .filter { $0.hasPrefix("settings.json.corrupt-") && $0.hasSuffix(".json") }
        #expect(copies.count == 1)
    }

    @Test func corruptSettingsAtStartupAlsoKeepADiagnosticCopy() throws {
        let root = try TemporaryDirectory()
        let store = JSONStateStore(baseDirectory: root.path)
        try root.write("{ broken json", to: "settings.json")

        #expect(store.loadSettingsForStartup() == AppSettings())
        let copies = try FileManager.default.contentsOfDirectory(atPath: root.path)
            .filter { $0.hasPrefix("settings.json.corrupt-") && $0.hasSuffix(".json") }
        #expect(copies.count == 1)
    }

    // settings.json stores enums as plain numbers, so ThemePreference's numeric values are a
    // compatibility contract: a file written before "follow the system" existed must keep its theme.
    @Test(arguments: [
        (#"{ "schemaVersion": 1, "theme": 1 }"#, ThemePreference.dark),
        (#"{ "schemaVersion": 1, "theme": 0 }"#, ThemePreference.light),
        (#"{ "schemaVersion": 1 }"#, ThemePreference.system),
        (#"{ "schemaVersion": 1, "theme": 99 }"#, ThemePreference.system)
    ])
    func themeNumbersKeepTheirMeaning(json: String, expected: ThemePreference) async throws {
        let root = try TemporaryDirectory()
        try root.write(json, to: "settings.json")
        let loaded = await JSONStateStore(baseDirectory: root.path).loadSettings()
        #expect(loaded.theme == expected)
    }

    @Test func outOfRangeSettingsAreClampedAndHandEditedFilesLoad() async throws {
        let root = try TemporaryDirectory()
        try root.write("""
            {
              // edited by hand
              "snapshotIntervalMinutes": 0,
              "maxSnapshotsPerFile": 5000,
              "groupColors": [],
              "groupSelectionTint": 3,
            }
            """, to: "settings.json")
        let loaded = await JSONStateStore(baseDirectory: root.path).loadSettings()
        #expect(loaded.snapshotIntervalMinutes == 1)
        #expect(loaded.maxSnapshotsPerFile == 200)
        #expect(loaded.groupColors == GroupColorPalette.defaults)
        #expect(loaded.groupSelectionTint == 0.5)
        #expect(loaded.darkEditorPalette == .darkDefault)
    }

    @Test func sessionRoundTrip() async throws {
        let root = try TemporaryDirectory()
        let store = JSONStateStore(baseDirectory: root.path)
        let paneID = UUID()
        var session = SessionState()
        session.rootNodeId = paneID
        session.nodes = [PaneNodeState(id: paneID)]
        var view = SessionViewState(viewId: UUID(), path: "/tmp/a.md", sourceGroupId: nil, paneId: paneID)
        view.caretOffset = 42
        view.verticalOffset = 310.5
        session.views = [view]
        session.activeViewId = view.viewId
        try await store.saveSession(session)
        #expect(await store.loadSession() == session)
    }

    @Test func datesFromTheWindowsBuildParse() throws {
        let reference = try #require(CoreJSON.parseDate("2026-01-01T00:00:00Z"))
        #expect(reference.timeIntervalSince1970 == 1_767_225_600)
        // .NET writes seven fractional digits.
        let fractional = try #require(CoreJSON.parseDate("2026-01-01T00:00:00.1234567Z"))
        #expect(abs(fractional.timeIntervalSince(reference) - 0.1234567) < 0.000_001)
        let offset = try #require(CoreJSON.parseDate("2026-01-01T08:00:00+08:00"))
        #expect(offset == reference)
        #expect(CoreJSON.parseDate("yesterday") == nil)
        #expect(CoreJSON.parseDate(CoreJSON.formatDate(reference)) == reference)
    }
}

@MainActor
@Suite struct RecentFilesTests {
    @Test func newestFirstDeduplicatedAndCapped() throws {
        let root = try TemporaryDirectory()
        let first = root.file("first.md")
        let second = root.file("second.md")

        let service = RecentFilesService()
        service.add(first)
        service.add(second)
        #expect(service.files.map(\.path) == [second, first])

        service.add(first)
        #expect(service.files.map(\.path) == [first, second])

        // Same file, spelled differently: de-duplication has to compare canonical paths, not strings.
        let detour = root.file("sub/../first.md")
        service.add(detour)
        #expect(service.files.count == 2)
        #expect(service.files[0].path == detour)

        let overflow = RecentFilesService()
        for index in 0..<(RecentFilesService.maxEntries + 5) {
            overflow.add(root.file("file\(index).md"))
        }
        #expect(overflow.files.count == RecentFilesService.maxEntries)
        #expect(overflow.files[0].path == root.file("file\(RecentFilesService.maxEntries + 4).md"))

        #expect(service.remove(first))
        #expect(!service.remove(first))
        service.clear()
        #expect(service.files.isEmpty)
    }

    // Everything a hand-edited or half-written recent.json can contain must load, not throw.
    @Test func damagedStateStillLoads() throws {
        let root = try TemporaryDirectory()
        let first = root.file("first.md")
        let decoder = CoreJSON.makeDecoder()

        let nullFiles = try decoder.decode(RecentFilesState.self, from: Data(#"{ "files": null }"#.utf8))
        let damaged = RecentFilesService()
        damaged.load(from: nullFiles)
        #expect(damaged.files.isEmpty)

        var entries = [#"null"#, #"{ "path": "   " }"#, #"{ "path": "\#(first)" }"#, #"{ "path": "\#(first)" }"#]
        entries += (0..<20).map { #"{ "path": "\#(root.file("messy\($0).md"))" }"# }
        let messy = try decoder.decode(
            RecentFilesState.self,
            from: Data(#"{ "files": [\#(entries.joined(separator: ","))] }"#.utf8))
        damaged.load(from: messy)
        #expect(damaged.files.count == RecentFilesService.maxEntries)
        #expect(damaged.files[0].path == first)
    }

    @Test func seedingAppendsAndSurvivesARoundTrip() async throws {
        let root = try TemporaryDirectory()
        let first = root.file("first.md")
        let second = root.file("second.md")

        let seeded = RecentFilesService()
        seeded.add(first)
        seeded.addIfMissing(second, lastOpenedUtc: Date())
        #expect(seeded.files[1].path == second)
        seeded.addIfMissing(first, lastOpenedUtc: Date())
        #expect(seeded.files.count == 2)

        let store = JSONStateStore(baseDirectory: root.path)
        try await store.saveRecentFiles(seeded.toState())
        let reloaded = RecentFilesService()
        reloaded.load(from: await store.loadRecentFiles())
        #expect(reloaded.files.map(\.path) == [first, second])
    }
}

@Suite struct SessionLayoutTests {
    @Test func layoutSurvivesARoundTrip() {
        let left = UUID(), topRight = UUID(), bottomRight = UUID()
        let layout = PaneLayoutNode.split(
            id: UUID(),
            orientation: .horizontal,
            ratio: 0.3,
            first: .editorPane(paneID: left),
            second: .split(
                id: UUID(),
                orientation: .vertical,
                ratio: 0.6,
                first: .editorPane(paneID: topRight),
                second: .editorPane(paneID: bottomRight)))

        var state = SessionState()
        state.setLayout(layout)
        #expect(state.nodes.count == 5)
        #expect(state.layout() == layout)
    }

    @Test func aSessionWithNoLayoutGivesNone() {
        #expect(SessionState().layout() == nil)
    }

    @Test func brokenLayoutsAreRejectedAndRatiosClamped() {
        let pane = UUID(), splitID = UUID()

        var missingChild = SessionState()
        var split = PaneNodeState(id: splitID)
        split.isSplit = true
        split.firstNodeId = pane
        split.secondNodeId = UUID()
        missingChild.rootNodeId = splitID
        missingChild.nodes = [split, PaneNodeState(id: pane)]
        #expect(missingChild.layout() == nil)

        var selfReferencing = SessionState()
        split.secondNodeId = splitID
        selfReferencing.rootNodeId = splitID
        selfReferencing.nodes = [split, PaneNodeState(id: pane)]
        #expect(selfReferencing.layout() == nil)

        var extreme = SessionState()
        let other = UUID()
        split.secondNodeId = other
        split.ratio = 0.99
        extreme.rootNodeId = splitID
        extreme.nodes = [split, PaneNodeState(id: pane), PaneNodeState(id: other)]
        guard case .split(_, _, let ratio, _, _)? = extreme.layout() else {
            Issue.record("expected a split")
            return
        }
        #expect(ratio == 0.9)
    }
}
