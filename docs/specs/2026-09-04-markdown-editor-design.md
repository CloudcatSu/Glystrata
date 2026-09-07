# MDeditor MVP 設計規格

**日期：** 2026-09-04  
**版本：** 0.0.1  
**目標：** 建立一款可在 Windows 11 獨立執行、啟動快速、介面極簡的 Markdown／YAML 文字編輯器，支援虛擬群組、瀏覽器式 TAB、自由多窗格編輯、獨立 Markdown 閱讀器、語法配色、自動存檔與隱藏檔快照。

## 1. 已確認的產品決策

- 交付為真正的 Windows 11 桌面應用程式，不是瀏覽器原型。
- 技術基礎採 `.NET 10 + WPF`。
- 交付第一版為 self-contained、Windows x64、自包含單一執行檔；目標電腦不需事先安裝 .NET。
- 不使用 Electron、WebView2、雲端服務或遙測，以降低體積、啟動成本與資料外傳風險。
- 側邊欄採平面虛擬群組；群組可以加入資料夾捷徑與單一檔案捷徑。
- 同一實體檔案可加入多個群組；群組只保存路徑，不複製文件內容。
- 虛擬群組支援新增、重新命名、排序、刪除；刪除群組或群組項目不刪除實體檔案。
- 可在 TAB 右鍵選單使用「移動至群組」；這只移動虛擬捷徑，不搬動實體檔案。從目前來源群組移除後加入目標群組；若沒有來源群組則只加入目標群組。
- 主編輯器只顯示純文字與語法高亮，不在編輯區渲染 Markdown。
- 每個編輯窗格有自己的 TAB 列；可自由水平或垂直分割，形成任意多欄、多列布局。
- 同一檔案可透過「在新檢視開啟」建立第二個編輯檢視。不同檢視共享內容、儲存狀態與 undo/redo，但擁有獨立游標與捲動位置。
- 一般開啟已開啟檔案時，切換至現有 TAB，不自動建立重複 TAB。
- 太陽／月亮按鈕控制全域亮色／暗色主題。
- 眼睛按鈕放在每個編輯器 TAB 上；每個 TAB 可獨立開啟一個即時更新的預覽視窗，不同文件的預覽視窗可同時存在。
- 預覽視窗綁定按下按鈕當下的 TAB；切換其他 TAB 不會改變其來源。再次按下同一 TAB 的眼睛按鈕會關閉對應預覽；手動關閉預覽也會同步解除 TAB 的啟用狀態。
- 語法顏色依亮色／暗色主題分開保存，並可在設定中逐類調整與恢復預設值。
- 渲染閱讀器可設定 H1～H6 字體大小、行距、段落間距與標題／正文間距；使用 Windows 系統 UI 字型。
- MVP 支援 CommonMark 與常見 GFM：標題、段落、粗體、斜體、刪除線、連結、清單、任務清單、引用、表格、行內程式碼、程式碼區塊、水平線與自動連結。
- MVP 不支援數學公式、Mermaid、外掛式 Markdown 語法或雲端同步。
- YAML 支援 Markdown 頂端 front matter、`yaml`／`yml` fenced code block，以及直接開啟 `.yaml`／`.yml` 檔案的語法分色；直接開啟 YAML 檔案不提供 Markdown 預覽。
- 編輯停止約 1 秒後自動存檔。快照只在文件有變更時建立，預設每 5 分鐘一份、每檔最多 20 份，兩者可在設定中調整。
- 每個原始檔案旁建立一個隱藏快照檔，保存該檔案全部歷史快照；可在選單刪除單次快照或刪除整個快照檔案。
- 快照可進行左右差異檢視，並可「回復並覆蓋目前內容」或「另存為新檔案」。
- 群組、設定與工作階段資料放在 `%LocalAppData%\MDeditor`，不在文件目錄建立群組設定檔。
- 下次啟動恢復群組、窗格布局、已開啟 TAB、各檢視游標／捲動位置與亮／暗主題；預覽視窗不自動重開。
- 軟體提供台灣繁體中文與英文，語言切換即時生效，不需重開程式；非這兩種 Windows 語言時預設使用台灣繁體中文。

## 2. MVP 範圍與明確排除項目

### 納入範圍

- 虛擬群組與群組項目管理。
- Markdown、YAML 與可解碼純文字檔的開啟、編輯、儲存。
- TAB 排列、TAB 拖曳排序、窗格分割／合併、同一檔案多檢視。
- 基本編輯操作：新增、開啟、另存新檔、儲存、關閉 TAB、undo/redo、尋找與取代。
- Markdown 語法分色、YAML 分色、亮／暗主題與可自訂配色。
- 獨立預覽視窗、基本 Markdown 閱讀排版、本機相對路徑圖片。
- 自動存檔、快照歷史、差異檢視、快照刪除與回復。
- UTF-8（含／不含 BOM）、UTF-16 Little Endian（含 BOM）與換行格式保存。
- 外部檔案變更偵測與安全處理。

### MVP 不納入

- 雲端、帳號、同步、協作或網路 API。
- 外掛系統、主題套件匯入／匯出與完整自訂 CSS。
- 數學公式、Mermaid、PDF／HTML／圖片匯出。
- Git 操作、專案建置、終端機、LSP、程式碼智能提示。
- 多個主編輯器主視窗；預覽視窗可多開，但主工作區只有一個主視窗。
- 直接刪除或搬移 Windows 實體檔案的檔案管理功能。

## 3. 使用者介面

### 主視窗

主視窗使用標準 Windows 視窗行為，內容採低干擾的 WPF 版面：左側虛擬群組側邊欄、右側編輯工作區、上方簡潔工具列／選單、下方狀態列。亮／暗主題以 WPF ResourceDictionary 切換，不自製會破壞 Windows 視窗按鈕行為的完整標題列。

工具列只保留常用動作：新增檔案、開啟檔案、儲存、分割檢視、尋找，以及右側的語言、主題與其他設定入口。完整功能放在選單與 TAB／群組右鍵選單。

### 虛擬群組側邊欄

- 群組以平面列表呈現，可拖曳排序。
- 群組內可列出檔案捷徑與資料夾捷徑；資料夾展開後即時讀取實體目錄內容。
- 資料夾可多層展開；隱藏檔與 MDeditor 快照檔不列入一般清單。
- 已知副檔名 `.md`、`.markdown`、`.yaml`、`.yml` 顯示對應圖示與語法模式；其他可解碼純文字檔以一般文字模式開啟。
- 找不到路徑時保留群組項目並標示遺失，可選擇重新指定路徑或移除捷徑。
- 群組右鍵選單提供新增檔案、新增資料夾、重新命名、排序與刪除；移除僅解除群組關聯。
- 從 Windows 檔案選擇器加入檔案／資料夾；支援從檔案總管拖曳路徑至群組。

### TAB 與編輯窗格

- 每個窗格有獨立的 TAB 列與編輯器。
- TAB 顯示檔名、儲存狀態、關閉按鈕與眼睛按鈕；完整路徑在 tooltip 顯示。
- TAB 支援拖曳排序、拖曳到其他窗格、右鍵建立新檢視、移動至群組、在檔案總管顯示與關閉。
- 分割動作針對目前窗格，可選水平或垂直；每個新窗格可再繼續分割。
- 關閉含有 TAB 的窗格時，TAB 會移到相鄰窗格；最後一個窗格不可被關閉。
- 關閉 TAB 只關閉該檢視；同檔案的其他檢視不受影響。
- 狀態列顯示目前檢視的行／欄、檔案編碼、換行格式與自動存檔／錯誤狀態。

### 預覽視窗

- 預覽使用 WPF 原生內容控制項，不依賴瀏覽器引擎。
- Markdig AST 轉換為 WPF 閱讀元件，包含標題、段落、清單、引用、表格、程式碼、連結、圖片與水平線。
- 預覽內容於文字變更後短暫 debounce 再更新，避免每個按鍵都重建整頁。
- 相對路徑圖片以目前 Markdown 文件所在目錄解析；找不到圖片時顯示替代文字與缺圖提示。
- 外部連結交由 Windows 預設瀏覽器開啟；預覽本身不執行 JavaScript。
- YAML front matter 保留在編輯器，但不在正文閱讀區顯示。

### 設定與語言

設定視窗分為「語言」、「編輯器」、「閱讀器」、「快照」四個區段：

- 語言：台灣繁體中文／English，即時切換。
- 編輯器：亮色／暗色分別設定一般文字、標題、粗體／斜體／刪除線、連結、清單／引用、程式碼、YAML key／value／註解／front matter 分隔線顏色。
- 閱讀器：H1～H6 字體大小、行距、段落間距、標題／正文間距，以及恢復預設值。
- 快照：快照間隔與單檔快照數量上限；數值以合理範圍限制，預設 5 分鐘／20 份。

設定變更即時套用至現有編輯器、預覽視窗與新建視窗。

## 4. Architecture

### 專案分層

```text
src/
  MDeditor.Core/       純 C# domain、文件、群組、快照、Markdown 與設定邏輯
  MDeditor/            WPF UI、視窗、ViewModel、命令與資源字典
tests/
  MDeditor.Verification/  不依賴第三方測試框架的可執行驗證案例
```

### 核心元件與責任

- `DocumentManager`：依 canonical full path 管理唯一 `DocumentSession`，處理開啟、共用內容、編碼、換行格式與生命週期。
- `DocumentSession`：保存目前文字、原始路徑、編碼、換行格式、最後成功儲存狀態、最後快照狀態與錯誤狀態。
- `DocumentViewState`：保存單一檢視的 caret offset、水平／垂直捲動位置、所屬窗格與來源群組 ID。
- `FileCodec`：辨識 UTF-8／UTF-8 BOM／UTF-16 LE BOM，將外部內容轉為內部文字，儲存時恢復原編碼與換行格式。
- `FilePersistenceService`：使用同目錄暫存檔與 replace 流程寫入，避免半寫入檔案；處理唯讀、權限與磁碟錯誤。
- `GroupManager`：管理平面群組、群組項目、路徑 canonicalization、重複項目與移動 TAB 對應捷徑。
- `SessionStore`：讀寫群組、設定與工作階段 JSON，採 atomic replace，啟動時對壞檔回退至最後可讀版本並顯示通知。
- `PaneLayoutManager`：管理遞迴 split tree、窗格比例、TAB 所屬窗格與布局序列化。
- `SyntaxHighlightingService`：載入 Markdown／YAML 語法定義，套用主題配色，辨識 front matter 與 fenced YAML。
- `MarkdownPreviewService`：使用 Markdig 解析 CommonMark／GFM，將 AST 交給原生 WPF renderer，解析相對圖片與連結。
- `PreviewWindowManager`：以檢視 ID 對應預覽視窗，支援多視窗共存、內容更新、主題／語言切換與關閉同步。
- `AutoSaveService`：每個文件以 debounce timer 排程自動儲存，避免多檢視重複寫入。
- `SnapshotService`：每個文件維護間隔計時器、隱藏 sidecar 路徑、快照清單、刪除、差異與回復。
- `ExternalChangeMonitor`：監控文件 last-write／length，偵測外部變更並要求使用者選擇重新載入、保留目前內容或進行差異檢視。
- `LocalizationService`：以語言資源字典提供 zh-TW／en-US 文字，切換時通知所有開啟視窗。

### 核心資料關係

- `DocumentSession` 與 `DocumentViewState` 為一對多；多個檢視指向同一 `DocumentSession`。
- `GroupItem` 只保存 `Path` 與 `Kind`，不持有文件文字。
- `PreviewWindow` 指向 `DocumentViewState`，而非只有檔案路徑，因此同一檔案的不同檢視可有獨立預覽視窗。
- `PaneLayoutNode` 以 `SplitNode` 或 `EditorPaneNode` 遞迴表示任意欄列布局。

### 主要介面

以下為應用程式內部 service contract；不對外發布 public API：

```csharp
interface IDocumentManager
{
    DocumentSession Open(string path, OpenMode mode);
    DocumentViewState CreateView(DocumentSession document, Guid? sourceGroupId);
    Task<SaveResult> SaveAsync(DocumentSession document, CancellationToken cancellationToken);
}

interface IGroupManager
{
    Group CreateGroup(string name);
    void AddPath(Guid groupId, string path, GroupItemKind kind);
    void MovePath(Guid sourceGroupId, Guid targetGroupId, string canonicalPath);
    void RemovePath(Guid groupId, string canonicalPath);
}

interface ISnapshotService
{
    Task<IReadOnlyList<SnapshotInfo>> ListAsync(string sourcePath, CancellationToken cancellationToken);
    Task CreateAsync(DocumentSession document, CancellationToken cancellationToken);
    Task DeleteAsync(string sourcePath, Guid snapshotId, CancellationToken cancellationToken);
    Task DeleteAllAsync(string sourcePath, CancellationToken cancellationToken);
    Task RestoreToDocumentAsync(string sourcePath, Guid snapshotId, RestoreMode mode, CancellationToken cancellationToken);
}
```

## 5. 持久化與資料流

### 使用者資料

`%LocalAppData%\MDeditor\` 下使用三個 UTF-8 JSON 檔：

- `settings.json`：語言、主題、亮／暗語法配色、閱讀器排版、快照間隔與數量上限。
- `groups.json`：群組 ID、名稱、排序、檔案／資料夾項目與路徑。
- `session.json`：pane split tree、窗格 TAB 順序、文件路徑、來源群組 ID、active TAB、caret／scroll state。

每次寫入先寫 `.tmp`，成功後以原子 replace 取代正式檔；若讀取失敗，保留壞檔供診斷並載入安全預設值。

### 快照 sidecar

對來源檔案 `<name>` 建立同目錄 sidecar：

```text
.<name>.mdeditor-snapshots.json
```

例如 `notes.md` 對應 `.notes.md.mdeditor-snapshots.json`。建立後設定 Windows `Hidden` attribute，側邊欄也以已知命名規則排除。sidecar 內保存 schema version、canonical source path、snapshot ID、UTC 建立時間、文字內容、編碼、換行格式與來源檔案 metadata。

快照刪除、回復與 sidecar 整檔刪除均先顯示確認；sidecar 寫入採暫存檔 replace。無法寫入時保留目前編輯內容並顯示可判定的錯誤，不靜默丟失資料。

### 開啟與儲存流程

1. 使用者從群組、檔案選擇器或 TAB 命令要求開啟路徑。
2. `DocumentManager` canonicalize 路徑；若文件已存在，通常建立／聚焦既有 view，只有「在新檢視開啟」才建立第二 view。
3. `FileCodec` 讀取 BOM、文字內容與換行格式；失敗時不建立可寫 TAB。
4. `SyntaxHighlightingService` 依副檔名與文件前幾行選擇 Markdown／YAML／Plain Text。
5. 編輯器內容變更後通知預覽、auto-save、外部變更狀態與 TAB 狀態。
6. 停止輸入約 1 秒後 `AutoSaveService` 觸發 `SaveAsync`；成功後更新磁碟 metadata。
7. 快照計時器到期且內容自上次快照有變更時，建立或更新 sidecar；超過上限刪除最舊項目。

### 外部變更流程

- 監控到磁碟 metadata 改變時先 debounce，避免單次儲存造成多次事件。
- 若目前文件沒有本地變更，也仍顯示外部更新提示，讓使用者選擇重新載入、保留目前內容或差異檢視。
- 若有本地變更，禁止自動覆蓋；「重新載入」明確警告將丟棄目前 buffer，「保留」維持本地內容，「差異檢視」以唯讀外部版本與目前版本並列。
- 使用者選擇保留本地內容後，下一次儲存會再次提示即將覆蓋外部版本。

## 6. 錯誤處理與安全性

- 所有檔案寫入使用同目錄暫存檔與原子取代；權限、唯讀、路徑不存在、磁碟空間不足均顯示原因與可行動作。
- 群組路徑使用 canonical full path；不因路徑大小寫或相對路徑造成同檔案重複 session。
- 群組刪除、群組項目移除、TAB 關閉與快照刪除均不直接刪除原始文件，只有明確的「刪除快照檔案」才移除 sidecar。
- 預覽不載入 JavaScript，不使用 WebView；相對圖片限制於目前文件目錄的有效檔案路徑，避免預覽內容透過任意路徑載入資源。
- 外部連結交由系統瀏覽器；不在應用程式內處理網路資料。
- 不收集遙測、不登入、不上傳文件或設定。
- JSON 設定與快照讀取有 schema version；未知欄位忽略，無法解析時回退並通知，不阻止應用程式啟動。

## 7. 技術與 Enterprise license review

### 選定項目

- `.NET 10 / WPF`：Windows 桌面執行環境；採 self-contained win-x64 發布。官方 .NET SDK／runtime 使用 MIT 與相關 Windows product license／third-party notices，發布時保留官方授權與 notices。
- `AvalonEdit 6.3.1.120`：WPF 編輯器元件，MIT License；只用於文字編輯與游標／捲動管理。
- `Markdig 1.3.2`：CommonMark／GFM parser，BSD-2-Clause；只在本機解析 Markdown AST。

### 不採用項目

- 不採用 WebView2、Electron、Tauri、瀏覽器服務或雲端服務。
- 不採用 YamlDotNet；MVP 的 YAML 需求是語法辨識與分色，不需要完整 YAML 反序列化。
- 不採用第三方 UI theme／color picker 套件；顏色選擇器以 WPF 原生控制項實作。

上述選定項目沒有 subscription、copyleft 或外部資料傳輸要求；MIT／BSD-2-Clause 的主要義務是隨發布物保留授權與著作權聲明。正式安裝 SDK、restore NuGet 與發布前，需固定版本、檢查 lock／transitive dependencies，並在應用程式附上 `THIRD-PARTY-NOTICES.txt`。

## 8. 驗證與驗收標準

### 自動化驗證

`tests/MDeditor.Verification` 使用不依賴第三方測試框架的 console assertions，覆蓋：

- 相同 canonical path 只產生一個 `DocumentSession`，不同 view 共享文字但不共享 caret／scroll state。
- 群組可新增檔案／資料夾、同檔案可存在多群組、移動只改虛擬關聯不改實體路徑。
- Markdown／YAML 副檔名與 front matter／fenced YAML 判斷。
- UTF-8 BOM／無 BOM、UTF-16 LE、CRLF／LF 保存與 round-trip。
- 快照間隔／上限、單次刪除、整檔刪除、restore 與 sidecar 隱藏 attribute。
- 設定／群組／session JSON 的 atomic write、壞檔回退與 session restore 資料。
- Markdown 基本節點轉換與相對圖片／外部連結策略。

### 人工驗收

- Windows 11 x64 上執行 self-contained EXE，不安裝 .NET 也能啟動。
- 左側建立至少三個平面群組，加入同一檔案至兩群組，從任一群組開啟並移動 TAB，確認實體檔案位置不變。
- 建立至少四個窗格，測試水平／垂直任意分割、TAB 拖曳、同檔案兩檢視獨立捲動與同步編輯。
- 開啟多個文件的眼睛按鈕，確認多個預覽視窗並存、即時更新、主題同步與關閉同步。
- 以 Markdown／YAML 範例確認所有既定語法分色、亮／暗配色、H1～H6 與間距設定即時生效。
- 修改檔案後等待自動存檔；在原始目錄確認 sidecar 存在且具 Hidden attribute，且側邊欄不顯示。
- 建立超過快照上限，確認最舊項目淘汰；刪除單次快照與整個 sidecar 均需要確認且結果可見。
- 觸發外部修改，確認重新載入／保留／差異檢視三條路徑不會靜默覆蓋本地內容。
- 切換中英文，確認目前視窗與預覽標題、選單、設定、提示訊息即時更新；重啟後語言仍保留。
- 關閉後重新開啟，確認群組、窗格、TAB、active TAB、caret／scroll state 與主題恢復，預覽視窗不自動重開。

### 交付驗收

- `dotnet build -c Release` 成功。
- `dotnet run --project tests/MDeditor.Verification` 全部 assertions 通過。
- `dotnet publish src/MDeditor/MDeditor.csproj -c Release -r win-x64 --self-contained true` 成功，輸出可在沒有 .NET runtime 的 Windows 11 x64 執行。
- 發布資料夾包含應用程式、必要授權與 third-party notices；不包含測試資料、原始文件或使用者快照。

## 9. 風險與取捨

- WPF 原生 Markdown renderer 比 WebView HTML renderer 需要較多控制項映射，但可避免 WebView2 runtime、JavaScript 與額外程序，符合輕量與離線要求。
- 任意窗格布局以遞迴 split tree 實作，序列化與恢復較複雜；換取真正的多欄／多列自由度，且可用單一 active pane 模型維持操作一致。
- 快照採同目錄單一 JSON sidecar，管理簡單且可隨原始檔案備份；大文件或大量快照會增加 sidecar 體積，因此以上限與 full-content snapshot 控制風險。
- 自包含發布檔案會比 framework-dependent 大；這是「目標電腦不需安裝 .NET」與「最小程式檔案」間的明確取捨。
