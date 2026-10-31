# Flux

[English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Русский](README.ru.md) | [Türkçe](README.tr.md) | [Bahasa Indonesia](README.id.md) | [فارسی](README.fa.md) | العربية | [Татарча](README.tt.md)

Flux هو عميل وكيل خفيف لنظام Windows يعتمد على نواة [mihomo](https://github.com/MetaCubeX/mihomo)، ومبني باستخدام WinUI 3 وتصميم Fluent.

[تنزيل](https://github.com/1595901624/flux/releases) · [الإبلاغ عن مشكلة](https://github.com/1595901624/flux/issues) · [الترخيص](LICENSE)

## لقطات الشاشة

### الصفحة الرئيسية

أدر وكيل النظام ووضع التوجيه، واطّلع على ملف الإعداد النشط والعقدة المختارة وحركة البيانات المباشرة وحالة النواة.

![الصفحة الرئيسية في Flux: إعدادات الوكيل وملف الإعداد وإحصاءات البيانات ومعلومات النواة](screenshots/home.png)

### إدارة الوكلاء

تصفّح مجموعات الوكلاء، وصفِّ العقد حسب الاسم أو النوع، وبدّل بينها واختبر زمن الاستجابة.

![إدارة الوكلاء في Flux: الأوضاع والمجموعات واختيار العقد واختبارات زمن الاستجابة](screenshots/proxies.png)

تستخدم اللقطات ملف الإعداد التجريبي [`sample-profile.yaml`](sample-profile.yaml). عناوين الخوادم أمثلة للعرض فقط ولا تصلح كوكلاء فعليين.

## الميزات

- **واجهة Windows أصلية** — WinUI 3 وخلفية Mica وتصميم Fluent وواجهة متعددة اللغات.
- **توجيه مرن** — أوضاع Rule وGlobal وDirect مع التحكم في وكيل النظام وTUN.
- **إدارة ملفات الإعداد** — استيراد ملفات YAML المتوافقة مع Clash أو روابط الاشتراك، والتبديل بين الملفات وتحديث الاشتراكات.
- **إدارة العقد** — اختيار العقد وتصفيتها حسب الاسم أو النوع، وعرض مزوّدي الوكلاء واختبار زمن الاستجابة.
- **البيانات والتشخيص** — رسوم مباشرة للرفع والتنزيل، واتصالات نشطة وقواعد توجيه وسجلات النواة واختبارات الوصول إلى الخدمات.
- **علبة النظام** — يؤدي إغلاق النافذة إلى إخفاء Flux في علبة النظام. استخدم قائمتها للخروج.
- **بيئة تشغيل مضمّنة** — تتضمن إصدارات التوزيع mihomo و.NET ومكوّنات Windows App SDK.

## البدء

يدعم **Windows 10 الإصدار 2004 (البنية 19041) أو أحدث** على معماريات x86 وx64 وARM64.

1. افتح [صفحة الإصدارات](https://github.com/1595901624/flux/releases)، واختر حزمة تناسب معمارية Windows واتبع تعليمات ذلك الإصدار. لحزمة ZIP للتثبيت الجانبي، فك الضغط وشغّل الملف المرفق `Install-FluxSideload.ps1` بصلاحيات المسؤول.
2. شغّل Flux وافتح **Profiles**. استورد رابط اشتراك أو ملف YAML محليًا، ثم اختر ملف الإعداد لتفعيله.
3. في **Proxies**، اختر عقدة أو مجموعة. يوجّه وضع **Rule** حركة البيانات وفق قواعد ملف الإعداد.
4. فعّل **System proxy** في **Home** لتستخدم Flux التطبيقات التي تتبع إعدادات وكيل Windows.

يتطلب وضع TUN صلاحيات المسؤول أو خدمة Flux ذات الامتيازات تعمل بصورة سليمة. يواصل Flux العمل في علبة النظام بعد إغلاق النافذة.

حزم MSIX غير الموقّعة الناتجة عن CI مخصّصة لاختبارات التطوير. إذا توفرت حزمة ZIP محمولة، فك ضغطها وشغّل `Flux.exe`.

## الإعدادات والبيانات

يستخدم Flux ملفات YAML متوافقة مع Clash تحتوي على `proxies` و`proxy-groups` و`rules`. راجع [`sample-profile.yaml`](sample-profile.yaml). يجب توفير بيانات خادم صالحة أو رابط اشتراك خاص بك؛ لا يوفّر Flux اشتراكات وكيل.

تُحفظ الإعدادات وملفات الإعداد والسجلات افتراضيًا في `%APPDATA%\flux`. للنسخة المحمولة في مجلد قابل للكتابة، أنشئ ملفًا فارغًا باسم `.config\PORTABLE` بجوار `Flux.exe` قبل التشغيل لحفظ البيانات في `.config\flux`.

## البناء من المصدر

ثبّت [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) على Windows. يستخدم المشروع WinUI 3 ويستهدف Windows SDK `10.0.22621.0`. للتطوير ضمن IDE، استخدم إصدار Visual Studio يدعم .NET 10 وWinUI 3.

```powershell
git clone https://github.com/1595901624/flux.git
cd flux
dotnet restore Flux.slnx
dotnet build Flux.csproj -c Release
dotnet run --project Flux.csproj -c Release --no-build
```

الهدف الافتراضي هو `win-x64`. للمعماريات الأخرى، حدّد `-r win-x86` أو `-r win-arm64`. يتضمن المستودع نواة mihomo المناسبة لكل هدف.

تشغيل الاختبارات:

```powershell
dotnet test tests/Flux.Tests/Flux.Tests.csproj -c Release
```

لتجهيز حزم التوزيع بما فيها الخدمة ذات الامتيازات، راجع [سير عمل البناء](.github/workflows/build-msix.yml).

## الترخيص

يُنشر Flux بموجب [GNU GPL v3.0](LICENSE). يتضمن [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) إصدار mihomo المرفق وقيمة التحقق والكود المصدري المقابل ومعلومات الترخيص.
