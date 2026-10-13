import Foundation
import Testing
@testable import GlystrataCore

@Suite struct CommandTests {
    @Test func noTwoCommandsShareAKey() {
        #expect(AppCommands.conflicts().isEmpty)
    }

    @Test func everyCommandAppearsExactlyOnce() {
        for id in AppCommandID.allCases {
            #expect(AppCommands.all.filter { $0.id == id }.count == 1, "\(id)")
        }
    }

    @Test func shortcutsPrintTheMacWay() {
        #expect(AppCommands.definition(.fileNew).shortcut?.displayText == "⌘N")
        #expect(AppCommands.definition(.fileSaveAs).shortcut?.displayText == "⇧⌘S")
        #expect(AppCommands.definition(.fileCreateSnapshot).shortcut?.displayText == "⌥⌘S")
        #expect(AppCommands.definition(.settingsOpen).shortcut?.displayText == "⌘,")
        #expect(AppCommands.definition(.viewSplitVertical).shortcut?.displayText == "⌥⌘\\")
        #expect(AppCommands.definition(.helpAbout).shortcut == nil)
    }

    @Test func keysReservedByMacOSAreLeftAlone() {
        let taken: Set<KeyShortcut> = [
            KeyShortcut("h"), KeyShortcut("q"), KeyShortcut("m"), KeyShortcut("z"), KeyShortcut("z", [.command, .shift]),
            KeyShortcut("g"), KeyShortcut("g", [.command, .shift]), KeyShortcut("c"), KeyShortcut("v"), KeyShortcut("x"), KeyShortcut("a")
        ]
        #expect(AppCommands.all.compactMap(\.shortcut).allSatisfy { !taken.contains($0) })
    }

    @Test func clashesAreReported() {
        let clashing = [
            AppCommandDefinition(.fileNew, "file.new", KeyShortcut("n")),
            AppCommandDefinition(.fileOpen, "file.open", KeyShortcut("n"))
        ]
        #expect(AppCommands.conflicts(in: clashing).count == 1)
    }

    // A command whose label is missing from a language would show its raw key in the menu.
    @Test(arguments: AppLanguage.allCases)
    func everyLabelledCommandHasText(language: AppLanguage) {
        for key in AppCommands.all.compactMap(\.localizationKey) {
            #expect(LocalizationService.text(key, language: language) != nil, "\(language) 缺少命令文字：\(key)")
        }
    }
}

@MainActor
@Suite struct LocalizationTests {
    @Test func switchesLanguageAndFallsBackToTheKey() {
        let localization = LocalizationService()
        #expect(localization.get("file.new") == "新增")
        localization.apply(.english)
        #expect(localization.get("file.new") == "New")
        #expect(localization.get("no.such.key") == "no.such.key")
    }

    @Test func menuTitlesCarryNoMnemonicsAndHintsNameMacKeys() {
        #expect(LocalizationService.text("menu.file", language: .traditionalChinese) == "檔案")
        #expect(LocalizationService.text("menu.file", language: .english) == "File")
        for language in AppLanguage.allCases {
            #expect(LocalizationService.text("editor.empty", language: language)?.contains("⌘N") == true)
            #expect(LocalizationService.text("settings.hideSnapshotFiles.help", language: language)?.contains(".gss") == true)
        }
    }

    @Test func bothLanguagesCoverTheSameKeys() {
        #expect(Set(LocalizationTable.traditionalChinese.keys) == Set(LocalizationTable.english.keys))
        #expect(LocalizationTable.traditionalChinese.count == 217)
    }
}

@Suite struct SyntaxHighlighterTests {
    private let none = FrontMatterContext(opensOnFirstLine: false, closingLine: nil)

    private func spans(_ text: String, line: Int = 5, yaml: Bool = false, frontMatter: FrontMatterContext? = nil) -> [SyntaxSpan] {
        SyntaxHighlighter.spans(forLine: text, lineNumber: line, isYAMLFile: yaml, frontMatter: frontMatter ?? none)
    }

    private func span(_ location: Int, _ length: Int, _ category: String) -> SyntaxSpan {
        SyntaxSpan(range: NSRange(location: location, length: length), category: category)
    }

    @Test func wholeLineRules() {
        #expect(spans("## 標題") == [span(0, 5, "heading")])
        #expect(spans("```swift") == [span(0, 8, "code")])
        #expect(spans("> 引言 **粗體**") == [span(0, 11, "quote"), span(5, 6, "emphasis")])
        #expect(spans("") == [])
        #expect(spans("一般文字") == [])
    }

    @Test func listMarkersAndInlineSpans() {
        #expect(spans("- 項目") == [span(0, 1, "list")])
        #expect(spans("  12) 項目") == [span(0, 5, "list")])
        #expect(spans("見 [連結](https://x.y) 與 `code`") == [span(2, 17, "link"), span(22, 6, "code")])
        // Ranges are UTF-16, so an emoji before the match shifts it by two.
        #expect(spans("😀 `a`") == [span(3, 3, "code")])
    }

    @Test func yamlFilesColourKeysValuesAndComments() {
        #expect(spans("name: Glystrata # 註解", yaml: true) == [
            span(0, 4, "yaml-key"),
            // The value runs from just after the colon to the end of the line; the comment then paints over its tail.
            span(5, 15, "yaml-value"),
            span(16, 4, "yaml-comment")
        ])
        // A comment line in YAML is not a Markdown heading.
        #expect(spans("# 設定", yaml: true) == [span(0, 4, "yaml-comment")])
        #expect(spans("# 設定") == [span(0, 4, "heading")])
    }

    @Test func frontMatterIsYAMLUntilItCloses() {
        let lines: [Substring] = ["---", "title: 測試", "---", "title: 內文"]
        let context = FrontMatterContext(leadingLines: lines)
        #expect(context == FrontMatterContext(opensOnFirstLine: true, closingLine: 3))

        #expect(spans("---", line: 1, frontMatter: context) == [span(0, 3, "front-matter")])
        #expect(spans("title: 測試", line: 2, frontMatter: context) == [
            span(0, 9, "front-matter"),
            span(0, 5, "yaml-key"),
            span(6, 3, "yaml-value")
        ])
        #expect(spans("---", line: 3, frontMatter: context) == [span(0, 3, "front-matter")])
        #expect(spans("title: 內文", line: 4, frontMatter: context) == [])
    }

    @Test func frontMatterNeedsToOpenOnTheFirstLineAndStaysNearTheTop() {
        #expect(FrontMatterContext(leadingLines: ["# 標題", "---", "a: 1", "---"] as [Substring]) == none)
        let unclosed = FrontMatterContext(leadingLines: ["---", "a: 1"] as [Substring])
        #expect(unclosed.isContent(line: 2))
        #expect(unclosed.isContent(line: 30))
        #expect(!unclosed.isContent(line: 31))
    }

    @Test func recognisesYAMLFilesByExtension() {
        #expect(SyntaxHighlighter.isYAMLFile("/a/config.YML"))
        #expect(SyntaxHighlighter.isYAMLFile("/a/config.yaml"))
        #expect(!SyntaxHighlighter.isYAMLFile("/a/notes.md"))
        #expect(!SyntaxHighlighter.isYAMLFile(nil))
    }
}
