# Flux

[English](README.md) | 简体中文

Flux 是一款基于 [mihomo](https://github.com/MetaCubeX/mihomo) 内核的轻量级 Windows 代理客户端，使用 WinUI 3 构建。

## 功能特性

- **现代化界面** — 原生 WinUI 3 界面，Mica 背景与 Fluent 设计
- **系统托盘** — 关闭窗口即隐藏到托盘，通过托盘菜单退出
- **实时流量图** — 实时显示上传/下载速率曲线
- **YAML 配置** — 兼容 Clash 风格的 YAML 配置（节点、策略组、规则）
- **开箱即用** — 内置 mihomo 内核与 Windows App SDK，无需额外安装

## 环境要求

- Windows 10 版本 19041（2004）或更高（x86、x64 或 ARM64）
- 构建（编译）需要安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## 快速开始

1. 克隆仓库：

   ```bash
   git clone https://github.com/1595901624/flux.git
   cd flux
   ```

2. 编译并运行：

   ```bash
   dotnet build Flux.csproj -c Release
   ```

   或使用 Visual Studio 2022 打开 `Flux.slnx`，按 <kbd>F5</kbd> 调试运行。

运行测试：

```bash
dotnet test Flux.slnx -c Release
```

发布页提供的便携 ZIP 解压后可直接运行。CI 生成的未签名 MSIX 仅用于开发测试，不作为公开安装包。

## 配置说明

Flux 使用标准的 Clash 兼容 YAML 配置文件。可参考 [`sample-profile.yaml`](sample-profile.yaml) 了解节点、策略组和规则的最小配置示例。

## 许可证

Flux 使用 [GNU GPL v3.0](LICENSE) 发布。内置 mihomo 的版本、校验值、源码与许可信息见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
