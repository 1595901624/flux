# Flux

[English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Русский](README.ru.md) | [Türkçe](README.tr.md) | [Bahasa Indonesia](README.id.md) | فارسی | [العربية](README.ar.md) | [Татарча](README.tt.md)

Flux یک کلاینت پروکسی سبک برای Windows است که از هستهٔ [mihomo](https://github.com/MetaCubeX/mihomo) استفاده می‌کند و با WinUI 3 و Fluent Design ساخته شده است.

[دانلود](https://github.com/1595901624/flux/releases) · [گزارش مشکل](https://github.com/1595901624/flux/issues) · [مجوز](LICENSE)

[![Get it from Microsoft](https://get.microsoft.com/images/en-us%20dark.svg)](https://apps.microsoft.com/store/detail/9NT71ZHK49Q7?cid=DevShareMCLPCS)

## تصاویر برنامه

### خانه

پروکسی سیستم و حالت مسیریابی را مدیریت کنید و پروفایل فعال، گره انتخاب‌شده، ترافیک زنده و وضعیت هسته را ببینید.

![خانهٔ Flux: تنظیمات پروکسی، پروفایل، آمار ترافیک و اطلاعات هسته](screenshots/home.png)

### مدیریت پروکسی

گروه‌های پروکسی را مرور کنید، گره‌ها را بر اساس نام یا نوع فیلتر کنید، گره را تغییر دهید و تأخیر را بسنجید.

![مدیریت پروکسی در Flux: حالت‌ها، گروه‌ها، انتخاب گره و آزمون تأخیر](screenshots/proxies.png)

تصاویر از پروفایل نمایشی [`sample-profile.yaml`](sample-profile.yaml) استفاده می‌کنند. نشانی‌های سرورها نمونه هستند و نمی‌توان از آن‌ها به‌عنوان پروکسی واقعی استفاده کرد.

## قابلیت‌ها

- **رابط بومی Windows** — WinUI 3، پس‌زمینهٔ Mica، طراحی Fluent و پشتیبانی از چند زبان.
- **مسیریابی انعطاف‌پذیر** — حالت‌های Rule، Global و Direct و کنترل پروکسی سیستم و TUN.
- **مدیریت پروفایل** — وارد کردن فایل YAML سازگار با Clash یا نشانی اشتراک، تعویض پروفایل و به‌روزرسانی اشتراک‌ها.
- **مدیریت گره‌ها** — انتخاب گره، فیلتر بر اساس نام یا نوع، مشاهدهٔ ارائه‌دهندگان پروکسی و سنجش تأخیر.
- **ترافیک و عیب‌یابی** — نمودار زندهٔ ارسال و دریافت، اتصال‌های فعال، قوانین مسیریابی، گزارش‌های هسته و آزمون دسترسی به سرویس‌ها.
- **سینی سیستم** — بستن پنجره، Flux را در سینی سیستم پنهان می‌کند. برای خروج از منوی سینی استفاده کنید.
- **محیط اجرا همراه برنامه** — نسخه‌های توزیع شامل mihomo، .NET و اجزای Windows App SDK هستند.

## شروع کار

از **Windows 10 نسخهٔ 2004 (بیلد 19041) و جدیدتر** با معماری‌های x86، x64 و ARM64 پشتیبانی می‌شود.

1. در [صفحهٔ انتشارها](https://github.com/1595901624/flux/releases)، بستهٔ متناسب با معماری Windows را انتخاب و طبق راهنمای همان نسخه نصب کنید. برای ZIP نصب جانبی، آن را استخراج کرده و فایل همراه `Install-FluxSideload.ps1` را با دسترسی مدیر اجرا کنید.
2. Flux را اجرا کنید و در **Profiles** نشانی اشتراک یا فایل YAML محلی را وارد کنید. سپس پروفایل را برای فعال‌سازی انتخاب کنید.
3. در **Proxies** یک گره یا گروه انتخاب کنید. حالت **Rule** ترافیک را طبق قوانین پروفایل هدایت می‌کند.
4. در **Home** گزینهٔ **System proxy** را فعال کنید تا برنامه‌هایی که از تنظیمات پروکسی Windows پیروی می‌کنند از Flux استفاده کنند.

حالت TUN به دسترسی مدیر یا سرویس دارای دسترسی ویژهٔ Flux که درست کار می‌کند نیاز دارد. پس از بستن پنجره، Flux در سینی سیستم به کار ادامه می‌دهد.

فایل‌های MSIX بدون امضای CI برای آزمون در زمان توسعه هستند. اگر ZIP قابل‌حمل ارائه شده باشد، آن را استخراج و `Flux.exe` را اجرا کنید.

## تنظیمات و داده‌ها

Flux از پروفایل YAML سازگار با Clash شامل `proxies`، `proxy-groups` و `rules` استفاده می‌کند. نمونه را در [`sample-profile.yaml`](sample-profile.yaml) ببینید. اطلاعات معتبر سرور یا نشانی اشتراک خود را تهیه کنید؛ Flux اشتراک پروکسی ارائه نمی‌دهد.

تنظیمات، پروفایل‌ها و گزارش‌ها به‌طور پیش‌فرض در `%APPDATA%\flux` ذخیره می‌شوند. در نسخهٔ قابل‌حمل داخل پوشه‌ای با اجازهٔ نوشتن، پیش از اجرا یک فایل خالی `.config\PORTABLE` کنار `Flux.exe` ایجاد کنید تا داده‌ها در `.config\flux` ذخیره شوند.

## ساخت از کد منبع

روی Windows، [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) را نصب کنید. پروژه از WinUI 3 استفاده می‌کند و Windows SDK `10.0.22621.0` را هدف می‌گیرد. برای توسعه در IDE، نسخه‌ای از Visual Studio با پشتیبانی از .NET 10 و WinUI 3 انتخاب کنید.

```powershell
git clone https://github.com/1595901624/flux.git
cd flux
dotnet restore Flux.slnx
dotnet build Flux.csproj -c Release
dotnet run --project Flux.csproj -c Release --no-build
```

هدف پیش‌فرض `win-x64` است. برای معماری‌های دیگر `-r win-x86` یا `-r win-arm64` را مشخص کنید. مخزن شامل هستهٔ mihomo مناسب هر هدف است.

اجرای آزمون‌ها:

```powershell
dotnet test tests/Flux.Tests/Flux.Tests.csproj -c Release
```

برای بسته‌بندی توزیع همراه با سرویس دارای دسترسی ویژه، [گردش کار ساخت](.github/workflows/build-msix.yml) را ببینید.

## مجوز

Flux با مجوز [GNU GPL v3.0](LICENSE) منتشر می‌شود. نسخهٔ mihomo همراه برنامه، مقدار checksum، کد منبع متناظر و اطلاعات مجوز در [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) آمده است.
