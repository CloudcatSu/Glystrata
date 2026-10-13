# Glystrata Mac 版：待決事項與假設

- 建立：2026/10/13
- 第 1、2 節是主模型整理的；第 3 節是五個子代理回報的「假設」與「需要人決定的事項」，原文照列、未經刪改。

## 1. 會擋住後續階段的決定

| # | 事項 | 為什麼現在要定 |
| --- | --- | --- |
| 1 | ~~最低支援的 macOS 版本~~ | 已定：macOS 14（2026/10/13） |
| 2 | ~~App 的建置方式~~ | 已定：XcodeGen（2026/10/13） |
| 3 | ~~Markdown 解析器與冷門語法~~ | 已定：顯示要跟 Windows 版一樣（2026/10/13） |
| 4 | ~~Windows 版現有的小問題：照現況做還是直接修~~ | 已定：Mac 版遇到就修，並記錄下來帶回 Windows 版（2026/10/13） |
| 5 | ~~快速鍵對照~~ | 已定：照 Mac 慣例；三個衝突改為取代 ⌥⌘F、重做 ⇧⌘Z、新增群組 ⇧⌘N，使用者已確認（2026/10/13） |
| 6 | ~~閱讀器的 emoji~~ | 已定：用蘋果系統的彩色 emoji（2026/10/13） |
| 7 | App Sandbox 開或不開（目前假設不開） | 開了之後，在文件旁邊寫 `.gss` 需要額外授權流程；上架 Mac App Store 必須開 |

### 1-1 快速鍵對照草案

把 Ctrl 換成 ⌘、Alt 換成 ⌥ 之後，有三個會跟 macOS 既有慣例相撞：

| 指令 | Windows | 直接換成 ⌘ | 相撞的對象 |
| --- | --- | --- | --- |
| 取代 | Ctrl+H | ⌘H | 系統的「隱藏視窗」 |
| 重做 | Ctrl+Y | ⌘Y | Mac 慣例是 ⇧⌘Z |
| 新增群組 | Ctrl+Shift+G | ⇧⌘G | 文字編輯區慣用的「尋找上一個」 |

其餘指令直接對應沒有衝突：新增 ⌘N、開啟 ⌘O、儲存 ⌘S、另存 ⇧⌘S、快照歷史 ⇧⌘H、建立快照 ⌥⌘S、偏好設定 ⌘,、尋找 ⌘F、左右分割 ⌘\、上下分割 ⌥⌘\、關閉窗格 ⇧⌘W、還原單一窗格 ⌥⌘1、在新窗格開啟 ⌥⌘N、切換閱讀器 ⇧⌘R、關閉 TAB ⌘W、結束 ⌘Q（系統提供）。

### 1-2 做主視窗時新出現的待決事項

| # | 事項 | 目前做法 |
| --- | --- | --- |
| 8 | ~~Bundle identifier（App 的系統識別名稱，公開發布後就不該再改）~~ | 已定：`com.firewatersmithy.Glystrata`（2026/10/13） |
| 9 | ~~系統選單列的頂層選單名稱（檔案、編輯、顯示方式、視窗、輔助說明）由 macOS 依系統語言顯示，不跟著 App 內的語言切換~~ | 已定：選單內的項目文字跟著 App 語言，頂層名稱跟著系統（2026/10/13） |
| 10 | ~~編輯器縮放的操作方式：Windows 是 Ctrl＋滾輪~~ | 已定：全部都做（2026/10/13）。⌘＋滾輪、觸控板兩指縮放、⌘＋／⌘－／⌘0，加上原本的狀態列滑桿，共用同一個縮放值（50%–200%，每級 10%）。滾輪方向跟著手的動作、不跟「自然捲動」設定：往前推＝放大，與 Windows 的 Ctrl＋滾輪同向 |

## 2. Core 移植時與 Windows 版不同的地方

| 項目 | Windows 版 | Mac 版 | 原因 |
| --- | --- | --- | --- |
| 文字緩衝區 | `DocumentSession` 持有 AvalonEdit 的 `TextDocument` | Core 只存字串，編輯器的文字儲存與復原紀錄放在介面層，透過 `updateText` 回報 | Core 不依賴介面元件 |
| 儲存中繼續打字 | 存檔完成時以「當下的文字」當作已儲存內容 | 以「實際寫進檔案的文字」為準 | 原做法會讓存檔期間打的字被誤判為已儲存 |
| 另存新檔失敗 | 未命名文件的路徑會被改掉且不還原 | 寫入成功後才改路徑 | 原做法會讓失敗後的文件不再是「未命名」 |
| 重疊儲存 | 每份文件一個 semaphore | 每份文件的儲存依序排隊 | 相同效果，Swift 的慣用寫法 |
| 快照檔並行保護 | 每個檔案一把鎖 | 整個 store 是一個 actor | 較簡單；不同文件的快照寫入會短暫互等 |
| 同一檔案的判斷 | 路徑轉大寫 | 路徑轉大寫，另外做 Unicode 正規化 | macOS 的檔名可能以分解形式出現 |
| 資料夾列舉 | 略過有 Hidden 屬性的項目 | 略過隱藏旗標與以 `.` 開頭的項目 | Mac 的隱藏慣例 |
| JSON 細節 | GUID 小寫、日期最多 7 位小數、`"key": value` | UUID 大寫、日期 3 位小數、`"key" : value`、鍵依字母排序 | 兩邊都讀得懂對方的寫法；尚未用真實的 Windows 檔案驗證 |
| 設定資料位置 | `%LocalAppData%\Glystrata` | `~/Library/Application Support/Glystrata` | 平台慣例 |
| Markdown 解析 | Markdig 產生 HTML，閱讀器再把 HTML 讀回來畫 | swift-markdown（cmark-gfm）解析成自家的小型語法樹，閱讀器直接畫 | Swift 沒有 Markdig；不經過 HTML 這一手 |
| 與 Markdig 對齊的調整 | Markdig 內建設定 | 解析前：去掉 YAML front matter、把 HTML 標籤的 `<` 跳脫。解析後：Setext 標題還原成段落、單波浪號不當刪除線、`^`／`++`／`==` 標記移除、裸網址轉連結 | cmark-gfm 沒有對應的開關 |
| 上述調整的精確度 | — | `^`／`++`／`==` 的配對與裸網址的邊界是簡化規則，不是照 Markdig 逐條移植；常見寫法一致，少見的巢狀寫法可能不同 | 以 Windows 截圖 29 為準再修 |

另外發現：Windows 閱讀器是把 Markdig 輸出的 HTML 當 XML 讀回來再畫，不認得的標籤只顯示內文（`WpfMarkdownRenderer.cs:420-422`）。所以 `H~2~O`、`x^2^`、`++x++`、`==x==` 在 Windows 版的實際效果是「符號消失、內容以一般文字顯示」，沒有上下標或標記效果。

## 3. 子代理回報原文

### 3-1 色票與樣式（`2026-10-13-mac-visual-tokens.md`）

假設：

- 第 1 節的「用途」由 grep 引用位置推斷，可能漏掉以字串組出 key 名稱的寫法。
- SystemColors.*BrushKey 五個覆寫，推斷影響沒有自訂樣式的 WPF 內建控制項，原檔未說明。
- GroupIndicatorBrush 在 XAML 與 C# 都沒有引用，推斷為未使用的殘留。
- StatusBarSliderStyle 在 XAML 內沒有引用，但 C# 與 PreviewWindow.xaml 有引用。
- WPF 語意（未實測）：同一 Style 內後宣告的 Trigger 覆寫先宣告的。
- WPF 語意（未實測）：MenuItem Style 沒有 IsMouseOver 觸發器，推斷滑鼠移入由 IsHighlighted 觸發。
- WPF 語意（未實測）：FindResource 取得的 Brush 不會自動更新，DynamicResource 會。推斷 C# 端靠 Theme_ThemeChanged 重新取值。
- App.xaml:6 靜態合併的 Light.xaml 不會被 ThemeService 移除，推斷功能上不受影響，但未實測。
- 原檔未寫的寬高、字級、字重、動畫時間一律標「原檔未指定」。
- 「沿用 WPF 預設」指 XAML 沒寫 FontSize 時繼承上層值，WPF 預設數值本身不在原檔中。

不一致之處：

- 編輯器文字色有兩個來源：AppSettings.cs:38 的亮色 plain 為 #1F2328，Light.xaml:11 的 EditorForegroundBrush 為 #24292F。暗色同理（AppSettings.cs:57 為 #E6EDF3，Dark.xaml:11 為 #E6EAF0）。原檔未說明實際畫面用哪一個。
- 閱讀器 Surface 色（WpfMarkdownRenderer.cs:29）與 Muted 色（:23、:28）寫死、不讀取調色盤。
- 字型名稱不同：Segoe UI Variable（XAML）與 Segoe UI（閱讀器）並存。

需要人決定的事項：

1. Mac 版要用哪一套字型來對應 Segoe UI Variable、Segoe UI、Cascadia Code、Cascadia Mono、Segoe Fluent Icons。原檔沒有 Mac 字型的對應，這些 Windows 字型在 macOS 上不一定存在。
2. 編輯器文字色以 plain（AppSettings.cs:38、:57）為準，還是以 EditorForegroundBrush（Light.xaml:11、Dark.xaml:11）為準。
3. 閱讀器 Surface 與 Muted 色，要保留 WpfMarkdownRenderer.cs 內的寫死值，還是改由調色盤或色票決定。
4. GroupIndicatorBrush 未使用，要保留在 Mac 版色票表中，還是移除。
5. SystemColors 覆寫，Mac 版是否需要對應。

### 3-2 主視窗（`2026-10-13-mac-main-window-inventory.md`）

假設：

- 主視窗未設定 ResizeMode，推斷為 WPF 預設 CanResize。
- 分頁列高度、捲動箭頭、按鈕 hover 樣式等由 App 資源字典決定（未閱讀）。
- 最大化鈕的 WM_NCHITTEST 處理與 Windows 11 Snap Layouts 有關（註解未明寫）。
- 分割時搬移的是 `_documents.Views` 列舉順序的最後一個分頁，不是分頁列上的視覺順序最後一個。
- 跨窗格拖曳分頁不被接受。
- 關閉窗格時，分頁併入列舉順序中第一個其他窗格，不一定是相鄰窗格。
- 檔案拖到群組列時，實際加入的是「目前群組」，不是游標所在群組。
- 啟動時不還原選取群組，預設為「全部 TAB」。
- 未命名文件不寫入工作階段，重新啟動後內容不會還原（**需要確認**：DocumentManager 可能另有機制，未閱讀）。
- 縮放比例每次啟動回到 100%。
- 自動儲存與快照只針對有檔案路徑的文件。
- SetFormattingToolbarVisible 沒有呼叫端（死碼）；TabContextRequested 沒有訂閱者。
- 語法規則之間的覆蓋順序取決於 AvalonEdit 的 ChangeLinePart 行為（推斷後者覆蓋前者）。
- 手動儲存與自動儲存都顯示「已自動儲存」，因為共用 SaveDocumentAsync。
- 對話框按鈕回傳值 0/1/2 的對應依呼叫端推斷（MessageDialogs 未閱讀）。
- 外部檔案被刪除時不會提示。
- 狀態列覆寫文字沒有計時器，會持續到下一次 UpdateStatus。

程式內的不一致與觀察：

1. 群組選取色 tint 為 0 時，AppSettings 註解寫「不上色」，但程式回退為 TabActiveBrush（GroupColors.cs:35-36、MainWindow.xaml.cs:927-928）。
2. 一般文字後備色：編輯器前景 `#24292F`（EditorPaneControl.cs:143）與分色器未知分類 `#1F2328`（SyntaxHighlightingService.cs:150）不一致。
3. 有序清單：分色規則接受 `1)`，清單續行只接受 `1.`。
4. 程式碼圍欄內部的文字不會被上色（分色器沒有跨行狀態，前置區塊除外）。
5. 狀態列的 `Ln`、`Col` 寫死英文，未經 localization（MainWindow.xaml.cs:2464）。
6. 外部變更選「重新載入」時沒有二次確認，即使本機有未儲存修改（MainWindow.xaml.cs:2389-2407）。
7. YAML 檔中以 `# ` 開頭的行會被當成 Markdown 標題上色（規則順序使然）。
8. 自動替換（`->`、`--` 等）對 YAML 檔也生效，因為掛載時不區分檔案類型。

需要人決定的事項：

- 快速鍵的修飾鍵：Windows 版全部以 Ctrl 為主，Mac 版是否改用 Command、保留 Ctrl，或兩者並存。
- 標題列樣式：Mac 版要沿用自繪標題列，或使用原生標題列。
- 未命名文件是否要在重新啟動後還原（Windows 版目前推斷為不還原）。
- 群組選取色 tint 為 0 時的行為：依註解「不上色」，或維持程式目前的回退行為。
- 狀態列的 `Ln`、`Col` 是否要改為 localization。
- 外部變更「重新載入」是否要加入未儲存修改的二次確認。
- 有序清單標記 `1)` 是否要讓續行功能支援（與分色規則一致）。
- 編輯器前景後備色 `#24292F` 與分色器後備色 `#1F2328` 是否統一。
- 格式工具列是否保留「預設隱藏、只能由設定開啟」的行為，或要支援執行中切換（目前的切換方法未被呼叫）。
- Windows 專屬的 `.gss` 檔案關聯、隱藏屬性、`%LOCALAPPDATA%` 路徑，在 Mac 上的對應做法。

未閱讀、因而標為「原始碼未指定」的部分：格式工具列各命令的實際輸出、最近檔案上限、字數計算規則、群組名稱規則、同檔案重複開啟的處理、所有中文文字（Localization 資源）、主題與調色盤預設色值、各項資料的實際儲存格式、快照刪除邏輯、分頁列與狀態列高度。

### 3-3 周邊視窗（`2026-10-13-mac-secondary-windows-inventory.md`）

假設：

1. YAML front matter 不顯示：依據是測試斷言（tests/Glystrata.Verification/Program.cs:352），未實際執行渲染。
2. DisableHtml 後的 HTML 會以純文字顯示：依據是測試斷言 HTML 中不含 <script（Program.cs:351），推論為被跳脫後以文字呈現，未實際執行。
3. UseEmphasisExtras() 的實際啟用範圍：Markdig 1.3.2 的預設行為未在本機驗證。推測僅啟用刪除線（~~），上標、下標、==mark==、++ins++ 是否啟用不確定。若未啟用，這些語法會被當成一般文字。
4. Setext 標題關閉後，--- 緊接文字的行為：推測變成分隔線（依 CommonMark 規則，未實際驗證）。
5. 引言中的多段落會合併成一段：依 WpfMarkdownRenderer.cs:121-124 的程式碼推得，未實際渲染確認。
6. DiffWindow 的 FlowDocument 會自動換行：依 WPF 預設推定，未實際驗證。
7. 動態資源在主題資源切換時會即時更新：依 WPF 語意推定。全域主題切換的實作（ThemeService、App.xaml.cs）未在本次讀取範圍內。
8. 快照列表的備註換行處理可能造成意外改寫：FormatNotePreview 會把換行換成空白，但比對是否變更使用的是原始備註，含換行的備註在關窗時可能被視為變更並改寫為單行。依程式碼推得，未實際執行。
9. 自動快照的時機：依 MainWindow.xaml.cs:2151-2180 推得，每 30 秒檢查一次，距離上次變更達設定分鐘數才建立。
10. MessageDialogs 只有一顆按鈕時，Esc 等同於按確定：依 MessageDialogs.cs:69 的 IsCancel 條件推得。
11. ColorPickerDialog 的十六進位輸入框在每次更新後會被改寫為大寫 #RRGGBB：依 ColorPickerDialog.cs:115，對游標位置的影響未驗證。
12. TextDiffService 的行切分先經過換行正規化：依 FileCodec.NormalizeLineEndings 的名稱推定，實作未讀取。
13. DiffWindow 外部比較模式的 -／+ 語意：依 MainWindow.xaml.cs:2418 與 DiffWindow.xaml.cs:23 的參數順序推得（- 為磁碟檔，+ 為編輯器內容）。
14. WPF BrushConverter 接受的顏色格式：推定包含 #RGB、命名色；#AARRGGBB 的 Alpha 被丟棄（由 SettingsWindow.xaml.cs:709 只輸出 RGB 推得）。
15. 快照字數以 UTF-16 長度計算：snapshot.Text.Length，與字元數不同（例如 emoji 計為 2）。
16. 閱讀器重新渲染後的捲動位置：原始碼未指定，推定回到頂端，未驗證。
17. 閱讀器首次開啟時的視窗背景來源：建構時未設定視窗背景，ApplyTheme() 只在設定套用時呼叫。推定由全域樣式提供，未確認。
18. Hyperlink 是否有底線：渲染器未設定 TextDecorations，取決於 WPF 預設樣式，未確認。
19. Emoji 圖檔的 zip 內檔名規則：依 EmojiRenderer.cs:115 的 {key}.png 推定，未逐一列舉 zip 內容確認涵蓋範圍。
20. PreviewWindowManager 的一視圖一閱讀器：依 PreviewWindowManager.cs:23-46 的字典鍵（view.ViewId）推得。

需要人決定的事項：

1. macOS 快速鍵：原始碼以 Ctrl 定義（AppCommand.cs:57-73），Mac 版是否改為 Cmd，或沿用 Ctrl。
2. 快照檔的「隱藏」在 macOS 的做法：Windows 使用隱藏屬性（SnapshotSidecarStore.cs:200-222），macOS 沒有完全對等的做法，需決定採用哪種替代，或不做。
3. .gss 快照檔與設定檔的相容性：Mac 版是否需要讀寫 Windows 產生的檔案，包括舊檔名的遷移（.glystrata-snapshots.json、.{檔名}.mdeditor-snapshots.json 等）。
4. 字型替代：Segoe UI、Segoe UI Emoji、Cascadia Mono、Consolas 在 macOS 的對應字型。
5. 尋找取代的過期位移：ReplaceCurrent 沒有重算比對快取，與 RecomputeMatches 的註解不一致（FindReplaceWindow.xaml.cs:103、:189-192）。沿用原行為、修正，或改寫，需決定。
6. 設定視窗的 Enter 行為（Windows 版無預設按鈕，Enter 不作用）。
7. 閱讀器的 Esc 是否關閉（Windows 版不關閉）。
8. 語言與主題的即時套用範圍（設定、比較、尋找、說明目前不即時，是否要改為即時）。
9. 說明文字中提到 Heptabase 的句子是否保留。
10. 群組顏色的數量上限（Windows 版無上限，且允許重複）。
11. 快照間隔與上限超出範圍時，要靜默夾住（Windows 版行為）或提示。
12. 快照字數以 UTF-16 長度計算（Windows 版行為），Mac 版是否改用字元數（會與 Windows 版顯示不同）。
13. 快照備註的換行處理（見假設第 8 項）：沿用、修正，需決定。
14. 比較視窗的單欄 unified 格式與長文件的記憶體成本（LCS 表為 (n+1)×(m+1) 個 int）：是否接受現況。
15. 外部檔案比較是否維持「每次開新視窗」的行為（MainWindow.xaml.cs:2418）。
16. Emoji 圖檔（Twemoji 15.1.0，見 THIRD-PARTY-NOTICES.txt:103）是否沿用同一份資源與授權聲明。
17. 閱讀器中 Setext 標題與 --- 的行為是否維持 Windows 版的設定（MarkdownPipelineFactory.cs:23-26）。

另外提醒：原始碼中的 CustomTitleBar、LocalizationService 字串內容、ThemeService、SystemThemeWatcher、AtomicFileWriter、MainWindow.RestoreSnapshot、Editing/ 目錄都不在指定清單內，文件只記錄它們被呼叫的方式，細節未整理。

主模型補充：假設第 3 項與第 2 節的發現不同。Markdig 的 `UseEmphasisExtras()` 預設會啟用刪除線、上標、下標、插入、標記五種（這點出自主模型對 Markdig 的既有認識，未在本機執行驗證），而閱讀器只對 `del`／`s` 有專門處理，其餘落到預設分支。最終以 Windows 截圖 29 為準。

### 3-4 功能驗收清單（`2026-10-13-mac-feature-checklist.md`）

文件之間的主要矛盾：

- 快照檔命名：SPEC 寫 `.<檔名>.glystrata-snapshots.json`；程式與 CL 1.1.5、README 是 `<檔名>.gss`。偏好設定的說明文字還是舊名稱，屬過期文字。
- 群組色票：色票說明文字寫「修改色票不會更動既有群組」，但 CL 1.2.1 與程式（RemapColors）實際會連動更新。
- settings.json 損毀：啟動路徑不保留損壞副本，與 SPEC §5 不符。
- 外部變更：SPEC 要求「重新載入」要警告，程式沒有；SPEC 要求保留後再次提示，程式不提示。
- 分頁拖到其他窗格：SPEC 有要求，程式只支援同窗格排序。
- fenced yaml 分色：SPEC 有要求，程式未實作。
- 全新安裝語言：SPEC 暗示依系統語言，程式一律預設繁體中文。
- Big5 等非 Unicode 檔案：SPEC 寫「可解碼純文字檔」，程式只接受 UTF-8、UTF-8 BOM、UTF-16 LE。

假設：

- 假設 Mac 版目標為 macOS（Apple Silicon）原生 Swift 應用，不使用 Electron 或瀏覽器元件，與 Windows 版的設計原則一致（SPEC §7）。
- 假設 Mac 版的 Markdown 解析器可以提供與 Markdig 等價的 CommonMark、GFM 表格、任務清單、刪除線、自動連結與 front matter 解析；等價程度需以 21 章的 Markdown 驗證補做。
- 假設 `System.Text.Json` 的日期格式以 22-B 的描述為準；小數秒位數的實際輸出需以 Windows 版產生的 `.gss` 檔實際確認。
- 假設「Hidden 屬性」與「隱藏快照檔案」的語意可以映射到 Mac 的檔案屬性，但映射方式未定（見 23-C 第 3 項）。
- 假設 `.gss` 的快照檔需要能與 Windows 版互通（SPEC 與任務描述要求），因此 22-H 的欄位型別與數值為硬性規格。
- 假設 1.2.3 之後沒有新的版本或未公開的程式變更；本清單只對應 `repo` 目前的內容。
- 未讀取 `docs/specs/2026-10-13-mac-visual-tokens.md`（不在本次指定的閱讀範圍內）。
- 未讀取 `src/Glystrata/MainWindow.xaml`、各 XAML 樣板與 `Resources/Themes/*.xaml` 的逐項樣式，相關外觀只依 CHANGELOG 與 LocalizationService 整理。
- 未實際執行 Windows 版程式，所有行為均來自文件與原始碼閱讀。依程式碼推斷的條目已在文字中標明「依程式碼推斷」或「推斷」。

需要人決定的事項：

1. 快捷鍵修飾鍵：全部改為 ⌘（符合 Mac 慣例），還是保留 Ctrl（與 Windows 版完全一致）？混用的話要列出哪些例外。
2. `.gss` 的雙重用途：快照與設定匯出檔目前共用副檔名。Mac 版要不要改用不同副檔名（例如設定檔另取名），以免使用者把快照檔匯入設定，或在 Finder 中分不清？快照檔的 Mac 檔案類型註冊方式（UTI）也要一併決定。
3. 快照檔的隱藏方式：Windows 用 Hidden 屬性。Mac 的對應方式（例如 `chflags hidden`，或以點開頭命名）各有影響；點開頭會改變檔名，與 `<檔名>.gss` 的規則衝突，需決定。
4. 資料存放路徑：Windows 為 `%LocalAppData%\Glystrata\`。Mac 版要放在哪裡？`errors.log` 的位置也要一併定。
5. 舊快照檔名遷移：Mac 版是否需要讀取 22-I 的三種舊檔名？若不需要，Program.cs 的 `SnapshotVerification` 第 (g) 項可以不移植。
6. 跨平台匯入群組與工作階段：群組與工作階段的路徑是 Windows 路徑，Mac 版讀到時如何處理（忽略、保留原字串、提示遺失）？
7. 非 Unicode 編碼：是否支援 Big5、Shift_JIS 等 ANSI 檔案？繁中使用者常見，若支援，需要定義自動偵測的順序與存檔時的編碼保留方式。
8. 語言預設：維持一律繁體中文（與程式一致），還是依系統語言決定（與 SPEC §1 一致）？
9. 外部變更的重新載入是否加警告（A6）；保留後是否再次提示（A7）。
10. 分頁拖到其他窗格是否要做（A8）。
11. Markdown 內 fenced yaml 分色是否要做（A9）。
12. 回復（取代目前內容）後是否自動存檔（B2）。
13. 未命名文件是否保留到工作階段（第 14 章）。
14. 手動儲存的狀態文字是否改為「已儲存」（A19、第 1 章）。
15. 搜尋比較規則（B9）與全部取代的 undo 行為（B8）。
16. 快照字數的計算單位（B10）。
17. 快照是否只記錄已存檔內容（B4）。
18. 重新命名後是否更新最近清單（B7）；另存新檔是否加入目前群組（B6）。
19. 視窗大小與位置是否保存（B13）：屬於新功能，原版沒有。
20. 工作階段是否保存群組篩選與分頁排序（A11、B3）。
21. 偏好設定是否需要即時預覽語言與主題（A4）。
22. 設定匯出檔的 `kind` 位置（A17）：只影響外觀，匯入不受影響，可以忽略。
23. 群組色票說明文字（A2）：說明要改寫為與程式一致的描述。
24. 快照中的 `notice` 警語：Mac 寫入時是否保留完全相同的文字（給 AI 助理與自動化工具看的警語）。
25. 「快速鍵是否可自訂」（第 18 章）：1.2.0 決定不做，Mac 版是否維持。
26. IME 相關：Mac 的組字行為由系統處理，本清單的 IME 條目只做驗證，不做實作；是否需要額外的組字顯示客製，需決定。

### 3-5 技術研究（`2026-10-13-mac-tech-research.md`）

假設（均非直接從來源讀到）：

- Apple Silicon 上的執行檔至少需要簽章（ad-hoc 也算）才能執行。
- `notarytool` 指令用法與「公證服務不接受 ad-hoc 或未簽章軟體」（來源未讀到，來自既有知識）。
- Sandbox 關閉時不受 sibling 檔授權限制（推論，未查到直接比較來源）。
- 在 macOS 15 上執行新 SDK 建置的 App，不會顯示 Liquid Glass 外觀（推論）。
- dotfile（以 `.` 開頭）作為跨平台隱藏的替代做法，在 Windows 上仍會顯示（推論）。
- 社群提供的 `actool` 參數在 Xcode 27 中仍可用（未驗證）。

需要人決定的事項：

- 要不要走 App Sandbox：不開 sandbox 比較簡單，但上架 Mac App Store 必須開；直接發布 Developer ID 則不一定需要。（本次只整理事實，不替你選擇。）
- 「`===`／`---` 底線在關閉 Setext 後應視為段落文字還是分隔線」。
- 是否要付 USD 99 年費取得 Developer ID 並公證（目前尚無 Apple Developer 帳號）。
- 圖示若要支援舊 macOS，是否接受 Asset Catalog 同時包含 `.icon` 與舊圖的做法，或只支援新系統。
- 下標、上標、插入、標記的語法細節（例如 `~` 與刪除線衝突時以哪個為準）。

來源矛盾與查不到的重點：

- STTextView 授權：GitHub 顯示 NOASSERTION，README 寫 GPL v3.0，需讀 LICENSE 檔確認。
- Xcode 27 主機最低 macOS：兩篇二手文章分別寫 26.6 與 26.4，需以 Apple 官方為準。
- `.icns` 回退：Chromium 與 mpv 的建置說明結論不同。
- StatCounter 分地區數據有明顯錯誤跡象，不建議引用。
- Apple 官方頁面（CFBundleDocumentTypes、Icon Composer、Adopting Liquid Glass、Xcode 27 Release Notes、App Sandbox、Notarizing）本次只抓到標題，內文未取得，相關結論已改標為二手來源。
