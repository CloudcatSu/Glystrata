import Foundation
import Testing
@testable import GlystrataCore

@Suite struct FileCodecTests {
    @Test(arguments: [
        (FileEncodingKind.utf8, LineEndingKind.lf),
        (FileEncodingKind.utf8WithBom, LineEndingKind.crLf),
        (FileEncodingKind.utf16LittleEndian, LineEndingKind.cr)
    ])
    func roundTripKeepsEncodingAndLineEnding(encoding: FileEncodingKind, lineEnding: LineEndingKind) throws {
        let root = try TemporaryDirectory()
        let codec = FileCodec()
        let source = "標題\n第二行\n"
        let path = root.file("codec.txt")
        try codec.encode(source, encoding: encoding, lineEnding: lineEnding).write(to: URL(fileURLWithPath: path))

        let decoded = try codec.read(path: path)
        #expect(decoded.text == source)
        #expect(decoded.encoding == encoding)
        #expect(decoded.lineEnding == lineEnding)
    }

    @Test func rejectsUTF16BigEndianAndInvalidUTF8() {
        let codec = FileCodec()
        let fingerprint = FileFingerprint(length: 0, lastWriteTimeUtc: Date())
        #expect(throws: GlystrataError.self) {
            try codec.decode(Data([0xFE, 0xFF, 0x00, 0x41]), fingerprint: fingerprint)
        }
        #expect(throws: GlystrataError.self) {
            try codec.decode(Data([0x41, 0xFF, 0xFE, 0x42]), fingerprint: fingerprint)
        }
    }

    @Test func mixedLineEndingsPickTheMostCommon() {
        #expect(FileCodec.detectLineEnding("a\r\nb\r\nc\n") == .crLf)
        #expect(FileCodec.detectLineEnding("a\nb\nc\r\n") == .lf)
        #expect(FileCodec.detectLineEnding("a\rb\rc\n") == .cr)
        #expect(FileCodec.detectLineEnding("no line breaks") == .lf)
    }
}

@MainActor
@Suite struct DocumentManagerTests {
    @Test func viewsOfTheSameFileShareContentButNotScrollPosition() throws {
        let root = try TemporaryDirectory()
        let path = try root.write("# views", to: "views.md")
        let manager = DocumentManager()
        let first = try manager.openView(path, paneID: UUID())
        let second = try manager.openView(path, paneID: UUID(), newView: true)

        #expect(first.viewID != second.viewID)
        #expect(first.document === second.document)
        first.verticalOffset = 120
        second.verticalOffset = 480
        #expect(first.verticalOffset == 120)
        #expect(second.verticalOffset == 480)

        // Without newView, asking for the file again hands back a tab that is already open.
        let again = try manager.openView(path, paneID: UUID())
        #expect(again.document === first.document)
        #expect(manager.views.count == 2)
    }

    @Test func samePathSpelledDifferentlyIsTheSameDocument() throws {
        let root = try TemporaryDirectory()
        let path = try root.write("x", to: "Notes.md")
        let manager = DocumentManager()
        let document = try manager.open(path)
        let respelled = try manager.open(root.file("sub/../notes.MD"))
        #expect(respelled === document)
    }

    @Test func overlappingSavesAllComplete() async throws {
        let root = try TemporaryDirectory()
        let path = try root.write("before", to: "save.md")
        let manager = DocumentManager()
        let document = try manager.open(path)
        document.updateText("after")

        async let first = manager.save(document)
        async let second = manager.save(document)
        async let third = manager.save(document)
        async let fourth = manager.save(document)
        let results = await [first, second, third, fourth]

        #expect(results.allSatisfy { $0.success })
        let written = try readText(path)
        #expect(written == "after")
        #expect(!document.isModified)
    }

    @Test func failedSaveKeepsTheDocumentModified() async throws {
        let root = try TemporaryDirectory()
        let manager = DocumentManager()
        let document = DocumentSession(
            filePath: root.file("missing-directory/failed.md"),
            text: "initial",
            encoding: .utf8,
            lineEnding: .lf)
        document.updateText("changed")

        let result = await manager.save(document)
        #expect(!result.success)
        #expect(document.isModified)
        #expect(document.lastError?.isEmpty == false)
    }

    @Test func untitledDocumentNeedsSaveAsAndFailedSaveAsLeavesItUntitled() async throws {
        let root = try TemporaryDirectory()
        let manager = DocumentManager()
        let view = manager.createUntitled(paneID: UUID())
        let document = view.document
        document.updateText("draft")

        let unsaved = await manager.save(document)
        #expect(!unsaved.success)

        let failed = await manager.saveAs(document, to: root.file("missing-directory/draft.md"))
        #expect(!failed.success)
        #expect(document.isUntitled)

        let target = root.file("draft.md")
        let saved = await manager.saveAs(document, to: target)
        #expect(saved.success)
        #expect(!document.isUntitled)
        #expect(manager.findDocument(target) === document)
        let written = try readText(target)
        #expect(written == "draft")
    }

    @Test func editsMadeWhileSavingStayModified() throws {
        let document = DocumentSession(filePath: "/tmp/typing.md", text: "one", encoding: .utf8, lineEnding: .lf)
        document.updateText("two")
        let written = document.text
        document.updateText("three")
        document.markSaved(FileFingerprint(length: 3, lastWriteTimeUtc: Date()), savedText: written)
        #expect(document.isModified)
    }

    @Test func compositionOnlyEditCountsAsModified() {
        let document = DocumentSession(filePath: nil, text: "\u{00E9}", encoding: .utf8, lineEnding: .lf)
        document.updateText("e\u{0301}")
        #expect(document.isModified)
    }

    @Test func saveReplacesContentAndKeepsLineEndings() async throws {
        let root = try TemporaryDirectory()
        let path = root.file("crlf.txt")
        try Data("a\r\nb\r\n".utf8).write(to: URL(fileURLWithPath: path))
        let manager = DocumentManager()
        let document = try manager.open(path)
        #expect(document.text == "a\nb\n")
        document.updateText("a\nb\nc\n")
        let saved = await manager.save(document)
        #expect(saved.success)
        let bytes = try Data(contentsOf: URL(fileURLWithPath: path))
        #expect(bytes == Data("a\r\nb\r\nc\r\n".utf8))
        let leftovers = try FileManager.default.contentsOfDirectory(atPath: root.path).filter { $0.hasSuffix(".tmp") }
        #expect(leftovers.isEmpty)
    }
}

@Suite struct PaneLayoutTests {
    @Test func removingAPaneCollapsesItsSplit() {
        let first = UUID()
        let second = UUID()
        let third = UUID()
        let root = PaneLayoutNode.split(
            id: UUID(),
            orientation: .horizontal,
            ratio: 0.5,
            first: .editorPane(paneID: first),
            second: .split(
                id: UUID(),
                orientation: .vertical,
                ratio: 0.5,
                first: .editorPane(paneID: second),
                second: .editorPane(paneID: third)))

        #expect(PaneLayoutOperations.paneIDs(root) == [first, second, third])
        let result = PaneLayoutOperations.removePane(root, paneID: second)
        #expect(result.removed)
        let remaining = try! #require(result.node)
        #expect(PaneLayoutOperations.paneIDs(remaining) == [first, third])
        #expect(!PaneLayoutOperations.containsPane(remaining, paneID: second))

        let collapsed = PaneLayoutOperations.collapseToSinglePane(root)
        #expect(collapsed == .editorPane(paneID: first))
    }
}

@Suite struct PaneSplitTests {
    @Test func splittingPutsTheNewPaneBesideTheOldOne() {
        let first = UUID()
        let second = UUID()
        let third = UUID()
        let once = PaneLayoutOperations.replacePaneWithSplit(.editorPane(paneID: first), paneID: first, orientation: .horizontal, newPaneID: second)
        #expect(PaneLayoutOperations.paneIDs(once) == [first, second])

        let twice = PaneLayoutOperations.replacePaneWithSplit(once, paneID: second, orientation: .vertical, newPaneID: third)
        #expect(PaneLayoutOperations.paneIDs(twice) == [first, second, third])
        guard case .split(let outerID, .horizontal, 0.5, .editorPane(first), .split(_, .vertical, 0.5, _, _)) = twice else {
            Issue.record("unexpected layout: \(twice)")
            return
        }

        let resized = PaneLayoutOperations.settingRatio(twice, splitID: outerID, ratio: 0.99)
        guard case .split(_, _, let ratio, _, .split(_, _, let innerRatio, _, _)) = resized else {
            Issue.record("unexpected layout: \(resized)")
            return
        }
        #expect(ratio == 0.9)
        #expect(innerRatio == 0.5)
    }
}

@Suite struct TextMetricsTests {
    @Test func countsUserPerceivedCharacters() {
        let text = "A\u{0301} B\n"
        #expect(TextMetrics.countCharacters(text, mode: .includeWhitespace) == 4)
        #expect(TextMetrics.countCharacters(text, mode: .excludeWhitespace) == 2)
        #expect(TextMetrics.countCharacters(text, mode: .excludeLineBreaks) == 3)
        #expect(TextMetrics.countCharacters(nil, mode: .includeWhitespace) == 0)
    }
}

@Suite struct TextDiffTests {
    @Test func marksRemovedAndAddedLines() {
        let diff = TextDiffService().compare("a\nb\n", "a\nc\n")
        #expect(diff.contains { $0.kind == .removed && $0.text == "b" && $0.leftLine == 2 })
        #expect(diff.contains { $0.kind == .added && $0.text == "c" && $0.rightLine == 2 })
        #expect(diff.first == DiffLine(kind: .unchanged, text: "a", leftLine: 1, rightLine: 1))
    }
}

@Suite struct MarkdownFormattingTests {
    let service = MarkdownFormattingService()

    @Test func wrapsSelectionInBold() {
        let bold = service.apply("Hello", selectionStart: 0, selectionLength: 5, command: .bold)
        #expect(bold == MarkdownEditResult(text: "**Hello**", selectionStart: 2, selectionLength: 5))
    }

    @Test func insertsPlaceholderWhenNothingIsSelected() {
        let bold = service.apply("", selectionStart: 0, selectionLength: 0, command: .bold)
        #expect(bold == MarkdownEditResult(text: "**bold**", selectionStart: 2, selectionLength: 4))
    }

    @Test func changesHeadingLevel() {
        let heading = service.apply("# Hello\nWorld", selectionStart: 0, selectionLength: 7, command: .heading2)
        #expect(heading.text == "## Hello\nWorld")
        let caretOnly = service.apply("# Hello", selectionStart: 4, selectionLength: 0, command: .heading3)
        #expect(caretOnly == MarkdownEditResult(text: "### Hello", selectionStart: 6, selectionLength: 0))
    }

    @Test func togglesListPrefixes() {
        #expect(service.apply("a\nb", selectionStart: 0, selectionLength: 3, command: .unorderedList).text == "- a\n- b")
        #expect(service.apply("- a\n- b", selectionStart: 0, selectionLength: 7, command: .unorderedList).text == "a\nb")
        #expect(service.apply("a\nb", selectionStart: 0, selectionLength: 3, command: .orderedList).text == "1. a\n1. b")
        #expect(service.apply("  2) a", selectionStart: 0, selectionLength: 6, command: .orderedList).text == "  a")
        #expect(service.apply("> a", selectionStart: 0, selectionLength: 3, command: .quote).text == "a")
    }

    @Test func buildsLinkAroundSelection() {
        let link = service.apply("文件", selectionStart: 0, selectionLength: 2, command: .link, linkURL: "https://example.com")
        #expect(link == MarkdownEditResult(text: "[文件](https://example.com)", selectionStart: 1, selectionLength: 2))
    }

    @Test func togglesCodeFence() {
        let code = service.apply("value", selectionStart: 0, selectionLength: 5, command: .codeBlock)
        #expect(code == MarkdownEditResult(text: "```\nvalue\n```", selectionStart: 4, selectionLength: 5))
        let unwrapped = service.apply(code.text, selectionStart: 0, selectionLength: code.text.utf16.count, command: .codeBlock)
        #expect(unwrapped.text == "value")
    }

    @Test func offsetsAreUTF16SoEmojiDoNotShiftTheSelection() {
        // "😀" is one character but two UTF-16 units; the selection covers "b" only.
        let result = service.apply("😀b", selectionStart: 2, selectionLength: 1, command: .bold)
        #expect(result == MarkdownEditResult(text: "😀**b**", selectionStart: 4, selectionLength: 1))
    }
}
