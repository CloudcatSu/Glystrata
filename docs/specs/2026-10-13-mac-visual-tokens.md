# Glystrata Mac 版視覺規格：主題色票與控制項樣式

- 整理日期：2026/10/13 06:51（Asia/Taipei）
- 來源 repo：`/Users/cloudsu/ClaudeCode/Glystrata_Mac`（Windows WPF 原始碼，唯讀整理）
- 行號格式：`Light.xaml:12` 表示 `src/Glystrata/Resources/Themes/Light.xaml` 第 12 行；未加路徑的檔名皆位於 `src/Glystrata/Resources/Themes/`。
- 「字面值」指 XAML 裡直接寫死的色碼（不是 `DynamicResource` key）。

---

## 1. 色票對照表

**Key 一致性檢查：** Light.xaml 與 Dark.xaml 的 resource key 完全一致（名稱、數量、順序都相同），沒有只存在於其中一邊的 key。

用途欄的「XAML」指 Style／Template 內用 `DynamicResource` 或 `StaticResource` 的引用行號；「C#」指程式碼內 `FindResource`、`TryFindResource`、`SetResourceReference` 的檔案與行號（不含 `Themes/` 資料夾）。

### 1.1 SolidColorBrush 色票（Light.xaml:3–:38）

| Key | 亮色 | 暗色 | 用途（從哪些地方引用推斷） |
|---|---|---|---|
| WindowBackgroundBrush | `#F7F8FA`（Light.xaml:3） | `#17191D`（Dark.xaml:3） | 視窗底色。XAML：DiffWindow.xaml:10、FindReplaceWindow.xaml:9、MarkdownHelpWindow.xaml:8、SnapshotHistoryWindow.xaml:9。C#：MainWindow.xaml.cs:426、InputDialogs.cs:22、:84、MessageDialogs.cs:29、ColorPickerDialog.cs:28、DiffWindow.xaml.cs:109–110、MarkdownHelpWindow.xaml.cs:24、SettingsWindow.xaml.cs:666、CustomTitleBar.cs:244（標題列背後的合成背景） |
| SurfaceBrush | `#FFFFFF`（Light.xaml:4） | `#202329`（Dark.xaml:4） | 輸入框、下拉、清單的底色。XAML：TextBox :137、ComboBox :144、ListBox :311。C#：MainWindow.xaml.cs:519、:546（狀態列底色）、EditorPaneControl.cs:47（編輯窗格底色）、SettingsWindow.xaml.cs:660（非強調按鈕底色） |
| SidebarBrush | `#F0F2F5`（Light.xaml:5） | `#1D2025`（Dark.xaml:5） | 側邊欄底色。C#：MainWindow.xaml.cs:699；GroupColors.cs:40（選取群組時的混色底色）。XAML 內未引用 |
| BorderBrush | `#D9DDE3`（Light.xaml:6） | `#343942`（Dark.xaml:6） | 通用邊框色。XAML：Button :59、TextBox :138、ComboBox :145、ListBox :312、TabControl :588、StatusBar 滑桿減量端 :846。C#：MainWindow.xaml.cs:419、:700、:1218；EditorPaneControl.cs:43；ColorPickerDialog.cs:55、:75 |
| PrimaryTextBrush | `#202124`（Light.xaml:7） | `#F1F3F5`（Dark.xaml:7） | 主要文字色。XAML：Button :57、TextBox :136、ComboBox :143、ComboBoxItem 選取 :265、ListBoxItem :277、:300、ListBox :314、TabControl :587、TreeViewItem :631、:705。C#：MainWindow.xaml.cs:661、:682、:693、:788、:863、:907；SettingsWindow.xaml.cs 多處 |
| SecondaryTextBrush | `#68707C`（Light.xaml:8） | `#A6ADB8`（Dark.xaml:8） | 次要文字、停用文字、提示。XAML：ComboBox 箭頭 :181、ListBoxItem 停用 :306。C#：MainWindow.xaml.cs:547（狀態列文字）、:807；TabHeaderControl.cs:78、:108；EditorPaneControl.cs:61；SettingsWindow.xaml.cs:207、:244、:300、:465 等 |
| AccentBrush | `#3B6FF5`（Light.xaml:9） | `#7C9CFF`（Dark.xaml:9） | 強調色。XAML：Button 鍵盤焦點 :94、ComboBox 滑鼠移入與焦點 :220、:223、狀態列滑桿拇指 :810、減量端軌道 :833。C#：DropIndicator.cs:125、TabHeaderControl.cs:77（作用中頁籤的標題）、SettingsWindow.xaml.cs:660 |
| EditorBackgroundBrush | `#FFFFFF`（Light.xaml:10） | `#202329`（Dark.xaml:10） | 編輯器底色。XAML 內未引用。C#：MainWindow.xaml.cs:1173、:2137（傳給 `SetEditorColors`） |
| EditorForegroundBrush | `#24292F`（Light.xaml:11） | `#E6EAF0`（Dark.xaml:11） | 編輯器文字色。XAML 內未引用。C#：MainWindow.xaml.cs:1174、:2138（傳給 `SetEditorColors`） |
| PreviewBackgroundBrush | `#FFFFFF`（Light.xaml:12） | `#202329`（Dark.xaml:12） | 閱讀器（預覽）底色。C#：PreviewWindow.xaml.cs:75；WpfMarkdownRenderer.cs:40 |
| MenuBackgroundBrush | `#FFFFFF`（Light.xaml:13） | `#202329`（Dark.xaml:13） | 選單、下拉、右鍵選單底色。XAML：ComboBox 下拉外框 :205、ComboBoxItem :236、Menu :333、ContextMenu :367、MenuItem :394、子選單外框 :461。C#：MainWindow.xaml.cs:441、:490 |
| MenuForegroundBrush | `#202124`（Light.xaml:14） | `#F1F3F5`（Dark.xaml:14） | 選單文字、標題列按鈕圖示色。XAML：TitleBarButtonStyle :100、ComboBoxItem :235、:261、Menu :332、ContextMenu :366、MenuItem :393、:504、TreeViewItem 展開箭頭 :675。C#：MainWindow.xaml.cs:442；CustomTitleBar.cs:118（標題文字，`SetResourceReference`） |
| MenuHoverBrush | `#E8EEFF`（Light.xaml:15） | `#303744`（Dark.xaml:15） | 選單、清單、樹狀列滑鼠移入或高亮的底色。XAML：ComboBoxItem 高亮 :260、ListBoxItem 移入 :303、MenuItem 高亮 :503、子選單開啟中 :507、TreeViewItem 移入 :712 |
| MenuDisabledForegroundBrush | `#9AA1AC`（Light.xaml:16） | `#69717D`（Dark.xaml:16） | 停用文字。XAML：ComboBoxItem 停用 :268、MenuItem 停用 :510、TreeViewItem 停用 :715 |
| MenuBorderBrush | `#D9DDE3`（Light.xaml:17） | `#343942`（Dark.xaml:17） | 選單邊框與分隔線色。XAML：ComboBox 下拉外框 :206、Menu :334、ContextMenu :368、MenuItem :395、子選單外框 :462、Separator :518 |
| TitleBarBackgroundBrush | `#FFFFFF`（Light.xaml:18） | `#202329`（Dark.xaml:18） | 自訂標題列底色。C#：CustomTitleBar.cs:96（`SetResourceReference`）。XAML 內未引用 |
| TitleBarButtonHoverBrush | `#E3E5E8`（Light.xaml:19） | `#2B3038`（Dark.xaml:19） | 標題列按鈕滑鼠移入。XAML：TitleBarButtonStyle :115 |
| TitleBarButtonPressedBrush | `#D2D5DA`（Light.xaml:20） | `#363C46`（Dark.xaml:20） | 標題列按鈕按下。XAML：TitleBarButtonStyle :118 |
| TabBackgroundBrush | `#F7F8FA`（Light.xaml:21） | `#1D2025`（Dark.xaml:21） | 頁籤列底色、未選取頁籤底色。XAML：TabItem :540、TabControl :586 |
| TabActiveBrush | `#E9EEF9`（Light.xaml:22） | `#28303B`（Dark.xaml:22） | 作用中（選取）頁籤底色。XAML：TabItem :569、ComboBoxItem 選取 :264、ListBoxItem 選取 :299。C#：MainWindow.xaml.cs:909、:928 |
| TabActiveHoverBrush | `#E1E8F8`（Light.xaml:23） | `#303A49`（Dark.xaml:23） | 作用中頁籤滑鼠移入。XAML：TabItem 複合觸發 :581 |
| TabHoverBrush | `#F0F3F8`（Light.xaml:24） | `#242B34`（Dark.xaml:24） | 未作用頁籤滑鼠移入。XAML：TabItem :574 |
| TabActiveBorderBrush | `#B8C9F4`（Light.xaml:25） | `#4D5E79`（Dark.xaml:25） | 作用中頁籤底線色。XAML：TabItem :571 |
| TabForegroundBrush | `#68707C`（Light.xaml:26） | `#A6ADB8`（Dark.xaml:26） | 未作用頁籤文字色。XAML：TabItem :539 |
| GroupIndicatorBrush | `#68707C`（Light.xaml:27） | `#F1F3F5`（Dark.xaml:27） | 原檔未指定用途。XAML 與 C# 內均未找到引用 |
| DiffAddedBackgroundBrush | `#E8F5EC`（Light.xaml:28） | `#20382B`（Dark.xaml:28） | 差異檢視「新增行」底色。C#：DiffWindow.xaml.cs:137 |
| DiffAddedForegroundBrush | `#1F6B3B`（Light.xaml:29） | `#9AD9AD`（Dark.xaml:29） | 差異檢視「新增行」文字。C#：DiffWindow.xaml.cs:131 |
| DiffRemovedBackgroundBrush | `#FBEAEC`（Light.xaml:30） | `#432C31`（Dark.xaml:30） | 差異檢視「刪除行」底色。C#：DiffWindow.xaml.cs:138 |
| DiffRemovedForegroundBrush | `#8A3440`（Light.xaml:31） | `#F2A8B0`（Dark.xaml:31） | 差異檢視「刪除行」文字。C#：DiffWindow.xaml.cs:132。XAML：SnapshotHistoryWindow.xaml:24（「全部刪除」按鈕文字） |
| DiffUnchangedBackgroundBrush | `Transparent`（Light.xaml:32） | `Transparent`（Dark.xaml:32） | 差異檢視「未變更行」底色。C#：DiffWindow.xaml.cs:139 |
| DiffUnchangedForegroundBrush | `#4E5968`（Light.xaml:33） | `#B5BDC8`（Dark.xaml:33） | 差異檢視「未變更行」文字。C#：DiffWindow.xaml.cs:133 |
| SystemColors.MenuBrushKey | `#FFFFFF`（Light.xaml:34） | `#202329`（Dark.xaml:34） | WPF 系統色覆寫。XAML 與 C# 內未直接引用；用途見第 7 節 |
| SystemColors.MenuTextBrushKey | `#202124`（Light.xaml:35） | `#F1F3F5`（Dark.xaml:35） | 同上 |
| SystemColors.HighlightBrushKey | `#E8EEFF`（Light.xaml:36） | `#303744`（Dark.xaml:36） | 同上 |
| SystemColors.HighlightTextBrushKey | `#202124`（Light.xaml:37） | `#F1F3F5`（Dark.xaml:37） | 同上 |
| SystemColors.GrayTextBrushKey | `#9AA1AC`（Light.xaml:38） | `#69717D`（Dark.xaml:38） | 同上 |

### 1.2 捲軸色票（Light.xaml:722–:725）

這四個 key 定義在 :722–:725，位置在 TreeViewItem 之後、其他 key 之外。

| Key | 亮色 | 暗色 | 用途 |
|---|---|---|---|
| ScrollBarTrackBrush | `#EEF0F3`（Light.xaml:722） | `#1B1E23`（Dark.xaml:722） | 捲軸軌道底色。XAML：Light.xaml:759、:774、:789 |
| ScrollBarThumbBrush | `#8C959F`（Light.xaml:723） | `#6E7681`（Dark.xaml:723） | 捲軸拇指預設色。XAML：:734 |
| ScrollBarThumbHoverBrush | `#6B7480`（Light.xaml:724） | `#8B949E`（Dark.xaml:724） | 捲軸拇指滑鼠移入。XAML：:737 |
| ScrollBarThumbPressedBrush | `#545B66`（Light.xaml:725） | `#ACB4BF`（Dark.xaml:725） | 捲軸拇指拖曳中。XAML：:740 |

### 1.3 樣式內直接寫死的字面色碼（不是 key）

這些值不經過色票，移植時要獨立處理。

| 位置 | 亮色 | 暗色 | 用途 |
|---|---|---|---|
| Button 按下背景（Light.xaml:80，Dark.xaml:80） | `#D3DFFC` | `#37426B` | ButtonBorder 的 IsPressed |
| Button 滑鼠移入背景（Light.xaml:91，Dark.xaml:91） | `#E8EEFF` | `#2B3550` | Button 的 IsMouseOver |
| 關閉鈕滑鼠移入（Light.xaml:125 / Dark.xaml:125） | `#C42B1C` | 同左 | TitleBarCloseButtonStyle |
| 關閉鈕按下（Light.xaml:129 / Dark.xaml:129） | `#9D2216` | 同左 | TitleBarCloseButtonStyle |
| 關閉鈕前景（:126、:130） | `White` | 同左 | TitleBarCloseButtonStyle |

---

## 2. Light.xaml 與 Dark.xaml 的差異摘要

**結論：除顏色值以外，兩份檔案的 Style、ControlTemplate、Trigger、字型、尺寸、圓角、動畫完全相同。** 以 `diff` 逐行比對，兩檔各有 41 行不同，全部是顏色值。兩邊的 resource key 名稱與順序完全一致，沒有只存在於其中一邊的 key。

差異清單（行號在兩檔相同）：

| 項目 | Light 行號 | Light 值 | Dark 值 | 類型 |
|---|---|---|---|---|
| 色票定義（SolidColorBrush） | :3–:31、:33–:38 | 見第 1 節 | 見第 1 節 | Color 屬性 |
| 色票定義：`DiffUnchangedBackgroundBrush` | :32 | `Transparent` | `Transparent` | 相同，不算差異 |
| Button 按下背景（字面值） | :80 | `#D3DFFC` | `#37426B` | Setter Value |
| Button 滑鼠移入背景（字面值） | :91 | `#E8EEFF` | `#2B3550` | Setter Value |
| 捲軸色票 | :722–:725 | 見第 1 節 | 見第 1 節 | Color 屬性 |

補充：
- `SystemColors.*BrushKey` 的覆寫（:34–:38）也只有顏色不同，Key 本身相同。
- 其他所有 Style 的 Setter、Trigger、Template、`Padding`、`CornerRadius`、`FontFamily` 都是逐字相同。

---

## 3. 控制項樣式規格

說明：
- 色票欄位寫的是 key 名稱，實際色值見第 1 節。
- 數值為亮色檔原文。暗色檔的數值差異只有第 2 節列出的 3 處字面值與 4 個捲軸色，其餘照原樣沿用。
- 原檔沒寫的項目一律寫「原檔未指定」。
- 全檔沒有任何 `Storyboard`、`Duration`、`Timeline`、`EasingFunction`。唯一的動畫是 `Popup` 的 `PopupAnimation="Fade"`（Light.xaml:202、:460），時間長度原檔未指定。

### 3.1 Button（隱式 Style，`TargetType="{x:Type Button}"`，:55–:97）

- 字型：`Segoe UI Variable`（:56）；字級、字重：原檔未指定
- 前景：`PrimaryTextBrush`（:57）；背景：`Transparent`（:58）；邊框色：`BorderBrush`（:59）；邊框粗細：`1`（:60）
- Padding：`8,4`（:61）；HorizontalContentAlignment：`Center`（:62）；VerticalContentAlignment：`Center`（:63）
- 寬度、高度：原檔未指定
- 圓角：`CornerRadius="4"`（:71，套在 `ButtonBorder`）；`SnapsToDevicePixels="True"`（:72）
- 內容 Margin 等於 Padding（:73）

| 狀態 | 來源行號 | 背景 | 邊框 | 其他 |
|---|---|---|---|---|
| 一般 | :58、:59 | `Transparent` | `BorderBrush` | — |
| 滑鼠移入（IsMouseOver，Style.Triggers） | :90–:92 | 字面值 `#E8EEFF`（暗 `#2B3550`） | 不變 | — |
| 按下（IsPressed，ControlTemplate.Triggers，套在 `ButtonBorder`） | :79–:81 | 字面值 `#D3DFFC`（暗 `#37426B`） | 不變 | — |
| 鍵盤焦點（IsKeyboardFocused，Style.Triggers） | :93–:95 | 不變 | `AccentBrush` | — |
| 停用（IsEnabled=False，ControlTemplate.Triggers） | :82–:84 | 不變 | 不變 | `Opacity 0.45`（整個控制項） |

### 3.2 TitleBarButtonStyle（x:Key，`TargetType="{x:Type Button}"`，:98–:121）

- 背景：`Transparent`（:99）；前景：`MenuForegroundBrush`（:100）；邊框粗細：`0`（:101）
- HorizontalContentAlignment／VerticalContentAlignment：`Center`（:102–:103）
- 字型、Padding、寬度、高度、圓角：原檔未指定（未設 `FontFamily`，會繼承）
- 模板：`Border` 背景綁定 `TemplateBinding Background`；`ContentPresenter` 置中（:106–:110）
- 滑鼠移入（:114–:116）：背景 `TitleBarButtonHoverBrush`
- 按下（:117–:119）：背景 `TitleBarButtonPressedBrush`

### 3.3 TitleBarCloseButtonStyle（x:Key，BasedOn `TitleBarButtonStyle`，:122–:133）

- 滑鼠移入（:124–:127）：背景 `#C42B1C`、前景 `White`
- 按下（:128–:131）：背景 `#9D2216`、前景 `White`
- 這兩個色是字面值，兩個主題相同（Dark.xaml:125–:130 同）

### 3.4 TextBox（隱式，:134–:140）

- 字型：`Segoe UI Variable`（:135）；前景：`PrimaryTextBrush`（:136）；背景：`SurfaceBrush`（:137）；邊框色：`BorderBrush`（:138）；Padding：`7,4`（:139）
- 未覆寫 Template、BorderThickness、CornerRadius，也未寫任何狀態樣式（滑鼠移入、焦點、停用）。原檔未指定，沿用 WPF 預設 TextBox 外觀。

### 3.5 ComboBox（隱式，:141–:232，`OverridesDefaultStyle=True`）

- 字型：`Segoe UI Variable`（:142）；前景：`PrimaryTextBrush`（:143）；背景：`SurfaceBrush`（:144）；邊框色：`BorderBrush`（:145）；邊框粗細：`1`（:146）
- Padding：`7,3`（:147）；HorizontalContentAlignment：`Left`（:148）；VerticalContentAlignment：`Center`（:149）；FocusVisualStyle：`null`（:150）
- 外框 `ComboBorder`：`CornerRadius="2"`（:160）
- 欄位：文字欄 `*`，箭頭欄寬 `28`（:163–:164）
- 箭頭 `Path`：寬 `8`、高 `5`（:176–:177），填色 `SecondaryTextBrush`（:181），路徑 `M 0,0 L 8,0 L 4,5 Z`（:182）
- 下拉開關：`ToggleButton` `DropDownButton`，`ClickMode="Press"`，`Focusable="False"`，覆蓋整個控制項（:184–:193）
- 下拉 `Popup`（:196–:216）：`Placement="Bottom"`（:200）；`PopupAnimation="Fade"`（:202）；`StaysOpen="False"`（:203）；`VerticalOffset="2"`（:204）
- 下拉外框 `Border`：背景 `MenuBackgroundBrush`（:205）；邊框色 `MenuBorderBrush`（:206）；邊框粗細 `1`（:207）；`MinWidth` 綁定控制項 `ActualWidth`（:208）
- 下拉捲動：垂直 `Auto`、水平 `Disabled`（:211–:212）
- 狀態：
  - 滑鼠移入（:219–:221）：`ComboBorder` 邊框色 `AccentBrush`
  - 鍵盤焦點所在（IsKeyboardFocusWithin，:222–:224）：`ComboBorder` 邊框色 `AccentBrush`
  - 停用（:225–:227）：`ComboBorder` `Opacity 0.55`
- 下拉項目的顏色由 `ComboBoxItem` 決定（見 3.6）

### 3.6 ComboBoxItem（隱式，:233–:274）

- 字型：`Segoe UI Variable`（:234）；前景：`MenuForegroundBrush`（:235）；背景：`MenuBackgroundBrush`（:236）；邊框粗細：`0`（:237）
- HorizontalContentAlignment：`Left`（:238）；VerticalContentAlignment：`Center`（:239）；Padding：`8,6`（:240）；FocusVisualStyle：`null`（:241）；OverridesDefaultStyle：`True`（:242）
- 模板：`Border ItemBorder`，Padding 綁定（:246–:250）
- 狀態：
  - `IsHighlighted`（:259–:262）：`ItemBorder` 背景 `MenuHoverBrush`；前景 `MenuForegroundBrush`
  - `IsSelected`（:263–:266）：`ItemBorder` 背景 `TabActiveBrush`；前景 `PrimaryTextBrush`
  - 停用（:267–:269）：前景 `MenuDisabledForegroundBrush`
- 圓角、高度、寬度：原檔未指定

### 3.7 ListBoxItem（隱式，:275–:309）

- 字型：`Segoe UI Variable`（:276）；前景：`PrimaryTextBrush`（:277）；背景：`Transparent`（:278）
- Padding：`6,4`（:279）；HorizontalContentAlignment：`Left`（:280）；VerticalContentAlignment：`Center`（:281）
- 模板：`Border Bd`，`SnapsToDevicePixels="True"`（:285–:290）
- 狀態：
  - `IsSelected`（:298–:300）：背景 `TabActiveBrush`；前景 `PrimaryTextBrush`
  - 滑鼠移入（:302–:304）：背景 `MenuHoverBrush`
  - 停用（:305–:307）：前景 `SecondaryTextBrush`

### 3.8 ListBox（隱式，:310–:329）

- 背景：`SurfaceBrush`（:311）；邊框色：`BorderBrush`（:312）；邊框粗細：`1`（:313）；前景：`PrimaryTextBrush`（:314）
- 模板：外層 `Border`（背景、邊框綁定），內層 `ScrollViewer`（`Focusable="False"`，Padding 綁定）包 `ItemsPresenter`（:317–:324）
- 沒有狀態樣式；項目狀態見 3.7

### 3.9 Menu（隱式，:330–:363，`OverridesDefaultStyle=True`）

- 字型：`Segoe UI Variable`（:331）；前景：`MenuForegroundBrush`（:332）；背景：`MenuBackgroundBrush`（:333）；邊框色：`MenuBorderBrush`（:334）；邊框粗細：`0`（:335）；Padding：`0`（:336）
- 高度：`32`（:337）
- HorizontalContentAlignment：`Left`（:338）；VerticalContentAlignment：`Center`（:339）；FocusVisualStyle：`null`（:340）
- ItemsPanel：水平 `StackPanel`（:345）
- 沒有狀態樣式

### 3.10 ContextMenu（隱式，:364–:390，`OverridesDefaultStyle=True`）

- 字型：`Segoe UI Variable`（:365）；前景：`MenuForegroundBrush`（:366）；背景：`MenuBackgroundBrush`（:367）；邊框色：`MenuBorderBrush`（:368）；邊框粗細：`1`（:369）；Padding：`0`（:370）；FocusVisualStyle：`null`（:371）
- 模板：`Border`（:376–:380），內層 `ScrollViewer`：`CanContentScroll="True"`、水平 `Disabled`、垂直 `Auto`（:381–:383）
- 圓角：原檔未指定（沒有 `CornerRadius`，為直角）
- 沒有狀態樣式；項目狀態見 3.11

### 3.11 MenuItem（隱式，:391–:516，`OverridesDefaultStyle=True`）

- 字型：`Segoe UI Variable`（:392）；前景：`MenuForegroundBrush`（:393）；背景：`MenuBackgroundBrush`（:394）；邊框色：`MenuBorderBrush`（:395）；邊框粗細：`0`（:396）
- Padding：`9,6`（:397）；HorizontalContentAlignment：`Left`（:398）；VerticalContentAlignment：`Center`（:399）；FocusVisualStyle：`null`（:400）
- ItemsPanel：`StackPanel`（:405）
- 版面（模板 :411–:474）：
  - `Grid MinHeight="30"`（:417）
  - 欄寬：`Auto`、`*`、`Auto`、`ArrowColumn`（初始寬 `0`）（:419–:422）
  - 圖示 `IconPresenter`：Margin `8,0,2,0`（:424–:428）
  - 標題 ContentPresenter：Margin 綁定 Padding（:429–:437）
  - 快捷鍵文字 `GestureText`：Margin `20,0,8,0`（:438–:443）
  - 子選單箭頭 `SubmenuArrow`：文字 `›`、`FontSize="17"`（:444–:451），初始 `Collapsed`
- 子選單 `Popup`（:454–:473）：`Placement="Right"`（:458）；`PopupAnimation="Fade"`（:460）；外框背景 `MenuBackgroundBrush`（:461）、邊框色 `MenuBorderBrush`（:462）、邊框粗細 `1`（:463）、`MinWidth="220"`（:464）、Padding `0`（:465）
- 角色（Role）觸發：
  - `TopLevelHeader`（:476–:484）：Popup `Placement="Bottom"`（:477）；快捷鍵與圖示收合（:478–:479）；MinWidth `0`（:480）；Padding `7,0`（:481）；HorizontalAlignment `Left`；HorizontalContentAlignment `Center`（:483）
  - `TopLevelItem`（:485–:492）：快捷鍵與圖示收合（:486–:487）；MinWidth `0`（:488）；Padding `7,0`（:489）；HorizontalAlignment `Left`；HorizontalContentAlignment `Center`（:491）
  - `SubmenuHeader`（:493–:498）：`SubmenuArrow` 顯示（:494）；`ArrowColumn` 寬 `18`（:495）；快捷鍵收合（:496）；BorderThickness `0,0,0,1`（:497）
  - `SubmenuItem`（:499–:501）：BorderThickness `0,0,0,1`（:500）
- 狀態：
  - `IsHighlighted`（:502–:505）：`ItemBorder` 背景 `MenuHoverBrush`；前景 `MenuForegroundBrush`
  - `IsSubmenuOpen`（:506–:508）：`ItemBorder` 背景 `MenuHoverBrush`
  - 停用（:509–:511）：前景 `MenuDisabledForegroundBrush`
- 本 Style 沒有 `IsMouseOver` 觸發器（見第 7 節）
- 圓角：原檔未指定

### 3.12 Separator（隱式，:517–:530）

- 背景：`MenuBorderBrush`（:518）；Margin：`8,4`（:519）；高度：`1`（:520）
- 模板：`Border`，高度與背景綁定，`SnapsToDevicePixels="True"`（:523–:526）

### 3.13 Separator 在選單內的覆寫（x:Key=`MenuItem.SeparatorStyleKey`，:531–:536）

- BasedOn 3.12 的 Separator Style
- Margin 改為 `0,4`（:535）；其餘（背景 `MenuBorderBrush`、高度 `1`）沿用 3.12
- :531–:533 的註解說明：選單內的分隔線由 `MenuItem` 透過這個 key 重新套樣式，沒有這條就會退回 Windows 預設（縮排、粗細不同）

### 3.14 TabItem（隱式，:537–:584，`OverridesDefaultStyle=True`）

- 字型：`Segoe UI Variable`（:538）；前景：`TabForegroundBrush`（:539）；背景：`TabBackgroundBrush`（:540）；邊框色：`Transparent`（:541）；邊框粗細：`0,0,0,2`（:542）
- Padding：`10,5`（:543）；FocusVisualStyle：`null`（:544）
- 模板：`Border TabBorder`，圓角 `CornerRadius="2,2,0,0"`（:553），Padding 綁定（:554）；內容 ContentPresenter 來源為 Header（:556–:562）
- 狀態：
  - 未選取、未移入（預設）：背景 `TabBackgroundBrush`；前景 `TabForegroundBrush`；底線（BorderBrush）`Transparent`
  - 選取 `IsSelected`（:568–:572）：背景 `TabActiveBrush`；前景 `PrimaryTextBrush`；底線色 `TabActiveBorderBrush`（底線粗細仍為 `2`）
  - 滑鼠移入 `IsMouseOver`（:573–:575）：背景 `TabHoverBrush`
  - 選取且滑鼠移入（MultiTrigger，:576–:582）：背景 `TabActiveHoverBrush`
- 寬度、高度：原檔未指定

### 3.15 TabControl（隱式，:585–:627，`OverridesDefaultStyle=True`）

- 背景：`TabBackgroundBrush`（:586）；前景：`PrimaryTextBrush`（:587）；邊框色：`BorderBrush`（:588）；邊框粗細：`0`（:589）
- HorizontalContentAlignment／VerticalContentAlignment：`Stretch`（:590–:591）；FocusVisualStyle：`null`（:592）
- 模板：`Grid` 兩列，列高 `Auto` 與 `*`（:598–:600）
- 頁籤列：`Border`（:602–:605），背景與邊框色綁定 TemplateBinding，邊框粗細 `0,0,0,1`（頁籤列下緣 1 px）
- 頁籤列內的 `ScrollViewer`：水平 `Auto`、垂直 `Disabled`、`CanContentScroll="False"`、`Focusable="False"`（:606–:610）
- 頁面內容：`ContentPresenter`，Margin 綁定 Padding（:616–:622）

### 3.16 TreeViewItem（隱式，:628–:721，`OverridesDefaultStyle=True`）

- 字型：`Segoe UI Variable`（:629）；Padding：`3,3`（:630）；前景：`PrimaryTextBrush`（:631）；背景：`Transparent`（:632）；邊框色：`Transparent`（:633）；邊框粗細：`0`（:634）
- HorizontalContentAlignment：`Left`（:635）；VerticalContentAlignment：`Center`（:636）；FocusVisualStyle：`null`（:637）
- ItemsPanel：`StackPanel`（:642）
- 版面：
  - 列高 `Grid MinHeight="28"`（:655）
  - 展開箭頭欄寬 `18`（:657）
  - 子項目縮排 `ItemsHost` Margin `18,0,0,0`（:688）
  - 展開箭頭 `Path`：寬 `5`、高 `8`（:672–:673）；填色 `MenuForegroundBrush`（:675）；路徑 `M 0,0 L 5,4 L 0,8 Z`（:676）
  - 箭頭展開時旋轉 `RotateTransform Angle="90" CenterX="2.5" CenterY="4"`（:699）
  - 展開箭頭 ToggleButton 預設 `Collapsed`（:669），有子項才 `Visible`（:692–:694）
- 狀態：
  - 展開 `IsExpanded`（:695–:702）：子項目 `ItemsHost` 顯示；箭頭旋轉 90 度
  - 選取 `IsSelected`（:703–:706）：前景 `PrimaryTextBrush`；不設背景（註解 :704：側邊欄自行替每個群組列設定顏色）
  - 滑鼠移入且未選取（MultiTrigger，:707–:713）：`ItemBorder` 背景 `MenuHoverBrush`
  - 停用（:714–:716）：前景 `MenuDisabledForegroundBrush`
- 圓角：原檔未指定
- 動畫：原檔未指定（箭頭旋轉沒有 Storyboard，是直接套 RenderTransform）

### 3.17 捲軸（ScrollBar）

色票：`ScrollBarTrackBrush`、`ScrollBarThumbBrush`、`ScrollBarThumbHoverBrush`、`ScrollBarThumbPressedBrush`（Light :722–:725）

- **Thumb**（x:Key `GlystrataScrollBarThumbStyle`，:726–:746）
  - `Focusable="False"`（:727）
  - `ThumbBorder`：`Margin="1"`、`CornerRadius="4"`（:732–:733）；背景 `ScrollBarThumbBrush`（:734）
  - 滑鼠移入（:736–:738）：背景 `ScrollBarThumbHoverBrush`
  - 拖曳 `IsDragging`（:739–:741）：背景 `ScrollBarThumbPressedBrush`
- **頁面鍵 RepeatButton**（x:Key `GlystrataScrollBarPageButtonStyle`，:747–:757）
  - `Focusable="False"`、`IsTabStop="False"`（:748–:749）
  - 模板：`Rectangle Fill="Transparent"`（:753）
- **垂直模板**（x:Key `GlystrataVerticalScrollBarTemplate`，:758–:772）
  - 外層 `Grid` 背景 `ScrollBarTrackBrush`（:759）
  - `Track` `IsDirectionReversed="True"`（:760）
  - 上、下頁鍵命令 `ScrollBar.PageUpCommand`、`ScrollBar.PageDownCommand`（:762、:768）
- **水平模板**（x:Key `GlystrataHorizontalScrollBarTemplate`，:773–:787）
  - 外層 `Grid` 背景 `ScrollBarTrackBrush`（:774）
  - `Track` `IsDirectionReversed="False"`（:775）
  - 左、右頁鍵命令 `ScrollBar.PageLeftCommand`、`ScrollBar.PageRightCommand`（:777、:783）
- **ScrollBar 預設**（隱式，:788–:802）
  - 背景 `ScrollBarTrackBrush`（:789）；寬度 `12`、MinWidth `12`（:790–:791）；預設模板為垂直模板（:792）
  - 當 `Orientation="Horizontal"`（:794–:800）：Width `Auto`、MinWidth `0`、Height `12`、MinHeight `12`，模板改為水平模板
- 圓角：Thumb 為 `4`；Track 為直角（原檔未指定）
- 動畫：原檔未指定（Thumb 只換色，沒有時間設定）

### 3.18 StatusBar 用 Slider（:803–:873）

- **StatusBarSliderThumbStyle**（x:Key，:803–:825）
  - Thumb 寬高 `14`、`14`（:804–:805）；模板為透明 `Grid` + `Ellipse ThumbEllipse`（寬高 `12`，填色 `AccentBrush`）（:809–:810）
  - 滑鼠移入（:813–:816）與拖曳（:817–:820）：`ThumbEllipse` 寬高皆改為 `14`
- **StatusBarSliderDecreaseButtonStyle**（x:Key，:826–:838）
  - `Focusable="False"`、`IsTabStop="False"`（:827–:828）
  - 透明 `Grid` 內放 `Border`：高 `3`、`CornerRadius="1.5"`、垂直置中、背景 `AccentBrush`（:832–:833）
- **StatusBarSliderIncreaseButtonStyle**（x:Key，:839–:851）
  - 同上，`Focusable="False"`、`IsTabStop="False"`（:840–:841）
  - `Border` 高 `3`、`CornerRadius="1.5"`、背景 `BorderBrush`（:845–:846）
- **StatusBarSliderStyle**（x:Key，TargetType `Slider`，:852–:873）
  - 高度 `20`（:853）
  - 模板 `Grid` 背景 `Transparent`，高度綁定（:857）
  - `Track` 垂直置中（:858）；減、加大量命令 `Slider.DecreaseLarge`、`Slider.IncreaseLarge`（:860、:866）
  - 注意：`StatusBarSliderStyle` 在 XAML 內沒有被引用（見第 7 節）

### 3.19 原檔沒有定義的控制項

- `ToolTip`：原檔未指定（兩份主題檔都沒有 ToolTip Style）
- `CheckBox`、`RadioButton`：原檔未指定
- `StatusBar`（控制項本身）：原檔未指定；只有其中的 Slider 有樣式（3.18）
- `ProgressBar`、`GroupBox`、`Expander`（獨立控制項）：原檔未指定

---

## 4. 字型清單

### 4.1 FontFamily

| FontFamily | 用在哪裡 | 來源行號 |
|---|---|---|
| `Segoe UI Variable` | 所有 XAML 樣式控制項：Button、TextBox、ComboBox、ComboBoxItem、ListBoxItem、Menu、ContextMenu、MenuItem、TabItem、TreeViewItem | Light.xaml:56、:135、:142、:234、:276、:331、:365、:392、:538、:629（Dark 相同行號） |
| `Segoe UI, Segoe UI Emoji` | 閱讀器（預覽）的 FlowDocument 預設字型 | WpfMarkdownRenderer.cs:37 |
| `Cascadia Mono, Consolas, Segoe UI Emoji` | 閱讀器的程式碼區塊與行內程式碼 | WpfMarkdownRenderer.cs:135、:367 |
| `Cascadia Code, Consolas, Segoe UI Emoji` | 編輯器（TextEditor） | EditorPaneControl.cs:274 |
| `Cascadia Mono` | 差異視窗（DiffWindow） | DiffWindow.xaml:29 |
| `Segoe Fluent Icons, Segoe MDL2 Assets` | 自訂標題列按鈕圖示 | CustomTitleBar.cs:146 |

注意：`Segoe UI Variable` 與閱讀器的 `Segoe UI` 是兩個不同的字型名稱，原檔如此（見第 7 節）。

### 4.2 字級與字重

字級（數值照原檔）：

| 字級 | 用在哪裡 | 來源行號 |
|---|---|---|
| 32、26、22、18、16、14（H1 至 H6） | 閱讀器標題 | AppSettings.cs:119–124（預設值）；WpfMarkdownRenderer.cs:91 |
| 15 | 閱讀器 FlowDocument 預設字級 | WpfMarkdownRenderer.cs:38 |
| 13 | 閱讀器程式碼區塊、行內程式碼；差異視窗 | WpfMarkdownRenderer.cs:136、:368；DiffWindow.xaml:30 |
| 14 × 縮放比例 | 編輯器（基準字級 `BaseFontSize`） | EditorPaneControl.cs:29、:275 |
| 24 | 工具列「＋」新增鈕 | MainWindow.xaml.cs:530 |
| 17 | 子選單箭頭 `›` | Light.xaml:448（Dark.xaml:448 相同） |
| 16 | 側邊欄標題、工具列圖示按鈕、頁籤內按鈕 | MainWindow.xaml.cs:659、:781；TabHeaderControl.cs:103 |
| 14 | 工具列小按鈕 | MainWindow.xaml.cs:800 |
| 12 | 自訂標題列文字；設定視窗說明文字 | CustomTitleBar.cs:115；SettingsWindow.xaml.cs:463、:642 |
| 10 | 自訂標題列按鈕圖示 | CustomTitleBar.cs:147 |
| 原檔未指定 | 所有 XAML 樣式控制項（沒有寫 `FontSize`） | — |

行高（不是字級）：閱讀器內文 `LineHeight = 15 × LineSpacing`（WpfMarkdownRenderer.cs:105、:119、:233、:306）；標題 `LineHeight = 標題字級 × LineSpacing`（:94）；FlowDocument 固定 `LineHeight = 22`（:42）。

字重：

| 字重 | 用在哪裡 | 來源行號 |
|---|---|---|
| SemiBold | 閱讀器所有標題 | WpfMarkdownRenderer.cs:92 |
| SemiBold | 側邊欄標題 | MainWindow.xaml.cs:660 |
| SemiBold | 群組列（TreeViewItem） | MainWindow.xaml.cs:862 |
| SemiBold | 設定視窗的「預覽」小標題 | SettingsWindow.xaml.cs:273 |
| SemiBold（粗體、H1 按鈕）；其他按鈕為 Normal | 編輯器工具列格式按鈕 | EditorPaneControl.cs:619–621 |
| 原檔未指定 | XAML 樣式控制項（沒有 `FontWeight`） | — |

---

## 5. 編輯器與閱讀器調色盤

以下為 `AppSettings.cs` 的預設值。使用者可在設定中改寫，改寫後以設定檔為準；`Normalize()` 只會在欄位為 null 時補回預設（AppSettings.cs:171–175）。

### 5.1 編輯器（EditorColorPalette）

亮色預設值來自 :36–:49（`CreateLightDefault()` 為 `new()`，沿用初始化器）；暗色來自 :53–:69。

| Key | 亮色 | 暗色 |
|---|---|---|
| plain | `#1F2328`（AppSettings.cs:38） | `#E6EDF3`（:57） |
| heading | `#0969DA`（:39） | `#79C0FF`（:58） |
| emphasis | `#8250DF`（:40） | `#D2A8FF`（:59） |
| link | `#0969DA`（:41） | `#79C0FF`（:60） |
| list | `#57606A`（:42） | `#8B949E`（:61） |
| quote | `#6E7781`（:43） | `#8B949E`（:62） |
| code | `#953800`（:44） | `#FFA657`（:63） |
| yaml-key | `#0550AE`（:45） | `#A5D6FF`（:64） |
| yaml-value | `#0A3069`（:46） | `#C9D1D9`（:65） |
| yaml-comment | `#6E7781`（:47） | `#8B949E`（:66） |
| front-matter | `#8250DF`（:48） | `#D2A8FF`（:67） |

### 5.2 閱讀器（ReaderColorPalette）

亮色預設值來自 :82–:89（`CreateLightDefault()` 為 `new()`，預設為單色，只有連結保留強調色，見 :91–:93 註解）；暗色來自 :96–:106。

| Key | 亮色 | 暗色 |
|---|---|---|
| text | `#24292F`（AppSettings.cs:84） | `#E6EAF0`（:100） |
| heading | `#24292F`（:85） | `#E6EAF0`（:101） |
| link | `#0969DA`（:86） | `#79C0FF`（:102） |
| quote | `#68707C`（:87） | `#A6ADB8`（:103） |
| code | `#24292F`（:88） | `#E6EAF0`（:104） |

閱讀器的程式內備用色（調色盤沒有該 key 時才會用到）：WpfMarkdownRenderer.cs:22–:29。

| 用途 | 亮色 | 暗色 | 來源 |
|---|---|---|---|
| 文字（text 缺值時；heading、code 缺值時也用此值） | `#24292F` | `#E6EAF0` | WpfMarkdownRenderer.cs:22、:30、:33 |
| 次要（quote 缺值時；Muted 固定使用此值） | `#68707C` | `#A6ADB8` | WpfMarkdownRenderer.cs:23、:28 |
| 連結（link 缺值時） | `#0969DA` | `#79C0FF` | WpfMarkdownRenderer.cs:24 |
| 表面（Surface：程式碼區塊 :133、行內程式碼 :366、表格邊框 :284、:311、表頭底色 :313） | `#F1F3F5` | `#292E36` | WpfMarkdownRenderer.cs:29 |

`Muted` 與 `Surface` 不讀取調色盤，永遠使用上表的程式內值。`Muted` 的實際用途：引言邊線（WpfMarkdownRenderer.cs:116）、分隔線底色（:157）、待辦清單方框邊框（:486）。

### 5.3 預覽字級（PreviewTypography，亮暗共用）

| 參數 | 預設值 | 來源行號 |
|---|---|---|
| H1Size | 32 | AppSettings.cs:119 |
| H2Size | 26 | :120 |
| H3Size | 22 | :121 |
| H4Size | 18 | :122 |
| H5Size | 16 | :123 |
| H6Size | 14 | :124 |
| LineSpacing | 1.35 | :125 |
| ParagraphSpacing | 10 | :126 |
| HeadingSpacing | 14 | :127 |

### 5.4 群組色

- 預設五色（AppSettings.cs:138–:142，亮暗共用）：`#3B6FF5`、`#2DA44E`、`#D97706`、`#8250DF`、`#CF222E`
- 群組顏色以 hex 字串存在群組資料中，不引用這份清單（AppSettings.cs:130–:133 註解）
- 群組選取底色的混色比例 `GroupSelectionTint` 預設 `0.14`（AppSettings.cs:165），`Normalize()` 夾在 `0` 至 `0.5`（:184）
- 混色公式（GroupColors.cs:45、:50–:53）：`ratio = Clamp(tint, 0, 1)`；每個色頻 = `Round(群組色 × ratio + SidebarBrush × (1 − ratio))`
- `tint <= 0` 時不上色（GroupColors.cs:35–:37）；hex 解析失敗視為沒有顏色條（GroupColors.cs:8–:28）

---

## 6. 主題切換機制

1. **偏好值**：`ThemePreference` 為 `Light = 0`、`Dark = 1`、`System = 2`，數值固定，因為寫在 settings.json（AppSettings.cs:12–:17）。預設為 `System`（:150）。
2. **解析**：`ThemeService.Resolve()` 把偏好轉成實際的 `ThemeKind`（ThemeService.cs:24–:29）。`Light`、`Dark` 直接對應；`System` 讀取 Windows 設定。
3. **讀取 Windows 設定**：`SystemThemeWatcher.Read()` 讀取 `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize` 的 `AppsUseLightTheme`（SystemThemeWatcher.cs:12–:13、:19–:34）。值為整數 `0` 時是暗色；其他情況（包括值不存在）都是亮色。讀取時遇到 `IOException`、`UnauthorizedAccessException`、`SecurityException` 也回傳亮色。
4. **套用**：`ThemeService.Apply()` 記下偏好後呼叫 `ApplyResolved()`（ThemeService.cs:31–:35）。`ApplyResolved()`（:37–:55）：
   - 依主題選 `Light.xaml` 或 `Dark.xaml`，建立相對 URI `Resources/Themes/{檔名}` 的 `ResourceDictionary`（:39–:43）
   - 若先前有加入過的字典，從 `Application.Current.Resources.MergedDictionaries` 移除（:46–:49）
   - 加入新字典、更新 `CurrentTheme`，觸發 `ThemeChanged`（:51–:54）
5. **系統主題變更**：`CustomTitleBar` 的視窗程序收到 `WM_SETTINGCHANGE`（0x001A，CustomTitleBar.cs:25、:291），且 `lParam` 字串為 `ImmersiveColorSet` 時，呼叫 `SystemThemeWatcher.NotifyColorSchemeChanged()`（CustomTitleBar.cs:292–:294；SystemThemeWatcher.cs:37）→ 觸發 `Changed` → `ThemeService.SystemThemeWatcher_Changed()`（ThemeService.cs:57–:72）：
   - 偏好不是 `System` 就直接忽略（:62–:65）
   - 偏好是 `System` 時重新讀取，只有與目前的 `CurrentTheme` 不同才重新套用（:67–:71）
   - 原檔註解：強調色變更也會觸發這個廣播，所以要比對，避免整個介面重建（ThemeService.cs:59–:61）
6. **C# 端的更新**：MainWindow 在 :84 訂閱 `ThemeChanged`，在 `Theme_ThemeChanged()`（MainWindow.xaml.cs:2127–:2145）中：
   - 標題列重新整理合成背景（`CustomTitleBar.RefreshCompositionBackground`）
   - 每個編輯窗格以 `EditorBackgroundBrush`、`EditorForegroundBrush` 與 `GetActivePalette()` 重新設定
   - 預覽視窗 `ApplyTheme()`，然後重新整理分頁篩選與群組選取，最後更新介面語言
   - 編輯器調色盤由 `GetActivePalette()` 依 `CurrentTheme` 選擇（MainWindow.xaml.cs:2147）
   - 閱讀器調色盤在 `PreviewWindow.xaml.cs:123–:124` 由 `ThemeService.Resolve()` 結果選擇
7. **啟動**：App.xaml:6 靜態合併 `Light.xaml`；MainWindow.xaml.cs:88–:89 先套用語言，再呼叫 `_theme.Apply(_settings.Theme)`。MainWindow.xaml.cs:1991–:1992 另有一處以 `settings.Theme` 重新套用。
8. **設定頁切換**：SettingsWindow.xaml.cs:172–:176 在偏好變更、且解析結果與原本不同時，先以舊的解析結果把目前欄位存進對應調色盤（`SavePaletteFields(previous)`），再載入新的色盤。

---

## 7. 假設與不確定之處

### 7.1 推斷（原檔沒有明寫）

1. 第 1 節的「用途」全部由 grep 出的引用位置推斷。若程式內有以字串變數組出 key 名稱的寫法，可能會漏掉；本次只搜尋了 `FindResource`、`TryFindResource`、`SetResourceReference`、`DynamicResource` 與 `StaticResource` 等明確寫法。
2. `SystemColors.*BrushKey` 五個覆寫（Light.xaml:34–:38）在 XAML 與 C# 內都沒有直接引用。推斷它影響沒有自訂樣式的 WPF 內建控制項，原檔沒有說明。
3. `GroupIndicatorBrush`（Light.xaml:27）在 XAML 與 C# 內都找不到引用，推斷為未使用的殘留，原檔沒有說明。
4. `StatusBarSliderStyle`（Light.xaml:852）在 XAML 內沒有被引用，但 MainWindow.xaml.cs:578 以 `SetResourceReference` 引用，PreviewWindow.xaml:50 以 `DynamicResource` 引用。
5. WPF 語意（未實測）：同一個 Style 內，後宣告的 Trigger 覆寫先宣告的。例如 ComboBoxItem 同時高亮與選取時，以 `IsSelected`（Light.xaml:263）的背景為準。
6. WPF 語意（未實測）：MenuItem 的 Style 沒有 `IsMouseOver` 觸發器（Light.xaml:391–:516）。推斷滑鼠移入是由 WPF 的 MenuItem 把 `IsHighlighted` 設為 true 來觸發。
7. WPF 語意（未實測）：用 `FindResource` 在建立時取得的 Brush，不會隨主題字典切換而更新；`DynamicResource` 則會。推斷 C# 端靠 `Theme_ThemeChanged` 重新取值，符合這個前提。
8. `App.xaml:6` 靜態合併的 `Light.xaml` 不會被 `ThemeService` 移除（ThemeService.cs:46–:49 只移除自己加入的字典）。推斷功能上因後加入的字典覆寫前者而不受影響，但沒有實測。
9. 原檔沒有寫明的項目一律以「原檔未指定」標示，包括：Button 等控制項的字級與字重、所有寬高（除 Menu 高 32、MenuItem 最小高 30、TreeViewItem 最小高 28、ScrollBar 寬 12、Slider 高 20 以外）、所有 ToolTip、CheckBox、RadioButton、StatusBar 本體、ProgressBar 的樣式，以及動畫時間。
10. 字級「沿用 WPF 預設」指的是 XAML 沒寫 `FontSize` 時會繼承上層值；WPF 的預設數值本身沒有在原檔中出現。

### 7.2 不一致之處（只列事實，不做建議）

1. **編輯器文字色有兩個來源**：`AppSettings.cs:38` 的亮色 `plain` 為 `#1F2328`，但 `Light.xaml:11` 的 `EditorForegroundBrush` 為 `#24292F`。暗色同理：`AppSettings.cs:57` 的 `plain` 為 `#E6EDF3`，`Dark.xaml:11` 為 `#E6EAF0`。原檔沒有說明實際畫面用哪一個。
2. **閱讀器的 Surface 色不讀取調色盤**：`WpfMarkdownRenderer.cs:29` 寫死為 `#F1F3F5`（亮）、`#292E36`（暗），與 `Light.xaml` 的 `SurfaceBrush`（`#FFFFFF`）不同，也不在 `ReaderColorPalette` 內。
3. **閱讀器的 Muted 色不讀取調色盤**：`WpfMarkdownRenderer.cs:23`、`:28` 寫死。`Muted` 用在引言邊線、分隔線與待辦方框（:116、:157、:486），`ReaderColorPalette` 的 `quote` 只作用於引言文字（:118、:123），兩者互不影響。
4. **字型名稱不同**：XAML 用 `Segoe UI Variable`，閱讀器用 `Segoe UI`，編輯器與程式碼用 `Cascadia Code` 與 `Cascadia Mono`。原檔未說明為何不統一。

### 7.3 需要人決定的事項

1. Mac 版要用哪一套字型來對應 `Segoe UI Variable`、`Segoe UI`、`Cascadia Code`、`Cascadia Mono`、`Segoe Fluent Icons`。原檔沒有 Mac 字型的對應，這些 Windows 字型在 macOS 上不一定存在。
2. 編輯器文字色以 `plain`（AppSettings.cs:38、:57）為準，還是以 `EditorForegroundBrush`（Light.xaml:11、Dark.xaml:11）為準。
3. 閱讀器 Surface 與 Muted 色，要保留 `WpfMarkdownRenderer.cs` 內的寫死值，還是改由調色盤或色票決定。
4. `GroupIndicatorBrush` 未使用，要保留在 Mac 版的色票表中，還是移除。
5. 第 7.1 節第 2 點的 `SystemColors` 覆寫，Mac 版是否需要對應。
