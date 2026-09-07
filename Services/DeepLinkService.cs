using Microsoft.Win32;

namespace Flux.Services;

/// <summary>
/// clash:// 深链协议（clash://install-config?url=...&name=...）：
/// - HKCU 注册协议（免管理员）
/// - 处理二次实例转发来的参数
/// </summary>
public class DeepLinkService
{
    private const string Scheme = "clash";
    public static bool IsSupportedUri(string value) =>
        DeepLinkParser.IsSupportedUri(value);

    public void Initialize()
    {
        try
        {
            RegisterScheme();
        }
        catch (Exception ex)
        {
            LogService.App("协议注册失败: " + ex.Message, "warn");
        }
    }

    private static void RegisterScheme()
    {
        var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Flux.exe");
        using var key = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{Scheme}");
        key.SetValue("", "URL:Flux Clash Protocol");
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
            LogService.App("深链导入失败: " + ex.Message, "error");
        }
    }

    internal static bool TryParseInstallUri(string value, out string importUrl, out string? name)
        => DeepLinkParser.TryParseInstallUri(value, out importUrl, out name);
}
