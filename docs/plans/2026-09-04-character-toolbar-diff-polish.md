# 字元統計、Markdown 快捷列與差異視窗修正計畫

**日期：** 2026-09-04
**版本範圍：** 既有 0.0.2 後續功能與 UI 修正
**目標產品：** Windows 11 原生 WPF 輕量 Markdown／YAML 編輯器

## 需求整理

1. 降低快照比較畫面的紅／綠色刺激，亮色與暗色主題都要保持文字清楚、背景低對比。
2. 狀態列新增可點選的字元統計，支援目前文件與即時選取文字統計。
3. 偏好設定新增「顯示文字編輯快捷列」開關；開啟後每個編輯窗格的文字區上方顯示 Markdown 操作列。
4. 頂層「檔案／編輯／檢視／群組／說明」整組按鈕置中，按鈕寬度仍依內容收縮。

## 已採用的行為方案

### 字元統計

狀態列顯示「全文件」數量；有反白選取時追加「選取」數量。點擊統計按鈕開啟選擇選單，提供：

- 字元（含空白與換行）
- 字元（不含空白）
- 字元（不含換行）

計數以 Unicode text element（使用者感知到的字元，例如 emoji 組合算一個）為單位；目前選擇的計算方式保存於設定檔，預設為「含空白與換行」。

### Markdown 編輯快捷列

- 開關為全域偏好，預設關閉，以維持極簡介面；套用設定後立即生效，不需重開程式。
- 快捷列放在每個編輯窗格的 TAB 列上方，亮／暗主題沿用現有表面、邊框與文字色。
- MVP 操作：內文／H1–H6 區塊樣式、粗體、斜體、刪除線、行內程式碼、連結、無序清單、有序清單、引用、程式碼區塊。
- 有選取文字時套用於選取內容；沒有選取文字時插入合理的 Markdown 佔位文字並將游標放在可繼續輸入的位置。
- 連結操作使用簡單輸入視窗取得 URL；有選取文字時保留選取文字作為連結文字。
- 編輯區仍只顯示語法分色，不改成所見即所得渲染。

### 差異視窗

- 亮色主題使用淡綠／淡玫瑰背景，暗色主題使用低亮度墨綠／酒紅背景。
- 新增主題資源讓新增、刪除與未變更行的前景／背景可分別調整，避免硬編碼高飽和色。
- 使用 DynamicResource，切換主題時已開啟的差異視窗也能同步更新。

## 技術方案與檔案範圍

- `src/Glystrata.Core/Persistence/AppSettings.cs`
  - 新增字元統計模式與快捷列顯示設定，保留舊設定檔相容性。
- `src/Glystrata.Core/Documents/TextMetrics.cs`
  - 實作 Unicode text element 計數與空白／換行模式。
- `src/Glystrata.Core/Markdown/MarkdownFormattingService.cs`
  - 實作區塊與行內 Markdown 包裹、清單／引用／程式碼區塊前綴處理及游標結果。
- `src/Glystrata/Controls/EditorPaneControl.cs`
  - 建立可切換的快捷列、綁定選取／游標事件、轉送格式化命令與字元統計更新事件。
- `src/Glystrata/MainWindow.xaml.cs`
  - 狀態列字元統計按鈕與模式選單、選取文字即時計算、偏好設定套用、頂層選單置中容器。
- `src/Glystrata/Views/SettingsWindow.xaml.cs`
  - 新增快捷列開關欄位與雙語文字。
- `src/Glystrata/Views/DiffWindow.xaml.cs`
  - 改用可切換主題資源，不再使用硬編碼紅／綠色。
- `src/Glystrata/Resources/Themes/Light.xaml`、`Dark.xaml`
  - 快照差異與快捷列相關色彩資源、頂層選單整組置中樣式。
- `src/Glystrata/Services/LocalizationService.cs`
  - 新增字元統計、快捷列與格式化操作的中英文資源。
- `tests/Glystrata.Verification/Program.cs`
  - 新增字元計數、格式化結果與設定持久化驗證。
- `Plan/CHANGELOG.md`
  - 記錄本次功能與修正。

## 風險與限制

- 格式化快捷列是文字轉換工具，不會嘗試完整解析所有 Markdown AST；巢狀清單與複雜選取先採保守的行首前綴處理。
- 「字元」採 Unicode text element 計數，與以 UTF-16 code unit 計算的開發工具數字可能不同；選單會清楚標示目前模式。
- 設定檔新增欄位使用既有 JSON schema 的可忽略欄位策略，不需要 migration，也不新增外部 dependency。

## 驗收方式

- 亮／暗主題開啟快照比較，新增／刪除／未變更行皆可讀，切換主題後已開啟視窗同步變色。
- 狀態列可切換三種字元模式；輸入、刪除、游標選取與取消選取時文件／選取數量即時更新。
- 偏好設定關閉／開啟快捷列後立即反映於所有窗格；各 MVP 按鈕能對選取文字或游標位置產生合理 Markdown。
- 頂層選單整組位於視窗中央，文字在按鈕中央，按鈕不被整列拉寬。
- `dotnet build Glystrata.sln -c Debug --no-restore -m:1 -p:UseSharedCompilation=false`、`Glystrata.Verification` 與 Release publish 全部通過。

## 實作狀態

使用者已以 `GO` 確認本計畫並完成實作。字元統計採三種模式，快捷列維持上述 MVP 操作範圍；本次未新增外部套件。
