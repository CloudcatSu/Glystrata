# Glystrata

Glystrata 是一款以 Windows 11 原生 WPF 為基礎的輕量化文字編輯器，支援 Markdown／YAML 語法渲染，目前版本為 1.2.3。

目前提供：

- 左側虛擬群組，可從群組右鍵或「群組」選單加入檔案；群組只保存連結，不搬移原始檔案。
- 選定群組時只顯示該群組的 TAB，也可切回「全部 TAB」。
- 多窗格、多列／多欄分割：分割時把目前窗格的最後一個 TAB 移到新窗格；TAB 右鍵可「在新窗格開啟」同一份文件（左右或上下並排），各檢視游標與捲動位置獨立。
- 關閉空窗格後會自動收合，並提供一鍵還原單一窗格；TAB 一律單列橫排，過多時可橫向捲動。
- 以純文字編輯為主，Markdown 與 YAML 語法分色；狀態列可縮放編輯器（Ctrl＋滾輪亦可）。
- 每個 TAB 的眼睛按鈕可開啟獨立 Markdown 閱讀器，預覽視窗可同時存在多個，並可各自縮放；閱讀器內的文字可選取與複製但不能編輯，emoji 以彩色圖檔繪製（編輯器維持系統的單色 emoji）。
- 亮色／暗色主題，繁體中文／English 即時切換；所有視窗使用跟隨主題的自訂標題列，支援 Windows 11 Snap Layouts。
- 自動儲存、原始文件目錄下的快照 sidecar（可於偏好設定選擇是否隱藏）、自動與手動快照、快照比較／回復／刪除。
- 支援從檔案總管或直接拖放一個／多個檔案到視窗，加入目前群組並開啟 TAB。
- UTF-8、UTF-8 BOM、UTF-16 LE 與常見換行格式保留。

## 下載版本

每個 release 提供兩種 zip，內容與功能完全相同，差別只在是否內含 .NET 執行環境：

| 檔案 | 內含 .NET | 需另裝 .NET 10 Desktop Runtime | 體積 | 適用 |
| --- | --- | --- | --- | --- |
| `Glystrata-v<版本>-win-x64.zip` | 是（self-contained） | 否 | 約 70 MB | 不確定電腦有沒有裝 .NET 時選這個，解壓即用 |
| `Glystrata-v<版本>-win-x64-fdd.zip` | 否（framework-dependent） | **是** | 約 4 MB | 電腦已裝 .NET 10 Desktop Runtime (x64) |

兩者版本號相同，精簡版的檔名多一個 `-fdd` 後綴。精簡版若在沒裝 Runtime 的電腦上開啟，Windows 會跳出英文提示並引導下載。Runtime 可從 [.NET 10 下載頁](https://dotnet.microsoft.com/download/dotnet/10.0) 取得，或以 `winget install Microsoft.DotNet.DesktopRuntime.10` 安裝；安裝後可用 `dotnet --list-runtimes` 確認有 `Microsoft.WindowsDesktop.App 10.x`。

**ARM64 版 Windows**（例如 Surface Pro X、Snapdragon 筆電、Mac 上的 Parallels）：Glystrata 是 x64 程式，在 ARM64 上透過系統模擬執行。完整版不受影響；精簡版則需要 **x64** 版的 Desktop Runtime，光裝 ARM64 版不夠（以 winget 安裝時加上 `--architecture x64`）。此時 `dotnet --list-runtimes` 只會列出 ARM64 版，要改用 `"C:\Program Files\dotnet\x64\dotnet.exe" --list-runtimes` 確認。

**Mac 版（Apple Silicon）**：`Glystrata-v<版本>-mac-arm64.zip`，適用 M 系列晶片的 Mac、macOS 14 以上（不支援 Intel Mac）。下載後雙擊 zip 解壓，把資料夾裡的 `Glystrata.app` 拖到「應用程式」資料夾（同一個資料夾還有授權聲明）。

這個版本沒有經過 Apple 公證（只有免費的 ad-hoc 簽章），所以第一次打開時 macOS 會擋下，顯示「未打開『Glystrata.app』」與「Apple 無法驗證『Glystrata.app』是否為惡意軟體…」。照下面做一次，之後就能正常打開：

1. 雙擊 `Glystrata.app`，出現警告時按「完成」。
2. 打開「系統設定 > 隱私權與安全性」，往下捲到「安全性」，在「已阻擋『Glystrata.app』以保護你的 Mac。」旁邊按「強制打開」。
3. 輸入登入密碼（或用 Touch ID）確認，再在跳出的對話框按「強制打開」。

macOS 15 起，在 Finder 用 Control-click（右鍵）選「打開」已經不能略過這個檢查，只能用上面的方法。也可以改用「終端機」移除「從網路下載」的標記，之後雙擊就能打開：

```bash
xattr -dr com.apple.quarantine /Applications/Glystrata.app
```

## 建置

需要 Windows 與 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。在 repo 根目錄以 PowerShell 執行：

```powershell
dotnet build .\Glystrata.sln -c Release
```

執行驗證程式：

```powershell
dotnet run --project .\tests\Glystrata.Verification\Glystrata.Verification.csproj
```

發布 Windows x64 self-contained 單檔：

```powershell
dotnet publish .\src\Glystrata\Glystrata.csproj -p:PublishProfile=win-x64
```

輸出位於 `dist/win-x64/`：單一 exe，已內含 .NET 執行環境，不需另外安裝。

打包成一份可直接複製到別台電腦的 zip：

```powershell
.\scripts\publish.ps1
```

會產生 `dist/win-x64/`（原始發布內容）與 `dist/Glystrata-v<版本>-win-x64.zip`（打包好的整包，只要複製這個 zip 到別台機器解壓即可執行，不需另外安裝 .NET）。

需要更小的檔案時，可打包 framework-dependent 版本（不含 .NET 執行環境，目標電腦須先安裝 .NET 10 Desktop Runtime (x64)，見上方〈下載版本〉）：

```powershell
.\scripts\publish.ps1 -Fdd
```

會產生 `dist/win-x64-fdd/` 與 `dist/Glystrata-v<版本>-win-x64-fdd.zip`；版本號與一般版相同，僅檔名多 `-fdd` 後綴。要一次打包兩個版本（release 用）：

```powershell
.\scripts\publish.ps1 -All
```

若只用 Visual Studio 建置、沒有安裝 `dotnet` CLI，可改成在方案總管右鍵 Glystrata 專案 → Publish → 選 `win-x64` 設定檔發布（一樣會輸出到 `dist/win-x64/`），再執行：

```powershell
.\scripts\publish.ps1 -SkipPublish
```

只做打包 zip 這一步。

Mac 版需要 Apple Silicon 的 Mac、Xcode 與 [XcodeGen](https://github.com/yonaskolb/XcodeGen)（`brew install xcodegen`），程式在 `mac/`：

```bash
cd mac
swift test                # 核心邏輯的測試
scripts/build.sh          # 建置開發版 App
scripts/package.sh        # 建置 Release 版並打包成 dist/Glystrata-v<版本>-mac-arm64.zip
```

## 授權

Glystrata 以 [MIT License](LICENSE) 釋出。

使用的第三方元件：

- .NET 10 執行環境／WPF：MIT License（self-contained 發布時會打包進執行檔）。
- AvalonEdit 6.3.1.120：MIT License。
- Markdig 1.3.2：BSD-2-Clause License。
- Twemoji 15.1.0（Markdown 閱讀器的彩色 emoji 圖檔）：CC-BY 4.0，著作權為 Twitter, Inc 與其他貢獻者所有。

發布目錄會附上 `LICENSE.txt`（本專案授權）、`THIRD-PARTY-NOTICES.txt`（各第三方元件的著作權聲明與授權全文）、`DOTNET-THIRD-PARTY-NOTICES.txt`（.NET 執行環境內含元件的聲明），以及 `TWEMOJI-LICENSE-GRAPHICS.txt`（emoji 圖檔的 CC-BY 4.0 授權全文）。

本專案不使用雲端服務、遙測、Electron 或 WebView2。

## 資料位置

應用程式設定、群組、工作階段與錯誤記錄（`errors.log`）位於 `%LocalAppData%\Glystrata`；Mac 版位於 `~/Library/Application Support/Glystrata`。Mac 版遇到介面層的錯誤會寫進 `errors.log` 後繼續執行，以免還沒存的文字跟著消失（Windows 版記錄後會結束程式）；真的無法繼續的錯誤兩邊都會結束。單一文件的快照位於原始文件同一目錄，檔名格式為：

```text
<原始檔名>.gss
```

例如 `筆記.md` 的快照存在同目錄的 `筆記.md.gss`（內容是 JSON）。此檔案預設為一般可見檔案，可在「偏好設定 → 快照 → 隱藏快照檔案」開啟後改為 Windows Hidden 屬性；開啟文件時與程式啟動時，既有的快照檔案會依目前設定轉換為對應的可見／隱藏狀態。

## 其他文件

- 版本紀錄：`CHANGELOG.md`
- 需求基線與實作計畫：`docs/specs/`、`docs/plans/`
