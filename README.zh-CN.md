# Flux

[English](README.md) | 简体中文 | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Русский](README.ru.md) | [Türkçe](README.tr.md) | [Bahasa Indonesia](README.id.md) | [فارسی](README.fa.md) | [العربية](README.ar.md) | [Татарча](README.tt.md)

Flux 是一款基于 [mihomo](https://github.com/MetaCubeX/mihomo) 内核的轻量级 Windows 代理客户端，使用 WinUI 3 构建，采用 Fluent 设计。

[下载安装](https://github.com/1595901624/flux/releases) · [反馈问题](https://github.com/1595901624/flux/issues) · [许可证](LICENSE)

## 界面预览

### 首页概览

集中管理系统代理与路由模式，查看当前配置、所选节点、实时流量和内核运行状态。

![Flux 首页：系统代理开关、当前配置、流量统计与内核信息](screenshots/home.png)

### 代理管理

按策略组浏览节点，按名称或类型筛选，切换节点并进行延迟测试。

![Flux 代理管理页：路由模式、策略组、节点选择与延迟测试结果](screenshots/proxies.png)

截图使用 [`sample-profile.yaml`](sample-profile.yaml) 中的演示配置。服务器地址为占位数据，仅用于展示界面，不能作为实际代理服务使用。

## 功能特性

- **原生 Windows 界面**：WinUI 3、Mica 背景与 Fluent 设计，支持多种界面语言。
- **灵活的路由控制**：支持规则、全局、直连模式，提供系统代理与 TUN 开关。
- **配置与订阅管理**：导入 Clash 兼容的 YAML 文件或订阅链接，切换配置并更新订阅。
- **节点与策略组管理**：切换节点，按名称或类型筛选，查看代理提供者并测试延迟。
- **流量与诊断**：实时上传/下载曲线、活动连接、路由规则、内核日志及解锁测试。
- **系统托盘**：关闭窗口后隐藏到托盘，通过托盘菜单退出应用。
- **内置运行环境**：发行构建包含 mihomo、.NET 与 Windows App SDK 组件。

## 快速开始

支持 **Windows 10 2004（内部版本 19041）及以上版本**，适用于 x86、x64 和 ARM64 架构。

1. 前往[发布页](https://github.com/1595901624/flux/releases)，选择与 Windows 架构匹配的安装包，按照对应版本的说明安装。若下载侧载 ZIP，请先解压，再以管理员身份运行包内的 `Install-FluxSideload.ps1`。
2. 启动 Flux，进入 **Profiles（配置）**，导入订阅链接或本地 Clash 兼容 YAML 文件，并选择该配置使其生效。
3. 进入 **Proxies（代理）**，选择节点或策略组。使用 **Rule（规则）** 模式时，流量将按配置中的规则分流。
4. 在 **Home（首页）** 开启 **System proxy（系统代理）**，让遵循 Windows 系统代理设置的应用使用 Flux。

TUN 模式需要管理员权限，或可正常工作的 Flux 特权服务。关闭窗口后，Flux 会继续在托盘中运行。

CI 生成的未签名 MSIX 仅用于开发测试。若提供便携 ZIP 产物，解压后运行 `Flux.exe` 即可。

## 配置与数据目录

Flux 使用 Clash 兼容的 YAML 配置，包含 `proxies`（节点）、`proxy-groups`（策略组）和 `rules`（规则），可参考 [`sample-profile.yaml`](sample-profile.yaml)。请使用自己的有效服务器信息或订阅链接；Flux 不提供代理订阅。

默认情况下，设置、配置文件和日志保存在 `%APPDATA%\flux`。对于放在可写目录中的便携版，可在启动前于 `Flux.exe` 同级目录创建空文件 `.config\PORTABLE`，将数据改为保存在 `.config\flux`。

## 从源码构建

请在 Windows 上安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。项目面向 Windows SDK `10.0.22621.0`，使用 WinUI 3。使用 IDE 开发时，请选择支持 .NET 10 和 WinUI 3 的 Visual Studio 版本。

```powershell
git clone https://github.com/1595901624/flux.git
cd flux
dotnet restore Flux.slnx
dotnet build Flux.csproj -c Release
dotnet run --project Flux.csproj -c Release --no-build
```

默认构建目标为 `win-x64`。构建其他架构时，可传入 `-r win-x86` 或 `-r win-arm64`。仓库已包含各架构对应的 mihomo 内核。

运行测试：

```powershell
dotnet test tests/Flux.Tests/Flux.Tests.csproj -c Release
```

包含特权服务载荷的发行打包流程见[构建工作流](.github/workflows/build-msix.yml)。

## 许可证

Flux 使用 [GNU GPL v3.0](LICENSE) 发布。内置 mihomo 的版本、校验值、对应源码与许可信息见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
