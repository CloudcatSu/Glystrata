import Foundation

// What the reader draws. Kept as our own small tree rather than exposing the parser library's
// nodes, so the renderer does not change if the parser underneath ever does.

public indirect enum MarkdownInline: Equatable, Sendable {
    case text(String)
    case strong([MarkdownInline])
    case emphasis([MarkdownInline])
    case strikethrough([MarkdownInline])
    case code(String)
    case link(destination: String, children: [MarkdownInline])
    case image(source: String, alt: String)
    case lineBreak
    case softBreak
}

public indirect enum MarkdownBlock: Equatable, Sendable {
    case heading(level: Int, inlines: [MarkdownInline])
    case paragraph([MarkdownInline])
    case blockQuote([MarkdownBlock])
    case codeBlock(language: String?, code: String)
    case list(MarkdownList)
    case table(MarkdownTable)
    case thematicBreak
}

public struct MarkdownList: Equatable, Sendable {
    public var isOrdered: Bool
    public var start: Int
    public var items: [MarkdownListItem]
}

public struct MarkdownListItem: Equatable, Sendable {
    /// nil for an ordinary item; otherwise whether the task is ticked.
    public var isChecked: Bool?
    public var blocks: [MarkdownBlock]
}

public enum MarkdownColumnAlignment: Equatable, Sendable {
    case left
    case center
    case right
}

public struct MarkdownTable: Equatable, Sendable {
    public var alignments: [MarkdownColumnAlignment?]
    public var header: [[MarkdownInline]]
    public var rows: [[[MarkdownInline]]]
}

public struct PreviewResource: Equatable, Sendable {
    public var source: String
    public var resolvedPath: String?
    public var isAvailable: Bool
    public var errorMessage: String?
}

public struct MarkdownPreviewDocument: Equatable, Sendable {
    public var blocks: [MarkdownBlock]
    public var sourceDirectory: String
    public var resources: [PreviewResource]
}
