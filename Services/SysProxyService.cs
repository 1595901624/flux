using System.Text.Json;
using Flux.Models;

namespace Flux.Services;

/// <summary>
/// Windows 系统代理（WinINET）。开启前持久化原始状态，正常退出和异常退出后的下次启动
/// 都只在当前代理仍由 Flux 管理时恢复，避免覆盖用户或其他代理软件的设置。
/// </summary>
public class SysProxyService
{
    private readonly object _sync = new();
    private AppliedProxy? _lastApplied;
    private System.Threading.Timer? _guardTimer;

    public static string DefaultBypass => "localhost;127.*;192.168.*;10.*;172.16.*;172.17.*;172.18.*;172.19.*;172.20.*;172.21.*;172.22.*;172.23.*;172.24.*;172.25.*;172.26.*;172.27.*;172.28.*;172.29.*;172.30.*;172.31.*;<local>";

    public void Apply(VergeConfig verge)
    {
        lock (_sync)
        {
            StopGuardUnsafe();
            if (!verge.EnableSystemProxy)
            {
                RestoreOriginalUnsafe();
                LogService.App("系统代理已恢复");
                return;
            }

            var bypass = verge.UseDefaultBypass
                ? (string.IsNullOrEmpty(verge.SystemProxyBypass)
                    ? DefaultBypass
                    : DefaultBypass + ";" + verge.SystemProxyBypass)
                : verge.SystemProxyBypass;
            var server = $"127.0.0.1:{AppServices.Config.MixedPort}";

            var snapshot = LoadSnapshot();
            if (snapshot is null)
            {
                var original = ReadState();
                snapshot = new ProxySnapshot(original.Enable, original.Server, original.Bypass, server);
            }
            else
            {
                snapshot.FluxServer = server;
            }
            SaveSnapshot(snapshot);

            SetProxy(new ProxyState(true, server, bypass));
            _lastApplied = new AppliedProxy(server, bypass);
            StartGuardUnsafe(verge);
            LogService.App($"系统代理已开启: {server}");
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            StopGuardUnsafe();
            RestoreOriginalUnsafe();
        }
    }

    /// <summary>恢复上次异常退出前保存的代理；兼容旧版本只记录当前端口的残留状态。</summary>
    public void ClearStaleProxy()
    {
        lock (_sync)
        {
            try
            {
                if (LoadSnapshot() is { } snapshot)
                {
                    var current = ReadState();
                    if (SystemProxyOwnership.IsOwned(current.Enable, current.Server, snapshot.FluxServer))
                    {
                        SetProxy(new ProxyState(snapshot.OriginalEnable, snapshot.OriginalServer, snapshot.OriginalBypass));
                        LogService.App("检测到上次未恢复的系统代理，已恢复原始设置", "warn");
                    }
                    DeleteSnapshot();
                    _lastApplied = null;
                    return;
                }

                var state = ReadState();
                if (AppServices.Config.Verge.EnableSystemProxy &&
                    SystemProxyOwnership.IsOwned(state.Enable, state.Server, $"127.0.0.1:{AppServices.Config.MixedPort}"))
                {
                    SetProxy(new ProxyState(false, "", ""));
                    LogService.App("检测到旧版本遗留的系统代理，已自动关闭", "warn");
                }
            }
            catch (Exception ex)
            {
                LogService.App("恢复遗留系统代理失败: " + ex.Message, "warn");
            }
        }
    }

    public static (bool Enable, string Server) GetSystemState()
    {
        var state = ReadState();
        return (state.Enable, state.Server);
    }

    private void RestoreOriginalUnsafe()
    {
        var snapshot = LoadSnapshot();
        if (snapshot is not null)
        {
            var current = ReadState();
            if (SystemProxyOwnership.IsOwned(current.Enable, current.Server, snapshot.FluxServer))
                SetProxy(new ProxyState(snapshot.OriginalEnable, snapshot.OriginalServer, snapshot.OriginalBypass));
            else
                LogService.App("系统代理已被其他程序修改，Flux 不再覆盖该设置", "warn");
            DeleteSnapshot();
        }
        _lastApplied = null;
    }

    private static ProxyState ReadState()
    {
        var state = WinInetProxySettings.Read();
        return new ProxyState(state.Enable, state.Server, state.Bypass);
    }

    private static void SetProxy(ProxyState state) =>
        WinInetProxySettings.Write(state.Enable, state.Server, state.Bypass);

    private static ProxySnapshot? LoadSnapshot()
    {
        try
        {
            return File.Exists(Paths.ProxyStateFile)
                ? JsonSerializer.Deserialize<ProxySnapshot>(File.ReadAllText(Paths.ProxyStateFile))
                : null;
        }
        catch (Exception ex)
        {
            LogService.App("系统代理快照读取失败: " + ex.Message, "warn");
            return null;
        }
    }

    private static void SaveSnapshot(ProxySnapshot snapshot) =>
        ConfigService.WriteAllTextAtomic(Paths.ProxyStateFile, JsonSerializer.Serialize(snapshot));

    private static void DeleteSnapshot()
    {
        try { if (File.Exists(Paths.ProxyStateFile)) File.Delete(Paths.ProxyStateFile); } catch { }
    }

    private void StartGuardUnsafe(VergeConfig verge)
    {
        if (!verge.EnableProxyGuard || _lastApplied is null) return;
        _guardTimer = new System.Threading.Timer(_ =>
        {
            lock (_sync)
            {
                try
                {
                    if (_lastApplied is not { } last) return;
                    var current = ReadState();
                    if (!SystemProxyOwnership.IsOwned(current.Enable, current.Server, last.Server) ||
                        current.Bypass != last.Bypass)
                    {
                        LogService.App("检测到系统代理被修改，正在恢复", "warn");
                        SetProxy(new ProxyState(true, last.Server, last.Bypass));
                    }
                }
                catch (Exception ex)
                {
                    LogService.App("代理守护异常: " + ex.Message, "warn");
                }
            }
        }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public void StartGuard(VergeConfig verge)
    {
        lock (_sync)
        {
            StopGuardUnsafe();
            StartGuardUnsafe(verge);
        }
    }

    public void StopGuard()
    {
        lock (_sync) StopGuardUnsafe();
    }

    private void StopGuardUnsafe()
    {
        _guardTimer?.Dispose();
        _guardTimer = null;
    }

    private sealed record ProxyState(bool Enable, string Server, string Bypass);
    private sealed record AppliedProxy(string Server, string Bypass);
    private sealed class ProxySnapshot
    {
        public ProxySnapshot() { }
        public ProxySnapshot(bool originalEnable, string originalServer, string originalBypass, string fluxServer)
        {
            OriginalEnable = originalEnable;
            OriginalServer = originalServer;
            OriginalBypass = originalBypass;
            FluxServer = fluxServer;
        }

        public bool OriginalEnable { get; set; }
        public string OriginalServer { get; set; } = "";
        public string OriginalBypass { get; set; } = "";
        public string FluxServer { get; set; } = "";
    }
}
