# Vxn

[简体中文](README.zh-CN.md)

Vxn is a lightweight proxy client for Windows powered by the [mihomo](https://github.com/MetaCubeX/mihomo) core, built with WinUI 3.

## Features

- **Modern UI** — Native WinUI 3 interface with Mica backdrop and Fluent design
- **System tray** — Closing the window hides to tray; exit from the tray menu
- **Real-time traffic graph** — Live upload/download throughput monitoring
- **YAML profiles** — Uses Clash-compatible YAML configuration (proxies, proxy groups, rules)
- **Self-contained** — Bundles the mihomo core and Windows App SDK, no extra installation required

## Requirements

- Windows 10 version 19041 (2004) or later (x64)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) with the Windows workload for building

## Getting Started

1. Clone the repository:

   ```bash
   git clone https://github.com/1595901624/flux.git
   cd flux
   ```

2. Build and run:

   ```bash
   dotnet build Vxn/Vxn.csproj -c Release
   ```

   Or open `Vxn.slnx` in Visual Studio 2022 and press <kbd>F5</kbd>.

## Configuration

Vxn uses standard Clash-compatible YAML profiles. See [`sample-profile.yaml`](sample-profile.yaml) for a minimal example covering proxies, proxy groups, and rules.

## License

This project is distributed for learning and personal use. The bundled mihomo core is licensed under its own terms — see [MetaCubeX/mihomo](https://github.com/MetaCubeX/mihomo).
