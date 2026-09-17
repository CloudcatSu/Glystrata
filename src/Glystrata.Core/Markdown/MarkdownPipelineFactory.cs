using Markdig;
using Markdig.Parsers;

namespace Glystrata.Core.Markdown;

public static class MarkdownPipelineFactory
{
    public static MarkdownPipeline Create()
    {
        var builder = new MarkdownPipelineBuilder()
            .UsePipeTables()
            .UseTaskLists()
            .UseEmphasisExtras()
            .UseAutoLinks()
            .UseYamlFrontMatter()
            .DisableHtml();

        // Setext headings (a line of "-"/"=" directly under a paragraph, with no blank line
        // separating them) silently promote that whole paragraph into a heading. Users who only
        // ever write headings with "# " and use a lone "-" as a stray dash get surprised by their
        // paragraph suddenly rendering huge. Turn that CommonMark corner case off; ATX ("#") headings
        // and blank-line-separated "---" thematic breaks are unaffected.
        var paragraphParser = builder.BlockParsers.Find<ParagraphBlockParser>();
        if (paragraphParser is not null)
        {
            paragraphParser.ParseSetexHeadings = false;
        }

        return builder.Build();
    }
}
