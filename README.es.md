# Flux

[English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Deutsch](README.de.md) | Español | [Русский](README.ru.md) | [Türkçe](README.tr.md) | [Bahasa Indonesia](README.id.md) | [فارسی](README.fa.md) | [العربية](README.ar.md) | [Татарча](README.tt.md)

Flux es un cliente proxy ligero para Windows basado en el núcleo [mihomo](https://github.com/MetaCubeX/mihomo), desarrollado con WinUI 3 y Fluent Design.

[Descargar](https://github.com/1595901624/flux/releases) · [Informar de un problema](https://github.com/1595901624/flux/issues) · [Licencia](LICENSE)

[![Get it from Microsoft](https://get.microsoft.com/images/en-us%20dark.svg)](https://apps.microsoft.com/store/detail/9NT71ZHK49Q7?cid=DevShareMCLPCS)

## Capturas de pantalla

### Inicio

Gestiona el proxy del sistema y el modo de enrutamiento, y consulta el perfil activo, el nodo seleccionado, el tráfico en tiempo real y el estado del núcleo.

![Inicio de Flux: controles de proxy, perfil, estadísticas de tráfico e información del núcleo](screenshots/home.png)

### Proxies

Explora los grupos de proxies, filtra los nodos por nombre o tipo, cambia de nodo y realiza pruebas de latencia.

![Gestión de proxies de Flux: modos, grupos, selección de nodos y pruebas de latencia](screenshots/proxies.png)

Las capturas usan el perfil de demostración de [`sample-profile.yaml`](sample-profile.yaml). Las direcciones de servidor son ficticias y no se pueden usar como proxies reales.

## Funciones

- **Interfaz nativa de Windows** — WinUI 3, fondo Mica, Fluent Design y varios idiomas.
- **Enrutamiento flexible** — Modos Rule, Global y Direct, con controles para el proxy del sistema y TUN.
- **Gestión de perfiles** — Importación de archivos YAML compatibles con Clash o URL de suscripción, cambio de perfil y actualización de suscripciones.
- **Gestión de proxies** — Selección de nodos, filtros por nombre o tipo, consulta de proveedores y pruebas de latencia.
- **Tráfico y diagnóstico** — Gráficas de subida y bajada en tiempo real, conexiones activas, reglas, registros del núcleo y pruebas de desbloqueo de servicios.
- **Bandeja del sistema** — Al cerrar la ventana, Flux se oculta en la bandeja. Usa su menú para salir.
- **Entorno incluido** — Las compilaciones de distribución incluyen mihomo, .NET y los componentes de Windows App SDK.

## Primeros pasos

Compatible con **Windows 10 versión 2004 (compilación 19041) o posterior**, en x86, x64 y ARM64.

1. Abre [Releases](https://github.com/1595901624/flux/releases), elige un paquete para la arquitectura de Windows y sigue las instrucciones de esa versión. Si usas un ZIP de instalación lateral, extráelo y ejecuta el archivo incluido `Install-FluxSideload.ps1` como administrador.
2. Inicia Flux y abre **Profiles**. Importa una URL de suscripción o un archivo YAML local y selecciona el perfil para activarlo.
3. En **Proxies**, elige un nodo o grupo. El modo **Rule** dirige el tráfico según las reglas del perfil.
4. Activa **System proxy** en **Home** para que las aplicaciones que respetan la configuración de proxy de Windows usen Flux.

El modo TUN requiere privilegios de administrador o un servicio privilegiado de Flux operativo. Flux sigue funcionando en la bandeja al cerrar la ventana.

Los artefactos MSIX sin firmar de CI son para pruebas de desarrollo. Si se ofrece un ZIP portátil, extráelo y ejecuta `Flux.exe`.

## Configuración y datos

Flux utiliza perfiles YAML compatibles con Clash que contienen `proxies`, `proxy-groups` y `rules`. Consulta [`sample-profile.yaml`](sample-profile.yaml). Debes aportar datos de servidor válidos o una URL de suscripción; Flux no proporciona suscripciones proxy.

La configuración, los perfiles y los registros se guardan en `%APPDATA%\flux` de forma predeterminada. En una distribución portátil situada en una carpeta con permisos de escritura, crea un archivo vacío `.config\PORTABLE` junto a `Flux.exe` antes de iniciar para guardar los datos en `.config\flux`.

## Compilar desde el código fuente

Instala el [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) en Windows. El proyecto utiliza WinUI 3 y tiene como destino Windows SDK `10.0.22621.0`. Para trabajar en un IDE, utiliza una versión de Visual Studio compatible con .NET 10 y WinUI 3.

```powershell
git clone https://github.com/1595901624/flux.git
cd flux
dotnet restore Flux.slnx
dotnet build Flux.csproj -c Release
dotnet run --project Flux.csproj -c Release --no-build
```

El destino predeterminado es `win-x64`. Usa `-r win-x86` o `-r win-arm64` para otras arquitecturas. El repositorio incluye el núcleo mihomo correspondiente a cada destino.

Ejecutar las pruebas:

```powershell
dotnet test tests/Flux.Tests/Flux.Tests.csproj -c Release
```

Consulta el [flujo de compilación](.github/workflows/build-msix.yml) para crear paquetes de distribución con el servicio privilegiado incluido.

## Licencia

Flux se distribuye bajo [GNU GPL v3.0](LICENSE). La versión de mihomo incluida, su suma de verificación, el código fuente correspondiente y la información de licencia están en [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
