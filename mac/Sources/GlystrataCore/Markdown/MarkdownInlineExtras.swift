import Foundation

/// Brings inline content in line with what the Windows reader shows for syntax the parser here
/// does not know: `^superscript^`, `++inserted++` and `==marked==` lose their markers and keep
/// their content as ordinary text, and bare URLs become links.
///
/// The marker matching is a simplified take on Markdig's emphasis rules (a marker opens when
/// followed by non-whitespace and closes when preceded by it), not a port of them, so unusual
/// nestings may still differ.
enum MarkdownInlineExtras {
    static func apply(_ inlines: [MarkdownInline], insideLink: Bool = false) -> [MarkdownInline] {
        let processed = inlines.map { inline -> MarkdownInline in
            switch inline {
            case .strong(let children):
                return .strong(apply(children, insideLink: insideLink))
            case .emphasis(let children):
                return .emphasis(apply(children, insideLink: insideLink))
            case .strikethrough(let children):
                return .strikethrough(apply(children, insideLink: insideLink))
            case .link(let destination, let children):
                return .link(destination: destination, children: apply(children, insideLink: true))
            default:
                return inline
            }
        }

        let stripped = stripMarkers(mergeText(processed))
        return mergeText(insideLink ? stripped : stripped.flatMap(linkify))
    }

    static func mergeText(_ inlines: [MarkdownInline]) -> [MarkdownInline] {
        var result: [MarkdownInline] = []
        for inline in inlines {
            if case .text(let next) = inline {
                if next.isEmpty {
                    continue
                }
                if case .text(let previous)? = result.last {
                    result[result.count - 1] = .text(previous + next)
                    continue
                }
            }
            result.append(inline)
        }
        return result
    }

    // MARK: - Markers

    private struct Marker {
        var node: Int
        var start: Int
        var length: Int
        var character: Unicode.Scalar
        var canOpen: Bool
        var canClose: Bool
    }

    private static func markerWidth(_ scalar: Unicode.Scalar) -> Int? {
        switch scalar {
        case "^": return 1
        case "+", "=": return 2
        default: return nil
        }
    }

    private static func stripMarkers(_ inlines: [MarkdownInline]) -> [MarkdownInline] {
        var texts: [Int: [Unicode.Scalar]] = [:]
        var markers: [Marker] = []

        for (index, inline) in inlines.enumerated() {
            guard case .text(let string) = inline else {
                continue
            }

            let scalars = Array(string.unicodeScalars)
            texts[index] = scalars
            var position = 0
            while position < scalars.count {
                guard let width = markerWidth(scalars[position]) else {
                    position += 1
                    continue
                }

                var end = position
                while end < scalars.count, scalars[end] == scalars[position] {
                    end += 1
                }

                if end - position >= width {
                    let before = position > 0
                        ? !scalars[position - 1].properties.isWhitespace
                        : neighbourIsContent(inlines, at: index - 1)
                    let after = end < scalars.count
                        ? !scalars[end].properties.isWhitespace
                        : neighbourIsContent(inlines, at: index + 1)
                    markers.append(Marker(
                        node: index,
                        start: position,
                        length: end - position,
                        character: scalars[position],
                        canOpen: after,
                        canClose: before))
                }
                position = end
            }
        }

        // Pair each closing marker with the nearest unmatched opener of the same kind, and drop the
        // marker characters on the inner side of each.
        var open: [Unicode.Scalar: [Marker]] = [:]
        var removals: [Int: [Range<Int>]] = [:]
        for marker in markers {
            let width = markerWidth(marker.character) ?? 1
            if marker.canClose, let opener = open[marker.character]?.popLast() {
                removals[opener.node, default: []].append((opener.start + opener.length - width)..<(opener.start + opener.length))
                removals[marker.node, default: []].append(marker.start..<(marker.start + width))
            } else if marker.canOpen {
                open[marker.character, default: []].append(marker)
            }
        }

        guard !removals.isEmpty else {
            return inlines
        }

        return inlines.enumerated().map { index, inline in
            guard var scalars = texts[index], let ranges = removals[index] else {
                return inline
            }

            for range in ranges.sorted(by: { $0.lowerBound > $1.lowerBound }) {
                scalars.removeSubrange(range)
            }
            var view = String.UnicodeScalarView()
            view.append(contentsOf: scalars)
            return .text(String(view))
        }
    }

    /// Whether the sibling at `index` counts as text for the purpose of flanking: a missing sibling
    /// or a line break is whitespace, anything else (bold, code, a link) is content.
    private static func neighbourIsContent(_ inlines: [MarkdownInline], at index: Int) -> Bool {
        guard inlines.indices.contains(index) else {
            return false
        }

        switch inlines[index] {
        case .softBreak, .lineBreak:
            return false
        default:
            return true
        }
    }

    // MARK: - Bare URLs

    private static func linkify(_ inline: MarkdownInline) -> [MarkdownInline] {
        guard case .text(let string) = inline else {
            return [inline]
        }

        let pattern = /(?:https?:\/\/|ftp:\/\/|mailto:|www\.)[^\s<]+/
        var result: [MarkdownInline] = []
        var cursor = string.startIndex
        for match in string.matches(of: pattern) {
            // A URL only starts a link at the beginning of a word, so "xhttp://…" stays text.
            if match.range.lowerBound > string.startIndex {
                let previous = string[string.index(before: match.range.lowerBound)]
                guard previous.isWhitespace || "*_~(".contains(previous) else {
                    continue
                }
            }

            let url = trimTrailingPunctuation(string[match.range])
            let prefixLength = url.hasPrefix("www.") ? 4 : (url.firstIndex(of: ":").map { url.distance(from: url.startIndex, to: $0) + 1 } ?? 0)
            guard url.count > prefixLength, url.dropFirst(prefixLength).contains(where: { $0.isLetter || $0.isNumber }) else {
                continue
            }

            let end = string.index(match.range.lowerBound, offsetBy: url.count)
            result.append(.text(String(string[cursor..<match.range.lowerBound])))
            let destination = url.hasPrefix("www.") ? "http://" + url : String(url)
            result.append(.link(destination: destination, children: [.text(String(url))]))
            cursor = end
        }

        result.append(.text(String(string[cursor...])))
        return result
    }

    /// Sentence punctuation after a URL belongs to the sentence, as does a closing parenthesis the
    /// URL never opened.
    private static func trimTrailingPunctuation(_ candidate: Substring) -> Substring {
        var url = candidate
        while let last = url.last {
            if "?!.,:;*_~'\"".contains(last) {
                url = url.dropLast()
            } else if last == ")", url.filter({ $0 == ")" }).count > url.filter({ $0 == "(" }).count {
                url = url.dropLast()
            } else {
                break
            }
        }
        return url
    }
}
