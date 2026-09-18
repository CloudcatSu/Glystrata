using Glystrata.Core.Documents;
using Glystrata.Core.Groups;
using Glystrata.Core.Layout;
using Glystrata.Core.Markdown;
using Glystrata.Core.Persistence;
using Glystrata.Core.Snapshots;
using Glystrata.Input;
using Glystrata.Services;
using System.Windows.Input;

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
            ShortcutVerification.Run();
            RecentFilesVerification.Run(root);
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

        VerificationAssert.True(first.Color is null, "新群組預設不應有顏色。");
        VerificationAssert.True(manager.SetGroupColor(first.Id, "#2DA44E"), "設定群組顏色失敗。");
        VerificationAssert.Equal("#2DA44E", first.Color, "群組顏色未寫入。");
        VerificationAssert.True(manager.SetGroupColor(first.Id, null), "清除群組顏色失敗。");
        VerificationAssert.True(first.Color is null, "清除後群組不應保留顏色。");
        // Whitespace has to clear the colour rather than be stored: a blank bar would still take layout
        // space and TryCreateBar would silently treat it as "no colour" anyway.
        manager.SetGroupColor(first.Id, "   ");
        VerificationAssert.True(first.Color is null, "空白顏色字串應視為未指定。");

        manager.SetGroupColor(second.Id, "#CF222E");
        var restored = new GroupManager();
        GroupsState.FromGroups(manager.Groups).ApplyTo(restored);
        VerificationAssert.Equal("#CF222E", restored.Find(second.Id)?.Color, "群組顏色未隨群組狀態保存。");
        VerificationAssert.True(restored.Find(first.Id)?.Color is null, "未指定顏色的群組不應在還原後得到顏色。");
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

        // settings.json stores enums as plain numbers, so ThemePreference's numeric values are a
        // compatibility contract: a file written before "follow the system" existed must keep its theme.
        File.WriteAllText(settingsPath, """{ "schemaVersion": 1, "theme": 1 }""");
        var legacyDark = store.LoadSettingsAsync().GetAwaiter().GetResult();
        VerificationAssert.Equal(ThemePreference.Dark, legacyDark.Theme, "舊設定檔的暗色主題應維持暗色。");

        File.WriteAllText(settingsPath, """{ "schemaVersion": 1, "theme": 0 }""");
        var legacyLight = store.LoadSettingsAsync().GetAwaiter().GetResult();
        VerificationAssert.Equal(ThemePreference.Light, legacyLight.Theme, "舊設定檔的亮色主題應維持亮色。");

        File.WriteAllText(settingsPath, """{ "schemaVersion": 1 }""");
        var withoutTheme = store.LoadSettingsAsync().GetAwaiter().GetResult();
        VerificationAssert.Equal(ThemePreference.System, withoutTheme.Theme, "未指定主題時應預設為跟隨系統。");

        File.WriteAllText(settingsPath, """{ "schemaVersion": 1, "theme": 99 }""");
        var outOfRange = store.LoadSettingsAsync().GetAwaiter().GetResult();
        VerificationAssert.Equal(ThemePreference.System, outOfRange.Theme, "超出範圍的主題值應回退為跟隨系統。");
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
    public static async Task RunAsync(string root)
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

        var sidecarContent = await File.ReadAllTextAsync(sidecar);
        VerificationAssert.True(sidecarContent.Contains("\"notice\"", StringComparison.Ordinal), "sidecar 未包含 notice 欄位。");
        VerificationAssert.True(sidecarContent.Contains(Path.GetFileName(path), StringComparison.Ordinal), "notice 未包含文件名。");
        var noticeIndex = sidecarContent.IndexOf("\"notice\"", StringComparison.Ordinal);
        var snapshotsIndex = sidecarContent.IndexOf("\"snapshots\"", StringComparison.Ordinal);
        VerificationAssert.True(noticeIndex < snapshotsIndex, "notice 應在 snapshots 之前。");

        var listed = snapshots.ListAsync(path).GetAwaiter().GetResult();
        VerificationAssert.Equal(2, listed.Count, "快照數量上限未生效。");
        VerificationAssert.Equal(string.Empty, listed[0].Note, "新快照的註解預設應為空字串。");

        var noteUpdated = snapshots.UpdateNoteAsync(path, listed[0].Id, "測試註解").GetAwaiter().GetResult();
        VerificationAssert.True(noteUpdated, "更新註解應回傳成功。");
        var afterNoteUpdate = snapshots.ListAsync(path).GetAwaiter().GetResult();
        VerificationAssert.Equal("測試註解", afterNoteUpdate.Single(entry => entry.Id == listed[0].Id).Note, "註解未能在 sidecar 寫入／讀回後保留。");

        var unknownNoteUpdated = snapshots.UpdateNoteAsync(path, Guid.NewGuid(), "不存在").GetAwaiter().GetResult();
        VerificationAssert.True(!unknownNoteUpdated, "更新不存在的快照 id 應回傳失敗。");

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

        // A sidecar created by a build from before the project was renamed from MDeditor to Glystrata
        // uses the old ".mdeditor-snapshots.json" suffix and must still be picked up.
        var legacyPath = Path.Combine(root, "snapshot-legacy.md");
        File.WriteAllText(legacyPath, "舊版內容");
        var legacySidecar = Path.Combine(root, ".snapshot-legacy.md.mdeditor-snapshots.json");
        await File.WriteAllTextAsync(legacySidecar, """
            {
              "schemaVersion": 1,
              "sourcePath": "snapshot-legacy.md",
              "snapshots": [
                { "id": "11111111-1111-1111-1111-111111111111", "createdUtc": "2026-01-01T00:00:00Z", "text": "舊版內容", "encoding": 0, "lineEnding": 1 }
              ]
            }
            """);
        var legacySnapshots = new SnapshotService();
        var migrated = legacySnapshots.ListAsync(legacyPath).GetAwaiter().GetResult();
        VerificationAssert.Equal(1, migrated.Count, "舊版 mdeditor sidecar 未被辨識。");
        VerificationAssert.Equal(string.Empty, migrated[0].Note, "沒有 note 欄位的舊版 sidecar 應向前相容成空字串。");
        var newSidecar = SnapshotSidecarStore.GetSidecarPath(legacyPath);
        VerificationAssert.True(File.Exists(newSidecar), "舊版 sidecar 未搬移到新檔名。");
        VerificationAssert.True(!File.Exists(legacySidecar), "舊版 sidecar 應在遷移後被移除。");
        legacySnapshots.DeleteAllAsync(legacyPath).GetAwaiter().GetResult();
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

internal static class ShortcutVerification
{
    public static void Run()
    {
        var shortcuts = new ShortcutService();
        VerificationAssert.Equal(
            0,
            shortcuts.Conflicts.Count,
            $"命令表有快捷鍵衝突：{string.Join("; ", shortcuts.Conflicts)}");

        foreach (var id in Enum.GetValues<AppCommandId>())
        {
            VerificationAssert.Equal(
                1,
                AppCommands.All.Count(command => command.Id == id),
                $"命令 {id} 在命令表中未恰好出現一次。");
        }

        VerificationAssert.Equal("Ctrl+N", shortcuts.GetGestureText(AppCommandId.FileNew), "Ctrl+N 顯示文字不正確。");
        VerificationAssert.Equal("Ctrl+Shift+S", shortcuts.GetGestureText(AppCommandId.FileSaveAs), "Ctrl+Shift+S 顯示文字不正確。");
        VerificationAssert.Equal("Ctrl+Alt+S", shortcuts.GetGestureText(AppCommandId.FileCreateSnapshot), "Ctrl+Alt+S 顯示文字不正確。");
        VerificationAssert.Equal("Ctrl+,", shortcuts.GetGestureText(AppCommandId.SettingsOpen), "OemComma 應顯示為逗號。");
        VerificationAssert.Equal("Ctrl+\\", shortcuts.GetGestureText(AppCommandId.ViewSplitHorizontal), "Oem5 應顯示為反斜線。");
        VerificationAssert.Equal("Ctrl+Alt+\\", shortcuts.GetGestureText(AppCommandId.ViewSplitVertical), "Ctrl+Alt+反斜線顯示文字不正確。");
        VerificationAssert.Equal("Ctrl+Alt+1", shortcuts.GetGestureText(AppCommandId.ViewResetLayout), "D1 應顯示為數字 1。");
        VerificationAssert.True(shortcuts.GetGestureText(AppCommandId.FileExit) is null, "沒有快捷鍵的命令不應回傳顯示文字。");

        // Undo/redo belong to AvalonEdit: the menu still advertises the key, but the window must not
        // intercept it, or the editor loses the only place it actually works.
        VerificationAssert.Equal("Ctrl+Z", shortcuts.GetGestureText(AppCommandId.EditUndo), "復原仍應顯示 Ctrl+Z。");
        VerificationAssert.True(
            AppCommands.All.Single(command => command.Id == AppCommandId.EditUndo).GestureOnly,
            "復原不應被視窗層攔截。");

        var clashing = new ShortcutService(new[]
        {
            new AppCommandDefinition(AppCommandId.FileNew, "file.new", ModifierKeys.Control, Key.N),
            new AppCommandDefinition(AppCommandId.FileOpen, "file.open", ModifierKeys.Control, Key.N)
        });
        VerificationAssert.Equal(1, clashing.Conflicts.Count, "重複的快捷鍵應被記錄為衝突。");

        // A command whose label is missing from a dictionary renders its raw key in the menu.
        var localization = new LocalizationService();
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            localization.Apply(language);
            foreach (var command in AppCommands.All.Where(command => command.LocalizationKey is not null))
            {
                var key = command.LocalizationKey!;
                VerificationAssert.True(
                    !string.Equals(localization.Get(key), key, StringComparison.Ordinal),
                    $"{language} 缺少命令文字：{key}");
            }
        }
    }
}

internal static class RecentFilesVerification
{
    public static void Run(string root)
    {
        var directory = Path.Combine(root, "recent");
        Directory.CreateDirectory(directory);
        var first = Path.Combine(directory, "first.md");
        var second = Path.Combine(directory, "second.md");

        var service = new RecentFilesService();
        service.Add(first);
        service.Add(second);
        VerificationAssert.Equal(2, service.Files.Count, "最近開啟清單應有兩筆。");
        VerificationAssert.Equal(second, service.Files[0].Path, "最近開啟清單應以最新的檔案排在最前。");

        service.Add(first);
        VerificationAssert.Equal(2, service.Files.Count, "重複加入同一個檔案不應新增第二筆。");
        VerificationAssert.Equal(first, service.Files[0].Path, "重複加入應把既有項目移到最前。");

        // Same file, spelled differently: de-duplication has to compare canonical paths, not strings.
        var detour = Path.Combine(directory, "sub", "..", "first.md");
        service.Add(detour);
        VerificationAssert.Equal(2, service.Files.Count, "同一個檔案的不同寫法不應各佔一筆。");
        VerificationAssert.Equal(detour, service.Files[0].Path, "應保留呼叫端傳入的路徑寫法。");

        var overflow = new RecentFilesService();
        for (var i = 0; i < RecentFilesService.MaxEntries + 5; i++)
        {
            overflow.Add(Path.Combine(directory, $"file{i}.md"));
        }
        VerificationAssert.Equal(RecentFilesService.MaxEntries, overflow.Files.Count, "最近開啟清單應截斷到上限。");
        VerificationAssert.Equal(
            Path.Combine(directory, $"file{RecentFilesService.MaxEntries + 4}.md"),
            overflow.Files[0].Path,
            "超出上限時應淘汰最舊的項目。");

        VerificationAssert.True(service.Remove(first), "移除既有項目應回報成功。");
        VerificationAssert.True(!service.Remove(first), "移除不存在的項目應回報失敗。");
        service.Clear();
        VerificationAssert.Equal(0, service.Files.Count, "清除後不應留下項目。");

        // Everything a hand-edited or half-written recent.json can contain must load, not throw.
        var damaged = new RecentFilesService();
        damaged.LoadFrom(new RecentFilesState { Files = null! });
        VerificationAssert.Equal(0, damaged.Files.Count, "files 為 null 時應載入為空清單。");

        var messy = new RecentFilesState();
        messy.Files.Add(null!);
        messy.Files.Add(new RecentFileEntry { Path = "   " });
        messy.Files.Add(new RecentFileEntry { Path = first });
        messy.Files.Add(new RecentFileEntry { Path = first });
        for (var i = 0; i < 20; i++)
        {
            messy.Files.Add(new RecentFileEntry { Path = Path.Combine(directory, $"messy{i}.md") });
        }
        damaged.LoadFrom(messy);
        VerificationAssert.Equal(RecentFilesService.MaxEntries, damaged.Files.Count, "損壞的清單應過濾並截斷到上限。");
        VerificationAssert.Equal(first, damaged.Files[0].Path, "損壞的清單應保留第一筆有效項目。");

        var seeded = new RecentFilesService();
        seeded.Add(first);
        seeded.AddIfMissing(second, DateTime.UtcNow);
        VerificationAssert.Equal(second, seeded.Files[1].Path, "AddIfMissing 應把項目排在最後。");
        seeded.AddIfMissing(first, DateTime.UtcNow);
        VerificationAssert.Equal(2, seeded.Files.Count, "AddIfMissing 不應重複加入既有項目。");

        var store = new JsonStateStore(directory);
        store.SaveRecentFilesAsync(seeded.ToState()).GetAwaiter().GetResult();
        var reloaded = new RecentFilesService();
        reloaded.LoadFrom(store.LoadRecentFilesAsync().GetAwaiter().GetResult());
        VerificationAssert.Equal(2, reloaded.Files.Count, "recent.json 往返後應保留項目數。");
        VerificationAssert.Equal(first, reloaded.Files[0].Path, "recent.json 往返後應保留順序。");
    }
}
