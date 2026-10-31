using Microsoft.Win32;

namespace Flux.Services;

/// <summary>
/// Flux 导入协议（flux://install-config?url=...&name=...），兼容 Clash 导入链接：
/// - HKCU 注册协议（免管理员）
/// - 处理二次实例转发来的参数
/// </summary>
public class DeepLinkService
{
    private const string Scheme = "flux";
    public static bool IsSupportedUri(string value) =>
        DeepLinkParser.IsSupportedUri(value);

    public void Initialize()
    {
        // MSIX 版本由 Package.appxmanifest 注册协议；仅便携/开发版写入 HKCU。
        if (PackageIdentity.IsPackaged) return;

        try
        {
            RegisterScheme(Scheme);
            RegisterScheme("clash");
        }
        catch (Exception ex)
        {
            LogService.App(L10n.F("DeepLink_RegisterFailed", ex.Message), "warn");
        }
    }

    private static void RegisterScheme(string scheme)
    {
        var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Flux.exe");
        using var key = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{scheme}");
        key.SetValue("", "URL:Flux Import Protocol");
        key.SetValue("URL Protocol", "");
        using var cmd = key.CreateSubKey(@"shell\open\command");
        cmd.SetValue("", $"\"{exe}\" \"%1\"");
    }

    /// <summary>处理启动参数（首次启动 argv 或二次实例转发）。</summary>
    public void HandleArgs(string[] args)
    {
        foreach (var arg in args)
        {
            if (IsSupportedUri(arg))
            {
                _ = HandleSchemeAsync(arg);
            }
        }
    }

    private async Task HandleSchemeAsync(string url)
    {
        try
        {
            if (!TryParseInstallUri(url, out var importUrl, out var name)) return;

            App.ShowMainWindow();
            await AppServices.Subscription.ImportAsync(importUrl, name);
            await AppServices.Core.ApplyConfigAsync();
        }
        catch (Exception ex)
        {
            LogService.App(L10n.F("DeepLink_ImportFailed", ex.Message), "error");
        }
    }

    internal static bool TryParseInstallUri(string value, out string importUrl, out string? name)
        => DeepLinkParser.TryParseInstallUri(value, out importUrl, out name);
}
