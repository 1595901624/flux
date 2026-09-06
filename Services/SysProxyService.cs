using Microsoft.Win32;
using System.Runtime.InteropServices;
using Flux.Models;

namespace Flux.Services;

/// <summary>
/// Windows 系统代理（WinINET）：注册表写入 + InternetSetOption 刷新生效；含代理守护。
/// </summary>
public class SysProxyService
{
    private const string InternetSettingsKey = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    private (bool Enable, string Server, string Bypass)? _lastApplied;

    [DllImport("wininet.dll", SetLastError = true)]
    private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

    private const int INTERNET_OPTION_REFRESH = 37;
    private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;

    public static string DefaultBypass => "localhost;127.*;192.168.*;10.*;172.16.*;172.17.*;172.18.*;172.19.*;172.20.*;172.21.*;172.22.*;172.23.*;172.24.*;172.25.*;172.26.*;172.27.*;172.28.*;172.29.*;172.30.*;172.31.*;<local>";

    /// <summary>按 verge 设置应用或清除系统代理。</summary>
    public void Apply(VergeConfig verge)
    {
        var port = AppServices.Config.MixedPort;
        if (verge.EnableSystemProxy)
        {
            var bypass = verge.UseDefaultBypass
                ? (string.IsNullOrEmpty(verge.SystemProxyBypass)
                    ? DefaultBypass
                    : DefaultBypass + ";" + verge.SystemProxyBypass)
                : verge.SystemProxyBypass;
            var server = $"127.0.0.1:{port}";
            SetProxy(true, server, bypass);
            _lastApplied = (true, server, bypass);
            LogService.App($"系统代理已开启: {server}");
        }
        else
        {
            SetProxy(false, "", "");
            _lastApplied = null;
            LogService.App("系统代理已关闭");
        }
        StartGuard(verge);
    }

    public void Reset()
    {
        StopGuard();
        SetProxy(false, "", "");
        _lastApplied = null;
    }

    /// <summary>当前系统状态（供首页/设置显示）。</summary>
    public static (bool Enable, string Server) GetSystemState()
    {
        using var key = Registry.CurrentUser.OpenSubKey(InternetSettingsKey);
        var enable = (key?.GetValue("ProxyEnable") as int?) == 1;
        var server = key?.GetValue("ProxyServer") as string ?? "";
        return (enable, server);
    }

    private static void SetProxy(bool enable, string server, string bypass)
    {
        using var key = Registry.CurrentUser.OpenSubKey(InternetSettingsKey, writable: true)
            ?? throw new InvalidOperationException("无法打开 Internet Settings 注册表");
        key.SetValue("ProxyEnable", enable ? 1 : 0, RegistryValueKind.DWord);
        if (enable)
        {
            key.SetValue("ProxyServer", server, RegistryValueKind.String);
            key.SetValue("ProxyOverride", bypass, RegistryValueKind.String);
        }
        Refresh();
    }

    private static void Refresh()
    {
        InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
        InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
    }

    // ---------- 代理守护 ----------

    private System.Threading.Timer? _guardTimer;

    public void StartGuard(VergeConfig verge)
    {
        StopGuard();
        if (!verge.EnableSystemProxy || !verge.EnableProxyGuard) return;

        // 守护只操作注册表，无 UI 依赖；可能在后台线程调用，不能用 DispatcherQueueTimer
        _lastApplied ??= (true, $"127.0.0.1:{AppServices.Config.MixedPort}", DefaultBypass);
        _guardTimer = new System.Threading.Timer(_ =>
        {
            try
            {
                if (_lastApplied is { } last)
                {
                    var (enable, server) = GetSystemState();
                    if (!enable || server != last.Server)
                    {
                        LogService.App("检测到系统代理被修改，正在恢复", "warn");
                        SetProxy(true, last.Server, last.Bypass);
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.App("代理守护异常: " + ex.Message, "warn");
            }
        }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public void StopGuard()
    {
        _guardTimer?.Dispose();
        _guardTimer = null;
    }
}
