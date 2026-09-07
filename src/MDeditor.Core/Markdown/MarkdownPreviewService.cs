using System.Text.RegularExpressions;
using Markdig;

namespace MDeditor.Core.Markdown;

public interface IMarkdownPreviewService
{
    MarkdownPreviewDocument Parse(string markdown, string sourceDirectory);
}

public sealed class MarkdownPreviewService : IMarkdownPreviewService
{
    private static readonly Regex ImageSourcePattern = new(
        "<img\\s+[^>]*src=\\\"(?<src>[^\\\"]+)\\\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly MarkdownPipeline _pipeline;

    public MarkdownPreviewService(MarkdownPipeline? pipeline = null)
    {
        _pipeline = pipeline ?? MarkdownPipelineFactory.Create();
    }

    public MarkdownPreviewDocument Parse(string markdown, string sourceDirectory)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);

        var directory = Path.GetFullPath(sourceDirectory);
        var html = Markdig.Markdown.ToHtml(markdown, _pipeline);
        var resources = ImageSourcePattern.Matches(html)
            .Select(match => ResolveResource(match.Groups["src"].Value, directory))
            .ToArray();
        return new MarkdownPreviewDocument(html, directory, resources);
    }

    private static PreviewResource ResolveResource(string source, string baseDirectory)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var absoluteUri) && absoluteUri.IsFile)
        {
            var filePath = absoluteUri.LocalPath;
            return CreateFileResource(source, filePath, baseDirectory);
        }

        if (Uri.TryCreate(source, UriKind.Absolute, out _))
        {
            return new PreviewResource(source, null, false, "預覽只載入本機相對圖片。");
        }

        var cleanSource = source.Split('#', 2)[0].Split('?', 2)[0];
        if (string.IsNullOrWhiteSpace(cleanSource))
        {
            return new PreviewResource(source, null, false, "圖片路徑是空的。");
        }

        try
        {
            var path = Path.GetFullPath(Path.Combine(baseDirectory, cleanSource));
            return CreateFileResource(source, path, baseDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return new PreviewResource(source, null, false, "圖片路徑無效。");
        }
    }

    private static PreviewResource CreateFileResource(string source, string path, string baseDirectory)
    {
        if (!IsWithinDirectory(path, baseDirectory))
        {
            return new PreviewResource(source, null, false, "圖片路徑必須位於文件目錄內。");
        }

        return File.Exists(path)
            ? new PreviewResource(source, path, true, null)
            : new PreviewResource(source, path, false, "找不到圖片檔案。");
    }

    private static bool IsWithinDirectory(string path, string baseDirectory)
    {
        var normalizedBase = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDirectory)) + Path.DirectorySeparatorChar;
        var normalizedPath = Path.GetFullPath(path);
        return normalizedPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.TrimEndingDirectorySeparator(normalizedPath), Path.TrimEndingDirectorySeparator(baseDirectory), StringComparison.OrdinalIgnoreCase);
    }
}
