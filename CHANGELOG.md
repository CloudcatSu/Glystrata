# Changelog

本檔案記錄 Glystrata 的版本、功能開發、錯誤修正與重要行為變更。

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
