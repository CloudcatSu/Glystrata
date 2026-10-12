# Glystrata Mac 版（Apple Silicon）移植計畫

- 建立：2026/10/13
- 分支：`mac-port`
- 對照基準：Windows 版 v1.2.3（commit `04ff0b4`）

## 1. 已定案的決策

| 項目 | 決定 |
| --- | --- |
| 技術路線 | Swift 原生重寫，不走 Avalonia、不走 Avalonia XPF |
| 介面技術 | SwiftUI 為主；編輯器本體用 AppKit 的 `NSTextView`（中文輸入法交給系統） |
| 一致的範圍 | 版面、配色、功能與 Windows 版一致 |
| 照 Mac 慣例的部分 | 字型（蘋果預設字型）、選單位置、視窗按鈕、視窗大小調整；不做 Snap Layouts |
| 隱藏快照檔 | 用 Mac 的隱藏旗標（`URLResourceValues.isHidden`），檔名維持 `<檔名>.gss` |
| 程式位置 | 同一個 repo，Mac 版全部放在 `mac/` |
| 發布 | 目前只自用（ad-hoc 簽章）；Apple Developer 帳號之後才申請 |
| 快速鍵 | 尚未定案，另行討論 |

## 2. 專案結構

```text
mac/
├── Package.swift
├── Sources/GlystrataCore/     ← 不含介面的核心邏輯（對應 src/Glystrata.Core）
└── Tests/GlystrataCoreTests/  ← 對應 tests/Glystrata.Verification
```

建置與測試（需要 Xcode 27）：

```bash
cd mac
swift build
swift test
```

## 3. 階段與進度

| 階段 | 內容 | 狀態 |
| --- | --- | --- |
| 0 | 盤點 Windows 版規格、整理驗收清單、技術研究 | 完成，產出見第 4 節；其中待決事項見 `2026-10-13-mac-open-decisions.md` |
| 1 | Core 移植成 Swift | 完成（Markdown 解析除外，等解析器定案）；43 個測試通過 |
| 2 | 主視窗：選單、群組側欄、TAB、分割窗格、編輯器、狀態列 | 未開始 |
| 3 | 周邊視窗：偏好設定、快照歷史與比較、尋找取代、閱讀器、說明、對話框 | 未開始 |
| 4 | Mac 整合：`.gss` 檔案關聯、Finder 拖放、跟隨系統深淺色、App 圖示 | 未開始 |
| 5 | 與 Windows 版截圖並排比對、逐條跑驗收清單 | 未開始，需要第 5 節的截圖 |
| 6 | 打包 arm64 `.app` 與發布腳本 | 未開始 |

## 4. 階段 0 的產出

| 檔案 | 內容 |
| --- | --- |
| `docs/specs/2026-10-13-mac-visual-tokens.md` | 色票亮暗對照、控制項樣式、字型、調色盤 |
| `docs/specs/2026-10-13-mac-main-window-inventory.md` | 主視窗版面、選單、側欄、TAB、編輯器、狀態列、快速鍵 |
| `docs/specs/2026-10-13-mac-secondary-windows-inventory.md` | 其餘視窗與對話框、閱讀器渲染規則、emoji |
| `docs/specs/2026-10-13-mac-feature-checklist.md` | 322 條功能驗收清單、資料檔格式 |
| `docs/plans/2026-10-13-mac-tech-research.md` | Markdown 解析器、編輯元件、建置、簽章、隱藏檔、系統版本的查證結果 |
| `docs/specs/2026-10-13-mac-reader-sample.md` | 閱讀器比對用的範例文件（兩個平台開同一份） |

這幾份是從原始碼與文件整理出來的，數值都附了來源行號；尚未對照實際執行中的 Windows 版。

## 5. 需要 Windows 那邊提供的東西

### 5-1 截圖

條件：顯示比例 100%、只截視窗本身（Alt + PrtScn）、PNG。亮色與暗色各一套，檔名用 `<編號>-<light|dark>.png`，放在 `docs/reference/windows-1.2.3/`。

| 編號 | 畫面 |
| --- | --- |
| 01 | 主視窗，剛啟動、沒有任何 TAB |
| 02 | 主視窗，開 4 個以上 TAB，其中一個有未儲存的修改 |
| 03 | 主視窗，3 個群組且各有不同顏色，選取其中一個有顏色的群組 |
| 04 | 主視窗，選取「全部 TAB」 |
| 05 | 主視窗，左右分割兩個窗格 |
| 06 | 主視窗，上下分割兩個窗格 |
| 07 | 主視窗，三個窗格（左右分割後其中一邊再上下分割） |
| 08 | 主視窗，顯示格式工具列 |
| 09 | 主視窗，TAB 多到需要橫向捲動 |
| 10 | 主視窗，編輯器縮放到 150% |
| 11–15 | 檔案、編輯、檢視、群組、說明五個選單各自展開 |
| 16 | TAB 的右鍵選單 |
| 17 | 群組的右鍵選單，含顏色子選單展開 |
| 18 | 編輯一份含各種語法的 Markdown（開 `2026-10-13-mac-reader-sample.md`） |
| 19 | 編輯一份 YAML 檔 |
| 20–24 | 偏好設定的五個分頁：一般、顏色、閱讀器顏色、群組顏色、快照 |
| 25 | 快照歷史視窗，至少 3 筆快照、其中一筆有備註 |
| 26 | 比較視窗，同時有新增行與刪除行 |
| 27 | 尋找與取代視窗 |
| 28 | Markdown 語法說明視窗 |
| 29 | 閱讀器視窗開 `2026-10-13-mac-reader-sample.md`，從頭到尾捲動分段截完 |
| 30 | 關於對話框 |
| 31 | 關閉未儲存文件時的詢問對話框 |
| 32 | 重新命名群組的輸入對話框 |
| 33 | 選色對話框 |
| 34 | 外部檔案變更時的提示 |

拖曳 TAB 或群組時的放置指示如果截得到也一併提供；截不到的話用錄影。

### 5-2 實際資料檔

用來確認 Mac 版讀寫的格式與 Windows 版一致。檔案裡會有本機路徑，提供前請先看過。

- 一份 Windows 版實際寫出的 `.gss`（至少 2 筆快照、其中一筆有備註）
- `%LocalAppData%\Glystrata\` 底下的 `settings.json`、`groups.json`、`session.json`、`recent.json`

### 5-3 反向驗證

Mac 版寫出的 `.gss` 拿到 Windows 版開啟，確認快照歷史能正常列出、比較、回復。
