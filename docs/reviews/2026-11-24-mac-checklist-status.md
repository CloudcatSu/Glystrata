# Glystrata Mac 驗收清單逐項狀態（mac/align-1，唯讀檢查）

路徑縮寫：`App`=mac/App、`Core`=mac/Sources/GlystrataCore、`T`=mac/Tests/GlystrataCoreTests、`plan`=docs/plans/2026-10-13-mac-port-plan.md（3-1…3-5 為其小節）、`dec`=docs/plans/2026-10-13-mac-open-decisions.md、`rev`=docs/reviews/2026-10-13-windows-issues-found-during-mac-port.md（B/T/S/C 為其項目編號）。
狀態：OK-tested / OK-code / DIFF-decided / DIFF-open / MISSING / N/A / UNSURE。本次沒有跑 swift test，「tested」指單元測試存在且名稱對得上，或 plan 3-2/3-4/3-5 有記錄。

## 1. 檔案開啟與儲存
| # | 項目 | 狀態 | 證據／備註 |
|---|---|---|---|
| 1.1 | ⌘N 建立未命名分頁，UTF-8 無 BOM、LF | OK-code | Core/Documents/DocumentManager.swift:62-66（.utf8/.lf）；AppModel.swift:239；標題 AppModel.swift:202。快速鍵改 ⌘N 屬 plan §1 決定 |
| 1.2 | ⌘O 開檔對話框，可多選，預設篩選 md/yaml/txt＋所有檔案 | DIFF-open | 可多選 OK（AppModel.swift:259-266），但沒設 allowedContentTypes/篩選，Mac 顯示所有檔案；群組「開啟檔案」同（AppModel+Groups.swift:78-87）。沒有決定記錄 |
| 1.3 | 開已開著的檔案不重複 | OK-tested | T/DocumentTests.swift viewsOfTheSameFileShareContentButNotScrollPosition、samePathSpelledDifferentlyIsTheSameDocument；plan 3-5「重開同一檔案不重複開」 |
| 1.4 | 開檔失敗跳「開啟檔案失敗」、不建分頁、從最近清單移除 | OK-code | AppModel.swift:280-286（recentFiles.remove＋showError error.open）；編碼拒絕有測試 DocumentTests.rejectsUTF16BigEndianAndInvalidUTF8，UI 流程無記錄 |
| 1.5 | 已有路徑的文件儲存，「•」消失、狀態列「已儲存」 | OK-tested | plan 3-2（自動儲存寫回）、3-5（⌘S）；DocumentModels.swift:134-139；狀態字串 SidebarAndStatusBar.swift:310 |
| 1.6 | 手動儲存後狀態列顯示「已自動儲存」（待決） | DIFF-decided | Mac 手動存顯示「已儲存」，只有自動存顯示「已自動儲存」（AppModel+Workspace.swift:306-308）。rev B5 |
| 1.7 | 未命名按儲存開另存對話框，預設 untitled.md、篩選 md/yaml/所有檔案，取消不變 | DIFF-open | 預設檔名是「未命名.md」（AppModel.swift:311，隨語言），不是 untitled.md；沒有篩選。取消不變 OK（AppModel.swift:314-316）。沒有決定記錄 |
| 1.8 | 另存新檔改分頁名；覆寫確認；失敗路徑回復舊的 | OK-tested | 覆寫確認由 NSSavePanel 內建；T/DocumentTests.swift untitledDocumentNeedsSaveAsAndFailedSaveAsLeavesItUntitled（寫入成功才改路徑，DocumentManager.swift:142-144，rev B2） |
| 1.9 | 另存成功後新路徑加入最近清單 | OK-code | AppModel.swift:320 |
| 1.10 | 唯讀存檔失敗：「儲存失敗：檔案是唯讀檔案。」、保留未儲存標記、可再存 | OK-tested | plan 3-2（唯讀資料夾，提示只跳一次、標記保留）；T/DocumentTests failedSaveKeepsTheDocumentModified；FilePersistenceService.swift:19-21、GlystrataError.swift:18 |
| 1.11 | 同目錄暫存檔→取代，失敗不留半寫入、暫存檔清除 | OK-code | Core/Documents/FilePersistenceService.swift:25-35（replaceItemAt/moveItem，defer 刪暫存）；無專門測試 |
| 1.12 | 同一文件多次存檔依序排隊 | OK-tested | T/DocumentTests overlappingSavesAllComplete；DocumentManager.swift:127-155（saveTails） |
| 1.13 | 關閉未修改分頁直接關 | OK-code | AppModel.swift:330-343 |
| 1.14 | 關閉有未儲存變更的分頁：「尚有未儲存變更」，儲存/不儲存/取消；取消或 Esc 不關 | OK-tested | plan 3-2（結束時詢問，取消/不儲存）、3-5（關閉漂浮視窗的儲存/不儲存/取消）。按鈕順序是 儲存/取消/不儲存（AppModel.swift:452-454，Mac 慣例）；Esc 對應見 20.5（UNSURE） |
| 1.15 | 未命名有內容按「儲存」→另存；對話框取消則不關 | OK-tested | plan 3-5（儲存走存檔對話框、取消不關）；AppModel.swift:335-337 |
| 1.16 | 關閉整個視窗／離開時逐份詢問，任一取消中止 | OK-tested | plan 3-2（取消留在 App、不儲存正常結束）；AppModel+Workspace.swift:463-486。Mac 關視窗不結束，詢問改在 ⌘Q（見 1.23） |
| 1.17 | 關閉時存檔在背景執行不死鎖 | OK-code | Swift async 流程，AppModel+Workspace.swift:476（await save）；quit 時「儲存」分支無專門記錄 |
| 1.18 | 分頁重新命名：改磁碟檔名、搬快照檔、更新群組項目與分頁路徑 | OK-code | AppModel+Groups.swift:198-242（moveItem、renameSidecar、groups.renamePath、documents.renamePath）；T/SnapshotTests sidecarFollowsARename 只涵蓋快照檔；UI 未手測 |
| 1.19 | 新名為空／相同／同名已存在不執行；同名顯示「目標檔名已經存在…」 | OK-code | AppModel+Groups.swift:211-226（含只改大小寫不算衝突） |
| 1.20 | 檔名含不合法字元顯示「檔名包含不合法的字元。」（需 Mac 規則） | OK-code | Mac 規則：含 / 或 :、或 . / ..（AppModel+Groups.swift:214）；規則為實作者自訂，沒有明文決定 |
| 1.21 | 未命名分頁重新命名提示「未選擇檔案」 | OK-code | AppModel+Groups.swift:200-203（dialog.noFile） |
| 1.22 | 分頁右鍵「在資料夾中顯示」（Mac=Finder），未存檔停用 | OK-code | PaneView.swift:156-159、AppModel+Groups.swift:177；plan 3-2 決定改稱「在 Finder 中顯示」 |
| 1.23 | 「離開」與關閉鈕走同一套關閉流程 | DIFF-decided | Mac 關主視窗不結束 App，詢問在 ⌘Q（GlystrataApp.swift:68-82）。dec #11、plan 3-1 |

## 2. 編碼與換行
| # | 項目 | 狀態 | 證據／備註 |
|---|---|---|---|
| 2.1 | UTF-8 無 BOM 讀寫保持 | OK-tested | T/DocumentTests roundTripKeepsEncodingAndLineEnding（utf8/LF） |
| 2.2 | UTF-8 BOM 保留 | OK-tested | 同上（utf8WithBom/CRLF）；FileCodec.swift:59-60 |
| 2.3 | UTF-16 LE 保留 BOM | OK-tested | 同上（utf16LE/CR）；FileCodec.swift:61-62 |
| 2.4 | UTF-16 BE 顯示「…不在支援範圍內。」不開啟 | OK-tested | T/DocumentTests rejectsUTF16BigEndianAndInvalidUTF8；FileCodec.swift:25-26 |
| 2.5 | 非 UTF-8／UTF-16LE（Big5 等）嚴格解碼失敗不開啟 | OK-tested | 同上（無效 UTF-8）；FileCodec.swift:33-35。Big5 不支援為 rev S4 決定不做 |
| 2.6 | 換行多數決，平手 CRLF>LF>CR，無換行視為 LF | OK-tested | T/DocumentTests mixedLineEndingsPickTheMostCommon；FileCodec.swift:71-104 |
| 2.7 | 存檔時換行統一為偵測格式 | OK-tested | T/DocumentTests saveReplacesContentAndKeepsLineEndings；FileCodec.swift:44-54 |
| 2.8 | 內部一律 LF；未命名為 LF | OK-tested | FileCodec.swift:38（decode 正規化）；roundTrip 測試讀回與 LF 原文相等；DocumentManager.swift:63 |
| 2.9 | 狀態列顯示列舉名稱（Utf8WithBom、CrLf） | OK-code | SidebarAndStatusBar.swift:342-356（刻意沿用同樣拼法） |

## 3. 編輯與語法分色
| # | 項目 | 狀態 | 證據／備註 |
|---|---|---|---|
| 3.1 | 行號＋自動換行，無水平捲軸 | OK-tested | EditorView.swift:31-73（widthTracksTextView、hasHorizontalScroller=false、LineNumberRulerView）；plan 3-3 截圖比對 |
| 3.2 | 預設字型 Cascadia/Consolas/Segoe UI Emoji、字級 14 | DIFF-decided | Mac 用系統等寬字（DocumentTextStore.swift:14），baseFontSize 14（:6）。plan §1「字型照 Mac」、3-3「編輯器行距…沒有改」 |
| 3.3 | 復原／重做由編輯器處理 | OK-tested | plan 3-2（復原與重做）；EditorView.swift:185-187 共用 UndoManager；系統 Edit 選單提供 Undo/Redo |
| 3.4 | 同文件兩檢視同步、游標捲動各自獨立 | OK-tested | plan 3-5（兩邊打字即時同步）；T/DocumentTests viewsOfTheSame… |
| 3.5 | 兩檢視共用 undo 歷史 | OK-code | DocumentTextStore.swift:27（每份文件一個 UndoManager）、EditorView.swift:185；無跨窗格 undo 的測試記錄 |
| 3.6 | 語法分色涵蓋標題、引用、清單、連結圖片、行內碼、強調、fence | OK-tested | T/InterfaceLogicTests wholeLineRules、listMarkersAndInlineSpans；SyntaxHighlighter.swift:80-145 |
| 3.7 | 第 1 行 `---` 到下一個 `---`（前 30 行）整段 front-matter 色 | OK-tested | T/InterfaceLogicTests frontMatterIsYAMLUntilItCloses、frontMatterNeedsToOpenOnTheFirstLineAndStaysNearTheTop；rev B15 |
| 3.8 | .yaml/.yml 整檔 YAML 分色 | OK-tested | T/InterfaceLogicTests yamlFilesColourKeysValuesAndComments、recognisesYAMLFilesByExtension；rev B10 |
| 3.9 | Markdown 內 ```yaml 區塊以 YAML 分色（現行程式未實作） | DIFF-decided | Mac 未實作（逐行分色，無圍欄狀態，SyntaxHighlighter.swift:80）。rev §3 S2 與「Cloud 對第 3、4 節的決定」：這一輪不做 |
| 3.10 | 亮/暗各一組配色，套用後已開分頁立即重繪 | OK-tested | AppModel+Settings.swift:38、DocumentTextStore.swift:177-182；rev §4 C5（實測改紅色後文字變紅） |
| 3.11 | 語法配色逐類可調、可還原預設 | OK-tested | T/SettingsFormTests（themeSwitch、roundTrip）；SettingsForm.swift:45 的 11 個 key |

## 4A. Markdown 自動輸入
| # | 項目 | 狀態 | 證據／備註 |
|---|---|---|---|
| 4A.1 | `- foo` 行尾 Enter 延續 `- ` | OK-tested | T/TypingAssistantTests listsContinue；plan 3-2（Enter 續行清單） |
| 4A.2 | `*`、`+` 延續相同符號 | OK-tested | listsContinue |
| 4A.3 | 有序清單遞增 | OK-tested | listsContinue（含 9.→10.、`1)`→`2)`，rev B9） |
| 4A.4 | 核取方塊續行為未勾選 | OK-tested | listsContinue（`- [x] done`→`- [ ] `） |
| 4A.5 | 縮排項目續行取代而非疊加縮排 | OK-tested | T/TypingAssistantTests indentationAlreadyOnTheNewLineIsReplacedNotDoubled；MarkdownTypingAssistant.swift:83-94 |
| 4A.6 | 項目文字中間按 Enter，後半成新項目內容 | OK-code | NSTextView 先斷行，再在新行首插入符號（EditorView.swift:249-256、TypingAssistant:48-95）；測試只涵蓋行尾與文件中段，沒有「行內切開」案例 |
| 4A.7 | 空項目 Enter 清除符號 | OK-tested | T/TypingAssistantTests returnOnAnEmptyItemClearsIt |
| 4A.8 | 非清單縮排行 Enter 只沿用縮排 | OK-code | AppKit 預設自動縮排行為；TypingAssistantTests otherLinesAreLeftAlone 只驗證不加符號 |
| 4A.9 | `->` `<->` `=>` `>=` `<=` `...` `--` 立即替換 | OK-tested | T/TypingAssistantTests typedSequencesBecomeSymbols；plan 3-2（`->` 換 `→`） |
| 4A.10 | `---` 與表格分隔列的 `--` 不替換 | OK-tested | T/TypingAssistantTests dividersTableRowsAndPartialSequencesAreLeftAlone |
| 4A.11 | 剛替換後 Backspace 還原 | OK-tested | plan 3-2（緊接 Backspace 還原）；typedSequencesBecomeSymbols（reverting）；EditorView.swift:258-273 |
| 4A.12 | 注音組字中不誤觸發續行／替換 | OK-code | EditorView.swift:239（`!hasMarkedText()`）；plan 3-2 明說注音輸入未手測。YAML 檔不替換符號（rev B11）已修 |

## 4B. 文字編輯快捷列
| # | 項目 | 狀態 | 證據／備註 |
|---|---|---|---|
| 4B.1 | 設定預設關，套用後立即出現／消失 | OK-tested | plan 3-2（勾選後套用，工具列立刻出現，設定檔有寫入）；PaneView.swift:16-18 |
| 4B.2 | 按鈕：內文、H1–H6、粗斜刪、行內碼、連結、清單、有序、引用、程式碼區塊 | OK-code | PaneView.swift:249-266（16 顆）；plan 3-3 版面比對（工具列移到 TAB 列上方） |
| 4B.3 | 有選取：套用到選取並保持選取 | OK-tested | T/DocumentTests wrapsSelectionInBold；plan 3-2（粗體按鈕） |
| 4B.4 | 無選取：插入 bold/italic/strikethrough/code 佔位字並選取 | OK-tested | T/DocumentTests insertsPlaceholderWhenNothingIsSelected（只驗粗體；其餘共用 applyInline，MarkdownFormattingService.swift:67-82） |
| 4B.5 | 標題按鈕覆寫各行層級（非切換），「內文」移除 | OK-tested | T/DocumentTests changesHeadingLevel |
| 4B.6 | 無選取按標題插入 `# heading`；內文插入 `text` | OK-code | MarkdownFormattingService.swift:104-108（空行才插佔位；與 Windows MarkdownFormattingService.cs:102-107 相同）；無測試 |
| 4B.7 | 清單／引用：全有前綴則移除，否則加上 | OK-tested | T/DocumentTests togglesListPrefixes |
| 4B.8 | 有序清單每行 `1. ` | OK-tested | togglesListPrefixes |
| 4B.9 | 程式碼區塊包 ``` 行 | OK-tested | T/DocumentTests togglesCodeFence |
| 4B.10 | 連結：輸入框預設 https://，有選取成連結文字，否則 `link text`，取消不變 | OK-tested | T/DocumentTests buildsLinkAroundSelection；對話框 AppModel.swift:227-231；取消 return |
| 4B.11 | 純文字轉換、巢狀清單保守處理 | OK-code | MarkdownFormattingService.swift（行首前綴） |
| 4B.12 | 編輯區仍只有語法分色 | OK-code | EditorView.swift:46（isRichText=false） |
| 4B.13 | 說明→Markdown 語法說明視窗 | OK-code | AppModel+Reader.swift:41-46；GlystrataApp.swift:230-232；plan 3-3 未列此畫面比對（亮色 28 還沒比對） |

## 5. 尋找與取代
| # | 項目 | 狀態 | 證據／備註 |
|---|---|---|---|
| 5.1 | ⌘F 開非模態視窗；已開著則啟用既有視窗 | OK-tested | plan 3-2（⌘F 開啟）；AuxiliaryWindows.swift:41-45（同 id 只前置）；快速鍵見 18.13 |
| 5.2 | Ctrl+H 開同一視窗並聚焦「取代為」 | DIFF-decided | Mac 為 ⌥⌘F（AppCommands.swift:81-82；FindReplaceSession.swift:40）。dec #5、plan §1 |
| 5.3 | 開視窗時編輯區選取文字帶入搜尋欄 | OK-code | AppModel+FindReplace.swift:10-14；FindReplaceSession.swift:35-38 |
| 5.4 | 搜尋不分大小寫（CurrentCultureIgnoreCase） | OK-tested | T/TextSearchTests ignoresCaseByDefault；TextSearch.swift:13（NSString caseInsensitive，只近似 .NET 文化比較，見 dec §4-2 假設 2，未經確認） |
| 5.5 | 純文字比對，無正規表示式／全字 | OK-code | TextSearch.swift:7-30 |
| 5.6 | 搜尋欄 Enter 找下一個、Shift+Enter 上一個、Esc 關閉 | OK-code | FindReplaceView.swift:17-19、40-42；dec §4-2 說 Esc 未實測 |
| 5.7 | 找到時選取並捲到可見、狀態列「第 x 項，共 y 項」 | OK-code | FindReplaceSession.swift:109-117；T/TextSearchTests（索引邏輯） |
| 5.8 | 找不到顯示「找不到符合項目」 | OK-code | FindReplaceSession.swift:62-64 |
| 5.9 | 「取代」取代目前選取並跳下一個；取代欄可留空 | OK-tested | plan 3-2（「取代」只換目前這一個）；T/TextSearchTests replacingOneMovesCaretPastReplacement、replacementCanBeEmptyOrContainTheQuery；rev B22 已修 |
| 5.10 | 取代欄 Enter 等同「取代」 | OK-code | FindReplaceView.swift:22 |
| 5.11 | 「全部取代」顯示「已取代 N 處」，搜尋欄留空不執行 | OK-tested | plan 3-2；T/TextSearchTests replaceAllCountsAndRewritesEveryMatch、replaceAllWithNoMatchOrEmptyQueryIsNil；FindReplaceSession.swift:96-107 |
| 5.12 | 對象是目前作用中檢視，切換分頁後重算 | OK-tested | plan 3-5（尋找取代作用在前面那個漂浮視窗的文件）；FindReplaceSession.swift:56 |
| 5.13 | 「全部取代」的 undo（待實機確認） | OK-tested | plan 3-2：復原一次整批還原；dec §4-2「需要使用者決定」#1 提到 Mac 與 Windows（整份重設）不同，未見最終確認 |

## 6. TAB 與窗格分割
| # | 項目 | 狀態 | 證據／備註 |
|---|---|---|---|
| 6.1 | 標題顯示檔名，未儲存加「 •」，未命名顯示「未命名」 | OK-code | AppModel.swift:201-204 |
| 6.2 | 標題最長 220 超過省略號、懸停顯示完整路徑 | OK-code | PaneView.swift:79-84 |
| 6.3 | 單列橫排、可橫向捲動、滾輪捲動、選取分頁自動捲入可視範圍 | MISSING | 橫向 ScrollView 有（PaneView.swift:20-26），但全 App 沒有 scrollTo／ScrollViewReader（grep App 只有 SnapshotHistoryView:14,33），選取分頁不會自動捲入。plan 3-3 只記錄「Mac 不顯示捲軸」，沒記錄自動捲入 |
| 6.4 | 分頁順序：群組色條、檔名、眼睛、× | OK-code | PaneView.swift:71-104；plan 3-3 已一致 |
| 6.5 | 點分頁切換；點編輯區也讓分頁成為作用中 | OK-tested | plan 3-2（點 TAB 切換）；EditorView onFocus→activate（PaneView.swift:37-42） |
| 6.6 | 分頁右鍵選單（順序）：關閉、閱讀器、在資料夾顯示、在新窗格開啟▸、移動到群組、快照紀錄、立即建立快照、重新命名 | DIFF-decided | 項目齊全（plan 3-2「八個項目都在」），差異：在 Finder 顯示（3-2 Cloud 決定）、「移動到群組」改子選單（dec #12）、多一項「在獨立視窗開啟／移回主視窗」（plan 3-5）。PaneView.swift:147-195 |
| 6.7 | 「在新窗格開啟」左右／上下，同一份文件開新檢視 | OK-code | AppModel+Groups.swift:167-175；漂浮視窗內停用（PaneView.swift:177，plan 3-5） |
| 6.8 | 左右／上下分割：多分頁則最後一個移到新窗格；單分頁開第二檢視 | OK-tested | plan 3-2（左右分割）、3-3（分割時搬走最後一個 TAB 已一致）；AppModel.swift:364-381 |
| 6.9 | 分割線可拖曳，比例 10%–90% | OK-tested | T/DocumentTests splittingPutsTheNewPaneBesideTheOldOne（0.99→0.9）；plan 3-1（拖曳調整）、MainWindowView.swift:100-118 |
| 6.10 | 可巢狀分割 | OK-tested | 同上（twice 巢狀） |
| 6.11 | 關閉分頁後窗格已空則自動收合 | OK-tested | plan 3-1、3-5（窗格關閉重開還原）；AppModel.swift:355-358 |
| 6.12 | 關閉目前窗格：分頁移到相鄰窗格；只剩一個無效 | OK-tested | T/DocumentTests removingAPaneCollapsesItsSplit；AppModel.swift:388-412。分頁併入「第一個剩餘窗格」而非相鄰（照 Windows，dec §3-2 假設）；最後一個窗格無效：AppModel.swift:395 |
| 6.13 | 還原單一窗格：所有分頁合併、不關文件 | OK-tested | plan 3-5；T/DocumentTests（collapseToSinglePane 保留第一窗格）；AppModel.swift:414-433 |
| 6.14 | 關閉某分頁不影響同文件其他檢視 | OK-code | AppModel.swift:345-351；DocumentManager.swift:85-94 |
| 6.15 | 無檢視後釋放監看、計時器、事件訂閱 | OK-code | AppModel.swift:348-350→detach（AppModel+Workspace.swift:218-225）；Mac 用輪詢無 watcher |
| 6.16 | 關閉後切換到同窗格其他分頁；窗格空則到其他窗格 | OK-code | AppModel.swift:352-358（選第一個，plan 3-3 照 Windows 改） |
| 6.17 | 每個檢視各自保存游標與捲動並在切換後恢復 | OK-code | EditorView.swift:157-175、177-183（.id(selected.viewID) 重建時還原）；plan 3-5 還原位置 |
| 6.18 | 同窗格拖曳排序，插入線 | OK-tested | plan 3-4（拖曳 TAB 排序，插入線跟著移動、工作階段順序跟著變） |
| 6.19 | 拖曳時被拖分頁變淡，放開後恢復 | OK-tested | plan 3-4 |
| 6.20 | 拖到不同窗格的分頁上不移動 | OK-code | AppModel+Groups.swift:152（同窗格才處理）；Windows 同（rev §3 S1 決定不做） |
| 6.21 | 拖分頁到群組：歸入該群組，整列外框標示 | OK-tested | plan 3-4（把 TAB 拖到另一個群組）；SidebarAndStatusBar.swift:125-146 |
| 6.22 | 拖到原本群組或其他窗格的分頁：不顯示提示、無變化 | OK-code | SidebarAndStatusBar.swift:129、PaneView.swift:139-143 |
| 6.23 | 「所有文件」模式每個分頁仍顯示群組色條 | OK-tested | plan 3-5（群組色條）；PaneView.swift:210-215 |

## 7. 虛擬群組
| # | 項目 | 狀態 | 證據／備註 |
|---|---|---|---|
| 7.1 | 側邊欄標題「群組」，右側「＋」提示「新增群組」 | OK-code | SidebarAndStatusBar.swift:11-19；plan 3-3 版面已一致 |
| 7.2 | 「所有文件」列，選取時填主題作用中色 | OK-code | SidebarAndStatusBar.swift:24-33 |
| 7.3 | 群組平面列表，不展開 | OK-code | SidebarAndStatusBar.swift:35-41 |
| 7.4 | 點＋／選單新增群組，名稱留空或取消不建立 | OK-tested | plan 3-2（側欄＋與 ⇧⌘N）；AppModel+Groups.swift:33-41（空白名稱 return） |
| 7.5 | 點群組只顯示該群組分頁，「所有文件」解除 | OK-tested | plan 3-3（03、04 截圖比對）；AppModel.swift:164-166 |
| 7.6 | 選取底色＝群組色與側邊欄底色混色，濃度預設 14% | OK-code | SidebarAndStatusBar.swift:117-123；plan 3-3 已一致 |
| 7.7 | 無群組色的群組選取底色同「所有文件」 | OK-code | SidebarAndStatusBar.swift:119-121（rev B8 決定與 Windows 相同） |
| 7.8 | 有群組色名稱左側 3px 色條，無色保留空間 | OK-code | SidebarAndStatusBar.swift:211-217；plan 3-3（色條 x=31） |
| 7.9 | 群組右鍵選單：開啟檔案…、群組顏色▸、重新命名、刪除群組 | OK-code | SidebarAndStatusBar.swift:148-180；plan 3-3 右鍵選單已一致；plan 3-2 說群組右鍵需手測，無手測記錄 |
| 7.10 | 群組「開啟檔案…」多選，加入群組並開啟，不切換篩選 | OK-code | AppModel+Groups.swift:78-87、105-111（不改 selectedGroupID）；篩選不同時分頁可能隱藏，與 Windows OpenFilesIntoGroup 相同。沒有 Mac 手測記錄 |
| 7.11 | 群組顏色子選單：色票（色碼＋色塊）、打勾、無顏色、管理顏色… | OK-code | SidebarAndStatusBar.swift:152-173 |
| 7.12 | 選色後立即套用、寫入 groups.json | OK-code | AppModel+Groups.swift:72-76；plan 3-2 說顏色需手測，3-5 只確認色條 |
| 7.13 | 重新命名預填原名、去頭尾空白、留空不套用 | OK-code | AppModel+Groups.swift:43-51；GroupManager.swift:19-27 |
| 7.14 | 刪除前確認（顯示群組名、按鈕「刪除」） | OK-code | AppModel+Groups.swift:54 |
| 7.15 | 刪除後分頁仍開、只失去群組關聯，檔案不受影響 | OK-code | AppModel+Groups.swift:59-62 |
| 7.16 | 刪除目前篩選的群組→改為「所有文件」 | OK-code | AppModel+Groups.swift:63-65 |
| 7.17 | 刪除最後一個群組自動建立預設「群組」 | OK-code | AppModel+Groups.swift:66-68 |
| 7.18 | 拖曳群組列重新排序，依方向決定位置並顯示插入線 | OK-tested | plan 3-4（拖曳群組排序）；SidebarAndStatusBar.swift:134-144 |
| 7.19 | 「移動到群組」跳選擇清單；來源移除、目標加入 | DIFF-decided | Mac 為子選單直接列出群組（PaneView.swift:178-184，dec #12）；歸屬更新 plan 3-2 已測；AppModel+Groups.swift:113-127 |
| 7.20 | 未命名文件移動到群組只更新分頁歸屬，不寫群組項目 | OK-code | AppModel+Groups.swift:120（有路徑才寫 items） |
| 7.21 | 同檔可屬多群組；同群組不重複 | OK-tested | T/GroupAndPersistenceTests groupsHoldLinksNotFiles；GroupManager.swift:88-90 |
| 7.22 | 移除關聯／刪群組不刪除或搬移實體檔案 | OK-code | GroupManager 只改清單；AppModel+Groups.swift:53-70 |
| 7.23 | 新分頁歸入目前選取群組；「所有文件」時依作用中分頁；仍無則第一個 | OK-code | 開檔走 currentGroupID（AppModel+Workspace.swift:140-148）；⌘N 只用 selectedGroupID（AppModel.swift:240），與 Windows NewUntitled（MainWindow.xaml.cs:1481）相同 |

## 8. Markdown 閱讀器
| # | 項目 | 狀態 | 證據／備註 |
|---|---|---|---|
| 8.1 | 眼睛／右鍵開獨立閱讀器；再按一次關閉 | OK-tested | plan 3-5（閱讀器 8 項）；AppModel+Reader.swift:10-34 |
| 8.2 | 開啟時眼睛變強調色且不透明，手動關閉後恢復 | OK-code | PaneView.swift:94-95；AppModel+Reader.swift:30（onClose 移除） |
| 8.3 | 可同時開多個閱讀器，各綁定開啟時的檢視 | OK-tested | plan 3-5（兩個閱讀器各自縮放與移動）；readerWindowID 以 viewID 為鍵（AppModel+Reader.swift:53-55） |
| 8.4 | 編輯後約 250 ms 防抖更新、背景解析、過期結果丟棄 | OK-code | ReaderView.swift:128-158（sleep 250ms、Task.isCancelled、detached 解析）；首次渲染不延遲 |
| 8.5 | YAML 不提供閱讀器：眼睛停用、提示、快速鍵不作用 | OK-code | AppModel+Reader.swift:11；PaneView.swift:86、97、206-208；GlystrataApp.swift:220 |
| 8.6 | 閱讀器文字可選取、全選、複製、右鍵（複製、全選），不能編輯 | OK-code | ReaderView.swift:186-187、285-291 |
| 8.7 | 縮放 50–200%；⌘＋滾輪每格 10%；底部滑桿；雙擊百分比回 100% | OK-code | ReaderView.swift:59-81、94-96、293-322；plan 3-2 說 ⌘＋滾輪與觸控板縮放需手測，無手測記錄 |
| 8.8 | 每個閱讀器縮放各自獨立，新開從 100%，不改編輯區字級 | OK-tested | plan 3-5（兩個閱讀器各自縮放）；ReaderView.swift:19、83-92 |
| 8.9 | 標題字級、行距、段距由偏好設定控制 | OK-code | ReaderView.swift:153（typography 傳入 renderer）；RefreshKey 含 typography（:38-42） |
| 8.10 | 閱讀器內文／標題／連結／引用／程式碼顏色亮暗各自設定 | OK-code | ReaderView.swift:98-100；Persistence/AppSettings.swift:90-104 |
| 8.11 | 支援：標題、段落、粗斜刪、連結、清單、任務清單、引用、表格、行內碼、程式碼區塊、水平線、自動連結 | OK-tested | T/MarkdownPreviewTests parsesTheBasics…、blockStructure、bareURLsBecomeLinks、readerSampleDocumentParses；plan 3-3（逐項與 Windows 截圖一致） |
| 8.12 | 任務清單 `[ ]`／`[x]` 畫成方框取代圓點；有序清單方框在編號後 | OK-tested | plan 3-3（核取方塊已一致）；ReaderRenderer.swift:219-249、551-554 |
| 8.13 | 只有項目開頭的 `[ ]`／`[x]` 才是核取方塊 | OK-code | 由 cmark-gfm tasklist 判斷（MarkdownPreviewService.swift，isChecked）；無專門測試案例 |
| 8.14 | 清單項目與子清單間不多空行；鬆散清單保留段距 | OK-tested | T/MarkdownPreviewTests blankLinesBetweenItemsMakeAListLoose；ReaderRenderer.swift:224 |
| 8.15 | 單獨 `===`／`---` 不使上一段成標題（停用 Setext） | OK-tested | T/MarkdownPreviewTests setextUnderlinesDoNotMakeHeadings；plan 3-3（文字下方緊接 `---` 顯示為文字） |
| 8.16 | 原始 HTML 不渲染、不執行 JS、不用瀏覽器元件 | OK-tested | T/MarkdownPreviewTests parsesTheBasics（script 顯示為文字）、inlineHTMLIsShownAsTyped；ReaderRenderer 用 NSTextView |
| 8.17 | YAML front matter 不顯示在正文 | OK-tested | T/MarkdownPreviewTests parsesTheBasics…、frontMatterNeedsAClosingFence |
| 8.18 | 閱讀器 emoji 為 Twemoji 彩色圖檔 | DIFF-decided | Mac 用系統 Apple 彩色 emoji，不帶 Twemoji。plan §1「閱讀器的 emoji」 |
| 8.19 | 相對路徑圖片以文件目錄為基準，超出顯示「圖片路徑必須位於文件目錄內。」 | OK-tested | T/MarkdownPreviewTests imagesResolveOnlyInsideTheDocumentFolder |
| 8.20 | 找不到圖片顯示替代文字與「找不到圖片」，不使閱讀器失敗 | OK-tested | 同上（errorMessage）；ReaderRenderer.swift:439-440；plan 3-3（缺圖提示已一致） |
| 8.21 | 網路圖片不下載，顯示「預覽只載入本機相對圖片。」 | OK-tested | imagesResolveOnlyInsideTheDocumentFolder（https 案例）；全 App 無 URLSession |
| 8.22 | 圖片路徑含 `&amp;` 等實體仍找得到檔案 | UNSURE | 沒有對應測試；Mac 靠 swift-markdown/cmark 在解析連結目的地時已解碼實體（MarkdownPreviewService.swift:217 附近取 destination），但我沒執行驗證 |
| 8.23 | 連結只處理 http／https／mailto，交給系統開啟 | OK-code | ReaderView.swift:267-273（NSWorkspace.open）；plan 3-5 未列連結實測 |
| 8.24 | 編輯、切主題、切語言、改閱讀器設定後所有閱讀器即時更新 | OK-tested | plan 3-5（主題與語言即時更新）；ReaderView.swift:37-45（RefreshKey 含 text/typography/palette/theme/language） |
| 8.25 | 閱讀器標題與縮放提示隨語言切換 | OK-code | ReaderView.swift:46-48、67、72（onChange(of: title)） |

## 9. 快照與比較
| # | 項目 | 狀態 | 證據／備註 |
|---|---|---|---|
| 9.1 | 快照存在同目錄 `<原檔名>.gss`，JSON，預設可見 | OK-tested | T/SnapshotTests snapshotLifecycle（`snapshot.md.gss`、非隱藏）；SnapshotSidecarStore.swift:21-23 |
| 9.2 | 「隱藏快照檔案」設隱藏／取消恢復；套用時既有快照一併轉換 | OK-tested | T/SnapshotTests hiddenSettingHidesAndUnhidesTheSidecar；掃描 AppModel+Workspace.swift:227-238、AppModel+Settings.swift:32-34；做法（isHidden 旗標）plan §1 已定 |
| 9.3 | 只有已有路徑的文件建立快照 | OK-code | SnapshotService.swift:18-20；AppModel+Groups.swift:184-187 |
| 9.4 | 自動快照：停止編輯達間隔（預設 5 分鐘，1–120）建立；30 秒計時 | OK-code | AppModel+Workspace.swift:320-323、339-352；無測試／實測記錄（plan 3-2 只測「立即建立快照」） |
| 9.5 | 自動快照只在內容與最新快照不同時建立 | OK-tested | T/SnapshotTests snapshotLifecycle（第二次 create 為 nil）；SnapshotService.swift:39-41、AppModel+Workspace.swift:346 |
| 9.6 | 每檔上限（預設 20，1–200），超過刪最舊 | OK-tested | snapshotLifecycle（上限 2）；SnapshotService.swift:22、44-46 |
| 9.7 | 「立即建立快照」：相同顯示「內容與最新快照相同…」，否則「已建立快照」 | OK-tested | plan 3-2（立即建立快照有產生 .gss）；AppModel+Groups.swift:183-195 |
| 9.8 | 立即建立快照不跳註解框，註解預設空白 | OK-tested | snapshotLifecycle（`listed[0].note == ""`）；AppModel+Groups.swift:190 |
| 9.9 | 快照紀錄視窗，依建立時間新到舊 | OK-tested | snapshotLifecycle（`["第四版","第三版"]`）；SnapshotSidecarStore.swift:68-72 |
| 9.10 | 每列格式「yyyy-MM-dd HH:mm:ss · N chars · 註解」，本地時間，UTF-16 長度 | OK-tested | T/SnapshotPresentationTests timestampUsesTheGivenTimeZone、headerCountsUtf16UnitsWithGrouping；SnapshotHistoryView.swift:87 |
| 9.11 | 註解同列單行可編輯，灰提示「加上註解…」，Enter／離開時儲存，Enter 不觸發視窗按鈕 | OK-code | SnapshotHistoryView.swift:90-117、SnapshotHistoryModel.swift:58-76；plan 3-5 未列註解編輯實測 |
| 9.12 | 關閉視窗時尚未儲存的註解先儲存 | OK-code | AppModel+Snapshots.swift:23（onClose→saveAllNotes）；dec §4-3 假設 7 說明有極小競態 |
| 9.13 | 註解換行在列表中換成空白 | OK-tested | T/SnapshotPresentationTests multiLineNoteIsOnlyRewrittenWhenTheFieldWasEdited；rev B7 |
| 9.14 | 「比較」開差異視窗，逐行標示新增、刪除、未變更 | OK-tested | plan 3-5（快照建立、紀錄、比較）。實際是單欄統一差異，沒有左右並排（Windows 也是，dec §4-3 假設 1）。DiffViews.swift:16-54 |
| 9.15 | 差異視窗下拉可換快照；與紀錄列表選取同步 | OK-code | DiffViews.swift:79-83、共用 SnapshotHistoryModel.selectedID；無手測記錄 |
| 9.16 | 再按「比較」沿用已開差異視窗；新建立的快照會出現在選單 | OK-code | AppModel+Snapshots.swift:85-95（同 id 前置）、SnapshotHistoryModel.swift:98-105（compareRequests 重算） |
| 9.17 | 差異視窗標題「比較 — 時間」 | OK-code | AppModel+Snapshots.swift:111-113；DiffViews.swift:100-102 |
| 9.18 | 逐行 LCS，新增／刪除低對比底色，切主題同步 | OK-tested | T/DocumentTests marksRemovedAndAddedLines；DiffViews.swift:7-12（色值取自 Windows）；SwiftUI 環境主題自動更新 |
| 9.19 | 「回復」對話框三按鈕（取代目前內容／另存為新檔案／取消） | OK-tested | plan 3-5（快照回復／取代目前內容）；SnapshotHistoryModel.swift:107-127 |
| 9.20 | 「取代目前內容」分頁標未儲存，不排程自動儲存 | OK-tested | plan 3-5（回復後不會自動儲存：Cloud 決定維持現狀）；AppModel+Snapshots.swift:33-38 |
| 9.21 | 「另存為新檔案」預設 `restored.md`，成功開新分頁，失敗不留新分頁 | OK-code | AppModel+Snapshots.swift:40-64；plan 3-5 只記取代目前內容的實測 |
| 9.22 | 「刪除選定」確認後只刪該筆 | OK-code | SnapshotHistoryModel.swift:129-148 |
| 9.23 | 「刪除整個快照檔案」紅字、單獨在左、確認後刪 `.gss`，清單空時停用 | OK-code | SnapshotHistoryView.swift:39-43；SnapshotHistoryModel.swift:150-169；T/SnapshotTests deletingTheLastSnapshotRemovesTheSidecar 驗證檔案移除 |
| 9.24 | 清單為空顯示「目前沒有快照」，比較／回復／刪除停用 | OK-code | SnapshotHistoryView.swift:17-22、53-61 |
| 9.25 | 建立、刪除、註解寫入同一把鎖，不互相覆蓋 | OK-code | SnapshotSidecarStore.swift:8、82-100（actor＋mutate）；無並行測試 |
| 9.26 | 快照檔損毀顯示「快照檔案格式損壞。」並不覆寫 | OK-tested | T/SnapshotTests corruptSidecarIsReportedNotSilentlyReplaced；SnapshotSidecarStore.swift:170-174 |
| 9.27 | 快照存完整文字＋編碼＋換行，回復時還原兩項 | OK-tested | T/SnapshotTests sidecarWrittenByTheWindowsBuildOpensAndSurvivesARewrite、sidecarRewrittenByTheWindowsBuildOpensHere；AppModel+Snapshots.swift:35、51 |

## 10. 自動儲存
| # | 項目 | 狀態 | 證據／備註 |
|---|---|---|---|
| 10.1 | 有路徑文件停止打字約 1 秒後自動存檔 | OK-tested | plan 3-2（自動儲存寫回檔案）；AppModel+Workspace.swift:284-286 |
| 10.2 | 未命名文件不自動存檔 | OK-code | AppModel+Workspace.swift:278-280 |
| 10.3 | 內容沒修改不寫入 | OK-code | AppModel+Workspace.swift:290-292 |
| 10.4 | 自動存檔成功顯示「已自動儲存」，「•」消失 | OK-tested | plan 3-2（自動儲存、狀態列恢復）；AppModel+Workspace.swift:305-308 |
| 10.5 | 自動存檔失敗：跳「儲存檔案失敗」＋原因，保留內容與未儲存標記 | OK-tested | plan 3-2（唯讀資料夾，只跳一次、標記保留）；rev B16（每次停止輸入不再重複跳出，與 Windows 有意不同） |
| 10.6 | 內容變動後約 700 ms 寫入工作階段 JSON | OK-code | AppModel+Workspace.swift:152-165（700ms）；plan 3-2 驗證 quit 時有存檔 |

## 11. 設定（偏好設定）
| # | 項目 | 狀態 | 證據／備註 |
|---|---|---|---|
| 11.1 | 入口在「檔案」選單最底部「偏好設定…」（Ctrl+,） | DIFF-decided | Mac 放 App 選單、⌘,（GlystrataApp.swift:129-131）。plan 3-1、§1 快捷鍵 |
| 11.2 | 五個分頁：一般、編輯器選項、閱讀器選項、群組顏色、快照 | OK-code | SettingsView.swift:26-32 五個。外觀用分段選擇器取代 TabControl：plan 3-3「待 Cloud 決定外觀取捨」，dec §4-1「需要使用者決定」#2，尚無定案記錄 |
| 11.3 | 按鈕列左：還原預設／匯出／匯入，右：取消／套用 | OK-code | SettingsView.swift:48-67（Return=套用、Esc=取消，Mac 慣例，dec §4-1 #3） |
| 11.4 | 「取消」關閉視窗、不寫入任何變更 | OK-code | SettingsView.swift:59-62；SettingsForm 為工作複本 |
| 11.5 | 「套用」先驗證數字與顏色，失敗顯示對應訊息並留在視窗 | OK-tested | T/SettingsFormTests validation；SettingsView.swift:81-96 |
| 11.6 | 「套用」寫入 settings.json、關閉視窗並立即套用語言／主題／配色／快照設定 | OK-tested | plan 3-2（套用後工具列立刻出現、設定檔有寫入）；AppModel+Settings.swift:21-41 |
| 11.7 | 「還原預設」只重設對話框內容，需按套用才生效 | OK-code | SettingsView.swift:49-51（form.replace(with: AppSettings())） |
| 11.8 | 一般分頁：語言、主題、快捷列（預設關）、字元計數方式 | OK-code | SettingsTabs.swift:13-57；另有 Mac 專屬「圖示使用玻璃質感」（plan 3-4 Cloud 決定） |
| 11.9 | 編輯器選項：亮暗各 11 色，文字框＋「選擇顏色」（HSV 選擇器），頂端顯示「目前編輯：…」 | DIFF-open | 11 色與說明列 OK（SettingsTabs.swift:74-87、60-72；SettingsForm.swift:45），但用系統 ColorPicker＋#RRGGBB 欄位取代自製 HSV 對話框。plan 3-3 列為「待 Cloud 決定外觀取捨：選色對話框」，dec §4-1「需要使用者決定」#1，尚無最終決定 |
| 11.10 | 切換主題下拉時，未套用的顏色輸入先寫回原主題 | OK-tested | T/SettingsFormTests themeSwitch、systemToSameTheme |
| 11.11 | 閱讀器選項：5 組配色亮暗各一＋H1–H6 字級、行距、段距、標題間距 | OK-code | SettingsTabs.swift:89-119（合併在同一分頁） |
| 11.12 | 群組顏色分頁：色塊、色碼、「…」HSV 選色、移除；新增顏色；底色濃度 0–50% 預設 14% | DIFF-open | 列出、移除（至少留一個，T/SettingsFormTests groupColors）、新增、濃度滑桿 OK（SettingsTabs.swift:121-161）；「…」HSV 選色改為系統 ColorPicker，同 11.9 未定案 |
| 11.13 | 套用時群組依色票位置自動換色，被刪除色票上的群組保留原色 | OK-tested | T/GroupAndPersistenceTests paletteEditsCarryOverByPosition；AppModel+Settings.swift:27 |
| 11.14 | 快照分頁：間隔 1–120、上限 1–200、隱藏快照檔；超出範圍夾回 | OK-tested | T/SettingsFormTests clamping、GroupAndPersistenceTests outOfRangeSettingsAreClampedAndHandEditedFilesLoad；SettingsTabs.swift:163-189 |
| 11.15 | 匯出另存 `glystrata-settings.gss`，匯出前先驗證，失敗不匯出 | OK-code | SettingsView.swift:102-122（預設檔名正確，先 validated()）；沒設 Windows 的篩選名稱「Glystrata 設定檔」，只用 .gss 型別；面板未手測（dec §4-1） |
| 11.16 | 匯出 JSON 含整份設定，最外層結尾加 `"kind": "glystrata-settings"` | DIFF-decided | 內容與 kind OK（T/SettingsFormTests exportImport），但 sortedKeys 使 kind 不在結尾（SettingsFile.swift:24-28），另多一個 Mac 專屬欄位 useGlassIcon。checklist 23-C #22 自己寫「只影響外觀…可以忽略」 |
| 11.17 | 匯出內容不含群組、工作階段、最近檔案 | OK-code | SettingsFile.swift:24-28 只序列化 AppSettings |
| 11.18 | 匯入只把值填進對話框，取消可放棄 | OK-code | SettingsView.swift:146-152 |
| 11.19 | 匯入含 snapshots／notice 或 kind 不對 → 「這不是 Glystrata 的設定檔…」 | OK-tested | T/SettingsFormTests importRejects；SettingsFile.swift:37-47 |
| 11.20 | schemaVersion>1 → 太新；JSON 損壞 → 格式不正確 | OK-tested | importRejects；SettingsFile.swift:33-35、49-56 |
| 11.21 | 「還原預設／匯出／匯入」沿用同配置、系統原生檔案對話框 | OK-code | SettingsView.swift:48-57、108-131（NSSavePanel/NSOpenPanel） |

## 12. 主題與語言
| # | 項目 | 狀態 | 證據／備註 |
|---|---|---|---|
| 12.1 | 主題三選項，預設跟隨系統；舊檔 0=亮、1=暗維持 | OK-tested | T/GroupAndPersistenceTests themeNumbersKeepTheirMeaning（0/1/缺欄位/99） |
| 12.2 | 跟隨系統時，系統切換外觀 App 立即跟著變 | OK-code | MainWindowView.swift:12、33 與 ThemedRoot 讀 colorScheme；plan 3-4 說明「App 開著時切換深淺色的情況沒有實測」 |
| 12.3 | 啟動時依系統外觀決定主題，不先閃亮色 | OK-tested | plan 3-4（系統深色時啟動是深色，強制淺色時啟動是淺色） |
| 12.4 | 亮暗切換時所有視窗、側欄、分頁列、編輯區、狀態列、選單、對話框、閱讀器、差異、快照視窗同步 | OK-tested | plan 3-5（漂浮視窗開著時切主題）；AuxiliaryWindows.swift:152-166（ThemedRoot）。選單、NSAlert 由系統繪製（plan 3-3），強制主題與系統不同時 NSAlert 不一定跟著 App 主題，沒有記錄 |
| 12.5 | 亮暗語法配色、閱讀器配色分開保存，重啟仍正確 | OK-tested | T/GroupAndPersistenceTests settingsRoundTrip；AppSettings.swift:151-154 |
| 12.6 | 語言兩選項，套用後立即切換含選單、側欄、對話框、閱讀器、快照視窗 | OK-tested | plan 3-5（開著時切語言）；頂層系統選單名稱跟系統語言（dec #9 已定） |
| 12.7 | 語言切換後選單 Alt 助憶鍵底線改變 | N/A | [Windows 專屬]；Mac 選單無助憶鍵（LocalizationTable.swift:1-2 已去除，T/InterfaceLogicTests menuTitlesCarryNoMnemonics…） |
| 12.8 | 未命名文件名稱隨語言切換 | OK-code | AppModel.swift:202（localization.get("document.untitled")） |
| 12.9 | 全新安裝預設語言為繁體中文，不依系統語言 | OK-code | AppSettings.swift:149（預設 traditionalChinese）；rev §3 S3 這一輪不做 |
| 12.10 | 「關於」顯示 `Glystrata 1.2.3` 與「輕量化的文字編輯器，支援語法渲染。」 | OK-code | AppModel+Windows.swift:7-14（版本取自 CFBundleShortVersionString，project.yml MARKETING_VERSION 1.2.3）；LocalizationService.swift 的 about.message |

## 13. 最近開啟過的文件
| # | 項目 | 狀態 | 證據／備註 |
|---|---|---|---|
| 13.1 | 子選單最多 10 筆，最新在前 | OK-tested | T/GroupAndPersistenceTests newestFirstDeduplicatedAndCapped；RecentFilesService.swift:11、17-28 |
| 13.2 | 項目文字為檔名（底線跳脫），懸停顯示完整路徑 | UNSURE | 檔名 OK（GlystrataApp.swift:142）；底線跳脫在 Mac 不需要；懸停路徑用 SwiftUI `.help`（:145），我不確定原生選單列項目是否顯示 tooltip，沒有記錄 |
| 13.3 | 檔案已不存在的項目停用（灰色） | OK-code | GlystrataApp.swift:146 |
| 13.4 | 清單為空顯示停用「（無）」 | OK-code | GlystrataApp.swift:138-139 |
| 13.5 | 點清單檔案開啟（加入目前選取的群組） | OK-code | GlystrataApp.swift:143→AppModel.swift:269-271（sourceGroupID=selectedGroupID，與 Windows MainWindow.xaml.cs:745 相同） |
| 13.6 | 「清除最近開啟紀錄」清空 | OK-code | GlystrataApp.swift:149-152；T/GroupAndPersistenceTests newestFirst…（clear） |
| 13.7 | 所有開檔入口都記錄到最近清單（含回復另存） | DIFF-decided | 開檔、拖放、Finder、群組開檔、另存新檔（AppModel.swift:320）、工作階段還原（AppModel+Workspace.swift:62）都記；「回復另存」不記（AppModel+Snapshots.swift:40-64）。Windows RestoreSnapshot（MainWindow.xaml.cs:1908-1946）同樣不記，dec §4-3 假設 5「沿用 Windows」；清單條目與 Windows 實際程式不符 |
| 13.8 | 同檔不同寫法視為同一筆、重複開啟移到最前 | OK-tested | newestFirstDeduplicatedAndCapped；RecentFilesService.swift:22-23 |
| 13.9 | 工作階段還原時以「補入」加到最後，滿 10 筆不補 | OK-tested | T/GroupAndPersistenceTests seedingAppendsAndSurvivesARoundTrip；RecentFilesService.swift:34-45 |
| 13.10 | 開啟失敗時跳錯誤並自動從清單移除 | OK-code | AppModel.swift:280-286 |
| 13.11 | 最近清單存於獨立 recent.json，不隨偏好設定匯出 | OK-code | JSONStateStore.swift:55-63；SettingsFile.swift:24-28 |
| 13.12 | 重新命名後清單中的舊路徑不會自動更新（待決） | DIFF-decided | Mac 改名時把舊路徑換成新路徑（AppModel+Groups.swift:238-239）。rev B17；Windows 之後也修（MainWindow.xaml.cs:1900 `_recent.Rename`） |

## 14. 工作階段還原
| # | 項目 | 狀態 | 證據／備註 |
|---|---|---|---|
| 14.1 | 離開時保存群組、窗格版面與比例、分頁、游標與捲動、作用中分頁、設定、最近清單 | OK-tested | plan 3-2（結束時工作階段有存檔）；AppModel+Workspace.swift:167-208、490-496；T/GroupAndPersistenceTests sessionRoundTrip、layoutSurvivesARoundTrip |
| 14.2 | 重啟後群組、巢狀版面、分頁順序、作用中分頁都還原 | OK-tested | plan 3-5（窗格分割、關閉、還原單一窗格與重開還原版面）；AppModel+Workspace.swift:9-70 |
| 14.3 | 還原游標與捲動，超出文字長度夾到範圍內 | OK-code | EditorView.swift:157-175（夾 caret）；plan 3-5 只記視窗位置與版面 |
| 14.4 | 同檔兩個檢視還原後重建兩個 | OK-code | AppModel+Workspace.swift:49、51（isSecondView→newView） |
| 14.5 | 缺檔略過、其他分頁照常還原、不提示 | OK-code | AppModel+Workspace.swift:43-53 |
| 14.6 | 未命名文件不還原 | OK-code | AppModel+Workspace.swift:190（filePath != nil） |
| 14.7 | 閱讀器視窗不會在重啟後自動開啟 | OK-code | SessionState 沒有閱讀器欄位（PersistedStates.swift:76-98） |
| 14.8 | 重啟後主題與語言維持 | OK-tested | settingsRoundTrip；plan 3-4 |
| 14.9 | 作用中分頁還原；對應不到則第一個 | OK-code | AppModel+Workspace.swift:66-69、AppModel.swift:168-175 |
| 14.10 | 視窗大小與位置不保存，每次 1240×760（最小 760×500） | UNSURE | 主視窗是 SwiftUI `Window` scene（GlystrataApp.swift:11-16，defaultSize 1240×760、min 760×500），App 沒寫任何主視窗邊界保存碼，但 SwiftUI/AppKit 預設可能自動記住視窗位置與大小，與 Windows「不保存」可能不同。我沒有實測。（漂浮視窗則有記，plan 3-5） |

## 15. 拖放與外部開檔
| # | 項目 | 狀態 | 證據／備註 |
|---|---|---|---|
| 15.1 | 從 Finder 拖檔進視窗任何位置，加入目前群組並在目前窗格開成分頁 | OK-code | MainWindowView.swift:34-44、EditorView.swift:338-362（編輯區也攔截）；plan 3-2 把「從 Finder 拖進視窗」列為工具模擬不了、需親手測，無手測記錄；規則（寫進群組清單）rev §6 |
| 15.2 | 拖入資料夾或不存在路徑略過、不崩潰 | OK-code | AppModel+Groups.swift:91-96；AppModel+Workspace.swift:95 |
| 15.3 | 拖入非檔案內容（純文字）視窗不攔截 | OK-code | MainWindowView.swift:34（只收 .fileURL）；EditorView.swift:347-362（無檔案時 super） |
| 15.4 | 拖入已開著的檔案切換到既有分頁 | OK-tested | T/DocumentTests viewsOfTheSame…（openView 回傳既有）；AppModel.swift:273-279 |
| 15.5 | 檔案關聯開檔：工作區還原完成後才開，多個都開 | OK-tested | plan 3-4（App 執行中從外部開檔、App 沒開著時啟動後開檔）；AppModel+Workspace.swift:9-36、86-104 |
| 15.6 | 程式已在執行時再開檔：不另開視窗，既有視窗開分頁並帶到前景 | OK-tested | plan 3-4（等同拖到 Dock 圖示：開成 TAB，歸入目前群組）；macOS 本身單一執行個體 |
| 15.7 | 第二次啟動且沒檔案參數時，既有視窗仍被還原並拉到前景 | N/A | [Windows 專屬]；Mac 對應是點 Dock 圖示叫回主視窗：GlystrataApp.swift:59-64，plan 3-5 已測 |
| 15.8 | 路徑含空白、重複、資料夾、不存在都不崩潰 | OK-code | AppModel+Workspace.swift:92-95（trim、去重、檔案存在） |
| 15.9 | 群組／分頁的內部拖曳不被當成外部檔案拖放 | OK-tested | plan 3-4（拖曳 TAB／群組實測）；AppModel+Groups.swift:5-22（私有 UTType）、MainWindowView.swift:34（只收 fileURL） |
| 15.10 | `.gss` 在檔案總管有獨立圖示，雙擊不啟動程式（Mac 需決定註冊） | DIFF-decided | 圖示 OK（project.yml UTExportedTypeDeclarations、SnapshotFile.icns，plan 3-4）；雙擊改為開對應文件並顯示快照紀錄（Cloud 於 2026/10/14 決定，rev §6）；五種情況 plan 3-4 已測 |

## 16. 外部變更偵測
| # | 項目 | 狀態 | 證據／備註 |
|---|---|---|---|
| 16.1 | 監看所在目錄，事件後等約 500 ms 再檢查 | DIFF-decided | Mac 每 2 秒輪詢大小＋修改時間，切回 App 時也立刻比對（AppModel+Workspace.swift:324-335）。dec #13 |
| 16.2 | 比對「長度＋最後寫入時間」，本程式自己的存檔不提示 | OK-code | AppModel+Workspace.swift:383-395（lastSavedFingerprint、savesInFlight）；plan 3-3（自動儲存前先確認，rev B24） |
| 16.3 | 偵測到外部修改跳「檔案已在外部變更」，三按鈕 重新載入／比較／保留本機內容 | OK-tested | plan 3-2（外部變更選「重新載入」情境）；AppModel+Workspace.swift:422-438 |
| 16.4 | 「重新載入」以磁碟內容取代、回到未修改；（現行無未儲存警告） | DIFF-decided | 有未儲存修改時先再確認一次，取消視為保留本機（AppModel+Workspace.swift:399-404）。rev B3；plan 3-2 已測取消／重新載入兩條路 |
| 16.5 | 「比較」開差異視窗，左目前緩衝、右磁碟版本，不改內容 | OK-code | AppModel+Snapshots.swift:98-109（old=本機、new=磁碟）；dec §4-3 只做了靜態畫面（diffFixed），沒真的改檔觸發 |
| 16.6 | 「保留本機內容」或 Esc：內容不變，該外部版本不再提示 | OK-code | AppModel+Workspace.swift:393、429（Esc 對應第三顆按鈕）；rev B4 決定現行行為即定案 |
| 16.7 | 下次存檔直接覆蓋外部版本，存檔後不再詢問 | OK-code | AppModel+Workspace.swift:257-258（成功存檔清 knownExternalFingerprints）；規格要求再次提示，rev B4 決定不做 |
| 16.8 | 檔案被改名或刪除時不提示 | DIFF-decided | 不跳對話框，但狀態列顯示「檔案已被刪除或移走，儲存時會重新建立」（AppModel+Workspace.swift:370-377）。rev B13，plan 3-2 已測 |
| 16.9 | 關閉分頁後釋放監看與計時器，不再對已關閉分頁提示 | OK-code | AppModel+Workspace.swift:218-225；AppModel.swift:348-350 |

## 17. 錯誤處理與資料安全
| # | 項目 | 狀態 | 證據／備註 |
|---|---|---|---|
| 17.1 | 所有寫入採「暫存檔→取代」 | OK-code | 文件：FilePersistenceService.swift:25-35；JSON：JSONStateStore.swift:11（.atomic）；快照走同一 AtomicFileWriter（SnapshotSidecarStore.swift:141） |
| 17.2 | 文件存檔失敗內容保留、未儲存標記保留、不崩潰 | OK-tested | T/DocumentTests failedSaveKeepsTheDocumentModified；plan 3-2 |
| 17.3 | 失敗訊息「儲存失敗：（原因）」，標題「儲存檔案失敗」 | OK-code | DocumentManager.swift:148、AppModel.swift:298（error.save）。訊息固定中文不隨語言，Windows 同（DocumentManager.cs:123、FilePersistenceService.cs:27） |
| 17.4 | groups/session/recent 解析失敗改名為 `.corrupt-yyyyMMddHHmmss.json` 並以預設啟動 | OK-code | JSONStateStore.swift:80-96（四個檔共用 load）；測試只涵蓋 settings.json（GroupAndPersistenceTests corruptSettings…） |
| 17.5 | settings.json 啟動時損壞：預設啟動（現行不保留副本，待決） | DIFF-decided | Mac 啟動路徑也保留副本。rev B12；T/GroupAndPersistenceTests corruptSettingsAtStartupAlsoKeepADiagnosticCopy |
| 17.6 | 快照檔損壞讀取顯示「快照檔案格式損壞。」，原檔不覆寫 | OK-tested | T/SnapshotTests corruptSidecarIsReportedNotSilentlyReplaced |
| 17.7 | 自動快照 IO／權限錯誤靜默略過 | OK-code | AppModel+Workspace.swift:349-350（try?） |
| 17.8 | 工作階段／設定寫入失敗程式繼續 | OK-code | JSONStateStore.swift:67-74（try?）；AppModel+Workspace.swift:172-181 |
| 17.9 | 未處理的 UI 例外寫入記錄檔並顯示錯誤對話框，程式不直接結束（Mac 記錄位置待定） | MISSING | grep `errors.log`／未捕捉例外處理：mac/App、mac/Sources 皆無結果；dec §3-4 #4、23-C #4 記為待決，沒有實作 |
| 17.10 | WPF 資源配額不足（Win32 1816）處理 | N/A | [Windows 專屬] |
| 17.11 | 刪群組、移出分頁、關閉分頁、刪單筆快照都不刪原始文件；只有「刪除整個快照檔案」移除 .gss | OK-code | AppModel+Groups.swift:53-70；SnapshotSidecarStore.swift:145-150（只刪 sidecar 與舊名稱） |
| 17.12 | 清空整份內容後仍會建立快照（不因為空而跳過） | OK-code | SnapshotService.swift:36-38 只在「無歷史且為空」時跳過；T/SnapshotTests emptyDocumentWithNoHistoryIsNotSnapshotted 只驗證該跳過的情況 |
| 17.13 | 不收集遙測、不登入、不上傳 | OK-code | mac/App、mac/Sources 無 URLSession／URLRequest |
| 17.14 | 設定或快照檔未知欄位忽略；schemaVersion 過高匯入拒絕 | OK-tested | T/GroupAndPersistenceTests floatingWindowsRoundTripAndToleranceForOtherBuilds、outOfRangeSettings…；SettingsFormTests importRejects |
| 17.15 | 驗證訊息與對話框文字都走在地化，無寫死英文 | OK-code | 例外：色彩分頁固定文字 `HEX`（SettingsTabs.swift:81，rev S7 決定維持）；Core 錯誤字串固定中文（17.3）。與 Windows 相同 |

## 18. 快速鍵
| # | 項目 | 狀態 | 證據／備註 |
|---|---|---|---|
| 18.1 | 選單文字與行為由同一張命令表產生 | OK-tested | AppCommands.swift:71-102；GlystrataApp.swift:247-252（item()）；T/InterfaceLogicTests everyCommandAppearsExactlyOnce、everyLabelledCommandHasText |
| 18.2 | 重複快捷鍵記錄衝突 | OK-tested | AppCommands.swift:109-123；T/InterfaceLogicTests noTwoCommandsShareAKey、clashesAreReported、keysReservedByMacOSAreLeftAlone |
| 18.3 | 只有帶修飾鍵的組合被攔截，單鍵不攔截 | OK-code | 所有命令鍵皆含 ⌘（AppCommands.swift:72-97），走 SwiftUI keyboardShortcut |
| 18.4 | 組字中的按鍵（ImeProcessed）不攔截 | UNSURE | 交給系統輸入法與 NSTextView 處理，沒有 App 層程式；plan 3-2 明說注音輸入未手測 |
| 18.5 | Ctrl+N 新增 | OK-tested | ⌘N；T/InterfaceLogicTests shortcutsPrintTheMacWay |
| 18.6 | Ctrl+O 開啟 | OK-code | ⌘O，AppCommands.swift:73 |
| 18.7 | Ctrl+S 儲存（未命名則另存） | OK-tested | ⌘S；plan 3-5；AppModel.swift:291-294 |
| 18.8 | Ctrl+Shift+S 另存新檔 | OK-tested | ⇧⌘S；shortcutsPrintTheMacWay |
| 18.9 | Ctrl+Shift+H 快照紀錄 | OK-code | ⇧⌘H，AppCommands.swift:76 |
| 18.10 | Ctrl+Alt+S 立即建立快照 | OK-tested | ⌥⌘S；shortcutsPrintTheMacWay |
| 18.11 | Ctrl+, 偏好設定 | OK-tested | ⌘,；plan 3-2（⌘, 開偏好設定）；shortcutsPrintTheMacWay |
| 18.12 | Ctrl+Z 復原、Ctrl+Y 重做，由編輯器處理 | DIFF-decided | 復原／重做由系統 Edit 選單提供（⌘Z、⇧⌘Z），不在命令表（AppCommands.swift:70 註解）。plan §1、dec 1-1 表；plan 3-2 復原與重做已測 |
| 18.13 | Ctrl+F 尋找取代；Ctrl+H 同視窗聚焦取代欄 | DIFF-decided | ⌘F；取代改 ⌥⌘F（⌘H 是系統隱藏）。AppCommands.swift:80-82、dec #5 |
| 18.14 | Ctrl+\ 左右分割；Ctrl+Alt+\ 上下分割 | OK-tested | ⌘\、⌥⌘\；shortcutsPrintTheMacWay（⌥⌘\） |
| 18.15 | Ctrl+Shift+W 關閉窗格；Ctrl+W 關閉分頁 | OK-code | ⇧⌘W、⌘W（AppCommands.swift:86、90；GlystrataApp.swift:169-175，⌘W 取代系統「關閉視窗」） |
| 18.16 | Ctrl+Alt+1 還原單一窗格；Ctrl+Alt+N 在新檢視開啟 | OK-code | ⌥⌘1、⌥⌘N，AppCommands.swift:87-88 |
| 18.17 | Ctrl+Shift+R 開關閱讀器 | OK-code | ⇧⌘R，GlystrataApp.swift:214-220 |
| 18.18 | Ctrl+Shift+G 新增群組 | DIFF-decided | ⇧⌘N（⇧⌘G 是 Mac 的「尋找上一個」）。AppCommands.swift:96-97、dec #5；plan 3-2 已測 |
| 18.19 | Ctrl＋滾輪在編輯區與閱讀器縮放（0.1／10%） | OK-code | ⌘＋滾輪：EditorView.swift:374-407、ReaderView.swift:293-322；另有觸控板兩指縮放、⌘＋／⌘－／⌘0（dec #10 已定）；plan 3-2 說明滾輪與觸控板需手測，無手測記錄 |
| 18.20 | 選單 Alt 助憶鍵 | N/A | [Windows 專屬] |
| 18.21 | Alt+F4 關閉程式走關閉流程 | N/A | [Windows 專屬]；Mac 對應 ⌘Q 走未儲存詢問（plan 3-2 已測） |
| 18.22 | 快捷鍵不支援自訂 | OK-code | 無自訂介面 |
| 18.23 | 規劃中但未實作的快捷鍵（F3、Ctrl+Tab…；非必備） | N/A | 非必備項目。Mac 反而另做了 ⌘＋／⌘－／⌘0 縮放（dec #10 已定）；其餘未實作，與 Windows 一致 |

## 19. 狀態列、選單與版面
| # | 項目 | 狀態 | 證據／備註 |
|---|---|---|---|
| 19.1 | 主視窗預設 1240×760，最小 760×500 | OK-code | GlystrataApp.swift:14-16（是否每次回預設見 14.10 UNSURE） |
| 19.2 | 版面：側欄（預設 258，可拖曳）、編輯區、上方選單／工具列、下方狀態列 | OK-tested | MainWindowView.swift:9-28；plan 3-3（主視窗版面與側欄寬度已一致） |
| 19.3 | 選單列頂層「檔案、編輯、檢視、群組、說明」，置中、分隔線 | DIFF-decided | Mac 為原生選單列，頂層名稱跟系統語言，只有「群組」是 App 自己的（GlystrataApp.swift:236-237）。dec #9 |
| 19.4 | 「檔案」選單順序：新增、開啟、最近、儲存、另存、快照紀錄、立即建立快照、離開、偏好設定 | DIFF-decided | Mac：新增、開啟、最近、儲存、另存、關閉、快照紀錄、立即建立快照；離開＝App 選單 ⌘Q、偏好設定／關於在 App 選單（GlystrataApp.swift:125-187）。plan 3-1 |
| 19.5 | 「編輯」選單：復原、重做、尋找與取代 | OK-code | GlystrataApp.swift:189-194；Undo/Redo 與剪貼簿項目由系統提供（多出系統標準項目） |
| 19.6 | 「檢視」選單：左右分割、上下分割、關閉目前窗格、還原單一窗格、在新檢視開啟 | OK-code | GlystrataApp.swift:197-201；另有 Mac 版新增「在獨立視窗開啟／移回主視窗」、閱讀器、縮放（plan 3-5、dec #10） |
| 19.7 | 「群組」選單：新增群組、開啟檔案… | OK-code | GlystrataApp.swift:236-245 |
| 19.8 | 「說明」選單：Markdown 語法說明、關於 Glystrata | DIFF-decided | 語法說明在說明選單（GlystrataApp.swift:230-232），「關於」在 App 選單（:125-127）。plan 3-1 |
| 19.9 | 工具列由左至右：新增、開啟、左右分割、上下分割、關閉窗格、還原單一窗格；無儲存／設定 | OK-tested | MainWindowView.swift:124-145；plan 3-3 |
| 19.10 | 無分頁時中央提示「按 Ctrl+N… Ctrl+O…」 | OK-code | PaneView.swift:48；Mac 文字改為 ⌘N／⌘O（LocalizationService.swift:51-52 的 macTraditionalChinese） |
| 19.11 | 狀態列左側顯示所在資料夾，省略號截斷，懸停完整路徑 | OK-code | SidebarAndStatusBar.swift:255-260 |
| 19.12 | 狀態列右側「狀態 · 編碼 · 換行」 | OK-code | SidebarAndStatusBar.swift:299-313 |
| 19.13 | 游標位置「Ln 行, Col 欄」 | DIFF-decided | Mac 顯示「第 3 行，第 18 欄」（LocalizationService.swift:55）。rev T1、§6 |
| 19.14 | 字元統計「字元 N」，選取時追加「· 選取 M」，以文字元素計 | OK-tested | T/DocumentTests countsUserPerceivedCharacters；SidebarAndStatusBar.swift:315-327 |
| 19.15 | 計數方式依設定，懸停顯示目前模式 | OK-code | SidebarAndStatusBar.swift:265-268、333-339 |
| 19.16 | 字元統計只顯示結果，不可點擊 | OK-code | SidebarAndStatusBar.swift:265-268（無點擊處理） |
| 19.17 | 縮放滑桿 50–200%、10% 一格吸附、提示文字、雙擊百分比回 100% | OK-code | SidebarAndStatusBar.swift:273-287；plan 3-3 版面已比對；互動無單獨手測記錄 |
| 19.18 | 編輯器縮放不改字級設定，重啟回 100% | OK-code | AppModel.swift:32（editorZoom 預設 1.0，未寫入 settings／session） |

## 20. 視窗、標題列與系統整合
| # | 項目 | 狀態 | 證據／備註 |
|---|---|---|---|
| 20.1 | 自訂標題列（跟隨主題、最小化／最大化／關閉）；Mac 用原生標題列 | N/A | [Windows 專屬]；Mac 原生標題列，視窗外觀跟隨主題（ThemedRoot/preferredColorScheme） |
| 20.2 | Windows 11 Snap Layouts／Aero Snap | N/A | [Windows 專屬]；plan §1「不做 Snap Layouts」 |
| 20.3 | 自訂標題列最大化時邊緣不被裁切 | N/A | [Windows 專屬] |
| 20.4 | 所有對話框都是跟隨主題的自訂對話框 | DIFF-decided | Mac 全用 NSAlert／系統面板（AppModel.swift:448-486 等）。plan 3-3「選單與系統對話框在 Mac 上由系統繪製」。App 主題與系統不同時對話框外觀跟系統，沒有記錄 |
| 20.5 | 對話框 Esc 關閉、Enter 確定（多行文字框 Enter 不觸發確定） | UNSURE | 外部變更與快照回復的「取消」明確綁 Esc（AppModel+Workspace.swift:429、SnapshotHistoryModel.swift:118）；「尚有未儲存變更」（AppModel.swift:448-463）、單一「好」按鈕的錯誤訊息沒有明確 Esc 對應，是否靠 NSAlert 預設行為取消我無法確認，也沒有記錄 |
| 20.6 | 注音組字候選字視窗在游標處顯示 | N/A | 系統輸入法處理（NSTextView）；dec 3-4 #26 說明不做額外客製，plan 3-2 未手測 |
| 20.7 | 發布包內附授權檔與第三方聲明 | MISSING | mac/scripts/package.sh（尚未追蹤，git status `??`）只做 ditto 壓縮 .app，不含 LICENSE、THIRD-PARTY-NOTICES；Twemoji 已不使用，但 swift-markdown/cmark-gfm 的授權聲明也沒有。plan 階段 6 標「未開始」 |
| 20.8 | 版本號 1.2.3，「關於」顯示與發布檔一致 | OK-code | project.yml MARKETING_VERSION 1.2.3；AppModel+Windows.swift:8、package.sh 檔名取自 Info.plist 版本 |
| 20.9 | 不需安裝 .NET 執行環境 | N/A | [Windows 專屬]；Swift 原生 |

## 21. 既有自動驗證涵蓋範圍（Windows Program.cs 13 組 → Mac 測試）
| # | Windows 檢查組 | 狀態 | 證據／備註 |
|---|---|---|---|
| 21.1 | FileCodecVerification | OK-tested | T/DocumentTests FileCodecTests（三組編碼／換行 round-trip、UTF-16 BE 拒絕、混合換行多數決） |
| 21.2 | DocumentManagerVerification | OK-tested | DocumentManagerTests（兩檢視共用文件捲動獨立、連續重疊儲存、寫到不存在目錄失敗保持修改）；另有 editsMadeWhileSavingStayModified 等 |
| 21.3 | PaneLayoutVerification | OK-tested | PaneLayoutTests、PaneSplitTests；GroupAndPersistenceTests layoutSurvivesARoundTrip、brokenLayoutsAreRejectedAndRatiosClamped |
| 21.4 | GroupVerification | OK-tested | GroupAndPersistenceTests groupsHoldLinksNotFiles、kindIsInferred…、colourIsStoredClearedAndPersisted、paletteEditsCarryOverByPosition（含互換不連鎖、被刪色票、色盤外、無色群組、無變更回 false；大小寫不敏感用 `#3b6ff5`）。`MovePath` 也有（groupsHoldLinksNotFiles，含實體檔案仍存在） |
| 21.5 | MarkdownTypingAssistantVerification | OK-tested | TypingAssistantTests（純函式；七種 Enter 行為除「項目中間切開」外都有對應，見 4A.6） |
| 21.6 | PersistenceVerification | OK-tested | settingsRoundTrip、corruptSettingsFallBack…、themeNumbersKeepTheirMeaning（0/1/缺欄位/99） |
| 21.7 | MarkdownVerification | OK-tested | MarkdownPreviewTests parsesTheBasicsAndHidesFrontMatterAndHTML |
| 21.8 | SnapshotVerification | OK-tested | SnapshotTests snapshotLifecycle、hiddenSettingHides…、deletingTheLastSnapshotRemovesTheSidecar、legacySidecarIsMigrated（(g) 舊檔名遷移也做了） |
| 21.9 | DiffVerification | OK-tested | TextDiffTests marksRemovedAndAddedLines |
| 21.10 | TextMetricsVerification | OK-tested | TextMetricsTests countsUserPerceivedCharacters（同樣的 `Á B\n` 案例） |
| 21.11 | MarkdownFormattingVerification | OK-tested | MarkdownFormattingTests（粗體、H2、清單、有序、連結、程式碼區塊、UTF-16 位移） |
| 21.12 | ShortcutVerification | OK-tested | CommandTests（無衝突、每個命令一次、顯示文字改為 Mac 寫法、衝突被記錄、雙語都有文字）；「復原顯示 Ctrl+Z」不適用（系統提供） |
| 21.13 | RecentFilesVerification | OK-tested | GroupAndPersistenceTests newestFirstDeduplicatedAndCapped、seedingAppendsAndSurvivesARoundTrip、damagedStateStillLoads |
| 21.M1 | 未涵蓋：外部變更三條路徑與對話框 | OK-tested | plan 3-2 手測「重新載入」＋再確認；比較只有靜態畫面（dec §4-3） |
| 21.M2 | 未涵蓋：自動存檔 1 秒、快照 30 秒、工作階段 700 ms 計時 | UNSURE | 自動存檔 plan 3-2 已測；快照 30 秒計時與 700 ms 工作階段防抖沒有測試或記錄 |
| 21.M3 | 未涵蓋：工作階段還原 | OK-tested | plan 3-5（重開還原版面、漂浮視窗、位置不漂移） |
| 21.M4 | 未涵蓋：拖放開檔、分頁排序、分頁拖到群組、群組排序 | OK-tested | plan 3-4（排序／群組拖曳實測）；Finder 拖進視窗仍未手測（plan 3-2） |
| 21.M5 | 未涵蓋：尋找與取代導覽與全部取代 | OK-tested | plan 3-2 |
| 21.M6 | 未涵蓋：快照註解就地編輯、回復三種結果、刪除確認 | UNSURE | 回復「取代目前內容」plan 3-5 有測；註解編輯、另存為新檔案、刪除確認沒有手測記錄（dec §4-3 明說未操作） |
| 21.M7 | 未涵蓋：設定匯出與匯入 | UNSURE | 邏輯有單元測試（exportImport、importRejects）；檔案面板與按鈕未手測（dec §4-1） |
| 21.M8 | 未涵蓋：主題跟隨系統即時切換、語言即時切換 | UNSURE | 語言與主題（App 內設定）plan 3-5 有測；「系統外觀在 App 開著時切換」未實測（plan 3-4） |
| 21.M9 | 未涵蓋：閱讀器縮放、連結開啟、本機圖片範圍 | UNSURE | 各閱讀器縮放 plan 3-5 已測；連結開啟無記錄；圖片範圍有單元測試 |
| 21.M10 | 未涵蓋：重新命名檔案（含快照檔與群組項目同步搬移） | UNSURE | 快照檔跟著搬有單元測試 sidecarFollowsARename；UI 流程、群組項目與分頁路徑同步沒有測試或手測記錄 |
| 21.M11 | 未涵蓋：IME 組字與快捷鍵互動 | UNSURE | plan 3-2 明說注音輸入需親手測，無記錄 |

## 22. 資料檔格式（逐小節簡述）
| # | 小節 | 狀態 | 證據／備註 |
|---|---|---|---|
| 22-A | 檔案總覽（位置、寫入時機、失敗處理） | DIFF-decided | 位置改 `~/Library/Application Support/Glystrata`（JSONStateStore.swift:21；dec §2「設定資料位置」）；損壞副本與寫入時機同 17.4；暫存檔命名見 22-K |
| 22-B | JSON 共通編碼規則 | DIFF-decided | 讀取：camelCase、容忍註解與尾端逗號（CoreJSON.swift:22；T/GroupAndPersistenceTests outOfRangeSettings…）、日期 0–7 位小數可讀（T datesFromTheWindowsBuildParse）；寫入：UUID 大寫、日期 3 位小數、`"key" : value`、鍵排序、中文不跳脫（dec §2「JSON 細節」，兩邊互讀；plan 5-3 Windows 讀 Mac 檔通過） |
| 22-C | 列舉數值對照 | OK-tested | Language 0/1、Theme 0/1/2、CountMode、Encoding、LineEnding、SplitOrientation、GroupItemKind 皆固定 raw value（AppSettings.swift:6-28、DocumentModels.swift:5-15、PaneLayout.swift:4-7、Group.swift:5-8）；themeNumbersKeepTheirMeaning |
| 22-D | settings.json | OK-tested | settingsRoundTrip、outOfRange…；Normalize 見 AppSettings.swift:197-205；Mac 另寫入 useGlassIcon（plan 3-4，Windows 不讀不寫）；預設色值與 22-D 範例一致（AppSettings.swift:42-104） |
| 22-E | groups.json | OK-code | PersistedStates.swift:6-74；空名稱→「未命名群組」（Group.swift:52）；kind 1 容忍（GroupItemKind）；Windows 路徑字串原樣保留（dec 23-C #6 未決，Mac 目前＝保留原字串） |
| 22-F | session.json | OK-tested | sessionRoundTrip、layout 三個測試（版面錯誤整個回單一窗格、ratio 夾 0.1–0.9）；Mac 另有 floatingWindows（plan 3-5，有容錯測試） |
| 22-G | recent.json | OK-tested | damagedStateStillLoads（files:null、空路徑、重複、超過 10 筆）、seedingAppends…；RecentFilesState |
| 22-H | 快照 sidecar `<檔名>.gss` | OK-tested | T/SnapshotTests sidecarWrittenByTheWindowsBuildOpensAndSurvivesARewrite、sidecarRewrittenByTheWindowsBuildOpensHere；notice 排第一（snapshotLifecycle）；空清單刪檔（deletingTheLastSnapshot…）；plan 5-3 反向驗證通過 |
| 22-I | 舊檔名遷移 | OK-tested | legacySidecarIsMigrated；SnapshotSidecarStore.swift:36-65（Mac 決定也支援） |
| 22-J | 設定匯出檔 | OK-tested | exportImport、importRejects；判斷順序 SettingsFile.swift:31-65；kind 位置見 11.16 |
| 22-K | 暫存與損壞副本命名 | OK-code | 文件暫存 `<路徑>.glystrata-write-<uuid>.tmp`（FilePersistenceService.swift:25）；損壞副本 `<檔>.corrupt-yyyyMMddHHmmss.json`（JSONStateStore.swift:91、103-108）；JSON 暫存改用系統原子寫入（item 允許） |

## 23. 矛盾與不確定之處（只標註決定已記錄在別處的項目）
- 已有決定記錄：C1 快捷鍵（⌘/⌥，plan §1）、C2/C3 `.gss` 與隱藏（kind 判斷＋雙擊開啟；isHidden 旗標，plan §1、3-4）、C4 資料路徑（dec §2）、C5 舊檔名遷移（支援，有測試）、C7 Big5（不做，rev S4）、C8 語言預設（不做，rev S3）、C9 外部變更警告（做，rev B3；保留後不再提示，B4）、C10 跨窗格拖曳（不做，rev S1）、C11 fenced yaml（不做，rev S2）、C12 回復後不自動存（plan 3-5 Cloud 決定）、C14 手動存狀態文字（rev B5）、C16 快照字數 UTF-16（照 Windows，有測試）、C18 改名更新最近清單（rev B17）、C23 色票說明（rev T3）、C24 notice 原文保留、C25 不自訂快速鍵、C26 IME 交給系統。
- 沒找到決定記錄：C6 跨平台路徑（目前保留原字串）、C13 未命名文件跨次保留（Mac 同 Windows 不保留）、C15 搜尋比較規則（Mac 自選 NSString 比較，dec §4-2 假設 2）、C19 主視窗大小保存（見 14.10 UNSURE）、C20 工作階段保存群組篩選（不保存；分頁順序現在會保存，AppModel+Workspace.swift:191-195）、C21 偏好設定即時預覽（不做，套用才生效）。

## 總結

編號：第 1–20 章共 322 條（第 4 章分 4A 12 條、4B 13 條）；第 21 章 13 組檢查＋11 條未涵蓋行為，共 24 列；第 22 章 11 個小節另計。

| 狀態 | 第 1–21 章（346 列） | 第 22 章（11 列） |
|---|---|---|
| OK-tested | 147 | 7 |
| OK-code | 144 | 2 |
| DIFF-decided | 25 | 2 |
| DIFF-open | 4 | 0 |
| MISSING | 3 | 0 |
| N/A | 11 | 0 |
| UNSURE | 12 | 0 |

### DIFF-open / MISSING / UNSURE 清單
- 1.2 DIFF-open：開檔／群組開檔對話框沒有檔案類型篩選（AppModel.swift:259-266、AppModel+Groups.swift:78-87）
- 1.7 DIFF-open：另存預設檔名是「未命名.md」而非 untitled.md，且無格式篩選（AppModel.swift:311）
- 11.9 DIFF-open：編輯器顏色用系統 ColorPicker 取代 HSV 對話框（SettingsTabs.swift:74-87），plan 3-3 列為待 Cloud 決定
- 11.12 DIFF-open：群組顏色「…」選色同上；另外分頁樣式（分段選擇器）也在待決（SettingsView.swift:26-32）
- 6.3 MISSING：選取的分頁不會自動捲入可視範圍（PaneView.swift:20-26，全 App 無 scrollTo）
- 17.9 MISSING：未處理例外沒有記錄檔／錯誤對話框（grep 無 errors.log 或例外處理）
- 20.7 MISSING：打包腳本不含 LICENSE、THIRD-PARTY-NOTICES（mac/scripts/package.sh:1-27，檔案未追蹤）
- 8.22 UNSURE：圖片路徑 `&amp;` 解碼，無測試，沒執行驗證
- 13.2 UNSURE：最近清單項目懸停顯示完整路徑，SwiftUI `.help` 在原生選單是否生效（GlystrataApp.swift:145）
- 14.10 UNSURE：主視窗大小位置是否被 SwiftUI 自動記住（GlystrataApp.swift:11-16），Windows 不保存
- 18.4 UNSURE：輸入法組字中按鍵不被攔截，全靠系統，plan 3-2 未手測
- 20.5 UNSURE：NSAlert 的 Esc／Enter 對應（AppModel.swift:448-463 與單鈕錯誤框沒有明確 Esc 綁定）
- 21.M2 UNSURE：快照 30 秒計時、工作階段 700 ms 防抖沒有測試或實測
- 21.M6 UNSURE：快照註解就地編輯、另存為新檔案、刪除確認沒有手測
- 21.M7 UNSURE：設定匯出／匯入的檔案面板未手測（邏輯有單元測試）
- 21.M8 UNSURE：系統外觀在 App 開著時切換未實測
- 21.M9 UNSURE：閱讀器連結開啟沒有記錄
- 21.M10 UNSURE：檔案重新命名 UI 流程（群組項目、分頁路徑同步）沒有測試／手測
- 21.M11 UNSURE：注音組字與快捷鍵互動未手測

### 其他備註
- 這份是讀碼與讀文件的結果，沒有跑 `swift test` 也沒有開 App；「OK-tested」依測試名稱與 plan 3-2/3-4/3-5 記錄判斷。
- 清單本身與 Windows 現況不符的條目（以 Windows 原始碼為準判斷）：9.14（Windows 比較視窗是單欄統一差異）、13.7（回復另存 Windows 也不記進最近清單）、13.12（Windows 已改成更新最近清單）。
- Mac 專屬、清單沒有的行為：⌘＋／⌘－／⌘0 縮放、玻璃圖示開關、漂浮編輯視窗、雙擊 .gss 開快照紀錄、拖曳時變淡。

## 後續處理（2026/11/24，主 session 核對後）

上面的逐項表是讀程式碼與既有紀錄整理的；以下是核對 DIFF-open、MISSING、UNSURE 之後的處理結果。

- 6.3 已修（`51cba07`）：選到的 TAB 會捲進可視範圍（含從選單、開檔、還原工作階段選到的）。用「12 個 TAB、選最後一個」拍畫面確認。
- 20.7 已修（`a0540ff`）：zip 裡 App 旁邊附 `LICENSE.txt` 與 Mac 專用的 `THIRD-PARTY-NOTICES.txt`（swift-markdown、swift-cmark 0.9.0 的授權原文），與 Windows 的 zip 一致。
- 20.5 已修（`ed36c96`）：「尚有未儲存變更」、一般確認、文字輸入三種詢問框的「取消」綁定 Esc。AppKit 只會自動把 Esc 給英文標題「Cancel」的按鈕，中文介面原本沒有。只確認程式碼與建置，還沒實際按（螢幕鎖定中無法製造未儲存狀態）。
- 1.2、1.7：Cloud 決定跟 Windows 一樣，已在 `mac/align-2` 補上類型選單與 `untitled.md`（計畫文件 3-7）。
- 11.9、11.12：原本就列在待 Cloud 決定的外觀取捨（偏好設定分頁樣式、按鈕、選色對話框）。
- 17.9：Cloud 決定跟 Windows 一樣，已在 `mac/align-2` 補上 `errors.log`（計畫文件 3-7）。
- 14.10：主視窗大小位置由系統記住，屬於第 1 節「視窗大小調整照 Mac 慣例」的決定。
- 其餘 UNSURE（8.22、13.2、18.4、21.M2、21.M6–M11）需要動手操作，列入待實測。2026/11/25–26 實測了 18.4／21.M11（注音）、21.M6（快照備註、另存、刪除）、21.M7（設定匯出入）、21.M8（深淺色）、21.M9（閱讀器連結）、21.M10（重新命名）與 20.5（Esc），都通過或修正後通過（計畫文件 3-7）。
