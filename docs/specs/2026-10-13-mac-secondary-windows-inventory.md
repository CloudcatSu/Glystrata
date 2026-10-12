# Glystrata 次要視窗與對話框規格盤點（Windows 原始碼對照）

- 日期：2026/10/13 06:55（Asia/Taipei）
- 目的：整理主視窗以外的所有視窗與對話框，供 macOS（Swift）原生版重做時逐項對照。
- 性質：唯讀盤點。本文件只描述 Windows 版原始碼「現在做了什麼」，不含設計建議。
- 原始碼根目錄：`/Users/cloudsu/ClaudeCode/Glystrata_Mac/src/`

## 共通約定

- 行號格式：`檔名:行號`。檔名在 repo 內唯一，對應路徑見各章節開頭的「來源」。例如 `SettingsWindow.xaml.cs:88` 指 `src/Glystrata/Views/SettingsWindow.xaml.cs` 第 88 行。
- 「原始碼未指定」代表該項目在讀過的檔案中沒有寫明，不是遺漏。
- 所有文字標籤都以 `LocalizationService` 的 key 表示（例如 `settings.reset`）。中文與英文的實際字串在 `src/Glystrata/Services/LocalizationService.cs` 的兩組字典中（繁中約第 8 行起，英文約第 224 行起），本文件未逐一抄錄。
- 「即時」指使用者在視窗內改動後，其他視窗或畫面立刻跟著變；「套用後」指要按確定（Apply）才生效。
- 主題相關的 brush 數值來自 `src/Glystrata/Resources/Themes/Light.xaml` 與 `Dark.xaml`。
- 本文件涵蓋的視窗與對話框：
  1. 設定視窗（SettingsWindow）
  2. 快照歷史視窗（SnapshotHistoryWindow）
  3. 比較視窗（DiffWindow）
  4. 尋找與取代視窗（FindReplaceWindow）
  5. Markdown 語法說明視窗（MarkdownHelpWindow）
  6. Markdown 閱讀器視窗（PreviewWindow ＋ PreviewWindowManager）
  7. 閱讀器渲染規則（WpfMarkdownRenderer）
  8. Emoji 處理（EmojiScanner ／ EmojiRenderer）
  9. 對話框（MessageDialogs、InputDialogs、ColorPickerDialog）
  10. 資料來源：Core 的 Markdown 與 Snapshots、AppSettings
  11. 假設與不確定之處
- 自訂標題列與按鍵輔助（`CustomTitleBar`、`DialogKeys`）不在指定清單內，但每個視窗都會呼叫，只在有必要處引用其行為（`src/Glystrata/Controls/CustomTitleBar.cs`、`DialogKeys.cs`）。標題列本身的外觀本文件未逐項整理。

---

## 1. 設定視窗（SettingsWindow）

- 來源：`src/Glystrata/Views/SettingsWindow.xaml`、`SettingsWindow.xaml.cs`、`src/Glystrata.Core/Persistence/AppSettings.cs`

### 用途與開啟方式

- 用途：編輯偏好設定（一般、顏色、閱讀器顏色、群組顏色、快照五個分頁），並可匯出／匯入設定檔。
- 開啟方式：
  - 快速鍵 `Ctrl+,`（`AppCommand.cs:60`，命令 id `SettingsOpen`，標籤 key `settings.open`）。
  - 主視窗命令表 `MainWindow.xaml.cs:108` 指向 `OpenSettings`。
  - 另有一個入口在 `MainWindow.xaml.cs:1070`（`manage.Click` 呼叫 `OpenSettings()`，該按鈕所在的選單位置原始碼未在本次讀取範圍內）。
  - `OpenSettings()` 位於 `MainWindow.xaml.cs:1969-1974`：`new SettingsWindow(_settings, _localization) { Owner = this }`，訂閱 `SettingsApplied` 後呼叫 `ShowDialog()`。

### 視窗屬性

- 標題：XAML 預設 `Preferences`（`SettingsWindow.xaml:4`），執行時由 `Title = _localization.Get("settings.title")` 覆寫（`SettingsWindow.xaml.cs:134`）。
- 預設大小：寬 720、高 660（`SettingsWindow.xaml:5-6`）。
- 最小大小：寬 620、高 520（`SettingsWindow.xaml:7-8`）。
- 可否調整大小：XAML 未設定 `ResizeMode`，為 WPF 預設（可調整）。原始碼未指定額外限制。
- 是否 modal：是。以 `ShowDialog()` 開啟（`MainWindow.xaml.cs:1973`）。
- 開啟位置：`WindowStartupLocation="CenterOwner"`（`SettingsWindow.xaml:9`），Owner 為主視窗。
- 能否同時開多個：modal 下實務上一次一個。原始碼未在 `OpenSettings` 內做重入防護。
- 背景：`WindowBackgroundBrush`（`SettingsWindow.xaml.cs:664-668` 以 `FindResource` 設定 Window 與 RootGrid 的背景）。
- Esc：關閉視窗（`DialogKeys.AttachEscapeToClose`，`SettingsWindow.xaml.cs:73`）。未綁定 Enter。

### 版面結構

由外到內：

1. `Window`（寬 720、高 660，最小 620×520）
   1. `RootGrid`（`Grid`，`Margin="18"`，`SettingsWindow.xaml:10`）
      - 列定義（`SettingsWindow.xaml.cs:91-92`）：
        - 第 0 列：`*`（放分頁）
        - 第 1 列：`Auto`（放按鈕列）
      - 第 0 列：`TabControl`（`xaml.cs:94-101`），依序五個分頁：
        1. `settings.general`（一般）
        2. `settings.colors`（顏色）
        3. `settings.readerColors`（閱讀器顏色）
        4. `settings.groupColors`（群組顏色）
        5. `settings.snapshots`（快照）
        - 每個分頁的 `Content` 都是 `ScrollViewer`（`VerticalScrollBarVisibility=Auto`，`xaml.cs:650-653`），內容為 `StackPanel`（`Margin=6`，`xaml.cs:648`）。
      - 第 1 列：`DockPanel`（`Margin=0,14,0,0`，`xaml.cs:103`）
        - 左側 `StackPanel`（水平，`DockPanel.Dock=Left`，`xaml.cs:105-106`）：Reset（`settings.reset`）、Export（`settings.export`）、Import（`settings.import`）。
        - 右側 `StackPanel`（水平，靠右，`xaml.cs:122`）：Cancel（`dialog.cancel`）、Apply（`settings.apply`，強調色）。
2. 按鈕樣式（`CreateButton`，`xaml.cs:655-662`）：`Padding=16,7`，`Margin=6,0,0,0`。Apply 背景為 `AccentBrush`、前景白色；其餘按鈕背景 `SurfaceBrush`、前景 `PrimaryTextBrush`。Apply 與 Cancel 都沒有設定 `IsDefault`／`IsCancel`。

分頁內的標準列（`AddLabeledControl`，`xaml.cs:620-634`）：

- 每列 `DockPanel`，`Margin=0,6,0,6`。
- 左側標籤 `TextBlock`，寬 250，垂直置中，前景 `PrimaryTextBrush`。
- 右側控制項，`HorizontalAlignment=Left`，垂直置中。

說明文字（`AddHelpText`，`xaml.cs:636-646`）：

- `TextBlock`，自動換行，`FontSize=12`，`Margin=250,-2,0,10`，前景 `SecondaryTextBrush`。

### 所有控制項

以下依畫面順序列出。「套用後生效」指只寫入 `_working`（暫存副本），要按 Apply 才寫回 `AppSettings`。

#### 分頁一：一般（`settings.general`，`xaml.cs:138-198`）

| 順序 | 控制項 | 標籤 key | 預設值 | 範圍或選項 | 生效時機 |
|---|---|---|---|---|---|
| 1 | ComboBox `_languageBox`，寬 220（`xaml.cs:141`） | `settings.language` | 繁體中文（`AppSettings.cs:149`） | 項目一 `settings.language.zh`，項目二 `settings.language.en`（`xaml.cs:142-143`） | 選擇變更即寫入 `_working.Language`（`xaml.cs:144-150`），按 Apply 才生效 |
| 2 | ComboBox `_themeBox`，寬 220（`xaml.cs:153`） | `settings.theme` | `System`（`AppSettings.cs:150`） | 顯示順序：`settings.theme.system`、`settings.light`、`settings.dark`（`xaml.cs:154-156`），對應 `ThemeOrder`（`xaml.cs:22-27`） | 見下方「主題下拉的特殊行為」 |
| 3 | CheckBox `_formattingToolbarBox`（`xaml.cs:185-190`） | `settings.showFormattingToolbar` | 關（`AppSettings.cs:159`） | 勾選／未勾 | 按 Apply（`xaml.cs:549`） |
| 4 | ComboBox `_characterCountModeBox`，寬 220（`xaml.cs:192`） | `settings.characterCountMode` | `IncludeWhitespace`（`AppSettings.cs:158`） | 顯示順序：`status.countMode.includeWhitespace`、`status.countMode.excludeWhitespace`、`status.countMode.excludeLineBreaks`（`xaml.cs:193-195`），順序由 `CharacterCountModeOrder` 固定（`xaml.cs:31-36`） | 按 Apply（`xaml.cs:551-554`） |

主題下拉的特殊行為（`xaml.cs:157-182`）：

- 只有在「解析後的主題（亮／暗）真的改變」時，才會：
  1. 把目前顏色分頁的輸入值存回舊主題的調色盤（`SavePaletteFields(previous)`、`SaveReaderPaletteFields(previous)`）。
  2. 重新載入新主題的調色盤到顏色分頁（`LoadPaletteFields()`、`LoadReaderPaletteFields()`）。
- 不論是否改變，都會更新顏色分頁上方的範圍說明（`UpdatePaletteScopeNotes()`）。
- `ThemeService.Resolve(...)` 用來把「跟隨系統」解析成亮或暗（`xaml.cs:172-174`）。

#### 分頁二：顏色（`settings.colors`，`xaml.cs:200-234`）

- 範圍說明（`AddPaletteScopeNote`，`xaml.cs:458-469`、`xaml.cs:471-483`）：
  - `TextBlock`，自動換行，`FontSize=12`，`Margin=0,0,0,8`，前景 `SecondaryTextBrush`。
  - 文字為 `settings.paletteScope` 套入主題名稱；主題名稱為 `settings.dark` 或 `settings.light`。若偏好是「跟隨系統」，主題名稱再套 `settings.theme.systemSuffix`。
- 固定文字 `HEX`（`xaml.cs:204-210`）。**此字串是寫死在程式中的英文，沒有 localization key**，前景 `SecondaryTextBrush`，`Margin=0,0,0,8`。
- 11 列顏色，key 依序為（`xaml.cs:211`）：
  1. `plain`
  2. `heading`
  3. `emphasis`
  4. `link`
  5. `list`
  6. `quote`
  7. `code`
  8. `yaml-key`
  9. `yaml-value`
  10. `yaml-comment`
  11. `front-matter`
- 每列結構（`xaml.cs:212-230`）：
  - `DockPanel`，`Margin=0,2,0,2`。
  - 色塊 `Border`：寬 28、高 24，`Margin=0,0,8,0`，`CornerRadius=3`。
  - 十六進位輸入 `TextBox`：寬 110，文字置中。
  - 「…」按鈕：寬 30，`Margin=6,0,0,0`，`Padding=0`，開啟 `ColorPickerDialog`。
  - 標籤 `TextBlock`：寬 170，key 為 `settings.color.{key}`，垂直置中，前景 `PrimaryTextBrush`。
- 色塊即時預覽：輸入框文字變動時，若能解析為顏色就更新色塊（`xaml.cs:215-216`、`xaml.cs:675-681`）。
- 預設值：載入時若調色盤缺某 key，用 `#808080` 補位（`xaml.cs:490`）。
- 生效：輸入值先暫存在輸入框；按 Apply 時才逐一驗證（`TryNormalizeColor`）並寫入「目前主題」的調色盤（`xaml.cs:570-579`）。驗證失敗時跳出錯誤訊息（`settings.error.invalidColor`，帶入原始輸入字串），視窗不關閉。
- 注意：只能編輯「目前解析後的主題」（`EditingTheme`，`xaml.cs:456`）的那一份調色盤。若偏好是「跟隨系統」，編輯的是當下系統所用的那一份。

#### 分頁三：閱讀器顏色（`settings.readerColors`，`xaml.cs:236-291`）

- 範圍說明：同顏色分頁（`AddPaletteScopeNote`）。
- 說明文字：`settings.readerColors.help`，自動換行，`SecondaryTextBrush`，`Margin=0,0,0,8`（`xaml.cs:240-247`）。
- 5 列顏色，key 依序為（`xaml.cs:248`）：`text`、`heading`、`link`、`quote`、`code`。列結構與顏色分頁相同（色塊 28×24、輸入框寬 110、「…」按鈕寬 30、標籤寬 170），標籤 key 為 `settings.readerColor.{key}`（`xaml.cs:260`）。
- 預覽區標題：`settings.preview`，`FontWeight=SemiBold`，`Margin=0,14,0,6`（`xaml.cs:270-277`）。
- 排版參數，每項為 `AddLabeledControl` 的一列，輸入框寬 90（`xaml.cs:278-288`）：

| 控制項 | 標籤 key | 對應 AppSettings 屬性 | 預設值（`AppSettings.cs`） |
|---|---|---|---|
| h1 | `settings.heading.h1` | `PreviewTypography.H1Size` | 32（:119） |
| h2 | `settings.heading.h2` | `PreviewTypography.H2Size` | 26（:120） |
| h3 | `settings.heading.h3` | `PreviewTypography.H3Size` | 22（:121） |
| h4 | `settings.heading.h4` | `PreviewTypography.H4Size` | 18（:122） |
| h5 | `settings.heading.h5` | `PreviewTypography.H5Size` | 16（:123） |
| h6 | `settings.heading.h6` | `PreviewTypography.H6Size` | 14（:124） |
| line | `settings.lineSpacing` | `PreviewTypography.LineSpacing` | 1.35（:125） |
| paragraph | `settings.paragraphSpacing` | `PreviewTypography.ParagraphSpacing` | 10（:126） |
| heading | `settings.headingSpacing` | `PreviewTypography.HeadingSpacing` | 14（:127） |

- 驗證（`TryDouble`，`xaml.cs:610-618`）：以 `NumberStyles.Float` 與 `InvariantCulture` 解析，必須大於 0；否則跳錯誤訊息 `settings.error.invalidNumber`（`xaml.cs:566`）。**沒有上限或下限檢查**（`Normalize()` 也沒有對這九個值做範圍限制，`AppSettings.cs:167-191`）。
- 生效：按 Apply 才寫入，數值與顏色一併驗證（`xaml.cs:555-590`）。

#### 分頁四：群組顏色（`settings.groupColors`，`xaml.cs:293-342`）

- 說明文字：`settings.groupColors.help`，自動換行（`xaml.cs:296-303`）。
- 清單容器：`StackPanel`，每次增減時整個重建（`xaml.cs:344-383`）。
- 每一列（`xaml.cs:352-381`）：
  - `DockPanel`，`Margin=0,2,0,2`。
  - 色塊 `Border` 28×24。
  - 十六進位輸入 `TextBox`，寬 110，文字置中，初始值為 `_working.GroupColors[i]` 的原始字串。
  - 「…」按鈕（寬 30），開啟 `ColorPickerDialog`。
  - 刪除按鈕「✕」（寬 30，`Margin=6,0,0,0`，`Padding=0`），ToolTip 為 `settings.groupColors.remove`。**當清單只剩一項時停用**（`IsEnabled = colors.Count > 1`，`xaml.cs:368`）。
- 新增按鈕：`settings.groupColors.add`，靠左，`Margin=0,6,0,16`（`xaml.cs:308-317`）。點擊時先把輸入框內容存回清單（`CaptureGroupColorRows`，`xaml.cs:446-452`），再加入 `#3B6FF5`，最後重建列。**沒有數量上限**，也允許重複顏色（原始碼未限制）。
- 刪除：先存回輸入內容，再移除該索引並重建（`xaml.cs:370-375`）。
- 群組選取底色強度：
  - 標籤 `settings.groupSelectionTint`，控制項是一列橫向 `StackPanel`，內含 `Slider` 與數值文字（`xaml.cs:319-338`）。
  - `Slider`：最小 0、最大 50、刻度 1、啟用吸附、寬 220、垂直置中。
  - 數值文字：`{(int)值}%`，`Margin=8,0,0,0`，前景 `PrimaryTextBrush`（`xaml.cs:439-440`）。
  - 說明文字：`settings.groupSelectionTint.help`。
  - 預設值：`AppSettings.GroupSelectionTint = 0.14`（`AppSettings.cs:165`），即滑桿顯示 14%。
  - 換算：滑桿是百分比（0–50），存入時除以 100（`xaml.cs:604`）；載入時乘以 100（`xaml.cs:433`）。
- 群組預設色（`GroupColorPalette.CreateDefaults`，`AppSettings.cs:136-143`）：`#3B6FF5`、`#2DA44E`、`#D97706`、`#8250DF`、`#CF222E`。
- 生效（`xaml.cs:592-604`）：按 Apply 時逐一驗證每個輸入框，失敗就跳 `settings.error.invalidColor` 並停留在視窗。全部通過後整份取代 `_working.GroupColors`。

#### 分頁五：快照（`settings.snapshots`，`xaml.cs:385-406`）

| 控制項 | 標籤 key | 說明 key | 對應 AppSettings | 預設值與範圍 |
|---|---|---|---|---|
| TextBox 寬 90（`xaml.cs:389`） | `settings.snapshotInterval` | `settings.snapshotInterval.help` | `SnapshotIntervalMinutes` | 5（:156）；`Normalize()` 夾在 1–120（:169） |
| TextBox 寬 90（`xaml.cs:393`） | `settings.maxSnapshots` | `settings.maxSnapshots.help` | `MaxSnapshotsPerFile` | 20（:157）；`Normalize()` 夾在 1–200（:170） |
| CheckBox（`xaml.cs:397-401`） | `settings.hideSnapshotFiles` | `settings.hideSnapshotFiles.help` | `HideSnapshotFiles` | 關（:160） |

- 整數驗證（`xaml.cs:540-545`）：以 `NumberStyles.Integer`、`InvariantCulture` 解析；失敗跳 `settings.error.invalidInteger`。
- 範圍：TryReadSettings **不做範圍檢查**，超出範圍的數值會在 `_working.Normalize()` 被靜默夾住（`xaml.cs:606`），不會提示使用者。
- 隱藏快照檔：套用時若此值改變，主視窗會呼叫 `SweepSnapshotVisibility`（`MainWindow.xaml.cs:1985-1987`；方法本體在 :366），並由 `SnapshotSidecarStore.ApplyVisibility` 設定檔案的隱藏屬性（見第 10 章）。

#### 底部按鈕

| 按鈕 | key | 行為 | 來源 |
|---|---|---|---|
| Reset | `settings.reset` | 以 `new AppSettings()` 的複本取代 `_working`，重新載入所有欄位。**不跳確認**，**不寫入磁碟**，需再按 Apply。會連語言與主題一起回預設（繁中、跟隨系統）。 | `xaml.cs:107-112` |
| Export | `settings.export` | 先執行 TryReadSettings（驗證失敗就中止）；開啟 SaveFileDialog（篩選 `settings.fileFilter` 為 `*.gss`，預設檔名 `glystrata-settings.gss`，覆寫前詢問）；寫入 JSON（縮排、camelCase），並在根物件加上 `"kind": "glystrata-settings"`。IO／權限錯誤顯示訊息。 | `xaml.cs:113-114`、`xaml.cs:728-760` |
| Import | `settings.import` | 開啟 OpenFileDialog（`*.gss`，檔案必須存在）。解析規則見下方。成功後只替換 `_working` 並重新載入欄位，需按 Apply 才生效；Cancel 可撤回。 | `xaml.cs:115-116`、`xaml.cs:762-829` |
| Cancel | `dialog.cancel` | `Close()`，不寫入。 | `xaml.cs:123-124` |
| Apply | `settings.apply` | 呼叫 `ApplyAndClose()`：通過 TryReadSettings 後觸發 `SettingsApplied` 事件並關閉視窗。 | `xaml.cs:125-126`、`xaml.cs:527-536` |

匯入的檢查順序（`xaml.cs:777-827`）：

1. 讀取檔案文字並以 JSON 解析（允許註解與尾隨逗號，`xaml.cs:778-787`）；解析結果不是物件 → `settings.import.invalid`。
2. 若含有 `snapshots` 或 `notice` 欄位（代表是快照檔而非設定檔）→ `settings.import.notSettings`（`xaml.cs:791-795`）。
3. `kind` 不等於 `glystrata-settings` → `settings.import.notSettings`（`xaml.cs:797-801`）。
4. `schemaVersion` 大於 1 → `settings.import.tooNew`（`xaml.cs:803-807`）。
5. 反序列化為 `AppSettings`，失敗 → `settings.import.invalid`（`xaml.cs:809-814`）。
6. 通過後 `Normalize()`，替換 `_working`，`LoadFields()`，並提示 `settings.import.success`（`xaml.cs:818-821`）。
- 例外處理：`JsonException`、`IOException`、`UnauthorizedAccessException`、`InvalidOperationException` 都會收斂成 `settings.import.invalid`（`xaml.cs:823-828`）。

### 互動行為

- Esc：關閉視窗，不套用（`xaml.cs:73`）。
- Enter：沒有綁定（Apply 不是 IsDefault）。原始碼未指定 Enter 行為，實際上不會觸發任何按鈕。
- 驗證錯誤訊息：
  - 所有錯誤皆使用 `MessageDialogs.Inform`，標題為 `settings.title`（`xaml.cs:543、566、575、586、597`）。
  - 錯誤訊息出現後，設定視窗保持開啟，輸入內容不變。
  - 錯誤的 key：`settings.error.invalidInteger`、`settings.error.invalidNumber`、`settings.error.invalidColor`（後者帶入輸入值）。
- 分頁切換：純 `TabControl`，無額外邏輯。
- 「套用後才寫入」的範圍：所有分頁的值都先放在 `_working` 的複本（`CloneSettings`，`xaml.cs:720-726`，透過 JSON 序列化複製，以免日後新增欄位被漏掉）。
- 關閉視窗時不做任何自動儲存（`OnClosed` 只取消語言事件訂閱，`xaml.cs:83-87`）。

### 主題與語言切換時

- 主題：
  - 視窗本身的顏色在開啟時以 `ApplyThemeResources` 取得，不會因為下拉選單改變而重繪（`xaml.cs:664-668`）。
  - 下拉選單只影響「顏色分頁應該編輯哪一份調色盤」（見上）。
  - 真正的全域主題在按 Apply 後由 `MainWindow.ApplySettings` 套用（`MainWindow.xaml.cs:1976-2001`）。
- 語言：
  - 視窗訂閱 `LanguageChanged`，但處理函式只更新標題（`xaml.cs:670-673`）。
  - 其餘控制項的文字在建立時已取定，不會改變。
  - 實務上：語言在按 Apply 後才寫入，Apply 會關閉視窗，因此畫面內的文字在使用者可見的範圍內不會即時切換。

---

### 設定項與 AppSettings 的對應表

來源：`src/Glystrata.Core/Persistence/AppSettings.cs`。所有 15 個屬性都已逐一檢查，只有 `SchemaVersion` 不在 UI 上（只在匯出時寫入，預設 1，`AppSettings.cs:148`）。

| 設定畫面位置 | 標籤 key | AppSettings 屬性 | 型別 | 預設值 | Normalize 規則 | 備註 |
|---|---|---|---|---|---|---|
| 一般 → 語言 | `settings.language` | `Language` | `AppLanguage`（TraditionalChinese、English） | TraditionalChinese（:149） | 無（:167-191 未處理） | 只有兩種選項 |
| 一般 → 主題 | `settings.theme` | `Theme` | `ThemePreference`（Light=0、Dark=1、System=2） | System（:150） | 不在定義值內 → 改為 System（:180-183） | 數值被固定，供 settings.json 相容 |
| 一般 → 顯示格式工具列 | `settings.showFormattingToolbar` | `ShowFormattingToolbar` | bool | false（:159） | 無 | |
| 一般 → 字數計算模式 | `settings.characterCountMode` | `CharacterCountMode` | `CharacterCountMode`（IncludeWhitespace、ExcludeWhitespace、ExcludeLineBreaks） | IncludeWhitespace（:158） | 不在定義值內 → IncludeWhitespace（:176-179） | |
| 顏色（亮色或暗色，視目前主題） | `settings.color.{key}` | `LightEditorPalette.Colors` 或 `DarkEditorPalette.Colors` | `Dictionary<string,string>` | 見 :36-49（亮）與 :53-69（暗） | `Normalize()` 只補 null 物件（:172-175） | 11 個 key |
| 閱讀器顏色（亮或暗） | `settings.readerColor.{key}` | `LightReaderPalette.Colors` 或 `DarkReaderPalette.Colors` | `Dictionary<string,string>` | 見 :84-89（亮）與 :100-105（暗） | 同上 | 5 個 key |
| 閱讀器排版 H1–H6 | `settings.heading.h1`…`h6` | `PreviewTypography.H1Size`…`H6Size` | double | 32、26、22、18、16、14（:119-124） | 無範圍限制（:167-191） | |
| 閱讀器行距 | `settings.lineSpacing` | `PreviewTypography.LineSpacing` | double | 1.35（:125） | 無 | |
| 閱讀器段落間距 | `settings.paragraphSpacing` | `PreviewTypography.ParagraphSpacing` | double | 10（:126） | 無 | |
| 閱讀器標題間距 | `settings.headingSpacing` | `PreviewTypography.HeadingSpacing` | double | 14（:127） | 無 | |
| 群組顏色清單 | `settings.groupColors` | `GroupColors` | `List<string>` | 五色（:136-143） | 空清單 → 還原預設五色（:185-190） | UI 不允許刪到剩零 |
| 群組選取底色強度 | `settings.groupSelectionTint` | `GroupSelectionTint` | double（0–0.5） | 0.14（:165） | 夾在 0–0.5（:184） | UI 為 0–50%，存入時除以 100 |
| 快照間隔（分鐘） | `settings.snapshotInterval` | `SnapshotIntervalMinutes` | int | 5（:156） | 夾在 1–120（:169） | UI 不驗證範圍，靜默夾住 |
| 每檔快照上限 | `settings.maxSnapshots` | `MaxSnapshotsPerFile` | int | 20（:157） | 夾在 1–200（:170） | 同上 |
| 隱藏快照檔 | `settings.hideSnapshotFiles` | `HideSnapshotFiles` | bool | false（:160） | 無 | 會觸發檔案隱藏屬性同步 |

補充：

- 顏色與閱讀器顏色的 key 不會被 `Normalize()` 檢查；若檔案缺 key，使用端以 `Get(key, fallback)` 取值（`AppSettings.cs:71-72`、`108-109`）。
- 設定檔的 JSON 欄位使用 camelCase（`AppSettings` 以 `JsonNamingPolicy.CamelCase` 序列化，`xaml.cs:38-56`）。
- `Normalize()` 會被 `TryReadSettings`、`CloneSettings`、匯入流程與主視窗的 `ApplySettings` 呼叫（`xaml.cs:606、724、818`；`MainWindow.xaml.cs:1978`）。

---

## 2. 快照歷史視窗（SnapshotHistoryWindow）

- 來源：`src/Glystrata/Views/SnapshotHistoryWindow.xaml`、`SnapshotHistoryWindow.xaml.cs`
- 資料來源：`src/Glystrata.Core/Snapshots/`（見第 10 章）

### 用途與開啟方式

- 用途：列出目前文件的所有快照，可為每筆快照寫備註、比較差異、還原、刪除單筆或全部，也可手動建立快照。
- 開啟方式：
  - 選單「快照歷史」，快速鍵 `Ctrl+Shift+H`（`AppCommand.cs:57`，命令 `FileSnapshotHistory`，標籤 key `file.snapshotHistory`；命令對應 `MainWindow.xaml.cs:105`）。
  - 編輯窗格發出 `SnapshotRequested` 事件時呼叫 `OpenSnapshotHistory(view)`（`MainWindow.xaml.cs:1324`）。發出事件的按鈕位置原始碼未在本次讀取範圍內。
- 開啟前的檢查（`MainWindow.xaml.cs:1760-1772`）：
  - 沒有作用中的分頁（且沒有指定 view）→ 跳 `dialog.noFile`（標題為 `snapshot.title`），不開視窗。
  - 目前文件沒有檔案路徑（未存檔）→ 同樣跳 `dialog.noFile`，不開視窗。

### 視窗屬性

- 標題：XAML 預設 `Snapshot history`（`SnapshotHistoryWindow.xaml:4`），執行時 `Title = _localization.Get("snapshot.title")`（`SnapshotHistoryWindow.xaml.cs:379`）。
- 預設大小：寬 720、高 560（`SnapshotHistoryWindow.xaml:5-6`）。
- 最小大小：寬 560、高 360（`SnapshotHistoryWindow.xaml:7-8`）。
- 可否調整大小：XAML 未設定 `ResizeMode`，為預設（可調整）。
- 是否 modal：否。`window.Show()`（`MainWindow.xaml.cs:1808`）。
- 開啟位置：`CenterOwner`（`SnapshotHistoryWindow.xaml:11`），Owner 為主視窗（`MainWindow.xaml.cs:1775`）。
- 能否同時開多個：可以。每次開啟都 `new` 一個，`MainWindow` 沒有保留參考（`MainWindow.xaml.cs:1775`）。
- 背景與前景：`WindowBackgroundBrush`、`PrimaryTextBrush`（`SnapshotHistoryWindow.xaml:9-10`，動態資源）。
- Esc：關閉（`DialogKeys.AttachEscapeToClose`，`SnapshotHistoryWindow.xaml.cs:19`）。

### 版面結構

由外到內：

1. `Window`（寬 720、高 560，最小 560×360）
   1. `RootGrid`（`Grid`，`Margin=16`，`SnapshotHistoryWindow.xaml:12`）
      - 列定義：第 0 列 `1*`（清單），第 1 列 `Auto`（按鈕列）（`SnapshotHistoryWindow.xaml:13-16`）。
      - 第 0 列：`ListBox` `SnapshotList`（`SnapshotHistoryWindow.xaml:17-21`）
        - `HorizontalContentAlignment=Stretch`
        - 背景 `SurfaceBrush`、邊框 `BorderBrush`、前景 `PrimaryTextBrush`（動態資源）。
        - 每一筆為一個 `ListBoxItem`（`SnapshotHistoryWindow.xaml.cs:152-221`）：
          - `Padding=8,7,8,7`，`HorizontalContentAlignment=Stretch`（:154-162）。
          - 內容為 `DockPanel`（:215-219）：
            - 左側（Dock Left）：時間與字數文字 `TextBlock`，垂直置中（:164-168）。文字格式為 `{本地時間 yyyy-MM-dd HH:mm:ss}  ·  {字數 N0} chars  ·  `（:166、:141-142）。
            - 右側：`Grid` 備註區（:211-213），內含兩層：
              - 底層 `hint`：`TextBlock`，文字為 `snapshot.notePlaceholder`，`IsHitTestVisible=false`，`Opacity=0.55`，前景 `SecondaryTextBrush`。備註為空才顯示（:170-180）。
              - 上層 `editor`：`TextBox`，背景透明、無邊框、`Padding=0`，垂直置中，`AcceptsReturn=false`，前景 `PrimaryTextBrush`（:182-191）。
      - 第 1 列：`DockPanel`（`Margin=0,12,0,0`，`SnapshotHistoryWindow.xaml:22`）
        - 左側（Dock Left）：`DeleteAllButton`，`Padding=14,6`，`Margin=5,0`，前景 `DiffRemovedForegroundBrush`（:23-24）。
        - 右側：`StackPanel`（水平，靠右，:25-30），由左到右依序：
          1. `CreateSnapshotButton`（`file.createSnapshot`，`Padding=14,6`，`Margin=5,0`）
          2. `CompareButton`（`snapshot.compare`）
          3. `RestoreButton`（`snapshot.restore`）
          4. `DeleteButton`（`snapshot.delete`）

- 列表內容的三種狀態（`RefreshAsync`，`:103-139`）：
  - 沒有檔案路徑：只放一個停用的 `ListBoxItem`，文字 `snapshot.none`；按鈕全部停用（:105-112）。
  - 有檔案但沒有快照：同樣放一個停用的 `snapshot.none` 項目，`DeleteAll` 停用（:122-125、:131）。
  - 有快照：依時間新到舊列出，預設選取第 0 筆（:117-129）。

### 所有控制項

| 控制項 | 類型 | 文字／標籤 key | 預設與狀態 | 選項或範圍 | 生效 |
|---|---|---|---|---|---|
| `SnapshotList` | `ListBox` | — | 有快照時選第 0 筆 | 單選 | 選取變動即通知比較視窗（`:25-32`） |
| 每筆備註 | `TextBox` | `snapshot.notePlaceholder`（提示字） | 空白 | 單行（不接受 Enter 換行） | 失去焦點時儲存（見互動行為） |
| `CreateSnapshotButton` | `Button` | `file.createSnapshot` | 啟用（有檔案時） | — | 立即執行 |
| `CompareButton` | `Button` | `snapshot.compare` | 有選取才啟用（`UpdateButtons`，:387-392） | — | 發出 `CompareRequested` |
| `RestoreButton` | `Button` | `snapshot.restore` | 有選取才啟用 | — | 跳出三選一對話框 |
| `DeleteButton` | `Button` | `snapshot.delete` | 有選取才啟用 | — | 跳出確認對話框 |
| `DeleteAllButton` | `Button` | `snapshot.deleteAll` | 有快照才啟用（:131） | — | 跳出確認對話框 |

### 互動行為

- 建立快照（`CreateSnapshotButton_Click`，`:323-345`）：
  1. 沒有檔案路徑 → 跳 `dialog.noFile`，結束。
  2. 呼叫 `_snapshots.CreateAsync(document, _maxSnapshots)`。`_maxSnapshots` 是開視窗當時的 `MaxSnapshotsPerFile`，視窗開著時不會更新（`:15`、`:23`）。
  3. 回傳 `null` → 跳 `snapshot.unchanged`（內容與最新快照相同，未建立）。這個分支同時涵蓋「文件為空且沒有任何歷史」（見第 10 章）。
  4. 成功 → 重新整理列表。
  5. IO／權限例外 → 跳錯誤訊息（標題 `snapshot.title`，內容為例外訊息）。

- 比較（`CompareButton_Click`，`:266-272`）：有選取時發出 `CompareRequested`，由主視窗接手開啟或更新比較視窗（見第 3 章）。本視窗不直接開比較視窗。

- 還原（`RestoreButton_Click`，`:274-297`）：
  - 跳出 `MessageDialogs.Show`，標題 `snapshot.restore`，訊息為 `snapshot.restoreTo` 套入時間。
  - 三個按鈕，順序與結果：
    - 第 0 個 `snapshot.replaceCurrent`：預設按鈕（Enter），結果為 `RestoreMode.ReplaceCurrent`。
    - 第 1 個 `snapshot.saveAsNew`：結果為 `RestoreMode.SaveAsNewFile`。
    - 第 2 個 `dialog.cancel`：Esc 對應此按鈕，不做任何事。
  - 選擇後發出 `RestoreRequested`，實際還原邏輯在 `MainWindow.RestoreSnapshot`（原始碼未在本次讀取範圍內）。

- 刪除單筆（`DeleteButton_Click`，`:299-321`）：
  - 確認框 `MessageDialogs.Confirm`：標題 `snapshot.delete`，訊息 `snapshot.deleteConfirm` 套入時間，確認按鈕 `dialog.delete`（預設，Enter），取消 `dialog.cancel`（Esc）。
  - 確認後呼叫 `DeleteAsync` 再重新整理。例外時跳錯誤訊息。

- 全部刪除（`DeleteAllButton_Click`，`:347-369`）：
  - 確認框：標題 `snapshot.deleteAll`，訊息 `snapshot.deleteAllConfirm`，確認 `dialog.delete`，取消 `dialog.cancel`。
  - 確認後呼叫 `DeleteAllAsync`（會刪除整個快照檔），再重新整理。

- 備註編輯（`CreateSnapshotRow`、`SaveRowNoteAsync`，`:182-258`）：
  - 點擊備註框會讓該列 `IsSelected=true`（`GotFocus`，:197），因為按鈕作用於目前選取列。
  - 失去焦點（`LostFocus`）時，若文字與原本備註不同，呼叫 `UpdateNoteAsync` 寫入，成功後更新該列的快照資料，避免關窗時重複儲存（:245-252）。
  - 按 Enter：吞掉按鍵（`Handled=true`），把焦點移到清單（`SnapshotList.Focus()`，:199-209）。備註是單行，不會插入換行。
  - 備註在列表中顯示時，換行會被換成空白並 Trim（`FormatNotePreview`，:144-145）。

- 關閉視窗（`OnClosing`，`:65-95`）：
  - 若有「文字與儲存值不同」的備註列，先取消關閉，非同步逐一儲存，儲存完成後自動再關閉一次（`_closingAfterNoteSave` 旗標）。
  - 理由寫在註解中：若阻塞等待會卡死 UI 執行緒。
  - 注意：按 Esc 會先經過 `PreviewKeyDown` 的關閉流程，因此在備註框內按 Esc 也會觸發同樣的儲存流程。

- 選取同步（`SelectSnapshot`，`:44-63`）：比較視窗選了另一份快照時，會呼叫本視窗的 `SelectSnapshot(id)` 同步選取；此時以 `_suppressSelectionEvent` 旗標避免迴圈。

- 鍵盤：Esc 關閉。清單上的上下鍵、Enter 等由 `ListBox` 預設行為處理，原始碼未指定。

### 主題與語言切換時

- 主題：
  - 背景、前景、清單與備註的顏色都用 `DynamicResource` 或 `SetResourceReference` 綁定（`SnapshotHistoryWindow.xaml:9-10、19-21、191`；`xaml.cs:180`），因此主題資源切換時會跟著變（依 WPF 動態資源語意，推定；全域主題切換的實作未在本次讀取範圍內）。
- 語言：
  - 訂閱 `LanguageChanged`（`:33`）。
  - 收到事件時重新套用按鈕與標題文字（`ApplyLocalization`，`:377-385`），並呼叫 `RefreshAsync()` **重建整個清單**（`:371-375`）。
  - 副作用：重建後選取回到第 0 筆，原本的選取位置會遺失。原始碼未處理備註編輯中的未儲存內容，此情況的實際行為為不確定（見第 11 章）。

---

## 3. 比較視窗（DiffWindow）

- 來源：`src/Glystrata/Views/DiffWindow.xaml`、`DiffWindow.xaml.cs`
- 差異演算法：`src/Glystrata.Core/Snapshots/TextDiffService.cs`（見第 10 章）

### 用途與開啟方式

本視窗有兩種進入點，行為略有不同：

1. **快照比較（選擇器模式）**：從快照歷史視窗按「比較」（`CompareRequested`）開啟。主視窗（`MainWindow.xaml.cs:1778-1806`）
   - 若已有比較視窗，先嘗試 `SelectSnapshot`，成功就啟用並結束；失敗（新快照不在清單內）就關閉舊的、重開。
   - 新開時用 `new DiffWindow(snapshots, snapshot, () => view.Document.Text, _diff, _localization) { Owner = this }`（`MainWindow.xaml.cs:1795`），用 `Show()` 開啟（非 modal）。
   - 同一時間主視窗只保留一個比較視窗的參考（`compareWindow`），`Closed` 時清空（:1797-1798）。
2. **外部檔案比較（單組模式）**：外部檔案變更對話框中選「比較」（`dialog.compare`）時開啟（`MainWindow.xaml.cs:2370-2389`、`:2409-2425`）。
   - `new DiffWindow(document.Text, decoded.Text, DateTime.UtcNow, _diff, _localization) { Owner = this }`（`MainWindow.xaml.cs:2418`）。
   - 每次都建立新視窗，主視窗不保留參考（原始碼未指定上限）。

### 視窗屬性

- 標題：XAML 預設 `Compare snapshot`（`DiffWindow.xaml:4`）。執行時由 `SetTitle` 改為 `{dialog.compare} — {本地時間}`（`DiffWindow.xaml.cs:101-105`）。
  - 單組模式在建構時呼叫 `SetTitle`（:24）。
  - 選擇器模式沒有在建構時呼叫 `SetTitle`，而是在選取初始化時由 `SelectionChanged` 觸發（:53-55 → :87-99）。
- 預設大小：寬 980、高 720（`DiffWindow.xaml:5-6`）。
- 最小大小：寬 640、高 400（`DiffWindow.xaml:7-8`）。
- 可否調整大小：預設（可調整）。
- 是否 modal：否（`Show()`，`MainWindow.xaml.cs:1798` 之後與 :2419）。
- 開啟位置：`CenterOwner`（`DiffWindow.xaml:9`），Owner 為主視窗。
- 背景與前景：`WindowBackgroundBrush`、`PrimaryTextBrush`（`DiffWindow.xaml:10-11`；另以 `ApplyBackground` 動態綁定，:107-111）。
- Esc：關閉（`DialogKeys.AttachEscapeToClose`，`DiffWindow.xaml.cs:21`、`:37`）。

### 版面結構

由外到內：

1. `Window`（寬 980、高 720，最小 640×400）
   1. `RootGrid`（`Grid`，`Margin=12`，`DiffWindow.xaml:12`）
      - 列定義：第 0 列 `Auto`（選擇器），第 1 列 `*`（差異內容）（`DiffWindow.xaml:13-16`）。
      - 第 0 列：`StackPanel` `SelectorPanel`（水平，`Margin=0,0,0,8`，`Visibility=Collapsed`，`DiffWindow.xaml:17`）
        - 只在選擇器模式下顯示（`DiffWindow.xaml.cs:41`）。
        - `TextBlock` `SelectorLabel`，`Margin=0,0,8,0`，垂直置中，文字 `snapshot.compareWith`（:18、:42）。
        - `ComboBox` `SnapshotSelector`，寬 320（`DiffWindow.xaml:19`）。
      - 第 1 列：`RichTextBox` `DiffText`（`DiffWindow.xaml:21-32`）
        - `IsReadOnly=True`、`IsReadOnlyCaretVisible=True`（唯讀但可見游標、可選取複製）。
        - `BorderThickness=1`，邊框 `BorderBrush`，背景 `SurfaceBrush`，前景 `PrimaryTextBrush`。
        - 字型 `Cascadia Mono`，字級 13（`DiffWindow.xaml:29-30`）。
        - `VerticalScrollBarVisibility=Auto`，`HorizontalScrollBarVisibility=Disabled`（:31-32）。

### 所有控制項

| 控制項 | 類型 | 文字 key | 預設 | 選項 | 生效 |
|---|---|---|---|---|---|
| `SelectorLabel` | TextBlock | `snapshot.compareWith` | 僅選擇器模式顯示 | — | 建構時設定 |
| `SnapshotSelector` | ComboBox，寬 320 | — | 初始選中傳入的 `initial` 快照；找不到則選第 0 項（:53-55） | 每項文字為 `{本地時間 yyyy-MM-dd HH:mm:ss}  ·  {字數 N0} chars`，`Tag` 為 `SnapshotInfo`（:45-50） | 選取變動即重算差異（見互動行為） |
| `DiffText` | RichTextBox（唯讀） | — | 顯示差異 | — | 每次重算整個 `Document` |

### 互動行為

- 差異呈現方式（`RenderDiff`，`:113-146`）：
  - **單欄、統一差異格式（unified）**：所有行依順序排列，每一行一個 `Paragraph`（不是並排）。
  - 每行前綴：新增 `+ `、刪除 `- `、未變更兩個空白（:118-123）。
  - 每行 `Paragraph`：`Margin=0`，`Padding=6,2,6,2`（:124-128）。
  - 每行前景與背景以動態資源綁定（:129-142）。
  - **沒有行號顯示**。`TextDiffService` 雖然計算了左右行號（`DiffLine.LeftLine`、`RightLine`），但這個視窗沒有使用（`DiffWindow.xaml.cs:113-146` 未引用）。
  - **未變更的行也全部顯示**，沒有折疊，也沒有上下文行數限制（原始碼未指定折疊機制）。

- 顏色對照（亮色 ／ 暗色，來源 `Light.xaml:28-33`、`Dark.xaml:28-33`）：

| 類別 | 前景（亮） | 前景（暗） | 背景（亮） | 背景（暗） | 資源 key |
|---|---|---|---|---|---|
| 新增（Added） | `#1F6B3B` | `#9AD9AD` | `#E8F5EC` | `#20382B` | `DiffAdded*Brush` |
| 刪除（Removed） | `#8A3440` | `#F2A8B0` | `#FBEAEC` | `#432C31` | `DiffRemoved*Brush` |
| 未變更（Unchanged） | `#4E5968` | `#B5BDC8` | 透明 | 透明 | `DiffUnchanged*Brush` |

- 左右方向（依程式碼推得）：
  - 快照比較（選擇器模式）：`diff.Compare(snapshot.Text, _getCurrentText())`（`:93`）。左邊是選取的快照，所以 `-` 是該快照的內容，`+` 是目前編輯器的內容。
  - 單組模式的建構式：`diff.Compare(snapshotText, currentText)`（`:23`）。外部比較時傳入的 `snapshotText` 是磁碟上讀到的 `decoded.Text`，`currentText` 是編輯器內容（`MainWindow.xaml.cs:2418`）。因此 `-` 是磁碟檔案，`+` 是編輯器內容。
  - 兩種情況的 `-`／`+` 語意一致：`-` 是左邊（較舊或磁碟），`+` 是右邊（目前編輯器）。

- 選擇器切換（`SnapshotSelector_SelectionChanged`，`:87-99`）：
  1. 取得所選快照。若 `_diff` 或 `_getCurrentText` 為 null（單組模式）則結束。
  2. 重算差異並重繪，更新標題。
  3. 若不是程式呼叫的選取（`_suppressSelectionEvent` 為假），發出 `SnapshotSelected`，主視窗會同步快照歷史視窗的選取（`MainWindow.xaml.cs:1796`）。

- 外部程式選取（`SelectSnapshot`，`:62-85`）：找不到快照時回傳 `false`，讓主視窗決定重開視窗。

- 內容不會自動更新：
  - 本視窗沒有訂閱編輯器的文字變更事件。
  - 選擇器模式下，只有在改變選取時才會用 `_getCurrentText()` 重新計算。
  - 因此使用者在主編輯器改了內容後，比較結果要切換一次選取才會更新（由程式碼推得，原始碼無註解說明）。

- 鍵盤：Esc 關閉。沒有其他按鍵綁定。唯讀文字可用滑鼠選取與複製（`IsReadOnly`）。

### 主題與語言切換時

- 主題：
  - 背景與差異顏色都以 `SetResourceReference` 綁定（`DiffWindow.xaml.cs:109-110`、`:141-142`），屬於動態資源，主題資源切換時會跟著變（依 WPF 語意推定）。
- 語言：
  - 本視窗**沒有訂閱 `LanguageChanged`**。
  - 標題、選擇器標籤在建立或切換快照時才設定，不會因語言切換更新。
  - 實務上：切換語言後，已開啟的比較視窗仍是舊語言，直到重新開啟。

---

## 4. 尋找與取代視窗（FindReplaceWindow）

- 來源：`src/Glystrata/Views/FindReplaceWindow.xaml`、`FindReplaceWindow.xaml.cs`

### 用途與開啟方式

- 用途：在目前作用中的編輯器中尋找文字、逐筆或全部取代。
- 開啟方式：
  - `Ctrl+F`：尋找（`AppCommand.cs:65`，`edit.findReplace`），呼叫 `OpenFindReplace(focusReplace: false)`（`MainWindow.xaml.cs:111`，命令表）。
  - `Ctrl+H`：取代（`AppCommand.cs:66`，此命令沒有選單標籤 key），呼叫 `OpenFindReplace(focusReplace: true)`（`MainWindow.xaml.cs:112`）。
- `OpenFindReplace`（`MainWindow.xaml.cs:1945-1966`）：
  - 沒有作用中的分頁 → 不做任何事。
  - 若視窗尚未開啟：建立、`Closed` 時清空參考、`Show()`（非 modal）。
  - 若已開啟：`Activate()`。
  - 之後無論哪種情況都呼叫 `Seed(selection, focusReplace)`：若作用中編輯器有選取文字，將其帶入尋找框。
- 單例：主視窗只保留一個 `_findReplaceWindow`，同一時間只會有一個。

### 視窗屬性

- 標題：XAML 預設 `Find and Replace`（`FindReplaceWindow.xaml:4`），建構時以 `findReplace.title` 覆寫（`FindReplaceWindow.xaml.cs:50`）。
- 寬度：固定 420（`FindReplaceWindow.xaml:5`）。
- 高度：`SizeToContent="Height"`，依內容自動決定（`FindReplaceWindow.xaml:6`）。
- 可否調整大小：`ResizeMode="NoResize"`（不可調整，`FindReplaceWindow.xaml:7`）。
- 是否 modal：否（`Show()`）。
- 開啟位置：`CenterOwner`（`FindReplaceWindow.xaml:8`），Owner 為主視窗（`FindReplaceWindow.xaml.cs:17`）。
- 工作列：`ShowInTaskbar="False"`（`FindReplaceWindow.xaml:11`）。
- 背景與前景：`WindowBackgroundBrush`、`PrimaryTextBrush`（`FindReplaceWindow.xaml:9-10`）。
- Esc：**不是**透過 `DialogKeys`，而是在 `FindBox`、`ReplaceBox` 的 `KeyDown` 中處理（`FindReplaceWindow.xaml.cs:60-84`）。只有焦點在兩個輸入框時 Esc 才會關閉；焦點在按鈕上時 Esc 無效。

### 版面結構

由外到內：

1. `Window`（寬 420，高自動，不可調整）
   1. `RootGrid`（`Grid`，`Margin=16`，`FindReplaceWindow.xaml:12`）
      - 四列，皆 `Auto`（`:13-18`）。
      - 第 0 列：`Grid`，`Margin=0,0,0,8`，兩欄：左 90（固定）、右 `*`（`:19-23`）。
        - 左：`TextBlock` `FindLabel`，垂直置中，前景 `PrimaryTextBrush`（`:24`）。
        - 右：`TextBox` `FindBox`，背景 `SurfaceBrush`，邊框 `BorderBrush`，前景 `PrimaryTextBrush`，`Padding=6,4`（`:25-29`）。
      - 第 1 列：結構同第 0 列（`:31-42`），左 `ReplaceLabel`，右 `ReplaceBox`。
      - 第 2 列：`TextBlock` `StatusText`，`Margin=0,0,0,8`，`MinHeight=18`，前景 `SecondaryTextBrush`（`:43-47`）。
      - 第 3 列：`DockPanel`（:48-58）
        - 左側 `StackPanel`（水平，Dock Left）：`FindButton`（`findReplace.findButton`）、`PreviousButton`（`findReplace.previous`）、`NextButton`（`findReplace.next`）。按鈕 `Padding=12,5`，`Margin` 依序為 `0,0,5,0`、`0,0,5,0`、`0,0,0,0`（:49-53）。
        - 右側 `StackPanel`（水平，靠右）：`ReplaceButton`（`findReplace.replace`）、`ReplaceAllButton`（`findReplace.replaceAll`）。`Padding=12,5`，`Margin=5,0,0,0`（:54-57）。

### 所有控制項

| 控制項 | 類型 | 標籤 key | 預設 | 行為摘要 |
|---|---|---|---|---|
| `FindBox` | TextBox | `findReplace.find` | 開啟時可能帶入選取文字 | 文字變動 → 清除快取的比對結果與狀態列（`:96-101`）；Enter → 下一筆；Shift+Enter → 上一筆；Esc → 關閉 |
| `ReplaceBox` | TextBox | `findReplace.replaceWith` | 空白 | Enter → 取代目前這一筆；Esc → 關閉（沒有 Shift 變體） |
| `FindButton` | Button | `findReplace.findButton` | — | 等同向後（向下）找一筆 |
| `PreviousButton` | Button | `findReplace.previous` | — | 向前找一筆 |
| `NextButton` | Button | `findReplace.next` | — | 向後找一筆（與 Find 相同） |
| `ReplaceButton` | Button | `findReplace.replace` | — | 取代目前這一筆 |
| `ReplaceAllButton` | Button | `findReplace.replaceAll` | — | 全部取代 |
| `StatusText` | TextBlock | — | 空白 | 顯示 `findReplace.matchCount`（第幾筆／共幾筆）、`findReplace.noMatches`，或 `findReplace.replacedCount`（取代數量） |

- 尋找框輸入時不會自動跳到第一筆，只有按 Enter 或按鈕才會移動選取（`InvalidateMatches`，:96-101）。

### 互動行為

- **比對規則**（`RecomputeMatches`，`:104-126`）：
  - 不分大小寫，使用 `StringComparison.CurrentCultureIgnoreCase`（**依目前文化設定**，:118）。
  - 不重疊：每次找到後從 `found + query.Length` 繼續（:124）。
  - 查詢字串為空 → 沒有比對結果，狀態列為空（:109-112）。
  - 比對範圍是整份文件 `view.Document.Text`，不限於選取範圍。
  - 不做循環提示（wrap）的文字提示，但到尾端會循環回頭（見下方）。

- **導覽**（`Navigate(direction)`，`:128-156`）：
  1. 取得目前分頁與編輯器，重算比對（每次導覽都重算，符合註解「編輯會改變位移」）。
  2. 沒有比對結果：狀態列顯示 `findReplace.noMatches`（若查詢非空），結束。
  3. 錨點：
     - 向前（direction ≥ 0）：若有選取，取選取結尾；否則取游標位置（:143-144）。
     - 向後（direction < 0）：若有選取，取選取開頭；否則取游標位置（:145）。
  4. 找下一筆：向前取第一個 `offset >= anchor`；向後取最後一個 `offset < anchor`（:146-148）。
  5. 找不到則循環：向前回到第 0 筆，向後回到最後一筆（:149-152）。
  6. `SelectMatch`：選取範圍長度是**尋找框的文字長度**（`editor.Select(offset, FindBox.Text.Length)`，:161），捲動到該行，然後 `editor.Focus()`（:163）。**每次導覽後焦點都會移到編輯器**，若要繼續輸入尋找字串需要再點回尋找框。
  7. 狀態列：`findReplace.matchCount` 套入（第 _matchIndex+1 筆，共 _matches.Count 筆）（:166-171）。

- **取代目前這一筆**（`ReplaceCurrent`，`:173-211`）：
  1. 查詢為空，或沒有編輯器與分頁 → 結束。
  2. 若目前沒有有效的比對索引，先呼叫一次 `Navigate(1)`（:180-183）。仍無效則結束。
  3. 以 `TextDocument.Replace(offset, query.Length, replacement)` 替換（:192）。
  4. 替換後重算比對（:195）。
     - 無比對 → 游標移到替換文字之後，狀態列顯示 `noMatches`，結束（:196-201）。
     - 有比對 → 找第一個 `>= caretAfter` 的比對作為目前項（找不到則設為 0），游標設到 `caretAfter`，再 `SelectMatch`，更新狀態列（:203-210）。

- **全部取代**（`ReplaceAll`，`:213-253`）：
  1. 查詢為空 → 結束（不跳訊息）。
  2. 以不分大小寫的方式逐段掃描原始文字，組出新字串（:228-243）。
  3. 若有替換（count > 0），以 `TextDocument.Text = 新字串` 整份覆寫（:245-248）。count 為 0 時不寫入。
  4. 清空比對快取，狀態列顯示 `findReplace.replacedCount` 套入數量（:250-251）。即使數量為 0 也會顯示。
  5. 編輯器取得焦點（:252）。
  - 原始碼未指定整份覆寫後的游標與捲動位置、以及 undo 的粒度。

- **已知的不一致（原始碼行為）**：
  - `RecomputeMatches` 的註解寫「必須在每次導覽或取代前呼叫」（:103），但 `ReplaceCurrent` 在使用 `_matches[_matchIndex]` 之前**沒有重算**（:189-192）。
  - 只有在尋找框文字變動（`InvalidateMatches`）或導覽時才會更新快取。因此若使用者在編輯器中打字後直接按「取代」，`_matches` 的位移可能已過期，取代到的位置可能與預期不同。
  - 此為原始碼現況的描述，不是設計建議。

- **鍵盤**：
  - 尋找框：Enter 向前找、Shift+Enter 向後找、Esc 關閉（`:60-71`）。
  - 取代框：Enter 取代目前、Esc 關閉（`:73-84`）。
  - 視窗載入時尋找框取得焦點（`:26`）。
  - `Seed` 會依 `focusReplace` 把焦點放在取代框或尋找框，並全選文字（`:29-46`）。

- **錯誤訊息**：本視窗沒有任何 `MessageDialogs` 呼叫，所有提示都是狀態列文字。

### 主題與語言切換時

- 主題：
  - 背景、前景與輸入框的顏色都以 `DynamicResource` 綁定（`FindReplaceWindow.xaml:9-10、26-28、38-40`），主題資源切換時會跟著變（依 WPF 語意推定）。
- 語言：
  - 文字只在建構時套用（`ApplyLocalization`，`:22`、`:48-58`）。
  - **沒有訂閱 `LanguageChanged`**。主視窗會保留同一個視窗參考，因此切換語言後，尋找視窗的文字仍是舊語言，直到關閉再開。
  - 狀態列文字是在導覽或取代時以當下語言產生，所以會是新語言。

---

## 5. Markdown 語法說明視窗（MarkdownHelpWindow）

- 來源：`src/Glystrata/Views/MarkdownHelpWindow.xaml`、`MarkdownHelpWindow.xaml.cs`

### 用途與開啟方式

- 用途：以閱讀器的渲染規則，顯示 Glystrata 目前支援的 Markdown 語法說明。
- 開啟方式：命令 `HelpMarkdownGuide`，標籤 key `help.markdownGuide`，**沒有快速鍵**（`AppCommand.cs:79`）。命令對應 `MainWindow.xaml.cs:122`，實作在 `ShowMarkdownGuide()`（`MainWindow.xaml.cs:2005-2009`）。選單的實際位置（推測為說明選單）原始碼未在本次讀取範圍內確認。

### 視窗屬性

- 標題：XAML 預設 `Markdown`（`MarkdownHelpWindow.xaml:4`），建構時以 `help.markdownGuide` 覆寫（`MarkdownHelpWindow.xaml.cs:23`）。
- 預設大小：寬 640、高 680（`MarkdownHelpWindow.xaml:5-6`）。
- 最小大小：XAML 未設定，原始碼未指定。
- 可否調整大小：預設（可調整）。
- 是否 modal：否（`window.Show()`，`MainWindow.xaml.cs:2008`）。
- 開啟位置：`CenterOwner`（`MarkdownHelpWindow.xaml:7`），Owner 為主視窗（`MarkdownHelpWindow.xaml.cs:17`）。
- 能否同時開多個：可以。每次點選都 `new`，主視窗沒有保留參考（`MainWindow.xaml.cs:2007-2008`）。
- 背景與前景：`WindowBackgroundBrush`、`PrimaryTextBrush`（`MarkdownHelpWindow.xaml:8-9`）。建構時再以 `FindResource` 設定 Window 與 RootGrid 背景（`xaml.cs:24-25`）。
- Esc：關閉（`DialogKeys.AttachEscapeToClose`，`xaml.cs:19`）。

### 版面結構

1. `Window`（寬 640、高 680）
   1. `RootGrid`（`Grid`，**沒有 Margin**，`MarkdownHelpWindow.xaml:10`）
      - 唯一子元素：`FlowDocumentScrollViewer` `Viewer`（`MarkdownHelpWindow.xaml:11-14`）
        - `VerticalScrollBarVisibility=Auto`
        - `HorizontalScrollBarVisibility=Disabled`
        - `IsToolBarVisible=False`
        - 內容邊距由渲染器的 `PagePadding` 決定：`34,26,34,34`（見第 7 章）。

### 所有控制項

- 只有一個捲動檢視器，沒有其他控制項。
- 內容不是 `TextBlock` 或 `TextBox`，而是 `FlowDocument`，由 `WpfMarkdownRenderer.Render` 產生（`xaml.cs:35`）。
- 使用者可以選取與複製文字（`FlowDocumentScrollViewer` 預設行為，原始碼未另外設定）。

### 內容來源與渲染流程

- 文字內容寫死在程式中，兩個常數：`ChineseContent`（`xaml.cs:38-100`）與 `EnglishContent`（`xaml.cs:102-164`）。**沒有使用 localization key**。
- 選擇依據：`_localization.Language == AppLanguage.English`（`xaml.cs:31`）。非英文一律使用繁體中文。
- 流程（`xaml.cs:29-36`）：
  1. `_markdown.Parse(內容, AppDomain.CurrentDomain.BaseDirectory)` 解析（`xaml.cs:32`）。
  2. 以 `ThemeService.Resolve(_settings.Theme)` 決定亮或暗（`xaml.cs:33`）。
  3. 依亮或暗選擇閱讀器調色盤（`xaml.cs:34`）。
  4. `_renderer.Render(parsed, _settings.PreviewTypography, theme, _localization, palette)`（`xaml.cs:35`）。
- 解析時的目錄是程式的基底目錄，所以內容中的圖片相對路徑不會被解析（本說明沒有圖片）。

### 說明內容的章節結構（供比對用）

中文版（英文版章節相同，順序一致）：

1. H1：Markdown 語法說明
2. 導言段落（說明 Glystrata 文件就是純文字 Markdown 檔）
3. H2「標題」：`#` 到 `######` 對應 H1 到 H6
4. H2「文字樣式」：粗體、斜體、粗斜體、刪除線、行內程式碼
5. H2「清單」：`-`、`*`、`+` 項目符號；`1.` 數字清單；Enter 自動接續；`- [ ]`、`- [x]` 待辦事項
6. H2「引用與程式碼區塊」：`>` 引言；三個反引號圍住的程式碼區塊
7. H2「分隔線」：空行夾住、單獨一行三個以上 `-`
8. H2「表格」：直線分隔、第二行 `---`
9. H2「連結與圖片」：`[文字](網址)`、`![替代文字](路徑)`
10. H2「輸入時自動轉換符號」：`->` → →、`=>` → ⇒、`<->` → ↔、`>=` → ≥、`<=` → ≤、`...` → …、`--` → —（說明中寫明「整行只有 `-`、`:`、`|`、空白時不轉換」）
11. H2「目前不支援」：提到「沒有 Heptabase 那種 Toggle 收合區塊」，以及 `a.`、`i.` 清單

- 注意：「目前不支援」段落的中文文字提到 **Heptabase**（另一款產品名）。是否保留這段文字，列入需人決定事項（第 11 章）。
- 「輸入時自動轉換符號」描述的是編輯器行為，實作位於 `Editing/` 目錄，本次未讀取，此處只記錄說明文字的主張。

### 互動行為

- Esc：關閉。
- 沒有按鈕、沒有輸入框。
- 連結：說明內容中的反引號文字不是超連結；渲染器對連結的處理見第 7 章。

### 主題與語言切換時

- 主題：**不即時**。調色盤與主題在建構時取定（`xaml.cs:33-35`），之後切換主題不會重繪此視窗。背景也在建構時設定一次（`xaml.cs:24-25`）。
- 語言：**不即時**。內容在建構時選定（`xaml.cs:31`），沒有訂閱 `LanguageChanged`。
- 字級與間距：建構時以當下的 `PreviewTypography` 渲染（`xaml.cs:35`），設定改變後需重新開啟。

---

## 6. Markdown 閱讀器視窗（PreviewWindow ＋ PreviewWindowManager）

- 來源：`src/Glystrata/Preview/PreviewWindow.xaml`、`PreviewWindow.xaml.cs`、`PreviewWindowManager.cs`
- 渲染規則見第 7 章，Emoji 見第 8 章。

### 用途與開啟方式

- 用途：把目前文件以 Markdown 閱讀器的方式顯示（唯讀），會隨編輯內容自動更新。
- 開啟與關閉（切換）：
  - 快速鍵 `Ctrl+Shift+R`（`AppCommand.cs:73`，命令 `ViewToggleReader`，無選單標籤 key）→ `ToggleActiveReader()` → `Pane_PreviewRequested`（`MainWindow.xaml.cs:1309-1318`）。
  - 編輯窗格的閱讀器按鈕同樣發出 `PreviewRequested` 事件，進入同一個方法（按鈕位置原始碼未在本次讀取範圍內）。
  - `Pane_PreviewRequested` 的規則（`MainWindow.xaml.cs:1309-1318`）：
    - 副檔名為 `.yaml` 或 `.yml` → 直接返回，不開啟（不支援 YAML 檔閱讀）。
    - 其他情況呼叫 `_previewWindows.Toggle(view, this)`，並更新窗格上閱讀器按鈕的狀態。
- 管理方式（`PreviewWindowManager`，`PreviewWindowManager.cs`）：
  - 以 `view.ViewId`（每個分頁視圖的 id）為鍵，**每個視圖最多一個閱讀器視窗**（`:23-46`）。同一份文件開兩個視圖就可以有兩個閱讀器。
  - `Toggle`：已開啟就關閉（`:29-32`）；未開啟則建立並 `Show()`（`:35-45`）。
  - 視窗關閉時從字典移除並發出 `StateChanged`，讓主視窗刷新窗格按鈕狀態（`:39-43`）。
- 其他入口：
  - 視圖關閉時：`_previewWindows.Close(view)`（`MainWindow.xaml.cs:1681`）。
  - 應用程式關閉時：`_previewWindows.CloseAll()`（`MainWindow.xaml.cs:2798`）。

### 視窗屬性

- 標題：XAML 預設 `Markdown reader`（`PreviewWindow.xaml:4`），執行時由 `GetTitle()` 決定：`{檔名或未命名} — {preview.reader}`（`PreviewWindow.xaml.cs:136-142`）。
  - 未命名文件使用 `document.untitled` 作為檔名。
  - 分隔符是 em dash（`—`）。
- 預設大小：寬 760、高 820（`PreviewWindow.xaml:5-6`）。
- 最小大小：寬 420、高 320（`PreviewWindow.xaml:7-8`）。
- 可否調整大小：預設（可調整）。
- 是否 modal：否（`window.Show()`，`PreviewWindowManager.cs:45`）。
- 開啟位置：`CenterOwner`（`PreviewWindow.xaml:9`），Owner 為主視窗（`PreviewWindowManager.cs:37`）。
- 能否同時開多個：每個視圖一個，不同視圖可同時各開一個（見上）。
- 背景：
  - 視窗與根格線的背景在 `ApplyTheme()` 才設為 `PreviewBackgroundBrush`（`PreviewWindow.xaml.cs:73-78`）。
  - 建構時沒有設定視窗背景。`ApplyTheme` 只在設定套用時呼叫（`MainWindow.xaml.cs:2000`、`PreviewWindowManager.cs:64-70`）。原始碼未指定首次開啟時的視窗背景來源（可能來自全域樣式，未在本次讀取範圍內）。
- Esc：**不關閉**。`PreviewWindow` 的建構式只呼叫 `CustomTitleBar.Attach`，沒有呼叫 `DialogKeys`（`PreviewWindow.xaml.cs:28`）。
- 關閉方式：只能用標題列的關閉按鈕。

### 版面結構

1. `Window`（寬 760、高 820，最小 420×320）
   1. `RootGrid`（`Grid`，`PreviewWindow.xaml:10`）
      - 列定義（`:11-14`）：第 0 列 `1*`，第 1 列 `Auto`。
      - 第 0 列：`FlowDocumentScrollViewer` `Viewer`（`:15-24`）
        - `IsToolBarVisible=False`、`VerticalScrollBarVisibility=Auto`、`HorizontalScrollBarVisibility=Disabled`、`Focusable=True`（`:17-20`）。
        - 輸入綁定：`Ctrl+Insert` → `ApplicationCommands.Copy`（`:21-23`）。
        - 右鍵選單（程式建立，`PreviewWindow.xaml.cs:51-56`）：「複製」（`edit.copy`，命令 Copy）與「全選」（`edit.selectAll`，命令 SelectAll）。
      - 第 1 列：`Border` `ZoomBar`（`:25-52`）
        - `Padding=10,4`，`BorderThickness=0,1,0,0`（只有上緣有線），背景 `SurfaceBrush`，邊框 `BorderBrush`（`:26-30`）。
        - 內容為 `DockPanel`，`LastChildFill=False`（`:31`），子元素依加入順序靠右排列：
          1. `ZoomPercentText`（Dock Right，寬 44，`Margin=6,0,0,0`，文字靠右，垂直置中，前景 `SecondaryTextBrush`，滑鼠指標為手形，`:32-39`）。
          2. `ZoomSlider`（Dock Right，寬 110，垂直置中，樣式 `StatusBarSliderStyle`，`:40-50`）。
      - 由右往左的視覺順序為：百分比文字在最右，滑桿在它左邊。

### 所有控制項

| 控制項 | 類型 | 設定 | 預設 | 行為 |
|---|---|---|---|---|
| `Viewer` | FlowDocumentScrollViewer | `MinZoom=50`、`MaxZoom=200`、`ZoomIncrement=10`（`PreviewWindow.xaml.cs:44-46`） | 縮放 100%（`:60`） | 顯示渲染結果；Ctrl+滾輪縮放 |
| `ZoomSlider` | Slider | 最小 50、最大 200、刻度 10、吸附、小變化 10、大變化 10（`PreviewWindow.xaml:42-48`） | 與縮放同步 | 拖曳改變縮放；工具提示 `preview.zoom` |
| `ZoomPercentText` | TextBlock | 文字格式 `{四捨五入百分比}%` | `100%` | 雙擊重設為 100%（`:153-159`）；工具提示 `preview.zoom` |

### 互動行為

- 文字變動與自動更新（`PreviewWindow.xaml.cs:90`、`:130-134`）：
  - 訂閱 `_view.Document.TextChanged`，每次變動呼叫 `ScheduleRefresh()`。
  - `ScheduleRefresh` 會停止並重新啟動一個 **250 毫秒**的 `DispatcherTimer`（去抖動）。
  - 計時器到期才呼叫 `Refresh()`。
- 重新渲染（`Refresh`，`:100-128`）：
  1. 取得文字與來源目錄：有檔案路徑時用檔案所在目錄；未命名時用 `Environment.CurrentDirectory`（`:103-106`）。
  2. 以 generation 計數器標記這次請求（`:102`）。
  3. 在背景執行緒做 Markdown 解析：`Task.Run(() => _previewService.Parse(text, sourceDirectory))`（`:111`）。
  4. 解析時若發生 `IOException` 或 `UnauthorizedAccessException`，**靜默返回**，不更新畫面（`:113-116`）。
  5. 回到 UI 後，若視窗已關閉或已有更新的請求，丟棄結果（`:118-121`）。
  6. 以目前設定的閱讀器調色盤與排版渲染，整份替換 `Viewer.Document`（`:123-125`）。
  7. 更新標題，並以 `_zoomPercent` 重新套用縮放（`:126-127`）。
  - 每次都是整份重新渲染，沒有增量更新。
  - 捲動位置在替換文件後的行為：原始碼未指定。
- 縮放：
  - 滑鼠滾輪：只有在 **Ctrl 單獨按下**（`Keyboard.Modifiers == Control`，完全相等）時才處理，每格 ±10，並標記為已處理（`:161-169`）。Ctrl+Shift 等組合不會縮放。
  - 縮放範圍夾在 50–200（`:173`）。
  - 縮放值保存在 `_zoomPercent`，在同一個視窗的多次重新渲染之間保留（`:15`、`:127`）。
  - 視窗建構完成時會強制 `ApplyZoom(100)`（`:60`），因此每次開啟都從 100% 開始，不會記住上次的值。
- 連結（`RenderBlock` 的 `a` 元素，見第 7 章）：
  - 只有 `http`、`https`、`mailto` 的連結會透過系統開啟（`PreviewWindow` 與渲染器的 `OpenLink`，`WpfMarkdownRenderer.cs:517-533`）。
  - 其他協定（例如本機檔案）直接忽略。
  - 滑鼠停在連結上會顯示網址作為工具提示。
- 關閉（`OnClosed`，`:80-88`）：
  - 設定 `_closed` 旗標，取消 TextChanged 與語言變更的訂閱，停止計時器，觸發 `PreviewClosed`。
- 右鍵選單與複製：見版面結構。

### 主題與語言切換時

- 主題：
  - 設定套用時，主視窗呼叫 `_previewWindows.ApplyTheme()`（`MainWindow.xaml.cs:2000`），每個閱讀器執行 `ApplyTheme()`：更新背景並排程重新渲染（`PreviewWindow.xaml.cs:73-78`）。
  - 因此**設定套用後即時**，閱讀器會以新主題重新渲染。
  - 作業系統主題變更是否會自動觸發此流程，取決於 `Services/SystemThemeWatcher.cs`（本次未讀取），原始碼未在本次範圍內確認。
- 語言：
  - 訂閱 `LanguageChanged`（`:41`）。收到事件時立即更新標題、縮放工具提示、右鍵選單文字，並呼叫 `Refresh()` 重新渲染（`:92-98`）。
  - 來源：設定套用時 `_previewWindows.RefreshLanguage()` 會呼叫 `UpdateSettings`（`PreviewWindowManager.cs:72-78`）。
  - 因此**語言切換即時**。

---

## 7. 閱讀器渲染規則（WpfMarkdownRenderer）

- 來源：`src/Glystrata/Preview/WpfMarkdownRenderer.cs`（548 行，全部讀過）、解析管線 `src/Glystrata.Core/Markdown/`（見第 10 章）。
- 入口：`Render(MarkdownPreviewDocument, PreviewTypography, ThemeKind, LocalizationService, ReaderColorPalette?)`（`:14-68`）。
- 作法：先把 Markdig 產生的 HTML 以 `XElement.Parse` 包在 `<glystrata-root>` 下（`:47`，保留空白 `PreserveWhitespace`），再逐一把 HTML 元素轉成 WPF 的 `Block` 與 `Inline`。HTML 解析失敗時，整段原始 HTML 以純文字段落顯示（`:53-59`）。

### 調色盤與基礎值

顏色來源（`:21-33`）：

| 用途 | 來源 | 亮色預設 | 暗色預設 |
|---|---|---|---|
| 內文 Foreground | 閱讀器調色盤 `text` | `#24292F`（`AppSettings.cs:84`） | `#E6EAF0`（`AppSettings.cs:100`） |
| 標題 Heading | 調色盤 `heading` | `#24292F`（:85） | `#E6EAF0`（:101） |
| 連結 Link | 調色盤 `link` | `#0969DA`（:86） | `#79C0FF`（:102） |
| 引言 Quote | 調色盤 `quote` | `#68707C`（:87） | `#A6ADB8`（:103） |
| 程式碼 Code | 調色盤 `code` | `#24292F`（:88） | `#E6EAF0`（:104） |
| 次要色 Muted（引言邊線、分隔線） | **寫死**（:23、`defaultMuted`） | `#68707C` | `#A6ADB8` |
| 底色 Surface（程式碼區塊、行內程式碼、表格邊框與表頭） | **寫死**（:29） | `#F1F3F5` | `#292E36` |

- 調色盤缺某個 key 時，使用 `Get(key, 預設)` 的預設值（`:27-33`）。
- Muted 與 Surface 不在調色盤中，使用者無法在設定中修改（`SettingsWindow` 的閱讀器顏色分頁只有 5 個 key，見第 1 章）。

FlowDocument 基礎（`:35-43`）：

- 字型：`Segoe UI, Segoe UI Emoji`（`:37`）。
- 字級：15（`:38`）。
- 前景：內文色；背景：`PreviewBackgroundBrush` 資源（`:39-40`）。
- `PagePadding=34,26,34,34`（`:41`）。
- `LineHeight=22`（`:42`）。

### 區塊元素

以下「H1」「段落」等指 Markdig 產生的 HTML 元素。`typography` 的欄位見第 1 章對照表。

| 元素 | WPF 型別 | 字型 | 字級 | 字重 | 顏色 | 間距 | 行高 | 來源 |
|---|---|---|---|---|---|---|---|---|
| h1 | Paragraph | 繼承 | H1Size（預設 32） | SemiBold | Heading | 上 HeadingSpacing（14）、下 ParagraphSpacing（10） | 字級 × LineSpacing | `:81-98` |
| h2 | Paragraph | 繼承 | H2Size（26） | SemiBold | Heading | 同上 | 同上 | 同上 |
| h3 | Paragraph | 繼承 | H3Size（22） | SemiBold | Heading | 同上 | 同上 | 同上 |
| h4 | Paragraph | 繼承 | H4Size（18） | SemiBold | Heading | 同上 | 同上 | 同上 |
| h5 | Paragraph | 繼承 | H5Size（16） | SemiBold | Heading | 同上 | 同上 | 同上 |
| h6 | Paragraph | 繼承 | H6Size（14） | SemiBold | Heading | 同上 | 同上 | 同上 |
| p | Paragraph | 繼承 | 15（FlowDocument 預設） | 一般 | 內文 | 下 ParagraphSpacing（10），上 0 | `15 × LineSpacing` | `:100-109` |
| blockquote | Paragraph | 繼承 | 15 | 一般 | Quote（引言色） | 左 12、下 ParagraphSpacing | `15 × LineSpacing` | `:111-126` |
| pre（程式碼區塊） | Paragraph | `Cascadia Mono, Consolas, Segoe UI Emoji` | 13 | 一般 | Code | 上 4、下 ParagraphSpacing；**內距 12 四邊** | 19（固定） | `:128-142` |
| ul／ol | List | 繼承 | 15 | 一般 | 內文 | 左 18、下 ParagraphSpacing；內距左 4 | 見清單 | `:144-147`、`:184-272` |
| table | Table | 繼承 | 15 | 一般 | 內文 | 上 2、下 ParagraphSpacing | 儲存格 `15 × LineSpacing` | `:149-151`、`:274-321` |
| hr | BlockUIContainer（Border，高 1） | — | — | — | Muted（寫死） | 上 4、下 ParagraphSpacing | — | `:153-160` |
| 其他區塊元素（如 div） | 見「未知元素」 | | | | | | | `:162-180` |

- 標題的字重是 **SemiBold（600）**，不是 Bold（`:92`）。
- 標題內的行內元素（粗體、連結等）以標題色作為預設 Foreground（`:96`）。
- 段落的行高寫的是 `15 * LineSpacing`，不是實際字級乘以行距（`:105`、`:119`、`:233`、`:306`）。標題則是用標題字級（`:94`）。

#### 引言（blockquote）

- 外觀：左側 3 pt 的邊線，顏色 Muted；內距 `12,2,0,2`（`:114-117`）。
- 文字顏色：Quote（引言色）。
- 內容組裝（`:121-124`）：把每個子元素（例如 `<p>`）的行內內容**依序加入同一個段落**，子元素之間**沒有插入換行或間距**。因此多段引言會被合併成一段。
- 巢狀引言：`AddInlines` 遇到未知的子元素會遞迴展開（見下方「行內元素」），所以巢狀引言也會被攤平成同一段。

#### 程式碼區塊（pre）

- 文字來源：`element.Element("code")?.Value ?? element.Value`（`:139`），即純文字，不解析行內語法。
- 內容使用單一 `Run`，**不經過 Emoji 處理**（`:140`）。
- 沒有語言標示、沒有語法高亮、沒有行號、沒有捲動。換行依 Paragraph 的自動換行（原始碼未設定 `TextWrapping`）。

#### 清單（ul、ol）

使用 `List` 元素，建立規則在 `CreateList`（`:184-272`）：

- 一般無序清單：`MarkerStyle=Disc`，`Margin=18,0,0,ParagraphSpacing`，`Padding=4,0,0,0`（`:214-220`）。
- 一般有序清單：`MarkerStyle=Decimal`，邊距與內距同上。
- **任務清單**：只要清單中有任何一個項目開頭是核取方塊（`FindTaskCheckbox`，`:197-208`），整個清單變成任務清單。
  - 無序任務清單：`MarkerStyle=None`，`Margin=0`，`Padding=0`（`hangsMarkers` 為真，`:211-220`）。核取方塊取代項目符號，並「懸掛」在文字左側。
  - 有序任務清單：保留 `Decimal` 數字，`Margin=18`，核取方塊放在數字之後（`:194-196`）。
- 任務項目（核取方塊）的繪製（`CreateCheckbox`，`:480-505`）：
  - 外框 `Border`：圓角 3，邊框 1.4，未勾選時邊框 Muted、背景透明；勾選時邊框與背景都是 Link 色。
  - 勾選時的勾號：`Path`，資料 `M 2.6,6.6 L 5.4,9.4 L 10.4,3.6`，白色，粗細 1.7，圓頭圓尾（`:492-501`）。
  - 整體放在 `MarkerSlot` 中：寬高 14、右側間距 8（`MarkerSize=14`、`MarkerGap=8`，`:453-456`、`:459-468`）；`BaselineAlignment=Center`；`IsHitTestVisible=false`。
- 任務清單內的一般項目：
  - 無序：畫一個直徑 5.5 的圓點（`CreateBullet`，`:470-478`，填色為 Foreground），置中於 14×14 的槽內。
  - 有序：留一個空白的 14×14 槽（`:236-239`），確保文字對齊。
- 項目段落：
  - `Margin` 下方：若該項目有 `<p>`（鬆散清單）用 ParagraphSpacing，否則 4（`:231`）。
  - `LineHeight = 15 × LineSpacing`（`:233`）。
- 巢狀清單：遞迴呼叫 `CreateList`（`:244`）。在任務清單中，子清單的左邊距加上 `MarkerSize + MarkerGap`（22），使其縮排在文字下方（`:249-251`）。
- 文字修整：
  - 項目最後一個文字節點若緊接著巢狀清單，會 `TrimEnd`，避免多出空行（`:256-261`）。
  - 核取方塊後的文字會 `TrimStart`，因為槽位已經提供間距（`:200-205`）。
- 核取方塊的判定（`FindTaskCheckbox`，`:434-443`）：只有項目（或鬆散清單中的第一個 `<p>`）的**第一個有意義的內容**是核取方塊，才算任務項目。行中間或巢狀清單中的 `[x]` 不算，會以純文字 `[x]` 或 `[ ]` 顯示（`:417-419`）。

#### 表格（table）

建立規則在 `CreateTable`（`:274-321`）：

- 表格本體：`CellSpacing=0`，邊框 `BorderThickness=1`，邊框顏色 Surface（寫死），`Margin=0,2,0,ParagraphSpacing`（`:281-287`）。
- 欄數：取所有列中最多的儲存格數（`:290`）；每欄寬度為 `*`（等寬）（`:291-294`）。
- 列：所有 `tr`（包含表頭與表身）放在同一個 `TableRowGroup`（`:296-319`）。
- 儲存格：
  - 內容為 `Paragraph`，`Margin=7,4,7,4`，前景內文色，`LineHeight=15 × LineSpacing`（`:302-307`）。
  - 邊框 Surface 色，粗細 `0,0,1,1`（只有右與下）（`:309-313`）。
  - 表頭（`thead`）的儲存格背景為 Surface，表身無背景（`:313`）。
- 對齊方式：渲染器**沒有處理** Markdown 的欄位對齊語法（`:---:` 等），原始碼未指定，預設為靠左。
- 表格內的行內元素與一般段落相同（見下）。

#### 分隔線（hr）

- `BlockUIContainer` 包住一個 `Border`，高 1，背景 Muted，`Margin=0,4,0,ParagraphSpacing`（`:153-160`）。寬度為整欄寬。

#### 未知區塊元素

- 若元素含有任一區塊子元素（`BlockElements` 中的 h1–h6、p、ul、ol、blockquote、pre、table、hr，`:7-10`），**遞迴處理子元素**（`:163-168`）。
- 否則包成一個段落，下方 ParagraphSpacing，內文色（`:172-178`）。

#### 段落與區塊之間的間距小結

- 段落、引言、程式碼區塊、清單、表格、分隔線的下方間距都是 **ParagraphSpacing**（預設 10）。
- 標題的上方間距是 HeadingSpacing（預設 14），下方是 ParagraphSpacing。
- 程式碼區塊上方固定 4。

### 行內元素

`AddInlines`（`:323-427`）：

| 元素 | 處理 | 字型與外觀 | 來源 |
|---|---|---|---|
| 文字節點 | 經 `EmojiRenderer.AddText` 切分（見第 8 章） | 前景為傳入的顏色 | `:333-337` |
| strong | `Bold`，子內容遞迴 | 字重 Bold（700），前景沿用傳入值 | `:347-350` |
| em | `Italic`，子內容遞迴 | 斜體，前景沿用傳入值 | `:352-355` |
| del／s | `Run(element.Value)`，**只取純文字**，不遞迴 | 刪除線（Strikethrough），前景沿用 | `:357-360` |
| code | `Run(element.Value)`，**只取純文字** | 前景 Code，背景 Surface，字型 `Cascadia Mono, Consolas, Segoe UI Emoji`，字級 13 | `:362-370` |
| a | `Hyperlink`，子內容遞迴 | 前景 Link 色；ToolTip 為 href；點擊 → `OpenLink` | `:371-381` |
| br | `LineBreak` | — | `:382-383` |
| img | 見下方「圖片」 | — | `:385-410` |
| input（核取方塊） | 任務項目 → 核取方塊；其他 → 文字 `[x]`／`[ ]` | 前景沿用傳入值 | `:411-421` |
| 其他（未知） | 遞迴展開子節點，**不加任何樣式** | — | `:422-425` |

- `del`、`s` 與 `code` 不經過 Emoji 處理，也不解析其中的巢狀格式（`:359`、`:363`）。
- 因此 `~~**粗**~~` 這類組合只會顯示刪除線純文字（推論，依程式碼）。
- Markdig 產生的 `ins`、`sub`、`sup`、`mark` 等若出現，會落入「未知」分支，只保留文字（見第 10 章的管線設定）。

#### 連結（a）

- 前景：`render.Link`（:372）。
- 工具提示：`href`（:377）。
- 點擊：`OpenLink(href)`（:378）。
- 外部開啟（`OpenLink`，`:517-533`）：只允許 `http`、`https`、`mailto`；以 `ProcessStartInfo(href) { UseShellExecute = true }` 開啟；失敗時靜默忽略（`:525-532`）。
- 底線：渲染器沒有設定 `TextDecorations`，是否有底線取決於 WPF 預設的 Hyperlink 樣式（原始碼未指定）。
- 沒有 `href` 時仍建立 Hyperlink（前景為連結色），但沒有點擊事件（`:375-379`）。

#### 圖片（img）

- 來源解析：以 `src` 屬性在 `document.Resources` 中找對應項目（`:386-387`）。解析規則見第 10 章。
- 可用（`IsAvailable` 且有 `ResolvedPath`）：
  - 建立 `BitmapImage`（以絕對 file URI 載入），包成 `Image`：`MaxWidth=720`，`Stretch=Uniform`，工具提示為 `alt`（`:388-400`）。
  - 包成 `InlineUIContainer`，即**行內**圖片（`:399`）。
  - 載入例外（`IOException`、`UriFormatException`、`NotSupportedException`）→ 顯示錯誤文字（`:401-404`）。
- 不可用：顯示文字 `[{preview.missingImage}: {src}]`，前景 `Brushes.IndianRed`（寫死，`:407-409`）。
- 未使用的錯誤訊息：`PreviewResource.ErrorMessage` 已在解析端產生（例如「找不到圖片檔案。」），但渲染器不顯示它（只用 `IsAvailable`，`:387-388`）。
- 圖片不會依文件目錄以外的路徑載入（解析端限制，第 10 章）。

#### 核取方塊（非任務）

- 文字節點：`[x]` 或 `[ ]`，前景沿用傳入值（`:419`）。

### 其他規則

- 原始 HTML：管線設定 `DisableHtml()`（`MarkdownPipelineFactory.cs:16`），HTML 標籤不會被當成 HTML。測試程式以 `<script>` 為例，斷言 HTML 中不含 `<script`（`tests/Glystrata.Verification/Program.cs:351`）。推論：標籤被跳脫後以純文字顯示。
- YAML front matter：管線設定 `UseYamlFrontMatter()`（`MarkdownPipelineFactory.cs:15`）。測試程式斷言 front matter 的內容不出現在 HTML 中（`Program.cs:352`）。因此閱讀器**不顯示** front matter。
- 任務清單：管線 `UseTaskLists()`（`:12`）。
- 管線擴充：`UsePipeTables`（`:11`）、`UseEmphasisExtras`（`:13`）、`UseAutoLinks`（`:14`）。詳見第 10 章。
- Setext 標題：**關閉**（`MarkdownPipelineFactory.cs:23-26`）。`文字` 下一行接 `---` 或 `===` 不會變成標題。推論：`---` 會變成分隔線（依 CommonMark 規則，未在本機驗證）。
- 標題錨點、腳註、數學式、定義清單、縮寫：管線未啟用（推論，依 `MarkdownPipelineFactory.cs` 只列出的擴充）。
- 圖片的寬度控制、表格對齊、程式碼語法高亮、行號：原始碼均未處理。

### 主題與渲染的更新時機

- `Render` 每次呼叫都會建立新的 `FlowDocument` 與所有 `Brush`（`CreateBrush` 會 `Freeze`，`:535-547`）。
- 閱讀器在文字變動（去抖動 250 毫秒）、設定套用、語言切換、主題套用時重新渲染（第 6 章）。

---

## 8. Emoji 處理（EmojiScanner ／ EmojiRenderer）

- 來源：`src/Glystrata/Preview/EmojiScanner.cs`、`EmojiRenderer.cs`；資源 `src/Glystrata/Resources/Emoji/twemoji-72.zip`（約 3.96 MB，Twemoji 15.1.0，見 `THIRD-PARTY-NOTICES.txt:103`）。
- 作用範圍：只在閱讀器中使用。編輯器不在本次範圍。

### 偵測（EmojiScanner.MatchSequence，`EmojiScanner.cs:16-80`）

1. **鍵帽序列**（`:23-38`）：基底為 `#`、`*`、`0`–`9` 之一，後面可接 U+FE0F（可省略），再接 U+20E3（鍵帽）才算 emoji；只有數字不會單獨成為 emoji。
2. **一般序列**（`:40-78`）：第一個碼點必須落在以下範圍（`IsEmojiStart`，`:154-167`）：
   - U+1F000–U+1FAFF
   - U+2600–U+27BF
   - U+2B00–U+2BFF
   - U+00A9、U+00AE、U+203C、U+2049、U+2122、U+2139
   - U+2194–U+21AA
   - U+231A–U+231B
   - U+23E9–U+23FA
   - U+24C2
   - U+25AA–U+25FE
   - U+2934、U+2935、U+3030、U+303D、U+3297、U+3299
3. **延伸**（`:45-78`）：後續碼點若是 U+FE0F、U+20E3、膚色修飾（U+1F3FB–U+1F3FF）、標籤字元（U+E0020–U+E007F）就併入。
4. **零寬連接字元 ZWJ（U+200D）**（`:56-67`）：只有 ZWJ 後面接的碼點也是 emoji 起始時才併入，否則停止。
5. **國旗**（`:69-74`）：第一個碼點是區域指示符號（U+1F1E6–U+1F1FF），且後面緊接第二個區域指示符號時併入，只限一對。
6. 回傳值是 UTF-16 長度（`:16`）。

### 對應圖檔（EmojiScanner.CandidateKeys，`:83-107`）

依序嘗試以下檔名（碼點以小寫十六進位、`-` 連接，`Format` `:109-110`），找到就停止：

1. 主要候選：若序列含 ZWJ 則保留 U+FE0F，否則移除 U+FE0F（`:88`）。
2. 移除 U+FE0F 後的版本（若與 1 不同，`:91-95`）。
3. 再移除膚色修飾後的版本（若與 2 不同且不為空，`:97-101`）。
4. 只取第一個碼點（序列長度大於 1 時，`:103-106`）。

- 圖檔在 zip 中的名稱為 `{key}.png`（`EmojiRenderer.cs:115`）。
- 依 key 快取結果，包含失敗的 `null`（`:89-110`）。快取是靜態 `Dictionary`，未加鎖（`:13`）。

### 繪製（EmojiRenderer.AddText，`EmojiRenderer.cs:19-57`）

- 切分：文字逐字掃描，每找到能成功載入圖檔的序列，就把前面的文字放進 `Run`（帶傳入的前景色），再加入圖片容器。找不到圖檔的序列**保留在文字中**，交給字型顯示（`:37-42`）。
- 圖片元素（`TryCreateImage`，`:59-87`）：
  - `Image`：`Stretch=Uniform`，`SnapsToDevicePixels=true`，`BitmapScalingMode=HighQuality`（`:67-73`）。
  - 包在 `InlineUIContainer` 中，`BaselineAlignment=TextBottom`（`:74`）。
  - **尺寸跟著字級**：`Width` 與 `Height` 都綁定到容器的 `FontSize`，經 `EmojiScaleConverter` 換算為 `FontSize × 1.15`；字級不是正數時用 16（`:76-85`、`:161-168`）。
  - 例：內文 15 pt → 約 17.25；H1 32 pt → 約 36.8。
- 圖檔讀取（`LoadFromArchive`，`:112-139`）：從 pack URI `pack://application:,,,/Resources/Emoji/twemoji-72.zip` 開啟 zip（`:11`、`:141-159`），讀入 `MemoryStream`，建立 `BitmapImage`（`OnLoad` 快取，`Freeze`）。讀取失敗回傳 null。

### 套用範圍

| 位置 | 是否處理 Emoji | 來源 |
|---|---|---|
| 段落文字、標題、清單項目、引言、表格儲存格、連結、粗體、斜體 | 是（經 `AddInlines` 的文字節點） | `WpfMarkdownRenderer.cs:333-337` 與各遞迴呼叫 |
| 程式碼區塊（pre） | **否**，直接放 `Run` | `WpfMarkdownRenderer.cs:140` |
| 行內程式碼（code） | **否** | `WpfMarkdownRenderer.cs:363-369` |
| 刪除線（del、s） | **否** | `WpfMarkdownRenderer.cs:359` |

- 未處理的 Emoji 會以字型顯示，WPF 的彩色字型支援有限（`EmojiRenderer.cs:6-7` 註解表示 WPF 會把彩色字型畫成單色字形）。

---

## 9. 對話框（MessageDialogs、InputDialogs、ColorPickerDialog）

- 來源：`src/Glystrata/Dialogs/MessageDialogs.cs`、`InputDialogs.cs`、`ColorPickerDialog.cs`
- 共用按鍵輔助：`src/Glystrata/Controls/DialogKeys.cs`（`AttachEscapeToClose`：Esc 關閉；`AttachConfirmCancel`：Esc 關閉，Enter 執行確認，但焦點在 `AcceptsReturn` 的多行輸入框時 Enter 不攔截）。
- 共用標題列：`CustomTitleBar.Attach(window, localization)`，標題列高 32（`src/Glystrata/Controls/CustomTitleBar.cs:64、95`）。

### 9.1 MessageDialogs.Show ／ Confirm ／ Inform

- 用途：取代 `MessageBox` 的主題化提示框，回傳被按下按鈕的索引，被關閉時回傳 -1（`MessageDialogs.cs:5-8`）。
- 呼叫方式：
  - `Show(owner, loc, title, message, params buttons)`（`:11`）。
  - `Confirm(owner, loc, title, message, confirmLabel)`：等於 `Show(..., confirmLabel, dialog.cancel) == 0`（`:101-102`）。
  - `Inform(owner, loc, title, message)`：只有一顆 `dialog.ok`（`:104-105`）。
- 視窗屬性（`:20-28`、`:84-89`）：
  - 寬 420，`ResizeMode=NoResize`，`ShowInTaskbar=false`。
  - 位置：有 owner 為 `CenterOwner`，否則 `CenterScreen`（`:26`）。
  - 背景：`WindowBackgroundBrush`（以動態資源綁定，`:29`）。
  - 呼叫 `ShowDialog()`，modal。
  - 高度：先量測內容（寬 420、高度無限制），高度 = `Ceiling(32 + 內容高) + 2`，夾在 150 與 `max(200, 螢幕工作區高 × 0.7)` 之間（`:93-99`）。註解說明不用 `SizeToContent`，避免自訂標題列建立後整窗變黑（`:85-86`）。
- 版面：
  - 根為 `DockPanel`，`Margin=20,18,20,16`，`LastChildFill=true`（`:31`）。
  - 底部按鈕列：靠右 `StackPanel`，`Margin=0,18,0,0`，`DockPanel.Dock=Bottom`（`:32-39`）。
  - 訊息：`TextBlock`，自動換行，前景 `PrimaryTextBrush`（動態），放在不可聚焦的 `ScrollViewer` 中，垂直捲動自動、水平停用（`:41-56`）。
- 按鈕（`:58-82`）：
  - `Padding=16,6`，`Margin=6,0,0,0`，`MinWidth=88`。
  - 索引 0：`IsDefault=true`（Enter 觸發），背景 `AccentBrush`，前景白色（`:71-75`）。
  - 最後一顆：`IsCancel=true`（Esc 觸發）。**只有一顆按鈕時，它同時是預設與取消按鈕**，所以 Inform 的 Esc 等同於按確定（由程式碼推得，`:69`）。
  - 點擊：記錄索引並 `DialogResult = true`（`:76-80`）。

### 9.2 InputDialogs.Prompt（單行輸入）

- 位置：`InputDialogs.cs:12-66`。
- 視窗：寬 390、高 170（固定），`NoResize`，`CenterOwner`（有 owner 時）或 `CenterScreen`，背景 `WindowBackgroundBrush`（以 `FindResource` 靜態取得，`:14-23`）。
- 版面（`:25-53`）：`Grid`，`Margin=18`，三列皆 `Auto`：
  - 第 0 列：標題文字 `TextBlock`（傳入的 `label`），前景 `PrimaryTextBrush`，`Margin=0,0,0,8`。
  - 第 1 列：`TextBox`，`MinWidth=300`，初始值為 `initialValue`。
  - 第 2 列：按鈕列，靠右，`Margin=0,14,0,0`；Cancel（`dialog.cancel`）在左，OK（`dialog.ok`）在右；兩顆都是 `Padding=14,5`、`Margin=5,0,0,0`，**沒有 IsDefault、沒有強調色**。
- 沒有 localization 參數時，按鈕文字退回英文 `Cancel`／`OK`（`:46-48`）。
- 標題列：只有傳入 localization 時才 Attach（`:55-58`）。
- 按鍵：`DialogKeys.AttachConfirmCancel`（`:59`）。Esc → 取消；Enter → 確認（輸入框 `AcceptsReturn=false`，所以 Enter 會確認）。
- 載入時：輸入框取得焦點並全選（`:60-64`）。
- 回傳：確認時回傳 `PromptResult(true, 輸入值.Trim())`；取消回傳 `PromptResult.Cancelled`（`:65`）。
- 驗證：**沒有空字串檢查**，空白也會以 Ok 回傳，由呼叫端判斷（呼叫端在 `MainWindow.xaml.cs:1292`、`1844`、`2020`、`2037`）。
- 主題與語言：只在開啟時取得文字，視窗短暫，原始碼未處理即時更新。

### 9.3 InputDialogs.SelectGroup（選擇群組）

- 位置：`InputDialogs.cs:68-115`。
- 呼叫端：`MainWindow.xaml.cs:1734`（「移到群組」，標題 `group.moveTo`）。
- 視窗：寬 360、高 160（固定），`NoResize`，`CenterOwner` 或 `CenterScreen`，背景 `WindowBackgroundBrush`（`:76-85`）。
- 版面（`:86-105`）：
  - `StackPanel`，`Margin=18`。
  - `ComboBox`：項目為群組名稱陣列，`SelectedIndex` 為目前群組索引，找不到則為 0。**沒有設定寬度**，會撐滿可用寬度（`:87-91`）。
  - 按鈕列靠右，`Margin=0,14,0,0`：Cancel、OK（`Padding=14,5`，`Margin=5,0,0,0`）。
- 沒有群組時直接回傳 null，不開視窗（`:71-74`）。
- 按鍵：`DialogKeys.AttachConfirmCancel`（`:111`）。Esc 取消，Enter 確認。
- 回傳：確認且索引有效時回傳對應群組，否則 null（`:112-114`）。

### 9.4 ColorPickerDialog.Show（HSV 色彩選擇）

- 呼叫端：`SettingsWindow.ChooseColor`（`SettingsWindow.xaml.cs:683-692`），傳入標題 `settings.pickColor`。
- 視窗（`ColorPickerDialog.cs:20-30`）：寬 280，`SizeToContent=Height`，`NoResize`，`CenterOwner` 或 `CenterScreen`，背景 `WindowBackgroundBrush`，`ShowInTaskbar=false`。
- 版面（`:32-91`）：`StackPanel`，`Margin=18`，由上到下：
  1. **SV 方塊**（`svCanvas`）：寬高 220（`SvSize`），`ClipToBounds`，游標為十字。外層 `Border` 邊框 `BorderBrush` 1 px（`:34-59`）。由下到上疊四層：
     - `hueBase`：純色，色相為目前 H，飽和 1、明度 1。
     - `whiteFade`：白色到透明，左到右（水平）。
     - `blackFade`：透明到黑色，上到下（垂直）。
     - `svThumb`：直徑 14 的圓，白色外框 2 px、透明內部（`:48`）。
  2. **色相條**（`hueCanvas`）：寬 220、高 18（`HueHeight`），上方 `Margin=0,10,0,0`，游標為手形（`:61-66`）。
     - `hueStrip`：7 個色停（0、60、120、180、240、300、360 度），圓角 3（`:62`、`:178-187`）。
     - `hueThumb`：6 × 22 的白色外框矩形，粗 2（`:63`）。
  3. **預覽列**（`:68-82`）：`DockPanel`，`Margin=0,14,0,0`。
     - 左側色塊 `Border`：寬 32、高 28，圓角 4，`Margin=0,0,8,0`，邊框 `BorderBrush` 1 px。
     - 右側 `TextBox` `hexBox`：填滿，內容垂直置中。
  4. **按鈕列**（`:84-91`）：靠右，`Margin=0,14,0,0`；Cancel、OK（`Padding=14,5`，`Margin=5,0,0,0`）。
- 互動：
  - 在 SV 方塊按下左鍵即開始擷取滑鼠（`CaptureMouse`），移動時更新；放開時釋放（`:133-145`）。
  - 色相條同理（`:147-159`）。
  - 飽和度 S = x ÷ 220，明度 V = 1 − y ÷ 220，皆夾在 0–1（`:120-125`）。
  - 色相 H = x ÷ 220 × 360（`:127-131`）。
  - 手動輸入 `hexBox`：每次文字變動時，若可解析為顏色就更新 HSV 與所有元件；更新時以 `updatingFromCode` 旗標避免迴圈（`:161-169`）。
  - 每次更新後，`hexBox` 會被改寫為大寫的 `#RRGGBB` 格式（`:115`）。
- 鍵盤：`DialogKeys.AttachConfirmCancel`（`:98`）。Enter 確認，Esc 取消。
- 回傳：確認且 `hexBox` 可解析時回傳 `#RRGGBB`（大寫），否則 null（`:173-175`）。
- 初始顏色：可解析的初始值，否則 `Colors.Gray`（`#808080`，`:17`）。載入時更新一次畫面（`:171`）。
- 色彩解析：使用 WPF 的 `BrushConverter`（`:199`），因此支援 WPF 能解析的格式（例如 `#RGB`、命名色，推定）。
- 有 Alpha 的輸入（`#AARRGGBB`）：只取 RGB，Alpha 被丟棄（`:203` 只讀取 `Color` 的 RGB 值，由程式碼推得）。

---

## 10. 資料來源：Core 的 Markdown、Snapshots、設定檔

### 10.1 Markdown 管線與解析服務

來源：`src/Glystrata.Core/Markdown/MarkdownPipelineFactory.cs`、`MarkdownPreviewService.cs`、`MarkdownPreviewModels.cs`。Markdig 版本為 **1.3.2**（`Directory.Packages.props`）。

管線設定（`MarkdownPipelineFactory.cs:8-29`）：

| 設定 | 行號 | 效果 |
|---|---|---|
| `UsePipeTables()` | :11 | 管線表格 |
| `UseTaskLists()` | :12 | `- [ ]`、`- [x]` 任務清單 |
| `UseEmphasisExtras()` | :13 | 啟用強調擴充（具體包含哪些，見第 11 章） |
| `UseAutoLinks()` | :14 | 裸露的網址自動變成連結 |
| `UseYamlFrontMatter()` | :15 | 開頭的 YAML 區塊獨立處理（閱讀器不顯示） |
| `DisableHtml()` | :16 | HTML 不生效 |
| `ParseSetexHeadings = false` | :23-26 | 關閉「文字＋底線式」標題 |

- 沒有呼叫 `UseAdvancedExtensions()`，所以未啟用的擴充包括腳註、數學式、定義清單、縮寫等（由程式碼推得）。

解析服務（`MarkdownPreviewService.cs`）：

- `Parse(markdown, sourceDirectory)`（`:24-35`）：
  1. 把來源目錄轉為完整路徑。
  2. `Markdig.Markdown.ToHtml(markdown, pipeline)` 產生 HTML（`:30`）。
  3. 以 regex `<img\s+[^>]*src="(?<src>[^"]+)"`（不分大小寫，`:13-15`）找出所有圖片來源，逐一解析為 `PreviewResource`（`:31-33`）。
- `ResolveResource(rawSource, baseDirectory)`（`:37-67`）：
  1. 先做 HTML 解碼（`WebUtility.HtmlDecode`，`:40`）。
  2. 絕對 file URI → 取本機路徑，再檢查是否在文件目錄內（`:41-45`）。
  3. 其他絕對 URI（例如 http）→ 不可用，錯誤訊息「預覽只載入本機相對圖片。」（`:47-50`）。
  4. 相對路徑：去掉 `#` 與 `?` 之後的部分（`:52`）。為空 → 「圖片路徑是空的。」（`:53-56`）。
  5. `Path.Combine(baseDirectory, 路徑)` 再 `GetFullPath`（`:60`），例外（`ArgumentException`、`NotSupportedException`）→ 「圖片路徑無效。」（`:63-65`）。
- `CreateFileResource`（`:69-79`）：不在文件目錄內 → 「圖片路徑必須位於文件目錄內。」；檔案存在 → 可用；不存在 → 「找不到圖片檔案。」。
- `IsWithinDirectory`（`:81-87`）：以目錄加上分隔符號做前綴比對，不分大小寫，允許等於目錄本身。子目錄允許，`..` 跳脫會被擋下。
- 目錄來源：`MarkdownPreviewService` 不知道文件，由呼叫端傳入；閱讀器傳入文件所在目錄，未命名文件用 `Environment.CurrentDirectory`（`PreviewWindow.xaml.cs:103-106`）。

資料模型（`MarkdownPreviewModels.cs`）：

- `MarkdownPreviewDocument(Html, SourceDirectory, Resources)`：`SourceDirectory` 目前沒有被渲染器使用。
- `PreviewResource(Source, ResolvedPath, IsAvailable, ErrorMessage)`：渲染器只用 `Source`、`ResolvedPath`、`IsAvailable`，`ErrorMessage` 未被使用（見第 7 章）。

### 10.2 Markdown 格式化服務（編輯器用，閱讀器不用）

來源：`src/Glystrata.Core/Markdown/MarkdownFormattingService.cs`（291 行，全部讀過）。它是純字串轉換，輸入文字與選取範圍，回傳新文字與新選取範圍（`MarkdownEditResult`，`:23`）。

| 命令 | 行為 | 空選取時 | 來源 |
|---|---|---|---|
| Bold | 以 `**` 包住選取 | 插入 `**bold**`，選取 `bold` | `:40`、`:60-76` |
| Italic | 以 `*` 包住選取 | 插入 `*italic*` | 同上 |
| Strikethrough | 以 `~~` 包住選取 | 插入 `~~strikethrough~~` | 同上 |
| InlineCode | 以反引號包住選取 | 插入 `` `code` `` | 同上 |
| Link | 產生 `[選取或佔位](url)`；url 預設 `https://` | 佔位文字 `link text` | `:44`、`:78-84` |
| Paragraph、Heading1–6 | 對選取所在的整行（或多行）去除既有 `#` 前綴後加上指定層級 | 空行時插入 `# heading`（或 `text`） | `:45-51`、`:86-118` |
| UnorderedList | 所有行都有 `- `、`* `、`+ ` 前綴時移除，否則每行加 `- ` | — | `:52`、`:120-148`、`:232-245` |
| OrderedList | 同上，前綴為 `1. `，判斷規則為「數字加 `.` 或 `)` 加空白」 | — | `:53`、`:247-277` |
| Quote | 同上，前綴為 `> ` | — | `:54`、`:279-290` |
| CodeBlock | 若選取的整行首尾都是 ``` 則去除，否則以 ``` 包住 | — | `:55`、`:150-167` |

- 寫死的英文佔位字：`bold`、`italic`、`strikethrough`、`code`、`link text`、`text`、`heading`（`:70`、`:81`、`:104`）。**不走 localization**，且會直接寫入文件內容。
- 選取範圍一律擴展到整行（`GetLineRange`，`:169-184`）。若選取以換行結尾，最後的換行不計入。
- 有序清單一律以 `1.` 作為前綴；自動遞增是編輯器 Enter 行為的一部分，不在本檔（說明文字的主張，見第 5 章）。

### 10.3 快照（Snapshots）

來源：`src/Glystrata.Core/Snapshots/SnapshotInfo.cs`、`SnapshotService.cs`、`SnapshotSidecarStore.cs`、`TextDiffService.cs`。

#### 資料模型（`SnapshotInfo.cs`）

- `SnapshotInfo`（record）（`:11-18`）：
  - `Id`：Guid。
  - `CreatedUtc`：UTC 時間。
  - `Text`：**整份文件內容**（不是差異，每筆都完整保存）。
  - `Encoding`：`FileEncodingKind`。
  - `LineEnding`：`LineEndingKind`。
  - `SourceFingerprint`：`FileFingerprint?`（建立時的來源檔指紋）。
  - `Note`：備註字串，預設空字串。
- `RestoreMode`：`ReplaceCurrent`、`SaveAsNewFile`（`:5-9`）。

#### 快照檔（sidecar）格式

- 位置：與文件同目錄，檔名為 `{原檔名}.gss`（`SnapshotSidecarStore.cs:36-41`）。
- 內容為 JSON：縮排、camelCase、以 `UnsafeRelaxedJsonEscaping` 編碼（`:8-17`、`:195`）。
- 結構（`SnapshotSidecar`，`SnapshotInfo.cs:20-35`）：
  - `schemaVersion`：預設 1。
  - `sourcePath`：文件完整路徑。
  - `notice`：固定的雙語說明字串（`:26-32`），排在最前面（`JsonPropertyOrder(-1)`）。
  - `snapshots`：陣列，**依 `CreatedUtc` 由新到舊排序**（`:156-158`、`:190-192`）。
- 每筆 `SnapshotEntry`（`:37-59`）：欄位與 `SnapshotInfo` 相同。
- 讀取時依 `CreatedUtc` 降冪排序（`ReadAsync`，`:106-113`）。
- 清空所有快照時直接刪除檔案，不留空檔（`MutateAsync`，`:165-168`）。
- 隱藏屬性：寫入與讀取時會依 `HideSidecarFiles` 設定檔案的 Windows `Hidden` 屬性（`ApplyVisibility`，`:200-222`）。

#### 舊檔名遷移（`SnapshotSidecarStore.cs:43-91`）

依序檢查的舊名稱（都在同一目錄）：

1. `{原檔名}.glystrata-snapshots.json`（`GetPreviousSidecarPath`，`:45-50`）。
2. `.{原檔名}.glystrata-snapshots.json`（點開頭的隱藏式命名，`:56-61`）。
3. `.{原檔名}.mdeditor-snapshots.json`（舊專案名稱 MDeditor，`:67-72`）。

- 讀取時若新檔不存在，就搬移第一個找到的舊檔（`MigrateLegacySidecar`，`:81-91`，`ReadSidecarAsync` `:276-282`）。
- 搬移失敗（例如被鎖定）時，直接從舊檔讀取（`:284-288`）。
- `MigrateIfNeeded`：提供給啟動時掃描用（`:97-104`）。
- 文件改名時 `RenameSidecar` 會搬移所有命名版本（`:225-231`）。

#### 並行與寫入

- 每個 sidecar 路徑一把 `SemaphoreSlim(1,1)`，存在 `ConcurrentDictionary` 中（`:22-25`）。
- `MutateAsync`：在同一把鎖內完成「讀取 → 修改 → 寫入」，避免兩個更新互相覆蓋（`:145-180`）。
- 寫入使用 `AtomicFileWriter`（`:10`、`:196`，實作未在本次讀取範圍內）。

#### 服務層（`SnapshotService.cs`）

- 介面 `ISnapshotService`（`:5-14`）：`ListAsync`、`CreateAsync`、`DeleteAsync`、`UpdateNoteAsync`、`DeleteAllAsync`、`GetAsync`，以及 `HideSidecarFiles` 屬性。
- `CreateAsync(document, maxSnapshots)`（`:34-88`）：
  1. 沒有檔案路徑 → 回傳 null（`:39-42`）。
  2. `maxSnapshots` 夾在 1–200（`:44`）。
  3. 在鎖內判斷，回傳 null（不建立）的情況：
     - 沒有任何快照，且文件內容為空（`:54-57`）。
     - 最新快照的 `Text` 與文件完全相同（`:58-61`）。
     - 注意：比較的是「最新快照」，不是磁碟上的檔案。
  4. 建立新快照，插入最前面，超過上限時刪除尾端（`:63-74`）。
  5. 成功後更新文件的 `LastSnapshotUtc` 與 `LastSnapshotText`（`:85-86`）。
- `DeleteAsync`、`UpdateNoteAsync`（找不到回傳 false）、`DeleteAllAsync`、`GetAsync`：`:90-119`。

#### 自動快照（主視窗的計時器，`MainWindow.xaml.cs:2151-2180`）

- 計時器每 **30 秒** 觸發一次（`:2151-2155`）。
- 對每份有檔案路徑的文件：
  - 距離「最後一次變更」不足 `SnapshotIntervalMinutes` → 略過。
  - 文字與 `LastSnapshotText` 相同 → 略過。
  - 其餘呼叫 `CreateAsync(document, MaxSnapshotsPerFile)`（`:2169-2180`）。
- 因此是「停止編輯達設定分鐘數後」才建立快照，不是固定間隔。
- 手動建立：`Ctrl+Alt+S`（`AppCommand.cs:58`），`CreateSnapshotNow`（`MainWindow.xaml.cs:1811`）。

### 10.4 差異演算法（TextDiffService）

來源：`src/Glystrata.Core/Snapshots/TextDiffService.cs`（70 行，全部讀過）。

- 模型：`DiffLine(Kind, Text, LeftLine, RightLine)`（`:12`）；`Kind` 為 `Unchanged`、`Added`、`Removed`（`:5-10`）。
- 行切分（`SplitLines`，`:68-69`）：先經 `FileCodec.NormalizeLineEndings` 正規化換行（實作未在本次讀取範圍內），再以 `\n` 切分。
- 演算法（`Compare`，`:16-66`）：
  - 最長共同子序列（LCS）的動態規劃表，大小為 `(左行數+1) × (右行數+1)` 的 `int` 陣列（`:20`）。
  - **記憶體與時間為 O(左行數 × 右行數)**，長文件（數千行以上）的成本會明顯變大（由程式碼推得）。
  - 回溯時：相同則輸出未變更；不同時若 `table[i+1,j] >= table[i,j+1]` 輸出刪除，否則輸出新增（`:45`）。
  - 尾端剩餘行依序輸出刪除或新增（`:55-63`）。
- 行號：左行號只在刪除與未變更時遞增，右行號只在新增與未變更時遞增（`:35-36`、`:41`、`:47`、`:51`）。

---

---

## 11. 假設與不確定之處

以下是推斷而非原始碼明寫的事項。每項都標註依據，方便抽查。

### 11.1 推斷事項

1. **YAML front matter 不顯示**：依據是測試斷言（`tests/Glystrata.Verification/Program.cs:352`），並未實際執行渲染。
2. **`DisableHtml` 後的 HTML 會以純文字顯示**：依據是測試斷言 HTML 中不含 `<script`（`Program.cs:351`），推論為被跳脫後以文字呈現，未實際執行。
3. **`UseEmphasisExtras()` 的實際啟用範圍**：Markdig 1.3.2 的預設行為未在本機驗證。推測僅啟用刪除線（`~~`），上標、下標、`==mark==`、`++ins++` 是否啟用不確定。若未啟用，這些語法會被當成一般文字。
4. **Setext 標題關閉後，`---` 緊接文字的行為**：推測變成分隔線（依 CommonMark 規則，未實際驗證）。
5. **引言中的多段落會合併成一段**：依 `WpfMarkdownRenderer.cs:121-124` 的程式碼推得，未實際渲染確認。
6. **DiffWindow 的 FlowDocument 會自動換行**：依 WPF 預設推定（`HorizontalScrollBarVisibility=Disabled` 且使用 FlowDocument），未實際驗證。
7. **動態資源（DynamicResource、SetResourceReference）在主題資源切換時會即時更新**：依 WPF 語意推定。全域主題切換的實作（`ThemeService`、`App.xaml.cs`）未在本次讀取範圍內。
8. **快照列表的備註換行處理可能造成意外改寫**：`FormatNotePreview` 會把換行換成空白（`SnapshotHistoryWindow.xaml.cs:144-145`），但比對是否變更使用的是原始備註（`:240`、`:263-264`）。因此含換行的備註在關窗時可能被視為變更並改寫為單行。這是依程式碼推得，未實際執行。
9. **自動快照的時機**：依 `MainWindow.xaml.cs:2151-2180` 的條件推得：每 30 秒檢查一次，距離上次變更達設定分鐘數才建立。
10. **MessageDialogs 只有一顆按鈕時，Esc 等同於按確定**：依 `MessageDialogs.cs:69` 的 `IsCancel` 條件推得（唯一按鈕同時是最後一顆）。
11. **ColorPickerDialog 的十六進位輸入框在每次更新後會被改寫為大寫 `#RRGGBB`**：依 `ColorPickerDialog.cs:115`，對游標位置的影響未驗證。
12. **TextDiffService 的行切分先經過換行正規化**：依 `FileCodec.NormalizeLineEndings` 的名稱推定，實作未讀取。
13. **DiffWindow 外部比較模式的 `-`／`+` 語意**：依 `MainWindow.xaml.cs:2418` 與 `DiffWindow.xaml.cs:23` 的參數順序推得（`-` 為磁碟檔，`+` 為編輯器內容）。
14. **WPF `BrushConverter` 接受的顏色格式**：推定包含 `#RGB`、命名色；`#AARRGGBB` 的 Alpha 被丟棄（由 `SettingsWindow.xaml.cs:709` 只輸出 RGB 推得）。
15. **快照字數以 UTF-16 長度計算**：`snapshot.Text.Length`（`SnapshotHistoryWindow.xaml.cs:166`、`DiffWindow.xaml.cs:48`），與字元數不同（例如 emoji 會計為 2）。
16. **閱讀器重新渲染後的捲動位置**：原始碼未指定。推定會回到頂端，未驗證。
17. **閱讀器在首次開啟時的視窗背景來源**：建構時未設定視窗背景（`PreviewWindow.xaml.cs:20-61`），`ApplyTheme()` 只在設定套用時呼叫。推定由全域樣式提供，未在本次範圍內確認。
18. **Hyperlink 是否有底線**：渲染器未設定 `TextDecorations`，取決於 WPF 預設樣式（未在本次範圍內確認）。
19. **Emoji 圖檔的 zip 內檔名規則**：依 `EmojiRenderer.cs:115` 的 `{key}.png` 推定，未逐一列舉 zip 內容確認涵蓋範圍。
20. **`PreviewWindowManager` 的一視圖一閱讀器**：依 `PreviewWindowManager.cs:23-46` 的字典鍵（`view.ViewId`）推得。

### 11.2 原始碼未涵蓋、但功能相依的部分

- `CustomTitleBar.cs` 的外觀與互動細節（只確認高度 32，`CustomTitleBar.cs:64`）。
- `LocalizationService.cs` 的字串內容（只確認 key 存在，中英文各一組字典）。
- `ThemeService.cs`、`SystemThemeWatcher.cs`（作業系統主題變更是否會觸發閱讀器更新，未確認）。
- `AtomicFileWriter.cs`、`FileCodec`（寫入方式與換行正規化）。
- `MainWindow.RestoreSnapshot`（還原快照的實際行為）。
- `Editing/`（自動轉換符號與列表 Enter 自動接續，只見到說明文字的主張）。
- 窗格上的按鈕（閱讀器與快照的入口按鈕）所在的 UI 檔案。

### 11.3 原始碼中觀察到的不一致或風險（事實描述，供抽查）

1. **尋找取代的過期位移**：`RecomputeMatches` 註解要求每次導覽或取代前重算（`FindReplaceWindow.xaml.cs:103`），但 `ReplaceCurrent` 直接使用快取的 `_matches`（`:189-192`）。使用者在編輯器打字後未經導覽就按取代，理論上會使用過期位移。
2. **比較視窗不自動更新**：沒有訂閱文字變更事件（`DiffWindow.xaml.cs` 全檔）。
3. **比較演算法的記憶體成本**：LCS 表大小為 `(n+1)×(m+1)` 個 `int`（`TextDiffService.cs:20`）。長文件時成本明顯。
4. **快照列表在語言切換時重建**：`RefreshAsync` 會清空並重建列表，選取回到第 0 筆；若備註編輯中尚未失去焦點，其內容的行為未處理（`SnapshotHistoryWindow.xaml.cs:371-375`）。
5. **Esc 行為不一致**：
   - 設定、快照、比較、說明、對話框：Esc 關閉（`DialogKeys.AttachEscapeToClose`）。
   - 尋找取代：只在兩個輸入框內有效。
   - 閱讀器：完全沒有 Esc 處理。
6. **語言與主題即時性不一致**：
   - 即時：快照歷史（語言）、閱讀器（語言與主題）。
   - 不即時：設定（套用後才）、比較（語言）、尋找（語言）、說明（語言與主題）。
7. **設定中的數值範圍**：快照間隔與上限超出範圍時靜默夾住，不提示（`SettingsWindow.xaml.cs:606`、`AppSettings.cs:169-170`）。閱讀器排版數值沒有任何上下限。
8. **群組顏色沒有數量上限**，且允許重複（`SettingsWindow.xaml.cs:308-317`）。
9. **說明視窗內容含產品名稱**：「目前不支援」段落提到 Heptabase（`MarkdownHelpWindow.xaml.cs` 的中文常數）。
10. **格式化服務的英文佔位字直接寫入文件**：`bold`、`italic`、`link text` 等（`MarkdownFormattingService.cs:70-82、104`）。

---

## 需要人決定的事項（原文照列於回報）

以下事項原始碼無法決定，需要 Cloud 確認方向後才能定案。

1. macOS 快速鍵：原始碼以 `Ctrl` 定義（`AppCommand.cs:57-73`），Mac 版是否改為 `Cmd`，或沿用 `Ctrl`。
2. 快照檔的「隱藏」在 macOS 的做法：Windows 使用隱藏屬性（`SnapshotSidecarStore.cs:200-222`），macOS 沒有完全對等的做法，需決定採用哪種替代，或不做。
3. `.gss` 快照檔與設定檔的相容性：Mac 版是否需要讀寫 Windows 產生的檔案，包括舊檔名的遷移（`.glystrata-snapshots.json`、`.{檔名}.mdeditor-snapshots.json` 等）。
4. 字型替代：`Segoe UI`、`Segoe UI Emoji`、`Cascadia Mono`、`Consolas` 在 macOS 的對應字型。
5. 尋找取代的過期位移（第 11.3 節第 1 項）：沿用原行為、修正，或改寫，需決定。
6. 設定視窗的 Enter 行為（Windows 版無預設按鈕，Enter 不作用）。
7. 閱讀器的 Esc 是否關閉（Windows 版不關閉）。
8. 語言與主題的即時套用範圍（第 11.3 節第 6 項中不即時的視窗是否要改為即時）。
9. 說明文字中提到 Heptabase 的句子是否保留。
10. 群組顏色的數量上限（Windows 版無上限）。
11. 快照間隔與上限超出範圍時，要靜默夾住（Windows 版行為）或提示。
12. 快照字數以 UTF-16 長度計算（Windows 版行為），Mac 版是否改用字元數（會與 Windows 版顯示不同）。
13. 快照備註的換行處理（第 11.1 節第 8 項）：沿用、修正，需決定。
14. 比較視窗的單欄 unified 格式與長文件的記憶體成本（第 11.3 節第 3 項）：是否接受現況。
15. 外部檔案比較是否維持「每次開新視窗」的行為（`MainWindow.xaml.cs:2418`）。
16. Emoji 圖檔（Twemoji 15.1.0，見 `THIRD-PARTY-NOTICES.txt:103`）是否沿用同一份資源與授權聲明。
17. 閱讀器中 Setext 標題與 `---` 的行為是否維持 Windows 版的設定（`MarkdownPipelineFactory.cs:23-26`）。
