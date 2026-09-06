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
            if (arg.StartsWith("clash://", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("clash-verge://", StringComparison.OrdinalIgnoreCase))
            {
                _ = HandleSchemeAsync(arg);
            }
        }
    }

    private async Task HandleSchemeAsync(string url)
    {
        try
        {
            // clash://install-config?url=<encoded>&name=<encoded>
            var schemeEnd = url.IndexOf("://", StringComparison.Ordinal) + 3;
            var parts = url[schemeEnd..].Split('/', 2);
            var query = parts.Length > 1 ? parts[1] : parts[0];
            var qIndex = query.IndexOf('?');
            if (qIndex >= 0) query = query[(qIndex + 1)..];

            string? importUrl = null, name = null;
            foreach (var kv in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = kv.Split('=', 2);
                if (pair.Length != 2) continue;
                if (pair[0] == "url") importUrl = Uri.UnescapeDataString(pair[1]);
                if (pair[0] == "name") name = Uri.UnescapeDataString(pair[1]);
            }
            if (string.IsNullOrEmpty(importUrl)) return;

            // 嵌套 URL 最多解码 2 层
            if (importUrl.StartsWith("clash%3A") || importUrl.StartsWith("http%3A"))
                importUrl = Uri.UnescapeDataString(importUrl);

            App.ShowMainWindow();
            await AppServices.Subscription.ImportAsync(importUrl, name);
            await AppServices.Core.ApplyConfigAsync();
        }
        catch (Exception ex)
        {
            LogService.App("深链导入失败: " + ex.Message, "error");
        }
    }
}
