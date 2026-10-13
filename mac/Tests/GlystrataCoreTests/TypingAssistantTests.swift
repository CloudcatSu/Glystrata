import Foundation
import Testing
@testable import GlystrataCore

@Suite struct MarkdownTypingAssistantTests {
    private func apply(_ edit: TextEdit, to text: String) -> String {
        (text as NSString).replacingCharacters(in: edit.range, with: edit.replacement)
    }

    /// Runs the continuation for `text` with the caret at its very end, as right after Return.
    private func continued(_ text: String) -> (text: String, caret: Int)? {
        let caret = (text as NSString).length
        guard let edit = MarkdownTypingAssistant.continuation(afterLineBreakIn: text, caret: caret) else {
            return nil
        }
        return (apply(edit, to: text), edit.caret)
    }

    @Test(arguments: [
        ("- first\n", "- first\n- "),
        ("* first\n", "* first\n* "),
        ("+ first\n", "+ first\n+ "),
        ("1. first\n", "1. first\n2. "),
        ("9. nine\n", "9. nine\n10. "),
        ("- [ ] todo\n", "- [ ] todo\n- [ ] "),
        ("- [x] done\n", "- [x] done\n- [ ] "),
        ("  - nested\n", "  - nested\n  - "),
        ("\t3. tabbed\n", "\t3. tabbed\n\t4. "),
        ("intro\n- 中文項目\n", "intro\n- 中文項目\n- ")
    ])
    func listsContinue(_ input: String, _ expected: String) throws {
        let result = try #require(continued(input))
        #expect(result.text == expected)
        #expect(result.caret == (expected as NSString).length)
    }

    @Test func indentationAlreadyOnTheNewLineIsReplacedNotDoubled() throws {
        let result = try #require(continued("  - nested\n  "))
        #expect(result.text == "  - nested\n  - ")
    }

    @Test(arguments: ["- \n", "-\n", "1. \n", "- [ ] \n", "  * \n"])
    func returnOnAnEmptyItemClearsIt(_ input: String) throws {
        let result = try #require(continued(input))
        #expect(result.text == "\n")
        #expect(result.caret == 1)
    }

    @Test(arguments: ["plain\n", "---\n", "-not a list\n", "1.5 apples\n", "# heading\n", "\n", "- first"])
    func otherLinesAreLeftAlone(_ input: String) {
        #expect(continued(input) == nil)
    }

    @Test func continuationWorksInTheMiddleOfADocument() throws {
        let text = "- one\n\n- three"
        let edit = try #require(MarkdownTypingAssistant.continuation(afterLineBreakIn: text, caret: 6))
        #expect(apply(edit, to: text) == "- one\n- \n- three")
        #expect(edit.caret == 8)
    }

    @Test(arguments: [
        ("a->", "a→"), ("a<->", "a↔"), ("a=>", "a⇒"), ("x>=", "x≥"), ("x<=", "x≤"),
        ("wait...", "wait…"), ("yes--", "yes—"), ("中文--", "中文—")
    ])
    func typedSequencesBecomeSymbols(_ input: String, _ expected: String) throws {
        let swap = try #require(MarkdownTypingAssistant.symbolReplacement(in: input, caret: (input as NSString).length))
        let replaced = apply(swap.edit, to: input)
        #expect(replaced == expected)
        #expect(swap.edit.caret == (expected as NSString).length)
        #expect(apply(swap.reverting, to: replaced) == input)
        #expect(swap.reverting.caret == (input as NSString).length)
    }

    @Test(arguments: ["--", "| --", "|:--", "- --", "a-", "a.", "a..", ""])
    func dividersTableRowsAndPartialSequencesAreLeftAlone(_ input: String) {
        #expect(MarkdownTypingAssistant.symbolReplacement(in: input, caret: (input as NSString).length) == nil)
    }

    @Test func onlyTheTextRightBeforeTheCaretCounts() throws {
        #expect(MarkdownTypingAssistant.symbolReplacement(in: "a-> b", caret: 5) == nil)
        let swap = try #require(MarkdownTypingAssistant.symbolReplacement(in: "a-> b", caret: 3))
        #expect(apply(swap.edit, to: "a-> b") == "a→ b")
    }
}
