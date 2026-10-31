# Flux

[English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | Deutsch | [Español](README.es.md) | [Русский](README.ru.md) | [Türkçe](README.tr.md) | [Bahasa Indonesia](README.id.md) | [فارسی](README.fa.md) | [العربية](README.ar.md) | [Татарча](README.tt.md)

Flux ist ein schlanker Proxy-Client für Windows mit [mihomo](https://github.com/MetaCubeX/mihomo) als Kern, entwickelt mit WinUI 3 und Fluent Design.

[Download](https://github.com/1595901624/flux/releases) · [Problem melden](https://github.com/1595901624/flux/issues) · [Lizenz](LICENSE)

## Screenshots

### Startseite

Systemproxy und Routingmodus verwalten sowie aktives Profil, ausgewählten Knoten, aktuellen Datenverkehr und Kernstatus anzeigen.

![Flux-Startseite mit Proxy-Einstellungen, Profil, Datenverkehr und Kerninformationen](screenshots/home.png)

### Proxys

Proxy-Gruppen durchsuchen, Knoten nach Name oder Typ filtern, Knoten wechseln und Latenztests ausführen.

![Flux-Proxyverwaltung mit Routingmodi, Gruppen, Knotenauswahl und Latenztests](screenshots/proxies.png)

Die Screenshots verwenden das Demoprofil aus [`sample-profile.yaml`](sample-profile.yaml). Die Serveradressen sind Platzhalter und keine nutzbaren Proxy-Endpunkte.

## Funktionen

- **Native Windows-Oberfläche** — WinUI 3, Mica-Hintergrund, Fluent Design und mehrere Oberflächensprachen.
- **Flexibles Routing** — Rule-, Global- und Direct-Modus sowie Steuerung von Systemproxy und TUN.
- **Profilverwaltung** — Clash-kompatible YAML-Dateien oder Abonnement-URLs importieren, Profile wechseln und Abonnements aktualisieren.
- **Proxyverwaltung** — Knoten auswählen, nach Name oder Typ filtern, Proxy-Provider anzeigen und Latenzen testen.
- **Datenverkehr und Diagnose** — Live-Diagramme für Upload und Download, aktive Verbindungen, Routingregeln, Kernprotokolle und Tests auf Dienstfreischaltung.
- **Infobereich** — Beim Schließen wird das Fenster in den Infobereich minimiert. Beenden erfolgt über das dortige Menü.
- **Mitgelieferte Laufzeit** — Distributionsbuilds enthalten mihomo, .NET und Windows-App-SDK-Komponenten.

## Erste Schritte

Unterstützt werden **Windows 10 Version 2004 (Build 19041) oder neuer** auf x86, x64 und ARM64.

1. Auf der [Release-Seite](https://github.com/1595901624/flux/releases) ein Paket für die Windows-Architektur auswählen und die Installationsanleitung des Releases befolgen. Ein Sideload-ZIP entpacken und die enthaltene `Install-FluxSideload.ps1` als Administrator ausführen.
2. Flux starten, unter **Profiles** eine Abonnement-URL oder lokale YAML-Datei importieren und das Profil zum Aktivieren auswählen.
3. Unter **Proxies** einen Knoten oder eine Gruppe auswählen. Im Modus **Rule** wird der Datenverkehr nach den Profilregeln geleitet.
4. Unter **Home** den **System proxy** aktivieren, damit Anwendungen, die die Windows-Proxyeinstellungen beachten, Flux verwenden.

TUN erfordert Administratorrechte oder einen funktionierenden privilegierten Flux-Dienst. Nach dem Schließen des Fensters läuft Flux im Infobereich weiter.

Unsignierte MSIX-Artefakte aus CI sind für Entwicklungstests bestimmt. Falls ein portables ZIP bereitgestellt wird, dieses entpacken und `Flux.exe` starten.

## Konfiguration und Daten

Flux verwendet Clash-kompatible YAML-Profile mit `proxies`, `proxy-groups` und `rules`. Ein Beispiel ist [`sample-profile.yaml`](sample-profile.yaml). Eigene gültige Serverdaten oder eine Abonnement-URL sind erforderlich; Flux stellt kein Proxy-Abonnement bereit.

Einstellungen, Profile und Protokolle liegen standardmäßig unter `%APPDATA%\flux`. Bei einer portablen Ausgabe in einem beschreibbaren Verzeichnis vor dem Start eine leere Datei `.config\PORTABLE` neben `Flux.exe` anlegen, um die Daten unter `.config\flux` zu speichern.

## Aus dem Quellcode erstellen

Unter Windows wird das [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) benötigt. Das Projekt verwendet WinUI 3 und zielt auf Windows SDK `10.0.22621.0`. Für die IDE-Entwicklung eine Visual-Studio-Version mit Unterstützung für .NET 10 und WinUI 3 verwenden.

```powershell
git clone https://github.com/1595901624/flux.git
cd flux
dotnet restore Flux.slnx
dotnet build Flux.csproj -c Release
dotnet run --project Flux.csproj -c Release --no-build
```

Standardziel ist `win-x64`. Für andere Architekturen `-r win-x86` oder `-r win-arm64` angeben. Das Repository enthält den passenden mihomo-Kern für jedes Ziel.

Tests ausführen:

```powershell
dotnet test tests/Flux.Tests/Flux.Tests.csproj -c Release
```

Die Paketierung einschließlich des privilegierten Dienstes ist im [Build-Workflow](.github/workflows/build-msix.yml) beschrieben.

## Lizenz

Flux steht unter [GNU GPL v3.0](LICENSE). Version, Prüfsumme, zugehöriger Quellcode und Lizenzinformationen des mitgelieferten mihomo stehen in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
