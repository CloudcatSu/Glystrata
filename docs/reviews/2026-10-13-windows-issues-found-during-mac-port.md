# Mac 移植過程中發現的 Windows 版問題

- 建立：2026/10/13，對照 Windows 版 v1.2.3（commit `04ff0b4`）
- 用途：Mac 版遇到就直接修，這份清單留給 Windows 那邊之後跟進。
- 所有項目都來自閱讀原始碼與文件，**沒有實際執行 Windows 版驗證**。「確認」欄標示是誰讀的：
  - 「主」＝主模型自己讀過該段程式碼
  - 「子」＝子代理讀碼後回報，主模型尚未複核，動手前請先看一眼來源行

## 1. 行為問題

| # | 問題 | 來源 | 確認 | Mac 版 |
| --- | --- | --- | --- | --- |
| B1 | 存檔進行中繼續打字，存檔完成時 `MarkSaved` 以當下文字當作已儲存內容，這段期間打的字會被標成「已儲存」 | `DocumentModels.cs:170-176` | 主 | 已修：以實際寫入的文字為準 |
| B2 | 未命名文件「另存新檔」失敗後，路徑已被改成目標路徑且不還原，文件不再是未命名 | `DocumentManager.cs:140-155` | 主 | 已修：寫入成功才改路徑 |
| B3 | 外部變更選「重新載入」時，即使有未儲存修改也不會再確認；規格書要求要警告 | `MainWindow.xaml.cs:2389-2407` | 子 | 待做 |
| B4 | 外部變更選擇保留後，不會再次提示；規格書要求要提示 | 規格書 §5、驗收清單 23-A | 子 | 待做 |
| B5 | 手動儲存與自動儲存都顯示「已自動儲存」 | `MainWindow.xaml.cs`（共用 `SaveDocumentAsync`） | 子 | 待做 |
| B6 | 尋找取代：`ReplaceCurrent` 之後沒有重算比對快取，位移可能過期，與 `RecomputeMatches` 的註解不一致 | `FindReplaceWindow.xaml.cs:103`、`:189-192` | 子 | 待做 |
| B7 | 快照備註含換行時，列表預覽會把換行換成空白，但比對是否變更用的是原始備註，關窗時可能被改寫成單行 | `SnapshotHistoryWindow.xaml.cs`（`FormatNotePreview`） | 子 | 待做 |
| B8 | 群組選取色的 tint 設為 0 時，註解寫「不上色」，程式實際回退為 `TabActiveBrush` | `GroupColors.cs:35-36`、`MainWindow.xaml.cs:927-928` | 子 | 待做 |
| B9 | 有序清單：分色規則接受 `1)`，按 Enter 續行只接受 `1.` | `SyntaxHighlightingService.cs`、`MarkdownTypingAssistant.cs` | 子 | 待做 |
| B10 | YAML 檔中以 `# ` 開頭的註解行被當成 Markdown 標題上色 | `SyntaxHighlightingService.cs:62-66`（規則順序） | 主 | 已修：YAML 內容不套用標題規則 |
| B11 | 自動替換（`->`、`--` 等）在 YAML 檔也會生效 | `MarkdownTypingAssistant.cs`（掛載時不分檔案類型） | 子 | 待做 |
| B12 | 啟動時 `settings.json` 損毀不會留下診斷副本（非啟動路徑會留）；規格書 §5 要求要留 | `JsonStateStore.cs:44-71` | 主 | 待定：啟動路徑是同步讀取，Mac 版目前照 Windows 現況 |
| B13 | 外部檔案被刪除時沒有任何提示 | `MainWindow.xaml.cs` | 子 | 待做 |
| B15 | front matter 的分色：第 1 行的 `---` 不上色，而前 30 行內任何一行 `---`（即使 front matter 早已結束）都會上色 | `SyntaxHighlightingService.cs:166-185` | 主 | 已修：只有開頭到結尾那一段上色，含兩條 `---` |
| B14 | 閱讀器的刪除線只取純文字（`element.Value`），刪除線裡的粗體、斜體、連結會失去格式 | `WpfMarkdownRenderer.cs:357-361` | 主 | 已修：語法樹保留刪除線內的格式 |

## 2. 介面文字

| # | 問題 | 來源 | 確認 | Mac 版 |
| --- | --- | --- | --- | --- |
| T1 | 狀態列的 `Ln`、`Col` 寫死英文，沒有走語言切換 | `MainWindow.xaml.cs:2464` | 主 | 已修：新增 `status.position` 字串（中文「第 {0} 行，第 {1} 欄」） |
| T2 | 偏好設定裡快照檔的說明文字還是舊檔名（`.glystrata-snapshots.json`），實際是 `<檔名>.gss` | `LocalizationService.cs`（`settings.hideSnapshotFiles.help`） | 主 | 已修：Mac 版改寫這段文字 |
| T3 | 群組色票的說明文字寫「修改色票不會更動既有群組」，實際上 1.2.1 起會連動更新 | `LocalizationService.cs`、`GroupManager.cs:48` | 子 | 待做 |

## 3. 規格書有寫、程式沒做

這幾項是規格書與程式不一致，不一定是程式錯，也可能是規格書過期。要不要做由 Cloud 決定。

| # | 項目 | 來源 | 確認 |
| --- | --- | --- | --- |
| S1 | 分頁拖到其他窗格（程式只支援同窗格內排序） | 規格書 §3 | 子 |
| S2 | Markdown 內 fenced yaml 區塊的分色 | 規格書 §3 | 子 |
| S3 | 全新安裝依系統語言決定介面語言（程式一律預設繁體中文） | 規格書 §1 | 子 |
| S4 | 開啟 Big5 等非 Unicode 檔案（程式只接受 UTF-8、UTF-8 BOM、UTF-16 LE） | 規格書 §2 | 子 |
| S5 | 程式碼圍欄內部的文字不分色 | `SyntaxHighlightingService.cs` | 子 |

## 4. 可清理的程式碼

| # | 項目 | 來源 | 確認 |
| --- | --- | --- | --- |
| C1 | `DocumentManager.Open` 的 `OpenMode` 參數沒有作用，兩種模式行為相同 | `DocumentManager.cs:22-40` | 主 |
| C2 | `CanonicalizePath` 在 `DocumentSession` 與 `DocumentManager` 各有一份相同實作 | `DocumentModels.cs:241-251`、`DocumentManager.cs:199-209` | 主 |
| C3 | `SetFormattingToolbarVisible` 沒有呼叫端；`TabContextRequested` 沒有訂閱者 | `EditorPaneControl.cs` | 子 |
| C4 | `GroupIndicatorBrush` 在 XAML 與 C# 都沒有被引用 | `Light.xaml`、`Dark.xaml` | 子 |
| C5 | 編輯器文字色有兩個來源且數值不同：調色盤 `plain`（`#1F2328`）與 `EditorForegroundBrush`（`#24292F`） | `AppSettings.cs:38`、`Light.xaml:11`、`EditorPaneControl.cs:143`、`SyntaxHighlightingService.cs:150` | 子 |
| C6 | 閱讀器的 Surface 與 Muted 色寫死在渲染器裡，不讀調色盤 | `WpfMarkdownRenderer.cs:22-29` | 子 |
