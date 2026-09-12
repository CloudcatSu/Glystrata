# Glystrata

Glystrata 是一款以 Windows 11 原生 WPF 為基礎的輕量化文字編輯器，支援 Markdown／YAML 語法渲染，目前版本為 1.1.0。

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

## 建置

需要 .NET SDK 10。使用 PowerShell：

```powershell
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
& $dotnet restore .\Glystrata.sln
& $dotnet build .\Glystrata.sln -c Release -m:1 -p:UseSharedCompilation=false
```

執行核心驗證：

```powershell
& $dotnet run --project .\tests\Glystrata.Verification\Glystrata.Verification.csproj -c Debug
```

發布 Windows x64 self-contained 單檔：

```powershell
& $dotnet publish .\src\Glystrata\Glystrata.csproj -p:PublishProfile=win-x64
```

輸出位於 `dist/win-x64/`（單一 exe，不含 .NET 執行環境相依）。

打包成一份可直接複製到別台電腦的 zip：

```powershell
.\scripts\publish.ps1
```

會產生 `dist/win-x64/`（原始發布內容）與 `dist/Glystrata-v<版本>-win-x64.zip`（打包好的整包，只要複製這個 zip 到別台機器解壓即可執行，不需另外安裝 .NET）。

若這台機器沒裝 `dotnet` CLI（例如只靠 Visual Studio 建置），改成在方案總管右鍵 Glystrata 專案 → Publish → 選 `win-x64` 設定檔發布（一樣會輸出到 `dist/win-x64/`），再執行：

```powershell
.\scripts\publish.ps1 -SkipPublish
```

只做打包 zip 這一步。

## 授權

Glystrata 以 [MIT License](LICENSE) 釋出。

使用的第三方元件：

- .NET 10 執行環境／WPF：MIT License（self-contained 發布時會打包進執行檔）。
- AvalonEdit 6.3.1.120：MIT License。
- Markdig 1.3.2：BSD-2-Clause License。
- Twemoji 15.1.0（Markdown 閱讀器的彩色 emoji 圖檔）：CC-BY 4.0，著作權為 Twitter, Inc 與其他貢獻者所有。

發布目錄會附上 `LICENSE.txt`（本專案授權）、`THIRD-PARTY-NOTICES.txt`（各第三方元件的著作權聲明與授權全文）、`DOTNET-THIRD-PARTY-NOTICES.txt`（.NET 執行環境內含元件的聲明），以及 `TWEMOJI-LICENSE-GRAPHICS.txt`（emoji 圖檔的 CC-BY 4.0 授權全文）。

本專案不使用雲端服務、遙測、Electron、WebView2 或 Docker Desktop。

## 資料位置

應用程式設定、群組、工作階段與錯誤記錄位於 `%LocalAppData%\Glystrata`。單一文件的快照位於原始文件同一目錄，檔名格式為：

```text
.<原始檔名>.glystrata-snapshots.json
```

此 sidecar 預設為一般可見檔案，可在「偏好設定 → 快照 → 隱藏快照檔案」開啟後改為 Windows Hidden 屬性；開啟文件時與程式啟動時，既有的快照檔案會依目前設定轉換為對應的可見／隱藏狀態。完整的需求基線與實作計畫在 `docs/specs/`、`docs/plans/`，版本紀錄在 `CHANGELOG.md`。
