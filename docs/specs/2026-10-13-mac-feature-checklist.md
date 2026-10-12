# Glystrata 1.2.3 功能與行為驗收清單（Mac 重寫用）

整理日期：2026/10/13｜來源版本：1.2.3（Windows／WPF）｜用途：Swift 原生 macOS 版逐項驗收

優先順序：CHANGELOG 較新版本 > 原始碼 > 早期規格與計畫。文件之間互相矛盾的地方不自行裁決，統一列在最後一章。

## 標記說明

- 平台標記（每條最後一個）
  - `[通用]`：兩個平台做法相同，照做即可。
  - `[需轉換]`：功能要有，但 Mac 的做法不同（資料路徑、隱藏檔、檔案關聯、Ctrl 快速鍵、系統對話框等），實作前需定案，決策項目見最後一章。
  - `[Windows 專屬]`：Mac 上沒有對應意義（Snap Layouts、自訂標題列、Win32 輸入法、.NET 執行環境、Windows 登錄檔等）。
- 來源簡稱：`CL` = CHANGELOG.md（後接版本）；`SPEC` = docs/specs/2026-09-04-markdown-editor-design.md；`PLAN-*` 為 docs/plans/ 的簡稱（PLAN-character-toolbar = 2026-09-04-character-toolbar-diff-polish.md；PLAN-group-menu = 2026-09-04-group-menu-snapshot-polish.md；PLAN-mvp-polish = 2026-09-04-mvp-polish-fixes.md；PLAN-v0.0.2 = 2026-09-04-v0-0-2-file-open-and-menu-polish.md；PLAN-v1-2-0 = 2026-09-18-v1-2-0-plan.md；PLAN-markdown-editor = 2026-09-04-markdown-editor.md）；`REVIEW` = docs/reviews/2026-09-07-code-review-findings.md；其餘為原始碼檔名與方法名（路徑皆在 src/ 下）。
- 驗證方式：每條都可以動手操作。預期結果寫在句尾。

---

## 1. 檔案開啟與儲存

- [ ] 按 Ctrl+N（或工具列「＋」、選單「檔案 → 新增」）建立「未命名」分頁，分頁標題顯示「未命名」，新文件為 UTF-8（無 BOM）、LF 換行。（來源：CL 0.0.1；DocumentManager.CreateUntitled）[需轉換]
- [ ] 按 Ctrl+O（或工具列開啟、選單「開啟…」）開啟檔案對話框，可多選；預設篩選為 `*.md;*.markdown;*.mdown;*.yaml;*.yml;*.txt`，另有「所有檔案」。每個被選的檔案各開一個分頁。（來源：MainWindow.OpenFiles）[需轉換]
- [ ] 開啟已經開著的檔案時，切換到既有分頁，不建立重複分頁。（來源：SPEC §1；DocumentManager.OpenView）[通用]
- [ ] 開啟不存在、無法讀取或編碼不支援的檔案時，跳出「開啟檔案失敗」訊息且不建立分頁；同時把該路徑從「最近開啟過的文件」移除。（來源：MainWindow.OpenPath；CL 1.2.0）[通用]
- [ ] 已有路徑的文件按儲存，內容寫回原檔；成功後分頁標題的「 •」消失，狀態列顯示「已儲存」。（來源：MainWindow.SaveActiveAsync、SaveDocumentAsync；TabHeaderControl.Refresh）[通用]
- [ ] 手動儲存成功後，狀態列文字顯示「已自動儲存」。這是現行程式的行為（`SaveDocumentAsync` 共用自動存檔的狀態文字），是否保留需決定，見最後一章。（來源：MainWindow.SaveDocumentAsync）[通用]
- [ ] 未命名文件按儲存，跳出另存新檔對話框，預設檔名 `untitled.md`，篩選為 Markdown、YAML、所有檔案；按取消則不儲存、分頁不變。（來源：MainWindow.SaveActiveAsAsync）[需轉換]
- [ ] 另存新檔（Ctrl+Shift+S）存到新路徑後，分頁改為新檔名；選到已存在的檔案時跳出覆寫確認。另存失敗時分頁路徑回復為舊路徑。（來源：DocumentManager.SaveAsAsync；SaveActiveAsAsync）[需轉換]
- [ ] 另存新檔成功後，新路徑加入「最近開啟過的文件」清單。（來源：MainWindow.SaveActiveAsAsync）[通用]
- [ ] 唯讀檔案存檔失敗：顯示「儲存失敗：檔案是唯讀檔案。」，分頁保留未儲存內容與「•」標記，之後可再次儲存。（來源：FilePersistenceService.SaveAsync；DocumentManager.SaveAsync；Program.cs DocumentManagerVerification）[通用]
- [ ] 存檔流程採「同目錄暫存檔 → 取代正式檔」，寫入中途失敗時不會留下半寫入的正式檔，暫存檔會被清除。（來源：FilePersistenceService.SaveAsync；SPEC §6）[需轉換]
- [ ] 同一文件的多次存檔（手動、自動、關閉時）會依序排隊執行，不會因為重疊寫入造成內容或狀態錯亂（同一文件有一把儲存閘門）。（來源：DocumentManager.SaveAsync；Program.cs 連續 4 次重疊 SaveAsync；CL 0.0.1 修正重疊存檔的序列化）[通用]
- [ ] 關閉未修改的分頁（Ctrl+W 或分頁 ×）直接關閉，不跳提示。（來源：MainWindow.CloseView）[需轉換]
- [ ] 關閉有未儲存變更的分頁，跳出「尚有未儲存變更」，按鈕為「儲存／不儲存／取消」。選取消或按 Esc 或關閉對話框，都不會關閉分頁。（來源：MainWindow.CloseView；CL 1.1.0）[通用]
- [ ] 在未命名、有內容的分頁按「儲存」後，會開另存新檔對話框；在對話框按取消，分頁不關閉。（來源：MainWindow.SaveDocumentSynchronously；CL 1.1.0）[通用]
- [ ] 關閉整個視窗（或選單「離開」）時，依序詢問每一份未儲存的文件；任何一份按取消，整個關閉流程中止。（來源：MainWindow.Window_Closing；CL 1.2.3）[通用]
- [ ] 關閉視窗時的存檔在背景執行，等待完成後才關閉，不會因為存檔流程需要回到 UI 執行緒而互相卡住（CL 1.2.3 修正的死鎖）。驗證方式：未存檔文件選「儲存」後，另存新檔完成，程式能正常關閉。（來源：MainWindow.WaitOffUiThread；CL 1.2.3）[通用]
- [ ] 分頁重新命名（分頁右鍵「重新命名…」）會直接改磁碟上的檔名，並同步搬移快照檔、更新群組項目與分頁路徑。（來源：MainWindow.RenameDocument；CL 1.1.5）[需轉換]
- [ ] 重新命名時，若新檔名為空、與原名相同，或已有同名檔案，不執行；已有同名檔案時顯示「目標檔名已經存在，請換一個名稱。」。（來源：MainWindow.RenameDocument；LocalizationService tab.renameConflict）[通用]
- [ ] 重新命名時，檔名含 Windows 不合法字元時顯示「檔名包含不合法的字元。」。Mac 的不合法字元集合不同，需依 Mac 規則重訂。（來源：MainWindow.RenameDocument）[需轉換]
- [ ] 未命名分頁無法重新命名，提示「未選擇檔案」。（來源：MainWindow.RenameDocument）[通用]
- [ ] 分頁右鍵「在資料夾中顯示」在檔案總管選取該檔案；未命名或未存檔的分頁此項停用。Mac 對應為在 Finder 顯示。（來源：EditorPaneControl.CreateContextMenu、ShowInFolder）[需轉換]
- [ ] 選單「檔案 → 離開」與視窗關閉鈕走同一套關閉流程（含未儲存詢問）。（來源：MainWindow.CreateCommandActions，FileExit = Close）[需轉換]

## 2. 編碼與換行

- [ ] 開啟 UTF-8（無 BOM）檔案，內容正確顯示；修改後儲存，檔案仍為 UTF-8 且沒有 BOM。（來源：FileCodec.Decode/Encode；Program.cs FileCodecVerification）[通用]
- [ ] 開啟 UTF-8 BOM（EF BB BF）檔案，修改後儲存，檔案仍保留 BOM。（來源：FileCodec.Encode；CL 0.0.1 修正）[通用]
- [ ] 開啟 UTF-16 LE（FF FE 開頭）檔案，修改後儲存，檔案仍為 UTF-16 LE 且保留 BOM。（來源：FileCodec；CL 0.0.1 修正 UTF-16 LE 的 preamble）[通用]
- [ ] 開啟 UTF-16 BE（FE FF 開頭）檔案，顯示「UTF-16 Big Endian 目前不在支援範圍內。」，且不開啟。（來源：FileCodec.Decode）[通用]
- [ ] 開啟既不是 UTF-8 也不是 UTF-16 LE 的檔案（例如 Big5、Shift_JIS、無 BOM 的 UTF-16）：UTF-8 嚴格解碼失敗時顯示「開啟檔案失敗」，不開啟。（來源：FileCodec.Decode）[通用]
- [ ] 換行偵測採多數決（CRLF、LF、CR 各自計數取最多者；平手時依 CRLF > LF > CR 的順序）；完全沒有換行的檔案視為 LF。（來源：FileCodec.DetectLineEnding）[通用]
- [ ] 存檔時，所有換行統一轉為偵測到的格式。混合換行的檔案存檔後，會全部改為多數格式。（來源：FileCodec.Encode；SPEC §2「換行格式保存」）[通用]
- [ ] 編輯區內部一律以 LF 處理文字，換行格式只在讀寫時轉換；未命名文件的換行為 LF。（來源：FileCodec.NormalizeLineEndings；DocumentSession）[通用]
- [ ] 狀態列顯示的編碼與換行是內部列舉名稱（例如 `Utf8WithBom`、`CrLf`），不是友善字串。是否沿用需決定，見最後一章。（來源：MainWindow.UpdateStatus）[通用]

## 3. 編輯與語法分色

- [ ] 編輯區顯示行號，並開啟自動換行；內容超過寬度時換行，不出現水平捲軸（僅在需要時出現）。（來源：EditorPaneControl.CreateEditor）[通用]
- [ ] 編輯區預設字型為 Cascadia Code、Consolas、Segoe UI Emoji，基準字級 14；字型名稱為 Windows 字型，Mac 需對應等寬字型並保留 emoji 後備。（來源：EditorPaneControl.CreateEditor、BaseFontSize）[需轉換]
- [ ] 編輯區的復原與重做（Ctrl+Z、Ctrl+Y）由編輯器元件處理，選單只顯示快捷鍵。（來源：AppCommands GestureOnly；MainWindow.Undo/Redo）[需轉換]
- [ ] 同一文件開兩個檢視，任一檢視打字，另一個檢視立即同步文字；兩者的游標與捲動位置各自獨立。（來源：SPEC §1；DocumentManager；CL 0.0.1）[通用]
- [ ] 同一文件的兩個檢視共用 undo／redo 歷史（一個檢視的修改可以在另一個檢視復原）。（來源：SPEC §4；PLAN-markdown-editor Task 2 驗收）[通用]
- [ ] 語法分色涵蓋：標題列（以 `#` 開頭，整行上色）、引用（`>`）、清單符號（`-`、`*`、`+`、`1.`）、連結與圖片語法、行內程式碼、強調（`**`、`__`、`*`、`_`）、程式碼區塊的 fence 行。（來源：SyntaxHighlightingService 各 Regex）[通用]
- [ ] 檔案第 1 行為 `---` 時，從第 1 行到下一個 `---`（限前 30 行內）整段以 front-matter 顏色分色。（來源：SyntaxHighlightingService.IsFrontMatterLine；SPEC §1）[通用]
- [ ] 直接開啟 `.yaml`／`.yml` 檔案，整檔以 YAML 規則分色（鍵、值、註解），不做 Markdown 分色。（來源：SyntaxHighlightingService.IsYamlFile；SPEC §1）[通用]
- [ ] Markdown 內的 ` ```yaml ` 或 ` ```yml ` 程式碼區塊應以 YAML 規則分色。規格有此要求，但現行程式碼未見實作，見最後一章。（來源：SPEC §1、§3）[通用]
- [ ] 亮色、暗色各有一組語法配色，設定中可分開編輯；套用後，已開啟的分頁立即重繪新顏色，不需要重新編輯或捲動。（來源：SyntaxHighlightingService.SetPalette；REVIEW #6）[通用]
- [ ] 語法配色可逐類調整（一般文字、標題、強調、連結、清單、引用、程式碼、YAML 鍵、YAML 值、YAML 註解、Front matter），並可還原預設（預設值見第 22 章）。（來源：SettingsWindow 編輯器選項；LocalizationService settings.color.*）[通用]

## 4. Markdown 自動輸入與格式工具列

### 4-A 自動輸入（打字時）

- [ ] 游標在項目 `- foo` 的行尾按 Enter，下一行自動產生 `- `（與上一行相同的符號加一個空格）。（來源：MarkdownTypingAssistant.ContinueList）[通用]
- [ ] `*` 與 `+` 項目同樣會延續相同符號。（來源：MarkdownTypingAssistant.ListMarkerPattern）[通用]
- [ ] 有序清單按 Enter 會遞增編號（`3. a` → 下一行 `4. `）。（來源：ContinueList；Program.cs MarkdownTypingAssistantVerification）[通用]
- [ ] 核取方塊項目按 Enter，下一行為未勾選的 `- [ ] `，不會沿用已勾選狀態。（來源：ContinueList；Program.cs）[通用]
- [ ] 縮排的清單項目按 Enter，新行維持同一層縮排，不會每按一次多縮一層（以取代而非疊加縮排）。（來源：ContinueList；CL 1.2.1；Program.cs）[通用]
- [ ] 在項目文字中間按 Enter，游標後半段文字成為同層新項目的內容。（來源：Program.cs MarkdownTypingAssistantVerification）[通用]
- [ ] 空的清單項目（只有符號）按 Enter，會清除該行的符號，不再延續。（來源：ContinueList）[通用]
- [ ] 非清單的縮排行按 Enter，只沿用縮排，不會加上項目符號。（來源：Program.cs）[通用]
- [ ] 打完 `->`、`<->`、`=>`、`>=`、`<=`、`...`、`--` 後，立即替換為 `→`、`↔`、`⇒`、`≥`、`≤`、`…`、`—`。（來源：MarkdownTypingAssistant.SymbolReplacements）[通用]
- [ ] `---` 分隔線與 `| --- |` 表格分隔列中的 `--` 不會被替換成 `—`。（來源：MarkdownTypingAssistant.LooksLikeStructuralDashes）[通用]
- [ ] 符號剛被替換後，立即按 Backspace，會還原成原本的字元（只對剛替換的那一次有效）。（來源：MarkdownTypingAssistant.Attach PreviewKeyDown）[通用]
- [ ] 中文輸入法（注音、拼音）組字未送出前，不會誤觸發續行或符號替換；組字結束、文字送進編輯區後才生效。（來源：MarkdownTypingAssistant 只處理 TextEntered；ImeComposition 系列為 Windows 實作；CL 0.1.0、1.1.0 修正組字顯示）[需轉換]

### 4-B 文字編輯快捷列

- [ ] 偏好設定「一般」的「顯示文字編輯快捷列」預設為關閉；勾選套用後，所有編輯窗格的分頁列上方立即出現快捷列，取消勾選後立即消失。（來源：SettingsWindow 一般；MainWindow.ApplySettings；CL 0.0.2 計畫）[通用]
- [ ] 快捷列按鈕包含：內文、標題 1～6、粗體、斜體、刪除線、行內程式碼、連結、無序清單、有序清單、引用、程式碼區塊。（來源：MarkdownFormatCommand；LocalizationService format.*）[通用]
- [ ] 有選取文字時，格式套用到選取內容。例如粗體包成 `**選取內容**`，並保持原有文字被選取。（來源：MarkdownFormattingService.ApplyInline）[通用]
- [ ] 沒有選取文字時，粗體、斜體、刪除線、行內程式碼會插入佔位字（`bold`、`italic`、`strikethrough`、`code`），並選取佔位字。（來源：ApplyInline；PLAN-character-toolbar 驗收）[通用]
- [ ] 標題按鈕會把選取涵蓋的每一行改為指定層級，已有其他層級的標題會被覆寫（不是切換）；選「內文」則移除標題符號。（來源：MarkdownFormattingService.ApplyHeading）[通用]
- [ ] 沒有選取文字時按標題按鈕，會插入 `# heading`（或對應層級），並選取 `heading` 佔位字；內文按鈕則插入 `text`。（來源：ApplyHeading）[通用]
- [ ] 清單、引用按鈕：若選取的每一行都已有該前綴，再按一次會移除前綴；否則為每一行加上前綴。（來源：ApplyListPrefix）[通用]
- [ ] 有序清單每一行加上 `1. `（Markdown 會自動編號）。（來源：Program.cs MarkdownFormattingVerification）[通用]
- [ ] 程式碼區塊按鈕會在選取內容外包一層 ` ``` ` 行。（來源：Program.cs MarkdownFormattingVerification；ApplyCodeBlock）[通用]
- [ ] 連結按鈕會跳出「連結網址」輸入框（預設值 `https://`）。有選取文字時，選取文字成為連結文字；沒有選取時使用佔位文字 `link text`。按取消則不做任何變更。（來源：MainWindow.Pane_FormattingRequested；ApplyLink）[通用]
- [ ] 快捷列操作是文字轉換，不做完整 Markdown 解析；巢狀清單與複雜選取採保守的行首前綴處理。（來源：PLAN-character-toolbar 風險段）[通用]
- [ ] 編輯區仍只有語法分色，不會變成所見即所得渲染。（來源：PLAN-character-toolbar 需求；SPEC §1）[通用]
- [ ] 說明 → 「Markdown 語法說明」開啟獨立說明視窗，內容為語法範例。（來源：MarkdownHelpWindow；LocalizationService help.markdownGuide）[通用]

## 5. 尋找與取代

- [ ] Ctrl+F（或編輯 → 「尋找與取代」）開啟「尋找與取代」視窗（非模態）；視窗已開著時再按，改為啟用既有視窗。（來源：MainWindow.OpenFindReplace）[需轉換]
- [ ] 按 Ctrl+H 開啟同一個視窗，並把焦點放在「取代為」欄位。（來源：AppCommands EditReplace；OpenFindReplace(focusReplace: true)）[需轉換]
- [ ] 若開啟視窗時編輯區有選取文字，該文字會帶入搜尋欄。（來源：OpenFindReplace；FindReplaceWindow.Seed）[通用]
- [ ] 搜尋不區分大小寫，使用系統文化設定的比較（`CurrentCultureIgnoreCase`）。Mac 需決定比較方式，見最後一章。（來源：FindReplaceWindow.RecomputeMatches）[通用]
- [ ] 搜尋為純文字比對，不支援正規表示式，也沒有全字比對選項。（來源：FindReplaceWindow）[通用]
- [ ] 搜尋欄按 Enter 找下一個，按 Shift+Enter 找上一個，按 Esc 關閉視窗。（來源：FindReplaceWindow.FindBox_KeyDown）[通用]
- [ ] 找到符合項時，編輯區選取該處並捲到可見範圍，狀態列顯示「第 x 項，共 y 項」。（來源：FindReplaceWindow.SelectMatch、UpdateStatus；LocalizationService findReplace.matchCount）[通用]
- [ ] 找不到時狀態列顯示「找不到符合項目」。（來源：LocalizationService findReplace.noMatches）[通用]
- [ ] 「取代」取代目前選取的符合項，並自動跳到下一個；取代欄位可以留空，留空代表把符合文字刪除（不會被當成取消）。（來源：FindReplaceWindow.ReplaceCurrent；REVIEW #3）[通用]
- [ ] 取代欄按 Enter 等同「取代」。（來源：FindReplaceWindow.ReplaceBox_KeyDown）[通用]
- [ ] 「全部取代」一次取代所有符合項，狀態列顯示「已取代 N 處」；搜尋欄留空時不執行。（來源：FindReplaceWindow.ReplaceAll；LocalizationService findReplace.replacedCount）[通用]
- [ ] 搜尋與取代的對象是目前作用中的檢視；切換分頁後，搜尋結果依新分頁重算。（來源：FindReplaceWindow.Navigate、_getView）[通用]
- [ ] 「全部取代」以整段文字重寫方式實作，是否保留 undo 歷史需實機確認，見最後一章。（來源：FindReplaceWindow.ReplaceAll）[通用]

## 6. TAB 與窗格分割

- [ ] 分頁標題顯示檔名；有未儲存變更時，檔名後加上「 •」；未命名文件顯示「未命名」（隨語言切換）。（來源：TabHeaderControl.Refresh；LocalizationService document.untitled）[通用]
- [ ] 分頁標題最長 220（WPF 單位），超過時以省略號截斷；滑鼠停留顯示完整路徑（未命名則顯示「未命名」）。（來源：TabHeaderControl 建構子與 Refresh）[通用]
- [ ] 分頁列一律單列橫排；分頁過多時可橫向捲動，滑鼠滾輪可捲動分頁列，目前選取的分頁會自動捲入可視範圍。（來源：CL 0.2.0；EditorPaneControl 分頁捲動器）[通用]
- [ ] 分頁由左至右依序為：群組色條（有群組色時才顯示）、檔名、閱讀器（眼睛）按鈕、關閉（×）按鈕。（來源：TabHeaderControl 建構子；CL 0.2.0、1.2.0）[通用]
- [ ] 點擊分頁即切換為目前分頁；點擊編輯區也會讓該分頁成為作用中分頁。（來源：EditorPaneControl.CreateEditor GotFocus）[通用]
- [ ] 分頁右鍵選單（順序）：關閉、Markdown 閱讀器（YAML 停用）、在資料夾中顯示、在新窗格開啟 ▸（左右並排、上下並排）、移動到群組、快照紀錄、立即建立快照（未存檔停用）、重新命名。（來源：EditorPaneControl.CreateContextMenu；CL 1.1.5、1.2.0）[通用]
- [ ] 分頁右鍵「在新窗格開啟」的兩個子項目，會在目前窗格旁邊（左右或上下）建立新窗格，並以同一份文件開一個新檢視。（來源：MainWindow.OpenViewInNewPane；CL 0.2.0）[通用]
- [ ] 用工具列或選單「左右分割」「上下分割」：若目前窗格有多個分頁，最後一個分頁移到新窗格；若只有一個分頁，新窗格開同一檔案的第二個檢視。（來源：MainWindow.SplitActivePane；CL 0.2.0）[通用]
- [ ] 窗格分割線可用滑鼠拖曳，比例限制在 10%～90%。（來源：MainWindow.CreateLayoutVisual；UpdateSplitRatio）[通用]
- [ ] 分割可以一再巢狀進行，形成任意多列、多欄的版面。（來源：SPEC §1、§9；PaneLayoutOperations）[通用]
- [ ] 關閉分頁後，若該窗格已沒有任何分頁，窗格自動收合，分隔線一併移除，不留下空白窗格。（來源：MainWindow.CloseView → RemovePaneAndMerge；CL 0.0.1 修正殘留的灰色分隔線）[通用]
- [ ] 「關閉目前窗格」（Ctrl+Shift+W、工具列、選單）：窗格內分頁移到相鄰窗格；只剩一個窗格時無效（不會關閉最後一個窗格）。（來源：MainWindow.CloseActivePane、RemovePaneAndMerge；SPEC §3）[通用]
- [ ] 「還原單一窗格」（Ctrl+Alt+1、工具列、選單）：所有分頁（含檢視）合併到一個窗格，分隔線全部消失；不關閉任何文件。（來源：MainWindow.CollapseToSinglePane；CL 0.0.1）[通用]
- [ ] 關閉某分頁時，同一文件的其他檢視不受影響，文件內容保留。（來源：SPEC §3「關閉 TAB 只關閉該檢視」；DocumentManager.CloseView）[通用]
- [ ] 關閉分頁後，若該文件已沒有任何檢視，釋放它的檔案監看、計時器與事件訂閱。（來源：MainWindow.DetachDocumentIfUnused；REVIEW #1）[通用]
- [ ] 關閉分頁後，作用中分頁切換到同一窗格的其他分頁；若窗格已空，切換到其他窗格的分頁。（來源：MainWindow.CloseView）[通用]
- [ ] 每個檢視各自保存游標位置與水平、垂直捲動位置；切換分頁後恢復。（來源：DocumentViewState；EditorPaneControl.RestoreViewPosition；SPEC §4）[通用]
- [ ] 拖曳分頁到同一窗格內的另一個分頁上，被拖的分頁插入到目標位置（拖往右方插入到右側，拖往左方插入到左側）；拖曳期間顯示插入線。（來源：EditorPaneControl.HandleTabDrop、ShowTabDropIndicator；CL 1.2.2）[通用]
- [ ] 拖曳分頁時，被拖的分頁變淡；放開或取消後恢復原樣。（來源：CL 1.2.2 落點提示；DropIndicator.RunDrag）[通用]
- [ ] 拖曳分頁到不同窗格的分頁上：現行程式碼不會移動分頁（只支援同窗格內排序）。規格要求可以拖到其他窗格，是否要做需決定，見最後一章。（來源：EditorPaneControl.HandleTabDrop；SPEC §3）[通用]
- [ ] 拖曳分頁到側邊欄的群組上，分頁歸入該群組，群組整列以主題色外框標示「會放進這個群組」。（來源：MainWindow.AttachGroupDragDrop；CL 1.2.2）[通用]
- [ ] 拖曳分頁到它原本就屬於的群組，或拖到其他窗格的分頁，不顯示落點提示、不做任何變化。（來源：CL 1.2.2）[通用]
- [ ] 在「所有文件」模式下，每個分頁仍顯示它所屬群組的色條。（來源：TabHeaderControl.SetGroupColor；CL 1.2.0）[通用]

## 7. 虛擬群組

- [ ] 側邊欄標題列為「群組」，右側有「＋」按鈕，提示文字為「新增群組」。（來源：MainWindow.BuildSidebar；LocalizationService group.new）[通用]
- [ ] 群組列表上方有「所有文件」列，選取時以主題的作用中色填滿，不分群組。（來源：MainWindow.BuildSidebar、RefreshAllTabsFilter；CL 1.2.0「全部 TAB」更名）[通用]
- [ ] 群組以平面列表呈現，每個群組一列；群組不展開，不列出群組內的檔案或資料夾。（來源：MainWindow.RebuildGroupsTree；CL 0.2.0、1.2.0）[通用]
- [ ] 點「＋」或選單「群組 → 新增群組」，跳出名稱輸入框；名稱留空或按取消，不建立群組。（來源：MainWindow.NewGroup）[通用]
- [ ] 點選某群組，編輯區只顯示該群組的分頁，其他分頁保留（不關閉）；點「所有文件」解除篩選，全部分頁回來。（來源：MainWindow.GroupTree_SelectedItemChanged、SelectAllTabs；SPEC §3；CL 0.0.1）[通用]
- [ ] 群組的選取底色為「群組色與側邊欄底色的混色」，混合比例即偏好設定的「選取群組的底色濃度」（預設 14%，0 為不上色）。（來源：GroupColors.TryCreateSelectionBackground；CL 1.2.0）[通用]
- [ ] 沒有指定群組色的群組被選取時，底色與「所有文件」的選取色相同。（來源：MainWindow.RefreshGroupSelection；CL 1.2.0）[通用]
- [ ] 有群組色的群組，名稱左側有寬約 3 px 的直槓色條；沒有群組色時保留空間但不顯示，群組名稱仍對齊。（來源：MainWindow.RebuildGroupsTree；CL 1.2.0）[通用]
- [ ] 群組右鍵選單（順序）：開啟檔案…、群組顏色 ▸、重新命名、刪除群組。（來源：MainWindow.CreateGroupContextMenu；CL 1.2.0、1.1.5）[通用]
- [ ] 群組右鍵「開啟檔案…」可多選檔案，檔案加入該群組並開啟分頁；不會切換目前的群組篩選。（來源：MainWindow.OpenFilesIntoGroup；CL 1.2.0）[通用]
- [ ] 群組右鍵「群組顏色」子選單：列出偏好設定中的每個色票（項目文字為色碼，前面有色塊），目前群組的顏色打勾；另有「無顏色」與「管理顏色…」（開啟偏好設定）。（來源：MainWindow.CreateGroupColorMenu；CL 1.2.0）[通用]
- [ ] 在群組顏色子選單選色後立即套用：側邊欄、分頁色條同步更新，並寫入 groups.json。（來源：MainWindow.ApplyGroupColor；CL 1.2.0）[通用]
- [ ] 群組重新命名：輸入框預填原名，確定後以去除頭尾空白的名稱儲存；留空不套用。（來源：MainWindow.RenameGroup；GroupManager.RenameGroup）[通用]
- [ ] 刪除群組前跳出確認（顯示群組名稱，按鈕為「刪除」）；取消則不刪除。（來源：MainWindow.DeleteGroup；MessageDialogs.Confirm）[通用]
- [ ] 刪除群組後，群組內的分頁仍然開著，只是失去群組關聯；實體檔案不受影響。（來源：MainWindow.DeleteGroup；SPEC §6）[通用]
- [ ] 刪除目前被篩選的群組時，篩選自動改為「所有文件」。（來源：MainWindow.DeleteGroup）[通用]
- [ ] 刪除最後一個群組時，自動建立一個名為「群組」（英文 Groups）的預設群組，列表不會變成空的。（來源：MainWindow.DeleteGroup；LoadWorkspaceAsync）[通用]
- [ ] 拖曳群組列可以重新排序；拖到目標群組的上半部或下半部，依拖曳方向決定插入位置，並顯示插入線。（來源：MainWindow.AttachGroupDragDrop、ShowGroupDropIndicator、GroupManager.MoveGroup；CL 1.2.2）[通用]
- [ ] 分頁右鍵「移動到群組」跳出群組選擇清單（不含目前群組）；確定後，來源群組移除該檔案的關聯，目標群組加入，實體檔案不動。（來源：MainWindow.MoveViewToGroup、AssignViewToGroup；CL 1.1.5）[通用]
- [ ] 未命名文件使用「移動到群組」時，只更新分頁所屬群組，不會寫入群組項目。（來源：MainWindow.AssignViewToGroup）[通用]
- [ ] 同一檔案可同時屬於多個群組；同一群組內重複加入同一檔案不會產生第二筆。（來源：Program.cs GroupVerification；SPEC §1、§8）[通用]
- [ ] 移除群組關聯、移出分頁、刪除群組，都不會刪除或搬移實體檔案。（來源：SPEC §6；GroupManager.MovePath 驗證）[通用]
- [ ] 新開的分頁會歸入目前選取的群組；若目前是「所有文件」，則依作用中分頁所屬群組；仍無法決定時，使用第一個群組。（來源：MainWindow.NewUntitled、ResolveCurrentGroup、EnsureDefaultGroup）[通用]

## 8. Markdown 閱讀器

- [ ] 分頁的眼睛按鈕（或分頁右鍵「Markdown 閱讀器」）開啟獨立閱讀器視窗；同一分頁再按一次，關閉該閱讀器。（來源：PreviewWindowManager.Toggle；EditorPaneControl；SPEC §1）[通用]
- [ ] 眼睛按鈕在閱讀器開啟時變為強調色、不透明；閱讀器被手動關閉時，眼睛按鈕同步恢復。（來源：TabHeaderControl.IsPreviewOpen；SetPreviewState；SPEC §1）[通用]
- [ ] 可同時開多個閱讀器，各自綁定開啟時的那個檢視；切換到其他分頁不會改變閱讀器的來源。（來源：PreviewWindowManager；SPEC §1）[通用]
- [ ] 閱讀器內容在編輯後約 250 ms 防抖後更新；解析在背景執行，過期的結果會被丟棄。（來源：PreviewWindow `_refreshTimer` 250 ms；REVIEW #9）[通用]
- [ ] YAML 檔案不提供閱讀器：眼睛按鈕停用，提示「YAML 檔案不提供 Markdown 預覽」；Ctrl+Shift+R 也不作用。（來源：TabHeaderControl.Refresh；MainWindow.Pane_PreviewRequested；LocalizationService toolbar.previewUnavailable）[通用]
- [ ] 閱讀器文字可以用滑鼠拖曳選取、Ctrl+A 全選、Ctrl+C 複製、右鍵選單（複製、全選），但不能編輯。（來源：README；CL 1.1.0）[通用]
- [ ] 閱讀器縮放範圍 50%～200%；Ctrl+滾輪每格 10%；底部滑桿可拖；雙擊百分比文字回到 100%。（來源：PreviewWindow.ApplyZoom、ZoomPercentText_MouseLeftButtonDown；CL 0.1.0）[通用]
- [ ] 每個閱讀器的縮放各自獨立，新開的閱讀器一律從 100% 開始；縮放不改變編輯區字級。（來源：PreviewWindow 建構子 ApplyZoom(100)；CL 0.1.0）[通用]
- [ ] 閱讀器標題 H1～H6 的字級、行距、分段間距、標題與內文的間距，由偏好設定「閱讀器選項」控制（預設值見第 22 章）。（來源：PreviewTypography；SettingsWindow 閱讀器選項；SPEC §1）[通用]
- [ ] 閱讀器內文、標題、連結、引用、程式碼的顏色亮色、暗色各自設定；預設為黑白（連結為藍色）。（來源：ReaderColorPalette；CL 1.1.5）[通用]
- [ ] 閱讀器支援：標題、段落、粗體、斜體、刪除線、連結、清單、任務清單、引用、表格、行內程式碼、程式碼區塊、水平線、自動連結。（來源：MarkdownPipelineFactory（UsePipeTables、UseTaskLists、UseEmphasisExtras、UseAutoLinks）；SPEC §1）[通用]
- [ ] 任務清單項目開頭的 `[ ]` 畫成空方框，`[x]` 畫成以主題色填滿並帶勾號的方框，並取代原本的圓點；有序清單 `1. [ ]` 保留編號，方框畫在編號後。（來源：CL 1.2.2）[通用]
- [ ] 只有項目開頭的 `[ ]`／`[x]` 才是核取方塊；寫在項目中間或接續行的，維持原文顯示。（來源：CL 1.2.2）[通用]
- [ ] 清單項目與它的子清單之間不會多出空白行；項目之間以空行分隔的鬆散清單，保留段落間距。（來源：CL 1.2.2）[通用]
- [ ] 單獨一行的 `===` 或 `---` 不會把上一段文字變成標題（Setext 標題已停用）；`#` 標題與有空行隔開的 `---` 水平線正常。（來源：MarkdownPipelineFactory；CL 1.1.5）[通用]
- [ ] 原始 HTML 不會被渲染，預覽不執行 JavaScript，也不使用瀏覽器元件。（來源：MarkdownPipelineFactory.DisableHtml；SPEC §6；Program.cs MarkdownVerification）[通用]
- [ ] YAML front matter 不顯示在閱讀區的正文中。（來源：UseYamlFrontMatter；Program.cs MarkdownVerification）[通用]
- [ ] 閱讀器中的 emoji 以彩色圖檔（Twemoji 15.1.0，CC-BY 4.0）繪製；編輯區仍是系統的單色 emoji。（來源：CL 1.1.0；src/Glystrata/Preview/EmojiRenderer.cs、EmojiScanner.cs）[通用]
- [ ] 相對路徑圖片以目前文件所在目錄為基準解析；路徑只能落在文件所在目錄之內，超出的顯示「圖片路徑必須位於文件目錄內。」。（來源：MarkdownPreviewService.CreateFileResource；SPEC §3、§6）[通用]
- [ ] 找不到圖片時，顯示替代文字與「找不到圖片」提示，不讓整份閱讀器失敗。（來源：MarkdownPreviewService；LocalizationService preview.missingImage）[通用]
- [ ] 網路圖片（http、https）不下載、不載入，顯示「預覽只載入本機相對圖片。」。（來源：MarkdownPreviewService.ResolveResource；SPEC §6）[通用]
- [ ] 圖片路徑含 `&` 等 HTML 實體字元（例如 `a&amp;b.png`）時，仍能正確找到磁碟上的檔案。（來源：MarkdownPreviewService.ResolveResource 先 HtmlDecode；REVIEW #8）[通用]
- [ ] 閱讀器中點連結：只處理 http、https、mailto，交給系統預設程式（瀏覽器或郵件）開啟；其他協定不處理。Mac 的開啟方式需以系統 API 對應實作。（來源：WpfMarkdownRenderer.OpenLink；SPEC §3）[需轉換]
- [ ] 閱讀器在編輯文字、切換主題、切換語言、修改閱讀器設定後，全部閱讀器即時更新（主題、語言、排版與顏色）。（來源：PreviewWindowManager.UpdateSettings、ApplyTheme、RefreshLanguage；SPEC §3）[通用]
- [ ] 閱讀器視窗的標題與縮放提示文字，隨語言設定切換。（來源：PreviewWindow.Localization_LanguageChanged）[通用]

## 9. 快照與比較

- [ ] 快照存在原始文件同一目錄，檔名為 `<原檔名>.gss`（例如 `筆記.md.gss`），內容為 JSON；快照檔預設為一般可見檔案。（來源：SnapshotSidecarStore.GetSidecarPath；README「資料位置」；CL 0.1.0）[需轉換]
- [ ] 偏好設定「快照 → 隱藏快照檔案」勾選後，快照檔被設為隱藏（Windows 為 Hidden 屬性）；取消勾選後恢復可見。設定套用時，開啟中的文件與群組內檔案的既有快照檔會一併轉換。Mac 的隱藏方式需決定，見最後一章。（來源：SnapshotSidecarStore.ApplyVisibility；MainWindow.SweepSnapshotVisibility；CL 0.1.0）[需轉換]
- [ ] 只有已有檔案路徑的文件會建立快照；未命名文件不建立快照（手動與自動皆然）。（來源：SnapshotService.CreateAsync；SPEC §1「只有已存檔的檔案會建立快照」）[通用]
- [ ] 自動快照：內容相對於上次變更後，停止編輯達到「快照間隔」（預設 5 分鐘，範圍 1～120）即建立一份。檢查由 30 秒計時器執行，因此實際時間可能比間隔多出最多約 30 秒（依程式碼推斷）。（來源：MainWindow.StartTimers、SnapshotTimer_Tick；SettingsWindow；LocalizationService settings.snapshotInterval.help）[通用]
- [ ] 自動快照只在內容與最新快照不同時建立；內容沒變就不會一直重複建立。（來源：SnapshotTimer_Tick 比對 LastSnapshotText；SnapshotService.CreateAsync 比對最新快照文字）[通用]
- [ ] 每檔快照上限（預設 20，範圍 1～200，手動與自動合計）。超過時刪除最舊的一份。（來源：SnapshotService.CreateAsync；AppSettings.MaxSnapshotsPerFile；LocalizationService settings.maxSnapshots.help）[通用]
- [ ] 「立即建立快照」（Ctrl+Alt+S、選單「檔案 → 立即建立快照」、分頁右鍵）：內容與最新快照相同時，狀態列顯示「內容與最新快照相同，未建立新快照。」；否則顯示「已建立快照」。（來源：MainWindow.CreateSnapshotNow；LocalizationService snapshot.unchanged、snapshot.created）[通用]
- [ ] 立即建立快照不跳出註解輸入框，註解預設為空白。（來源：PLAN-v1-2-0 已定案段；SnapshotInfo.Note 預設值）[通用]
- [ ] 快照紀錄視窗（Ctrl+Shift+H、選單「快照紀錄…」、分頁右鍵「快照紀錄」）：清單依建立時間由新到舊排列。（來源：SnapshotHistoryWindow；SnapshotSidecarStore.ReadAsync OrderByDescending）[通用]
- [ ] 快照紀錄的每一列文字格式為「yyyy-MM-dd HH:mm:ss · N chars · 註解」，時間為本地時間。「N chars」以文字的 UTF-16 長度計算，與狀態列的字元計數模式不同。（來源：SnapshotHistoryWindow.CreateSnapshotRow、FormatTimestamp）[通用]
- [ ] 每列的註解是同一列內可直接編輯的單行文字框，灰色提示為「加上註解…」；按 Enter 或焦點離開時儲存，按 Enter 不會觸發視窗的按鈕。（來源：SnapshotHistoryWindow.CreateSnapshotRow；CL 1.2.0）[通用]
- [ ] 關閉快照紀錄視窗時，尚未儲存的註解會先儲存。（來源：SnapshotHistoryWindow.OnClosing → CloseAfterSavingNotesAsync）[通用]
- [ ] 註解文字中的換行在列表中會被換成空白顯示。（來源：SnapshotHistoryWindow.FormatNotePreview）[通用]
- [ ] 選取一筆快照後按「比較」，開啟差異視窗，左側為快照內容、右側為目前緩衝內容，逐行標示新增、刪除、未變更。（來源：MainWindow.OpenSnapshotHistory CompareRequested；DiffWindow；TextDiffService.Compare）[通用]
- [ ] 差異視窗的下拉選單可換成其他快照；快照紀錄列表與差異視窗的選取同步。（來源：DiffWindow.SnapshotSelector；SnapshotHistoryWindow.SelectedSnapshotChanged；CL 1.1.0）[通用]
- [ ] 再按一次「比較」會沿用已開著的差異視窗；選到開窗之後才建立的快照時，會重建差異視窗。（來源：MainWindow.OpenSnapshotHistory；CL 1.1.0）[通用]
- [ ] 差異視窗的標題為「比較 — 時間」，時間為該快照的本地時間。（來源：DiffWindow.SetTitle）[通用]
- [ ] 差異比較為逐行最長共同子序列演算法，新增行與刪除行以亮色、暗色各自的低對比底色顯示，切換主題後已開啟的差異視窗同步更新。（來源：TextDiffService；CL 0.0.2）[通用]
- [ ] 選取快照後按「回復」，跳出對話框「回復到『時間』的內容。要取代目前內容，還是另存為新檔案？」，按鈕為「取代目前內容」「另存為新檔案」「取消」。（來源：SnapshotHistoryWindow.RestoreButton；LocalizationService snapshot.restoreTo；CL 1.1.0）[通用]
- [ ] 「取代目前內容」把分頁內容換成快照內容，分頁標示為未儲存；程式碼沒有排程自動儲存，要存檔需手動儲存。是否應自動儲存，見最後一章。（來源：MainWindow.RestoreSnapshot ReplaceCurrent；DocumentSession.ApplyRestoredContent）[通用]
- [ ] 「另存為新檔案」跳出另存對話框（預設檔名 `restored.md`）；成功後開新分頁，原分頁內容不變；失敗時顯示錯誤且不留下新分頁。（來源：MainWindow.RestoreSnapshot）[需轉換]
- [ ] 快照紀錄選取一筆後按「刪除選定」，跳出確認「確定要刪除『時間』這份快照嗎？此動作無法復原。」，確認後只刪除該筆。（來源：SnapshotHistoryWindow.DeleteButton_Click；LocalizationService snapshot.deleteConfirm）[通用]
- [ ] 「刪除整個快照檔案」為紅字按鈕，單獨放在左側；確認後刪除整份 `.gss`，原始文件不受影響。快照清單為空時此按鈕停用。（來源：SnapshotHistoryWindow.DeleteAllButton_Click；LocalizationService snapshot.deleteAll、deleteAllConfirm；CL 0.1.0）[通用]
- [ ] 快照清單為空時，列表顯示「目前沒有快照」且不可選取；「比較」「回復」「刪除選定」停用。（來源：SnapshotHistoryWindow；LocalizationService snapshot.none）[通用]
- [ ] 快照的建立、刪除、註解寫入採同一把檔案鎖，自動快照與手動操作同時進行時，不會互相覆蓋（不會遺失快照或註解）。（來源：SnapshotSidecarStore.MutateAsync 與 GetLock；CL 1.2.0）[通用]
- [ ] 快照列表點「比較」「回復」時，若快照檔損毀，顯示「快照檔案格式損壞。」並不覆寫檔案。（來源：SnapshotSidecarStore.ReadSidecarAsync InvalidDataException）[通用]
- [ ] 快照檔內容保存的是完整文字（不是差異），並帶有編碼與換行資訊，回復時會還原這兩項。（來源：SnapshotInfo、SnapshotEntry；SnapshotService.CreateAsync；MainWindow.RestoreSnapshot）[通用]

## 10. 自動儲存

- [ ] 已有檔案路徑的文件，停止打字約 1 秒（每次打字都重新計時）後，自動存檔。（來源：MainWindow.ScheduleAutoSave，計時器 1 秒；SPEC §5 步驟 6）[通用]
- [ ] 未命名文件不會自動存檔，需使用者手動另存。（來源：MainWindow.ScheduleAutoSave `FilePath is null` 直接返回）[通用]
- [ ] 自動存檔時若內容沒有修改，不寫入磁碟。（來源：MainWindow.ScheduleAutoSave Tick 的 `IsModified` 檢查）[通用]
- [ ] 自動存檔成功後，狀態列顯示「已自動儲存」，分頁的「•」消失。（來源：MainWindow.SaveDocumentAsync；LocalizationService status.autoSaved）[通用]
- [ ] 自動存檔失敗（例如唯讀、磁碟錯誤）：跳出「儲存檔案失敗」與原因；緩衝內容保留，分頁仍標示未儲存，之後可以再次手動儲存。（來源：MainWindow.SaveDocumentAsync、ShowSaveError；DocumentManager.SaveAsync）[通用]
- [ ] 文件內容變動後約 700 ms，工作階段狀態（群組、分頁、版面、設定、最近開啟）會寫入 JSON，與文件的自動存檔分開進行。（來源：MainWindow.ScheduleSessionSave；SPEC §5）[通用]

## 11. 設定（偏好設定）

- [ ] 入口在「檔案」選單最底部，項目為「偏好設定…」（快捷鍵 Ctrl+,）；視窗右上角沒有設定按鈕。（來源：MainWindow.BuildMenu；AppCommandId.SettingsOpen；CL 0.0.2、1.2.0）[需轉換]
- [ ] 偏好設定視窗依序有五個分頁：一般、編輯器選項、閱讀器選項、群組顏色、快照。（來源：SettingsWindow.BuildUi；LocalizationService settings.*）[通用]
- [ ] 按鈕列左側為「還原預設」「匯出設定檔…」「匯入設定檔…」，右側為「取消」「套用」。（來源：SettingsWindow.BuildUi；CL 1.2.0 版面調整）[通用]
- [ ] 「取消」關閉視窗，不寫入任何變更（語言、主題、色票、快照設定都不生效）。（來源：SettingsWindow cancel → Close；_working 為複本）[通用]
- [ ] 「套用」先驗證數字與顏色欄位，驗證失敗時顯示對應訊息（「請輸入有效的整數。」「請輸入有效的數字。」「無效的顏色：…」）並留在視窗內。（來源：SettingsWindow.ApplyAndClose、TryReadSettings；LocalizationService settings.error.*；REVIEW #10）[通用]
- [ ] 「套用」成功後寫入 settings.json，關閉視窗，並立即套用語言、主題、配色、快照設定到所有開啟的視窗。（來源：MainWindow.ApplySettings；SPEC §3「設定變更即時套用」）[通用]
- [ ] 「還原預設」只把對話框內容重設為預設值（含語言、主題、色票、快照設定、群組色票），需按「套用」才會生效。（來源：SettingsWindow reset 只重設 _working；CL 1.2.0）[通用]
- [ ] 一般分頁：介面語言（繁體中文、English）；主題（跟隨系統、亮色、暗色）；顯示文字編輯快捷列（預設關）；字元計數方式（含空白與換行、不含空白、不含換行）。（來源：SettingsWindow.CreateGeneralTab；CL 1.2.0「字元計數」移到一般）[通用]
- [ ] 編輯器選項分頁：可分別編輯亮色與暗色的語法配色（共 11 色），每色為文字框加「選擇顏色」按鈕（開啟 HSV 色彩選擇器）；頁面上方顯示「目前編輯：亮色（或暗色）主題的顏色」。（來源：SettingsWindow.CreateColorsTab、ColorPickerDialog；CL 1.1.5）[通用]
- [ ] 在編輯器選項中切換主題下拉選單時，目前沒套用的顏色輸入會先寫回原本的主題，不會被蓋掉。（來源：SettingsWindow theme SelectionChanged；REVIEW #4）[通用]
- [ ] 閱讀器選項分頁：閱讀器配色（內文、標題、連結、引用、程式碼）亮暗各一組；標題字級 H1～H6、分行間隔、分段間隔、標題間隔（輸入數字）。（來源：SettingsWindow.CreateReaderColorsTab；CL 1.1.5）[通用]
- [ ] 群組顏色分頁：列出所有色票，每一列有色塊、色碼文字框、「…」（HSV 選色）、移除按鈕；底部「新增顏色」；另有「選取群組的底色濃度」滑桿（0%～50%，預設 14%）。（來源：SettingsWindow.CreateGroupColorsTab；CL 1.2.0）[通用]
- [ ] 套用時，群組依色票位置自動換成新色：例如把第 1 個色票由藍改成紅，原本使用藍色的群組會變紅；被刪除色票上的群組保留原色。（來源：MainWindow.ApplySettings → GroupManager.RemapColors；CL 1.2.1；Program.cs GroupVerification）[通用]
- [ ] 快照分頁：快照間隔（分鐘，1～120）、單檔快照上限（1～200）、隱藏快照檔案（勾選）。超出範圍的值由 Normalize 夾回範圍內。（來源：SettingsWindow.CreateSnapshotsTab；AppSettings.Normalize）[通用]
- [ ] 「匯出設定檔…」另存為 `glystrata-settings.gss`（篩選名稱「Glystrata 設定檔」）。匯出前先驗證目前對話框的值；驗證失敗不匯出。（來源：SettingsWindow.ExportSettings；LocalizationService settings.export、settings.fileFilter）[通用]
- [ ] 匯出的 JSON 含整份設定，並在最外層結尾加上 `"kind": "glystrata-settings"` 作為識別。格式細節見第 22 章。（來源：SettingsWindow.ExportSettings）[通用]
- [ ] 匯出內容不包含群組、工作階段、最近開啟的檔案。（來源：SettingsWindow.ExportSettings 只序列化 AppSettings；README；CL 1.2.0）[通用]
- [ ] 「匯入設定檔…」讀入後只把值填入對話框，不直接生效；按「取消」可放棄匯入，按「套用」才生效。（來源：SettingsWindow.ImportSettings；LocalizationService settings.import.success）[通用]
- [ ] 匯入時，如果檔案含有 `snapshots` 或 `notice` 欄位，或 `kind` 不是 `glystrata-settings`，顯示「這不是 Glystrata 的設定檔。副檔名 .gss 也用於文件的快照檔…」。（來源：SettingsWindow.ImportSettings；LocalizationService settings.import.notSettings）[通用]
- [ ] 匯入時，`schemaVersion` 大於 1 顯示「這個設定檔由較新版本的 Glystrata 匯出，目前版本無法讀取。」；JSON 損壞或無法解析顯示「設定檔格式不正確或已損壞，無法匯入。」。（來源：SettingsWindow.ImportSettings；LocalizationService settings.import.tooNew、invalid）[通用]
- [ ] 偏好設定的「還原預設」「匯出」「匯入」在 Mac 上可以沿用相同的按鈕配置；檔案對話框為系統原生。（來源：SettingsWindow；SPEC §3）[需轉換]

## 12. 主題與語言

- [ ] 主題有三個選項：跟隨系統（預設）、亮色、暗色。全新安裝預設為跟隨系統；舊版 settings.json 中明確指定的亮色或暗色維持不變。（來源：AppSettings.Theme 預設 System；ThemePreference 數值 0=亮、1=暗、2=系統；Program.cs PersistenceVerification；CL 1.2.0）[通用]
- [ ] 主題選「跟隨系統」時，系統外觀從亮色切到暗色（或反過來）後，程式立即跟著變，不需重啟。（來源：SystemThemeWatcher、CustomTitleBar WM_SETTINGCHANGE；CL 1.2.0）[需轉換]
- [ ] 啟動時就依系統外觀決定主題（不先閃一下預設的亮色）。（來源：ThemeService.Resolve；REVIEW #7；CL 0.1.0 修正啟動閃爍）[需轉換]
- [ ] 亮色與暗色切換時，主視窗、側邊欄、分頁列、編輯區、狀態列、選單、右鍵選單、對話框、閱讀器、差異視窗、快照視窗全部同步變色。（來源：MainWindow.Theme_ThemeChanged；PreviewWindowManager.ApplyTheme；CL 0.0.2、CL 0.0.1 修正對比）[通用]
- [ ] 亮色與暗色的語法配色、閱讀器配色分開保存，重新啟動後仍正確。（來源：AppSettings.LightEditorPalette、DarkEditorPalette、LightReaderPalette、DarkReaderPalette；SPEC §1）[通用]
- [ ] 語言有兩個選項：繁體中文、English；設定套用後立即切換，不需重新開啟程式，包含選單、側邊欄、分頁、狀態列、按鈕、對話框、閱讀器與快照視窗的文字。（來源：LocalizationService.Apply；MainWindow.Localization_LanguageChanged；SPEC §1；CL 0.0.1）[通用]
- [ ] 語言切換後，選單的 Alt 助憶鍵底線隨語言改變（例如 `檔案(_F)`、`File(_F)`）。（來源：LocalizationService menu.*；CL 1.2.0）[Windows 專屬]
- [ ] 未命名文件的名稱隨語言切換（「未命名」或 "Untitled"）。（來源：LocalizationService document.untitled；REVIEW #10）[通用]
- [ ] 全新安裝的預設語言為繁體中文，不依系統語言決定；規格要求依系統語言，現行程式未實作，見最後一章。（來源：AppSettings.Language 預設值；SPEC §1）[通用]
- [ ] 「關於 Glystrata」顯示版本號（格式為 `Glystrata 1.2.3`）與「輕量化的文字編輯器，支援語法渲染。」。（來源：MainWindow.ShowAbout；LocalizationService about.message；README）[通用]

## 13. 最近開啟過的文件

- [ ] 「檔案 → 最近開啟過的文件 ▸」子選單最多列出 10 筆，最近開啟的排在最前面。（來源：RecentFilesService.MaxEntries = 10；Program.cs RecentFilesVerification；CL 1.2.0）[通用]
- [ ] 子選單項目的文字為檔名（檔名中的底線會被跳脫，不會變成助憶鍵）；滑鼠停留顯示完整路徑。（來源：MainWindow.RebuildRecentFilesMenu；CL 1.2.0）[通用]
- [ ] 檔案已不存在的項目顯示為停用（灰色），不能點選。（來源：MainWindow.RebuildRecentFilesMenu `IsEnabled = File.Exists`）[通用]
- [ ] 清單為空時，子選單顯示停用的「（無）」。（來源：LocalizationService file.recent.empty）[通用]
- [ ] 點選清單中的檔案，開啟該檔案（加入目前選取的群組）。（來源：MainWindow.RebuildRecentFilesMenu Click → OpenPath）[通用]
- [ ] 清單底部「清除最近開啟紀錄」清空整份清單。（來源：MainWindow.RebuildRecentFilesMenu；LocalizationService file.recent.clear）[通用]
- [ ] 以下所有開檔入口都會記錄到最近清單：開啟檔案、拖放、命令列或檔案關聯參數、群組開啟檔案、另存新檔、回復另存、工作階段還原。（來源：MainWindow.OpenPath、SaveActiveAsAsync、RestoreSnapshot、RestoreViews；PLAN-v1-2-0 第 5 項）[通用]
- [ ] 同一個檔案以不同寫法開啟（例如含 `..` 的路徑），仍視為同一筆，不重複；重複開啟會把它移到最前。（來源：RecentFilesService.Add（以規範化路徑比對）；Program.cs RecentFilesVerification）[通用]
- [ ] 工作階段還原時，已開著的檔案以「補入」方式加到清單最後，不改變既有排序，也不會把舊的紀錄擠掉（清單滿 10 筆時不補入）。（來源：RecentFilesService.AddIfMissing；RestoreViews）[通用]
- [ ] 開啟清單中的檔案失敗（例如已被刪除）時，跳出錯誤訊息並自動把該項目從清單移除。（來源：MainWindow.OpenPath catch → _recent.Remove）[通用]
- [ ] 最近清單存於獨立的 recent.json，不隨偏好設定匯出（避免匯出檔帶有使用者的檔案路徑）。（來源：JsonStateStore.LoadRecentFilesAsync 註解；PLAN-v1-2-0 第 5 項）[需轉換]
- [ ] 檔案在應用程式內被重新命名後，清單中的舊路徑不會自動更新，需確認後決定是否要改，見最後一章。（來源：MainWindow.RenameDocument 未呼叫 _recent）[通用]

## 14. 工作階段還原

- [ ] 離開程式時，保存群組、窗格版面與分隔比例、分頁（只限有檔案路徑者）、各檢視的游標與捲動位置、作用中分頁、設定與最近清單。（來源：MainWindow.CreateSessionState、CaptureWorkspace；SPEC §5）[通用]
- [ ] 重新啟動後，群組、巢狀窗格版面、分頁（依保存的順序）、作用中分頁，都會還原。（來源：MainWindow.LoadWorkspaceAsync、RestoreViews、BuildLayoutFromState；SPEC §5；PLAN-markdown-editor Task 7）[通用]
- [ ] 還原時，各分頁的游標位置與捲動位置也會恢復；超出文字長度的游標位置會被夾到文件範圍內。（來源：MainWindow.RestoreViews；EditorPaneControl.RestoreViewPosition）[通用]
- [ ] 同一檔案有兩個檢視時，還原後會重建兩個檢視。（來源：MainWindow.RestoreViews `newView = !pathCounts.Add(canonical)`）[通用]
- [ ] 還原時，已不存在的檔案會被略過，其他分頁照常還原；不會因為缺檔而啟動失敗，也不跳出提示。（來源：MainWindow.RestoreViews `File.Exists` 檢查；SPEC §5 要求「非阻塞提示」，見最後一章）[通用]
- [ ] 尚未存檔的未命名文件不會被還原，內容會在關閉時遺失（程式只保存有路徑的分頁）。是否要保留，見最後一章。（來源：MainWindow.CreateSessionState 的 `FilePath is not null` 篩選）[通用]
- [ ] 閱讀器（預覽）視窗不會在重新啟動後自動開啟。（來源：SPEC §5；PLAN-markdown-editor Task 7；CreateSessionState 不含閱讀器旗標）[通用]
- [ ] 重新啟動後，主題與語言維持關閉前的設定。（來源：SPEC §5；AppSettings 持久化）[通用]
- [ ] 作用中分頁還原為關閉前的那一個（若該分頁還在）。（來源：MainWindow.RestoreViews `ActiveViewId` 對應）[通用]
- [ ] 視窗大小與位置不會被保存，每次啟動都是預設的 1240×760（最小 760×500）。（來源：MainWindow.xaml 預設尺寸；原始碼中沒有保存視窗邊界的程式碼；推斷，見最後一章）[需轉換]

## 15. 拖放與外部開檔

- [ ] 從 Finder（檔案總管）把一個或多個檔案拖進視窗的任何位置，檔案加入目前群組，並在目前窗格開啟各自成為分頁。（來源：MainWindow.Window_PreviewDrop；CL 0.0.2）[需轉換]
- [ ] 拖入的若是資料夾或不存在的路徑，會被略過，不會造成錯誤或崩潰。（來源：MainWindow.Window_PreviewDrop `File.Exists`；CL 0.0.2 驗收）[通用]
- [ ] 拖入的內容若不包含檔案（例如純文字），視窗不攔截，交給編輯區或其他元件處理。（來源：MainWindow.Window_PreviewDragOver 的註解；CL 1.1.5 修正拖放事件攔截）[通用]
- [ ] 拖入已經開著的檔案，切換到既有分頁，不建立重複分頁。（來源：DocumentManager.OpenView）[通用]
- [ ] 以檔案關聯開啟檔案（Finder 的「打開方式」或命令列參數）時，工作區還原完成後才開啟該檔案；多個檔案都會開啟。（來源：MainWindow.OpenStartupFiles；App.xaml.cs；CL 0.0.2）[需轉換]
- [ ] 程式已在執行時再開啟檔案（第二次啟動）：不另開視窗，既有視窗把檔案開在分頁中，並帶到前景。（來源：App.xaml.cs 單一執行個體（Mutex + Named Pipe）；MainWindow.OpenExternalPaths；CL 1.1.5）[需轉換]
- [ ] 第二次啟動且沒有檔案參數時，既有視窗仍會被還原（若最小化）並拉到前景，不只是工作列圖示閃爍。（來源：MainWindow.BringToFront；WindowActivation.RestoreAndRaise；CL 1.2.0）[Windows 專屬]
- [ ] 拖放或開檔時，路徑含空白、重複、資料夾、不存在等情況，都不會造成崩潰。（來源：CL 0.0.2 驗收；MainWindow.OpenExternalPaths `Distinct` 與 `File.Exists`）[通用]
- [ ] 從群組或分頁拖曳到側邊欄或分頁列的內部拖曳，不會被當成外部檔案拖放。（來源：MainWindow.Window_PreviewDragOver；CL 1.1.5）[通用]
- [ ] `.gss` 快照檔在檔案總管中顯示獨立圖示（Windows 登錄檔註冊）；雙擊不會啟動程式。Mac 的檔案類型註冊方式需決定，見最後一章。（來源：SnapshotFileAssociation.EnsureRegistered；CL 1.1.5）[需轉換]

## 16. 外部變更偵測

- [ ] 每個已開啟的文件，其所在目錄被監看（只看該檔名的修改、建立、更名事件）；偵測到事件後，等約 500 ms 再檢查。（來源：MainWindow.ConfigureWatcher、ScheduleExternalCheck；SPEC §5）[需轉換]
- [ ] 檢查時比對磁碟上檔案的「長度 + 最後寫入時間」與本程式最後一次存檔（或載入）時的值；兩者相同（包括本程式自己的存檔）就不提示。（來源：MainWindow.CheckExternalChange；FileFingerprint）[通用]
- [ ] 偵測到外部修改時，跳出「檔案已在外部變更」，說明「目前檔案已被其他程式修改。要重新載入、比較，還是保留本機內容？」，按鈕為「重新載入」「比較」「保留本機內容」。（來源：MainWindow.CheckExternalChange；LocalizationService dialog.externalChange*）[通用]
- [ ] 按「重新載入」：以磁碟上的內容取代分頁緩衝，分頁回到未修改狀態。現行程式碼沒有在有未儲存變更時先警告，與規格不同，見最後一章。（來源：MainWindow.ReloadDocument）[通用]
- [ ] 按「比較」：開啟差異視窗，左側為目前緩衝、右側為磁碟上的版本；不改變任何內容。（來源：MainWindow.CompareExternalDocument）[通用]
- [ ] 按「保留本機內容」或直接關閉對話框（Esc）：分頁內容不變，該外部版本不會再次提示。（來源：MainWindow.CheckExternalChange `_knownExternalFingerprints`）[通用]
- [ ] 下次存檔會把本機內容寫回磁碟、覆蓋外部版本；存檔後不會再次詢問。與規格「下次存檔會再次提示」不同，見最後一章。（來源：MainWindow.SaveDocumentAsync；SPEC §5）[通用]
- [ ] 檔案被改名或刪除時，檢查流程發現檔案不存在就不提示。（來源：MainWindow.CheckExternalChange `File.Exists` 檢查）[通用]
- [ ] 關閉分頁後，該文件的監看與計時器會釋放，之後不會再對已關閉的分頁跳出外部變更提示。（來源：MainWindow.DetachDocumentIfUnused；REVIEW #1）[通用]

## 17. 錯誤處理與資料安全

- [ ] 所有寫入（文件、設定、群組、工作階段、最近清單、快照）都採「暫存檔 → 取代」，寫入中途失敗不會留下半寫入的正式檔。（來源：AtomicFileWriter；FilePersistenceService；SPEC §6）[需轉換]
- [ ] 文件存檔失敗（唯讀、權限不足、磁碟錯誤）時，內容保留在編輯區，未儲存標記保留，程式不崩潰。（來源：DocumentManager.SaveAsync catch；Program.cs DocumentManagerVerification；SPEC §6）[通用]
- [ ] 存檔失敗訊息格式為「儲存失敗：（原因）」，對話框標題為「儲存檔案失敗」。（來源：DocumentManager.SaveAsync；LocalizationService error.save）[通用]
- [ ] groups.json、session.json、recent.json 解析失敗時，會把損壞檔改名為 `<原檔名>.corrupt-yyyyMMddHHmmss.json` 保留給診斷，改以預設值啟動。（來源：JsonStateStore.LoadAsync；Program.cs PersistenceVerification）[通用]
- [ ] settings.json 在啟動時損壞：程式以預設值啟動，不會崩潰。但啟動路徑（LoadSettingsForStartup）不保留損壞副本，之後的儲存會覆蓋該檔，見最後一章。（來源：JsonStateStore.LoadSettingsForStartup；Program.cs 驗證的是 LoadSettingsAsync 路徑）[通用]
- [ ] 快照檔損壞時，讀取顯示「快照檔案格式損壞。」，原檔不被覆寫，不影響文件本身。（來源：SnapshotSidecarStore.ReadSidecarAsync）[通用]
- [ ] 自動快照與計時器背景快照發生 IO 或權限錯誤時，靜默略過，不中斷編輯。（來源：MainWindow.SnapshotTimer_Tick catch）[通用]
- [ ] 工作階段或設定寫入失敗（IO、權限）時，程式繼續執行，不跳出例外。（來源：MainWindow.PersistWorkspaceAsync catch）[通用]
- [ ] 未處理的 UI 例外會寫入記錄檔（Windows 為 `%LocalAppData%\Glystrata\errors.log`），並顯示錯誤對話框，程式不直接結束。Mac 的記錄位置需決定。（來源：App.xaml.cs App_DispatcherUnhandledException、LogException；CL 0.0.1）[需轉換]
- [ ] WPF 資源配額不足（Win32 錯誤 1816）時，記錄後以非致命提示處理，程式不終止，提示說明目前編輯內容仍保留。（來源：App.xaml.cs；CL 0.0.1、PLAN-mvp-polish）[Windows 專屬]
- [ ] 刪除群組、移出分頁、關閉分頁、刪除單筆快照，都不會刪除原始文件；只有明確的「刪除整個快照檔案」會移除 `.gss`。（來源：SPEC §6）[通用]
- [ ] 清空整份文件內容後，仍會在自動或手動快照時建立快照（不因內容為空而跳過）。（來源：SnapshotService.CreateAsync；REVIEW #2）[通用]
- [ ] 不收集遙測、不登入、不上傳文件或設定。（來源：README「授權」；SPEC §6）[通用]
- [ ] 設定或快照檔中的未知欄位會被忽略，不阻止程式啟動；`schemaVersion` 高於支援值時，匯入會拒絕（見第 11 章）。（來源：SPEC §6；SettingsWindow.ImportSettings）[通用]
- [ ] 驗證訊息與對話框文字都走在地化，不會出現寫死的英文（例如未命名、數字錯誤訊息）。（來源：REVIEW #10；CL 0.1.0 修正驗證訊息的在地化）[通用]

## 18. 快速鍵

- [ ] 快捷鍵的選單文字與實際行為由同一張命令表產生，不會出現選單寫一個鍵、程式接另一個鍵的情況。（來源：Input/AppCommand.cs；ShortcutService；CL 1.2.0）[通用]
- [ ] 命令表中有重複的快捷鍵時，記錄衝突，後者失效；驗證程式會檢查衝突數為 0。（來源：ShortcutService.Conflicts；Program.cs ShortcutVerification）[通用]
- [ ] 只有帶修飾鍵（Ctrl、Alt、Shift）的組合會被視窗攔截；單鍵不攔截。（來源：MainWindow.Window_PreviewKeyDown `Keyboard.Modifiers == None` 直接返回）[通用]
- [ ] 中文輸入法組字中的按鍵（`Key.ImeProcessed`）不攔截，交給編輯器。（來源：MainWindow.Window_PreviewKeyDown；CL 0.1.0、1.1.0）[需轉換]
- [ ] Ctrl+N 新增未命名文件。（來源：AppCommands FileNew）[需轉換]
- [ ] Ctrl+O 開啟檔案。（來源：AppCommands FileOpen）[需轉換]
- [ ] Ctrl+S 儲存（未命名則開另存新檔）。（來源：AppCommands FileSave）[需轉換]
- [ ] Ctrl+Shift+S 另存新檔。（來源：AppCommands FileSaveAs）[需轉換]
- [ ] Ctrl+Shift+H 開啟快照紀錄。（來源：AppCommands FileSnapshotHistory；CL 1.2.0）[需轉換]
- [ ] Ctrl+Alt+S 立即建立快照。（來源：AppCommands FileCreateSnapshot；CL 1.2.0）[需轉換]
- [ ] Ctrl+, 開啟偏好設定。（來源：AppCommands SettingsOpen；CL 1.2.0）[需轉換]
- [ ] Ctrl+Z 復原、Ctrl+Y 重做：由編輯器處理，選單只顯示，視窗不攔截。（來源：AppCommands EditUndo、EditRedo GestureOnly）[需轉換]
- [ ] Ctrl+F 開啟尋找與取代；Ctrl+H 開啟同一視窗並聚焦取代欄（Ctrl+H 不在選單中）。（來源：AppCommands EditFindReplace、EditReplace）[需轉換]
- [ ] Ctrl+\ 左右分割；Ctrl+Alt+\ 上下分割。（來源：AppCommands ViewSplitHorizontal、ViewSplitVertical；CL 1.2.0）[需轉換]
- [ ] Ctrl+Shift+W 關閉目前窗格；Ctrl+W 關閉目前分頁（Ctrl+W 不在選單中）。（來源：AppCommands ViewClosePane、ViewCloseTab）[需轉換]
- [ ] Ctrl+Alt+1 還原單一窗格；Ctrl+Alt+N 在新檢視開啟。（來源：AppCommands ViewResetLayout、ViewNewView；CL 1.2.0）[需轉換]
- [ ] Ctrl+Shift+R 開關目前分頁的閱讀器（不在選單中）。（來源：AppCommands ViewToggleReader；CL 1.2.0）[需轉換]
- [ ] Ctrl+Shift+G 新增群組。（來源：AppCommands GroupNew；CL 1.2.0）[需轉換]
- [ ] Ctrl+滾輪在編輯區與閱讀器中縮放（編輯區步進 0.1、閱讀器步進 10%）。（來源：EditorPaneControl PreviewMouseWheel；PreviewWindow.Viewer_PreviewMouseWheel）[需轉換]
- [ ] 選單的 Alt 助憶鍵：Alt+F 開「檔案」、Alt+E 開「編輯」、Alt+V 開「檢視」、Alt+G 開「群組」、Alt+H 開「說明」，可只靠鍵盤操作每個選單項目。（來源：LocalizationService menu.*；CL 1.2.0）[Windows 專屬]
- [ ] Alt+F4 關閉程式，走關閉流程（含未儲存詢問）。（來源：CL 1.2.0；系統提供）[Windows 專屬]
- [ ] 快捷鍵目前不支援自訂（1.2.0 決定只做固定預設鍵）。（來源：PLAN-v1-2-0 決策 D）[通用]
- [ ] 規劃中但未實作的快捷鍵（不要做成必備項目）：F3、Shift+F3、Ctrl+Tab、Ctrl+Shift+Tab、Ctrl+=、Ctrl+-、Ctrl+Shift+0、Ctrl+Shift+O、Ctrl+Shift+M、Ctrl+Shift+A、Markdown 格式快捷鍵（Ctrl+B 等）。（來源：PLAN-v1-2-0 第 3 項表格與「決定」；AppCommands 未列出）[通用]

## 19. 狀態列、選單與版面

- [ ] 主視窗預設 1240×760，最小 760×500。（來源：MainWindow.xaml Width、Height、MinWidth、MinHeight）[通用]
- [ ] 版面由左至右：群組側欄（預設寬 258，可用分隔線拖曳）、編輯區；上方為選單列、工具列；下方為狀態列。（來源：MainWindow.BuildShell；CL 0.0.1；SPEC §3）[通用]
- [ ] 選單列頂層項目為「檔案、編輯、檢視、群組、說明」，整組置中、寬度依文字收縮；頂層之間與工具列之間有貫通的淡色分隔線。（來源：MainWindow.BuildMenu；CL 0.0.2、CL 0.2.0）[通用]
- [ ] 「檔案」選單順序：新增、開啟…、最近開啟過的文件 ▸、儲存、另存新檔…、快照紀錄…、立即建立快照、離開、偏好設定…。（來源：MainWindow.BuildMenu）[通用]
- [ ] 「編輯」選單：復原、重做、尋找與取代。（來源：MainWindow.BuildMenu）[通用]
- [ ] 「檢視」選單：左右分割、上下分割、關閉目前窗格、還原單一窗格、在新檢視開啟。（來源：MainWindow.BuildMenu）[通用]
- [ ] 「群組」選單：新增群組、開啟檔案…。（來源：MainWindow.BuildMenu）[通用]
- [ ] 「說明」選單：Markdown 語法說明、關於 Glystrata。（來源：MainWindow.BuildMenu；LocalizationService help.*）[通用]
- [ ] 工具列按鈕由左至右：新增檔案（＋）、開啟檔案、左右分割窗格、上下分割窗格、關閉目前窗格、還原為單一窗格。工具列沒有儲存或偏好設定按鈕。（來源：MainWindow.BuildToolbar；CL 0.2.0、1.2.0）[通用]
- [ ] 沒有分頁時，編輯區中央顯示提示「按 Ctrl+N 新增檔案，或按 Ctrl+O 開啟檔案」。（來源：LocalizationService editor.empty；CL 0.2.0）[通用]
- [ ] 狀態列左側顯示目前檔案所在的資料夾（不含檔名）；過長時以省略號截斷，滑鼠停留顯示完整路徑；未命名或無分頁時為空。（來源：MainWindow.UpdateStatus；CL 0.2.0）[通用]
- [ ] 狀態列右側顯示「狀態 · 編碼 · 換行」，狀態為「未儲存變更」「已儲存」「已自動儲存」或快照訊息。（來源：MainWindow.UpdateStatus）[通用]
- [ ] 狀態列顯示目前游標位置「Ln 行, Col 欄」，以目前作用中檢視計算。（來源：MainWindow.UpdateStatus）[通用]
- [ ] 狀態列字元統計顯示「字元 N」；有選取時追加「· 選取 M」。計數以 Unicode 文字元素為單位（組合字與 emoji 組合算 1 個）。（來源：TextMetrics.CountCharacters；MainWindow.UpdateStatus；CL 0.0.2）[通用]
- [ ] 字元統計的計數方式依設定（含空白與換行、不含空白、不含換行）計算；滑鼠停留顯示目前模式。（來源：TextMetrics；SettingsWindow 一般；CL 1.2.0）[通用]
- [ ] 狀態列的字元統計只顯示結果，不可點擊（計數方式改到偏好設定）。（來源：CL 1.2.0）[通用]
- [ ] 狀態列右側的縮放滑桿範圍 50%～200%，每 10% 一格並吸附；滑鼠停留顯示「編輯器縮放（點兩下百分比重設為 100%）」；雙擊百分比回到 100%。（來源：MainWindow.BuildStatusBar；LocalizationService status.zoom；CL 0.1.0）[通用]
- [ ] 編輯器縮放不改變字級設定，重新啟動後回到 100%。（來源：CL 0.1.0；MainWindow `_editorZoom` 初始值）[通用]

## 20. 視窗、標題列與系統整合

- [ ] 所有視窗使用跟隨主題的自訂標題列，並提供最小化、最大化、關閉按鈕。Mac 使用原生標題列，此項不需要照做，但標題列需跟隨亮暗主題。（來源：CustomTitleBar.Attach；CL 0.1.0、1.2.0）[Windows 專屬]
- [ ] 最大化按鈕支援 Windows 11 Snap Layouts；拖曳標題列的 Aero Snap 分割視窗。（來源：CL 0.1.0、SPEC §3）[Windows 專屬]
- [ ] 自訂標題列在最大化時邊緣不被裁切。（來源：CL 0.1.0）[Windows 專屬]
- [ ] 所有對話框（關於、刪除群組、刪除快照、外部變更、未存檔提醒、開檔與存檔錯誤、偏好設定驗證訊息）都是跟隨主題的自訂對話框；程式啟動失敗的訊息使用系統對話框。（來源：CL 1.1.0）[需轉換]
- [ ] 對話框按 Esc 關閉、按 Enter 確定（多行文字框中的 Enter 不觸發確定）。（來源：DialogKeys.AttachEscapeToClose、AttachConfirmCancel；CL 1.1.5）[通用]
- [ ] 注音等輸入法組字時，候選字視窗在編輯區的游標處顯示，不另外在螢幕左下角畫一個組字視窗。（來源：CL 0.1.0、1.1.0；Editing/ImeCompositionLayer.cs、ImeHwndHook.cs 為 Windows 實作）[需轉換]
- [ ] 發布包內附授權檔與第三方聲明（LICENSE、THIRD-PARTY-NOTICES、Twemoji 的 CC-BY 4.0 授權全文）。（來源：README「授權」；CL 1.0.0）[通用]
- [ ] 版本號為 1.2.3，「關於」中顯示的版本與發布檔版本一致。（來源：CL 1.2.3；MainWindow.ShowAbout）[通用]
- [ ] 同一個程式包不需要另外安裝 .NET 執行環境即可執行（Windows 的 self-contained 發布）；Mac 版以原生 Swift 實作，此項不適用。（來源：README「下載版本」；SPEC §8）[Windows 專屬]

## 21. 既有自動驗證涵蓋範圍

來源：`tests/Glystrata.Verification/Program.cs`。`Main` 共執行下列 13 組檢查（下表依功能分組，與 `Main` 的執行順序不同），任一失敗即回傳非零。之後移植成 Swift 測試時，每組都要對應一個 XCTest 或 Swift Testing 測試。

| # | 方法 | 測什麼 | 對應 Swift 測試要點 |
|---|---|---|---|
| 1 | `FileCodecVerification` | 三組編碼／換行組合（UTF-8＋LF、UTF-8 BOM＋CRLF、UTF-16 LE＋CR）的 round-trip：寫入後讀回文字相同、編碼偵測正確、換行偵測正確。測試文字為 `標題\n第二行\n`。 | 每組寫入後讀回三項一致；另補 UTF-16 BE 拒絕、BOM 保留、CR 偵測。 |
| 2 | `DocumentManagerVerification` | (a) 同一路徑開兩個檢視，文件物件相同（`ReferenceEquals`），但各自的垂直捲動位置獨立；(b) 同一文件 4 次重疊 `SaveAsync` 全部成功，檔案內容正確，`IsModified` 為 false；(c) 寫到不存在的目錄，回傳失敗、`IsModified` 保持 true、`LastError` 有值。 | 三個子案例各一個測試；重疊儲存要用 Task 並行呼叫。 |
| 3 | `PaneLayoutVerification` | 三個窗格的巢狀分割（水平 split 內含垂直 split）：窗格數為 3；移除中間窗格後 split 被收合為 2 個窗格；`ContainsPane` 正確；`CollapseToSinglePane` 保留第一個窗格的 ID。 | 純資料結構測試，不需要 UI。 |
| 4 | `GroupVerification` | 群組項目去重（同一路徑只留一筆，檔案與資料夾各一筆）；同檔案加入第二群組；`MovePath` 只改關聯、目標不重複、實體檔案仍存在；群組色碼設定、清除、空白字串視為清除；群組色透過 `GroupsState` round-trip 保存；`RemapColors` 大小寫不敏感、互換兩色不連鎖、被刪色票上的群組保留原色、色盤外的顏色不動、無色群組不被指派、無變更時回傳 false。 | 群組模型與 JSON round-trip 各一測試；RemapColors 的五個邊界要逐一對應。 |
| 5 | `MarkdownTypingAssistantVerification` | 七種 Enter 行為：頂層 `- a` 續行、縮排 `  - a` 同層續行、Tab 縮排續行、`  3. a` 編號遞增為 `4.`、`  - [x] a` 續為 `- [ ]`、項目中間按 Enter 後半段變成新項目、非清單的縮排行只沿用縮排。 | 需要 AppKit 的文字元件（`NSTextView`）或抽成純字串函式測試；以純函式較穩。 |
| 6 | `PersistenceVerification` | (a) 設定存讀往返（語言、快照間隔 8、上限 12、計數模式、快捷列）；(b) 損壞的 settings.json 回退預設，並留下 `settings.json.corrupt-*.json`；(c) 舊檔 `theme: 1` 讀為暗色、`theme: 0` 讀為亮色、缺欄位讀為跟隨系統、`theme: 99` 回退跟隨系統。 | 主題數值相容性是硬性契約，Swift 端 enum raw value 必須是 0/1/2。 |
| 7 | `MarkdownVerification` | Markdig 解析含 front matter、任務清單、表格、`<script>` 的內容：有 `<h1>`、有 `<table>`、沒有 `<script`、沒有 front matter 內文。 | Swift 端需要等價的 Markdown 解析器（例如 swift-markdown 或 cmark-gfm），要對這四項做等價斷言。 |
| 8 | `SnapshotVerification` | (a) 上限 2 時建立 3 份快照，只剩 2 份；sidecar 存在且預設不隱藏；(b) 手動設隱藏後下次寫入會恢復可見；(c) sidecar 含 `notice` 欄位，且在 `snapshots` 之前；(d) 新快照註解預設空字串；`UpdateNoteAsync` 寫入後讀回一致、對不存在的 id 回傳 false；(e) 單筆刪除後剩 1 筆；整檔刪除後 sidecar 不存在；(f) 隱藏模式寫入的 sidecar 是隱藏的，`ApplyVisibility(false)` 可還原；(g) 舊版 `.<檔名>.mdeditor-snapshots.json` 被辨識並遷移到新檔名，註解為空字串，舊檔被移除。 | 隱藏屬性與遷移在 Mac 上的對應方式需先決定（見第 23 章）；(g) 只有在 Mac 版決定支援舊檔名時才需要。 |
| 9 | `DiffVerification` | `a\nb\n` 對 `a\nc\n`：有 Removed `b`、Added `c`。 | 差異演算法的最小測試。 |
| 10 | `TextMetricsVerification` | 文字 `Á B\n`（A 加組合重音符號）：含空白計數 4、不含空白 2、不含換行 3。 | 驗證以 Unicode 文字元素計數（組合字算 1）；Swift 的 `Character` 也是這個單位，但需確認與 `StringInfo` 結果一致。 |
| 11 | `MarkdownFormattingVerification` | 粗體包裹（選取位置與長度跟著變）、H2 標題改寫、無序清單、有序清單（全部改成 `1.`）、連結（`[文件](https://example.com)`）、程式碼區塊包裹。 | 純字串函式，可直接移植。 |
| 12 | `ShortcutVerification` | 命令表無衝突；每個命令 ID 在表中恰好出現一次；顯示文字正確（`Ctrl+N`、`Ctrl+Shift+S`、`Ctrl+Alt+S`、`Ctrl+,`、`Ctrl+\`、`Ctrl+Alt+\`、`Ctrl+Alt+1`）；無快捷鍵的命令回傳 null；復原顯示 `Ctrl+Z` 且不被視窗攔截；人為製造的衝突會被記錄；每個有選單文字的命令在中英文字典都有值。 | Mac 版的快捷鍵字串要改為 ⌘ 或維持 Ctrl，依決策而定（見第 23 章）。 |
| 13 | `RecentFilesVerification` | 最近清單順序（新的在前）；重複加入移到最前；同一檔案不同寫法（含 `..`）不重複，且保留呼叫端傳入的寫法；超過 10 筆截斷並淘汰最舊；移除與清除；`LoadFrom` 容忍 `files: null`、空路徑、null 元素、重複、超過上限；`AddIfMissing` 排在最後且不重複；`recent.json` 往返保留筆數與順序。 | 路徑規範化比較要用 Mac 的檔案系統規則（大小寫、Unicode 正規化）。 |

**未被自動驗證涵蓋、只能手動或 UI 測試的行為**（Windows 版無自動測試，Mac 版需要另外設計）：

- 外部變更偵測的三條路徑與對話框。
- 自動存檔 1 秒防抖、快照 30 秒計時、工作階段 700 ms 防抖。
- 工作階段還原（窗格、分頁、游標與捲動位置）。
- 拖放開檔、分頁排序、分頁拖到群組、群組排序。
- 尋找與取代的導覽與全部取代。
- 快照註解的就地編輯、回復的三種結果、刪除確認。
- 設定匯出與匯入（含 `kind` 判斷與 `schemaVersion` 拒絕）。
- 主題跟隨系統即時切換、語言即時切換。
- 閱讀器縮放、連結開啟、本機圖片範圍限制。
- 重新命名檔案（含快照檔與群組項目同步搬移）。
- IME 組字與快捷鍵的互動。

## 22. 資料檔格式

### 22-A 總覽

| 檔案 | 位置（Windows 版） | 格式與 schema | 寫入時機 | 讀取失敗時 |
|---|---|---|---|---|
| `settings.json` | `%LocalAppData%\Glystrata\` | JSON，`schemaVersion` 1 | 偏好設定按「套用」；工作階段儲存時一併寫入 | 啟動路徑回傳預設值（不保留損壞副本，見第 23 章）；其他路徑改名為 `.corrupt-*.json` 後回傳預設值 |
| `groups.json` | 同上 | JSON，`schemaVersion` 1 | 工作階段儲存（群組或分頁變動後約 700 ms）與關閉時 | 改名為 `groups.json.corrupt-yyyyMMddHHmmss.json`，回傳空群組 |
| `session.json` | 同上 | JSON，`schemaVersion` 1 | 同上 | 改名為 `.corrupt-*.json`，回傳空工作階段 |
| `recent.json` | 同上 | JSON，`schemaVersion` 1 | 同上 | 改名為 `.corrupt-*.json`，回傳空清單 |
| `<檔名>.gss`（快照） | 原始文件同目錄 | JSON，`schemaVersion` 1 | 建立、刪除單筆、刪除整檔、改註解、搬移或改名時 | 顯示「快照檔案格式損壞。」，不改寫原檔 |
| `*.gss`（設定匯出檔） | 使用者選擇的位置 | 與 `settings.json` 相同欄位，外加 `kind` | 使用者按「匯出設定檔…」 | 匯入時顯示錯誤，不生效 |
| `*.tmp` | 與目標檔同目錄 | 暫存檔，寫完後取代正式檔，失敗時刪除 | 每次寫入 | 殘留的暫存檔不被讀取 |

### 22-B 共通編碼規則（JSON 讀寫）

- 寫入：UTF-8、無 BOM（`AtomicFileWriter` 使用 `UTF8Encoding(false)`）；縮排 2 個空白（`System.Text.Json` 預設）；屬性名稱為 camelCase。
- 讀取：屬性名稱大小寫不敏感；忽略註解；允許尾端逗號（`JsonStateStore`、設定匯入都是如此）。
- 列舉（enum）一律以整數寫入，沒有使用字串列舉轉換器。下方所有列舉都以整數表示。
- 非 ASCII 字元：`settings.json`、`groups.json`、`session.json`、`recent.json` 使用預設編碼器，中文會被跳脫為 `\uXXXX`；`.gss` 使用 `UnsafeRelaxedJsonEscaping`，中文原樣輸出。兩種寫法都是合法 JSON，Mac 端讀取兩者都必須正確（依程式碼推斷）。
- 日期時間：`DateTime` 以 `System.Text.Json` 的 ISO 8601 格式寫出，結尾為 `Z`（UTC）。小數秒的位數由序列化器決定，讀取端需容許 0～7 位小數（依程式碼推斷，需以實際檔案驗證）。
- GUID：小寫、含連字號，例如 `3f1e2c7a-...`。
- 路徑：寫入的是完整路徑（`Path.GetFullPath` 結果）。Windows 為反斜線與磁碟機代號，Mac 版的檔案不能直接沿用 Windows 路徑，跨平台匯入群組與工作階段時需要另外處理（見第 23 章）。

### 22-C 列舉數值對照

| 列舉 | 值 |
|---|---|
| `AppLanguage` | `0` = TraditionalChinese（繁體中文）、`1` = English |
| `ThemePreference` | `0` = Light、`1` = Dark、`2` = System（跟隨系統）。數值為契約，不可改動 |
| `CharacterCountMode` | `0` = IncludeWhitespace、`1` = ExcludeWhitespace、`2` = ExcludeLineBreaks |
| `FileEncodingKind` | `0` = Utf8、`1` = Utf8WithBom、`2` = Utf16LittleEndian |
| `LineEndingKind` | `0` = CrLf、`1` = Lf、`2` = Cr |
| `SplitOrientation` | `0` = Horizontal（左右分割，子節點並排）、`1` = Vertical（上下分割） |
| `GroupItemKind` | `0` = File、`1` = Folder（UI 已不顯示資料夾項目，但舊檔可能仍含 `1`，讀取時需容忍） |

### 22-D `settings.json`

```json
{
  "schemaVersion": 1,
  "language": 0,
  "theme": 2,
  "lightEditorPalette": { "colors": { "plain": "#1F2328", "heading": "#0969DA", "emphasis": "#8250DF", "link": "#0969DA", "list": "#57606A", "quote": "#6E7781", "code": "#953800", "yaml-key": "#0550AE", "yaml-value": "#0A3069", "yaml-comment": "#6E7781", "front-matter": "#8250DF" } },
  "darkEditorPalette": { "colors": { "plain": "#E6EDF3", "heading": "#79C0FF", "emphasis": "#D2A8FF", "link": "#79C0FF", "list": "#8B949E", "quote": "#8B949E", "code": "#FFA657", "yaml-key": "#A5D6FF", "yaml-value": "#C9D1D9", "yaml-comment": "#8B949E", "front-matter": "#D2A8FF" } },
  "lightReaderPalette": { "colors": { "text": "#24292F", "heading": "#24292F", "link": "#0969DA", "quote": "#68707C", "code": "#24292F" } },
  "darkReaderPalette": { "colors": { "text": "#E6EAF0", "heading": "#E6EAF0", "link": "#79C0FF", "quote": "#A6ADB8", "code": "#E6EAF0" } },
  "previewTypography": { "h1Size": 32, "h2Size": 26, "h3Size": 22, "h4Size": 18, "h5Size": 16, "h6Size": 14, "lineSpacing": 1.35, "paragraphSpacing": 10, "headingSpacing": 14 },
  "snapshotIntervalMinutes": 5,
  "maxSnapshotsPerFile": 20,
  "characterCountMode": 0,
  "showFormattingToolbar": false,
  "hideSnapshotFiles": false,
  "groupColors": ["#3B6FF5", "#2DA44E", "#D97706", "#8250DF", "#CF222E"],
  "groupSelectionTint": 0.14
}
```

- 色碼格式：`#RRGGBB`，大寫十六進位（設定對話框輸出的格式）。讀取時若是小寫或其他格式，程式以 `BrushConverter` 解析，Mac 端要同樣容忍大小寫。
- 顏色字典的鍵名是固定字串（含連字號，例如 `yaml-key`），不做 camelCase 轉換。
- 正規化（`Normalize`）規則：`snapshotIntervalMinutes` 夾到 1～120；`maxSnapshotsPerFile` 夾到 1～200；`groupSelectionTint` 夾到 0～0.5；`characterCountMode`、`theme` 超出列舉範圍時回到預設（`0` 與 `2`）；`groupColors` 為空時回到預設五色；缺少的調色盤回到預設值。
- 預設值與程式碼一致；`theme` 缺欄位時為 `2`（跟隨系統）。
- `schemaVersion` 目前只用於匯入檢查（大於 1 拒絕），讀取本檔時沒有遷移邏輯。

### 22-E `groups.json`

```json
{
  "schemaVersion": 1,
  "groups": [
    {
      "id": "6f0b9c2e-1d7a-4b8e-9a3f-2c5d7e8f9a01",
      "name": "研究",
      "color": "#2DA44E",
      "items": [
        { "id": "0a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d", "path": "C:\\Users\\Cloud\\Notes\\筆記.md", "kind": 0 }
      ]
    }
  ]
}
```

- `color` 為 `null` 或色碼字串；色碼不存色票編號。
- `items[].path` 為完整路徑；同一群組內路徑不重複（以規範化路徑比對，大小寫不敏感）。
- `items[].kind`：`0` 為檔案。`1` 為資料夾，UI 已不使用，但讀取時要能容忍。
- 群組順序即陣列順序。群組名稱去除頭尾空白後儲存；空名稱讀取時會被改為「未命名群組」。

### 22-F `session.json`

```json
{
  "schemaVersion": 1,
  "rootNodeId": "9d8c7b6a-0000-4000-8000-000000000001",
  "nodes": [
    { "id": "9d8c7b6a-0000-4000-8000-000000000001", "isSplit": true, "orientation": 0, "ratio": 0.5, "firstNodeId": "9d8c7b6a-0000-4000-8000-000000000002", "secondNodeId": "9d8c7b6a-0000-4000-8000-000000000003" },
    { "id": "9d8c7b6a-0000-4000-8000-000000000002", "isSplit": false, "orientation": 0, "ratio": 0.5, "firstNodeId": null, "secondNodeId": null },
    { "id": "9d8c7b6a-0000-4000-8000-000000000003", "isSplit": false, "orientation": 0, "ratio": 0.5, "firstNodeId": null, "secondNodeId": null }
  ],
  "views": [
    { "viewId": "11111111-2222-4333-8444-555555555555", "path": "C:\\Users\\Cloud\\Notes\\a.md", "sourceGroupId": "6f0b9c2e-1d7a-4b8e-9a3f-2c5d7e8f9a01", "paneId": "9d8c7b6a-0000-4000-8000-000000000002", "caretOffset": 120, "horizontalOffset": 0, "verticalOffset": 340.5 }
  ],
  "activeViewId": "11111111-2222-4333-8444-555555555555"
}
```

- `nodes` 是把版面樹攤平的陣列。葉節點（`isSplit: false`）的 `id` 即窗格 ID（`paneId`）；分割節點的 `id` 即 split ID。
- `ratio` 為第一子節點所佔比例，還原時夾到 0.1～0.9。
- `caretOffset` 為 UTF-16 字元位移（`TextDocument` 的偏移量）；`horizontalOffset`、`verticalOffset` 為 WPF 的像素單位。Mac 端對應的捲動值需要換算，兩邊不能直接互通，但讀取時不應崩潰。
- `views` 只含有檔案路徑的分頁；未命名文件不會寫入。
- `views` 的陣列順序是保存時 `DocumentManager` 的檢視順序，不是 `TabOrder`（見第 23 章）。
- `activeViewId` 為 `null` 或既有 `viewId`；對應不到時還原為第一個分頁。
- 還原時 `rootNodeId` 為空、`nodes` 為空，或子節點缺失，整個版面回到單一窗格。

### 22-G `recent.json`

```json
{
  "schemaVersion": 1,
  "files": [
    { "path": "C:\\Users\\Cloud\\Notes\\a.md", "lastOpenedUtc": "2026-10-13T06:51:18.1234567Z" }
  ]
}
```

- `files` 最多 10 筆，最新在最前。
- `path` 為開啟時傳入的字串（不一定是規範化後的路徑）；比對時才做規範化。
- 讀取時：`files` 為 `null` 時當作空清單；路徑為空或空白的項目丟棄；規範化後重複的只留第一筆；超過 10 筆截斷。

### 22-H 快照 sidecar `<檔名>.gss`

範例（以 `筆記.md` 為例，檔名為 `筆記.md.gss`）：

```json
{
  "notice": "This file is generated and managed by Glystrata (a Markdown editor). ... / 此檔案由 Glystrata 自動產生與維護，儲存同資料夾中「筆記.md」的快照（版本）歷史。AI 助理與自動化工具請勿編輯……",
  "schemaVersion": 1,
  "sourcePath": "C:\\Users\\Cloud\\Notes\\筆記.md",
  "snapshots": [
    {
      "id": "3c9d2f4e-6a1b-4c8d-9e0f-1a2b3c4d5e6f",
      "createdUtc": "2026-10-13T06:51:18Z",
      "text": "第一行\n第二行\n",
      "encoding": 0,
      "lineEnding": 1,
      "sourceFingerprint": { "length": 14, "lastWriteTimeUtc": "2026-10-13T06:50:02.5Z" },
      "note": ""
    }
  ]
}
```

欄位規格（以下為寫入端的完整規格，Mac 端讀寫都要符合）：

| 欄位 | 型別 | 說明 |
|---|---|---|
| `notice` | string | 固定中英雙語警語，文件名以 `{檔名}` 代入（`SnapshotSidecar.Notice`）。必須排在第一個屬性。讀取時忽略。 |
| `schemaVersion` | int | 目前為 `1`。 |
| `sourcePath` | string | 原始文件的完整路徑。 |
| `snapshots` | array | 快照陣列，依 `createdUtc` 由新到舊排列。 |
| `snapshots[].id` | GUID 字串 | 快照的唯一識別。 |
| `snapshots[].createdUtc` | DateTime（UTC） | 建立時間，格式見 22-B。 |
| `snapshots[].text` | string | 文件全文，**換行一律為 `\n`**（編輯區內部格式）。 |
| `snapshots[].encoding` | int | `FileEncodingKind`：`0` Utf8、`1` Utf8WithBom、`2` Utf16LittleEndian。 |
| `snapshots[].lineEnding` | int | `LineEndingKind`：`0` CrLf、`1` Lf、`2` Cr。還原時用來決定寫檔時的換行。 |
| `snapshots[].sourceFingerprint` | object 或 `null` | `{ "length": 長整數, "lastWriteTimeUtc": DateTime }`，是建立快照時原始檔案的長度與最後寫入時間。 |
| `snapshots[].note` | string | 註解，預設 `""`。單行顯示，可以為空。 |

行為規則：

- 建立快照時，若內容與最新一筆的 `text` 完全相同（序數比較），不建立。若快照清單為空且文件內容也為空，不建立。
- 建立後超過上限（預設 20，範圍 1～200）時，移除最舊的項目，保留新的。
- 清單變成空的時候，直接刪除 `.gss` 檔，不寫入空檔。
- 寫入採「暫存檔 → 取代」，並依設定套用隱藏屬性（Windows 的 Hidden 屬性）。
- 修改流程（讀取、改寫、寫入）在同一把鎖內完成（同一份 sidecar 的操作互斥）。
- 讀取時若發現舊檔名（見 22-I），會先遷移到新檔名再讀。
- `text` 內的換行是 `\n`，所以跨平台讀寫時不需要換行轉換；還原時再依 `lineEnding` 轉回。

### 22-I 舊檔名（只讀取、用於遷移）

Windows 版歷史上使用過的快照檔名，讀取時會依序檢查並遷移到 `<檔名>.gss`：

1. `<檔名>.glystrata-snapshots.json`（過渡版，非隱藏）
2. `.<檔名>.glystrata-snapshots.json`（隱藏點檔名版）
3. `.<檔名>.mdeditor-snapshots.json`（MDeditor 時期）

這三種名稱的內容格式與 22-H 相同（舊版缺少 `note` 欄位時，讀取為 `""`）。Mac 版是否需要遷移，見第 23 章。

### 22-J 設定匯出檔（`.gss`，與快照同副檔名）

- 內容與 `settings.json` 完全相同，但**最外層結尾**多一個 `"kind": "glystrata-settings"`（由程式在序列化後附加，因此位於最後一個欄位）。
- 匯入判斷順序：
  1. 不是 JSON 物件 → 「設定檔格式不正確或已損壞」。
  2. 含 `snapshots` 或 `notice` 欄位 → 「這不是 Glystrata 的設定檔…」（視為快照檔）。
  3. `kind` 不等於 `glystrata-settings` → 同上。
  4. `schemaVersion` 大於 1 → 「較新版本…無法讀取」。
  5. 反序列化失敗 → 「格式不正確或已損壞」。
- 判斷依據是欄位內容，不依賴欄位的位置。Mac 端匯入時也應採相同的判斷順序，避免把快照檔誤判為設定檔。

### 22-K 寫入暫存與損壞副本的命名

- 通用寫入（設定、群組、工作階段、最近清單、快照）：暫存檔為 `<完整路徑>.<32 位十六進位>.tmp`。
- 文件存檔：暫存檔為 `<檔案完整路徑>.glystrata-write-<32 位十六進位>.tmp`。
- 損壞副本：`<原檔名>.corrupt-yyyyMMddHHmmss.json`（時間為 UTC，格式為 `yyyyMMddHHmmss`）。
- 這些暫存與副本只是檔案系統層的細節，Mac 端可以採相同命名，也可以改用系統提供的原子寫入 API；但損壞副本的保留行為需要保留。

## 23. 矛盾與不確定之處

依規定，文件之間的矛盾不自行裁決。下列條目的「本清單採用」是整理時的預設讀法，正式定案前需要人確認。

### 23-A 文件之間的矛盾

| # | 項目 | 文件 A | 文件 B／程式碼 | 本清單採用 |
|---|---|---|---|---|
| A1 | 快照檔命名 | SPEC §5：`.<檔名>.glystrata-snapshots.json` | CL 1.1.5、README、`SnapshotSidecarStore`：`<檔名>.gss` | 採 `<檔名>.gss`。偏好設定的說明文字（LocalizationService `settings.hideSnapshotFiles.help`）仍寫舊名稱，屬於過期文字，Mac 版不得照抄。 |
| A2 | 群組色票與既有群組的關係 | `settings.groupColors.help`：修改或刪除色票「不會更動已經指定過顏色的群組」 | CL 1.2.1 與 `ApplySettings` → `RemapColors`：修改色票後，同位置的群組會跟著換色；被刪除色票上的群組保留原色 | 採 CL 1.2.1 與程式碼。說明文字與實際行為不符，Mac 版的說明要改寫。 |
| A3 | 群組選取底色的混合比例 | PLAN-v1-2-0 第 8 項：亮色 12%、暗色 24%（建議），且「混合比例做成偏好設定的滑桿」 | `AppSettings.GroupSelectionTint` 預設 `0.14`，單一數值，上限 0.5 | 採程式碼（單一數值、預設 14%）。亮暗分開的建議未被採用。 |
| A4 | 偏好設定的即時性 | SPEC §3「設定變更即時套用」；CL 0.0.1「語言切換即時生效」 | 設定對話框內切換語言下拉只改 `_working`；只有按「套用」才影響主視窗 | 採程式碼（按套用才生效）。是否需要即時預覽，見第 23-C。 |
| A5 | 全新安裝的預設語言 | SPEC §1：非繁中、英文的 Windows 語言時，預設繁體中文（暗示依系統語言決定） | `AppSettings.Language` 預設為繁體中文，程式沒有讀取系統語言 | 採程式碼（一律預設繁體中文）。 |
| A6 | 外部變更：重新載入的警告 | SPEC §5：「重新載入」明確警告將丟棄目前 buffer | `ReloadDocument` 直接覆蓋緩衝，沒有警告 | 採程式碼（無警告）。Mac 版要決定是否補上警告；建議補上，因為規格有明寫。 |
| A7 | 外部變更：保留後的再次提示 | SPEC §5：保留本機內容後，下次儲存會再次提示即將覆蓋外部版本 | 程式在下次儲存時直接覆寫，沒有再次提示；同一外部版本也不會重複提示 | 採程式碼（不再提示）。 |
| A8 | 分頁拖到其他窗格 | SPEC §3「TAB 支援拖曳排序、拖曳到其他窗格」 | SPEC 0.2.0 註記「TAB 尚不支援拖曳排序或拖到其他窗格」；CL 1.1.5 只提到排序與拖到群組；`HandleTabDrop` 只處理同窗格 | 採程式碼（只支援同窗格排序）。 |
| A9 | Markdown 內的 yaml 程式碼區塊分色 | SPEC §1、PLAN-markdown-editor Task 3：` ```yaml ` 區塊要分色 | `SyntaxHighlightingService` 只對整檔 YAML 與 front matter 做 YAML 分色，沒有處理 fenced yaml | 規格要求但程式未實作。Mac 版需決定要不要做。 |
| A10 | 工作階段還原時缺檔的提示 | SPEC §5 與 PLAN-markdown-editor Task 7：對不存在的檔案顯示非阻塞提示 | `RestoreViews` 直接略過，不提示 | 採程式碼（靜默略過）。 |
| A11 | 工作階段保存的內容 | SPEC §5：session 含「active TAB」，PLAN-mvp-polish 提到群組篩選狀態需保存 | `SessionState` 沒有群組篩選欄位；重新啟動後篩選是否還原，程式碼中沒有對應資料 | 採程式碼（不保存篩選）。 |
| A12 | 設定面板的分頁數 | SPEC §3 寫四個區段（語言、編輯器、閱讀器、快照） | CL 1.2.0 之後為五個分頁（另有群組顏色） | 採 CL 1.2.0 與程式碼（五個分頁）。 |
| A13 | 資料夾群組項目 | SPEC §3 要求群組可加入資料夾捷徑、展開資料夾 | CL 0.2.0 移除資料夾功能；`GroupManager` 仍保留 `Folder` 種類與 `EnumerateFolder` 程式碼，但 UI 完全不使用 | 採 CL 0.2.0（UI 不提供資料夾）。資料模型保留的 `kind: 1` 需容忍讀取。 |
| A14 | 群組缺檔標示 | SPEC §3：找不到路徑時保留群組項目並標示遺失，可重新指定或移除 | 群組內的項目不在 UI 中列出，沒有遺失標示 | 採程式碼（功能不存在）。 |
| A15 | Big5 等非 Unicode 純文字 | SPEC §1、§2：「UTF-8、UTF-8 BOM、UTF-16 LE」與「可解碼純文字檔」 | `FileCodec` 只接受 UTF-8、UTF-8 BOM、UTF-16 LE，其餘拒絕 | 採程式碼（拒絕）。繁中使用者可能遇到 Big5 檔案，是否支援需決定（見第 23-C）。 |
| A16 | settings.json 損壞時的保留 | SPEC §5：讀取失敗保留壞檔供診斷 | 啟動路徑 `LoadSettingsForStartup` 回傳預設值，不改名保留；非啟動路徑才會改名 | 採程式碼，但這與規格不符，見第 23-B。 |
| A17 | 設定匯出時 `kind` 的位置 | PLAN-v1-2-0 第 11 項：`kind` 放在檔案最前面 | 程式在序列化後附加到最後 | 採程式碼（最後）。匯入只依欄位內容判斷，位置不影響。 |
| A18 | 快照註解的輸入方式 | PLAN-v1-2-0 第 1 項原案：多行文字框、獨立編輯區 | 已定案為「就地單行編輯」（PLAN-v1-2-0 已定案段），程式碼也是單行（Enter 提交） | 採已定案與程式碼。 |
| A19 | 「已自動儲存」狀態文字 | 狀態列語意為自動存檔 | 手動儲存也顯示此文字（同一方法） | 採程式碼，但可能是疏漏，見第 23-C。 |

### 23-B 程式碼推斷與不確定事項

- B1 **settings.json 啟動路徑不保留損壞副本**：`LoadSettingsForStartup` 在 `JsonException` 時直接回傳預設值，沒有改名。下一次套用設定時，損壞的內容會被覆寫，使用者的舊設定無法找回。與 SPEC §5 的要求不符。Mac 版建議啟動與一般讀取走同一條路徑。
- B2 **回復（取代目前內容）不排程自動存檔**：`ApplyRestoredContent` 會抑制 `TextChanged`，因此 `Document_TextChanged` 不會觸發，自動存檔計時器不會啟動。分頁會標示為未儲存，但需要手動儲存或再打一個字才會存檔（依程式碼推斷，需實機確認）。
- B3 **分頁排序在重新啟動後可能失效**：`CreateSessionState` 依 `_documents.Views` 的字典順序寫出，而拖曳排序只改 `TabOrder`，不改字典順序。還原時依寫出的順序建立分頁，因此重新啟動後排序可能回到建立順序（依程式碼推斷，需實機確認）。
- B4 **快照內容取自記憶體緩衝**：自動與手動快照寫入的是 `document.Text`，不是磁碟上的內容。尚未存檔的修改也會進入快照（依程式碼推斷）。CL 0.0.1 寫的是「只有已存檔的檔案會建立快照」，指的是「有檔案路徑」而非「內容已存檔」，兩者語意不同。
- B5 **自動快照的實際延遲**：檢查計時器每 30 秒執行一次，所以停止編輯達到間隔後，實際建立可能延後最多約 30 秒（依程式碼推斷）。
- B6 **另存新檔不加入群組項目**：`SaveActiveAsAsync` 只更新分頁的群組關聯（`SourceGroupId`，若有的話），沒有寫入 `groups.json` 的項目（依程式碼推斷）。
- B7 **改名後的最近清單**：`RenameDocument` 沒有更新 `_recent`，清單中的舊路徑會變成停用（依程式碼推斷）。
- B8 **「全部取代」的 undo**：`ReplaceAll` 以 `TextDocument.Text = …` 整段覆寫，是否能用一次 Ctrl+Z 復原，程式碼無法判定，需實機確認。
- B9 **搜尋的比較規則**：使用 `CurrentCultureIgnoreCase`，在繁體中文系統上的行為與英文系統不同，且會受變音符號與全形半形影響。Mac 版要明確定義規則。
- B10 **快照的字數顯示單位**：快照列表使用 `text.Length`（UTF-16 字元數），與狀態列的文字元素計數不同，同一份文字兩處數字可能不一致。是否刻意保留，需決定。
- B11 **UTF-16 LE 無 BOM 檔案**：UTF-8 嚴格解碼對 NUL 位元組不會報錯，因此無 BOM 的 UTF-16 LE 檔案可能被當成含 NUL 的 UTF-8 開啟，而不是報錯（依程式碼推斷，需以實機檔案驗證）。
- B12 **狀態列的編碼與換行文字**：直接顯示列舉名稱（`Utf8WithBom`、`CrLf`），屬於開發用的顯示，不是使用者友善的字串。
- B13 **視窗大小與位置**：原始碼中沒有保存與還原視窗邊界的程式碼，每次啟動都是預設尺寸（推斷）。
- B14 **閱讀器連結的開啟方式**：只允許 http、https、mailto；其他協定（例如 `file:`）不處理，也不提示，使用者會以為連結沒反應（依程式碼推斷）。
- B15 **快照索引的 `FileFingerprint` 可能為 null**：`LastSavedFingerprint` 在某些路徑下可能為 null（例如尚未存檔的文件），`sourceFingerprint` 會寫成 `null`。讀取端需容許（依程式碼推斷）。
- B16 **群組色票說明與 `RemapColors` 位置比對**：色票只能修改、新增、刪除，不能排序（程式註解的假設）。若 UI 之後允許拖曳排序色票，`RemapColors` 會失效。
- B17 **快捷鍵 `Ctrl+Y` 在 Mac 上的意義**：Windows 的重做鍵，Mac 慣例為 ⇧⌘Z。Mac 版要決定是否照抄。
- B18 **閱讀器縮放的重設時機**：每個新開的閱讀器都從 100% 開始（`ApplyZoom(100)` 寫在建構子），因此同一次執行中關閉再開啟也會回到 100%。CL 0.1.0 只寫「重開程式回到 100%」，未提到這種情況（依程式碼推斷）。

### 23-C 需要人決定的事項

以下每一項都會影響 Mac 版的實作方式，請在開始寫程式前定案。

1. **快捷鍵修飾鍵**：全部改為 ⌘（符合 Mac 慣例），還是保留 Ctrl（與 Windows 版完全一致）？混用的話要列出哪些例外。
2. **`.gss` 的雙重用途**：快照與設定匯出檔目前共用副檔名。Mac 版要不要改用不同副檔名（例如設定檔另取名），以免使用者把快照檔匯入設定，或在 Finder 中分不清？快照檔的 Mac 檔案類型註冊方式（UTI）也要一併決定。
3. **快照檔的隱藏方式**：Windows 用 Hidden 屬性。Mac 的對應方式（例如 `chflags hidden`，或以點開頭命名）各有影響；點開頭會改變檔名，與 `<檔名>.gss` 的規則衝突，需決定。
4. **資料存放路徑**：Windows 為 `%LocalAppData%\Glystrata\`。Mac 版要放在哪裡？`errors.log` 的位置也要一併定。
5. **舊快照檔名遷移**：Mac 版是否需要讀取 22-I 的三種舊檔名？若不需要，Program.cs 的 `SnapshotVerification` 第 (g) 項可以不移植。
6. **跨平台匯入群組與工作階段**：群組與工作階段的路徑是 Windows 路徑，Mac 版讀到時如何處理（忽略、保留原字串、提示遺失）？
7. **非 Unicode 編碼**：是否支援 Big5、Shift_JIS 等 ANSI 檔案？繁中使用者常見，若支援，需要定義自動偵測的順序與存檔時的編碼保留方式。
8. **語言預設**：維持一律繁體中文（與程式一致），還是依系統語言決定（與 SPEC §1 一致）？
9. **外部變更的重新載入是否加警告**（A6）；保留後是否再次提示（A7）。
10. **分頁拖到其他窗格**是否要做（A8）。
11. **Markdown 內 fenced yaml 分色**是否要做（A9）。
12. **回復（取代目前內容）後是否自動存檔**（B2）。
13. **未命名文件是否保留到工作階段**（第 14 章）。
14. **手動儲存的狀態文字**是否改為「已儲存」（A19、第 1 章）。
15. **搜尋比較規則**（B9）與**全部取代的 undo 行為**（B8）。
16. **快照字數的計算單位**（B10）。
17. **快照是否只記錄已存檔內容**（B4）。
18. **重新命名後是否更新最近清單**（B7）；**另存新檔是否加入目前群組**（B6）。
19. **視窗大小與位置是否保存**（B13）：屬於新功能，原版沒有。
20. **工作階段是否保存群組篩選與分頁排序**（A11、B3）。
21. **偏好設定是否需要即時預覽語言與主題**（A4）。
22. **設定匯出檔的 `kind` 位置**（A17）：只影響外觀，匯入不受影響，可以忽略。
23. **群組色票說明文字**（A2）：說明要改寫為與程式一致的描述。
24. **快照中的 `notice` 警語**：Mac 寫入時是否保留完全相同的文字（給 AI 助理與自動化工具看的警語）。
25. **「快速鍵是否可自訂」**（第 18 章）：1.2.0 決定不做，Mac 版是否維持。
26. **IME 相關**：Mac 的組字行為由系統處理，本清單的 IME 條目只做驗證，不做實作；是否需要額外的組字顯示客製，需決定。

### 23-D 本清單的假設

- 假設 Mac 版目標為 macOS（Apple Silicon）原生 Swift 應用，不使用 Electron 或瀏覽器元件，與 Windows 版的設計原則一致（SPEC §7）。
- 假設 Mac 版的 Markdown 解析器可以提供與 Markdig 等價的 CommonMark、GFM 表格、任務清單、刪除線、自動連結與 front matter 解析；等價程度需以 21 章的 Markdown 驗證補做。
- 假設 `System.Text.Json` 的日期格式以 22-B 的描述為準；小數秒位數的實際輸出需以 Windows 版產生的 `.gss` 檔實際確認。
- 假設「Hidden 屬性」與「隱藏快照檔案」的語意可以映射到 Mac 的檔案屬性，但映射方式未定（見 23-C 第 3 項）。
- 假設 `.gss` 的快照檔需要能與 Windows 版互通（SPEC 與任務描述要求），因此 22-H 的欄位型別與數值為硬性規格。
- 假設 1.2.3 之後沒有新的版本或未公開的程式變更；本清單只對應 `repo` 目前的內容。
- 未讀取 `docs/specs/2026-10-13-mac-visual-tokens.md`（不在本次指定的閱讀範圍內）。
- 未讀取 `src/Glystrata/MainWindow.xaml`、各 XAML 樣板與 `Resources/Themes/*.xaml` 的逐項樣式，相關外觀只依 CHANGELOG 與 LocalizationService 整理。
- 未實際執行 Windows 版程式，所有行為均來自文件與原始碼閱讀。依程式碼推斷的條目已在文字中標明「依程式碼推斷」或「推斷」。
