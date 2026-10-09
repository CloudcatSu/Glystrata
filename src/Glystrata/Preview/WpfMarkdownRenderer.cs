using System.Xml.Linq;

namespace Glystrata.Preview;

public sealed class WpfMarkdownRenderer
{
    private static readonly HashSet<string> BlockElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "h1", "h2", "h3", "h4", "h5", "h6", "p", "ul", "ol", "blockquote", "pre", "table", "hr"
    };

    private readonly record struct RenderPalette(Brush Foreground, Brush Muted, Brush Surface, Brush Heading, Brush Link, Brush Quote, Brush Code);

    public FlowDocument Render(
        MarkdownPreviewDocument document,
        PreviewTypography typography,
        ThemeKind theme,
        LocalizationService localization,
        ReaderColorPalette? palette = null)
    {
        palette ??= theme == ThemeKind.Dark ? ReaderColorPalette.CreateDarkDefault() : ReaderColorPalette.CreateLightDefault();
        var defaultText = theme == ThemeKind.Dark ? "#E6EAF0" : "#24292F";
        var defaultMuted = theme == ThemeKind.Dark ? "#A6ADB8" : "#68707C";
        var defaultLink = theme == ThemeKind.Dark ? "#79C0FF" : "#0969DA";

        var render = new RenderPalette(
            Foreground: CreateBrush(palette.Get("text", defaultText)),
            Muted: CreateBrush(defaultMuted),
            Surface: CreateBrush(theme == ThemeKind.Dark ? "#292E36" : "#F1F3F5"),
            Heading: CreateBrush(palette.Get("heading", defaultText)),
            Link: CreateBrush(palette.Get("link", defaultLink)),
            Quote: CreateBrush(palette.Get("quote", defaultMuted)),
            Code: CreateBrush(palette.Get("code", defaultText)));

        var flow = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI, Segoe UI Emoji"),
            FontSize = 15,
            Foreground = render.Foreground,
            Background = (Brush)Application.Current.FindResource("PreviewBackgroundBrush"),
            PagePadding = new Thickness(34, 26, 34, 34),
            LineHeight = 22
        };

        try
        {
            var root = XElement.Parse($"<glystrata-root>{document.Html}</glystrata-root>", LoadOptions.PreserveWhitespace);
            foreach (var element in root.Elements())
            {
                AppendBlock(flow, element, document, typography, render, localization);
            }
        }
        catch (Exception exception) when (exception is System.Xml.XmlException or InvalidOperationException)
        {
            flow.Blocks.Add(new Paragraph(new Run(document.Html))
            {
                Margin = new Thickness(0),
                Foreground = render.Foreground
            });
        }

        if (flow.Blocks.Count == 0)
        {
            flow.Blocks.Add(new Paragraph(new Run(string.Empty)));
        }

        return flow;
    }

    private static void AppendBlock(
        FlowDocument flow,
        XElement element,
        MarkdownPreviewDocument document,
        PreviewTypography typography,
        RenderPalette render,
        LocalizationService localization)
    {
        var name = element.Name.LocalName.ToLowerInvariant();
        switch (name)
        {
            case "h1":
            case "h2":
            case "h3":
            case "h4":
            case "h5":
            case "h6":
                var level = int.Parse(name[1..], CultureInfo.InvariantCulture);
                var heading = new Paragraph
                {
                    Margin = new Thickness(0, typography.HeadingSpacing, 0, typography.ParagraphSpacing),
                    FontSize = GetHeadingSize(typography, level),
                    FontWeight = FontWeights.SemiBold,
                    Foreground = render.Heading,
                    LineHeight = GetHeadingSize(typography, level) * typography.LineSpacing
                };
                AddInlines(heading.Inlines, element.Nodes(), document, render.Heading, render, localization);
                flow.Blocks.Add(heading);
                break;

            case "p":
                var paragraph = new Paragraph
                {
                    Margin = new Thickness(0, 0, 0, typography.ParagraphSpacing),
                    Foreground = render.Foreground,
                    LineHeight = 15 * typography.LineSpacing
                };
                AddInlines(paragraph.Inlines, element.Nodes(), document, render.Foreground, render, localization);
                flow.Blocks.Add(paragraph);
                break;

            case "blockquote":
                var quote = new Paragraph
                {
                    Margin = new Thickness(12, 0, 0, typography.ParagraphSpacing),
                    Padding = new Thickness(12, 2, 0, 2),
                    BorderBrush = render.Muted,
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    Foreground = render.Quote,
                    LineHeight = 15 * typography.LineSpacing
                };
                foreach (var child in element.Elements())
                {
                    AddInlines(quote.Inlines, child.Nodes(), document, render.Quote, render, localization);
                }
                flow.Blocks.Add(quote);
                break;

            case "pre":
                var code = new Paragraph
                {
                    Margin = new Thickness(0, 4, 0, typography.ParagraphSpacing),
                    Padding = new Thickness(12),
                    Background = render.Surface,
                    Foreground = render.Code,
                    FontFamily = new FontFamily("Cascadia Mono, Consolas, Segoe UI Emoji"),
                    FontSize = 13,
                    LineHeight = 19
                };
                var codeText = element.Element("code")?.Value ?? element.Value;
                code.Inlines.Add(new Run(codeText));
                flow.Blocks.Add(code);
                break;

            case "ul":
            case "ol":
                flow.Blocks.Add(CreateList(element, document, typography, render, localization));
                break;

            case "table":
                flow.Blocks.Add(CreateTable(element, document, typography, render, localization));
                break;

            case "hr":
                flow.Blocks.Add(new BlockUIContainer(new Border
                {
                    Height = 1,
                    Background = render.Muted,
                    Margin = new Thickness(0, 4, 0, typography.ParagraphSpacing)
                }));
                break;

            default:
                if (element.Elements().Any(child => BlockElements.Contains(child.Name.LocalName)))
                {
                    foreach (var child in element.Elements())
                    {
                        AppendBlock(flow, child, document, typography, render, localization);
                    }
                }
                else
                {
                    var fallback = new Paragraph
                    {
                        Margin = new Thickness(0, 0, 0, typography.ParagraphSpacing),
                        Foreground = render.Foreground
                    };
                    AddInlines(fallback.Inlines, element.Nodes(), document, render.Foreground, render, localization);
                    flow.Blocks.Add(fallback);
                }
                break;
        }
    }

    private static List CreateList(
        XElement element,
        MarkdownPreviewDocument document,
        PreviewTypography typography,
        RenderPalette render,
        LocalizationService localization)
    {
        var items = element.Elements("li").ToArray();
        var isOrdered = element.Name.LocalName.Equals("ol", StringComparison.OrdinalIgnoreCase);
        // A task list ("- [ ] a") draws its own checkbox in place of the bullet; WPF has one marker
        // style per list, so the plain items in such a list get a hand-drawn bullet the same width as
        // the checkbox. An ordered list keeps its numbers and draws the checkbox after them, so its
        // plain items get an empty slot instead; either way every item's text starts at the same x.
        var taskCheckboxes = items.Select(FindTaskCheckbox).ToArray();
        foreach (var checkbox in taskCheckboxes)
        {
            checkbox?.AddAnnotation(TaskCheckboxAnnotation.Instance);
            // The slot already leaves a gap; Markdig's space after the checkbox would widen it.
            if (checkbox?.NextNode is XText after)
            {
                after.Value = after.Value.TrimStart();
            }
        }

        var isTaskList = taskCheckboxes.Any(checkbox => checkbox is not null);
        // An unordered task list draws its markers inside the text column, so it starts one slot further
        // left: the checkboxes then hang where bullets would and the text lines up with a plain list's.
        var hangsMarkers = isTaskList && !isOrdered;
        var list = new List
        {
            MarkerStyle = isOrdered
                ? TextMarkerStyle.Decimal
                : isTaskList
                    ? TextMarkerStyle.None
                    : TextMarkerStyle.Disc,
            Margin = new Thickness(hangsMarkers ? 0 : 18, 0, 0, typography.ParagraphSpacing),
            Padding = new Thickness(hangsMarkers ? 0 : 4, 0, 0, 0),
            Foreground = render.Foreground
        };

        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var listItem = new ListItem();
            var paragraph = new Paragraph
            {
                // A loose list (items separated by blank lines) wraps each item in <p>; keep its gap.
                Margin = new Thickness(0, 0, 0, item.Elements("p").Any() ? typography.ParagraphSpacing : 4),
                Foreground = render.Foreground,
                LineHeight = 15 * typography.LineSpacing
            };
            listItem.Blocks.Add(paragraph);
            if (isTaskList && taskCheckboxes[index] is null)
            {
                paragraph.Inlines.Add(CreateMarkerSlot(isOrdered ? null : CreateBullet(render)));
            }
            foreach (var node in item.Nodes())
            {
                if (node is XElement child && (child.Name.LocalName is "ul" or "ol"))
                {
                    var nested = CreateList(child, document, typography, render, localization);
                    if (isTaskList)
                    {
                        // This item's text sits one marker slot in from the list item's edge, so a child
                        // list measured from that edge has to move over by the slot to nest under the text.
                        var margin = nested.Margin;
                        margin.Left += MarkerSize + MarkerGap;
                        nested.Margin = margin;
                    }

                    listItem.Blocks.Add(nested);
                }
                else if (node is XText text && (text.NextNode is null || text.NextNode is XElement { Name.LocalName: "ul" or "ol" }))
                {
                    // Markdig ends "<li>a" with a newline before a nested list; drawn as-is it leaves an
                    // empty line between the item and its children.
                    AddInlines(paragraph.Inlines, new[] { new XText(text.Value.TrimEnd()) }, document, render.Foreground, render, localization);
                }
                else
                {
                    AddInlines(paragraph.Inlines, new[] { node }, document, render.Foreground, render, localization);
                }
            }

            list.ListItems.Add(listItem);
        }

        return list;
    }

    private static Table CreateTable(
        XElement element,
        MarkdownPreviewDocument document,
        PreviewTypography typography,
        RenderPalette render,
        LocalizationService localization)
    {
        var table = new Table
        {
            CellSpacing = 0,
            BorderBrush = render.Surface,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 2, 0, typography.ParagraphSpacing)
        };

        var rows = element.Descendants("tr").ToArray();
        var columnCount = rows.Select(row => row.Elements().Count()).DefaultIfEmpty(1).Max();
        for (var column = 0; column < columnCount; column++)
        {
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        }

        var group = new TableRowGroup();
        foreach (var row in rows)
        {
            var tableRow = new TableRow();
            foreach (var cell in row.Elements())
            {
                var paragraph = new Paragraph
                {
                    Margin = new Thickness(7, 4, 7, 4),
                    Foreground = render.Foreground,
                    LineHeight = 15 * typography.LineSpacing
                };
                AddInlines(paragraph.Inlines, cell.Nodes(), document, render.Foreground, render, localization);
                tableRow.Cells.Add(new TableCell(paragraph)
                {
                    BorderBrush = render.Surface,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Background = cell.Parent?.Name.LocalName == "thead" ? render.Surface : null
                });
            }
            group.Rows.Add(tableRow);
        }

        table.RowGroups.Add(group);
        return table;
    }

    private static void AddInlines(
        InlineCollection target,
        IEnumerable<XNode> nodes,
        MarkdownPreviewDocument document,
        Brush foreground,
        RenderPalette render,
        LocalizationService localization)
    {
        foreach (var node in nodes)
        {
            if (node is XText text)
            {
                EmojiRenderer.AddText(target, text.Value, foreground);
                continue;
            }

            if (node is not XElement element)
            {
                continue;
            }

            var name = element.Name.LocalName.ToLowerInvariant();
            switch (name)
            {
                case "strong":
                    var bold = new Bold { Foreground = foreground };
                    AddInlines(bold.Inlines, element.Nodes(), document, foreground, render, localization);
                    target.Add(bold);
                    break;
                case "em":
                    var italic = new Italic { Foreground = foreground };
                    AddInlines(italic.Inlines, element.Nodes(), document, foreground, render, localization);
                    target.Add(italic);
                    break;
                case "del":
                case "s":
                    var strike = new Run(element.Value) { Foreground = foreground, TextDecorations = TextDecorations.Strikethrough };
                    target.Add(strike);
                    break;
                case "code":
                    target.Add(new Run(element.Value)
                    {
                        Foreground = render.Code,
                        Background = render.Surface,
                        FontFamily = new FontFamily("Cascadia Mono, Consolas, Segoe UI Emoji"),
                        FontSize = 13
                    });
                    break;
                case "a":
                    var link = new Hyperlink { Foreground = render.Link };
                    AddInlines(link.Inlines, element.Nodes(), document, link.Foreground, render, localization);
                    var href = element.Attribute("href")?.Value;
                    if (!string.IsNullOrWhiteSpace(href))
                    {
                        link.ToolTip = href;
                        link.Click += (_, _) => OpenLink(href);
                    }
                    target.Add(link);
                    break;
                case "br":
                    target.Add(new LineBreak());
                    break;
                case "img":
                    var source = element.Attribute("src")?.Value ?? string.Empty;
                    var resource = document.Resources.FirstOrDefault(item => item.Source == source);
                    if (resource?.IsAvailable == true && resource.ResolvedPath is not null)
                    {
                        try
                        {
                            var image = new Image
                            {
                                Source = new BitmapImage(new Uri(resource.ResolvedPath, UriKind.Absolute)),
                                MaxWidth = 720,
                                Stretch = Stretch.Uniform,
                                ToolTip = element.Attribute("alt")?.Value
                            };
                            target.Add(new InlineUIContainer(image));
                        }
                        catch (Exception exception) when (exception is IOException or UriFormatException or NotSupportedException)
                        {
                            target.Add(new Run($"[{localization.Get("preview.missingImage")}: {source}]") { Foreground = Brushes.IndianRed });
                        }
                    }
                    else
                    {
                        target.Add(new Run($"[{localization.Get("preview.missingImage")}: {source}]") { Foreground = Brushes.IndianRed });
                    }
                    break;
                case "input":
                    if (IsCheckbox(element))
                    {
                        var isChecked = element.Attribute("checked") is not null;
                        // Markdig also turns a "[x]" on a continuation line into a checkbox; like GitHub,
                        // only the one opening a list item is a task marker, the rest stay as typed.
                        target.Add(element.Annotation<TaskCheckboxAnnotation>() is not null
                            ? CreateCheckbox(isChecked, render)
                            : new Run(isChecked ? "[x]" : "[ ]") { Foreground = foreground });
                    }
                    break;
                default:
                    AddInlines(target, element.Nodes(), document, foreground, render, localization);
                    break;
            }
        }
    }

    private static bool IsCheckbox(XElement element) =>
        string.Equals(element.Attribute("type")?.Value, "checkbox", StringComparison.OrdinalIgnoreCase);

    // A task item opens with its checkbox: first inside the <li>, or first inside its leading <p> when the
    // list is loose. A checkbox anywhere else (a continuation line, a nested list) does not count.
    private static XElement? FindTaskCheckbox(XElement item)
    {
        var first = FirstContent(item);
        if (first is XElement { Name.LocalName: "p" } paragraph)
        {
            first = FirstContent(paragraph);
        }

        return first is XElement { Name.LocalName: "input" } input && IsCheckbox(input) ? input : null;
    }

    private static XNode? FirstContent(XElement element) =>
        element.Nodes().FirstOrDefault(node => node is not XText text || !string.IsNullOrWhiteSpace(text.Value));

    private sealed class TaskCheckboxAnnotation
    {
        public static readonly TaskCheckboxAnnotation Instance = new();
    }

    private const double MarkerSize = 14;

    // Slot plus gap equals a plain list's margin plus padding (18 + 4), so task and plain text align.
    private const double MarkerGap = 8;

    // The fixed-width box in front of a task-list item's text: a checkbox, a bullet, or nothing.
    private static InlineUIContainer CreateMarkerSlot(UIElement? content) =>
        new(new Border
        {
            Width = MarkerSize,
            Height = MarkerSize,
            Margin = new Thickness(0, 0, MarkerGap, 0),
            Child = content,
            IsHitTestVisible = false
        })
        { BaselineAlignment = BaselineAlignment.Center };

    private static UIElement CreateBullet(RenderPalette render) =>
        new System.Windows.Shapes.Ellipse
        {
            Width = 5.5,
            Height = 5.5,
            Fill = render.Foreground,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

    private static InlineUIContainer CreateCheckbox(bool isChecked, RenderPalette render)
    {
        var box = new Border
        {
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1.4),
            BorderBrush = isChecked ? render.Link : render.Muted,
            Background = isChecked ? render.Link : Brushes.Transparent,
            SnapsToDevicePixels = true
        };
        if (isChecked)
        {
            box.Child = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 2.6,6.6 L 5.4,9.4 L 10.4,3.6"),
                Stroke = Brushes.White,
                StrokeThickness = 1.7,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Stretch = Stretch.None
            };
        }

        return CreateMarkerSlot(box);
    }

    private static double GetHeadingSize(PreviewTypography typography, int level) => level switch
    {
        1 => typography.H1Size,
        2 => typography.H2Size,
        3 => typography.H3Size,
        4 => typography.H4Size,
        5 => typography.H5Size,
        _ => typography.H6Size
    };

    private static void OpenLink(string href)
    {
        if (!Uri.TryCreate(href, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeMailto))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(href) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or UriFormatException)
        {
            // 閱讀器中的連結開啟失敗時不應影響編輯器。
        }
    }

    private static Brush CreateBrush(string value)
    {
        try
        {
            var brush = (Brush?)new BrushConverter().ConvertFromString(value) ?? Brushes.Gray;
            brush.Freeze();
            return brush;
        }
        catch (FormatException)
        {
            return Brushes.Gray;
        }
    }
}
