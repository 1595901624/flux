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
    private readonly Func<WinInetProxySettings.State> _readState;
    private readonly Action<bool, string, string> _writeManual;
    private readonly Action<bool, string> _writePac;
    private readonly Action<WinInetProxySettings.State> _restore;
    private readonly string? _snapshotFile;
    private string SnapshotFile => _snapshotFile ?? Paths.ProxyStateFile;

    public SysProxyService()
    {
        _readState = WinInetProxySettings.Read;
        _writeManual = WinInetProxySettings.Write;
        _writePac = WinInetProxySettings.WritePac;
        _restore = WinInetProxySettings.Restore;
    }

    internal SysProxyService(string snapshotFile, Func<WinInetProxySettings.State> read,
        Action<bool, string, string> writeManual, Action<bool, string> writePac,
        Action<WinInetProxySettings.State> restore) : this()
    {
        _snapshotFile = snapshotFile;
        _readState = read;
        _writeManual = writeManual;
        _writePac = writePac;
        _restore = restore;
    }

    public static string DefaultBypass => "localhost;127.*;192.168.*;10.*;172.16.*;172.17.*;172.18.*;172.19.*;172.20.*;172.21.*;172.22.*;172.23.*;172.24.*;172.25.*;172.26.*;172.27.*;172.28.*;172.29.*;172.30.*;172.31.*;<local>";

    public void Apply(VergeConfig verge)
    {
        lock (_sync)
        {
            StopGuardUnsafe();
            if (!verge.EnableSystemProxy)
            {
                RestoreOriginalUnsafe();
                LogService.App(L10n.T("Proxy_Restored"));
                return;
            }

            var bypass = verge.UseDefaultBypass
                ? (string.IsNullOrEmpty(verge.SystemProxyBypass)
                    ? DefaultBypass
                    : DefaultBypass + ";" + verge.SystemProxyBypass)
                : verge.SystemProxyBypass;
            var server = $"127.0.0.1:{AppServices.Config.MixedPort}";
            var pacUrl = verge.EnablePacMode ? WritePacFile(verge, bypass) : null;

            var snapshot = LoadSnapshot();
            if (snapshot is null)
            {
                var original = ReadState();
                snapshot = new ProxySnapshot(original.Enable, original.Server, original.Bypass, server)
                {
                    OriginalPacEnabled = original.PacEnabled,
                    OriginalAutoConfigUrl = original.AutoConfigUrl,
                    OriginalAutoDetect = original.AutoDetect,
                };
            }
            else
            {
                snapshot.FluxServer = server;
            }
            snapshot.FluxPacUrl = pacUrl ?? "";
            SaveSnapshot(snapshot);

            if (pacUrl is not null)
            {
                _writePac(true, pacUrl);
                _lastApplied = new AppliedProxy(server, bypass, pacUrl);
                LogService.App(L10n.F("Proxy_EnabledPac", pacUrl));
            }
            else
            {
                SetProxy(new ProxyState(true, server, bypass));
                _lastApplied = new AppliedProxy(server, bypass, null);
                LogService.App(L10n.F("Proxy_Enabled", server));
            }
            StartGuardUnsafe(verge);
        }
    }

    /// <summary>生成 PAC 文件并返回其 file:// URL。</summary>
    private static string WritePacFile(VergeConfig verge, string bypass)
    {
        var port = AppServices.Config.MixedPort;
        var content = Flux.Core.Proxy.PacScriptGenerator.Generate(port, bypass.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        ConfigService.WriteAllTextAtomic(Paths.PacFile, content);
        return Paths.PacFileUrl;
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
                    if (SystemProxyOwnership.IsOwned(current.Enable, current.Server, snapshot.FluxServer) ||
                        SystemProxyOwnership.IsOwnedPac(current.PacEnabled, current.AutoConfigUrl, snapshot.FluxPacUrl))
                    {
                        RestoreSnapshot(snapshot, current);
                        LogService.App(L10n.T("Proxy_StaleRestored"), "warn");
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
                    LogService.App(L10n.T("Proxy_LegacyDisabled"), "warn");
                }
            }
            catch (Exception ex)
            {
                LogService.App(L10n.F("Proxy_StaleRestoreFailed", ex.Message), "warn");
            }
        }
    }

    public static (bool Enable, string Server) GetSystemState()
    {
        var state = WinInetProxySettings.Read();
        return (state.Enable, state.Server);
    }

    private void RestoreOriginalUnsafe()
    {
        var snapshot = LoadSnapshot();
        if (snapshot is not null)
        {
            var current = ReadState();
            var ownedManual = SystemProxyOwnership.IsOwned(current.Enable, current.Server, snapshot.FluxServer);
            var ownedPac = SystemProxyOwnership.IsOwnedPac(current.PacEnabled, current.AutoConfigUrl, snapshot.FluxPacUrl);
            if (ownedManual || ownedPac)
                RestoreSnapshot(snapshot, current);
            else
                LogService.App(L10n.T("Proxy_ChangedByOther"), "warn");
            DeleteSnapshot();
        }
        _lastApplied = null;
    }

    private void RestoreSnapshot(ProxySnapshot snapshot, ProxyState current) =>
        _restore(new WinInetProxySettings.State(snapshot.OriginalEnable, snapshot.OriginalPacEnabled,
            snapshot.OriginalServer, snapshot.OriginalBypass, snapshot.OriginalAutoConfigUrl,
            snapshot.OriginalAutoDetect ?? current.AutoDetect));

    private ProxyState ReadState()
    {
        var state = _readState();
        return new ProxyState(state.Enable, state.Server, state.Bypass, state.PacEnabled, state.AutoConfigUrl, state.AutoDetect);
    }

    private void SetProxy(ProxyState state) =>
        _writeManual(state.Enable, state.Server, state.Bypass);

    private ProxySnapshot? LoadSnapshot()
    {
        try
        {
            return File.Exists(SnapshotFile)
                ? JsonSerializer.Deserialize<ProxySnapshot>(File.ReadAllText(SnapshotFile))
                : null;
        }
        catch (Exception ex)
        {
            LogService.App(L10n.F("Proxy_SnapshotReadFailed", ex.Message), "warn");
            return null;
        }
    }

    private void SaveSnapshot(ProxySnapshot snapshot) =>
        ConfigService.WriteAllTextAtomic(SnapshotFile, JsonSerializer.Serialize(snapshot));

    private void DeleteSnapshot()
    {
        try { if (File.Exists(SnapshotFile)) File.Delete(SnapshotFile); } catch { }
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
                    var intact = last.PacUrl is not null
                        ? SystemProxyOwnership.IsOwnedPac(current.PacEnabled, current.AutoConfigUrl, last.PacUrl)
                        : SystemProxyOwnership.IsOwned(current.Enable, current.Server, last.Server) &&
                          current.Bypass == last.Bypass;
                    if (!intact)
                    {
                        LogService.App(L10n.T("Proxy_ChangedRestoring"), "warn");
                        if (last.PacUrl is not null)
                            _writePac(true, last.PacUrl);
                        else
                            SetProxy(new ProxyState(true, last.Server, last.Bypass));
                    }
                }
                catch (Exception ex)
                {
                    LogService.App(L10n.F("Proxy_GuardError", ex.Message), "warn");
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

    private sealed record ProxyState(bool Enable, string Server, string Bypass, bool PacEnabled = false,
        string AutoConfigUrl = "", bool AutoDetect = false);
    private sealed record AppliedProxy(string Server, string Bypass, string? PacUrl);
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
        public bool OriginalPacEnabled { get; set; }
        public string OriginalAutoConfigUrl { get; set; } = "";
        public bool? OriginalAutoDetect { get; set; }
        public string FluxServer { get; set; } = "";
        public string FluxPacUrl { get; set; } = "";
    }
}
