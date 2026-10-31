# Flux

[English](README.md) | [简体中文](README.zh-CN.md) | 繁體中文 | [日本語](README.ja.md) | [한국어](README.ko.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Русский](README.ru.md) | [Türkçe](README.tr.md) | [Bahasa Indonesia](README.id.md) | [فارسی](README.fa.md) | [العربية](README.ar.md) | [Татарча](README.tt.md)

Flux 是一款基於 [mihomo](https://github.com/MetaCubeX/mihomo) 核心的輕量級 Windows 代理用戶端，使用 WinUI 3 建置，採用 Fluent 設計。

[下載安裝](https://github.com/1595901624/flux/releases) · [回報問題](https://github.com/1595901624/flux/issues) · [授權條款](LICENSE)

## 介面預覽

### 首頁概覽

集中管理系統代理與路由模式，查看目前設定檔、所選節點、即時流量和核心執行狀態。

![Flux 首頁：系統代理、設定檔、流量統計與核心資訊](screenshots/home.png)

### 代理管理

依策略群組瀏覽節點，依名稱或類型篩選，切換節點並測試延遲。

![Flux 代理管理：路由模式、策略群組、節點選擇與延遲測試](screenshots/proxies.png)

截圖使用 [`sample-profile.yaml`](sample-profile.yaml) 中的示範設定。伺服器位址僅為佔位資料，不能作為實際代理服務使用。

## 功能特色

- **原生 Windows 介面**：WinUI 3、Mica 背景與 Fluent 設計，支援多種介面語言。
- **彈性路由**：規則、全域、直連模式，以及系統代理與 TUN 開關。
- **設定檔與訂閱**：匯入 Clash 相容的 YAML 檔案或訂閱連結，切換設定檔並更新訂閱。
- **節點管理**：切換節點、依名稱或類型篩選、查看代理提供者並測試延遲。
- **流量與診斷**：即時上傳／下載曲線、作用中連線、路由規則、核心記錄及解鎖測試。
- **系統匣**：關閉視窗後隱藏至系統匣，透過系統匣選單結束程式。
- **內建執行環境**：發行組建包含 mihomo、.NET 與 Windows App SDK 元件。

## 快速開始

支援 **Windows 10 2004（組建 19041）及以上版本**，適用於 x86、x64 和 ARM64。

1. 前往[發行頁](https://github.com/1595901624/flux/releases)，依 Windows 架構選擇套件並按照該版本的說明安裝。若使用側載 ZIP，先解壓縮，再以系統管理員身分執行 `Install-FluxSideload.ps1`。
2. 啟動 Flux，進入 **Profiles（設定檔）**，匯入訂閱連結或本機 YAML 檔案，並選取設定檔使其生效。
3. 進入 **Proxies（代理）** 選擇節點或策略群組。**Rule（規則）** 模式會依設定檔中的規則分流。
4. 在 **Home（首頁）** 開啟 **System proxy（系統代理）**，讓遵循 Windows 代理設定的應用程式使用 Flux。

TUN 模式需要系統管理員權限，或可正常運作的 Flux 特權服務。關閉視窗後，Flux 仍會在系統匣中執行。

CI 產生的未簽署 MSIX 僅供開發測試。若提供可攜式 ZIP，解壓縮後執行 `Flux.exe` 即可。

## 設定與資料目錄

Flux 使用 Clash 相容的 YAML 設定，包含 `proxies`、`proxy-groups` 和 `rules`，範例見 [`sample-profile.yaml`](sample-profile.yaml)。請提供有效的伺服器資訊或訂閱連結；Flux 不提供代理訂閱。

設定、設定檔與記錄預設儲存在 `%APPDATA%\flux`。對於放在可寫入目錄的可攜式版本，啟動前可在 `Flux.exe` 同層建立空白的 `.config\PORTABLE` 檔案，改將資料儲存在 `.config\flux`。

## 從原始碼建置

請在 Windows 安裝 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。專案使用 WinUI 3，目標為 Windows SDK `10.0.22621.0`。使用 IDE 時，請選擇支援 .NET 10 與 WinUI 3 的 Visual Studio 版本。

```powershell
git clone https://github.com/1595901624/flux.git
cd flux
dotnet restore Flux.slnx
dotnet build Flux.csproj -c Release
dotnet run --project Flux.csproj -c Release --no-build
```

預設目標為 `win-x64`。其他架構可傳入 `-r win-x86` 或 `-r win-arm64`；儲存庫已包含各架構對應的 mihomo 核心。

執行測試：

```powershell
dotnet test tests/Flux.Tests/Flux.Tests.csproj -c Release
```

包含特權服務載荷的發行封裝流程見[建置工作流程](.github/workflows/build-msix.yml)。

## 授權條款

Flux 依 [GNU GPL v3.0](LICENSE) 發行。內建 mihomo 的版本、校驗值、對應原始碼與授權資訊見 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
