using Markdig;

namespace MDeditor.Core.Markdown;

public static class MarkdownPipelineFactory
{
    public static MarkdownPipeline Create()
    {
        return new MarkdownPipelineBuilder()
            .UsePipeTables()
            .UseTaskLists()
            .UseEmphasisExtras()
            .UseAutoLinks()
            .UseYamlFrontMatter()
            .DisableHtml()
            .Build();
    }
}
