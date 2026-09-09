# Glystrata MVP 實作計畫

**目標：** 交付一款可在 Windows 11 x64 上獨立執行的 .NET 10 WPF Markdown／YAML 編輯器，具備虛擬群組、自由多窗格 TAB、獨立即時預覽、雙語介面、語法配色、自動存檔與隱藏檔快照。
**版本：** 0.0.1
**需求來源：** `docs/specs/2026-09-04-markdown-editor-design.md`
**Architecture：** 純 C# domain／持久化核心 + WPF UI；文件內容以 canonical path 建立單一共享 session，檢視以遞迴 split tree 管理；預覽採 Markdig AST 到原生 WPF FlowDocument，不使用 WebView。
**Tech stack：** .NET 10 WPF、C# nullable／implicit usings、AvalonEdit 6.3.1.120、Markdig 1.3.2、System.Text.Json、Windows x64 self-contained single-file publish。

## Global constraints

- 只做 Windows 11 x64 桌面程式；MVP 不提供雲端、登入、同步、遙測、外掛、Git、LSP、數學公式、Mermaid 或多主視窗。
- 使用者文件不離開本機；群組／設定／session 只寫入 `%LocalAppData%\Glystrata`，快照 sidecar 才寫入原始文件目錄。
- 群組是平面虛擬群組；加入、移動與移除只改路徑關聯，不刪除或搬移實體文件。
- 同一實體檔案的多個檢視必須共享文字與 undo/redo，但保留各自 caret／scroll state。
- 所有原始文件與設定寫入採暫存檔後 atomic replace；外部修改不可靜默覆蓋本地內容。
- 主編輯器不渲染 Markdown；眼睛按鈕只在 TAB 上，預覽視窗可多開且綁定各自 view。
- 所有 UI 文字、設定與提示需同時有台灣繁體中文與英文資源，語言切換即時生效。
- 開發與修正必須追加記錄至 `Plan/CHANGELOG.md`。
- PowerShell 命令需使用 UTF-8；不自動執行 `git commit`。

## Enterprise review

- 本機目前未安裝可用的 `dotnet` SDK，實作前需要使用 Microsoft 官方 .NET 10 SDK。官方 .NET SDK／runtime 以 MIT 與 Windows product license／third-party notices 發布；self-contained 發布物需保留相應 notices。
- `AvalonEdit 6.3.1.120` 採 MIT License；`Markdig 1.3.2` 採 BSD-2-Clause。發布物附 `THIRD-PARTY-NOTICES.txt`，保留著作權與授權文字。
- 不新增 WebView2、Electron、Tauri、YamlDotNet、第三方 UI theme 或雲端服務；沒有 subscription、copyleft、帳號或文件資料傳輸需求。
- 實作前必須取得使用者對安裝 SDK、restore NuGet 套件與使用外部套件的明確確認；restore 後檢查 lock file 中的直接與 transitive package 授權，發現非 MIT／BSD-2-Clause 項目即停止發布流程並重新檢視。

## 建議交付順序與相依關係

```text
Task 1 Toolchain／solution
        ↓
Task 2 Core 文件／群組／持久化 ─────┐
        ↓                           │
Task 3 Markdown／YAML／preview model ├─→ Task 4 WPF shell／TAB／pane／group UI
        ↓                           │                 ↓
Task 5 autosave／snapshot／external change ─────→ Task 6 settings／theme／i18n／window polish
                                                      ↓
                                          Task 7 publish／manual QA／delivery
```

## Task 1：建立 solution、專案分層與受控相依

**Files**

- Create：`Glystrata.sln`
- Create：`Directory.Build.props`
- Create：`Directory.Packages.props`
- Create：`src/Glystrata.Core/Glystrata.Core.csproj`
- Create：`src/Glystrata/Glystrata.csproj`
- Create：`src/Glystrata/App.xaml`、`src/Glystrata/App.xaml.cs`
- Create：`tests/Glystrata.Verification/Glystrata.Verification.csproj`
- Create：`THIRD-PARTY-NOTICES.txt`
- Create：`README.md`
- Create：`Plan/CHANGELOG.md`

**Interfaces**

- 無對外 public API 變更；建立 solution 內部 project reference：`Glystrata` → `Glystrata.Core`，`Glystrata.Verification` → `Glystrata.Core`。
- `Directory.Packages.props` 固定 `AvalonEdit` 為 `6.3.1.120`、`Markdig` 為 `1.3.2`，啟用 NuGet lock file 與 audit。

**步驟**

- [ ] 在使用者確認安裝授權後，安裝 Microsoft 官方 .NET 10 SDK；執行 `dotnet --info`，確認 SDK 與 Windows desktop workload 可用。
- [ ] 建立 `net10.0-windows` WPF application、core library 與無第三方測試框架的 verification console project。
- [ ] 加入 AvalonEdit／Markdig 的固定版本與 lock file 設定；先執行 `dotnet restore Glystrata.sln` 產生 lock file，再執行 `dotnet restore Glystrata.sln --locked-mode`，檢查 direct／transitive dependencies 與 license。
- [ ] 設定 Release self-contained 發布屬性：`RuntimeIdentifier=win-x64`、`SelfContained=true`、`PublishSingleFile=true`、`IncludeNativeLibrariesForSelfExtract=true`、`PublishTrimmed=false`。
- [ ] 執行 `dotnet build Glystrata.sln -c Debug`，確認空殼 solution 可編譯。
- [ ] 將 `0.0.1` 寫入 application assembly／file version，並以 `Plan/CHANGELOG.md` 記錄初始開發基線。

**驗收**

- solution 可 restore／build，三個 project 都被 solution 正確引用。
- lock file 只包含已審查的直接／transitive package；發布 notices 可追溯到每個非 framework package。
- 產生的 WPF application target 是 `net10.0-windows`，沒有 WebView2 或其他未核准 runtime。

## Task 2：實作文件、編碼、群組與工作階段核心

**Files**

- Create：`src/Glystrata.Core/Documents/DocumentSession.cs`
- Create：`src/Glystrata.Core/Documents/DocumentManager.cs`
- Create：`src/Glystrata.Core/Documents/FileCodec.cs`
- Create：`src/Glystrata.Core/Documents/DocumentViewState.cs`
- Create：`src/Glystrata.Core/Documents/DocumentModels.cs`
- Create：`src/Glystrata.Core/Groups/Group.cs`
- Create：`src/Glystrata.Core/Groups/GroupItem.cs`
- Create：`src/Glystrata.Core/Groups/GroupManager.cs`
- Create：`src/Glystrata.Core/Layout/PaneLayoutNode.cs`
- Create：`src/Glystrata.Core/Persistence/AppSettings.cs`
- Create：`src/Glystrata.Core/Persistence/GroupsState.cs`
- Create：`src/Glystrata.Core/Persistence/SessionState.cs`
- Create：`src/Glystrata.Core/Persistence/JsonStateStore.cs`
- Create：`src/Glystrata.Core/Persistence/AtomicFileWriter.cs`
- Modify：`src/Glystrata.Core/Glystrata.Core.csproj`
- Modify：`tests/Glystrata.Verification/Program.cs`
- Create：`tests/Glystrata.Verification/DocumentVerification.cs`
- Create：`tests/Glystrata.Verification/GroupVerification.cs`
- Create：`tests/Glystrata.Verification/PersistenceVerification.cs`

**Interfaces**

- `IDocumentManager.Open(string path, OpenMode mode)`, `CreateView(DocumentSession document, Guid? sourceGroupId)`、`SaveAsync(DocumentSession document, CancellationToken cancellationToken)`。
- `IGroupManager.CreateGroup(string name)`、`AddPath(Guid groupId, string path, GroupItemKind kind)`、`MovePath(Guid sourceGroupId, Guid targetGroupId, string canonicalPath)`、`RemovePath(Guid groupId, string canonicalPath)`。
- `IStateStore.LoadSettingsAsync`／`SaveSettingsAsync`、`LoadGroupsAsync`／`SaveGroupsAsync`、`LoadSessionAsync`／`SaveSessionAsync`；具體資料型別分別為 `AppSettings`、`GroupsState`、`SessionState`。

**步驟**

- [ ] 以 `Path.GetFullPath`、大小寫不敏感比較與檔案系統 canonicalization 建立 `DocumentKey`；同一路徑只建立一個 `DocumentSession`。
- [ ] 在 `DocumentModels.cs` 定義 `OpenMode`、`GroupItemKind`、`RestoreMode`、`SaveResult` 與 encoding／line-ending enum，讓文件、群組與快照 service 共用明確結果型別。
- [ ] 讓 `DocumentSession` 持有共享的 AvalonEdit `TextDocument`、原始 encoding、line ending、last successful write metadata、modified／save error 狀態。
- [ ] 讓 `DocumentViewState` 保存 view ID、document key、source group ID、caret offset、horizontal／vertical scroll offset 與 pane ID。
- [ ] 實作 UTF-8 BOM／無 BOM、UTF-16 LE BOM 讀取；新檔預設 UTF-8 無 BOM；儲存時保留編碼與 CRLF／LF／CR 選擇。
- [ ] 實作平面群組、檔案／資料夾項目、路徑去重、群組排序、遺失路徑標示，以及「移動至群組」只改虛擬關聯的邏輯。
- [ ] 以 `%LocalAppData%\Glystrata\settings.json`、`groups.json`、`session.json` 建立 versioned JSON schema；使用 `.tmp` + atomic replace，讀取損壞時回退安全預設並回傳通知。
- [ ] 執行 `dotnet run --project tests/Glystrata.Verification/Glystrata.Verification.csproj -c Debug`，確認文件／群組／JSON round-trip assertions 通過。

**驗收**

- 同一檔案在兩個 view 編輯時，兩個 editor 讀到同一文字與 undo/redo；caret／scroll 可各自不同。
- 同檔案可加入多群組；移動 TAB 不改實體路徑、不建立重複群組項目，且只移除指定來源關聯。
- UTF-8／UTF-16 LE 與換行格式 round-trip 不改變原始文件的預期格式。
- 任一設定檔被截斷或格式錯誤時，應用程式仍可啟動並保留損壞檔供診斷。

## Task 3：Markdown／YAML 語法服務與原生預覽模型

**Files**

- Create：`src/Glystrata.Core/Markdown/MarkdownPipelineFactory.cs`
- Create：`src/Glystrata.Core/Markdown/MarkdownPreviewService.cs`
- Create：`src/Glystrata.Core/Markdown/MarkdownPreviewModels.cs`
- Create：`src/Glystrata/Syntax/SyntaxHighlightingService.cs`
- Create：`src/Glystrata/Syntax/Markdown.xshd`
- Create：`src/Glystrata/Syntax/Yaml.xshd`
- Create：`src/Glystrata/Preview/WpfMarkdownRenderer.cs`
- Create：`src/Glystrata/Preview/RelativeResourceResolver.cs`
- Create：`tests/Glystrata.Verification/MarkdownVerification.cs`
- Create：`tests/Glystrata.Verification/Fixtures/basic.md`
- Create：`tests/Glystrata.Verification/Fixtures/frontmatter.md`
- Create：`tests/Glystrata.Verification/Fixtures/sample.yaml`

**Interfaces**

- `IMarkdownPreviewService.Parse(string markdown, string sourceDirectory)` 回傳 `MarkdownPreviewDocument`，其中包含可供 WPF renderer 使用的 block／inline model 與相對資源結果。
- `ISyntaxHighlightingService.SelectDefinition(string extension, string text)` 回傳 Markdown、YAML 或 Plain Text 定義；`ApplyPalette(EditorColorPalette palette)` 更新分類顏色。
- `IWpfMarkdownRenderer.Render(MarkdownPreviewDocument document, PreviewTypography typography, ThemeKind theme)` 回傳 WPF `FlowDocument`。

**步驟**

- [ ] 建立 Markdig pipeline，啟用 CommonMark 與基本 GFM（tables、strikethrough、task lists、autolinks、fenced code），啟用 YAML front matter 解析但不把 front matter 顯示到正文。
- [ ] 將標題、段落、粗斜體／刪除線、連結、清單／任務清單、引用、表格、程式碼、水平線、圖片與自動連結映射成 preview model。
- [ ] 實作相對圖片解析：限制在文件所在目錄的有效路徑；缺圖回傳 alt text／缺圖狀態；外部連結不在 app 內下載。
- [ ] 以 AvalonEdit XSHD 加上 custom colorizing transformer，識別 Markdown heading／emphasis／links／code／quote／list，以及 front matter、fenced YAML、YAML key／value／comment。
- [ ] 對 `.yaml`／`.yml` 直接選用 YAML highlighting；未知可解碼純文字選 Plain Text。
- [ ] 以 verification fixtures 檢查 parser node、front matter 隱藏、相對圖片與 YAML token 分類；執行 `dotnet run --project tests/Glystrata.Verification/Glystrata.Verification.csproj -c Debug`。

**驗收**

- 範例 Markdown 在預覽中正確顯示基本 GFM；編輯區保留原始 Markdown 文字且只套用分色。
- front matter 與 fenced YAML 會分色；直接開啟 YAML 只可編輯，不出現 Markdown 預覽。
- 預覽不執行 JavaScript、不載入外部圖片、不建立網路資料流；相對本機圖片可顯示或明確顯示缺圖。

## Task 4：建立 WPF 主視窗、側邊欄、TAB 與自由窗格

**Files**

- Modify：`src/Glystrata/App.xaml`、`src/Glystrata/App.xaml.cs`
- Create：`src/Glystrata/MainWindow.xaml`、`src/Glystrata/MainWindow.xaml.cs`
- Create：`src/Glystrata/ViewModels/MainWindowViewModel.cs`
- Create：`src/Glystrata/ViewModels/SidebarViewModel.cs`
- Create：`src/Glystrata/ViewModels/EditorPaneViewModel.cs`
- Create：`src/Glystrata/ViewModels/EditorTabViewModel.cs`
- Create：`src/Glystrata/Controls/PaneHost.xaml`、`PaneHost.xaml.cs`
- Create：`src/Glystrata/Controls/EditorPane.xaml`、`EditorPane.xaml.cs`
- Create：`src/Glystrata/Controls/TabHeader.xaml`、`TabHeader.xaml.cs`
- Create：`src/Glystrata/Controls/GroupSidebar.xaml`、`GroupSidebar.xaml.cs`
- Create：`src/Glystrata/Commands/AppCommands.cs`
- Create：`src/Glystrata/Services/PaneLayoutManager.cs`
- Create：`src/Glystrata/Services/GroupInteractionService.cs`

**Interfaces**

- `PaneLayoutManager.Split(Guid paneId, SplitOrientation orientation)`、`Close(Guid paneId)`、`MoveView(Guid viewId, Guid targetPaneId)`、`Serialize()`／`Restore(PaneLayoutNode state)`。
- `EditorTabViewModel` 提供 `CloseCommand`、`OpenNewViewCommand`、`MoveToGroupCommand`、`TogglePreviewCommand`；命令只改 view／group state，不直接改實體檔案位置。

**步驟**

- [ ] 建立極簡主視窗：左 sidebar、右工作區、上方選單／工具列、下方 status bar；保留標準 Windows 視窗按鈕與 resize 行為。
- [ ] 將平面群組與資料夾樹綁定到 sidebar；加入／移除／重新命名／排序、檔案／資料夾 picker、Explorer drag-drop 與遺失路徑狀態。
- [ ] 實作 recursive split tree 與 GridSplitter；目前 pane 可水平／垂直分割，分割後仍可繼續分割；關閉 pane 時把 TAB 移到相鄰 pane，最後一 pane 不可關閉。
- [ ] 實作每 pane 的 AvalonEdit editor、TAB 排序／拖曳、close、普通開啟聚焦既有 view、右鍵「在新檢視開啟」、在檔案總管顯示與「移動至群組」。
- [ ] 將 DocumentSession 的共享 TextDocument 綁到多個 editor，保存每個 editor 的 caret／scroll state；狀態列顯示行／欄、encoding、line ending 與 save status。
- [ ] 加入 Ctrl+N、Ctrl+O、Ctrl+S、Ctrl+Shift+S、Ctrl+W、Ctrl+F、Ctrl+H、Ctrl+Z、Ctrl+Y 等基本命令，並讓選單與 context menu 使用同一命令來源。
- [ ] 執行 `dotnet build Glystrata.sln -c Debug`，再依人工檢核清單測試四窗格、同檔案雙檢視、TAB 拖曳與群組移動。

**驗收**

- 可建立任意水平／垂直多欄多列布局，TAB 可在 pane 間移動，重啟後布局可由 session 還原。
- 同一檔案的兩個 view 同步文字變更，但捲動／游標獨立；一般開啟不產生重複 view，明確命令才產生。
- TAB「移動至群組」會從來源群組移除並加入目標群組，實體檔案路徑與內容不變。

## Task 5：自動存檔、快照、差異與外部變更

**Files**

- Create：`src/Glystrata.Core/Snapshots/SnapshotInfo.cs`
- Create：`src/Glystrata.Core/Snapshots/SnapshotService.cs`
- Create：`src/Glystrata.Core/Snapshots/SnapshotSidecarStore.cs`
- Create：`src/Glystrata.Core/Snapshots/TextDiffService.cs`
- Create：`src/Glystrata.Core/Documents/FilePersistenceService.cs`
- Create：`src/Glystrata/Services/AutoSaveService.cs`
- Create：`src/Glystrata/Services/ExternalChangeMonitor.cs`
- Create：`src/Glystrata/ViewModels/SnapshotHistoryViewModel.cs`
- Create：`src/Glystrata/Views/SnapshotHistoryWindow.xaml`、`SnapshotHistoryWindow.xaml.cs`
- Create：`src/Glystrata/Views/DiffWindow.xaml`、`DiffWindow.xaml.cs`
- Create：`tests/Glystrata.Verification/SnapshotVerification.cs`
- Create：`tests/Glystrata.Verification/ExternalChangeVerification.cs`

**Interfaces**

- `ISnapshotService.ListAsync(string sourcePath, CancellationToken cancellationToken)`、`CreateAsync(DocumentSession document, CancellationToken cancellationToken)`、`DeleteAsync(string sourcePath, Guid snapshotId, CancellationToken cancellationToken)`、`DeleteAllAsync(string sourcePath, CancellationToken cancellationToken)`、`RestoreToDocumentAsync(string sourcePath, Guid snapshotId, RestoreMode mode, CancellationToken cancellationToken)`。
- `IExternalChangeMonitor.Start(DocumentSession document)`、`Stop(DocumentSession document)` 與 `ExternalChangeDecision`（Reload、KeepLocal、Compare）。

**步驟**

- [ ] 由 `SnapshotSidecarStore.GetSidecarPath(string sourcePath)` 實作「原始檔名加前置句點與 `.glystrata-snapshots.json` 後綴」的命名規則，設定 Hidden attribute，並保存 schema version、完整文字內容、encoding／line ending、UTC timestamp 與來源 metadata。
- [ ] 實作每文件 1 秒 idle debounce auto-save；成功寫入後更新 metadata，失敗時保留 buffer、顯示錯誤並可再次儲存。
- [ ] 實作預設 5 分鐘 snapshot interval／20 份上限；只在內容自上次 snapshot 有變更時建立，超過上限刪除最舊項目。
- [ ] 實作快照列表、左右差異檢視、刪除單次快照、確認後刪除整個 sidecar、回復覆蓋與另存新檔；回復覆蓋前保留目前內容的安全狀態。
- [ ] 實作檔案 metadata watcher debounce；外部變更提供重新載入、保留目前內容、差異檢視；有本地變更時禁止自動覆蓋。
- [ ] 以獨立 temp directory 測試 sidecar、上限淘汰、刪除、restore、權限／唯讀失敗與外部變更競態；執行 `dotnet run --project tests/Glystrata.Verification/Glystrata.Verification.csproj -c Debug`。

**驗收**

- 原始目錄出現單一 Hidden snapshot sidecar，側邊欄不顯示；單次刪除與整檔刪除結果可在 snapshot UI 觀察。
- 自動存檔不需要手動按 Save；快照上限與間隔設定有效，且不會在內容未變更時產生無限快照。
- 差異檢視能清楚顯示目前版本／指定快照或外部版本；回復與另存新檔均不靜默丟失目前內容。
- 外部修改三種路徑均可操作，且本地未儲存內容不會被背景事件覆蓋。

## Task 6：主題、語法配色、閱讀器排版、預覽多視窗與雙語

**Files**

- Create：`src/Glystrata/Resources/Themes/Light.xaml`
- Create：`src/Glystrata/Resources/Themes/Dark.xaml`
- Create：`src/Glystrata/Resources/Strings.zh-TW.xaml`
- Create：`src/Glystrata/Resources/Strings.en-US.xaml`
- Create：`src/Glystrata/Services/ThemeService.cs`
- Create：`src/Glystrata/Services/LocalizationService.cs`
- Create：`src/Glystrata/Services/PreviewWindowManager.cs`
- Create：`src/Glystrata/Views/PreviewWindow.xaml`、`PreviewWindow.xaml.cs`
- Create：`src/Glystrata/Views/SettingsWindow.xaml`、`SettingsWindow.xaml.cs`
- Create：`src/Glystrata/ViewModels/SettingsViewModel.cs`
- Modify：`src/Glystrata/Controls/TabHeader.xaml`
- Modify：`src/Glystrata/Preview/WpfMarkdownRenderer.cs`

**Interfaces**

- `IThemeService.CurrentTheme`、`SetTheme(ThemeKind theme)`、`SetEditorPalette(EditorColorPalette palette)`。
- `ILocalizationService.CurrentLanguage`、`SetLanguage(AppLanguage language)`；以 resource dictionary change notification 讓現有視窗重新取字串。
- `PreviewWindowManager.Toggle(DocumentViewState view)`、`Close(Guid viewId)`、`CloseAllForDocument(string documentKey)`、`RefreshAll()`。

**步驟**

- [ ] 建立 Light／Dark WPF resource dictionaries，設計低干擾 sidebar、TAB、分隔線、editor、status bar 與 preview 色彩。
- [ ] 將語法分類 palette 以 theme-specific settings 綁定到 `SyntaxHighlightingService`；提供顏色選擇、即時套用與恢復預設。
- [ ] 建立 PreviewWindow，讓每個 view 的眼睛按鈕可開／關一個獨立視窗；多文件、多 view 預覽同時存在，手動關閉會同步 TAB 狀態。
- [ ] 以 150–300ms debounce 重新產生 FlowDocument；主題與閱讀器設定變更即時刷新所有預覽視窗。
- [ ] 實作 H1～H6 字體大小、行距、段落間距、標題／正文間距設定；使用系統 UI 字型。
- [ ] 實作 zh-TW／en-US resource dictionary 即時切換，更新選單、設定、提示、TAB／預覽標題；預設跟隨 Windows，其他語言回退 zh-TW。
- [ ] 執行 `dotnet build Glystrata.sln -c Debug`，依人工清單測試主題、顏色、閱讀器排版、雙語切換與多預覽視窗。

**驗收**

- 亮／暗主題可由右上角按鈕切換；兩個主題的語法顏色可分開保存，重啟後仍正確。
- 眼睛按鈕位於每個 TAB，按下會切換狀態並開啟對應 preview；多個文件預覽可並存且即時更新。
- 閱讀器設定只影響 preview，不改原始文件與編輯器字體內容。
- 語言切換不需重開，現有主視窗、設定頁、提示與預覽標題都更新；重啟後語言保留。

## Task 7：session restore、發布與最終驗證

**Files**

- Modify：`src/Glystrata/App.xaml.cs`
- Modify：`src/Glystrata/MainWindow.xaml.cs`
- Modify：`src/Glystrata/Services/PaneLayoutManager.cs`
- Modify：`src/Glystrata.Core/Persistence/SessionState.cs`
- Create：`docs/verification/2026-09-04-mvp-manual-qa.md`
- Create：`scripts/Publish-Glystrata.ps1`
- Create：`artifacts/.gitkeep`

**Interfaces**

- `SessionCoordinator.RestoreAsync()` 與 `SaveAsync()`：關閉時保存群組／pane／TAB／active view／caret／scroll／theme／language；不保存 preview windows 的 reopen flag。
- `Publish-Glystrata.ps1` 接受固定 configuration／runtime 參數，預設執行 `dotnet publish src/Glystrata/Glystrata.csproj -c Release -r win-x64 --self-contained true`。

**步驟**

- [ ] 在 app 啟動時依序讀取 settings、groups、session，對不存在檔案、遺失群組路徑與無法載入文件顯示非阻塞提示。
- [ ] 在正常關閉、視窗關閉與 application exit handler 保存 session；預覽視窗不寫入重開狀態。
- [ ] 執行 `dotnet build Glystrata.sln -c Release`。
- [ ] 執行 `dotnet run --project tests/Glystrata.Verification/Glystrata.Verification.csproj -c Release`，確認所有 assertions 通過。
- [ ] 執行 `.\scripts\Publish-Glystrata.ps1`，再執行 `Get-ChildItem .\artifacts\Glystrata-win-x64` 與 `Get-Item .\artifacts\Glystrata-win-x64\Glystrata.exe` 檢查輸出。
- [ ] 在 Windows 11 x64 且沒有 .NET Desktop Runtime 的測試環境啟動發布 EXE，執行 `Glystrata.exe`，依 `docs/verification/2026-09-04-mvp-manual-qa.md` 完成完整人工驗收。
- [ ] 檢查發布目錄包含 `THIRD-PARTY-NOTICES.txt`，不包含測試 fixture、開發中設定、使用者文件或快照資料。

**驗收**

- self-contained win-x64 EXE 可在沒有預裝 .NET 的 Windows 11 上啟動。
- 重啟後群組、窗格、TAB、active TAB、各檢視 caret／scroll、主題與語言恢復；預覽視窗不重開。
- build、verification、publish 與人工 QA 全部通過，發布物無未審查的外部套件或多餘資料。

## 風險控制與停止條件

- 若 .NET 10 SDK 安裝或 NuGet restore 失敗，先停止安裝／重試，回報具體錯誤，不改用未審查的替代下載源。
- 若 AvalonEdit 版本無法在 .NET 10 WPF 正常編譯，先以同一官方套件來源檢查相容版本與授權；未重新確認前不引入新的 editor framework。
- 若原生 WPF renderer 無法在不引入 WebView 的前提下完成某一 Markdown 節點，該節點以安全的純文字 fallback 顯示，不能擴大 MVP 到瀏覽器 runtime。
- 若 sidecar 或 atomic replace 失敗，保留編輯 buffer 與錯誤狀態；不得以刪除原始檔案或靜默覆蓋作為恢復手段。
- 若使用者需求新增會改變資料格式、部署、網路資料流或第三方依賴的功能，先更新 design spec 與 plan，再重新取得 implementation 確認。
