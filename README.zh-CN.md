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

- Windows 10 版本 19041（2004）或更高（x64）
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

## 配置说明

Flux 使用标准的 Clash 兼容 YAML 配置文件。可参考 [`sample-profile.yaml`](sample-profile.yaml) 了解节点、策略组和规则的最小配置示例。

## 许可证

本项目仅供学习与个人使用。内置的 mihomo 内核遵循其自身的开源许可协议，详见 [MetaCubeX/mihomo](https://github.com/MetaCubeX/mihomo)。
