# Changelog

本檔案記錄 Glystrata 的版本、功能開發、錯誤修正與重要行為變更。

## [1.1.0] - 2026-09-12

### Added

- Markdown 閱讀器的文字可以選取與複製（拖曳選取、Ctrl+A 全選、Ctrl+C 複製、右鍵選單），但仍然完全無法編輯。
- 快照紀錄清單與比較視窗的選取互相同步；再次按「比較」會沿用已開啟的比較視窗，選到開窗後才建立的快照時會自動重建。

### Changed

- 快照回復的對話框改為顯示「回復到『快照時間』的內容」，按鍵直接標示「取代目前內容／另存為新檔案／取消」。
- 全部對話框（關於、刪除群組、刪除快照、外部變更、未存檔提醒、開檔／存檔錯誤、偏好設定驗證訊息）改用跟隨主題、含自訂標題列的對話框；程式啟動失敗的訊息仍使用系統對話框。
- 關閉未存檔文件的提示改問「要在關閉前儲存嗎？」，按鍵為「儲存／不儲存／取消」。
- Markdown 閱讀器的 emoji 改以彩色圖檔繪製（內建 Twemoji 15.1.0，約 3.8MB）。WPF 無法繪製彩色字型，編輯器維持系統的單色 emoji。

### Fixed

- 修正注音等輸入法在組字時，除了編輯器內的顯示外，還會在螢幕左下角另外畫一個組字視窗的問題。
- 修正關閉從未存檔的分頁時，按「儲存」沒有反應的問題；現在會跳出存檔路徑選擇器，取消存檔則不關閉分頁。
- 修正對話框偶爾整塊畫成黑色、要重開一次才正常的問題。自訂標題列會讓視窗的合成背景保持透明以露出 DWM 邊框（在 Windows 11 是純黑），而對話框是靜態畫面，只在開啟時繪製一次，那一次若沒送上螢幕就沒有東西會再要求重繪。現在內容就緒後會主動要求重繪一次，合成背景也改成主題底色；對話框同時改為開啟時就是最終尺寸，過長的訊息可捲動。
- 編輯器與閱讀器加入 emoji 後備字型，避免缺字時顯示成方框。

## [1.0.0] - 2026-09-12

首個正式版。功能與 0.2.0 相同，補齊授權後正式公開發布。

### Added

- Glystrata 以 MIT License 釋出（新增 `LICENSE`），發布包附上 `LICENSE.txt`。
- 發布包新增 `DOTNET-THIRD-PARTY-NOTICES.txt`，收錄 .NET 執行環境內含元件的聲明。

### Fixed

- `THIRD-PARTY-NOTICES.txt` 改為附上 AvalonEdit、Markdig、.NET 執行環境的著作權聲明與授權全文，全文取自各專案對應版本的原始授權檔（原本只有授權名稱，且 AvalonEdit 著作權人寫錯）。
- README 將 .NET／WPF 的授權更正為 MIT。

### Verified

- `dotnet build Glystrata.sln -c Debug -m:1 -p:UseSharedCompilation=false`：0 warning、0 error。
- `Glystrata.Verification`：所有驗證 assertions 通過。
- 發布包內容：`Glystrata.exe`、`LICENSE.txt`、`THIRD-PARTY-NOTICES.txt`、`DOTNET-THIRD-PARTY-NOTICES.txt`。

## [0.2.0] - 2026-09-12

### Added

- 分頁右鍵新增「在新窗格開啟 ▸ 左右並排／上下並排」，可在兩個窗格同時編輯同一份檔案。

### Changed

- 左右／上下分割改為把目前窗格的最後一個分頁移到新窗格；窗格只有一個分頁時，新窗格開同一檔案的第二個檢視。
- 「水平分割／垂直分割」改稱「左右分割／上下分割」，避免方向誤解。
- 工具列改用新的線條圖示（開啟檔案、左右分割、上下分割、關閉窗格、還原單一窗格），顏色跟隨主題；「新增檔案」的＋放大為 150%。
- 分頁上開啟 Markdown 閱讀器的眼睛按鈕改用新的線條圖示，開啟中時顯示為強調色。
- 標題列、選單列、工具列之間加上貫通的淡色分隔線。
- 群組側邊欄標題列只保留「新增群組」，標題列下方加分隔線，「全部 TAB」與各群組之間加內縮分隔線；群組右鍵選單保留「加入檔案」、「重新命名」、「刪除群組」。
- 移除「加入資料夾」功能（上方「群組」選單與側邊欄皆不再提供），群組只加入檔案。
- 移除「檢視 → 切換側邊欄」。
- 「關於」改為「輕量化的文字編輯器，支援語法渲染。」，版本號改由程式組件自動帶入。
- 分頁一律單列橫排，超出時可橫向捲動（滑鼠滾輪可捲），選取的分頁會自動捲入可視範圍。
- 空白編輯區提示改為「按 Ctrl+N 新增檔案，或按 Ctrl+O 開啟檔案」。
- 狀態列左側改為只顯示目前檔案所在資料夾（不含檔名，過長時以「…」截斷，滑鼠停留顯示完整路徑）；儲存狀態、編碼與換行格式移到右側。
- 偏好設定快照頁：三個選項各加完整說明（自動快照時機、上限計算方式、快照檔位置與隱藏設定的套用範圍），標籤不再被截斷。
- 說明文件同步更新：README 功能清單、MVP 設計規格加註與目前版本的差異、第三方授權聲明版本號。

### Fixed

- 修正偏好設定中核取方塊與其他選項沒有對齊的問題，所有控制項改為從同一欄靠左起排。
- 修正第三方授權聲明檔開頭仍標示 0.0.2 的問題。

### Verified

- `dotnet build Glystrata.sln -c Debug -m:1 -p:UseSharedCompilation=false`：0 warning、0 error。
- `Glystrata.Verification`：所有驗證 assertions 通過。

## [0.1.0] - 2026-09-12

### Added

- 專案更名為 Glystrata（原 MDeditor），並加入新的程式圖示；標題列圖示會隨亮／暗主題換色。
- 所有視窗改用自訂、跟隨主題的標題列（最小化／最大化／關閉），保留 Aero Snap 拖曳分割，並在最大化按鈕支援 Windows 11 Snap Layouts。
- 狀態列加入編輯器縮放拉桿（50%～200%，Ctrl＋滾輪也可縮放，雙擊百分比回到 100%）；Markdown 閱讀器也有自己的縮放。縮放不改字級設定，重開程式回到 100%。
- 快照：新增「立即建立快照」（快照視窗與檔案選單），與自動快照並存。
- 快照比較視窗可直接切換要比較的快照，差異內容可跨行選取與複製。
- 快照檔 JSON 開頭加入給 AI／自動化工具的中英文說明，提醒不要編輯或刪除快照檔。
- 偏好設定 → 快照新增「隱藏快照檔案」；開啟文件與程式啟動時，既有快照檔會轉成設定的狀態。
- 新增 `scripts/publish.ps1`：發布輸出改到 `dist/win-x64/`，並打包成 `dist/Glystrata-v<版本>-win-x64.zip`。

### Changed

- 快照檔預設改為一般可見檔案，方便與文件一起移動或改名。
- 「刪除全部快照」改為紅字「刪除整個快照檔案」並單獨置左，確認訊息明確說明無法復原。
- 卷軸改為跟隨主題的細卷軸（約 8px），提高滑塊對比。
- 行號與分隔線、分隔線與內文之間各留一個全形空白寬度。
- 頂層選單改為整排靠左，每個按鍵文字置中。
- 縮放拉桿移到狀態列最右側並固定百分比寬度，滑塊改為圓形。
- 按鈕與清單改用完整的主題控制範本，各種狀態（滑過、按下、停用、選取）都使用主題色。

### Fixed

- 修正注音等輸入法組字超過編輯區寬度時，未送出的文字跳到視窗最左側（側邊欄上或左下角）的問題；組字改由編輯器在游標處繪製並在編輯區內換行。
- 修正快照視窗開啟時顏色未套用主題、需滑鼠經過才正確的問題。
- 修正關閉分頁不會釋放檔案監看、計時器與事件訂閱。
- 修正清空文件內容時不會建立快照。
- 修正取代功能無法以空字串取代（留空會被當成取消）。
- 修正偏好設定切換主題時，未套用的顏色編輯被無聲丟棄。
- 修正版面重建後分頁的預覽圖示與實際預覽視窗狀態脫節。
- 修正套用語法顏色後不會立即重繪。
- 修正啟動時先閃一下預設主題／語言。
- 修正圖片路徑含 `&` 等字元時，預覽誤判找不到圖片。
- 修正預覽在 UI 執行緒同步解析造成大型文件輸入卡頓。
- 修正驗證訊息與「未命名」未走在地化。
- 修正自訂標題列最大化時邊緣被裁切。

### Verified

- `dotnet build Glystrata.sln -c Debug -m:1 -p:UseSharedCompilation=false`：0 warning、0 error。
- `Glystrata.Verification`：所有驗證 assertions 通過（含快照可見／隱藏設定與 AI 說明欄位）。

## [0.0.2] - 2026-09-04

### Added

- 支援將一個或多個檔案拖入視窗，加入目前群組並開啟 TAB。
- 支援 Windows 檔案關聯啟動參數，從檔案總管開啟時自動載入文件。
- 在 TAB 右鍵選單加入目前文件的快照歷史入口。
- 狀態列加入 Unicode 字元統計、選取文字即時計算，以及含空白／不含空白／不含換行三種模式。
- 偏好設定加入可立即套用的 Markdown 文字編輯快捷列，提供標題、內文、行內格式、清單、引用與程式碼區塊操作。

### Changed

- 當前群組改以群組名稱前的小圓點表示，不再使用整列反白。
- 頂層選單改為緊湊置中，下拉功能項目改用完整寬度細線逐項分隔。
- 偏好設定維持在「檔案」選單最底部，右上角不再顯示設定按鈕。
- 快照歷史視窗沿用目前亮／暗主題配色。
- 快照比較視窗改用亮／暗主題的低對比新增／刪除／未變更行配色，並讓差異列完整填滿內容區。
- 頂層檔案／編輯／檢視／群組／說明選單整組置於視窗中央，按鈕維持依文字收縮。

### Fixed

- 修正自訂選單、下拉選單與 TreeView 在亮／暗色主題下的背景與文字對比。
- 修正檔案總管啟動參數未被載入的問題。
- 修正左側群組樹只顯示平面群組名稱，以及「移動到群組」下拉選單的群組名稱顯示。
- 修正頂層選單寬度與文字置中問題，移除頂層快捷鍵欄位造成的額外寬度。
- 修正差異比較視窗使用高飽和紅／綠底色造成的閱讀刺激，切換主題時沿用動態資源。
- 修正頂層選單只做到按鈕內文置中、整列仍靠左的版面問題。

### Verified

- `dotnet build Glystrata.sln -c Debug --no-restore -m:1 -p:UseSharedCompilation=false`：0 warning、0 error。
- `Glystrata.Verification`：所有驗證 assertions 通過。
- `win-x64` self-contained 發佈成功，EXE 檔案版本為 `0.0.2.0`，並包含 `THIRD-PARTY-NOTICES.txt`。
- 以 `README.md` 作為啟動參數的桌面煙霧測試成功，視窗可正常關閉且 exit code 為 0。
- `dotnet build`、`Glystrata.Verification`：新增字元統計、Markdown 格式化與新設定欄位驗證全部通過。

## [0.0.1] - 2026-09-04（MVP）

### Added

- 確立 Windows 11 x64 self-contained 桌面版 MVP 方向。
- 建立虛擬群組、自由多窗格 TAB、同檔案多檢視與多預覽視窗的需求基線。
- 建立 Markdown／YAML 分色、亮暗主題、雙語切換、自動存檔與隱藏快照的設計規格與實作計畫。
- 安裝 .NET SDK 10.0.400，作為 0.0.1 的開發工具鏈。
- 建立 `.NET 10 WPF` solution、Core／桌面／驗證三個專案與版本 0.0.1 組件資訊。
- 加入 AvalonEdit 純文字編輯區、Markdown／YAML 基本分色、TAB、多檢視與可遞迴多列／多欄窗格。
- 加入虛擬群組檔案／資料夾樹、TAB 右鍵「移動到群組」功能；移動只變更虛擬關聯，不搬移原始檔案。
- 加入每個 TAB 的眼睛按鈕、可並存的獨立 Markdown WPF 閱讀器，以及本機圖片安全解析。
- 加入亮色／暗色主題、繁體中文／English 即時切換、編輯器語法色彩與閱讀器排版設定。
- 加入自動儲存、外部檔案變更提示、原始文件目錄中的隱藏快照 sidecar、快照上限／間隔、比較／回復／刪除。
- 加入 UTF-8、UTF-8 BOM、UTF-16 LE 與 CRLF／LF／CR 保留，並以原子寫入避免半寫入檔案。
- 加入 Release `win-x64` self-contained single-file publish profile 與專案 README。
- 加入 `THIRD-PARTY-NOTICES.txt`，並隨發布內容附帶 AvalonEdit／Markdig 授權摘要。
- 加入常用鍵盤操作（Ctrl+N／O／S／W／F／H）、編輯器空狀態提示與右上角主題／設定工具列。
- 加入群組 TAB 篩選、全部 TAB 解除篩選、空窗格自動收合與一鍵還原單一窗格。
- 加入選單／右鍵選單／下拉項目的主題色彩資源、低亮度暗色當前 TAB，以及存檔錯誤記錄。

### Fixed

- Replaced WPF system templates with explicit theme-bound templates for the menu bar, context menus, combo-box dropdowns, and editor tabs.
- Moved the All Tabs filter out of the TreeView so editing a document no longer hides the filter label behind an inactive selection highlight.
- Moved Preferences to the bottom of the File menu and removed the duplicate toolbar Preferences button.
- Fixed the window-close deadlock caused by synchronous shutdown waiting on UI-context state-file writes.
- Removed duplicate WPF system-color resource declarations in the dark theme to keep runtime theme loading deterministic.
- 修正同一檔案建立第二個檢視時誤覆蓋既有文件 session 的問題。
- 修正 UTF-8 BOM／UTF-16 LE 編碼寫出時 preamble 未正確附加的問題。
- 修正快照檔案隱藏屬性、上限淘汰、空快照去重與刪除 sidecar 流程。
- 修正 WPF 視窗重建、窗格比例保存、同檔多視圖各自保存捲動／游標位置的串接。
- 修正預覽視窗關閉後 TAB 眼睛狀態未同步、預覽連結可執行非預期 scheme，以及設定／快照／外部變更錯誤未完整攔截的問題。
- 修正暗色主題選單文字不可見、當前 TAB 過亮，以及關閉最後 TAB 後殘留灰色 splitter／空窗格的問題。
- 修正手動／自動／關閉時重疊存檔的序列化與背景執行緒 UI 事件，並加入 WPF 資源配額例外的非致命處理。

### Verified

- `dotnet build Glystrata.sln -c Debug --no-restore -m:1 -p:UseSharedCompilation=false`：成功，0 warning、0 error。
- `Glystrata.Verification`：全部核心驗證通過，包含編碼、文件多視圖、群組移動、狀態持久化、Markdown／YAML、快照與差異比較。
- `Glystrata.Verification`：新增巢狀 pane 收合、單窗格還原、重疊存檔與儲存失敗保留內容驗證，全部通過。
- Debug 與 Release self-contained 單檔 EXE 均可建立 `Glystrata` 主視窗；發布目錄包含 `Glystrata.exe` 與 `THIRD-PARTY-NOTICES.txt`。
- 使用者回報的 `Glystrata.exe` 崩潰事件已確認為 .NET `Win32Exception 1816`（WPF `HwndTarget` 更新視窗時的資源配額不足），已加入記錄與非致命處理；錯誤記錄位於 `%LocalAppData%\Glystrata\errors.log`。

### Notes

- 後續每項功能新增、修正、相容性調整與驗證結果，都追加到本檔案對應版本下。
