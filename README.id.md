# Flux

[English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Русский](README.ru.md) | [Türkçe](README.tr.md) | Bahasa Indonesia | [فارسی](README.fa.md) | [العربية](README.ar.md) | [Татарча](README.tt.md)

Flux adalah klien proxy ringan untuk Windows yang menggunakan inti [mihomo](https://github.com/MetaCubeX/mihomo), dibangun dengan WinUI 3 dan Fluent Design.

[Unduh](https://github.com/1595901624/flux/releases) · [Laporkan masalah](https://github.com/1595901624/flux/issues) · [Lisensi](LICENSE)

## Tangkapan layar

### Beranda

Kelola proxy sistem dan mode perutean, serta lihat profil aktif, node yang dipilih, lalu lintas langsung, dan status inti.

![Beranda Flux: kontrol proxy, profil, statistik lalu lintas, dan informasi inti](screenshots/home.png)

### Pengelolaan proxy

Jelajahi grup proxy, filter node berdasarkan nama atau jenis, ganti node, dan jalankan uji latensi.

![Pengelolaan proxy Flux: mode, grup, pemilihan node, dan hasil uji latensi](screenshots/proxies.png)

Tangkapan layar menggunakan profil demo dari [`sample-profile.yaml`](sample-profile.yaml). Alamat server hanya contoh untuk tampilan dan tidak dapat digunakan sebagai proxy nyata.

## Fitur

- **Antarmuka Windows native** — WinUI 3, latar Mica, Fluent Design, dan berbagai bahasa antarmuka.
- **Perutean fleksibel** — Mode Rule, Global, dan Direct, dengan kontrol proxy sistem dan TUN.
- **Pengelolaan profil** — Impor berkas YAML yang kompatibel dengan Clash atau URL langganan, ganti profil, dan perbarui langganan.
- **Pengelolaan proxy** — Pilih node, filter berdasarkan nama atau jenis, lihat penyedia proxy, dan uji latensi.
- **Lalu lintas dan diagnostik** — Grafik unggah/unduh langsung, koneksi aktif, aturan perutean, log inti, dan pengujian akses layanan.
- **Baki sistem** — Menutup jendela menyembunyikan Flux ke baki sistem. Gunakan menu baki untuk keluar.
- **Runtime disertakan** — Build distribusi menyertakan mihomo, .NET, dan komponen Windows App SDK.

## Memulai

Mendukung **Windows 10 versi 2004 (build 19041) atau lebih baru**, untuk x86, x64, dan ARM64.

1. Buka [halaman rilis](https://github.com/1595901624/flux/releases), pilih paket sesuai arsitektur Windows, dan ikuti petunjuk rilis tersebut. Untuk ZIP sideload, ekstrak lalu jalankan `Install-FluxSideload.ps1` yang disertakan sebagai administrator.
2. Jalankan Flux, buka **Profiles**, impor URL langganan atau berkas YAML lokal, lalu pilih profil untuk mengaktifkannya.
3. Di **Proxies**, pilih node atau grup. Mode **Rule** merutekan lalu lintas sesuai aturan profil.
4. Aktifkan **System proxy** di **Home** agar aplikasi yang mengikuti pengaturan proxy Windows menggunakan Flux.

Mode TUN memerlukan hak administrator atau layanan berhak istimewa Flux yang berfungsi. Flux tetap berjalan di baki setelah jendela ditutup.

Artefak MSIX tanpa tanda tangan dari CI hanya untuk pengujian pengembangan. Jika tersedia ZIP portabel, ekstrak dan jalankan `Flux.exe`.

## Konfigurasi dan data

Flux menggunakan profil YAML yang kompatibel dengan Clash dengan bagian `proxies`, `proxy-groups`, dan `rules`. Lihat [`sample-profile.yaml`](sample-profile.yaml). Siapkan informasi server yang valid atau URL langganan Anda sendiri; Flux tidak menyediakan langganan proxy.

Pengaturan, profil, dan log secara default disimpan di `%APPDATA%\flux`. Untuk distribusi portabel di direktori yang dapat ditulisi, buat berkas kosong `.config\PORTABLE` di samping `Flux.exe` sebelum dijalankan agar data disimpan di `.config\flux`.

## Build dari kode sumber

Pasang [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) di Windows. Proyek menggunakan WinUI 3 dan menargetkan Windows SDK `10.0.22621.0`. Untuk IDE, gunakan versi Visual Studio yang mendukung .NET 10 dan WinUI 3.

```powershell
git clone https://github.com/1595901624/flux.git
cd flux
dotnet restore Flux.slnx
dotnet build Flux.csproj -c Release
dotnet run --project Flux.csproj -c Release --no-build
```

Target default adalah `win-x64`. Untuk arsitektur lain, gunakan `-r win-x86` atau `-r win-arm64`. Repositori menyertakan inti mihomo yang sesuai untuk setiap target.

Jalankan pengujian:

```powershell
dotnet test tests/Flux.Tests/Flux.Tests.csproj -c Release
```

Untuk pengemasan distribusi termasuk layanan berhak istimewa, lihat [alur kerja build](.github/workflows/build-msix.yml).

## Lisensi

Flux dilisensikan di bawah [GNU GPL v3.0](LICENSE). Versi mihomo yang disertakan, checksum, kode sumber terkait, dan informasi lisensinya ada di [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
