# Flux

[简体中文](README.zh-CN.md)

Flux is a lightweight proxy client for Windows powered by the [mihomo](https://github.com/MetaCubeX/mihomo) core, built with WinUI 3.

## Features

- **Modern UI** — Native WinUI 3 interface with Mica backdrop and Fluent design
- **System tray** — Closing the window hides to tray; exit from the tray menu
- **Real-time traffic graph** — Live upload/download throughput monitoring
- **YAML profiles** — Uses Clash-compatible YAML configuration (proxies, proxy groups, rules)
- **Self-contained** — Bundles the mihomo core and Windows App SDK, no extra installation required

## Requirements

- Windows 10 version 19041 (2004) or later (x86, x64, or ARM64)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) with the Windows workload for building

## Getting Started

1. Clone the repository:

   ```bash
   git clone https://github.com/1595901624/flux.git
   cd flux
   ```

2. Build and run:

   ```bash
   dotnet build Flux.csproj -c Release
   ```

   Or open `Flux.slnx` in Visual Studio 2022 and press <kbd>F5</kbd>.

Run the tests:

```bash
dotnet test Flux.slnx -c Release
```

The portable ZIP from Releases can be extracted and run directly. Unsigned MSIX artifacts produced by CI are for development testing only and are not published as installable releases.

## Configuration

Flux uses standard Clash-compatible YAML profiles. See [`sample-profile.yaml`](sample-profile.yaml) for a minimal example covering proxies, proxy groups, and rules.

## License

Flux is licensed under the [GNU GPL v3.0](LICENSE). See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for the bundled mihomo version, checksum, corresponding source, and license information.
