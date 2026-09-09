using Markdig;

namespace Glystrata.Core.Markdown;

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
