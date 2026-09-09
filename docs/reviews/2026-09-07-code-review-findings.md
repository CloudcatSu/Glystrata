# Code Review Findings — 2026-09-07

來源：`/code-review` 對整個專案的 high-effort 掃描（fork 執行,8 個 finder 角度 + 13 項驗證,全部 CONFIRMED）。
狀態：**尚未修改任何程式碼**,僅記錄,待討論後再處理。

## 資源／狀態管理類（建議優先）

### 1. 關閉分頁不釋放 FileSystemWatcher / 計時器 / 事件訂閱
- **檔案**：`src/Glystrata/MainWindow.xaml.cs:1221`
- **問題**：`CloseView()` 移除 view 後，`_watchers` / `_autoSaveTimers` / `_externalCheckTimers` / `_attachedDocuments` 都沒有清掉，`DocumentSession.Dispose()` 也只解除內部的 `TextChanged`。
- **後果**：每次開關檔案都漏一個檔案監控 handle 和一個計時器，且已關閉的文件物件仍被計時器持有而不會被回收；之後若該檔案在磁碟上被外部修改，還可能跳出「檔案已變更」的錯誤提示（指向一個已經沒開的分頁）。

### 2. 清空文件內容時不會建立快照
- **檔案**：`src/Glystrata.Core/Snapshots/SnapshotService.cs:31`
- **問題**：`CreateAsync` 用 `string.IsNullOrEmpty(document.Text)` 當作「沒東西可存」的判斷，寫在真正的「內容是否變更」檢查之前。
- **後果**：使用者全選刪除一份長期追蹤的文件內容後，週期性快照會直接跳過，正好在最需要復原歷史的時候沒有記錄。

## UI 行為不一致

### 3. Prompt() 無法回傳「刻意留空」的結果
- **檔案**：`src/Glystrata/Dialogs/InputDialogs.cs:53`
- **問題**：文字框留空或空白時 `Prompt()` 回傳 `null`，跟按「取消」無法區分。
- **後果**：在尋找/取代功能中，若使用者故意把取代欄位留空來刪除所有符合的文字，會被當成取消，功能悄悄失效。

### 4. Settings 切換主題會不提示地丟棄未套用的顏色編輯
- **檔案**：`src/Glystrata/Views/SettingsWindow.xaml.cs:93`
- **問題**：`SelectionChanged` 直接呼叫 `LoadPaletteFields()`，用尚未更新的 `_working` palette 覆蓋顏色欄位；使用者輸入只會在按下「套用」時才寫回 `_working`。
- **後果**：使用者改了顏色色碼還沒按套用，就切換主題下拉選單，剛剛輸入的顏色會被無聲蓋掉。

### 5. 預覽開關圖示在版面重建後會跟實際狀態脫節
- **檔案**：`src/Glystrata/Controls/EditorPaneControl.cs:165`
- **問題**：`AddView()` 一律把 tab header 的 `IsPreviewOpen` 重設為 `false`，沒有檢查 `PreviewWindowManager` 實際的視窗開關狀態。
- **後果**：開著 A 檔案的預覽視窗，關閉同個 pane 中的其他分頁或分割視窗觸發重建後，A 的圖示會顯示「未開啟」，但實際預覽視窗還在開；點下去反而會把它關掉。

### 6. 套用語法顏色後不會立即重繪
- **檔案**：`src/Glystrata/Syntax/SyntaxHighlightingService.cs:39`
- **問題**：`SetPalette()` 呼叫 `CurrentContext?.TextView.Redraw()`，但 `CurrentContext` 只有在 AvalonEdit 正在做 colorizing pass 時才非 null；從一般程式碼呼叫時是無效的 no-op。
- **後果**：在 Settings 改語法顏色並按套用後，畫面上已顯示的行不會立刻變色，要等使用者編輯或捲動該行才會生效，看起來像設定沒生效。

### 7. 啟動時會先閃一下預設主題／語言
- **檔案**：`src/Glystrata/MainWindow.xaml.cs:76`
- **問題**：建構子先用寫死的預設 `AppSettings`（亮色、繁體中文）建好 UI，`Window_Loaded` 才非同步載入使用者實際設定並重建一次。
- **後果**：已設定暗色主題＋英文的使用者開啟程式時，會先看到一瞬間的亮色／繁中畫面，再跳到正確設定。

## 其他

### 8. 圖片路徑含 HTML 實體字元時預覽會誤判找不到圖片
- **檔案**：`src/Glystrata/Preview/WpfMarkdownRenderer.cs:331`
- **問題**：`MarkdownPreviewService` 存的是 Markdig 輸出、尚未解碼的 `src`（例如含 `&amp;`），但 `WpfMarkdownRenderer` 用 `XElement.Parse` 讀回的 `src` 屬性已經被 XML 解碼過，字串比對因而失敗。
- **後果**：路徑中含 `&` 等字元的圖片會被誤判成「找不到圖片」，即使檔案實際存在且驗證過。

### 9. 預覽視窗每次刷新都在 UI 執行緒做同步的解析與磁碟 I/O
- **檔案**：`src/Glystrata/Preview/PreviewWindow.xaml.cs:70`
- **問題**：`Refresh()` 在每次 250ms 防抖之後，同步在 UI 執行緒跑 Markdig 解析、逐張圖片 `File.Exists`（磁碟 I/O）與完整 FlowDocument 重建。
- **後果**：編輯大型文件或內含多張圖片（尤其在網路磁碟上）時，預覽視窗開著會讓輸入明顯延遲或整個介面卡頓。

### 10. 部分英文字串沒有走在地化
- **檔案**：`src/Glystrata/Views/SettingsWindow.xaml.cs:217,235,244` 等；`"Untitled"` 另外重複寫死在 `MainWindow.xaml.cs:1892`、`TabHeaderControl.cs:55-57`、`PreviewWindow.xaml.cs:88-90`
- **問題**：三則驗證錯誤訊息直接寫死英文，沒有透過 `LocalizationService`；`LocalizationService.cs` 裡也完全沒有 `untitled` 對應的 key。
- **後果**：繁體中文介面下，輸入無效的整數／數字／顏色值時仍會看到英文錯誤訊息；未存檔文件的分頁、狀態列、預覽視窗標題永遠顯示英文的 "Untitled"，不受語言設定影響。

---

## 待討論事項
- 哪幾項要優先修？（建議先處理 #1、#2，屬於資源/資料遺失風險）
- #10 在地化問題是否要順便補上 `untitled` 的 key，並統一改用 `LocalizationService`？
- #9 的效能問題是否要拆到背景執行緒，或先用簡單的節流/取消上一輪解析來緩解？
