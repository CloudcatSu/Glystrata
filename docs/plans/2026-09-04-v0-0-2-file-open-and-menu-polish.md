# MDeditor 0.0.2 檔案開啟與介面修正計畫

**目標：** 讓 MDeditor 能以拖放或 Windows 檔案關聯開啟檔案，並完成群組選取與主選單的視覺收斂，發布版本更新為 0.0.2。

**需求來源：** 使用者 2026-09-04 回報：檔案拖入視窗、檔案關聯開啟、群組白點選取、主選單尺寸／置中／分隔線，以及版本 0.0.2。

**Architecture：** 維持原生 WPF；由 `App` 傳遞 Windows 啟動參數給 `MainWindow`，在工作區載入完成後開啟檔案；由 `MainWindow` 統一處理 `FileDrop`、目前群組解析與選單／群組視覺；亮暗主題仍使用既有 ResourceDictionary。

**Tech stack：** .NET 10 WPF、既有 AvalonEdit／Markdig；不新增 dependency、不變更持久化 schema。

## Global constraints

- 拖放支援一個或多個檔案；資料夾與不存在的路徑不作為文件開啟。
- 拖入檔案會加入目前選取的群組並在目前窗格開啟；若目前是「全部 TAB」篩選，使用目前文件所屬群組，沒有可用群組時使用第一個／預設群組。
- Windows 檔案關聯透過 `StartupEventArgs.Args` 支援，檔案在工作區載入完成後開啟；0.0.2 不加入單一實例 IPC／DDE。
- 當前群組不使用選取反白，改以群組名稱左側小圓點表示；一般滑鼠 hover 不代表目前群組狀態。
- 主選單列採緊湊、水平置中的頂層項目；下拉選單移除分組 `Separator`，每個功能項目以一條完整寬度的細線分隔。
- 偏好設定維持位於「檔案」選單最底部，右上角不放偏好設定按鈕。
- 不變更快照、群組或工作階段 JSON 格式；不搬移原始檔案。

## Enterprise review

- 無新增或變更外部項目，不需額外 license review。

### Task 1：版本與需求驗證基線

**Files**

- Modify：`Directory.Build.props`、`README.md`、`THIRD-PARTY-NOTICES.txt`、`src/MDeditor/Services/LocalizationService.cs`
- Test：版本建置與發布檔 metadata 檢查

**Interfaces**

- 無 public interface 變更。

**步驟**

- [ ] 將產品版本、About 文案與 notices 更新為 0.0.2。
- [ ] 確認歷史 0.0.1 計畫與規格文件維持歷史記錄，不改寫。

**驗收**

- Release EXE 的 FileVersion 為 `0.0.2.0`、ProductVersion 為 `0.0.2`。

### Task 2：拖放與 Windows 檔案關聯開啟

**Files**

- Modify：`src/MDeditor/App.xaml.cs`、`src/MDeditor/MainWindow.xaml.cs`
- Test：啟動參數與檔案拖放人工 smoke test；既有核心驗證 regression test

**Interfaces**

- `MainWindow` 建構子接收啟動檔案路徑集合（內部 UI interface）。

**步驟**

- [ ] `App.OnStartup` 將 `StartupEventArgs.Args` 傳入主視窗。
- [ ] 主視窗載入 workspace、群組與主題後，再逐一開啟有效啟動檔案。
- [ ] 啟用 Window `PreviewDragOver`／`PreviewDrop`，驗證 `FileDrop`，將有效檔案加入目標群組並在目前窗格開啟。
- [ ] 避免拖放事件被 AvalonEdit 內部處理成文字插入，並對資料夾／不存在路徑安全略過。

**驗收**

- 從檔案總管拖入一個或多個 `.md` 檔後，檔案出現在目前群組且各自成為 TAB。
- 以「開啟檔案方式」啟動 `MDeditor.exe <file.md>` 後，工作區載入完成時該檔案會開啟。
- 路徑含空白、重複開啟、資料夾與不存在路徑不會造成崩潰。

### Task 3：群組白點與主選單視覺收斂

**Files**

- Modify：`src/MDeditor/MainWindow.xaml.cs`、`src/MDeditor/Resources/Themes/Light.xaml`、`src/MDeditor/Resources/Themes/Dark.xaml`
- Test：亮／暗主題人工檢查與 Release 啟動 smoke test

**Interfaces**

- 無 public interface 變更。

**步驟**

- [ ] 將群組 TreeViewItem 的 selected background 設為透明，並維持文字可讀。
- [ ] 在群組名稱前加入主題可見的小圓點，依目前群組更新顯示狀態。
- [ ] 縮小頂層選單項目寬度、設定內容置中，移除系統 focus／Aero 視覺殘留。
- [ ] 移除主選單與右鍵選單的分組 Separator，讓每個下拉功能項目以完整寬度細線分隔。
- [ ] 重新檢查選單、TAB、ComboBox 在亮／暗主題的前景、背景、hover 與分隔線。

**驗收**

- 選取群組時只有群組文字前的圓點變為啟用狀態，不出現整列反白。
- 「檔案／編輯／檢視／群組／說明」寬度緊湊且文字置中。
- 所有下拉功能項目之間的細線橫跨選單內容寬度，不出現殘缺短線或白框。

### Task 4：文件、版本與驗證

**Files**

- Modify：`Plan/CHANGELOG.md`
- Verify：`MDeditor.sln`、`tests/MDeditor.Verification`

**步驟**

- [ ] 在 changelog 新增 `[0.0.2]`，記錄功能、視覺與檔案關聯修正。
- [ ] 執行 `dotnet build MDeditor.sln -c Debug --no-restore -m:1 -p:UseSharedCompilation=false`。
- [ ] 執行 `dotnet run --project tests/MDeditor.Verification/MDeditor.Verification.csproj -c Debug --no-build`。
- [ ] 執行 Release `win-x64` self-contained publish，檢查 EXE metadata 與 `THIRD-PARTY-NOTICES.txt`。
- [ ] 啟動發布版並以 `WM_CLOSE` 驗證關閉流程，確認無新錯誤事件。

**驗收**

- 建置 0 warning、0 error，核心驗證全數通過。
- 0.0.2 發布檔可建立主視窗、接受檔案關聯啟動參數，且正常關閉。
- `Plan/CHANGELOG.md` 完整記錄 0.0.2 變更與驗證結果。
