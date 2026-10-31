# Flux

English | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Русский](README.ru.md) | [Türkçe](README.tr.md) | [Bahasa Indonesia](README.id.md) | [فارسی](README.fa.md) | [العربية](README.ar.md) | [Татарча](README.tt.md)

A lightweight Windows proxy client powered by [mihomo](https://github.com/MetaCubeX/mihomo), built with WinUI 3 and Fluent design.

[Downloads](https://github.com/1595901624/flux/releases) · [Report an issue](https://github.com/1595901624/flux/issues) · [License](LICENSE)

[![Get it from Microsoft](https://get.microsoft.com/images/en-us%20dark.svg)](https://apps.microsoft.com/store/detail/9NT71ZHK49Q7?cid=DevShareMCLPCS)

## Screenshots

### Home

Manage the system proxy and routing mode, and view the active profile, current node, live traffic, and core status in one place.

![Flux home page showing network controls, the active profile, traffic statistics, and core information](screenshots/home.png)

### Proxies

Browse proxy groups, filter nodes by name or type, switch nodes, and run latency tests.

![Flux proxies page showing routing modes, proxy groups, node selection, and latency test results](screenshots/proxies.png)

The screenshots use the demo profile in [`sample-profile.yaml`](sample-profile.yaml). Its placeholder servers are for UI demonstration and cannot be used as working proxy endpoints.

## Features

- **Native Windows interface** — WinUI 3, Mica backdrop, Fluent design, and multiple interface languages.
- **Flexible routing** — Rule, Global, and Direct modes, with system proxy and TUN controls.
- **Profile management** — Import Clash-compatible YAML files or subscription URLs, switch profiles, and update subscriptions.
- **Proxy management** — Select nodes, filter by name or type, inspect proxy providers, and test latency.
- **Traffic and diagnostics** — Live upload/download graphs, active connections, routing rules, core logs, and unlock tests.
- **Tray integration** — Close the window to hide Flux to the system tray; use the tray menu to exit.
- **Bundled runtime and core** — Distribution builds include mihomo, .NET, and Windows App SDK components.

## Getting started

Flux supports **Windows 10 version 2004 (build 19041) or later**, on x86, x64, and ARM64.

1. Open [Releases](https://github.com/1595901624/flux/releases) and select a package matching your Windows architecture. Follow the installation instructions for that release. For a sideload ZIP, extract it and run the included `Install-FluxSideload.ps1` as administrator.
2. Launch Flux and open **Profiles**. Import your subscription URL or a local Clash-compatible YAML file, then select the profile to activate it.
3. Open **Proxies** to choose a node or proxy group. Use **Rule** mode to route traffic according to the profile.
4. Enable **System proxy** on **Home** to let applications that follow Windows proxy settings use Flux.

TUN mode requires administrator privileges or a working Flux privileged service. Closing the window keeps Flux running in the tray.

Unsigned MSIX artifacts from CI are for development testing. Portable ZIP artifacts, when provided, can be extracted and launched with `Flux.exe`.

## Configuration and data

Flux uses Clash-compatible YAML profiles containing `proxies`, `proxy-groups`, and `rules`. See [`sample-profile.yaml`](sample-profile.yaml) for an example. Supply your own valid server details or subscription URL; Flux does not include a proxy subscription.

By default, settings, profiles, and logs are stored in `%APPDATA%\flux`. For a portable distribution in a writable directory, create an empty `.config\PORTABLE` file beside `Flux.exe` before launch to store data in `.config\flux` instead.

## Build from source

Build on Windows with the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). The project targets Windows SDK `10.0.22621.0` and uses WinUI 3. For IDE development, use a Visual Studio version that supports .NET 10 and WinUI 3.

```powershell
git clone https://github.com/1595901624/flux.git
cd flux
dotnet restore Flux.slnx
dotnet build Flux.csproj -c Release
dotnet run --project Flux.csproj -c Release --no-build
```

The default target is `win-x64`. Pass `-r win-x86` or `-r win-arm64` to build for another architecture. The repository includes a matching mihomo core for each target.

Run the tests:

```powershell
dotnet test tests/Flux.Tests/Flux.Tests.csproj -c Release
```

For distribution packaging, including the privileged service payload, see the [build workflow](.github/workflows/build-msix.yml).

## Community

This open-source project links to and recognizes the [LINUX DO community](https://linux.do/).

## License

Flux is licensed under [GNU GPL v3.0](LICENSE). See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for the bundled mihomo version, checksum, corresponding source, and license information.
