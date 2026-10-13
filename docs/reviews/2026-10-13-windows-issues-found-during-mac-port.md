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
| B5 | 手動儲存與自動儲存都顯示「已自動儲存」 | `MainWindow.xaml.cs:1551-1565`（`SaveActiveAsync` 共用 `SaveDocumentAsync`，後者固定顯示 `status.autoSaved`） | 主 | 已修：只有自動儲存顯示「已自動儲存」，手動儲存顯示「已儲存」 |
| B6 | 尋找取代：`ReplaceCurrent` 之後沒有重算比對快取，位移可能過期，與 `RecomputeMatches` 的註解不一致 | `FindReplaceWindow.xaml.cs:103`、`:189-192` | 子 | 原描述不成立，實際問題見 B22 |
| B7 | 快照備註含換行時，列表預覽會把換行換成空白，但比對是否變更用的是原始備註，關窗時可能被改寫成單行 | `SnapshotHistoryWindow.xaml.cs`（`FormatNotePreview`） | 子（已由第二個子代理讀碼確認為真） | 已修：欄位內容沒被使用者改過就不寫回，保留原本的換行 |
| B8 | 群組選取色的 tint 設為 0 時，註解寫「不上色」，程式實際回退為 `TabActiveBrush` | `GroupColors.cs:35-36`、`MainWindow.xaml.cs:927-928` | 子 | 待做 |
| B9 | 有序清單：分色規則接受 `1)`，按 Enter 續行只接受 `1.` | `SyntaxHighlightingService.cs`、`MarkdownTypingAssistant.cs` | 子 | 待做 |
| B10 | YAML 檔中以 `# ` 開頭的註解行被當成 Markdown 標題上色 | `SyntaxHighlightingService.cs:62-66`（規則順序） | 主 | 已修：YAML 內容不套用標題規則 |
| B11 | 自動替換（`->`、`--` 等）在 YAML 檔也會生效 | `MarkdownTypingAssistant.cs`（掛載時不分檔案類型） | 子 | 待做 |
| B12 | 啟動時 `settings.json` 損毀不會留下診斷副本（非啟動路徑會留）；規格書 §5 要求要留 | `JsonStateStore.cs:44-71` | 主 | 待定：啟動路徑是同步讀取，Mac 版目前照 Windows 現況 |
| B13 | 外部檔案被刪除時沒有任何提示 | `MainWindow.xaml.cs` | 子 | 待做 |
| B15 | front matter 的分色：第 1 行的 `---` 不上色，而前 30 行內任何一行 `---`（即使 front matter 早已結束）都會上色 | `SyntaxHighlightingService.cs:166-185` | 主 | 已修：只有開頭到結尾那一段上色，含兩條 `---` |
| B14 | 閱讀器的刪除線只取純文字（`element.Value`），刪除線裡的粗體、斜體、連結會失去格式 | `WpfMarkdownRenderer.cs:357-361` | 主 | 已修：語法樹保留刪除線內的格式 |
| B16 | 自動儲存失敗時（例如檔案變成唯讀），每次停止輸入 1 秒就再跳一次錯誤對話框 | `MainWindow.xaml.cs:2255-2278`、`:1614-1633` | 主（讀碼推論，未實測） | 已修：同一份文件只提示一次，直到下次儲存成功為止；TAB 仍保留未儲存標記 |
| B17 | 重新命名檔案後，「最近開啟過的文件」仍留著舊路徑（變成灰色的失效項目），新路徑不在清單裡 | `MainWindow.xaml.cs:1830-1888`（沒有動到 `_recent`） | 主 | 已修：改名時把清單裡的舊路徑換成新路徑 |
| B18 | 同一份文件開了兩個 TAB，關掉其中一個也會詢問未儲存變更（另一個 TAB 還開著，內容並不會遺失） | `MainWindow.xaml.cs:1654-1679` | 主 | 已修：只有關掉該文件的最後一個 TAB 才詢問 |
| B19 | 閱讀器：引用區塊裡有多段文字或清單時，全部被接成同一段，中間沒有換行 | `WpfMarkdownRenderer.cs:118-131`（把每個子元素的內容都加進同一個 Paragraph） | 主 | 已修：各段各自成段，共用左側直條 |
| B20 | 閱讀器：清單項目裡的第二段文字、程式碼區塊會被接在項目文字後面變成同一行 | `WpfMarkdownRenderer.cs:214-244`（除了巢狀清單，其餘節點都加進同一個 Paragraph） | 主 | 已修：對齊在項目文字底下各自成段 |
| B21 | 閱讀器：有序清單一律從 1 開始編號，原始檔寫 `3.` 開頭也顯示 1 | `WpfMarkdownRenderer.cs:196-207`（沒有設定 `List.StartIndex`） | 主（讀碼推論，未實測） | 已修：照原始檔的起始數字 |
| B22 | 尋找取代：「取代」直接使用上一次尋找留下的位置，期間文件若被編輯會取代到錯的地方；比對長度用關鍵字長度而非實際比對到的長度 | `FindReplaceWindow.xaml.cs:161`、`:180-192` | 子（B6 的複核結果：原描述「取代後沒有重算」不成立，問題在取代之前） | 已修：取代前用當下文字重新確認位置 |
| B23 | 設定視窗：數值驗證只檢查 `<= 0`，`NaN` 會通過；驗證途中就把值寫進暫存設定，驗證失敗時暫存設定已被改掉一部分 | `SettingsWindow.xaml.cs:538-618` | 子 | 已修：拒絕非有限值；驗證失敗時不改任何東西 |

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
| S6 | 閱讀器的表格欄位對齊（`:---:`、`---:`）沒有作用，一律靠左 | `WpfMarkdownRenderer.cs:262-304`（沒有讀取儲存格的對齊） | 主 |
| S7 | 設定視窗顏色分頁的 `HEX` 欄名寫死在程式裡，沒有走語言切換 | `SettingsWindow.xaml.cs:204-210` | 子 |

## 4. 可清理的程式碼

| # | 項目 | 來源 | 確認 |
| --- | --- | --- | --- |
| C1 | `DocumentManager.Open` 的 `OpenMode` 參數沒有作用，兩種模式行為相同 | `DocumentManager.cs:22-40` | 主 |
| C2 | `CanonicalizePath` 在 `DocumentSession` 與 `DocumentManager` 各有一份相同實作 | `DocumentModels.cs:241-251`、`DocumentManager.cs:199-209` | 主 |
| C3 | `SetFormattingToolbarVisible` 沒有呼叫端；`TabContextRequested` 沒有訂閱者 | `EditorPaneControl.cs` | 子 |
| C4 | `GroupIndicatorBrush` 在 XAML 與 C# 都沒有被引用 | `Light.xaml`、`Dark.xaml` | 子 |
| C5 | 編輯器文字色有兩個來源且數值不同：調色盤 `plain`（`#1F2328`）與 `EditorForegroundBrush`（`#24292F`） | `AppSettings.cs:38`、`Light.xaml:11`、`EditorPaneControl.cs:143`、`SyntaxHighlightingService.cs:150` | 子 |
| C6 | 閱讀器的 Surface 與 Muted 色寫死在渲染器裡，不讀調色盤 | `WpfMarkdownRenderer.cs:22-29` | 子 |
