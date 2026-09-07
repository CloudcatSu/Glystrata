namespace MDeditor.Core.Markdown;

public sealed record MarkdownPreviewDocument(
    string Html,
    string SourceDirectory,
    IReadOnlyList<PreviewResource> Resources);

public sealed record PreviewResource(
    string Source,
    string? ResolvedPath,
    bool IsAvailable,
    string? ErrorMessage);
