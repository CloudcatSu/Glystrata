# Glystrata

Glystrata 是一款以 Windows 11 原生 WPF 為基礎的輕量 Markdown 編輯器，版本 0.0.2 為 MVP 增量版。

目前提供：

- 左側虛擬群組，可加入檔案與資料夾；群組只保存連結，不搬移原始檔案。
- 選定群組時只顯示該群組的 TAB，也可切回「全部 TAB」。
- 多窗格、多列／多欄分割，以及同一份文件的多個獨立檢視。
- 關閉空窗格後會自動收合，並提供一鍵還原單一窗格。
- 以純文字編輯為主，Markdown 與 YAML 語法分色。
- 每個 TAB 的眼睛按鈕可開啟獨立 Markdown 閱讀器，預覽視窗可同時存在多個。
- 亮色／暗色主題，繁體中文／English 即時切換。
- 自動儲存、原始文件目錄下的隱藏快照 sidecar、快照比較／回復／刪除。
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

## 相依套件與授權

- .NET 10／WPF：Microsoft .NET 授權條款。
- AvalonEdit 6.3.1.120：MIT License。
- Markdig 1.3.2：BSD-2-Clause License。

發布目錄同時包含 `THIRD-PARTY-NOTICES.txt`，列出第三方套件與授權摘要。

本機專案不使用雲端服務、遙測、Electron、WebView2 或 Docker Desktop。依企業環境規則，正式部署前仍應由組織法務／採購確認適用的商業使用條件。

## 資料位置

應用程式設定、群組、工作階段與錯誤記錄位於 `%LocalAppData%\Glystrata`。單一文件的快照位於原始文件同一目錄，檔名格式為：

```text
.<原始檔名>.glystrata-snapshots.json
```

此 sidecar 會設為 Windows Hidden 屬性。完整的需求基線與實作計畫在 `docs/specs/`、`docs/plans/`，版本紀錄在 `CHANGELOG.md`。
