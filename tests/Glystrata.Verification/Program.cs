using Glystrata.Core.Documents;
using Glystrata.Core.Groups;
using Glystrata.Core.Layout;
using Glystrata.Core.Markdown;
using Glystrata.Core.Persistence;
using Glystrata.Core.Snapshots;

namespace Glystrata.Verification;

internal static class Program
{
    public static async Task<int> Main()
    {
        var root = Path.Combine(Path.GetTempPath(), $"Glystrata.Verification-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            FileCodecVerification.Run(root);
            DocumentManagerVerification.Run(root);
            PaneLayoutVerification.Run();
            GroupVerification.Run(root);
            PersistenceVerification.Run(root);
            MarkdownVerification.Run(root);
            await SnapshotVerification.RunAsync(root);
            DiffVerification.Run();
            TextMetricsVerification.Run();
            MarkdownFormattingVerification.Run();
            Console.WriteLine("All Glystrata verification assertions passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch (IOException)
            {
                // The process exit result remains useful even when a test file is locked.
            }
        }
    }
}

internal static class DocumentManagerVerification
{
    public static void Run(string root)
    {
        var path = Path.Combine(root, "views.md");
        File.WriteAllText(path, "# views");
        using var manager = new DocumentManager();
        var first = manager.OpenView(path, Guid.NewGuid());
        var second = manager.OpenView(path, Guid.NewGuid(), newView: true);
        VerificationAssert.True(first.ViewId != second.ViewId, "同檔案的新檢視應有獨立 view id。");
        VerificationAssert.True(ReferenceEquals(first.Document, second.Document), "同一檔案的多個檢視應共用文件內容。");
        first.VerticalOffset = 120;
        second.VerticalOffset = 480;
        VerificationAssert.Equal(120d, first.VerticalOffset, "第一個檢視應保存自己的捲動位置。");
        VerificationAssert.Equal(480d, second.VerticalOffset, "第二個檢視應保存自己的捲動位置。");

        var savePath = Path.Combine(root, "save.md");
        File.WriteAllText(savePath, "before");
        using var saveManager = new DocumentManager();
        var document = saveManager.Open(savePath);
        document.TextDocument.Text = "after";
        var results = Enumerable.Range(0, 4)
            .Select(_ => saveManager.SaveAsync(document))
            .ToArray();
        Task.WhenAll(results).GetAwaiter().GetResult();
        VerificationAssert.True(results.All(task => task.Result.Success), "重疊儲存應全部安全完成。");
        VerificationAssert.Equal("after", File.ReadAllText(savePath), "重疊儲存後檔案內容不正確。");
        VerificationAssert.True(!document.IsModified, "成功儲存後文件不應保持未儲存狀態。");

        using var failedDocument = new DocumentSession(
            Path.Combine(root, "missing-directory", "failed.md"),
            "initial",
            FileEncodingKind.Utf8,
            LineEndingKind.Lf);
        failedDocument.TextDocument.Text = "changed";
        var failed = saveManager.SaveAsync(failedDocument).GetAwaiter().GetResult();
        VerificationAssert.True(!failed.Success, "無法寫入的路徑應回傳儲存失敗結果。");
        VerificationAssert.True(failedDocument.IsModified, "儲存失敗時應保留未儲存狀態。");
        VerificationAssert.True(!string.IsNullOrWhiteSpace(failedDocument.LastError), "儲存失敗應留下錯誤狀態。");
    }
}

internal static class PaneLayoutVerification
{
    public static void Run()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        var root = new PaneLayoutNode.Split(
            Guid.NewGuid(),
            SplitOrientation.Horizontal,
            0.5,
            new PaneLayoutNode.EditorPane(first),
            new PaneLayoutNode.Split(
                Guid.NewGuid(),
                SplitOrientation.Vertical,
                0.5,
                new PaneLayoutNode.EditorPane(second),
                new PaneLayoutNode.EditorPane(third)));

        VerificationAssert.Equal(3, PaneLayoutOperations.EnumeratePaneIds(root).Count(), "巢狀布局 pane 數量不正確。");
        var removedRoot = PaneLayoutOperations.RemovePane(root, second, out var removed);
        VerificationAssert.True(removed && removedRoot is not null, "移除巢狀 pane 失敗。");
        VerificationAssert.Equal(2, PaneLayoutOperations.EnumeratePaneIds(removedRoot!).Count(), "移除 pane 後應直接收合 split。");
        VerificationAssert.True(!PaneLayoutOperations.ContainsPane(removedRoot!, second), "移除後不應保留目標 pane。");

        var collapsed = PaneLayoutOperations.CollapseToSinglePane(root);
        VerificationAssert.True(collapsed is PaneLayoutNode.EditorPane, "單窗格還原後應只剩 editor pane。");
        VerificationAssert.Equal(first, PaneLayoutOperations.GetFirstPaneId(collapsed), "單窗格還原應保留第一個 pane ID。");
    }
}

internal static class VerificationAssert
{
    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected: {expected}; actual: {actual}.");
        }
    }
}

internal static class FileCodecVerification
{
    public static void Run(string root)
    {
        var codec = new FileCodec();
        var cases = new[]
        {
            (FileEncodingKind.Utf8, LineEndingKind.Lf),
            (FileEncodingKind.Utf8WithBom, LineEndingKind.CrLf),
            (FileEncodingKind.Utf16LittleEndian, LineEndingKind.Cr)
        };

        foreach (var (encoding, lineEnding) in cases)
        {
            var source = "標題\n第二行\n";
            var path = Path.Combine(root, $"codec-{encoding}-{lineEnding}.txt");
            File.WriteAllBytes(path, codec.Encode(source, encoding, lineEnding));
            var decoded = codec.Read(path);
            VerificationAssert.Equal(source, decoded.Text, "文字 round-trip 失敗。");
            VerificationAssert.Equal(encoding, decoded.Encoding, "編碼辨識失敗。");
            VerificationAssert.Equal(lineEnding, decoded.LineEnding, "換行格式辨識失敗。");
        }
    }
}

internal static class GroupVerification
{
    public static void Run(string root)
    {
        var file = Path.Combine(root, "group.md");
        var directory = Path.Combine(root, "group-folder");
        File.WriteAllText(file, "# group");
        Directory.CreateDirectory(directory);

        var manager = new GroupManager();
        var first = manager.CreateGroup("第一群組");
        var second = manager.CreateGroup("第二群組");
        manager.AddPath(first.Id, file, GroupItemKind.File);
        manager.AddPath(first.Id, directory, GroupItemKind.Folder);
        manager.AddPath(second.Id, file, GroupItemKind.File);
        manager.AddPath(first.Id, file, GroupItemKind.File);

        VerificationAssert.Equal(2, first.Items.Count, "群組內路徑未正確去重。");
        VerificationAssert.Equal(1, second.Items.Count, "同檔案加入第二群組失敗。");
        VerificationAssert.True(manager.MovePath(first.Id, second.Id, file), "移動虛擬捷徑失敗。");
        VerificationAssert.Equal(1, first.Items.Count, "移動後來源群組仍保留檔案捷徑。");
        VerificationAssert.Equal(1, second.Items.Count, "移動至已有捷徑的群組不應建立重複項目。");
        VerificationAssert.True(File.Exists(file), "移動群組捷徑不應搬動實體檔案。");
    }
}

internal static class PersistenceVerification
{
    public static void Run(string root)
    {
        var store = new JsonStateStore(Path.Combine(root, "state"));
        var settings = new AppSettings
        {
            Language = AppLanguage.English,
            SnapshotIntervalMinutes = 8,
            MaxSnapshotsPerFile = 12,
            CharacterCountMode = CharacterCountMode.ExcludeLineBreaks,
            ShowFormattingToolbar = true
        };
        store.SaveSettingsAsync(settings).GetAwaiter().GetResult();
        var loaded = store.LoadSettingsAsync().GetAwaiter().GetResult();
        VerificationAssert.Equal(AppLanguage.English, loaded.Language, "設定保存失敗。");
        VerificationAssert.Equal(8, loaded.SnapshotIntervalMinutes, "快照間隔保存失敗。");
        VerificationAssert.Equal(CharacterCountMode.ExcludeLineBreaks, loaded.CharacterCountMode, "字元統計模式保存失敗。");
        VerificationAssert.True(loaded.ShowFormattingToolbar, "快捷列顯示設定保存失敗。");

        var settingsPath = Path.Combine(store.BaseDirectory, "settings.json");
        File.WriteAllText(settingsPath, "{ broken json");
        var fallback = store.LoadSettingsAsync().GetAwaiter().GetResult();
        VerificationAssert.Equal(AppLanguage.TraditionalChinese, fallback.Language, "損壞設定未回退預設值。");
        VerificationAssert.True(
            Directory.EnumerateFiles(store.BaseDirectory, "settings.json.corrupt-*.json").Any(),
            "損壞設定未保存診斷副本。");
    }
}

internal static class MarkdownVerification
{
    public static void Run(string root)
    {
        var service = new MarkdownPreviewService();
        var markdown = "---\ntitle: 測試\n---\n# 標題\n\n- [x] 完成\n- 第二項\n\n| A | B |\n| --- | --- |\n| 1 | 2 |\n\n<script>alert('blocked')</script>\n";
        var result = service.Parse(markdown, root);
        VerificationAssert.True(result.Html.Contains("<h1>", StringComparison.Ordinal), "Markdown 標題解析失敗。");
        VerificationAssert.True(result.Html.Contains("<table>", StringComparison.Ordinal), "Markdown 表格解析失敗。");
        VerificationAssert.True(!result.Html.Contains("<script", StringComparison.OrdinalIgnoreCase), "預覽不應保留 script。");
        VerificationAssert.True(!result.Html.Contains("title: 測試", StringComparison.Ordinal), "YAML front matter 不應顯示在正文。");
    }
}

internal static class SnapshotVerification
{
    public static Task RunAsync(string root)
    {
        var path = Path.Combine(root, "snapshot.md");
        File.WriteAllText(path, "第一版");
        using var documents = new DocumentManager();
        var document = documents.Open(path);
        var snapshots = new SnapshotService();
        var first = snapshots.CreateAsync(document, 2).GetAwaiter().GetResult();
        VerificationAssert.True(first is not null, "第一份快照未建立。");

        document.TextDocument.Text = "第二版";
        var second = snapshots.CreateAsync(document, 2).GetAwaiter().GetResult();
        VerificationAssert.True(second is not null, "第二份快照未建立。");
        document.TextDocument.Text = "第三版";
        var third = snapshots.CreateAsync(document, 2).GetAwaiter().GetResult();
        VerificationAssert.True(third is not null, "第三份快照未建立。");

        var sidecar = SnapshotSidecarStore.GetSidecarPath(path);
        VerificationAssert.True(File.Exists(sidecar), "快照 sidecar 不存在。");
        VerificationAssert.True(!File.GetAttributes(sidecar).HasFlag(FileAttributes.Hidden), "預設情況下快照 sidecar 不應被隱藏。");

        File.SetAttributes(sidecar, File.GetAttributes(sidecar) | FileAttributes.Hidden);
        document.TextDocument.Text = "第四版";
        var fourth = snapshots.CreateAsync(document, 2).GetAwaiter().GetResult();
        VerificationAssert.True(fourth is not null, "第四份快照未建立。");
        VerificationAssert.True(!File.GetAttributes(sidecar).HasFlag(FileAttributes.Hidden), "手動隱藏的 sidecar 應在下次寫入後恢復可見。");

        var listed = snapshots.ListAsync(path).GetAwaiter().GetResult();
        VerificationAssert.Equal(2, listed.Count, "快照數量上限未生效。");

        snapshots.DeleteAsync(path, listed[0].Id).GetAwaiter().GetResult();
        VerificationAssert.Equal(1, snapshots.ListAsync(path).GetAwaiter().GetResult().Count, "單次快照刪除失敗。");
        snapshots.DeleteAllAsync(path).GetAwaiter().GetResult();
        VerificationAssert.True(!File.Exists(sidecar), "整個快照 sidecar 刪除失敗。");

        var hiddenPath = Path.Combine(root, "snapshot-hidden.md");
        File.WriteAllText(hiddenPath, "第一版");
        using var hiddenDocuments = new DocumentManager();
        var hiddenDocument = hiddenDocuments.Open(hiddenPath);
        var hiddenStore = new SnapshotSidecarStore { HideSidecarFiles = true };
        var hiddenSnapshots = new SnapshotService(hiddenStore);
        var hiddenFirst = hiddenSnapshots.CreateAsync(hiddenDocument, 2).GetAwaiter().GetResult();
        VerificationAssert.True(hiddenFirst is not null, "隱藏模式下的第一份快照未建立。");

        var hiddenSidecar = SnapshotSidecarStore.GetSidecarPath(hiddenPath);
        VerificationAssert.True(File.Exists(hiddenSidecar), "隱藏模式下快照 sidecar 不存在。");
        VerificationAssert.True(File.GetAttributes(hiddenSidecar).HasFlag(FileAttributes.Hidden), "啟用隱藏設定時新寫入的 sidecar 應被隱藏。");

        SnapshotSidecarStore.ApplyVisibility(hiddenPath, false);
        VerificationAssert.True(!File.GetAttributes(hiddenSidecar).HasFlag(FileAttributes.Hidden), "ApplyVisibility(false) 應使 sidecar 恢復可見。");

        hiddenSnapshots.DeleteAllAsync(hiddenPath).GetAwaiter().GetResult();
        return Task.CompletedTask;
    }
}

internal static class DiffVerification
{
    public static void Run()
    {
        var diff = new TextDiffService().Compare("a\nb\n", "a\nc\n");
        VerificationAssert.True(diff.Any(line => line.Kind == DiffLineKind.Removed && line.Text == "b"), "差異未標示移除行。");
        VerificationAssert.True(diff.Any(line => line.Kind == DiffLineKind.Added && line.Text == "c"), "差異未標示新增行。");
    }
}

internal static class TextMetricsVerification
{
    public static void Run()
    {
        const string text = "A\u0301 B\n";
        VerificationAssert.Equal(4, TextMetrics.CountCharacters(text, CharacterCountMode.IncludeWhitespace), "Unicode text element 計數不正確。");
        VerificationAssert.Equal(2, TextMetrics.CountCharacters(text, CharacterCountMode.ExcludeWhitespace), "排除空白的字元計數不正確。");
        VerificationAssert.Equal(3, TextMetrics.CountCharacters(text, CharacterCountMode.ExcludeLineBreaks), "排除換行的字元計數不正確。");
    }
}

internal static class MarkdownFormattingVerification
{
    public static void Run()
    {
        var service = new MarkdownFormattingService();
        var bold = service.Apply("Hello", 0, 5, MarkdownFormatCommand.Bold);
        VerificationAssert.Equal("**Hello**", bold.Text, "粗體格式化結果不正確。");
        VerificationAssert.Equal(2, bold.SelectionStart, "粗體格式化後選取起點不正確。");
        VerificationAssert.Equal(5, bold.SelectionLength, "粗體格式化後選取長度不正確。");

        var heading = service.Apply("# Hello\nWorld", 0, 7, MarkdownFormatCommand.Heading2);
        VerificationAssert.Equal("## Hello\nWorld", heading.Text, "標題格式化結果不正確。");

        var list = service.Apply("a\nb", 0, 3, MarkdownFormatCommand.UnorderedList);
        VerificationAssert.Equal("- a\n- b", list.Text, "無序清單格式化結果不正確。");

        var ordered = service.Apply("a\nb", 0, 3, MarkdownFormatCommand.OrderedList);
        VerificationAssert.Equal("1. a\n1. b", ordered.Text, "有序清單格式化結果不正確。");

        var link = service.Apply("文件", 0, 2, MarkdownFormatCommand.Link, "https://example.com");
        VerificationAssert.Equal("[文件](https://example.com)", link.Text, "連結格式化結果不正確。");

        var code = service.Apply("value", 0, 5, MarkdownFormatCommand.CodeBlock);
        VerificationAssert.Equal("```\nvalue\n```", code.Text, "程式碼區塊格式化結果不正確。");
    }
}
