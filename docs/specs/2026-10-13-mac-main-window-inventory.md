# Glystrata 主視窗規格盤點（供 macOS Swift 版重寫用）

- 日期：2026/10/13
- 範圍：Windows WPF 版的主視窗版面、選單、工具列、群組側欄、TAB 與窗格、編輯器、狀態列、視窗層級行為、快速鍵、Windows 專屬部分。
- 性質：唯讀盤點。只整理原始碼中明寫的內容；推斷與原始碼未指定的項目分別標註。

## 引用格式

- `檔名:行號` 為來源位置。除非另外標註，檔案都位於 `src/Glystrata/`。
- 標註 `（Core）` 的檔案位於 `src/Glystrata.Core/`。
- 「原始碼未指定」代表本次閱讀的程式碼沒有寫出該數值或行為，不是遺漏。
- 「推斷」代表由程式碼邏輯推得，原始碼沒有明寫。
- 文字內容（中文字串）位於 Localization 資源，本次未閱讀，因此選單與按鈕只列 localization key。

---

## 1. 主視窗版面結構

- **主視窗 `Window`**（`MainWindow.xaml:8-13`）
  - 標題：初始為 `Glystrata`；語系切換後改為 localization key `app.title`（`MainWindow.xaml.cs:2109`）。
  - 預設大小：寬 1240 × 高 760（`MainWindow.xaml:8`）。
  - 最小大小：寬 760 × 高 500（`MainWindow.xaml:9`）。
  - 啟動位置：螢幕置中 `CenterScreen`（`MainWindow.xaml:10`）。
  - 可否調整大小：XAML 未設定 `ResizeMode`，推斷為 WPF 預設 `CanResize`。程式以 `ResizeMode` 判斷是否可調整（`CustomTitleBar.cs:49-50`）。
  - 調整邊框厚度：6 px（可調整時，`CustomTitleBar.cs:65`）。
  - 根版面：`Grid RootGrid`（`MainWindow.xaml:14`）。實際執行時外層包一層 `DockPanel`，順序為「自訂標題列（頂部）」加「RootGrid（填滿）」（`CustomTitleBar.cs:71-82`）。
  - WindowChrome 圓角：`CornerRadius = 0`（`CustomTitleBar.cs:66`）。

- **1-1 自訂標題列 `CustomTitleBar`**（`CustomTitleBar.cs:93-139`）
  - 高度：32 px（`CustomTitleBar.cs:64`、`:95`）；`WindowChrome.CaptionHeight = 32`。
  - 可否調整：固定高度，不可調整。
  - Margin／Padding：原始碼未指定（外層 Grid 無 Margin）。
  - 背景：`TitleBarBackgroundBrush`（`CustomTitleBar.cs:96`）。
  - 欄位：三欄，寬度依序為 Auto、星號（填滿）、Auto（`CustomTitleBar.cs:97-99`）。
    - **圖示 Logo**：Rectangle，16 × 16；Margin `12,0,8,0`；垂直置中；填色 `MenuForegroundBrush`；遮罩圖為 `Resources/Icons/GlystrataGlyph.png`（`CustomTitleBar.cs:101-111`）。
    - **標題文字**：`FontSize 12`；垂直置中；前景 `MenuForegroundBrush`；綁定 `Window.Title`（`CustomTitleBar.cs:113-121`）。
    - **視窗控制鈕區**：水平 StackPanel（`CustomTitleBar.cs:123-137`）。由左至右：
      1. 最小化：僅當 `ResizeMode != NoResize` 時存在（`:125-129`）。
      2. 最大化／還原：僅當可調整大小時存在（`:130-134`）。
      3. 關閉：一律存在（`:135-136`）。
      - 每顆按鈕：寬 46、高 32、Margin 0、Padding 0、`FontSize 10`（`CustomTitleBar.cs:143-153`）。
      - 字型：`Segoe Fluent Icons, Segoe MDL2 Assets`（`CustomTitleBar.cs:146`）。
      - 字符：最小化 `U+E921`、最大化 `U+E922`、還原 `U+E923`、關閉 `U+E8BB`（`CustomTitleBar.cs:12-15`）。
      - 樣式：關閉鈕用 `TitleBarCloseButtonStyle`，其餘用 `TitleBarButtonStyle`（`CustomTitleBar.cs:153`）。
      - 提示文字 key：`titlebar.minimize`、`titlebar.maximize`、`titlebar.restore`、`titlebar.close`（`CustomTitleBar.cs:127`、`:135`、`:190`）。
  - 啟用狀態：視窗啟用時，標題與 Logo 不透明度 1.0；未啟用時 0.6（`CustomTitleBar.cs:216-221`）。
  - 最大化時的外溢邊距：DockPanel 的 Margin 設為「系統 sizing frame（SM 32）加 padded border（SM 92）」換算後的 DIP 值；非最大化時為 0（`CustomTitleBar.cs:193-209`）。屬 Windows 專屬，見第 10 章。
  - WindowChrome 其他設定：`GlassFrameThickness = 0,0,0,1`、`UseAeroCaptionButtons = false`（`CustomTitleBar.cs:62-69`）。

- **1-2 功能表列 `Menu`**（`MainWindow.xaml.cs:437-501`）
  - 外層 `Border`：上下各 1 px 分隔線（`BorderThickness 0,1,0,1`），邊線顏色 `BorderBrush`（`:494-499`）。
  - 中層 `Grid`：高度 32；背景 `MenuBackgroundBrush`（`:487-491`）。
  - `Menu` 本體：Margin `4,0,0,0`；無邊框；左對齊；垂直置中；背景 `MenuBackgroundBrush`；前景 `MenuForegroundBrush`（`:439-449`）。
  - 頂層項目：高度 32、`MinWidth 0`、Padding `10,0,10,0`、Margin 0、水平靠左、內容置中（`:503-513`）。
  - 頂層選單順序與內容見第 2 章。

- **1-3 工具列 `Toolbar`**（`MainWindow.xaml.cs:515-540`）
  - 高度：38（`:521`）。
  - 背景：`SurfaceBrush`（`:519`）。
  - `LastChildFill = true`（`:520`）。
  - 左側按鈕群：StackPanel 水平，Margin `5,3,5,3`，垂直置中（`:523-528`）。
  - 按鈕細節見第 3 章。

- **1-4 主體 `body`**（`MainWindow.xaml.cs:406-429`）
  - 三欄 Grid：
    - 欄 0：群組側欄，寬 258（固定，`:407`）。
    - 欄 1：`GridSplitter`，寬 5（`:408`、`:414-424`）。背景 `BorderBrush`；`ResizeDirection = Columns`；`ResizeBehavior = PreviousAndNext`。可拖曳，可拖曳的最小寬度：原始碼未指定。
    - 欄 2：窗格主機 `_paneHost`，寬度為星號（剩餘空間）；背景 `WindowBackgroundBrush`（`:409`、`:426-427`）。
  - 側欄的 Border 與內容見第 4 章；窗格見第 5 章。

- **1-5 狀態列 `StatusBar`**（`MainWindow.xaml.cs:401-404`、`:542-623`）
  - 外層 `Border`：上方 1 px 分隔線（`BorderThickness 0,1,0,0`），線色 `BorderBrush`，寬度為全視窗（跨越側欄，`:399-400` 註解）。
  - Dock：Bottom。
  - `StatusBar` 本體：背景 `SurfaceBrush`；前景 `SecondaryTextBrush`（`:544-547`）。
  - 高度：原始碼未指定（WPF `StatusBar` 預設樣式）。
  - 內容見第 7 章。

- **1-6 格式工具列 `FormattingToolbar`**（位於每個窗格內，不屬於主視窗層級）
  - 預設隱藏：由 `_settings.ShowFormattingToolbar` 決定（`MainWindow.xaml.cs:1171`；`EditorPaneControl.cs:56-57`）。預設值為 `false`（`AppSettings.cs:159`，Core）。
  - 高度：推斷為 32 px（按鈕高 27、ScrollViewer 上下 Padding 各 2、底線 1）。
  - 細節見第 3 章。

- **1-7 Dock 排列順序（由上到下）**（`MainWindow.xaml.cs:390-430`）
  1. 功能表列（Top）
  2. 工具列（Top）
  3. 狀態列（Bottom）
  4. 主體（填滿）

### 1-8 主要尺寸一覽

| 區塊 | 寬 | 高 | 可否調整 | 預設／最小 | 來源 |
|---|---|---|---|---|---|
| 視窗 | 1240 | 760 | 可（依 ResizeMode 推斷） | 最小 760 × 500 | `MainWindow.xaml:8-9` |
| 自訂標題列 | 視窗寬 | 32 | 否 | 固定 | `CustomTitleBar.cs:95` |
| 功能表列（含上下 1 px 線） | 視窗寬 | 34（32 + 2） | 否 | 固定 | `MainWindow.xaml.cs:489`、`:494-499` |
| 工具列 | 視窗寬 | 38 | 否 | 固定 | `MainWindow.xaml.cs:521` |
| 群組側欄 | 258 | 填滿 | 可拖曳分隔桿 | 最小值：原始碼未指定 | `MainWindow.xaml.cs:407` |
| 分隔桿 | 5 | 填滿 | 否 | 固定 | `MainWindow.xaml.cs:408` |
| 窗格區 | 剩餘寬 | 剩餘高 | 是（星號） | 分割比例 0.1 至 0.9 | `MainWindow.xaml.cs:1200` |
| 狀態列（含上方 1 px 線） | 視窗寬 | 原始碼未指定 | 否 | 固定 | `MainWindow.xaml.cs:401` |

---

## 2. 選單完整結構

- 頂層順序：檔案（`menu.file`）、編輯（`menu.edit`）、檢視（`menu.view`）、群組（`menu.group`）、說明（`menu.help`）（`MainWindow.xaml.cs:451`、`:463`、`:469`、`:477`、`:482`）。
- 頂層選單之間與各選單內，**沒有加入任何分隔線**。分隔線只出現在「最近使用的檔案」子選單與「群組色彩」子選單（`MainWindow.xaml.cs:750`、`:1059`）。
- 所有項目都沒有依狀態停用或隱藏的條件，除了「最近使用的檔案」（見下）。
- 項目的快速鍵文字由 `ShortcutService.GetGestureText` 自動產生（`MainWindow.xaml.cs:766`），格式為 `Ctrl+Shift+S` 之類。

### 2-1 檔案 `menu.file`（`MainWindow.xaml.cs:451-461`）

| 順序 | 項目 | localization key | 快速鍵（Windows） | 停用／隱藏條件 | 動作 |
|---|---|---|---|---|---|
| 1 | 新增 | `file.new` | Ctrl+N | 無 | `NewUntitled`（`:452`） |
| 2 | 開啟 | `file.open` | Ctrl+O | 無 | `OpenFiles`（`:453`） |
| 3 | 最近使用的檔案（子選單） | `file.recent` | 無 | 無 | 見下方 2-1-a |
| 4 | 儲存 | `file.save` | Ctrl+S | 無（未設定停用） | `SaveActiveAsync`（`:455`、`:103`） |
| 5 | 另存新檔 | `file.saveAs` | Ctrl+Shift+S | 無 | `SaveActiveAsAsync`（`:456`） |
| 6 | 快照歷程 | `file.snapshotHistory` | Ctrl+Shift+H | 無 | `OpenSnapshotHistory`；無檔案時顯示 `dialog.noFile`（`:457`、`:1760-1773`） |
| 7 | 建立快照 | `file.createSnapshot` | Ctrl+Alt+S | 無 | `CreateSnapshotNow`；無路徑時顯示 `dialog.noFile`（`:458`、`:1811-1817`） |
| 8 | 結束 | `file.exit` | 無 | 無 | 等同視窗 `Close()`，會觸發關閉流程（`:459`、`:107`） |
| 9 | 設定 | `settings.open` | Ctrl+, | 無 | `OpenSettings`（`:460`、`:1969`） |

- **2-1-a 最近使用的檔案子選單**（`MainWindow.xaml.cs:715-758`）
  - 進入子選單時才重建（`:722`），因此內容是即時的。
  - 沒有項目時：顯示 `file.recent.empty`，該項目停用（`:719-721`、`:729-733`）。
  - 每個項目：
    - 顯示文字：檔名（`Path.GetFileName`），底線會被替換為雙底線，避免成為 Alt 助記字元（`:742`）。
    - 工具提示：完整路徑（`:743`）。
    - 啟用條件：`File.Exists(path)`（`:744`）；不存在的檔案會停用。
    - 點擊：`OpenPath(path, GetSelectedGroup()?.Id)`（`:746`）。
  - 最後：分隔線，然後 `file.recent.clear`（清除清單，並排程工作階段儲存，`:750-757`）。
  - 清單上限：原始碼未指定（`RecentFilesService` 未在本次閱讀範圍）。

### 2-2 編輯 `menu.edit`（`MainWindow.xaml.cs:463-467`）

| 順序 | 項目 | localization key | 快速鍵 | 停用／隱藏條件 | 動作 |
|---|---|---|---|---|---|
| 1 | 復原 | `edit.undo` | Ctrl+Z（GestureOnly） | 無 | `Undo()`：僅在 `ActiveEditor.CanUndo` 時執行（`:1929-1935`） |
| 2 | 取消復原 | `edit.redo` | Ctrl+Y（GestureOnly） | 無 | `Redo()`：同上（`:1937-1943`） |
| 3 | 尋找與取代 | `edit.findReplace` | Ctrl+F | 無 | `OpenFindReplace(focusReplace: false)`；無啟用分頁時不動作（`:111`、`:1947-1952`） |

- 「GestureOnly」代表視窗層不攔截該按鍵，由 AvalonEdit 自行處理；選單只負責顯示快速鍵（`AppCommand.cs:36-41`、`:62-64`）。

### 2-3 檢視 `menu.view`（`MainWindow.xaml.cs:469-475`）

| 順序 | 項目 | localization key | 快速鍵 | 停用／隱藏條件 | 動作 |
|---|---|---|---|---|---|
| 1 | 左右分割 | `view.splitHorizontal` | Ctrl+\ | 無 | `SplitActivePane(Horizontal)`（`:113`） |
| 2 | 上下分割 | `view.splitVertical` | Ctrl+Alt+\ | 無 | `SplitActivePane(Vertical)`（`:114`） |
| 3 | 關閉窗格 | `view.closePane` | Ctrl+Shift+W | 無 | `CloseActivePane`（`:115`） |
| 4 | 重設版面 | `view.resetLayout` | Ctrl+Alt+1 | 無 | `CollapseToSinglePane`（`:116`） |
| 5 | 新增檢視 | `view.newView` | Ctrl+Alt+N | 無 | `OpenNewView`（`:117`） |

- 「新增檢視」：以目前啟用分頁的同一份文件，在同一窗格建立第二個檢視；沒有啟用分頁時改為新增未命名文件（`:1534-1549`）。

### 2-4 群組 `menu.group`（`MainWindow.xaml.cs:477-480`）

| 順序 | 項目 | localization key | 快速鍵 | 停用／隱藏條件 | 動作 |
|---|---|---|---|---|---|
| 1 | 新增群組 | `group.new` | Ctrl+Shift+G | 無 | `NewGroup`（`:120`、`:2018-2028`） |
| 2 | 開啟檔案到群組 | `group.openFile` | 無 | 無 | `OpenFilesIntoGroup()`（`:121`、`:2074-2094`） |

### 2-5 說明 `menu.help`（`MainWindow.xaml.cs:482-485`）

| 順序 | 項目 | localization key | 快速鍵 | 停用／隱藏條件 | 動作 |
|---|---|---|---|---|---|
| 1 | Markdown 語法說明 | `help.markdownGuide` | 無 | 無 | `ShowMarkdownGuide`，以非模態視窗 `Show()` 開啟（`:122`、`:2005-2009`） |
| 2 | 關於 | `help.about` | 無 | 無 | `ShowAbout`：標題 `help.about`，內容為版本號（3 段）加 `about.message`（`:123`、`:2011-2016`） |

### 2-6 沒有選單項目、只有快速鍵的指令

- `EditReplace`（Ctrl+H）：無 localization key，因此不在 Edit 選單中（`AppCommand.cs:66`、`MainWindow.xaml.cs:112`）。動作：`OpenFindReplace(focusReplace: true)`。
- `view.toggleReader`（Ctrl+Shift+R）：無選單項目（`AppCommand.cs:73`）。動作：切換啟用分頁的閱讀器預覽（`MainWindow.xaml.cs:126-132`）。
- `view.closeTab`（Ctrl+W）：無選單項目（`AppCommand.cs:74`）。動作：關閉啟用分頁（`MainWindow.xaml.cs:134-140`）。

---

## 3. 工具列與格式工具列

### 3-1 主工具列（`MainWindow.xaml.cs:515-540`）

- 按鈕共通設定（`CreateToolbarButton`，`:775-792`）：寬 34、高 30；Margin `2,0,2,0`；Padding 0；`BorderThickness 0`；背景透明；前景 `PrimaryTextBrush`；`FontSize 16`；點擊執行對應動作。
- 按鈕之間：沒有分隔線（`:523-536` 未加入 Separator）。
- 顯示條件：一律顯示（原始碼未設定條件）。停用條件：無。
- 按鈕沒有「儲存」或「復原」按鈕。

| 順序 | 內容 | 圖示來源 | 提示文字 key | 動作 | 來源 |
|---|---|---|---|---|---|
| 1 | `＋`（全形加號字元，`FontSize 24`） | 文字，不是圖示 | `toolbar.new` | `NewUntitled` | `:529-530` |
| 2 | 圖示 | `Resources/Icons/Toolbar/OpenFile.png` | `toolbar.open` | `OpenFiles` | `:532` |
| 3 | 圖示 | `Resources/Icons/Toolbar/SplitLeftRight.png` | `toolbar.splitHorizontal` | `SplitActivePane(Horizontal)` | `:533` |
| 4 | 圖示 | `Resources/Icons/Toolbar/SplitTopBottom.png` | `toolbar.splitVertical` | `SplitActivePane(Vertical)` | `:534` |
| 5 | 圖示 | `Resources/Icons/Toolbar/ClosePane.png` | `toolbar.closePane` | `CloseActivePane` | `:535` |
| 6 | 圖示 | `Resources/Icons/Toolbar/SinglePane.png` | `toolbar.resetLayout` | `CollapseToSinglePane` | `:536` |

- 圖示實作：`IconFactory.Create("Toolbar/{name}.png", 18, "PrimaryTextBrush")`（`:772-773`）。
  - 尺寸 18 × 18。
  - 圖示 PNG 是黑底透明的線稿，作為 `OpacityMask` 使用；填色由 `PrimaryTextBrush` 決定，會跟隨主題（`IconFactory.cs:6-30`）。
  - 高品質縮放（`RenderOptions` 設為 `HighQuality`，`IconFactory.cs:15`）。
- 按鈕 hover、pressed 樣式：由 App 資源字典的 Button 樣式決定，本次未閱讀（原始碼未指定於本檔）。

### 3-2 格式工具列（`EditorPaneControl.cs:567-627`）

- 位置：每個窗格的最上方，在分頁列之上（`EditorPaneControl.cs:70-75`）。
- 顯示條件：`ShowFormattingToolbar`（設定值，預設 `false`，見 `AppSettings.cs:159`）。窗格建立時依設定決定（`MainWindow.xaml.cs:1171`、`EditorPaneControl.cs:57`）。
  - 設定頁有對應勾選框（`SettingsWindow.xaml.cs:413`、`:549`）。
  - 執行中切換的方法 `SetFormattingToolbarVisible` 已定義，但沒有任何呼叫端（`EditorPaneControl.cs:117-118`）。
- 外層：`Border`，`BorderThickness 0,0,0,1`（下方 1 px 線）；背景 `SurfaceBrush`；線色 `BorderBrush`（`:595-601`）。
- 內層：`ScrollViewer`
  - 水平捲動：Auto；垂直捲動：Disabled（`:587-591`）。
  - Padding `5,2,5,2`。
  - 工具提示：`toolbar.formatting`（`:593`）。
- 按鈕（`AddFormatButton`，`:605-627`）：
  - 高度 27；寬度：標籤長度大於 2 字元時 `MinWidth 38`，否則 `MinWidth 30`。
  - Padding `5,0,5,0`；Margin `1,0,1,0`；`BorderThickness 0`；背景透明；前景 `PrimaryTextBrush`；內容水平與垂直置中。
  - 字重：`B`（粗體）與 `H1` 為 SemiBold，其餘為 Normal（`:619-621`）。
  - 點擊：觸發 `FormattingRequested`，最後由 `ApplyFormatting` 套用到選取文字（`EditorPaneControl.cs:347-378`）。
  - 停用條件：無。

| 順序 | 顯示文字 | 提示文字 key | 格式命令 | 特別行為 | 來源 |
|---|---|---|---|---|---|
| 1 | ¶ | `format.paragraph` | Paragraph | 無 | `:570` |
| 2 | H1 | `format.heading1` | Heading1 | 粗體 | `:571` |
| 3 | H2 | `format.heading2` | Heading2 | 無 | `:572` |
| 4 | H3 | `format.heading3` | Heading3 | 無 | `:573` |
| 5 | H4 | `format.heading4` | Heading4 | 無 | `:574` |
| 6 | H5 | `format.heading5` | Heading5 | 無 | `:575` |
| 7 | H6 | `format.heading6` | Heading6 | 無 | `:576` |
| 8 | B | `format.bold` | Bold | 粗體 | `:577` |
| 9 | I | `format.italic` | Italic | 無 | `:578` |
| 10 | S | `format.strikethrough` | Strikethrough | 無 | `:579` |
| 11 | `` ` `` | `format.inlineCode` | InlineCode | 無 | `:580` |
| 12 | ↗ | `format.link` | Link | 先彈出 `InputDialogs.Prompt`，標題 `format.link`，欄位 `format.linkUrl`，預設值 `https://`；取消則不動作（`MainWindow.xaml.cs:1290-1304`） | `:581` |
| 13 | • | `format.unorderedList` | UnorderedList | 無 | `:582` |
| 14 | 1. | `format.orderedList` | OrderedList | 無 | `:583` |
| 15 | ❯ | `format.quote` | Quote | 無 | `:584` |
| 16 | ``` | `format.codeBlock` | CodeBlock | 無 | `:585` |

- 套用結果：若文字沒有變化，只把焦點移回編輯器並回傳 `false`（`EditorPaneControl.cs:360-364`）。
- 各格式實際產生的 Markdown 文字：原始碼未指定於本次閱讀範圍（`MarkdownFormattingService` 未閱讀）。

---

## 4. 群組側欄

### 4-1 側欄版面（`MainWindow.xaml.cs:643-704`）

- 外層 `Border`：
  - 背景 `SidebarBrush`；線色 `BorderBrush`；`BorderThickness 0,0,1,0`（右側 1 px 線，`:697-703`）。
  - 寬度由主體欄 0 決定，固定 258（`:407`）。
- 標題區 `header`（Dock Top）：
  - 外層 `Border` 下方 1 px 線（`BorderThickness 0,0,0,1`），Margin `0,0,0,6`（`:647-652`）。
  - `Grid` Margin `12,10,10,8`（`:646`）。
  - 標題文字 `sidebar.groups`：`FontSize 16`、`FontWeight SemiBold`、前景 `PrimaryTextBrush`、垂直置中（`:656-663`）。
  - 右側 `＋` 小按鈕：`CreateSmallButton`，寬 25、高 24、`FontSize 14`、Margin `2,0,0,0`、透明背景、前景 `SecondaryTextBrush`、無邊框；提示文字 `group.new`；點擊 `NewGroup`（`:667`、`:794-811`）。
- 「全部 TAB」按鈕（Dock Top）：
  - 內容 `sidebar.allDocuments`；高 30；Margin `8,0,8,6`；Padding `10,4,10,4`；`BorderThickness 0`；水平靠左；`Focusable false`（`:673-686`）。
  - 前景 `PrimaryTextBrush`。
  - 背景：未選取群組時為 `TabActiveBrush`，否則透明（`:899-911`）。
- 群組樹 `TreeView`：無邊框、背景透明、前景 `PrimaryTextBrush`，填滿剩餘空間（`:689-696`）。

### 4-2 群組列長相（`MainWindow.xaml.cs:833-877`）

- 每個群組是一個 `TreeViewItem`，只有一層，沒有子項目。
- 列的外觀：
  - `IsExpanded = true`；`FontWeight SemiBold`；前景 `PrimaryTextBrush`（`:857-863`）。
  - Margin `8,0,8,0`。
  - `BorderThickness 0,1,0,0`：每一列上方有 1 px 線，線色 `BorderBrush`。程式註解說明這條線分隔「全部 TAB」或前一個群組（`:856`、`:864-868`）。
- 列內容（`StackPanel` 水平）：
  1. **色條**：`Border`，寬 3、高 15、`CornerRadius 1.5`、Margin `1,0,7,0`、垂直置中。背景為群組色；沒有顏色時 `Visibility = Hidden`，保留空間讓名稱對齊（`:836-846`）。
  2. **名稱**：`TextBlock`，前景 `PrimaryTextBrush`，垂直置中（`:848-854`）。
- 列高、內縮、展開箭頭的寬度：原始碼未指定（TreeViewItem 預設樣式）。
- 名稱長度上限、截斷方式：原始碼未指定。

### 4-3 選取樣式與群組色混入背景

- 未選取：背景透明（`MainWindow.xaml.cs:920-923`）。
- 已選取：背景 = `GroupColors.TryCreateSelectionBackground(群組色, GroupSelectionTint)`；若回傳 `null` 則回退為 `TabActiveBrush`（`:925-928`）。
- 混色公式（`Services/GroupColors.cs:33-53`）：
  - 每個色彩通道：`群組色 × t + 側欄背景 × (1 − t)`，四捨五入。
  - `t` 為 `GroupSelectionTint`，預設 `0.14`（`AppSettings.cs:165`，Core），會被限制在 0 至 1。
  - 側欄背景取自資源 `SidebarBrush`（`GroupColors.cs:40`）。
  - 結果凍結（Freeze）後使用。
- 無效情況：
  - 群組沒有顏色，或顏色無法解析：`TryCreateBar` 回傳 `null`，改用 `TabActiveBrush`（`GroupColors.cs:8-28`）。
  - `t <= 0`：`TryCreateSelectionBackground` 回傳 `null`，改用 `TabActiveBrush`（`GroupColors.cs:35-36`）。
    - **注意**：`AppSettings.cs:24-25` 的註解寫「0 會讓選取列不上色」，但程式實際回退為 `TabActiveBrush`，與註解不一致。見第 11 章。
- 前景維持 `PrimaryTextBrush`；`TreeViewItem` 樣式不覆寫已選取背景，因此可以逐群組設定（`MainWindow.xaml.cs:913-915`）。

### 4-4 群組右鍵選單（`MainWindow.xaml.cs:1020-1073`）

由上到下（沒有分隔線）：

1. `group.openFile`：開啟檔案到此群組（`OpenFilesIntoGroup(group)`，`:1024`）。
2. `group.color`（子選單）（`:1030`、`:1036-1073`）：
   - 色票：來源為設定中的 `GroupColors` 清單（預設值見下）。
     - 每一項：Header 顯示色碼本身；Icon 為 12 × 12 色塊（`CornerRadius 2`）；可勾選；勾選條件為「與該群組目前色碼相同（不分大小寫）」。點擊呼叫 `ApplyGroupColor(group, color)`（`:1042-1055`）。
   - 分隔線（`CreateMenuSeparator`，`:1059`）。
   - `group.color.none`：可勾選，勾選條件為「群組沒有顏色」；點擊套用無色（`:1060-1067`）。
   - `group.color.manage`：開啟設定視窗（`OpenSettings`，`:1069-1071`）。
   - 預設色票（`AppSettings.cs:138-142`）：`#3B6FF5`、`#2DA44E`、`#D97706`、`#8250DF`、`#CF222E`。
3. `group.rename`：重新命名（`RenameGroup(group)`，`:1025-1026`）。
4. `group.delete`：刪除群組（`DeleteGroup(group)`，`:1027-1028`）。

- 停用條件：無。
- 「套用群組色」流程：`SetGroupColor` 成功後重建側欄、重新整理所有窗格的群組色條、排程工作階段儲存（`MainWindow.xaml.cs:1075-1093`）。

### 4-5 拖放行為（`MainWindow.xaml.cs:934-1018`）

- 拖曳來源：群組列。
  - 在整列（`TreeViewItem`，不只標題文字）按下左鍵後，移動超過系統拖曳距離門檻（`SystemParameters.MinimumHorizontalDragDistance` 與 `MinimumVerticalDragDistance`）才開始拖曳（`:938-953`）。
  - 資料格式：`GlystrataGroupId`，內容為群組 Id 字串（`:932`、`:953`）。
  - 拖曳效果：`Move`。
  - 拖曳中，來源列不透明度為 0.5，結束後還原（`DropIndicator.cs:96-109`）。
- 放置目標：每個群組列都接受以下兩種資料：
  - 群組（`GlystrataGroupId`）：效果 `Move`。
  - 分頁（`GlystrataTabViewId`，見第 5 章）：效果 `Move`。
  - 其他資料：效果 `None`（`:957-964`）。
- 放置指示（`ShowGroupDropIndicator`，`:993-1018`）：
  - 分頁拖到「不同於其所屬群組」的群組列：整列外框指示（Outline）。
  - 分頁拖到「其所屬群組」：隱藏指示，放下也無動作（`:995-1005`）。
  - 群組拖到另一個群組列：
    - 被拖曳的群組原本在目標上方（索引較小）：指示線在目標的下緣（Bottom）。
    - 被拖曳的群組原本在目標下方：指示線在目標的上緣（Top）（`:1013`）。
  - 群組拖到自己：隱藏指示。
- 放下結果（`:966-988`）：
  - 分頁放到群組：若與來源群組不同，呼叫 `AssignViewToGroup`（改寫該分頁的所屬群組，並把檔案加入目標群組的清單，`MainWindow.xaml.cs:1743-1758`）。
  - 群組放到群組：`_groups.MoveGroup(來源索引, 目標索引)`，重建側欄，排程工作階段儲存。
- 離開群組列：`DropIndicator.HideSoon`，延遲 90 ms 隱藏指示（`:965`、`DropIndicator.cs:24`、`:64-71`）。
- 拖到群組列以外的位置（例如「全部 TAB」按鈕或群組之間的空白）：程式未設定放置處理，本次閱讀範圍內沒有動作（推斷：`「全部 TAB」` 按鈕未設定 `AllowDrop`）。

### 4-6 重新命名群組流程（`MainWindow.xaml.cs:2030-2043`）

1. 只能從右鍵選單觸發。沒有行內（inline）編輯，也沒有快速鍵。
2. 彈出 `InputDialogs.Prompt`：標題 `group.rename`、欄位標籤 `group.rename`、預設值為目前名稱（`:2037`）。
3. 確認條件：按下確認、輸入不為空白、且 `_groups.RenameGroup(id, 新名稱)` 回傳成功（`:2038`）。
4. 成功：重建側欄，排程工作階段儲存。
5. 名稱是否可重複、長度上限、是否去除前後空白：原始碼未指定（`GroupManager` 未閱讀）。

### 4-7 新增與刪除群組流程

- 新增（`MainWindow.xaml.cs:2018-2028`）：
  - 彈出 `InputDialogs.Prompt`，標題與欄位標籤皆為 `group.new`，無預設值。
  - 取消或輸入為空白：不建立。
  - 成功：新群組加在清單最後；重建側欄，排程工作階段儲存。
- 刪除（`MainWindow.xaml.cs:2045-2072`）：
  1. 彈出 `MessageDialogs.Confirm`：標題 `group.delete`、內容為群組名稱、確認鈕文字 `dialog.delete`。未確認則不動作。
  2. 屬於該群組的所有分頁：所屬群組設為空（`SourceGroupId = null`），分頁本身不會關閉，會回到「未分組」（`:2056-2059`）。
  3. 從清單移除群組；若它是選取中的群組，選取會清除，改為「全部 TAB」（`:2061-2064`）。
  4. 若刪除後沒有任何群組，自動建立一個名為 `sidebar.groups` 的群組（`:2065-2068`）。
  5. 重建側欄與窗格，排程工作階段儲存。

### 4-8 「全部 TAB」的行為（`MainWindow.xaml.cs:891-911`、`:1146-1147`）

- 點擊「全部 TAB」按鈕：`SelectAllTabs`，把選取群組設為空，重建側欄、重建窗格版面、更新狀態列（`:891-897`）。
- 效果：所有分頁都顯示（`IsViewVisible` 在沒有選取群組時回傳 `true`，`:1146-1147`）。
- 按鈕外觀：未選取群組時背景為 `TabActiveBrush`（`:908-910`）。
- 選取某個群組時：只顯示所屬群組為該群組的分頁；其餘分頁仍然開啟，只是不顯示在窗格中（`:1146-1147`）。
- 選取群組時若啟用分頁被隱藏，會自動換成第一個可見分頁（`NormalizeActiveViewForFilter`，`:1149-1160`）。
- 啟動時：選取的群組不會被還原（工作階段未保存此狀態），因此預設為「全部 TAB」（推斷，見第 11 章）。

---

## 5. TAB 與窗格

### 5-1 TAB 容器（`EditorPaneControl.cs:49-53`、`:204-212`）

- 每個窗格內是一個 WPF `TabControl`：`BorderThickness 0`、`Padding 0`（`:49-53`）。
- 每個分頁是 `TabItem`：`Padding 8,3,8,3`（`:209`）。
- `TabItem.Header` 為 `TabHeaderControl`（見 5-2）；`TabItem.Content` 為該分頁的 `TextEditor`（`:204-208`）。
- 分頁列位置：窗格內，格式工具列之下（`:70-77`）。
- 分頁列高度：原始碼未指定（取決於 App 資源字典中的 TabControl 樣式，本次未閱讀）。
- 分頁順序：由 `DocumentManager` 的 `TabOrder` 決定（`MainWindow.xaml.cs:1192`），拖曳重排後寫回（見 5-4）。

### 5-2 TAB 標頭的組成（`TabHeaderControl.cs:3-120`）

水平 `StackPanel`，Margin `2,0,0,0`，垂直置中（`:20-22`）。子元素由左至右：

1. **群組色條**（`:24-32`、`:61-65`）
   - `Border`：寬 3、高 14、`CornerRadius 1.5`、Margin `0,0,6,0`、垂直置中。
   - 背景為所屬群組的顏色；沒有群組或群組無色時 `Visibility = Collapsed`（不保留空間）。
   - 注意：這點與側欄色條（用 `Hidden`，保留空間）不同。
2. **檔名文字**（`:34-39`、`:82-88`）
   - `TextBlock`，垂直置中，`MaxWidth 220`，`TextTrimming = CharacterEllipsis`。
   - 內容：
     - 未命名文件：`document.untitled`。
     - 已存檔：檔名（`Path.GetFileName`）。
     - 已修改（未儲存）：在名稱後加 ` •`（半形空白加 U+2022）。
   - 工具提示：完整路徑；沒有路徑時為名稱。
3. **眼睛按鈕（閱讀器預覽）**（`:41-45`、`:71-80`、`:89-91`、`:97-112`）
   - 圖示：`Resources/Icons/Eye.png`，尺寸 15 × 15，填色跟隨按鈕前景（`eye.SetBinding(Shape.Fill, Button.Foreground)`）。
   - 按鈕：`FontSize 16`、Padding `4,0,4,0`、Margin `5,0,0,0`、`BorderThickness 0`、背景透明、`Focusable false`、垂直內容置中。
   - 狀態：
     - 閱讀器開啟中：不透明度 1，前景 `AccentBrush`。
     - 未開啟：不透明度 0.62，前景 `SecondaryTextBrush`。
   - 停用條件：副檔名為 `.yaml` 或 `.yml`；此時工具提示改為 `toolbar.previewUnavailable`。
   - 工具提示（可用時）：`toolbar.preview`。
   - 點擊：觸發 `PreviewRequested`，切換閱讀器視窗（見 5-3 的 `Pane_PreviewRequested`，`MainWindow.xaml.cs:1309-1318`）。
4. **關閉按鈕**（`:47-48`、`:92`）
   - 內容 `×`（U+00D7），其餘外觀同眼睛按鈕，但前景固定為 `SecondaryTextBrush`。
   - 工具提示：`dialog.close`。
   - 點擊：觸發 `CloseRequested` → `CloseView`。

- 標頭整體高度、寬度（除標題的 `MaxWidth 220` 外）：原始碼未指定。
- 標頭沒有未儲存的小圓點，只用文字後的 ` •` 表示未儲存（`:87`）。

### 5-3 TAB 右鍵選單（`EditorPaneControl.cs:395-450`）

- 同一個 `ContextMenu` 同時掛在標頭與 `TabItem` 上（`:201`、`:211`）。
- 由上到下（沒有分隔線）：

| 順序 | 項目 | 文字 key | 啟用條件 | 動作 | 來源 |
|---|---|---|---|---|---|
| 1 | 關閉 | `dialog.close` | 一律 | `CloseView` | `:398-400` |
| 2 | 閱讀器 | `preview.reader` | 副檔名不是 `.yaml`、`.yml` | 切換閱讀器預覽 | `:401-407` |
| 3 | 在資料夾中顯示 | `tab.showInFolder` | 有檔案路徑 | 開啟 `explorer.exe /select,"路徑"` | `:408-414`、`:550-565` |
| 4 | 在新窗格開啟（子選單） | `tab.openInNewPane` | 一律 | 見下 | `:415-422` |
| 5 | 移至群組 | `group.moveTo` | 一律 | 開啟 `InputDialogs.SelectGroup`，再 `AssignViewToGroup` | `:423-425`、`MainWindow.xaml.cs:1732-1741` |
| 6 | 快照歷程 | `file.snapshotHistory` | 一律（無路徑時對話框提示） | `OpenSnapshotHistory(view)` | `:426-428` |
| 7 | 建立快照 | `file.createSnapshot` | 有檔案路徑（開啟選單時重新計算） | `CreateSnapshotNow(view)` | `:429-435`、`:442-447` |
| 8 | 重新命名 | `tab.rename` | 一律（無路徑時對話框提示） | `RenameDocument(view)` | `:436-438` |

- 「在新窗格開啟」子選單（`:415-421`）：
  - `tab.openInNewPane.leftRight`：左右分割（`Horizontal`）。
  - `tab.openInNewPane.topBottom`：上下分割（`Vertical`）。
- 「重新命名」會真的更名磁碟上的檔案（`File.Move`），並同時搬移快照邊車檔、更新群組的檔案路徑（`MainWindow.xaml.cs:1830-1888`）。目標檔名已存在時拒絕；名稱含非法字元時拒絕（`:1856-1868`）。
- 除此之外沒有停用條件；選單內沒有分隔線。
- 標頭的 `MouseRightButtonUp` 會觸發 `TabContextRequested` 事件，但 `MainWindow` 沒有訂閱，因此沒有實際作用（`EditorPaneControl.cs:90`、`:202`）。

### 5-4 TAB 拖曳與放置指示（`EditorPaneControl.cs:452-548`）

- 拖曳起點：只有在分頁自身的視覺樹內按下才會開始拖曳（`StartedOnTab`，`:496-497`）。
  - 目的：避免在編輯區內選取文字時誤觸分頁拖曳（`:489-495` 註解）。
- 拖曳門檻：`SystemParameters.MinimumHorizontalDragDistance` 與 `MinimumVerticalDragDistance`（系統值，`:465-466`；數值在 Windows 上由系統決定，本次原始碼未指定數值）。
- 拖曳資料格式：`GlystrataTabViewId`，內容為分頁 ViewId（`TabHeaderControl.cs:7`、`EditorPaneControl.cs:471`）。
- 拖曳效果：`Move`；拖曳中來源分頁不透明度 0.5（`DropIndicator.cs:96-109`）。

**同一窗格內的放置：**

- 接受條件：資料格式存在，但放置前會檢查被拖曳的分頁是否屬於同一窗格（`ShowTabDropIndicator` 以 `_views.ContainsKey` 過濾，`:501-510`）。因此**跨窗格拖曳分頁不會被接受**（推斷，見第 11 章）。
- 放置指示（`:512-520`）：
  - 被拖曳分頁的索引小於目標（從左往右拖）：指示在目標的**右緣**（`DropEdge.Right`）。
  - 否則：指示在目標的**左緣**（`DropEdge.Left`）。
  - 拖到自己：隱藏指示。
- 放下（`HandleTabDrop`，`:523-548`）：
  1. 從分頁列移除被拖曳的分頁，並插入到目標的索引位置（因此拖曳時會放到目標之後或之前，取決於方向）。
  2. 把被拖曳的分頁設為選取。
  3. 觸發 `TabsReordered`，把窗格內所有分頁的新順序寫回 `DocumentManager`，並排程工作階段儲存（`MainWindow.xaml.cs:1328-1337`）。
- 離開分頁：`HideSoon`，延遲 90 ms（`:481`）。

**放到群組列：** 見第 4 章 4-5。

**放到窗格外或編輯區：** 視窗層級的 `PreviewDrop` 只處理檔案（`FileDrop`），分頁資料會被略過（`MainWindow.xaml.cs:167-183`、`:185-222`）。

### 5-5 放置指示（DropIndicator）的畫法（`DropIndicator.cs:1-146`）

- 全域只有一個指示器，同時間只存在一個（`:22-28`）。
- 淡入動畫：不透明度 0 到 1，時間 120 ms（`:45-49`）。
- 隱藏延遲：`DragLeave` 後 90 ms（`:24`、`:64-71`），以免在子元素之間移動時閃爍。
- 色彩：`AccentBrush` 資源；找不到時後備為 `#3B6FF5`（`:125-127`）。
- **邊線型（Left、Right、Top、Bottom）**（`:136-143`）：
  - 厚度 3 px，圓角半徑 1.5。
  - 位置：以目標邊線為中心，往外與往內各半個厚度（例如左緣 `x = -1.5`，右緣 `x = 寬度 - 1.5`）。
  - 長度：沿該邊完整延伸（左右緣為目標高度，上下緣為目標寬度）。
- **外框型（Outline，用於放入群組）**（`:129-134`）：
  - 圓角矩形，圓角半徑 4。
  - 外框線 1.5 px，顏色為強調色。
  - 填色為強調色、不透明度約 15%（Alpha `0x26`）。
  - 矩形向內縮 0.75 px，讓線條完整顯示。
- 指示器不接收滑鼠事件（`IsHitTestVisible = false`，`:118`）。

### 5-6 窗格（EditorPaneControl）外觀

- 外層 `Border`：`BorderThickness 1,1,1,0`（底部沒有線，因為狀態列會畫全寬的分隔線，`EditorPaneControl.cs:43-46`）；線色 `BorderBrush`；背景 `SurfaceBrush`（`:43-47`）。
- 內部版面：`Grid`，兩列：
  - 第 1 列：格式工具列（Auto 高度）。
  - 第 2 列：分頁列（星號，填滿）（`:69-78`）。
- 空白提示 `_emptyHint`（沒有分頁時顯示）：
  - 文字 `editor.empty`；Margin 24；水平與垂直置中；文字置中並換行；不透明度 0.8；前景 `SecondaryTextBrush`（`:58-68`、`:699`）。

### 5-7 窗格樹與分割

- 資料模型（`Glystrata.Core/Layout/PaneLayoutNode.cs:1-19`）：
  - `EditorPane(PaneId)`：葉節點，代表一個窗格。
  - `Split(SplitId, Orientation, Ratio, First, Second)`：分割節點。`Orientation` 為 `Horizontal`（左右）或 `Vertical`（上下）。`Ratio` 為第一個子節點所佔比例。
- 渲染（`MainWindow.xaml.cs:1198-1252`）：
  - 左右分割：三欄 Grid，欄寬依序為 `Ratio`（星號）、分隔桿 5 px、`1 − Ratio`（星號）（`:1201-1206`）。
  - 上下分割：三列 Grid，列高同理（`:1207-1212`）。
  - 中間分隔桿（`GridSplitter`）：背景 `BorderBrush`；`ShowsPreview = false`（拖曳即時改變，不顯示預覽線）；`ResizeBehavior = PreviousAndNext`（`:1216-1224`）。
  - 拖曳結束：依實際寬高重新計算比例，限制在 0.1 至 0.9，寫回樹狀結構、清除空窗格、排程工作階段儲存（`:1240-1250`）。
  - 比例在渲染時也會被限制在 0.1 至 0.9（`:1200`）。
- 新分割的預設比例：0.5（`MainWindow.xaml.cs:2666`）。
- 窗格最小寬高：原始碼未指定（只有比例限制，沒有像素下限）。
- 作用中窗格的視覺標示（例如外框高亮）：原始碼未指定。

### 5-8 分割、關閉與還原規則

**分割（`SplitActivePane`，`MainWindow.xaml.cs:1355-1387`）：**

- 作用中窗格沒有分頁：不動作（`:1366-1369`）。
- 作用中窗格只有 1 個分頁：新窗格開啟同一份文件的第二個檢視，等同「在新窗格開啟」（`:1372-1376`）。
- 作用中窗格有 2 個以上分頁：把 `_documents.Views` 過濾後的**最後一個**分頁移到新窗格（`:1378-1386`）。
  - 「最後一個」是依 `_documents.Views` 的列舉順序，而不是分頁列上的視覺順序（推斷，見第 11 章）。
- 新窗格成為作用中窗格，並把被移過去的分頁設為啟用、把焦點移到該分頁。

**在新窗格開啟（`OpenViewInNewPane`，`MainWindow.xaml.cs:1389-1405`）：**

- 在同一份文件上建立新檢視（沿用來源群組），放在新窗格。
- 原窗格被替換為分割節點：原窗格為第一個子節點，新窗格為第二個子節點，比例 0.5（`ReplacePaneWithSplit`，`:2661-2676`）。

**關閉窗格（`CloseActivePane` → `RemovePaneAndMerge`，`:1407-1414`、`:1436-1460`）：**

- 只剩一個窗格：不動作（`:1439`）。
- 否則：被關閉窗格的所有分頁移到**列舉順序中第一個不是它自己的窗格**（`paneIds.First(id != paneId)`，`:1444-1448`）。
  - 這不一定是相鄰的窗格（推斷）。
- 從樹狀結構移除該窗格；兄弟節點取代父節點的位置（`PaneLayoutOperations.RemovePane`，`PaneLayoutOperations.cs:35-58`）。
- 作用中窗格改為啟用分頁所在的窗格；沒有啟用分頁時改為替代窗格（`:1457`）。

**還原單一窗格（`CollapseToSinglePane`，`MainWindow.xaml.cs:1416-1434`）：**

- 已經是單一窗格：不動作（`:1419-1422`）。
- 否則：所有分頁移到樹中第一個窗格，版面變成單一 `EditorPane`，作用中窗格為該窗格。
- 對應按鈕：工具列第 6 顆、選單「重設版面」、快速鍵 Ctrl+Alt+1。

**關閉分頁時的窗格清理（`CloseView`，`MainWindow.xaml.cs:1694-1699`）：**

- 若被關閉的分頁是該窗格最後一個分頁，且窗格數量大於 1：自動移除該窗格（`RemovePaneAndMerge`）。
- 只剩一個窗格時，即使沒有分頁也保留該窗格。

**空窗格清理（`PruneEmptyPanes`，`MainWindow.xaml.cs:1462-1473`）：**

- 在啟動還原後與分隔桿拖曳結束後執行，移除所有沒有分頁的窗格，直到只剩一個。

### 5-9 TAB 過多時的捲動（`EditorPaneControl.cs:666-697`）

- 分頁列是 WPF `TabControl`，超出寬度時由其樣式中的 `TabStripScrollViewer` 提供橫向捲動（程式以範本名稱取得，`:676-686`）。
- 滑鼠滾輪：當 `ScrollableWidth > 0` 時，把滾輪的垂直量轉為水平捲動，移動量為 `−Delta`（原始單位，WPF 的 120 為一格），並標記為已處理（`:688-697`）。
- 切換或新增分頁時，會把該分頁 `BringIntoView`，確保可見（`:666-674`、`:220-222`）。
- 左右箭頭按鈕或捲動條外觀：原始碼未指定（取決於樣式）。

---

## 6. 編輯器

### 6-1 基本設定（`EditorPaneControl.cs:267-297`）

| 項目 | 數值 | 來源 |
|---|---|---|
| 字型 | `Cascadia Code, Consolas, Segoe UI Emoji`（依序回退） | `:274` |
| 基準字級 | 14（WPF 單位） | `:29`、`:275` |
| 行號 | 顯示（`ShowLineNumbers = true`） | `:272` |
| 自動換行 | 開啟（`WordWrap = true`） | `:273` |
| 水平捲動條 | Auto（在自動換行下通常不會出現） | `:279` |
| 垂直捲動條 | Auto | `:280` |
| 內距 Padding | `14,10,14,10` | `:276` |
| 超連結 | 關閉（`EnableHyperlinks = false`、`EnableEmailHyperlinks = false`） | `:281` |
| 目前行標示 | 原始碼未設定（沒有 `HighlightCurrentLine`，採 AvalonEdit 預設） | 搜尋結果為空 |

### 6-2 縮放

- 縮放值：`_editorZoom`，預設 1.0（`MainWindow.xaml.cs:52`）。
- 字級計算：`FontSize = 14 × 縮放值`（`EditorPaneControl.cs:155-163`、`:275`）。
- 範圍：0.5 至 2.0（`MainWindow.xaml.cs:627`、`EditorPaneControl.cs:296`）。
- 步進：
  - Ctrl+滾輪：每格 ±0.1（`EditorPaneControl.cs:288-297`）。條件為 `Keyboard.Modifiers` 恰好等於 Control，並會標記為已處理。
  - 狀態列滑桿：50 至 200（百分比），步進 10，且會貼齊刻度（`MainWindow.xaml.cs:565-573`）。
  - 狀態列百分比文字：雙擊重設為 100%（`:590-596`）。
- 縮放會套用到所有窗格（`MainWindow.xaml.cs:637-640`），並重新計算行號欄的間距（見 6-3）。
- 持久化：**不保存**。`Glystrata.Core` 的持久化資料沒有縮放欄位（搜尋結果為空），每次啟動都回到 100%（推斷）。

### 6-3 行號欄的間距修正（`EditorPaneControl.cs:317-345`）

- 行號欄（`LineNumberMargin`）與虛線欄（`DottedLineMargin`）的右側 Margin，設為「一個全形空白（U+3000）在目前字型與字級下的寬度」。
- 目的是讓行號與文字之間的距離與中文字對齊（註解未明寫，依程式推斷）。
- 每次字級改變或左側欄位集合改變時重新計算（`:287`、`:161`）。

### 6-4 其他編輯器行為

- 每個分頁的游標位置與捲動位置都會被記錄，並在分頁加入時還原（`EditorPaneControl.cs:220`、`:303-304`、`:388-393`）。
- 每個 TextEditor 都掛上 IME 支援（`ImeComposition.Attach`）與 Markdown 輸入助理（`MarkdownTypingAssistant.Attach`，`:284-285`）。這些掛載不區分檔案類型，因此 YAML 檔也會套用自動替換與清單續行。
- 編輯器背景與前景：
  - 背景 `EditorBackgroundBrush`、前景 `EditorForegroundBrush`（`MainWindow.xaml.cs:1172-1175`）。
  - 依主題選取調色盤：深色主題用 `DarkEditorPalette`，否則用 `LightEditorPalette`（`MainWindow.xaml.cs:2147`）。
  - 一般文字的後備色為 `#24292F`（`EditorPaneControl.cs:143`）。
  - 調色盤的預設色值：`EditorColorPalette.CreateLightDefault` 與 `CreateDarkDefault`（`AppSettings.cs:151-152`，Core），色值本次未閱讀，原始碼於本檔未指定。
- 編輯器右鍵選單、拼字檢查、標點轉換等：原始碼未指定（使用 AvalonEdit 預設）。

### 6-5 語法分色規則（`Syntax/SyntaxHighlightingService.cs`）

- 機制：逐行處理（`ColorizeLine`，`:45-116`），每條規則以正規表示式比對整行或片段，再以分類 key 查詢調色盤。
- 調色盤來源：`_palette.Get(分類 key, 後備色)`（`:153`）。實際色值來自設定中的目前主題調色盤。
- 注意：規則之間沒有跨行狀態，只有前置區塊（front matter）有跨行判斷。

| 順序 | 規則 | 比對方式 | 分類 key | 後備色 | 套用範圍 | 來源 |
|---|---|---|---|---|---|---|
| 1 | 前置區塊（front matter） | 第 1 行為 `---`；第 2 至第 30 行中，尚未遇到收尾 `---` 的行，以及收尾 `---` 本身 | `front-matter` | `#8250DF` | 整行 | `:57-60`、`:166-185` |
| 2 | 標題 | `^\s{0,3}#{1,6}(?:\s+.*)?$` | `heading` | `#0969DA` | 整行，之後不再套用其他規則 | `:16`、`:62-66` |
| 3 | 程式碼圍欄 | `^\s*(`{3,}\|~{3,}).*$` | `code` | `#953800` | 整行（只有圍欄行本身），之後不再套用其他規則 | `:19`、`:68-72` |
| 4 | YAML 鍵 | `^\s*[-?]?\s*([A-Za-z_][A-Za-z0-9_.-]*)(?=\s*:)` 的第 1 群組 | `yaml-key` | `#0550AE` | 鍵名 | `:23`、`:74-78` |
| 5 | YAML 值 | `(?<=:\s?)(?!$).+$` | `yaml-value` | `#0A3069` | 冒號之後到行尾 | `:24`、`:81-84` |
| 6 | YAML 註解 | `(?:^|\s)(#.*)$` 的第 1 群組 | `yaml-comment` | `#6E7781` | 註解文字 | `:25`、`:86-89` |
| 7 | 引用 | `^\s*>\s?.*$` | `quote` | `#6E7781` | 整行（與清單互斥） | `:17`、`:92-95` |
| 8 | 清單 | `^\s*(?:[-*+]\s+\|\d+[.)]\s+).*$` 命中時，只上色標記 `^\s*(?:[-*+]\|\d+[.)])` | `list` | `#57606A` | 標記（`-`、`*`、`+`、`1.`、`1)`） | `:18`、`:96-100` |
| 9 | 連結與圖片 | `!?(?:\[[^\]]+\]\([^\)]*\)\|\[[^\]]+\]\[[^\]]*\])` | `link` | `#0969DA` | 整段比對 | `:20`、`:102-105` |
| 10 | 行內程式碼 | `` `[^`\r\n]+` `` | `code` | `#953800` | 整段比對 | `:21`、`:107-110` |
| 11 | 強調 | `(?:\*\*\|__\|\*\|_)[^\r\n]+?(?:\*\*\|__\|\*\|_)` | `emphasis` | `#8250DF` | 整段比對 | `:22`、`:112-115` |

- 規則順序與互斥：
  - 第 2、3 條命中後直接結束該行（`return`），其餘規則不再套用。
  - 第 1 條（前置區塊）不會結束該行，之後的規則照常套用。
  - 第 4 至 6 條（YAML）對 `.yml`／`.yaml` 檔的所有行，或前置區塊內的行生效（`:74`）。
  - 第 7 與第 8 條互斥（`else if`）。
  - 第 9 至第 11 條對非早退的行都會生效，包括清單行與引用行。
- 結果：在 YAML 檔中，以 `# ` 開頭的行會先命中標題規則，而不是 YAML 註解（因為第 2 條先回傳）（推斷）。
- 結果：程式碼圍欄內部的文字不會被上色，只有圍欄那一行被上色（由程式邏輯可直接看出）。
- 後備色：
  - 未知分類 key 的後備色為 `#1F2328`（`:150`）。
  - 解析失敗時，回傳灰色 `Colors.Gray`（`:160-163`），並非上表的後備色。
  - 注意：「一般文字」的後備色在編輯器前景設定（`EditorPaneControl.cs:143`）為 `#24292F`，與分色器未知分類的 `#1F2328` 不同。見第 11 章。
- 疊加順序：同一段文字被多條規則上色時，後套用者是否覆蓋前者，取決於 AvalonEdit 的 `ChangeLinePart` 行為（推斷為後者覆蓋前者，見第 11 章）。

### 6-6 Markdown 輸入助理（`Editing/MarkdownTypingAssistant.cs`）

**A. 清單續行（按 Enter 時）**（`:61-99`）

- 觸發：在 `TextEntered` 收到換行字元時執行（`:34-41`）。
- 比對對象：**上一行**（游標所在行的前一行）是否符合：`^([ \t]*)([-*+]|\d+\.)(\s\[[ xX]\])?(\s.*)?$`（`:13-15`）。
  - 注意：有序清單只接受 `1.` 形式，不接受 `1)`（與分色規則不同，見第 11 章）。
- 若上一行標記之後沒有內容（去除空白後為空）：
  - 刪除上一行的文字（保留換行，因此會留下一個空白行），不再產生新標記（`:77-83`）。
- 否則，新行的前綴為「相同縮排 + 下一個標記 + 核取方塊 + 一個空白」：
  - 有序清單：數字加 1（例如 `3.` 變 `4.`）。
  - 無序清單（`-`、`*`、`+`）：標記不變（`:85-89`）。
  - 核取方塊：若上一行有 `[ ]` 或 `[x]`，新行一律加 ` [ ]`（未勾選）（`:90`）。
  - 縮排：AvalonEdit 已先複製上一行的前導空白，因此會把它整段取代，避免巢狀清單每按一次就多一層（`:92-97`）。
  - 游標移到新前綴之後（`:98`）。

**B. 符號自動替換（每次輸入一般字元後）**（`:101-131`）

- 觸發：每次 `TextEntered`（非換行）後，檢查游標之前的文字是否剛好以觸發字串結尾。
- 規則，依下列順序，第一個命中即替換（`:17-26`）：

| 觸發字串 | 替換為 |
|---|---|
| `<->` | ↔ |
| `->` | → |
| `=>` | ⇒ |
| `>=` | ≥ |
| `<=` | ≤ |
| `...` | … |
| `--` | — |

- 例外（`LooksLikeStructuralDashes`，`:119-123`、`:133-138`）：`--` 若同一行中，它之前的內容只包含 `-`、`:`、`|` 與空白，則不替換。目的是保留 `---` 分隔線與表格分隔列。

**C. 替換後的撤銷（Backspace）**（`:46-58`）

- 剛完成的替換，若游標仍在替換文字結尾，按 Backspace 會還原為原始觸發字串，且該按鍵被標記為已處理。
- 任何其他按鍵都會清除此狀態。

**D. 不在助理內的行為**

- 沒有自動配對括號、沒有自動補齊標題符號、沒有 Tab 縮排規則。這些若存在，屬於 AvalonEdit 預設行為（原始碼未指定）。

---

## 7. 狀態列

- 位置與外觀：見 1-5。背景 `SurfaceBrush`；前景 `SecondaryTextBrush`（`MainWindow.xaml.cs:544-547`）。
- 內容由 `UpdateStatus()` 更新（`:2440-2474`）。更新時機包括文字變更、選取變更、游標移動（經由 `ViewChanged`）、分頁切換、群組切換、儲存完成等。

### 7-1 由左至右的格子

**左側（填滿剩餘寬度）：檔案所在資料夾（`_statusText`）**

- 內容：`Path.GetDirectoryName(檔案路徑)`；未命名文件或無路徑時為空字串（`:2456-2457`）。
- 工具提示：完整檔案路徑（`:2458`）。
- 格式：`TextBlock`，Margin `8,0,20,0`，`TextTrimming = CharacterEllipsis`，垂直置中（`:550-555`）。
- 超出寬度時從字元中間截斷（以 `...` 表示）。

**右側群組（靠右對齊，由左至右依序）（`:600-612`）**

1. **文件資訊（`_documentInfoText`）**（Margin `8,0,8,0`，`:556`）
   - 格式：`{狀態} · {編碼} · {換行符號}`（`:2460`）。
   - 狀態的決定：
     - 文件已修改：`status.modified`。
     - 未修改：若有覆寫文字（例如剛自動儲存），顯示覆寫文字；否則 `status.saved`（`:2459`）。
     - 覆寫文字的來源：`status.autoSaved`（儲存完成後，見 8-7）、`snapshot.created`、`snapshot.unchanged`（手動建立快照後，`:1822`）。
   - 沒有任何分頁時：顯示覆寫文字或 `status.noFile`，其餘格子清空（`:2446-2454`）。
2. 分隔線（`Separator`）。
3. **字數（`_characterCountText`）**（Margin `8,0,8,0`，前景 `SecondaryTextBrush`，`:558-563`）
   - 格式：`{status.characters} {總字數}`。
   - 有選取文字時追加：` · {status.selectedCharacters} {選取字數}`（`:2470-2472`）。
   - 沒有分頁時：`{status.characters} —`（`:2452`）。
   - 工具提示：目前的計數模式名稱（`:2453`、`:2476-2481`）。
   - 計數模式：`status.countMode.includeWhitespace`（預設）、`status.countMode.excludeWhitespace`、`status.countMode.excludeLineBreaks`（`AppSettings.cs:158` 預設 `IncludeWhitespace`）。
   - 計數規則本身（`TextMetrics.CountCharacters`）：原始碼未指定（未閱讀）。
4. 分隔線。
5. **位置（`_positionText`）**（Margin `8,0,8,0`，`:557`、`:2461-2464`）
   - 格式：`Ln {行號}, Col {欄號}`，兩者皆從 1 開始；欄號 = 游標位移 − 行首位移 + 1。
   - 沒有分頁時為空字串。
   - **注意**：`Ln` 與 `Col` 是寫死的英文，沒有經過 localization（`:2464`）。見第 11 章。
6. 分隔線。
7. **縮放滑桿（`_zoomSlider`）**（`:565-578`）
   - 寬 110；垂直置中。
   - 範圍 50 至 200；`TickFrequency 10`；`IsSnapToTickEnabled = true`；`SmallChange` 與 `LargeChange` 皆為 10。
   - 數值 = 縮放值 × 100。
   - 變更時呼叫 `SetEditorZoom(數值 / 100)`（`:579`）。
   - 樣式：`StatusBarSliderStyle`（資源）。
   - 工具提示：`status.zoom`。
8. **縮放百分比（`_zoomPercentText`）**（`:580-589`）
   - 寬 44；右對齊；Margin `6,0,10,0`；垂直置中。
   - 文字：`{Math.Round(縮放 × 100)}%`。
   - 雙擊：重設為 100%（`:590-596`）。
   - 工具提示：`status.zoom`。

- 注意：分隔線只有 3 條，位於文件資訊、字數、位置之間；滑桿與百分比之間沒有分隔線（`:606-612`）。

### 7-2 互動

- 縮放滑桿：拖曳或鍵盤方向鍵，以 10% 為單位變動。
- 百分比文字：雙擊回到 100%。
- 檔案路徑：只有工具提示，沒有點擊行為（原始碼未指定點擊行為）。
- 文件資訊、字數、位置：純顯示，沒有互動。

### 7-3 覆寫文字的停留時間

- 覆寫文字沒有計時器，會一直顯示到下一次 `UpdateStatus()` 被呼叫為止（推斷）。

---

## 8. 視窗層級行為

### 8-1 啟動流程

**應用程式層（`App.xaml.cs:22-44`）**

1. 註冊 UI 執行緒與 AppDomain 的未處理例外記錄（`:25-26`）。
2. 建立具名 Mutex `Glystrata-SingleInstance-3F1E2C7A`（`:15`、`:28`）。
   - 若已存在（不是第一個執行個體）：把命令列參數轉交給既有視窗（見 8-5），然後結束本程序（`:29-35`）。
3. 第一個執行個體：呼叫 `SnapshotFileAssociation.EnsureRegistered()`（寫入登錄檔，見第 10 章，`:38`）。
4. 建立 `MainWindow(e.Args)`，設為 `MainWindow`，啟動命名管道伺服器（見 8-5），然後 `Show()`（`:40-43`）。

**主視窗建構子（`MainWindow.xaml.cs:68-93`）**

1. 保存啟動參數（去除空白、去除空白項目）（`:70-73`）。
2. `InitializeComponent`；附加自訂標題列（`:74-75`）。
3. 註冊拖放事件（`:76-78`）。
4. 建立預設根節點（一個空窗格）（`:79-80`）。
5. 同步讀取設定（`LoadSettingsForStartup`），套用語系與主題（`:86-89`）。
6. 建立指令表（`:91`），建立空白版面（`BuildShell`，`:92`）。

**載入完成後（`Window_Loaded` → `LoadWorkspaceAsync`，`:156-165`、`:338-364`）**

1. 讀取群組；若沒有任何群組，建立一個名為 `sidebar.groups` 的群組（`:340-345`）。
2. 讀取最近使用的檔案（`:347`）。
3. 讀取工作階段（`:349`）。
4. 若工作階段含有有效的窗格樹，採用之；否則保留單一空窗格（`:350-355`）。
5. 設定作用中窗格為第一個窗格（`:355`）。
6. 重建外殼（`BuildShell`），還原分頁（`RestoreViews`），清除空窗格，重建窗格版面（`:356-359`）。
7. 啟動計時器（快照計時器，30 秒，見 8-7）（`:360`、`:2149-2156`）。
8. 更新狀態列（`:361`）。
9. 開啟命令列參數中的檔案（`OpenStartupFiles`，`:362`、`:224-232`）。
10. 背景執行緒處理快照邊車檔的隱藏屬性（`SweepSnapshotVisibility`，`:363`、`:366-383`）。

### 8-2 還原上次工作階段

**還原的內容（`CreateSessionState`，`:2527-2545`；`RestoreViews`，`:2547-2588`）**

- 窗格樹：節點 Id、是否分割、方向、比例、兩個子節點 Id。
- 分頁（只包含有檔案路徑的分頁）：
  - 分頁 Id、檔案路徑、所屬群組、所在窗格、游標位移、水平與垂直捲動位移。
- 作用中分頁 Id。

**還原規則：**

- 檔案不存在：略過（`:2554-2556`）；讀取時拋出 `IOException` 也略過（`:2574-2577`）。
- 同一路徑出現第二次：以新檢視開啟（共用同一份文件）（`:2559-2560`）。
- 窗格 Id 不存在於目前版面：改為第一個窗格（`:2558`）。
- 游標與捲動位置：還原（`:2564-2566`）。
- 還原的檔案會加入最近使用清單（`AddIfMissing`，`:2572`）。
- 作用中分頁：若能還原，設為啟用，並把作用中窗格設為該分頁所在窗格（`:2580-2587`）。

**不還原的內容（依程式推斷）：**

- 未命名文件（沒有路徑）：`CreateSessionState` 只寫入有路徑的分頁（`:2531`），因此未命名文件的內容**不會**在重新啟動後還原（推斷，見第 11 章）。
- 選取的群組（`_selectedGroupId`）：工作階段中沒有此欄位，啟動時為「全部 TAB」（推斷）。
- 縮放比例：不保存，啟動時為 100%（見 6-2）。
- 側欄與格式工具列的顯示狀態：由設定決定，不屬於工作階段。

**保存時機（`ScheduleSessionSave`，`:2483-2494`）：**

- 任何相關變更後 700 ms 才寫入（防彈跳，同一計時器重新計時）。
- 寫入內容：設定、群組、工作階段、最近使用檔案（`PersistWorkspaceAsync`，`:2512-2525`）。
- 寫入失敗：忽略，不中斷程式（`:2521-2524`）。
- 視窗尚未載入或正在關閉時，不排程（`:2485`）。

### 8-3 關閉時未儲存的詢問流程（`Window_Closing`，`:2751-2807`）

**逐一詢問：**

- 對 `_documents.Documents` 中每一個「已修改」的文件，依序彈出對話框（不只是目前分頁）。
- 對話框：
  - 標題：`dialog.unsavedTitle`。
  - 內容：`dialog.unsavedMessage`。
  - 三個按鈕，回傳值依序為：`0` = `file.save`（儲存）、`1` = `dialog.dontSave`（不儲存）、`2` = `dialog.cancel`（取消）。
  - 回傳值 `-1`（關閉對話框）、Esc、取消：都視為取消關閉，不得當成確認（`:2767-2772`）。
  - 按鈕回傳值與順序的對應，依呼叫端推斷（`MessageDialogs.Show` 未閱讀）。
- 選擇「儲存」：`SaveDocumentSynchronously`（`:2775`）。
  - 文件沒有路徑：彈出「另存新檔」對話框，預設檔名 `untitled.md`，篩選為 `Markdown`、`YAML`、`All files`（`:2721-2730`）。
  - 使用者取消對話框：取消關閉（`:2730-2733`）。
  - 儲存失敗，或儲存後仍為已修改：取消關閉（`:2775-2779`）。
- 選擇「不儲存」：繼續下一個文件（不寫入磁碟）。

**全部通過後：**

1. 設定 `_isClosing = true`（`:2783`）。
2. 停止快照、工作階段、自動儲存、外部變更檢查等計時器（`:2784-2793`）。
3. 釋放所有檔案監看器（`:2794-2797`）。
4. 關閉所有閱讀器預覽視窗（`:2798`）。
5. 同步寫入工作階段（在背景執行緒執行，UI 執行緒等待完成，`:2800-2805`）。
6. 釋放文件管理器（`:2806`）。

- 「結束」選單與視窗關閉按鈕走同一條流程（`:107`）。

### 8-4 檔案拖放進視窗（`MainWindow.xaml.cs:167-222`）

- 整個視窗都是放置區：`AllowDrop = true`，在視窗層級攔截 `PreviewDragOver` 與 `PreviewDrop`（`:76-78`）。
- 只處理包含 `FileDrop` 格式的拖曳（檔案）。其他拖曳（分頁、群組、文字）不攔截，交給子元素處理（`:169-176` 註解）。
- 拖曳經過：
  - 若拖曳資料中至少有一個路徑：效果 `Copy`；否則 `None`。
  - 會把事件標記為已處理（`:179-182`）。
- 放下：
  1. 排除不存在的路徑（例如資料夾）（`:204-206`）。
  2. 加入「目前群組」：`ResolveCurrentGroup`，順序為（`:286-299`）：
     - 側欄目前選取的群組。
     - 作用中分頁所屬的群組。
     - 第一個群組（沒有則建立預設群組）。
  3. 每個檔案：加入群組清單，並以 `selectGroup: false` 開啟（`:209-210`）。
  4. 若有開啟任何檔案：重建側欄，排程工作階段儲存（`:214-218`）。
  5. 效果：有開啟則 `Copy`，否則 `None`；標記為已處理（`:220-221`）。
- 拖到某個群組列上：群組列的 `DragOver` 會因為視窗層級已標記為已處理而不會執行（WPF 的 Preview 事件特性，推斷）。因此檔案會加入「目前群組」，而不是游標所在的群組（推斷，見第 11 章）。

### 8-5 單一執行個體與從外部開檔

**第二次啟動（`App.xaml.cs:46-66`）：**

1. 呼叫 `WindowActivation.RaiseOtherInstance()`，由新程序代為把既有視窗拉到前景（必須在新程序仍持有前景權限時執行，見第 10 章）。
2. 透過命名管道 `Glystrata-SingleInstance-Pipe-3F1E2C7A` 逐行送出參數（連線逾時 2000 ms；失敗則放棄，只是使用者需要自己在既有視窗開檔）。
3. 新程序結束。

**既有程序的管道伺服器（`App.xaml.cs:68-105`）：**

- 持續等待連線；每次連線讀取所有行，收集為路徑清單。
- 在 UI 執行緒執行：
  1. 一定呼叫 `BringToFront()`，即使沒有任何路徑（`:87-89` 註解：第二次啟動已決定退出，若不把視窗拉前，使用者會覺得雙擊沒有反應）。
  2. 若有路徑：呼叫 `OpenExternalPaths`。
- 連線中斷：忽略，繼續等待下一次（`:99-102`）。

**`OpenExternalPaths`（`MainWindow.xaml.cs:261-284`）：**

- 去除空白、去除重複（不分大小寫）、只保留存在的檔案。
- 沒有任何有效路徑：直接返回（視窗已在前面）。
- 有效路徑：呼叫 `BringToFront()`，依序以 `selectGroup: false` 開啟，加入「目前群組」，重建側欄，排程工作階段儲存。

**`BringToFront`（`MainWindow.xaml.cs:237-257`）：**

- 沒有視窗控制代碼（尚未建立）：若最小化則還原，然後 `Activate()`。
- 有視窗控制代碼：`WindowActivation.RestoreAndRaise`（僅在最小化時還原，然後設為前景），再 `Activate()`。
- 「僅在最小化時還原」是為了避免把使用者最大化的視窗變回一般大小（`WindowActivation.cs:74-75`）。

### 8-6 外部檔案變更偵測

**監看器（`ConfigureWatcher`，`:2280-2315`）：**

- 每個有路徑的文件，對其所在資料夾建立 `FileSystemWatcher`，篩選條件為該檔名，不含子資料夾（`:2300-2305`）。
- 監看的變更類型：最後寫入時間、大小、檔名（`:2302`）。
- 監看的事件：`Changed`、`Created`、`Renamed`（`:2306-2308`）。
- 檔案路徑改變時重新建立監看（`:2248-2252`）。
- 建立失敗（`IOException`、`UnauthorizedAccessException`）：忽略，仍可編輯與儲存（`:2311-2314`）。

**防彈跳與檢查（`ScheduleExternalCheck`、`CheckExternalChange`，`:2317-2387`）：**

1. 任何監看事件後，啟動 500 ms 計時器；每次事件重新計時（`:2337`）。
2. 計時到期：讀取檔案指紋（`FileFingerprint`）。檔案不存在則返回（`:2351-2353`）。
3. 若指紋與「上次儲存的指紋」相同，或與「上次已提示過的外部指紋」相同：不提示（`:2364-2366`）。
4. 否則記錄為已知的外部指紋（避免重複提示），並彈出對話框：
   - 標題：`dialog.externalChangeTitle`；內容：`dialog.externalChangeMessage`。
   - 按鈕：`0` = `dialog.reload`（重新載入）、`1` = `dialog.compare`（比較）、`2` = `dialog.keepLocal`（保留本機版本）。
   - Esc 或關閉對話框：與「保留本機版本」相同，不動內容（`:2386`）。

**三個選項的行為：**

- 重新載入（`ReloadDocument`，`:2389-2407`）：
  - 直接讀取磁碟內容，覆蓋編輯器中的內容，清除變更時間，重新整理窗格（`:2397-2401`）。
  - **沒有任何二次確認**，即使本機有未儲存的修改（程式碼明確可見）。
- 比較（`CompareExternalDocument`，`:2409-2425`）：開啟比較視窗，左邊為目前內容，右邊為磁碟內容。
- 保留本機版本：不做任何事。

**儲存後的處理：** `SaveDocumentAsync` 成功後會移除該文件的已知外部指紋（`:1624`）。

**外部刪除或改名：** 事件會觸發檢查，但檔案不存在時直接返回，因此不會提示（`:2351-2353`，推斷）。

### 8-7 自動儲存與自動快照的計時

| 項目 | 計時 | 觸發條件 | 行為 | 來源 |
|---|---|---|---|---|
| 自動儲存 | 最後一次文字變更後 1 秒，每次變更重新計時 | 有檔案路徑，且已修改；未命名文件不自動儲存 | 呼叫 `SaveDocumentAsync`；狀態列暫時顯示 `status.autoSaved` | `:2255-2278`（1 秒在 `:2264`） |
| 工作階段儲存 | 任何相關變更後 700 ms，每次重新計時 | 視窗已載入且未關閉 | 寫入設定、群組、工作階段、最近使用檔案 | `:2483-2494` |
| 外部變更檢查 | 監看事件後 500 ms，每次重新計時 | 有檔案路徑 | 見 8-6 | `:2337` |
| 快照計時器 | 每 30 秒檢查一次 | 有檔案路徑；距離最後一次文字變更已達快照間隔；且目前文字與上次快照不同 | 建立快照，每檔最多保留 `MaxSnapshotsPerFile` 份，超出的刪除 | `:2149-2185`（30 秒在 `:2152`） |
| 快照間隔（設定） | — | — | 預設 5 分鐘；範圍 1 至 120 | `AppSettings.cs:156`、`:30`（Core） |
| 每檔快照上限（設定） | — | — | 預設 20 份；範圍 1 至 200 | `AppSettings.cs:157`、`:31`（Core） |
| 手動建立快照 | 立即 | 有檔案路徑 | 成功顯示 `snapshot.created`；內容沒變顯示 `snapshot.unchanged` | `:1811-1828` |

- 快照判斷「距離最後一次變更」的時間來源是 `_lastChangedUtc`，只在文字變更與還原時更新（`:2223`、`:1895`、`:2399`）。
- 快照計時器只會在「閒置達到間隔」時建立快照，活躍編輯時不會建立（由程式邏輯可直接看出）。
- 快照上限的刪除邏輯位於 `SnapshotService`，本次未閱讀（原始碼未指定）。

### 8-8 儲存流程與狀態訊息

- 手動儲存（`Ctrl+S`）：`SaveActiveAsync`（`:1551-1571`）。沒有路徑時改走「另存新檔」（`:1560-1564`）。
- 另存新檔：`SaveActiveAsAsync`（`:1573-1612`）。
  - 對話框篩選：`Markdown|*.md|YAML|*.yaml;*.yml|All files|*.*`（`:1584`）。
  - 預設檔名：未命名為 `untitled.md`，否則沿用檔名（`:1585`）。
  - 成功後：重新掛載文件、重建群組樹、更新窗格標頭、加入最近使用、排程工作階段儲存、更新狀態列（`:1601-1606`）。
- 儲存成功（包含手動與自動）：更新標頭、清除已知外部指紋、狀態列顯示 `status.autoSaved`，排程工作階段儲存（`:1614-1633`）。
  - 注意：手動儲存與自動儲存都會顯示「已自動儲存」的文字（`SaveDocumentAsync` 為共用路徑，推斷）。
- 儲存失敗：彈出 `error.save`，內容為例外訊息或結果訊息（`:1635-1652`）。關閉過程中不彈出（`:1647-1649`）。

### 8-9 開啟檔案的流程（`OpenPath`，`:1505-1532`）

- 若指定群組：把該群組設為選取（僅當 `selectGroup` 為真時）。
- 開啟文件（`_documents.OpenView`）、掛載、設定為作用中、重建版面、焦點移到編輯器、更新狀態列、加入最近使用、排程工作階段儲存（`:1514-1522`）。
- 失敗（`IOException`、`UnauthorizedAccessException`、`InvalidDataException`、`UnsupportedTextEncodingException`）：
  - 從最近使用清單移除該路徑（讓已被刪除的檔案不再出現）。
  - 排程工作階段儲存。
  - 彈出 `error.open` 對話框，內容為例外訊息（`:1524-1530`）。
- 開啟對話框：`Multiselect = true`；篩選 `Markdown and text|*.md;*.markdown;*.mdown;*.yaml;*.yml;*.txt|All files|*.*`（`:1487-1493`）。
- 同一檔案重複開啟時是否共用文件：原始碼未指定（`DocumentManager` 未閱讀）。

### 8-10 錯誤處理與記錄

- UI 執行緒未處理例外與 AppDomain 未處理例外：寫入 `%LOCALAPPDATA%\Glystrata\errors.log`，附時間戳（ISO 格式）與完整例外內容（`App.xaml.cs:107-149`）。
- Win32 錯誤碼 1816（桌面資源不足）：標記為已處理，改彈出雙語（繁中與英文）警告，說明編輯內容仍保留（`App.xaml.cs:107-121`）。

### 8-11 語系與主題變更

- 語系變更（`Localization_LanguageChanged`，`:2116-2125`）：更新標題列文字、重建外殼（但套用設定時不重建，`:2119`）、更新側欄標題、更新預覽視窗文字。
- 主題變更（`Theme_ThemeChanged`，`:2127-2145`）：更新標題列底色、所有窗格的編輯器配色、預覽視窗主題、全部 TAB 按鈕與群組選取色、側欄標題。
- 套用設定（`ApplySettings`，`:1976-2003`）：正規化設定、重新映射群組色、必要時重新掃描快照邊車檔的隱藏屬性、套用語系與主題、重建外殼、更新預覽設定、排程工作階段儲存、非同步保存設定。

---

## 9. 快速鍵表

### 9-1 指令表（`Input/AppCommand.cs:51-81`）

判斷規則：`ShortcutService.Resolve` 以「按鍵」加「修飾鍵完全相同」比對；修飾鍵多一個或少一個都不算符合（`ShortcutService.cs:46-61`）。

| 指令（AppCommandId） | 選單文字 key | Windows 按鍵 | GestureOnly | 備註 |
|---|---|---|---|---|
| FileNew | `file.new` | Ctrl+N | 否 | |
| FileOpen | `file.open` | Ctrl+O | 否 | |
| FileSave | `file.save` | Ctrl+S | 否 | |
| FileSaveAs | `file.saveAs` | Ctrl+Shift+S | 否 | |
| FileSnapshotHistory | `file.snapshotHistory` | Ctrl+Shift+H | 否 | |
| FileCreateSnapshot | `file.createSnapshot` | Ctrl+Alt+S | 否 | |
| FileExit | `file.exit` | 無 | 否 | 選單項目無快速鍵 |
| SettingsOpen | `settings.open` | Ctrl+, | 否 | `OemComma` |
| EditUndo | `edit.undo` | Ctrl+Z | **是** | 視窗不攔截，交給 AvalonEdit |
| EditRedo | `edit.redo` | Ctrl+Y | **是** | 視窗不攔截，交給 AvalonEdit |
| EditFindReplace | `edit.findReplace` | Ctrl+F | 否 | |
| EditReplace | 無（無選單） | Ctrl+H | 否 | 只有快速鍵 |
| ViewSplitHorizontal | `view.splitHorizontal` | Ctrl+\ | 否 | `Oem5` |
| ViewSplitVertical | `view.splitVertical` | Ctrl+Alt+\ | 否 | `Oem5` |
| ViewClosePane | `view.closePane` | Ctrl+Shift+W | 否 | |
| ViewResetLayout | `view.resetLayout` | Ctrl+Alt+1 | 否 | `D1` |
| ViewNewView | `view.newView` | Ctrl+Alt+N | 否 | |
| ViewToggleReader | 無（無選單） | Ctrl+Shift+R | 否 | 只有快速鍵 |
| ViewCloseTab | 無（無選單） | Ctrl+W | 否 | 只有快速鍵 |
| GroupNew | `group.new` | Ctrl+Shift+G | 否 | |
| GroupOpenFile | `group.openFile` | 無 | 否 | |
| HelpMarkdownGuide | `help.markdownGuide` | 無 | 否 | |
| HelpAbout | `help.about` | 無 | 否 | |

- 表中的按鍵組合沒有重複（由人工逐列比對；`ShortcutService` 的衝突記錄機制也會把重複記錄在 `Conflicts`，`ShortcutService.cs:13-35`）。
- 只有 Ctrl+Z 與 Ctrl+Y 是 GestureOnly，其餘都由視窗層攔截。

### 9-2 視窗層攔截流程（`MainWindow.xaml.cs:314-336`）

1. `PreviewKeyDown` 在視窗層執行，早於編輯器接收按鍵。
2. 若按鍵是 `ImeProcessed`：不攔截（`:318-321`；注音等輸入法組字中的字母鍵不可被當成快速鍵）。
3. 若 `Keyboard.Modifiers == None`：不攔截（`:324-327`；沒有修飾鍵的按鍵一律不是快速鍵）。
4. 以 `ShortcutService.Resolve` 查表；Alt 組合在 WPF 中會以 `Key.System` 送出，改用 `SystemKey` 取得實際按鍵（`ShortcutService.cs:50`）。
5. 找到且有對應動作：執行並標記為已處理。GestureOnly 不在表中，因此不會被攔截。

### 9-3 其他地方攔截的按鍵

| 按鍵 | 位置 | 行為 | 來源 |
|---|---|---|---|
| Ctrl+滾輪 | 編輯器（`PreviewMouseWheel`） | 縮放 ±0.1，範圍 0.5 至 2.0；需修飾鍵恰好為 Control | `EditorPaneControl.cs:288-297` |
| 滾輪（分頁列上，分頁列有溢出時） | 分頁列的 `TabStripScrollViewer` | 轉為水平捲動 | `EditorPaneControl.cs:688-697` |
| Enter | 編輯器（`TextEntered`） | 清單續行或清除空清單標記 | `MarkdownTypingAssistant.cs:34-41`、`:61-99` |
| Backspace | 編輯器（`PreviewKeyDown`） | 剛完成的符號替換，按一次還原 | `MarkdownTypingAssistant.cs:46-58` |
| Esc | 對話框（`DialogKeys.AttachEscapeToClose`、`AttachConfirmCancel`） | 關閉視窗 | `Controls/DialogKeys.cs:6-36` |
| Enter | 對話框（`DialogKeys.AttachConfirmCancel`） | 確認；焦點在多行文字框（`AcceptsReturn`）時不攔截，改為換行 | `Controls/DialogKeys.cs:19-36` |
| Ctrl+Tab | 全域搜尋 `Key.Tab` 無結果 | 原始碼未攔截，採 WPF 預設行為 | 搜尋結果為空 |

- `DialogKeys` 被以下視窗或對話框使用：`InputDialogs`、`ColorPickerDialog`、`DiffWindow`、`MarkdownHelpWindow`、`SnapshotHistoryWindow`、`SettingsWindow`（搜尋結果）。
- 編輯器內建的快速鍵（複製、貼上、全選、方向鍵、Tab 縮排等）：原始碼未指定（AvalonEdit 預設）。

---

## 10. Windows 專屬、Mac 需另尋做法的部分

以下每一項都只說明 Windows 版做了什麼，Mac 上的對應做法本次不提供建議，列為待確認項目。

| 項目 | 位置 | Windows 版做了什麼 | Mac 需另尋做法 |
|---|---|---|---|
| WindowChrome 自訂標題列 | `CustomTitleBar.cs:60-69` | 用 `WindowChrome` 把標題列換成 32 px 自繪列，並保留可調整大小的邊框（6 px）。 | 需確認原生標題列與交通燈按鈕的處理方式。 |
| 視窗控制鈕（Segoe 字符） | `CustomTitleBar.cs:12-15`、`:141-158` | 最小化、最大化、關閉以 Segoe Fluent Icons 字符繪製，每顆 46 × 32。 | 需另尋（字元與字型不適用）。 |
| 最大化時的外溢邊距 | `CustomTitleBar.cs:17-18`、`:201-209` | 以 `GetSystemMetricsForDpi`（SM 32 與 SM 92）計算最大化時多出的框架寬度，並換算 DIP。 | 推斷不適用，需確認。 |
| 最大化鈕的命中測試 | `CustomTitleBar.cs:305-311`、`:339-354` | 攔截 `WM_NCHITTEST`，若落在最大化鈕範圍內回傳 `HTMAXBUTTON`（9）。 | 推斷與 Windows 11 Snap Layouts 有關（註解未明寫）。 |
| 最大化鈕的 hover 與 pressed 色 | `CustomTitleBar.cs:312-335`、`:356-370` | 攔截 `WM_NCMOUSEMOVE`、`WM_NCLBUTTONDOWN`、`WM_NCLBUTTONUP`，手動切換背景資源。 | 同上。 |
| 合成背景色 | `CustomTitleBar.cs:234-261` | 把 `HwndSource.CompositionTarget.BackgroundColor` 設為視窗背景色，避免 Windows 11 將未繪製區域顯示為黑色。 | 需另尋。 |
| 強制重繪 | `CustomTitleBar.cs:263-285` | 以 `RedrawWindow` 在 Loaded 與 ApplicationIdle 時強制重繪，避免對話框開啟後不出畫面。 | 需另尋。 |
| 系統深色模式變更 | `CustomTitleBar.cs:291-296`；`Services/SystemThemeWatcher.cs:23`（讀 HKCU 的 Personalize 機碼，本次未閱讀該檔） | 收到 `WM_SETTINGCHANGE`（`ImmersiveColorSet`）後通知主題監看器。 | 需另尋系統外觀變更的通知方式。 |
| 單一執行個體 | `App.xaml.cs:15-16`、`:28-36`、`:46-105` | 具名 Mutex 判斷是否已有執行個體；具名命名管道轉交參數；第二個程序結束。 | 需另尋。 |
| 前景視窗控制 | `Services/WindowActivation.cs` 全檔 | 呼叫 `AllowSetForegroundWindow`、`SetForegroundWindow`、`ShowWindow(SW_RESTORE)`、`IsIconic`，並以 `Process.GetProcessesByName` 找既有程序。Windows 只允許前景程序轉交前景權限，因此由第二個程序代為拉起既有視窗（檔案註解 `:5-14`）。 | 需另尋。 |
| 檔案關聯（.gss） | `Services/SnapshotFileAssociation.cs:10-63` | 在 `HKCU\Software\Classes` 寫入 `.gss` 的 ProgId `Glystrata.SnapshotFile`、`DefaultIcon`，並以 `SHChangeNotify` 通知 Explorer 刷新。只註冊 `.gss`，`.md` 與 `.yaml` 的關聯未在程式中設定（由程式碼可直接看出）。 | 需另尋。 |
| 快照圖示檔 | `SnapshotFileAssociation.cs:65-88` | 把資源中的 `SnapshotFile.ico` 寫到 `%LOCALAPPDATA%\Glystrata\icons\`。 | 需另尋。 |
| 在資料夾中顯示 | `EditorPaneControl.cs:550-565` | `Process.Start("explorer.exe", "/select,\"路徑\"")`。 | 需另尋。 |
| 字型回退鏈 | `EditorPaneControl.cs:274`；`CustomTitleBar.cs:146`；`Preview/WpfMarkdownRenderer.cs:37`、`:135`、`:367`（未閱讀） | 編輯器字型為 `Cascadia Code, Consolas, Segoe UI Emoji`；標題列為 Segoe Fluent Icons 與 Segoe MDL2 Assets。 | 需另尋字型。 |
| 滑鼠滾輪單位 | `EditorPaneControl.cs:288-297`、`:688-697` | 使用 WPF 的 `Delta`（每格 120）。 | 推斷觸控板與滾輪的行為差異，需確認。 |
| 拖曳門檻 | `MainWindow.xaml.cs:947-948`；`EditorPaneControl.cs:465-466` | 使用 `SystemParameters.MinimumHorizontalDragDistance` 與 `MinimumVerticalDragDistance`（系統值）。 | 原始碼未指定數值。 |
| 修飾鍵 | `Input/AppCommand.cs` 全檔；`ShortcutService.cs:50` | 所有快速鍵以 Ctrl 為主；Alt 組合透過 `Key.System` 與 `SystemKey` 取得按鍵。 | 需決定修飾鍵的對應方式（待確認）。 |
| 檔案對話框 | `MainWindow.xaml.cs:1487-1493`、`:1582-1588`、`:1902-1908`、`:2077-2082`、`:2723-2729` | 使用 `Microsoft.Win32.OpenFileDialog` 與 `SaveFileDialog`，篩選字串為 WPF 格式。 | 需另尋。 |
| 檔案拖放格式 | `MainWindow.xaml.cs:167-222`；`DropIndicator.cs:96-109` | 使用 `DataFormats.FileDrop` 與 WPF 的 `DragDrop.DoDragDrop`，自訂格式為 `GlystrataGroupId`、`GlystrataTabViewId`。 | 需另尋。 |
| 應用程式資料路徑 | `Glystrata.Core/Persistence/JsonStateStore.cs:33`；`App.xaml.cs:135-137`；`SnapshotFileAssociation.cs:68-70` | 使用 `Environment.SpecialFolder.LocalApplicationData`，即 `%LOCALAPPDATA%\Glystrata\`。 | 需另尋。 |
| 隱藏屬性（快照邊車檔） | `Glystrata.Core/Snapshots/SnapshotSidecarStore.cs:211-217`；`Glystrata.Core/Groups/GroupManager.cs:176-179` | 以 `FileAttributes.Hidden` 隱藏 `.gss` 邊車檔，並在列出檔案時過濾。 | 需另尋（推斷 Mac 沒有相同屬性）。 |
| 唯讀屬性 | `Glystrata.Core/Documents/FilePersistenceService.cs:20`（未閱讀） | 以 `FileAttributes.ReadOnly` 判斷是否可寫入。 | 需另尋。 |
| 輸入法（IME） | `Editing/ImeComposition.cs`、`ImeCompositionLayer.cs`、`ImeHwndHook.cs`、`ImeNativeMethods.cs` | 攔截 Win32 IME 訊息，讓輸入法不另開組字視窗，改由編輯器自行繪製組字內容（依 `ImeHwndHook.cs` 註解）。 | Mac 需另尋（本次不深入）。 |
| 桌面資源不足例外 | `App.xaml.cs:107-121` | 處理 Win32 錯誤碼 1816，以 `MessageBox` 顯示警告。 | 推斷 Mac 不適用，需確認。 |
| 螢幕置中與 DPI | `MainWindow.xaml:10`；`CustomTitleBar.cs:204-207` | `CenterScreen` 與 DPI 縮放（`VisualTreeHelper.GetDpi`）。 | 需另尋（以 Mac 的點數座標處理）。 |
| 檔案監看 | `MainWindow.xaml.cs:2280-2315` | 使用 `FileSystemWatcher`，監看目錄內單一檔名的最後寫入、大小與檔名變更。 | 需另尋。 |

---

## 11. 假設與不確定之處

### 11-1 推斷（原始碼未明寫）

- 主視窗未設定 `ResizeMode`，推斷為 WPF 預設 `CanResize`（`MainWindow.xaml:8-13`）。
- 分頁列高度、分頁列的捲動箭頭、按鈕 hover 與 pressed 樣式：由 App 資源字典決定，本次未閱讀。
- 狀態列高度、群組列高度、群組列內縮：由 WPF 或主題樣式決定。
- 標題列底部的 1 px 線（`GlassFrameThickness = 0,0,0,1`）是否可見，依 Windows DWM 而定，程式沒有直接繪製。
- 最大化鈕的 `WM_NCHITTEST` 回傳值與 Windows 11 Snap Layouts 有關（註解未明寫）。
- 分割時搬移的是 `_documents.Views` 列舉順序中的「最後一個」分頁，不是分頁列上視覺順序的最後一個（`MainWindow.xaml.cs:1363-1379`）。
- 跨窗格拖曳分頁不會被接受（`EditorPaneControl.cs:501-510` 以 `_views.ContainsKey` 過濾）。
- 關閉窗格後，分頁被併入「列舉順序中第一個其他窗格」，不一定是相鄰窗格（`MainWindow.xaml.cs:1444-1448`）。
- 檔案拖到群組列上時，因為視窗層的 `PreviewDragOver` 已標記為已處理，群組列的 `DragOver` 不會執行，檔案加入的是「目前群組」而非游標所在群組（`MainWindow.xaml.cs:167-183`）。
- 啟動時不還原選取群組，因此預設為「全部 TAB」（`CreateSessionState` 沒有此欄位，`:2527-2545`）。
- 未命名文件不被寫入工作階段（`CreateSessionState` 只寫有路徑的分頁，`:2531`），因此重新啟動後未命名內容不會還原。**此點需要確認**：`DocumentManager` 或其他儲存路徑可能另有機制，本次未閱讀。
- 縮放比例每次啟動回到 100%（工作階段與設定都沒有此欄位，`Glystrata.Core` 搜尋結果為空）。
- 自動儲存與快照只針對有檔案路徑的文件；未命名文件不會自動儲存。
- 格式工具列的 `SetFormattingToolbarVisible` 方法沒有任何呼叫端（死碼），因此執行中無法切換，只能由設定決定。
- `TabContextRequested` 事件會被觸發，但 `MainWindow` 沒有訂閱（無實際作用）。
- 規則之間的上色覆蓋順序，取決於 AvalonEdit 的 `ChangeLinePart` 行為（推斷為後套用者覆蓋先套用者）。
- 手動儲存與自動儲存都顯示 `status.autoSaved`，因為兩者共用 `SaveDocumentAsync`（`MainWindow.xaml.cs:1614-1627`）。
- 按鈕回傳值 `0`、`1`、`2` 與對話框按鈕的順序一致，依呼叫端推斷（`MessageDialogs.Show` 未閱讀）。
- 外部檔案被刪除時不會提示（檔案不存在即返回，`:2351-2353`）。
- 狀態列的覆寫文字沒有計時器，會持續到下一次 `UpdateStatus` 為止。

### 11-2 原始碼未指定（本次閱讀範圍外，或原始碼沒有寫出）

- 格式工具列各命令實際產生的 Markdown 文字（`MarkdownFormattingService` 未閱讀）。
- 最近使用檔案的清單上限（`RecentFilesService` 未閱讀）。
- 字數計算規則（`TextMetrics.CountCharacters` 未閱讀）。
- 群組名稱的重複規則、長度上限、是否去除前後空白（`GroupManager` 未閱讀）。
- 同一檔案重複開啟時是否共用文件（`DocumentManager` 未閱讀）。
- 所有 localization 的中文文字（`LocalizationService` 與資源檔未閱讀）。
- 主題色值（App 資源字典）與編輯器調色盤的預設色值（`EditorColorPalette` 未閱讀）。
- 工作階段、設定、群組、最近使用檔案的實際儲存路徑與格式（`JsonStateStore` 只讀到路徑）。
- 側欄與窗格的最小寬度、分割線的最小距離。
- 狀態列與分頁列的高度、捲動箭頭外觀。
- 編輯器的右鍵選單、內建快速鍵、拼字檢查等（AvalonEdit 預設）。
- 快照的刪除邏輯（`SnapshotService` 未閱讀）。

### 11-3 程式內的不一致與觀察（事實，由原始碼可直接核對）

1. **群組選取色的 tint 為 0 時**：`AppSettings.cs:24-25` 註解寫「0 會讓選取列不上色」，但 `GroupColors.cs:35-36` 在 `tint <= 0` 時回傳 `null`，`MainWindow.xaml.cs:927-928` 再以 `TabActiveBrush` 回退。實際行為是選取列仍有底色。
2. **一般文字的後備色不一致**：編輯器前景後備為 `#24292F`（`EditorPaneControl.cs:143`）；分色器遇到未知分類時後備為 `#1F2328`（`SyntaxHighlightingService.cs:150`）。
3. **有序清單標記不一致**：分色規則接受 `1.` 與 `1)`（`SyntaxHighlightingService.cs:18`、`:98`）；清單續行只接受 `1.`（`MarkdownTypingAssistant.cs:13-14`）。
4. **程式碼圍欄內容不上色**：分色器沒有跨行狀態（前置區塊除外），圍欄之間的文字只按一般規則上色（`SyntaxHighlightingService.cs:68-72`）。
5. **狀態列的 `Ln`、`Col` 寫死英文**：未經 localization（`MainWindow.xaml.cs:2464`）。
6. **外部變更的「重新載入」沒有二次確認**：即使本機有未儲存的修改，也會直接覆蓋（`MainWindow.xaml.cs:2389-2407`）。
7. **未命名文件的工作階段**：見 11-1 的推斷，需確認實際行為。
8. **SetFormattingToolbarVisible 為死碼**：見 11-1。
9. **TabContextRequested 無訂閱者**：見 11-1。
10. **YAML 的 `#` 行**：在 `.yaml` 檔案中，以 `# ` 開頭的行會被當成 Markdown 標題上色，而不是 YAML 註解（`SyntaxHighlightingService.cs:62-66`）。
11. **自動替換在 YAML 檔也生效**：`MarkdownTypingAssistant.Attach` 不區分檔案類型（`EditorPaneControl.cs:285`），因此 YAML 中的 `->`、`--` 等也會被替換。

### 11-4 需要人決定的事項（僅列出，不提供設計建議）

- 快速鍵的修飾鍵：Windows 版全部以 Ctrl 為主，Mac 版是否改用 Command、保留 Ctrl，或兩者並存。
- 標題列樣式：Mac 版要沿用自繪標題列，或使用原生標題列。
- 未命名文件是否要在重新啟動後還原（Windows 版目前推斷為不還原）。
- 群組選取色 tint 為 0 時的行為：要依註解「不上色」，或維持程式目前的回退行為。
- 狀態列的 `Ln`、`Col` 是否要改為 localization。
- 外部變更「重新載入」是否要加入未儲存修改的二次確認。
- 有序清單標記 `1)` 是否要讓續行功能支援（與分色規則一致）。
- 編輯器前景後備色 `#24292F` 與分色器後備色 `#1F2328` 是否統一。
- 格式工具列是否保留「預設隱藏、只能由設定開啟」的行為，或要支援執行中切換（目前的切換方法未被呼叫）。
- Windows 專屬的 `.gss` 檔案關聯、隱藏屬性、`%LOCALAPPDATA%` 路徑，在 Mac 上的對應做法。
