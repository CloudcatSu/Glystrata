using Glystrata.Controls;
using Glystrata.Core.Markdown;
using Glystrata.Preview;

namespace Glystrata.Views;

public partial class MarkdownHelpWindow : Window
{
    private readonly IMarkdownPreviewService _markdown = new MarkdownPreviewService();
    private readonly WpfMarkdownRenderer _renderer;
    private readonly LocalizationService _localization;
    private readonly AppSettings _settings;

    public MarkdownHelpWindow(Window owner, WpfMarkdownRenderer renderer, LocalizationService localization, AppSettings settings)
    {
        InitializeComponent();
        Owner = owner;
        CustomTitleBar.Attach(this, localization);
        DialogKeys.AttachEscapeToClose(this);
        _renderer = renderer;
        _localization = localization;
        _settings = settings;
        Title = _localization.Get("help.markdownGuide");
        Background = (Brush)Application.Current.FindResource("WindowBackgroundBrush");
        RootGrid.Background = Background;
        Render();
    }

    private void Render()
    {
        var markdown = _localization.Language == AppLanguage.English ? EnglishContent : ChineseContent;
        var parsed = _markdown.Parse(markdown, AppDomain.CurrentDomain.BaseDirectory);
        var palette = _settings.Theme == ThemeKind.Dark ? _settings.DarkReaderPalette : _settings.LightReaderPalette;
        Viewer.Document = _renderer.Render(parsed, _settings.PreviewTypography, _settings.Theme, _localization, palette);
    }

    private const string ChineseContent = """
        # Markdown 語法說明

        Glystrata 的文件就是純文字 Markdown 檔，可以用任何編輯器打開；以下是目前「閱讀器」會正確渲染的語法。

        ## 標題

        `# ` 到 `###### `（開頭 1 到 6 個 `#` 加一個空格）分別對應 H1 到 H6。

        ## 文字樣式

        - `**粗體**`
        - `*斜體*`
        - `***粗斜體***`
        - `~~刪除線~~`
        - `` `行內程式碼` ``

        ## 清單

        - `- `、`* `、`+ ` 開頭都是項目符號清單
        - `1. ` 開頭是數字清單；在清單項目結尾按 Enter，Glystrata 會自動幫下一行接上符號（數字會自動 +1），項目留空時按 Enter 則會取消清單
        - `- [ ] ` 是待辦事項，`- [x] ` 是已完成，同樣支援按 Enter 自動接續

        ## 引用與程式碼區塊

        - `> ` 開頭是引用
        - 三個反引號單獨一行開始、再三個反引號單獨一行結束，中間是程式碼區塊

        ## 分隔線

        前後各留一行空白，單獨一行打三個或以上的 `-`（例如 `---`）會變成貫通版面的分隔線。

        ## 表格

        用直線 `|` 分隔欄位，第二行用 `---` 標示分隔列，例如：

        `| 欄位一 | 欄位二 |`

        `| --- | --- |`

        `| 內容 | 內容 |`

        ## 連結與圖片

        - `[顯示文字](網址)`
        - `![替代文字](圖片路徑)`

        ## 輸入時自動轉換符號

        在編輯器裡打出以下符號組合，會立刻換成對應的特殊符號；換完後馬上按 Backspace 可以復原成原本打的字：

        - `->` → →
        - `=>` → ⇒
        - `<->` → ↔
        - `>=` → ≥
        - `<=` → ≤
        - `...` → …
        - `--` → —（如果整行到目前為止只有 `-`、`:`、`|`、空白，代表可能正在打分隔線或表格分隔列，就不會轉換）

        ## 目前不支援

        Glystrata 是單純的 Markdown 文字編輯器，沒有 Heptabase 那種「Toggle 收合區塊」的概念，也不支援英文字母（a.）或羅馬數字（i.）清單——這些不是標準 Markdown 語法。
        """;

    private const string EnglishContent = """
        # Markdown Syntax Guide

        Glystrata documents are plain-text Markdown files you can open in any editor. Below is the syntax the built-in Reader currently renders.

        ## Headings

        `# ` through `###### ` (1 to 6 leading `#` characters plus a space) map to H1 through H6.

        ## Text styles

        - `**bold**`
        - `*italic*`
        - `***bold italic***`
        - `~~strikethrough~~`
        - `` `inline code` ``

        ## Lists

        - `- `, `* `, or `+ ` starts a bullet list
        - `1. ` starts a numbered list. Press Enter at the end of a list item and Glystrata continues the marker on the next line automatically (numbers increment); pressing Enter on an empty item ends the list instead
        - `- [ ] ` is an unchecked to-do, `- [x] ` a checked one — Enter continues these too

        ## Quotes and code blocks

        - `> ` starts a quote
        - Three backticks on their own line open a code block; three more on their own line close it

        ## Dividers

        A line with three or more `-` characters (e.g. `---`), with a blank line before and after, becomes a full-width divider.

        ## Tables

        Separate columns with `|`, with a `---` row marking the header separator, e.g.:

        `| Column 1 | Column 2 |`

        `| --- | --- |`

        `| Value | Value |`

        ## Links and images

        - `[link text](url)`
        - `![alt text](image path)`

        ## Auto-replaced symbols while typing

        Typing these in the editor instantly swaps in the special character; press Backspace right after to undo:

        - `->` → →
        - `=>` → ⇒
        - `<->` → ↔
        - `>=` → ≥
        - `<=` → ≤
        - `...` → …
        - `--` → — (skipped while the line so far is only `-`, `:`, `|`, or spaces, so `---` dividers and table separator rows still work)

        ## Not supported

        Glystrata is a plain Markdown text editor — it has no Heptabase-style collapsible "Toggle" blocks, and no letter (`a.`) or Roman-numeral (`i.`) lists, since those aren't standard Markdown.
        """;
}
