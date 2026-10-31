# Flux

[English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Русский](README.ru.md) | Türkçe | [Bahasa Indonesia](README.id.md) | [فارسی](README.fa.md) | [العربية](README.ar.md) | [Татарча](README.tt.md)

Flux, [mihomo](https://github.com/MetaCubeX/mihomo) çekirdeğini kullanan, WinUI 3 ve Fluent Design ile geliştirilmiş hafif bir Windows proxy istemcisidir.

[İndir](https://github.com/1595901624/flux/releases) · [Sorun bildir](https://github.com/1595901624/flux/issues) · [Lisans](LICENSE)

## Ekran görüntüleri

### Ana sayfa

Sistem proxy'sini ve yönlendirme modunu yönetin; etkin profili, seçili düğümü, anlık trafiği ve çekirdek durumunu görün.

![Flux ana sayfası: proxy denetimleri, profil, trafik istatistikleri ve çekirdek bilgileri](screenshots/home.png)

### Proxy yönetimi

Proxy gruplarını inceleyin, düğümleri ada veya türe göre filtreleyin, düğüm değiştirin ve gecikme testi yapın.

![Flux proxy yönetimi: modlar, gruplar, düğüm seçimi ve gecikme testleri](screenshots/proxies.png)

Görüntülerde [`sample-profile.yaml`](sample-profile.yaml) içindeki demo profil kullanılır. Sunucu adresleri örnektir ve çalışan proxy uç noktaları değildir.

## Özellikler

- **Yerel Windows arayüzü** — WinUI 3, Mica arka planı, Fluent Design ve çoklu dil desteği.
- **Esnek yönlendirme** — Rule, Global ve Direct modları; sistem proxy'si ve TUN denetimleri.
- **Profil yönetimi** — Clash uyumlu YAML dosyaları veya abonelik URL'leri içe aktarma, profil değiştirme ve abonelik güncelleme.
- **Proxy yönetimi** — Düğüm seçimi, ad veya türe göre filtreleme, proxy sağlayıcılarını görüntüleme ve gecikme testi.
- **Trafik ve tanılama** — Anlık yükleme/indirme grafikleri, etkin bağlantılar, kurallar, çekirdek günlükleri ve hizmet erişim testleri.
- **Sistem tepsisi** — Pencere kapatıldığında Flux tepsiye gizlenir. Çıkmak için tepsi menüsünü kullanın.
- **Dahil edilen çalışma ortamı** — Dağıtım derlemeleri mihomo, .NET ve Windows App SDK bileşenlerini içerir.

## Başlangıç

**Windows 10 sürüm 2004 (derleme 19041) ve üzeri**, x86, x64 ve ARM64 mimarilerinde desteklenir.

1. [Sürümler](https://github.com/1595901624/flux/releases) sayfasından Windows mimarinize uygun paketi seçin ve sürümün kurulum yönergelerini izleyin. Yandan yükleme ZIP'ini açıp içindeki `Install-FluxSideload.ps1` dosyasını yönetici olarak çalıştırın.
2. Flux'u başlatıp **Profiles** bölümünde abonelik URL'sini veya yerel YAML dosyasını içe aktarın. Etkinleştirmek için profili seçin.
3. **Proxies** bölümünde bir düğüm veya grup seçin. **Rule** modu trafiği profil kurallarına göre yönlendirir.
4. Windows proxy ayarlarını izleyen uygulamaların Flux'u kullanması için **Home** bölümünde **System proxy** seçeneğini açın.

TUN modu yönetici yetkisi veya çalışan bir Flux ayrıcalıklı hizmeti gerektirir. Pencere kapandığında Flux tepside çalışmaya devam eder.

CI tarafından üretilen imzasız MSIX dosyaları geliştirme testleri içindir. Taşınabilir ZIP sunuluyorsa açıp `Flux.exe` dosyasını çalıştırabilirsiniz.

## Yapılandırma ve veriler

Flux, `proxies`, `proxy-groups` ve `rules` içeren Clash uyumlu YAML profilleri kullanır. Örnek için [`sample-profile.yaml`](sample-profile.yaml) dosyasına bakın. Geçerli sunucu bilgilerinizi veya abonelik URL'nizi sağlamalısınız; Flux proxy aboneliği sunmaz.

Ayarlar, profiller ve günlükler varsayılan olarak `%APPDATA%\flux` altında saklanır. Yazılabilir bir klasördeki taşınabilir sürümde, başlatmadan önce `Flux.exe` yanında boş bir `.config\PORTABLE` dosyası oluşturursanız veriler `.config\flux` altında saklanır.

## Kaynak koddan derleme

Windows'a [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) kurun. Proje WinUI 3 kullanır ve Windows SDK `10.0.22621.0` sürümünü hedefler. IDE için .NET 10 ve WinUI 3 destekleyen bir Visual Studio sürümü kullanın.

```powershell
git clone https://github.com/1595901624/flux.git
cd flux
dotnet restore Flux.slnx
dotnet build Flux.csproj -c Release
dotnet run --project Flux.csproj -c Release --no-build
```

Varsayılan hedef `win-x64` olur. Diğer mimariler için `-r win-x86` veya `-r win-arm64` belirtin. Depo, her hedefe uygun mihomo çekirdeğini içerir.

Testleri çalıştırın:

```powershell
dotnet test tests/Flux.Tests/Flux.Tests.csproj -c Release
```

Ayrıcalıklı hizmet dahil dağıtım paketleme adımları için [derleme iş akışına](.github/workflows/build-msix.yml) bakın.

## Lisans

Flux, [GNU GPL v3.0](LICENSE) ile dağıtılır. Dahil edilen mihomo sürümü, sağlama toplamı, ilgili kaynak kodu ve lisans bilgileri [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) dosyasındadır.
