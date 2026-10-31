# Flux

[English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | 日本語 | [한국어](README.ko.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Русский](README.ru.md) | [Türkçe](README.tr.md) | [Bahasa Indonesia](README.id.md) | [فارسی](README.fa.md) | [العربية](README.ar.md) | [Татарча](README.tt.md)

Flux は [mihomo](https://github.com/MetaCubeX/mihomo) コアを搭載した軽量な Windows 向けプロキシクライアントです。WinUI 3 と Fluent デザインを採用しています。

[ダウンロード](https://github.com/1595901624/flux/releases) · [問題の報告](https://github.com/1595901624/flux/issues) · [ライセンス](LICENSE)

[![Get it from Microsoft](https://get.microsoft.com/images/en-us%20dark.svg)](https://apps.microsoft.com/store/detail/9NT71ZHK49Q7?cid=DevShareMCLPCS)

## スクリーンショット

### ホーム

システムプロキシとルーティングモードを管理し、使用中のプロファイル、ノード、リアルタイムの通信量、コアの状態を確認できます。

![Flux のホーム：プロキシ設定、プロファイル、通信量、コア情報](screenshots/home.png)

### プロキシ管理

グループごとにノードを表示し、名前や種類で絞り込み、ノードの切り替えや遅延テストを行えます。

![Flux のプロキシ管理：モード、グループ、ノード選択、遅延テスト](screenshots/proxies.png)

画像では [`sample-profile.yaml`](sample-profile.yaml) のデモ設定を使用しています。サーバーアドレスは表示用のダミーで、実際のプロキシとしては利用できません。

## 主な機能

- **Windows ネイティブ UI**：WinUI 3、Mica 背景、Fluent デザイン、多言語表示。
- **柔軟なルーティング**：Rule・Global・Direct モード、システムプロキシと TUN の切り替え。
- **プロファイル管理**：Clash 互換 YAML ファイルやサブスクリプション URL のインポート、設定の切り替え、サブスクリプションの更新。
- **ノード管理**：ノード選択、名前や種類による絞り込み、プロバイダーの確認、遅延テスト。
- **通信量と診断**：送受信速度のグラフ、接続一覧、ルール、コアログ、サービスの利用可否テスト。
- **システムトレイ**：ウィンドウを閉じるとトレイに格納されます。終了するにはトレイメニューを使用します。
- **ランタイム同梱**：配布ビルドには mihomo、.NET、Windows App SDK が含まれます。

## はじめに

**Windows 10 バージョン 2004（ビルド 19041）以降**の x86・x64・ARM64 に対応しています。

1. [リリースページ](https://github.com/1595901624/flux/releases)から Windows のアーキテクチャに合ったパッケージを選び、そのリリースの手順でインストールします。サイドロード ZIP は展開し、同梱の `Install-FluxSideload.ps1` を管理者として実行します。
2. Flux の **Profiles** でサブスクリプション URL またはローカルの YAML ファイルをインポートし、プロファイルを選択して有効にします。
3. **Proxies** でノードやグループを選択します。**Rule** モードではプロファイルのルールに従って通信を振り分けます。
4. **Home** の **System proxy** を有効にすると、Windows のプロキシ設定に従うアプリが Flux を使用します。

TUN モードには管理者権限、または正常に動作する Flux 特権サービスが必要です。ウィンドウを閉じてもトレイで動作を続けます。

CI の未署名 MSIX は開発テスト用です。ポータブル ZIP が提供されている場合は、展開して `Flux.exe` を起動できます。

## 設定とデータ

Clash 互換 YAML の `proxies`、`proxy-groups`、`rules` を使用します。[`sample-profile.yaml`](sample-profile.yaml) を参考に、有効なサーバー情報やサブスクリプション URL を用意してください。Flux はプロキシのサブスクリプションを提供しません。

設定、プロファイル、ログは既定で `%APPDATA%\flux` に保存されます。書き込み可能な場所のポータブル版では、起動前に `Flux.exe` と同じ場所に空の `.config\PORTABLE` ファイルを作成すると、保存先を `.config\flux` に変更できます。

## ソースからビルド

Windows に [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) をインストールしてください。WinUI 3 と Windows SDK `10.0.22621.0` を対象とします。IDE を使う場合は .NET 10 と WinUI 3 に対応した Visual Studio を使用してください。

```powershell
git clone https://github.com/1595901624/flux.git
cd flux
dotnet restore Flux.slnx
dotnet build Flux.csproj -c Release
dotnet run --project Flux.csproj -c Release --no-build
```

既定のターゲットは `win-x64` です。他のアーキテクチャには `-r win-x86` または `-r win-arm64` を指定します。対応する mihomo コアはリポジトリに含まれています。

テストの実行：

```powershell
dotnet test tests/Flux.Tests/Flux.Tests.csproj -c Release
```

特権サービスを含む配布パッケージの作成は[ビルドワークフロー](.github/workflows/build-msix.yml)を参照してください。

## ライセンス

Flux は [GNU GPL v3.0](LICENSE) で公開されています。同梱 mihomo のバージョン、チェックサム、対応するソース、ライセンス情報は [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) を参照してください。
