# Glystrata Mac 版技術研究（2026/10/13）

> 研究時間：2026/10/13（台北時間）。結論皆附來源網址與日期或版本號；查不到者標示「查不到」。
> GitHub 資料來自 GitHub API（2026/10/13 查詢）；功能細節來自各專案原始碼或 README（以查詢當天的 main 分支為準，未標明 release 版本者）。

## 1. Markdown 解析器

### 1-1 候選清單（GitHub API 查詢，2026/10/13）

| 專案 | 最新版本 | 發布日期 | 授權 | 最後 push | 狀態與備註 | 來源 |
|---|---|---|---|---|---|---|
| swiftlang/swift-markdown | 0.9.0 | 2026-09-21 | Apache-2.0 | 2026-10-12 | Apple（swiftlang）官方，底層為 cmark-gfm；有 typed AST（`Markup` 樹）；Swift tools 6.2 | https://github.com/swiftlang/swift-markdown ／ https://api.github.com/repos/swiftlang/swift-markdown/releases |
| swiftlang/swift-cmark | 0.9.0 | 2026-09-21 | 原始 repo 授權欄顯示 NOASSERTION（需讀 COPYING）| 2026-10-12 | swift-markdown 的 C 底層；apple/swift-cmark 已改為 swiftlang/swift-cmark（API 回 Moved Permanently） | https://github.com/swiftlang/swift-cmark |
| objecthub/swift-markdownkit | 2.0.0 | 2026-10-10 | Apache-2.0 | 2026-10-12 | 純 Swift（Swift 6）、有 AST；CommonMark 0.31.2；Xcode 16+、macOS 11+ | https://github.com/objecthub/swift-markdownkit |
| loopwerk/Parsley | 1.2.1 | 2026-08-24 | MIT | 2026-08-24 | 支援 metadata；**只輸出 HTML，無 AST** | https://github.com/loopwerk/Parsley |
| sciasxp/SwiftMarkdownParser | 無 release | 查不到（repo 最後 push 2026-10-09） | MIT | 2026-10-09 | 有 typed AST；Swift 6.2、macOS 14+、iOS 17+；52 commits、11 stars（查詢當下），偏新、成熟度未知 | https://github.com/sciasxp/SwiftMarkdownParser |
| JohnSundell/Ink | 0.6.0 | 2023-04-07 | MIT | 2024-03-27 | 已久未更新 | https://github.com/JohnSundell/Ink |
| johnxnguyen/Down | 無 release | 查不到 | NOASSERTION | 2023-07-15 | 已久未更新（cmark 包裝） | https://github.com/johnxnguyen/Down |
| KristopherGBaker/libcmark_gfm | 無 release | 查不到 | BSD-2-Clause | 2026-05-30 | **已封存（archived: true）** | https://github.com/KristopherGBaker/libcmark_gfm |
| gonzalezreal/swift-markdown-ui | 2.4.1 | 2024-10-13 | MIT | 2025-12-28 | 維護模式；README 指向新專案 Textual（僅為 SwiftUI 渲染器，非解析器） | https://github.com/gonzalezreal/swift-markdown-ui |
| tree-sitter-grammars/tree-sitter-markdown | v0.5.3 | 2026-02-26 | MIT | 2026-09-13 | 增量解析的語法樹（CST），偏編輯器語法分色用途，非 GFM AST | https://github.com/tree-sitter-grammars/tree-sitter-markdown |
| Markdig（xoofx/markdig，Windows 版所用） | 1.4.0 | 2026-09-20 | BSD-2-Clause | 2026 | C# 套件，Swift 不能直接用；只作為功能對照基準 | https://github.com/xoofx/markdig/releases |

### 1-2 swift-markdown 功能支援（以原始碼為準）

查證方式：讀取 `Sources/Markdown/Parser/CommonMarkConverter.swift`（main 分支，2026/10/13），其中解析時只掛載三個 cmark 擴充：`table`、`strikethrough`、`tasklist`（第 430–432 行）。解析選項 `ParseOptions` 只有 `parseBlockDirectives`、`parseSymbolLinks`、`disableSmartOpts`、`parseMinimalDoxygen`、`disableSourcePosOpts` 五個（`Sources/Markdown/Parser/ParseOptions.swift`），沒有任何和 HTML、Setext、front matter 有關的開關。

| 需求 | 支援？ | 說明與來源 |
|---|---|---|
| Pipe tables | 支援 | 掛載 `table` 擴充（CommonMarkConverter.swift:430） |
| Task lists | 支援 | 掛載 `tasklist` 擴充（:432），節點 `taskListItem` 可讀勾選狀態 |
| 刪除線 `~~` | 支援 | 掛載 `strikethrough` 擴充（:431），節點 `strikethrough` |
| 下標 `~x~` | **不支援** | cmark-gfm 無此擴充。補法：在 `Text` 節點上自寫行內解析器（需注意 `~` 與刪除線衝突，要對齊 Markdig 的判定規則） |
| 上標 `^x^` | **不支援** | 同上，無內建擴充。補法：同上 |
| 插入 `++x++` | **不支援** | 同上。補法：同上 |
| 標記 `==x==` | **不支援** | 同上。補法：同上 |
| Autolinks | 部分支援 | CommonMark 的 `<https://...>` 有支援；GFM 的裸網址自動連結（`www.`、無尖括號的 `https://`）**未掛載** `autolink` 擴充。補法：在 `Text` 節點上用 regex 或 `NSDataDetector` 補（自寫） |
| YAML front matter | **不支援** | 原始碼與 README 都沒有相關項目。補法：解析前自己切出檔首 `---`…`---` 區段，只把剩下的文字交給 parser，並保留行號偏移 |
| 停用原始 HTML | **parser 層無開關，但可在渲染層忽略** | 解析後仍會產生 `htmlBlock`（`html_block`）與 `inlineHTML`（`html_inline`）節點（CommonMarkConverter.swift:42、51、224–280）。App 直接在渲染時略過這兩種節點即可，不需要額外套件 |
| 關閉 Setext 標題 | **不支援（無選項）** | Setext 是 CommonMark 規範的一部分，cmark 沒有開關。解析後的 `heading` 節點有原始位置（`parsedRange`），可以比對原文：若標題行不是 `#` 開頭，就在渲染時改畫成段落。這是自己設計的繞法，**需人決定**「`===`/`---` 底線在關閉 Setext 後應視為段落文字還是分隔線」 |

### 1-3 其他候選的功能支援（依各自 README 與原始頁面）

| 套件 | Pipe tables | Task lists | 刪除線 | 下標／上標／插入／標記 | Autolink | YAML front matter | AST | 停用 HTML | 關閉 Setext |
|---|---|---|---|---|---|---|---|---|---|
| objecthub/swift-markdownkit 2.0.0 | 支援（`ExtendedMarkdownParser`） | 支援（`FullMarkdownParser`） | 支援 `~~x~~` | **不支援下標／上標／插入／標記**；它的 `~x~` 是「底線」（underline），語法與 Markdig 的下標相反，移植時要注意 | 不支援 GFM 裸網址 | 不支援 | 支援（`Block` 遞迴 enum） | 有 `safeMode`（在 `HtmlGenerator`／`RenderingOptions` 設定，不是 parser 選項） | 查不到 |
| sciasxp/SwiftMarkdownParser | 支援（`GFMTableNode`） | 支援 | README 未提及 | 未提及 | 未提及 | 未提及 | 支援（`AST.DocumentNode`） | `Configuration` 無相關選項 | 查不到 |
| loopwerk/Parsley 1.2.1 | 支援 | README 未提及 | 支援 | README 未提及 | 支援 | 支援（metadata 拆出） | **不支援（只輸出 HTML）** | 查不到 | 查不到 |

### 1-4 一句話結論

- swift-markdown（Apple 官方、Apache-2.0、0.9.0 於 2026/09/21 發布）是唯一同時具備「官方維護、有 typed AST、表格／任務清單／刪除線」的 Swift 解析器；但它**不支援**下標、上標、插入、標記、front matter，**也不能關閉 HTML 與 Setext**。
- 要做到 Markdig 的完整設定，需要用 swift-markdown 當底、再自寫一層行內擴充（下標、上標、插入、標記）與 front matter 切割，並在渲染層忽略 HTML、處理 Setext。
- swift-markdownkit 2.0.0（2026/10/10）有 AST 但語法不同（`~x~` 是底線），和 Markdig 的 `~x~` 下標衝突，不能直接替換。
- Parsley 有 front matter 但沒有 AST，不適合「走 AST 自己畫」的架構。
- libcmark_gfm 已封存；Ink、Down 已多年未更新。

### 1-5 查不到或待確認的項目

- Markdig 的 Setext 關閉方式：本次只查到候選 API 名稱的推測，沒有找到官方文件或原始碼證據，**查不到**。
- swift-markdown 的 cmark strikethrough 擴充是否同時接受單個 `~`：**未驗證**（需用測試字串實測）。若會，會與下標 `~x~` 衝突。
- sciasxp/SwiftMarkdownParser 的 strikethrough、autolink、front matter 支援：README 未提及，**查不到**。
- swift-markdown 0.9.0 的完整 release notes：**查不到**（只確認了 release 名稱與日期）。


---

## 2. 文字編輯元件

### 2-(a) NSTextView 的 TextKit 2 與 TextKit 1 狀況

| 查到的事實 | 來源與日期 |
|---|---|
| Apple 官方說明：`NSTextView` 在使用 TextKit 2 時，若程式碼存取 `layoutManager`（TextKit 1 的屬性），或遇到 TextKit 2 尚不支援的屬性（例如表格），或列印時，會自動切回 TextKit 1。可訂閱 `willSwitchToNSLayoutManager`／`didSwitchToNSLayoutManager` 通知偵測。 | WWDC21 "Meet TextKit 2"（session 10061）與 WWDC22 "What's new in TextKit and text views"（session 10090）；https://developer.apple.com/videos/play/wwdc2021/10061/ ／ https://developer.apple.com/videos/play/wwdc2022/10090/（投影片原文日期：2021、2022） |
| macOS 26 上的 fallback：有部落格回報 `NSTextView` 在 macOS 26 存取 `.layoutManager` 會降級到 TextKit 1，且舊 SDK 建置的 App 不會觸發。**這是二手資訊，Apple 官方文件未查到明確說明。** | Steve Troughton-Smith 的回報，經 Michael Tsai 轉述：https://mjtsai.com/blog/2025/08/15/textkit-2-the-promised-land/（2025/08/15） |
| TextKit 2 有視窗（viewport）相關的穩定性問題回報，例如捲動不穩、viewport 錯誤。作者 Marcin Krzyżanowski 表示已用繞法處理多數問題。 | https://blog.krzyzanowskim.com/2025/08/14/textkit-2-the-promised-land/（2025/08/14） |
| STTextView 專案列出的 Apple Feedback：FB14700414「NSTextList 自 macOS 14 起失效（回歸）」等，清單宣稱不完整。 | https://github.com/krzyzanowskim/STTextView（2026/10/13 讀取） |
| WWDC26 session 370 "Elevate your app's text experience with TextKit" 新增 `NSTextViewportRenderingSurface`、可子類化 `NSTextView` 覆寫 viewport 委派方法、文字附件的 view provider 重用策略。內容以 UIKit 範例為主，**未明確寫出 macOS 27**，頁面用語「our 2027 releases」與「WWDC26」並存，需以 Apple 文件確認。 | https://developer.apple.com/videos/play/wwdc2026/370/（2026 年 WWDC，讀取日 2026/10/13） |

**多個 NSTextView 共用同一份文件（TextKit 2）是否可行：可行，但做法是「共用 content storage，各自一個 layout manager」。**

- Apple DTS 的做法：一個 `NSTextContentStorage` 可以用 `addTextLayoutManager(_:)` 掛上多個 `NSTextLayoutManager`，每個 layout manager 各自搭配自己的 text container；`automaticallySynchronizesTextLayoutManagers` 預設為 `true`，可讓多個 layout manager 自動同步。來源：https://developer.apple.com/forums/thread/748367（原始貼文 2024/04，DTS 於 2024/06 標示推薦的回覆；2024/08 補充編輯需放在 `performEditingTransaction(_:)` 內）。
- WWDC26 session 370 也明確寫出「連接多個 `NSTextLayoutManager` 到同一個 `NSTextContentStorage`，一處編輯會透過共用儲存同步到其他 view」。來源同上 WWDC26 370。
- **未查到**：多個 `NSTextContentStorage` 共用同一個 `NSTextStorage` 的官方做法。論壇 thread 中提問者找到的是 content storage 預設會自建 backing `NSTextStorage`，沒有明確的設定方式。
- 注意：論壇的 Apple 回覆中，編輯 backing store 需用 `performEditingTransaction(_:)`，否則變更不一定會同步到 layout manager。這點要寫進實作規範。

### 2-(b) 開源 macOS 編輯器元件

GitHub API 與各 repo 的 Package.swift 查詢時間：2026/10/13。

| 元件 | 最新版本 | 發布日期 | 授權 | 最後 push | 最低系統 | 中日韓輸入法（IME） | 已知限制與備註 | 來源 |
|---|---|---|---|---|---|---|---|---|
| **STTextView** (krzyzanowskim/STTextView) | 2.4.1 | 2026-09-08 | GitHub 顯示 NOASSERTION；README 頁面寫 GPL v3.0，並提供商業授權 | 2026-10-06 | macOS 14.0+、iOS 16+、Xcode 26+、Swift 5.9+ | **README 未宣稱 IME／中日韓支援**。Issue 搜尋只找到無關的 PR；作者有向 Apple 回報 FB13789916「中文鍵盤下 `setMarkedText` 給出錯誤的 selection range」，**是否已處理未查到** | 語法分色要靠外掛（Plugin-TreeSitter、Plugin-Neon）或自寫 `addAttributes`；不是完整 NSTextView 替代品，不支援 RTL；未提多 view 共用 storage | https://github.com/krzyzanowskim/STTextView ／ https://api.github.com/repos/krzyzanowskim/STTextView/releases |
| **CodeEditTextView** (CodeEditApp/CodeEditTextView) | 0.12.1 | 2025-07-30 | MIT | 2026-09-20 | macOS 13+（Package.swift） | GitHub issue／PR 紀錄有修過的中日文問題：PR #40「Fix Marked Text Input」（2024/06/22 關閉；GitHub API 只顯示 closed，是否合併未確認）、Issue #26「使用中文輸入法時輸入框沒清空」（2024/04/02 關閉）、Issue #37「輸入中文時偶爾多出字母」（2024/06/13 關閉）。**目前版本是否仍有問題：查不到** | README 未說明 TextKit 版本；語法分色交給同門的 CodeEditSourceEditor；README 未提大檔效能數字 | https://github.com/CodeEditApp/CodeEditTextView ／ https://api.github.com/repos/CodeEditApp/CodeEditTextView/releases |
| **CodeEditSourceEditor** (CodeEditApp/CodeEditSourceEditor) | 0.15.2 | 2025-09-16 | MIT | 2026-04-20 | macOS 13+ | **查不到**（README 與 issue 搜尋未找到 IME 相關說明） | 以 tree-sitter 做語法分色；建立在 CodeEditTextView 上 | https://github.com/CodeEditApp/CodeEditSourceEditor |
| **CodeEdit**（完整 App，非元件） | v0.3.6 | 2025-08-26 | MIT | 2026-08-18 | 查不到 | 不適用 | 只作參考實作 | https://github.com/CodeEditApp/CodeEdit |

補充：

- STTextView 的 Package.swift 相依 `STTextKitPlus`（0.3.1 以上）與 `CoreTextSwift`（0.2.0 以上）；來源：https://raw.githubusercontent.com/krzyzanowskim/STTextView/main/Package.swift
- CodeEditSourceEditor 的 Package.swift 有多個相依套件，包含 `swift-custom-dump`；來源：https://raw.githubusercontent.com/CodeEditApp/CodeEditSourceEditor/main/Package.swift
- 2026 年針對 TextKit 2 在 marked text 行為的論壇回報：Apple Developer Forums thread 808915（2026/01）指出，日文等輸入法在組字（marked text）期間，`shouldChangeTextIn` 被呼叫時 marked range 尚未更新，UITextView 與 NSTextView 都有此描述；FB21230723；DTS 回覆在 iOS 26.1 測試時 markedTextRange 有更新。**NSTextView 在 macOS 上是否確認有此問題：查不到。** 來源：https://developer.apple.com/forums/thread/808915

### 2-(c) 對應需求的查證結論

- **純文字編輯／語法分色（正規表示式）**：STTextView 提供 `addAttributes(_:range:)`，可自己用 regex 上色；CodeEditSourceEditor 用 tree-sitter，是另一條路。
- **可縮放字級**：STTextView 與 CodeEditTextView 的 README 皆未查到字級縮放的專門說明，**查不到**，需自行驗證。
- **多窗格、游標與捲動獨立**：依 Apple DTS 做法，每個窗格需要自己的 `NSTextLayoutManager` 與 text container，共用同一個 `NSTextContentStorage`。游標與捲動位置屬於 view 狀態，各窗格獨立即可。**這是依 Apple 的架構推論，未找到已實作此需求的開源專案範例。**
- **中日韓輸入法**：三個元件都**沒有**明確宣稱完整支援；CodeEditTextView 有修過相關 bug，STTextView 有 Apple 回報的 bug 但未查到修復狀態。
- **大檔案不卡**：三者 README 都沒有提供具體檔案大小與效能數據，**查不到**。

### 2-(d) 一句話結論

- TextKit 2 下多個 NSTextView 共用一份文件是 Apple 支援的架構（共用 `NSTextContentStorage`、各自 layout manager）。
- TextKit 2 在 macOS 26 有二手回報的降級與 viewport 穩定性問題；Apple 官方 macOS 26／27 的確切變更未查到。
- 三個開源元件都是 MIT 或 GPL，最低系統 macOS 13 至 14；沒有一個明確宣稱中日韓輸入法完整支援。

### 2-(e) 查不到或來源互相矛盾

- STTextView 授權：GitHub API 顯示 NOASSERTION，README 頁面寫 GPL v3.0，**來源互相矛盾**，需讀 repo 根目錄 LICENSE 檔確認。
- macOS 26／27 上 NSTextView 是否預設 TextKit 2、何時會降級：**查不到官方文件**，只有部落格與論壇二手資訊。
- 多個 `NSTextContentStorage` 共用同一 `NSTextStorage`：**查不到**官方做法。
- STTextView、CodeEditTextView、CodeEditSourceEditor 的縮放字級與大檔效能：**查不到**具體數據。

---

## 3. 從命令列建置 macOS App

### 3-(a) 純 Swift Package 加自組 .app bundle

- 做法：`swift build -c release` 產生執行檔，再由腳本手動建立 `Glystrata.app/Contents/{MacOS,Resources}` 與 `Info.plist`，手動複製執行檔與資源。來源：社群範例 https://github.com/mabino/HelloSwiftGUI（未標日期）；社群 skill 套件 `macos-spm-app-packaging`（dimillian 版）https://skills.sh/dimillian/skills/macos-spm-app-packaging（未標日期，**未驗證是否適用 Xcode 27**）。
- Info.plist：得自己寫，社群範例只含執行檔名、bundle identifier、`LSUIElement`，**缺版本字串等欄位**（來源同上）。
- App 圖示：`.icns` 需自己組；`.icon` 需透過 `actool` 編譯（見 3-(b) 的圖示段），**查到的 actool 用法都來自社群文章，未找到 Apple 官方說明**。
- 自訂文件類型：需自己寫 `CFBundleDocumentTypes` 與 `UTExportedTypeDeclarations`（同 3-(c)）。
- 資源檔：Swift 5.3 起 SwiftPM 支援資源打包，透過 `Bundle.module` 存取（來源：同上社群文章，**屬二手資訊**）。
- 單元測試：`swift test` 可執行 XCTest 與 Swift Testing（兩者可同 target 共存）；來源 https://www.atelier-socle.com/en/articles/swift-testing-guide 與 https://forums.swift.org/t/will-swift-testing-allow-testing-w-o-xcode-on-macos/73843（二手；**查不到 Xcode 27 下的正式說明**）。

### 3-(b) XcodeGen 或 Tuist 產生 Xcode 專案，再以 xcodebuild 建置

| 工具 | 最新版本 | 發布日期 | 授權 | 最後 push | 來源 |
|---|---|---|---|---|---|
| XcodeGen | 2.46.0 | 2026-07-16 | MIT | 2026-09-13 | https://github.com/yonaskolb/XcodeGen/releases |
| Tuist | 4.212.0 | 2026-10-12 | GitHub 顯示 NOASSERTION（需讀 LICENSE） | 2026-10-12 | https://github.com/tuist/tuist |

- XcodeGen 2.45.1（2026/03/05）加入內建 `.icon` 資料夾支援（for Icon Composer）（來源：XcodeGen releases 頁，PR #1600）。
- XcodeGen 的 release 頁沒有提到 document types、UTI、Xcode 26／27 支援；**這些項目查不到**。
- Tuist 本次只查到 release 與授權資訊，**Info.plist、圖示、文件類型處理方式查不到**。
- 設定檔產生的 `.xcodeproj` 仍可用 `xcodebuild -scheme ... -destination 'platform=macOS'` 建置（通用 Xcode 命令列用法，**本次未取得 Apple 官方頁面內容確認**）。

### 3-(c) 直接把 .xcodeproj 放進版本控制

- 本次**查不到**針對此做法的比較型來源（merge conflict、專案檔維護成本等）。
- 查到的事實：XcodeGen 的 2.45.0 版引入 `projectFormat` 與 synced folder 相關設定（來源同 XcodeGen releases 頁），顯示專案檔格式與同步資料夾是工具端持續在改的範圍。

### 3-(d) 圖示（Icon Composer `.icon` 與 `.icns`）

- Xcode 26 之後 Icon Composer 產出 `.icon`；社群做法：把 `.icon` 拖進專案與 `Assets.xcassets` 同層，在 target 的 App Icon 欄位填入名稱（不含副檔名），並在 Info.plist 設 `CFBundleIconName`。來源：https://developer.apple.com/forums/thread/794485 ／ https://useyourloaf.com/blog/adding-icon-composer-icons-to-xcode/（2025 年 Xcode 26 beta 期間的討論）。
- 命令列編譯：社群提供 `xcrun actool` 的範例（`--compile`、`--app-icon`、`--platform macosx`、`--target-device mac`、`--output-partial-info-plist /dev/null`），輸出 `Assets.car`。來源：https://forum.xojo.com/t/how-to-create-app-icons-for-macos-26-and-maintain-compatibility-with-older-oss/86527?page=3 ／ https://chromium.googlesource.com/chromium/src/+/505351ae3da61d53f4b4a9338803c818db8ca0af/tools/mac/icons/compile_car.py。**這些 flag 多半是未公開的 flag，Xcode 版本之間可能變動，需實機確認。**
- `.icns` 回退（給舊系統）：Xcode 26 beta 4 之後，「Asset Catalog 與 `.icon` 並存、讓舊 macOS 用 `.icns`」的做法已不可行（來源：https://developer.apple.com/forums/thread/794485）；Xcode 26.1 據報移除 `--enable-icon-stack-fallback-generation=disabled` 的解析（來源：https://mjtsai.com/blog/2025/11/05/xcode-26-1/ 的引述，**二手**）。**Apple 官方文件的 `.icns` 回退作法：查不到**（Apple 文件頁面本次只取得標題）。
- 來源矛盾：Chromium 註解說 Xcode 26b5 之後不再產生 `.icns`，mpv 的建置說明仍會同時產生 `.icns`。兩者版本不同，**無法統一結論**。

### 3-(e) 自訂文件類型（`.gss` 可用本 App 開啟並顯示自訂圖示）

- 社群做法：在 Xcode 的 Info 分頁設定 Exported UTIs（identifier、描述、conformance、副檔名），或直接寫 plist：`UTExportedTypeDeclarations` 加上 `CFBundleDocumentTypes`（`LSItemContentTypes` 填入同一 identifier、`LSHandlerRank` 設 `Owner`）。來源：https://developer.apple.com/forums/thread/783646?page=2 ／ https://developer.apple.com/forums/thread/110815（論壇，2024–2025）。
- 已知陷阱：`LSItemContentTypes` 必須與匯出的 UTI 字串完全一致；否則雙擊無法開啟（來源：同上論壇討論）。
- 注意：一則論壇留言指出 Apple 的 `UTExportedTypeDeclarations` 參考頁面頂端有「已過時、不再更新」的警示；**本次未能讀到 Apple 官方頁面全文確認此警示**，屬查不到。
- 自訂圖示：文件類型的圖示同樣需要設定（來源：https://developer.apple.com/forums/thread/775581，論壇，未取得內容）。**查不到具體步驟。**

### 3-(f) 一句話結論

- XcodeGen 2.46.0（2026/07/16，MIT）是目前可確認仍在更新、且已支援 `.icon` 的專案產生工具；Tuist 4.212.0（2026/10/12）也在活躍更新。
- 純 SwiftPM 加自組 bundle 可行，但 Info.plist、圖示、文件類型都要自己處理，且找到的範例多為社群作品。
- `.icon` 圖示的命令列處理與舊系統回退，目前主要依賴社群文章與未公開的 `actool` 參數，**不穩定**。

### 3-(g) 查不到或來源矛盾

- Apple 官方文件（CFBundleDocumentTypes、Icon Composer、Adopting Liquid Glass、Xcode 27 Release Notes、App Sandbox）本次 WebFetch 只取得頁面標題，**內文未取得**，相關結論均改用二手來源。
- `.icns` 回退與 `actool` 參數在不同 Xcode 版本的行為：來源互相矛盾，**需實機測試**。

---

## 4. 簽章與發布

### 4-(a) Ad-hoc 簽章的 App 在 macOS 26／27（Apple Silicon）上的限制

- 來源確定的部分：macOS Sequoia（15）起，使用者無法再以 Control-click 略過未簽章或未公證軟體的 Gatekeeper 檢查；要到「系統設定 > 隱私權與安全性」逐一允許。來源：https://www.cultofmac.com/864843/macos-sequoia-removes-handy-shortcut-bypass-gatekeeper-security/（2024/08）、https://9to5mac.com/2024/08/13/security-bite-apple-finally-making-it-harder-to-override-gatekeeper-is-a-telling-move/（2024/08）。
- 查不到：macOS 26 與 27 是否仍是同樣行為的明確來源。本次找到的來源多是 2024 年 Sequoia 測試期的報導。
- 查不到：ad-hoc 簽章在 macOS 26／27 上的其他限制（例如 entitlements、鑰匙圈、權限重設）。
- 假設（未從來源讀到）：Apple Silicon 上的執行檔必須至少有簽章（ad-hoc 也算），否則無法執行；此點為本研究者的推論，需實機確認。

### 4-(b) Developer ID 簽章加 notarization 的流程與費用

- 費用：Apple Developer Program 年費 **USD 99**（來源：https://developer.apple.com/programs/，頁面只有 "Copyright © 2026"，無發布日期；2026/10/13 讀取）。同頁有 Enterprise Program 為 USD 299 年費，與本案無關。
- 流程（來源：https://developer.apple.com/developer-id/，頁面 © 2026，無發布日期）：
  1. 用 Account Holder 身分在 Certificates 產生 Developer ID 憑證（Xcode 或開發者網站）。
  2. 開啟 Hardened Runtime，宣告需要的 entitlements，archive 後測試。
  3. 送公證：`xcrun notarytool`（ZIP、PKG 或 DMG）。
  4. 公證成功後以 `xcrun stapler` 附加票證。
- 補充（來源：Apple 公證頁面本次只取得標題，**以下為研究者既有知識，未從來源確認**）：`notarytool` 的 `store-credentials`、`submit --keychain-profile --wait` 指令；公證服務不接受 ad-hoc 或未簽章的軟體。
- 查不到：公證的排隊時間、每次公證的實際費用（公證本身屬於年費內，但頁面未明確寫出「公證包含在 $99 內」）。

### 4-(c) App Sandbox 開或不開的差別（快照檔與重開後重新開啟上次檔案）

- **寫入與來源檔同一資料夾的 sibling 檔（例如 `筆記.md.gss`）**：
  - 只勾選 user-selected read-write 時，sandbox 只授權使用者在 open panel 選到的那個檔案，**不授權同資料夾的其他檔案**。Apple DTS 於 2024/11 回覆（非完整肯定）：建議研究「document-relative security scoped bookmarks」；社群建議先用 open panel 授權整個資料夾。來源：https://developer.apple.com/forums/thread/769345（2024/11，讀取內容）。
  - 社群的資料夾授權做法：授權是 session 級的，若要跨啟動保留需另存 bookmark；文章的 bookmark 儲存步驟只是佔位註解。**查不到完整可行的範例。**
- **重開後重新開啟上次檔案**：
  - 一般來源（社群文件與 Apple 論壇）指出：用 `url.bookmarkData(options: .withSecurityScope, ...)` 存 bookmark，下次啟動再 resolve 並呼叫 `startAccessingSecurityScopedResource()`。來源：https://developer.apple.com/forums/thread/66259（論壇，未標精確日期）與 https://direct.appcoda.com/?p=18601（未取得全文）。**Apple App Sandbox 官方頁面本次只取得標題，此做法未由官方文件確認。**
- **不開 sandbox 的差別**：查不到直接比較來源。研究者推論：不開 sandbox 時，檔案讀寫不受 entitlement 限制，上述問題不存在；但 Developer ID 發布仍需 Hardened Runtime 與公證。**這是假設，需人決定是否要走 sandbox。**
- 查不到：sandbox 下寫入 sibling 檔的完整正式方案（例如 document-relative bookmark 的實際程式碼）。

### 4-(d) 一句話結論

- 未公證、ad-hoc 簽章的 App 在 Sequoia 起需要使用者到系統設定手動允許，此行為在 macOS 26／27 的確認來源未找到。
- Developer ID＋公證的年費是 USD 99；流程為 Developer ID 簽章 → Hardened Runtime → `notarytool` → `stapler`。
- Sandbox 下寫入來源檔旁的 sibling 檔需要額外的授權（document-relative bookmark 或資料夾授權），重啟後恢復存取要靠 security-scoped bookmark；這些都未取得 Apple 官方完整頁面確認。

---

## 5. 隱藏檔

- Finder 表現與 `URLResourceValues.isHidden`、`chflags hidden` 的具體行為：**本次未取得 Apple 官方頁面內容**，`isHidden` 是否可寫入、Finder 顯示細節皆查不到官方說明。
- iCloud Drive（來源：https://eclecticlight.co/2026/06/02/bsd-flags-are-incompatible-with-icloud-drive/，2026/06/02，測試環境 macOS 26.5）：
  - 將資料夾移入 iCloud Drive 後，`hidden` 旗標在第二台 Mac 上**偵測不到**，檔案完整可見。
  - 文章分類：`arch`、`hidden`、`nodump`、`opaque` 同步後失去；`uchg` 在原機就被移除；`sappnd`、`schg`、`uappnd` 造成同步錯誤。
  - 作者推測此問題可能影響其他雲端服務（FileProvider 框架），**屬推測**。
- Dropbox：社群回覆提到隱藏 `.dropboxignore` 不影響 Dropbox 讀規則，但修改屬性會被視為檔案變更而觸發同步；來源：https://community.dropbox.com/en/profile/comments/vladgasan（未標日期）。**Dropbox 是否保留 hidden 旗標：查不到。**
- OneDrive：查不到。
- Windows：**查不到**。本次沒有找到 macOS `hidden` 旗標如何對應到 Windows 的 Hidden 屬性的來源。
- 假設：若需要在各平台都隱藏，檔名以 `.` 開頭（dotfile）是另一種常見做法，但在 Windows 上仍會顯示；這是研究者的推論，未查證來源。

---

## 6. 最低系統版本

### 6-(a) Xcode 27 可設定的最低 macOS deployment target

- Xcode 27（build 27A266a）於 2026/09/14 隨 macOS 27.0 發布（來源：https://blakecrosley.com/blog/xcode-27-release，2026/09/15，更新 2026/10/11；該文引用 Apple 的 Releases feed 與 Xcode 27 Release Notes，**但本次 Apple 原始頁面只取得標題，無法逐項核對**）。
- 最低 deployment target：macOS **12**（由 11 提高）；watchOS 9；iOS／iPadOS／tvOS 15（不變）（同上來源）。
- Xcode 27 只在 Apple silicon 上安裝與執行（來源：同上與 https://blakecrosley.com/blog/xcode-27-drops-intel，2026/07/26，更新 2026/09/16，引用 Apple Deprecations 條目）。
- 主機需求的來源矛盾：`xcode-27-release` 文章寫 macOS Tahoe 26.6 以上；`xcode-27-drops-intel` 文章寫 26.4 以上（更新後標示為 26.6）。**以 Apple 官方頁面為準，本次未能確認。**
- Swift 版本：Swift 6.4（同 `xcode-27-release` 文章）。
- 注意：目標設為 macOS 27.0 以上時，`ARCHS_STANDARD` 不再包含 `x86_64`（同 drops-intel 文章）。本案只支援 Apple Silicon，此項影響很小。

### 6-(b) macOS 版本使用比例

- BusinessStats（2026 報告）估計：至 2026 年中，Tahoe（macOS 26）約佔全球 Mac 的 64%，最新兩版約 84%。來源：https://businesstats.com/worldwide-macos-version-market-share/（**模型估計值，不是實際計數**）。
- StatCounter 的分地區數據與 BusinessStats 不一致，且 StatCounter 自 Catalina 之後無法精確區分 macOS 版本（來源：https://gs.statcounter.com/macos-version-market-share/all/virgin-islands-(us)，**頁面數據在本次查詢中有明顯錯誤跡象，不建議引用**）。
- 查不到：Apple 官方的版本使用統計。

### 6-(c) Liquid Glass 對用新 SDK 建置的 App 在舊系統上的影響

- Apple 官方頁面「Adopting Liquid Glass」本次只取得標題，**內文查不到**。
- 二手來源（Apple Developer Forums 留言）：Liquid Glass 的外觀由作業系統提供，不是 App 自帶；使用新 SDK 建置、在舊系統上執行時，**預期**不會看到 Liquid Glass（研究者推論，**未從來源確認**）。來源：https://developer.apple.com/forums/thread/787471 等（未取得全文）。
- 二手來源（blog）：Xcode 26 release candidate 需要建置機器為 macOS Sequoia 15.6 以上（來源：https://www.issoh.co.jp/tech/details/9027/ 的相關段落，**未確認**）。
- 查不到：舊系統上 App 圖示、控制項外觀、文件類型圖示在新 SDK 建置後的行為。

---

## 文件結尾

### 各題一句話結論（只陳述查到的事實）

1. **Markdown 解析器**：swift-markdown 0.9.0（2026/09/21，Apache-2.0）支援表格、任務清單、刪除線，有 AST，但不支援下標／上標／插入／標記、front matter、停用 HTML 與關閉 Setext 的選項。
2. **文字編輯元件**：Apple 文件與 WWDC26 說明多個 `NSTextLayoutManager` 可共用一個 `NSTextContentStorage`；STTextView 2.4.1（2026/09/08）、CodeEditTextView 0.12.1（2025/07/30）、CodeEditSourceEditor 0.15.2（2025/09/16）均未明確宣稱完整中日韓輸入法支援。
3. **命令列建置**：XcodeGen 2.46.0（2026/07/16，MIT）已支援 `.icon` 資料夾；純 SwiftPM 組 bundle 可行但 Info.plist、圖示、文件類型都需手動處理；`.icon` 的命令列與 `.icns` 回退依賴社群經驗。
4. **簽章與發布**：Sequoia 起未公證軟體需使用者在系統設定中手動允許（2024 年來源）；Developer ID 年費 USD 99；sandbox 下寫入來源檔旁的 sibling 檔需要額外授權，Apple DTS 建議 document-relative bookmark。
5. **隱藏檔**：`hidden` 旗標在 iCloud Drive 同步後會遺失（2026/06 測試，macOS 26.5）；Dropbox、OneDrive、Windows 的行為查不到。
6. **最低系統版本**：Xcode 27 的最低 macOS deployment target 為 12（二手來源，2026/09/15）；Tahoe 約佔全球 Mac 64%（BusinessStats 模型估計）；Liquid Glass 的舊系統行為查不到官方說明。

### 查不到或來源互相矛盾的項目

- Apple 官方頁面（CFBundleDocumentTypes、Icon Composer、Adopting Liquid Glass、Xcode 27 Release Notes、App Sandbox、Notarizing macOS software）本次 WebFetch 只取得頁面標題，**內文無法核對**；相關結論改用二手來源，已在各段標示。
- Xcode 27 主機最低 macOS 版本：26.6 與 26.4 兩種說法（二手來源互相矛盾）。
- `.icns` 回退：Chromium 與 mpv 的建置說明結論不同。
- STTextView 授權：GitHub 顯示 NOASSERTION，README 寫 GPL v3.0。
- StatCounter 分地區數據不可靠（同上，見 6-(b)）。
- ad-hoc 簽章在 macOS 26／27 的確切行為：查不到。
- 不開 sandbox 的實際差異：查不到直接來源。
- Dropbox／OneDrive／Windows 對 hidden 旗標的處理：查不到。
- Apple 官方的 Developer ID 年費是否包含公證：頁面未明確寫出。

### 你的假設（非直接從來源讀到）

- Apple Silicon 上的執行檔至少需要簽章（ad-hoc 亦可）才能執行。
- `notarytool` 指令用法與「公證服務不接受 ad-hoc 或未簽章軟體」（來源未讀到，來自既有知識）。
- Sandbox 關閉時不受本文提到的 sibling 檔授權限制（推論，未查到直接比較來源）。
- 在 macOS 15 上執行新 SDK 建置的 App，不會顯示 Liquid Glass 外觀（推論）。
- dotfile（以 `.` 開頭）作為跨平台隱藏的替代做法，在 Windows 上仍會顯示（推論）。
- 社群提供的 `actool` 參數在 Xcode 27 中仍可用（未驗證）。

### 需要人決定的事項

- 要不要走 App Sandbox：不開 sandbox 比較簡單，但上架 Mac App Store 必須開；直接發布 Developer ID 則不一定需要。（本次只整理事實，不替你選擇。）
- 「`===`／`---` 底線在關閉 Setext 後應視為段落文字還是分隔線」。
- 是否要付 USD 99 年費取得 Developer ID 並公證（目前尚無 Apple Developer 帳號）。
- 圖示若要支援舊 macOS，是否接受 Asset Catalog 同時包含 `.icon` 與舊圖的做法，或只支援新系統。
- 下標、上標、插入、標記的語法細節（例如 `~` 與刪除線衝突時以哪個為準）。
