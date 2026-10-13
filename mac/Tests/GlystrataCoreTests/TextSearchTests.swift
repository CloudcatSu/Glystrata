import Foundation
import Testing
@testable import GlystrataCore

@Suite struct TextSearchTests {
    private func apply(_ edit: TextEdit, to text: String) -> String {
        (text as NSString).replacingCharacters(in: edit.range, with: edit.replacement)
    }

    // MARK: - Finding

    @Test func emptyQueryFindsNothing() {
        #expect(TextSearch.matches(in: "abc", query: "").isEmpty)
        #expect(TextSearch.matches(in: "", query: "a").isEmpty)
    }

    @Test func ignoresCaseByDefault() {
        let found = TextSearch.matches(in: "Foo foo FOO", query: "foo")
        #expect(found == [NSRange(location: 0, length: 3), NSRange(location: 4, length: 3), NSRange(location: 8, length: 3)])
    }

    @Test func caseSensitiveOptionKeepsOnlyExactCase() {
        let found = TextSearch.matches(in: "Foo foo FOO", query: "foo", caseSensitive: true)
        #expect(found == [NSRange(location: 4, length: 3)])
    }

    @Test func matchesDoNotOverlap() {
        let found = TextSearch.matches(in: "aaaa", query: "aa")
        #expect(found == [NSRange(location: 0, length: 2), NSRange(location: 2, length: 2)])
    }

    @Test func chineseOffsetsAreUTF16() {
        let found = TextSearch.matches(in: "你好世界，你好", query: "你好")
        #expect(found == [NSRange(location: 0, length: 2), NSRange(location: 5, length: 2)])
    }

    @Test func emojiCountAsTwoUnits() {
        // "😀" is a surrogate pair, so the match after it starts at 2, not 1.
        let found = TextSearch.matches(in: "😀ab😀ab", query: "ab")
        #expect(found == [NSRange(location: 2, length: 2), NSRange(location: 6, length: 2)])
        #expect(TextSearch.matches(in: "x😀y😀", query: "😀") == [NSRange(location: 1, length: 2), NSRange(location: 4, length: 2)])
    }

    // MARK: - Moving between matches

    private let three = [NSRange(location: 2, length: 1), NSRange(location: 6, length: 1), NSRange(location: 10, length: 1)]

    @Test func forwardTakesFirstMatchAtOrAfterAnchor() {
        #expect(TextSearch.matchIndex(in: three, anchor: 0, forward: true) == 0)
        #expect(TextSearch.matchIndex(in: three, anchor: 6, forward: true) == 1)
        #expect(TextSearch.matchIndex(in: three, anchor: 7, forward: true) == 2)
    }

    @Test func forwardWrapsToFirst() {
        #expect(TextSearch.matchIndex(in: three, anchor: 11, forward: true) == 0)
    }

    @Test func backwardTakesLastMatchBeforeAnchor() {
        #expect(TextSearch.matchIndex(in: three, anchor: 10, forward: false) == 1)
        #expect(TextSearch.matchIndex(in: three, anchor: 6, forward: false) == 0)
    }

    @Test func backwardWrapsToLast() {
        #expect(TextSearch.matchIndex(in: three, anchor: 2, forward: false) == 2)
        #expect(TextSearch.matchIndex(in: three, anchor: 0, forward: false) == 2)
    }

    @Test func noMatchesGiveNoIndex() {
        #expect(TextSearch.matchIndex(in: [], anchor: 0, forward: true) == nil)
        #expect(TextSearch.matchIndex(in: [], anchor: 0, forward: false) == nil)
    }

    // MARK: - Replacing one

    @Test func replacingOneMovesCaretPastReplacement() {
        let text = "cat dog cat"
        let edit = TextSearch.replacing(NSRange(location: 0, length: 3), with: "tiger", in: text)
        #expect(apply(edit, to: text) == "tiger dog cat")
        #expect(edit.caret == 5)
    }

    @Test func offsetsAfterReplacementShiftByTheLengthDifference() {
        let text = "cat dog cat"
        let edit = TextSearch.replacing(NSRange(location: 0, length: 3), with: "tiger", in: text)
        let after = apply(edit, to: text)
        let found = TextSearch.matches(in: after, query: "cat")
        #expect(found == [NSRange(location: 10, length: 3)])
        // The next match to visit is the first one at or after the caret.
        #expect(TextSearch.matchIndex(in: found, anchor: edit.caret, forward: true) == 0)
    }

    @Test func replacementCanBeEmptyOrContainTheQuery() {
        let text = "ab ab"
        #expect(apply(TextSearch.replacing(NSRange(location: 0, length: 2), with: "", in: text), to: text) == " ab")
        let grown = TextSearch.replacing(NSRange(location: 0, length: 2), with: "abab", in: text)
        #expect(apply(grown, to: text) == "abab ab")
        #expect(grown.caret == 4)
    }

    @Test func replacingEmojiWithChineseUsesUTF16Caret() {
        let text = "a😀b"
        let edit = TextSearch.replacing(NSRange(location: 1, length: 2), with: "好", in: text)
        #expect(apply(edit, to: text) == "a好b")
        #expect(edit.caret == 2)
    }

    // MARK: - Replacing all

    @Test func replaceAllCountsAndRewritesEveryMatch() throws {
        let text = "one fish, two Fish, red fish"
        let result = try #require(TextSearch.replacingAll(in: text, query: "fish", with: "cat"))
        #expect(result.count == 3)
        #expect(apply(result.edit, to: text) == "one cat, two cat, red cat")
    }

    @Test func replaceAllEditCoversOnlyTheChangedSpan() throws {
        let text = "keep fish middle fish keep"
        let result = try #require(TextSearch.replacingAll(in: text, query: "fish", with: "X"))
        #expect(result.edit.range == NSRange(location: 5, length: 16))
        #expect(result.edit.replacement == "X middle X")
        #expect(result.edit.caret == 5 + 10)
    }

    @Test func replaceAllRespectsCaseOption() throws {
        let text = "Ab ab AB"
        let result = try #require(TextSearch.replacingAll(in: text, query: "ab", with: "-", caseSensitive: true))
        #expect(result.count == 1)
        #expect(apply(result.edit, to: text) == "Ab - AB")
    }

    @Test func replaceAllDoesNotRescanItsOwnOutput() throws {
        let text = "aa"
        let result = try #require(TextSearch.replacingAll(in: text, query: "a", with: "aa"))
        #expect(result.count == 2)
        #expect(apply(result.edit, to: text) == "aaaa")
    }

    @Test func replaceAllHandlesChineseAndEmoji() throws {
        let text = "你好😀你好"
        let result = try #require(TextSearch.replacingAll(in: text, query: "你好", with: "哈囉"))
        #expect(result.count == 2)
        #expect(apply(result.edit, to: text) == "哈囉😀哈囉")
    }

    @Test func replaceAllWithNoMatchOrEmptyQueryIsNil() {
        #expect(TextSearch.replacingAll(in: "abc", query: "x", with: "y") == nil)
        #expect(TextSearch.replacingAll(in: "abc", query: "", with: "y") == nil)
    }
}
