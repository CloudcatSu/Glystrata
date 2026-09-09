# Glystrata 0.0.1 MVP 介面與穩定性修正計畫

**目標：** 修正 0.0.1 回報的暗色選單／TAB 對比、群組 TAB 篩選、分割窗格收合與手動存檔崩潰問題，並保留可恢復的單一窗格操作。
**需求來源：** 使用者 2026-09-04 回報的介面與存檔問題；既有基線 `docs/specs/2026-09-04-markdown-editor-design.md`
**Architecture：** 延續既有 WPF resource dictionary、遞迴 `PaneLayoutNode` 與共享 `DocumentSession`；新增可測試的純 layout operation，UI 只負責套用 view／pane 關聯與篩選。
**Tech stack：** .NET 10 WPF、C#、AvalonEdit 6.3.1.120、Markdig 1.3.2；不新增外部套件。

## Global constraints

- 暗色與亮色主題的 `Menu`、`MenuItem`、`ContextMenu`、`Separator`、`TabItem`、選單下拉項目都必須有明確且可讀的背景／前景／hover／disabled 色彩。
- 選定群組時，只在所有窗格顯示該群組的 TAB；其他 TAB 保留在 session 中，不關閉、不刪除、不搬動實體檔案；提供「全部 TAB」入口解除篩選。
- 關閉某窗格最後一個 TAB 後，若該窗格沒有任何保留中的 view，立即移除該 pane 與 splitter；不留下可再次拖出的空白窗格。
- 提供一鍵「還原單一窗格」；還原時保留所有 view，將它們移到一個 pane，不刪除文件或 TAB。
- 手動存檔、自動存檔與關閉時存檔不得因例外而讓應用程式未處理崩潰；寫入仍採同目錄暫存檔與 atomic replace。
- 不變更使用者文件、快照 sidecar 或既有設定 JSON 的 schema；不新增 dependency、外部服務、資料傳輸或權限。
- 每項程式修正與驗證結果追加至 `Plan/CHANGELOG.md`。

## Enterprise review

- 無新增或變更外部項目，不需額外 license review；沿用既有 .NET／AvalonEdit／Markdig 審查與發布 notices。

## Task 1：修正主題選單與當前 TAB 對比

**Files**

- Modify：`src/Glystrata/Resources/Themes/Light.xaml`
- Modify：`src/Glystrata/Resources/Themes/Dark.xaml`
- Modify：`src/Glystrata/Controls/TabHeaderControl.cs`（必要時明確套用 header 前景）
- Modify：`src/Glystrata/Controls/EditorPaneControl.cs`（必要時讓 TabControl／TabItem 使用 theme resource）

**Interfaces**

- 無 public interface 變更；新增的 theme keys 只供桌面 UI 內部使用。

**步驟**

- [ ] 為兩套 theme dictionary 建立 menu background／foreground／hover／disabled、tab active／hover／border 等成對資源。
- [ ] 為 `Menu`、`ContextMenu`、`MenuItem`、`Separator`、`TabControl`、`TabItem`、`ComboBoxItem`／`ListBoxItem` 補上 DynamicResource style，避免回退到 Windows 系統白底或系統文字色。
- [ ] 將暗色當前 TAB 改為低亮度的深色表面與低干擾邊線，保留足夠文字對比與鍵盤 focus 狀態；同步檢查亮色主題。
- [ ] 建置後人工檢查主選單、每個子選單、TAB／群組右鍵選單、設定下拉清單在兩種主題均可閱讀。

**驗收**

- 亮／暗主題下，選單底色與文字色不再使用互相遮蔽的系統預設色。
- 暗色主題目前 TAB 不再呈現刺眼亮色；未選取、hover、disabled 與 focus 狀態均可辨識。

## Task 2：加入群組 TAB 篩選

**Files**

- Modify：`src/Glystrata/MainWindow.xaml.cs`
- Modify：`src/Glystrata/Services/LocalizationService.cs`

**Interfaces**

- 無對外 public interface 變更；新增 MainWindow 內部的 selected group filter state。

**步驟**

- [ ] 保存目前選定的 `Guid?` 群組篩選；新增「全部 TAB／All tabs」樹節點以清除篩選。
- [ ] 群組、資料夾或檔案節點被選取時套用其所屬群組；檔案節點仍照常開啟，資料夾節點只切換群組篩選。
- [ ] 建立 pane 時只將符合篩選的 view 傳給 `EditorPaneControl`；被隱藏的 view 保留在 `DocumentManager`，切換群組即可恢復。
- [ ] 篩選切換、移動 TAB、刪除群組、關閉 TAB 後，重新選擇可見 active view；若沒有可見 TAB，顯示既有 empty state。
- [ ] 重建 sidebar 或切換語言／主題時保留目前篩選狀態，且不讓 TreeView 事件重建流程誤清除篩選。

**驗收**

- 選取群組 A 時，所有 pane 只顯示 A 的 TAB；群組 B 的 TAB 不會被刪除。
- 選擇「全部 TAB」後，原本隱藏的 TAB 回復顯示；切換語言／主題不會重置篩選。

## Task 3：正確移除空窗格並提供一鍵單窗格

**Files**

- Create：`src/Glystrata.Core/Layout/PaneLayoutOperations.cs`
- Modify：`src/Glystrata/MainWindow.xaml.cs`
- Modify：`src/Glystrata/Services/LocalizationService.cs`
- Modify：`tests/Glystrata.Verification/Program.cs`

**Interfaces**

- 新增 Core 內部可測試操作：`PaneLayoutOperations.RemovePane(PaneLayoutNode root, Guid paneId, out bool removed)`。
- 新增 Core 內部可測試操作：`PaneLayoutOperations.CollapseToSinglePane(PaneLayoutNode root)`。
- 不變更文件／群組／快照 public data contract。

**步驟**

- [ ] 將 recursive remove／contains／pane enumerate 邏輯集中到 Core operation，覆蓋巢狀水平／垂直 split，移除後直接以 sibling 取代 split。
- [ ] 關閉 TAB 後，若其原 pane 已沒有任何全域 view，移除該 pane 並讓剩餘 pane 填滿範圍；若只是因群組篩選暫時不可見，則不刪除 pane。
- [ ] 保留「關閉目前窗格」功能，移除前把該 pane 的 view 轉移至存活 pane；最後一個 pane 不可移除。
- [ ] 新增選單與工具列「還原單一窗格／Reset to single pane」，將所有 view 指向保留的 pane ID，重建 layout 時不再產生 splitter 或灰線。
- [ ] 對巢狀 split、關閉最後 TAB、明確關閉 pane、單窗格還原加入 verification assertions。

**驗收**

- 關閉某 pane 的最後 TAB 後，灰色 splitter 完全消失，該區域不可再拖出空窗格。
- 一鍵還原後只有一個 editor pane、沒有 splitter，所有原本的 TAB 與內容仍可使用。
- 巢狀 split 移除任一 pane 後不會留下空 split node。

## Task 4：強化存檔競態與 WPF 例外防護

**Files**

- Modify：`src/Glystrata.Core/Documents/DocumentManager.cs`
- Modify：`src/Glystrata/MainWindow.xaml.cs`
- Modify：`src/Glystrata/App.xaml.cs`
- Modify：`tests/Glystrata.Verification/Program.cs`

**Interfaces**

- `DocumentManager.SaveAsync` 的既有 signature 保持不變；內部加入每文件儲存序列化與安全例外結果。
- 不新增外部 API；錯誤記錄寫入既有 `%LocalAppData%\Glystrata` 應用程式資料目錄。

**步驟**

- [ ] 以每文件 save gate 串行化手動／自動／關閉時的重疊存檔，避免不同寫入完成順序造成 metadata 與內容狀態競態。
- [ ] 將可回報的儲存例外轉為 `SaveResult.Failed`，保留目前 buffer 與可再次儲存能力；成功寫入後才標記 `DocumentSession` 為已儲存。
- [ ] 包住手動 Save／Save As 與 timer async entrypoint；失敗顯示可讀訊息，不留下 fire-and-forget 未觀察例外。
- [ ] 確保關閉時的同步存檔不在背景執行緒直接觸碰 WPF controls；文件事件若從非 UI thread 回來，改由 Dispatcher 安全排程。
- [ ] 在 App 層記錄未處理 UI 例外；針對已觀察的 WPF `Win32Exception` native error 1816（quota insufficient）記錄並以非致命提示處理，避免因視窗更新錯誤直接終止程序。
- [ ] 加入 save round-trip、連續／重疊 SaveAsync、錯誤回傳與關閉流程的 verification；保留 atomic write 與既有 snapshot regression checks。

**驗收**

- 按 Ctrl+S 或選單儲存時，成功後文件已寫入且不會因背景 save／watcher 競態崩潰。
- 寫入失敗時程式仍存活、顯示錯誤、內容仍在編輯器內且可重試。
- 關閉含未儲存文件的視窗時，儲存／取消流程不會跨執行緒更新 WPF UI。
- WPF 1816 事件會被記錄，不再以未處理例外直接終止 `Glystrata.exe`；不影響正常存檔資料。

## Task 5：更新紀錄、建置與交付驗證

**Files**

- Modify：`Plan/CHANGELOG.md`
- Modify：必要時 `README.md`（若操作名稱或診斷資料位置有變更）

**步驟**

- [ ] 在 0.0.1 追加介面、群組、pane、存檔修正與已知 WPF 1816 診斷結果。
- [ ] 執行 `dotnet build Glystrata.sln -c Debug --no-restore -m:1 -p:UseSharedCompilation=false`，預期 0 warning、0 error。
- [ ] 執行 `dotnet run --project tests/Glystrata.Verification/Glystrata.Verification.csproj -c Debug --no-build`，預期全部 assertions 通過。
- [ ] 執行 Release `win-x64` publish，確認版本 0.0.1、EXE 與 notices 均輸出。
- [ ] 實際啟動發布版，人工檢查兩套主題選單、群組篩選、關閉空 pane／單窗格、Ctrl+S 與錯誤提示。

**驗收**

- 所有四類使用者回報問題均有可觀察修正，且既有 Markdown／YAML、預覽、快照、編碼與 session regression 不退化。
- 0.0.1 changelog 包含此次修改與驗證結果。
