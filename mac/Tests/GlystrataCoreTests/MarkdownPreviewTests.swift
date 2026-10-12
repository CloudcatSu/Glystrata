import Foundation
import Testing
@testable import GlystrataCore

@Suite struct MarkdownPreviewTests {
    let service = MarkdownPreviewService()

    private func blocks(_ markdown: String) -> [MarkdownBlock] {
        service.parse(markdown, sourceDirectory: "/tmp").blocks
    }

    private func paragraph(_ markdown: String) -> [MarkdownInline] {
        guard case .paragraph(let inlines)? = blocks(markdown).first else {
            return []
        }
        return inlines
    }

    // The checks the Windows verification suite makes of its preview: heading and table parsed,
    // script not live, front matter not shown.
    @Test func parsesTheBasicsAndHidesFrontMatterAndHTML() {
        let markdown = "---\ntitle: 測試\n---\n# 標題\n\n- [x] 完成\n- 第二項\n\n| A | B |\n| --- | --- |\n| 1 | 2 |\n\n<script>alert('blocked')</script>\n"
        let result = blocks(markdown)

        #expect(result.first == .heading(level: 1, inlines: [.text("標題")]))
        #expect(result.contains(.table(MarkdownTable(
            alignments: [nil, nil],
            header: [[.text("A")], [.text("B")]],
            rows: [[[.text("1")], [.text("2")]]]))))
        #expect(result.contains(.list(MarkdownList(isOrdered: false, start: 1, items: [
            MarkdownListItem(isChecked: true, blocks: [.paragraph([.text("完成")])]),
            MarkdownListItem(isChecked: nil, blocks: [.paragraph([.text("第二項")])])
        ]))))
        #expect(result.last == .paragraph([.text("<script>alert('blocked')</script>")]))
        #expect(!"\(result)".contains("title: 測試"))
    }

    @Test func frontMatterNeedsAClosingFence() {
        #expect(MarkdownPreviewService.stripFrontMatter("---\na: 1\n...\nbody") == "body")
        #expect(MarkdownPreviewService.stripFrontMatter("---\nnot closed") == "---\nnot closed")
        #expect(MarkdownPreviewService.stripFrontMatter("text\n---\na\n---") == "text\n---\na\n---")
    }

    @Test func inlineHTMLIsShownAsTyped() {
        #expect(paragraph("行內 <b>HTML</b> 標籤。") == [.text("行內 <b>HTML</b> 標籤。")])
        // Markdown inside what would have been an HTML block is still Markdown.
        #expect(paragraph("<div>**粗體**</div>") == [.text("<div>"), .strong([.text("粗體")]), .text("</div>")])
        // An angle-bracket URL is a link, not HTML.
        #expect(paragraph("<https://example.org>") == [.link(destination: "https://example.org", children: [.text("https://example.org")])])
        // Code keeps its angle brackets without growing a backslash.
        #expect(paragraph("`<b>`") == [.code("<b>")])
    }

    @Test func setextUnderlinesDoNotMakeHeadings() {
        #expect(blocks("段落\n---") == [.paragraph([.text("段落")]), .thematicBreak])
        #expect(blocks("段落\n===") == [.paragraph([.text("段落"), .softBreak, .text("===")])])
        #expect(blocks("段落\n-") == [.paragraph([.text("段落"), .softBreak, .text("-")])])
        #expect(blocks("段落\n---\n\n下一段\n===\n\n結尾") == [
            .paragraph([.text("段落")]),
            .thematicBreak,
            .paragraph([.text("下一段"), .softBreak, .text("===")]),
            .paragraph([.text("結尾")])
        ])
        #expect(blocks("第一行\n第二行\n---") == [.paragraph([.text("第一行"), .softBreak, .text("第二行")]), .thematicBreak])
        #expect(blocks("## 真的標題") == [.heading(level: 2, inlines: [.text("真的標題")])])
        #expect(blocks("> # 引言裡的標題") == [.blockQuote([.heading(level: 1, inlines: [.text("引言裡的標題")])])])
    }

    @Test func onlyDoubledTildesStrikeThrough() {
        #expect(paragraph("~~刪除~~") == [.strikethrough([.text("刪除")])])
        #expect(paragraph("H~2~O") == [.text("H2O")])
    }

    @Test func extraEmphasisMarkersDisappear() {
        #expect(paragraph("x^2^") == [.text("x2")])
        #expect(paragraph("++插入的文字++") == [.text("插入的文字")])
        #expect(paragraph("==標記的文字==") == [.text("標記的文字")])
        #expect(paragraph("==a **b** c==") == [.text("a "), .strong([.text("b")]), .text(" c")])
        // Markers with whitespace on the wrong side are ordinary characters.
        #expect(paragraph("a == b, C++ and x ^ y") == [.text("a == b, C++ and x ^ y")])
        #expect(paragraph("`==code==`") == [.code("==code==")])
    }

    @Test func bareURLsBecomeLinks() {
        #expect(paragraph("見 https://example.com 這裡") == [
            .text("見 "),
            .link(destination: "https://example.com", children: [.text("https://example.com")]),
            .text(" 這裡")
        ])
        // A URL runs until whitespace, so full-width punctuation typed right after it is swept in.
        // Markdig is believed to do the same; to be confirmed against the Windows reader.
        #expect(paragraph("見 https://example.com。") == [
            .text("見 "),
            .link(destination: "https://example.com。", children: [.text("https://example.com。")])
        ])
        #expect(paragraph("see https://example.com/a(b), ok") == [
            .text("see "),
            .link(destination: "https://example.com/a(b)", children: [.text("https://example.com/a(b)")]),
            .text(", ok")
        ])
        #expect(paragraph("www.example.com") == [.link(destination: "http://www.example.com", children: [.text("www.example.com")])])
        #expect(paragraph("[文字](https://example.com)") == [.link(destination: "https://example.com", children: [.text("文字")])])
        #expect(paragraph("nothttps://example.com") == [.text("nothttps://example.com")])
    }

    @Test func punctuationIsNotSmartened() {
        #expect(paragraph("\"quote\" -- dash...") == [.text("\"quote\" -- dash...")])
    }

    @Test func blockStructure() {
        #expect(blocks("```swift\nlet x = 1\n```") == [.codeBlock(language: "swift", code: "let x = 1")])
        #expect(blocks("> 引言") == [.blockQuote([.paragraph([.text("引言")])])])
        #expect(blocks("3. 三\n4. 四") == [.list(MarkdownList(isOrdered: true, start: 3, items: [
            MarkdownListItem(isChecked: nil, blocks: [.paragraph([.text("三")])]),
            MarkdownListItem(isChecked: nil, blocks: [.paragraph([.text("四")])])
        ]))])
        #expect(blocks("| 左 | 中 | 右 |\n| :-- | :-: | --: |\n| a | b | c |") == [.table(MarkdownTable(
            alignments: [.left, .center, .right],
            header: [[.text("左")], [.text("中")], [.text("右")]],
            rows: [[[.text("a")], [.text("b")], [.text("c")]]]))])
        #expect(paragraph("第一行  \n第二行") == [.text("第一行"), .lineBreak, .text("第二行")])
    }

    // The document both platforms open to compare their readers side by side.
    @Test func readerSampleDocumentParses() throws {
        let sample = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
            .appending(path: "docs/specs/2026-10-13-mac-reader-sample.md")
        let result = service.parse(try String(contentsOf: sample, encoding: .utf8), sourceDirectory: sample.deletingLastPathComponent().path)

        #expect(result.blocks.first == .heading(level: 1, inlines: [.text("一級標題 Heading 1")]))
        let headings = result.blocks.compactMap { block -> Int? in
            if case .heading(let level, _) = block { return level }
            return nil
        }
        // Six levels up top, then nine "##" sections; the two Setext-looking lines add none.
        #expect(headings == [1, 2, 3, 4, 5, 6] + Array(repeating: 2, count: 9))
        #expect(result.blocks.contains(.list(MarkdownList(isOrdered: false, start: 1, items: [
            MarkdownListItem(isChecked: nil, blocks: [.paragraph([.text("單波浪號：H2O")])]),
            MarkdownListItem(isChecked: nil, blocks: [.paragraph([.text("上標：x2")])]),
            MarkdownListItem(isChecked: nil, blocks: [.paragraph([.text("插入：插入的文字")])]),
            MarkdownListItem(isChecked: nil, blocks: [.paragraph([.text("標記：標記的文字")])])
        ]))))
        #expect(result.blocks.contains(.paragraph([.text("<script>alert('blocked')</script>")])))
        #expect(result.resources == [PreviewResource(
            source: "missing-image.png",
            resolvedPath: sample.deletingLastPathComponent().appending(path: "missing-image.png").path,
            isAvailable: false,
            errorMessage: "找不到圖片檔案。")])
    }

    @Test func imagesResolveOnlyInsideTheDocumentFolder() throws {
        let root = try TemporaryDirectory()
        try root.write("", to: "here.png")
        let document = service.parse(
            "![a](here.png) ![b](missing.png) ![c](../outside.png) ![d](https://example.com/x.png)",
            sourceDirectory: root.path)

        #expect(document.resources.map(\.isAvailable) == [true, false, false, false])
        #expect(document.resources[0].resolvedPath == root.file("here.png"))
        #expect(document.resources[1].errorMessage == "找不到圖片檔案。")
        #expect(document.resources[2].errorMessage == "圖片路徑必須位於文件目錄內。")
        #expect(document.resources[3].errorMessage == "預覽只載入本機相對圖片。")
    }
}
