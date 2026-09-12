using H.NotifyIcon.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Flux.Models;

namespace Flux.Services;

/// <summary>
/// 系统托盘：左键显示主窗口；菜单含模式切换、订阅切换、系统代理/TUN、重启内核、退出。
/// </summary>
public class TrayService
{
    private TrayIconWithContextMenu? _tray;
    private System.Drawing.Icon? _icon;
    private DispatcherQueue? _dispatcher;

    public void Initialize()
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            Program.Trace("tray: icon load begin");
            _icon = new System.Drawing.Icon(iconPath);
            Program.Trace("tray: icon loaded");
            _tray = new TrayIconWithContextMenu
            {
                Icon = _icon.Handle,
                ToolTip = "Flux",
            };
            _tray.MessageWindow.MouseEventReceived += OnMouseEvent;
            Program.Trace("tray: create begin");
            _tray.Create();
            Program.Trace("tray: created");
            RebuildMenu();
            Program.Trace("tray: menu rebuilt");
            AppServices.Config.RuntimeInvalidated += RebuildMenuOnUiThread;
            AppServices.Subscription.ProfilesChanged += RebuildMenuOnUiThread;
        }
        catch (Exception ex)
        {
            LogService.App(L10n.F("Tray_InitFailed", ex.Message), "warn");
        }
    }

    public void UpdateToolTip(string text)
    {
        try { _tray?.UpdateToolTip(text); } catch { }
    }

    private void OnMouseEvent(object? sender, MessageWindow.MouseEventReceivedEventArgs e)
    {
        var mouse = e.MouseEvent.ToString();
        if (mouse.Contains("IconLeftMouseUp", StringComparison.OrdinalIgnoreCase) ||
            mouse.Contains("IconDoubleMouseDown", StringComparison.OrdinalIgnoreCase))
        {
            _dispatcher?.TryEnqueue(() => App.ShowMainWindow());
        }
    }

    // ---------- 菜单 ----------

    private void RebuildMenuOnUiThread() => _dispatcher?.TryEnqueue(RebuildMenu);

    public void RebuildMenu() => RebuildMenu(skipProxyRefresh: false);

    private void RebuildMenu(bool skipProxyRefresh)
    {
        if (_tray is null) return;
        try
        {
            var verge = AppServices.Config.Verge;
            var mode = AppServices.Config.Mode;

            var menu = new PopupMenu
            {
                Items =
                {
                    new PopupMenuItem(L10n.T("Tray_ShowWindow"), (_, _) => _dispatcher?.TryEnqueue(() => App.ShowMainWindow())),
                    new PopupMenuSeparator(),
                    CreateModeItem(L10n.T("Tray_ModeRule"), "rule", mode),
                    CreateModeItem(L10n.T("Tray_ModeGlobal"), "global", mode),
                    CreateModeItem(L10n.T("Tray_ModeDirect"), "direct", mode),
                    new PopupMenuSeparator(),
                    CreateProfilesSubMenu(),
                    CreateProxiesSubMenu(),
                    new PopupMenuSeparator(),
                    new PopupMenuItem(L10n.T("Tray_SystemProxy"), (_, _) => _dispatcher?.TryEnqueue(async () => await ToggleSystemProxyAsync()))
                        { Checked = verge.EnableSystemProxy },
                    new PopupMenuItem(L10n.T("Tray_TunMode"), (_, _) => _dispatcher?.TryEnqueue(async () => await ToggleTunAsync()))
                        { Checked = verge.EnableTunMode },
                    new PopupMenuItem(L10n.T("Tray_Lightweight"), (_, _) => _dispatcher?.TryEnqueue(() =>
                    {
                        if (LightweightManager.IsLightweight) LightweightManager.Exit();
                        else LightweightManager.Enter();
                    }))
                        { Checked = LightweightManager.IsLightweight, Enabled = true },
                    new PopupMenuSeparator(),
                    new PopupMenuItem(L10n.T("Tray_RestartCore"), (_, _) => _dispatcher?.TryEnqueue(async () =>
                        await AppServices.Core.RestartAsync())),
                    new PopupMenuItem(L10n.T("Tray_Exit"), (_, _) => _dispatcher?.TryEnqueue(ExitApp)),
                }
            };
            _tray.ContextMenu = menu;
            if (!skipProxyRefresh) RefreshProxyGroupsCache();
        }
        catch (Exception ex)
        {
            LogService.App(L10n.F("Tray_MenuRefreshFailed", ex.Message), "warn");
        }
    }

    /// <summary>最近一次获取的代理组快照（托盘菜单使用，限制条目数防止菜单过长）。</summary>
    private IReadOnlyList<(string Name, IReadOnlyList<string> Nodes, string? Now)> _proxyGroupsCache = [];
    private bool _refreshingProxyGroups;

    private PopupSubMenu CreateProxiesSubMenu()
    {
        var sub = new PopupSubMenu(L10n.T("Tray_ProxyGroups"));
        if (_proxyGroupsCache.Count == 0)
        {
            sub.Items.Add(new PopupMenuItem(L10n.T("Tray_CoreNotRunning"), (_, _) => { }) { Enabled = false });
            return sub;
        }
        foreach (var (group, nodes, now) in _proxyGroupsCache)
        {
            var groupName = group;
            foreach (var node in nodes)
            {
                var nodeName = node;
                sub.Items.Add(new PopupMenuItem($"{groupName}: {nodeName}", (_, _) =>
                    _dispatcher?.TryEnqueue(() => _ = SelectNodeAsync(groupName, nodeName)))
                {
                    Checked = string.Equals(now, nodeName, StringComparison.Ordinal)
                });
            }
        }
        return sub;
    }

    private async Task SelectNodeAsync(string group, string node)
    {
        try
        {
            await AppServices.Api.SelectProxyAsync(group, node);
            AppServices.Config.SaveCurrentProxySelection(group, node);
            _proxyGroupsCache = _proxyGroupsCache
                .Select(g => g.Name == group
                    ? (g.Name, g.Nodes, (string?)node)
                    : g)
                .ToList();
            RebuildMenuOnUiThread();
        }
        catch (Exception ex)
        {
            LogService.App(L10n.F("Tray_ToggleNodeFailed", ex.Message), "warn");
        }
    }

    /// <summary>异步刷新代理组快照后重建菜单（对齐参考项目托盘的动态节点菜单）。</summary>
    private bool _suppressProxyRefresh;

    private void RefreshProxyGroupsCache()
    {
        if (_refreshingProxyGroups || _suppressProxyRefresh || !AppServices.Core.IsRunning) return;
        _refreshingProxyGroups = true;
        _ = Task.Run(async () =>
        {
            try
            {
                var json = await AppServices.Api.GetProxiesAsync();
                if (!json.TryGetProperty("proxies", out var proxies) ||
                    proxies.ValueKind != System.Text.Json.JsonValueKind.Object)
                    return;

                var groups = new List<(string, IReadOnlyList<string>, string?)>();
                foreach (var p in proxies.EnumerateObject())
                {
                    if (p.Value.ValueKind != System.Text.Json.JsonValueKind.Object) continue;
                    var type = p.Value.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
                    if (type is not ("Selector" or "URLTest" or "Fallback" or "LoadBalance" or "Relay"))
                        continue;
                    var now = p.Value.TryGetProperty("now", out var n) ? n.GetString() : null;
                    var nodes = new List<string>();
                    if (p.Value.TryGetProperty("all", out var all) && all.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var node in all.EnumerateArray())
                        {
                            var name = node.GetString();
                            if (!string.IsNullOrEmpty(name)) nodes.Add(name);
                        }
                    }
                    // 菜单条目上限：每组最多展示 20 个节点
                    groups.Add((p.Name, nodes.Take(20).ToList(), now));
                }

                if (groups.Count > 8) groups = groups.Take(8).ToList();
                _proxyGroupsCache = groups;
                // 刷新完成后的重建不再触发新的刷新，避免无限循环
                _dispatcher?.TryEnqueue(() =>
                {
                    _suppressProxyRefresh = true;
                    try { RebuildMenu(skipProxyRefresh: true); }
                    finally { _suppressProxyRefresh = false; }
                });
            }
            catch { }
            finally { _refreshingProxyGroups = false; }
        });
    }

    private PopupSubMenu CreateProfilesSubMenu()
    {
        var sub = new PopupSubMenu(L10n.T("Tray_Profiles"));
        var profiles = AppServices.Config.Profiles;
        foreach (var item in profiles.Items)
        {
            var uid = item.Uid;
            sub.Items.Add(new PopupMenuItem(item.Name, (_, _) =>
                _dispatcher?.TryEnqueue(() => _ = SelectProfileAsync(uid)))
            {
                Checked = profiles.Current == uid
            });
        }
        if (profiles.Items.Count == 0)
        {
            sub.Items.Add(new PopupMenuItem(L10n.T("Tray_NoProfiles"), (_, _) => { }) { Enabled = false });
        }
        return sub;
    }

    private async Task SelectProfileAsync(string uid)
    {
        try
        {
            await AppServices.Subscription.SelectAsync(uid);
        }
        catch (Exception ex)
        {
            LogService.App(L10n.F("Tray_ProfileSwitchFailed", ex.Message), "warn");
        }
    }

    private PopupMenuItem CreateModeItem(string text, string mode, string currentMode) =>
        new(text, (_, _) => _ = SetModeAsync(mode)) { Checked = currentMode == mode };

    private async Task SetModeAsync(string mode)
    {
        try
        {
            await AppServices.Api.PatchConfigsAsync(new() { ["mode"] = mode });
            AppServices.Config.PatchClashBase("mode", mode);
            if (AppServices.Config.Verge.AutoCloseConnection)
                await AppServices.Api.CloseAllConnectionsAsync();
        }
        catch (Exception ex)
        {
            LogService.App(L10n.F("Tray_ModeSwitchFailed", ex.Message), "warn");
        }
    }

    // ---------- 动作 ----------

    private async Task ToggleSystemProxyAsync()
    {
        var verge = AppServices.Config.Verge;
        var previous = verge.EnableSystemProxy;
        verge.EnableSystemProxy = !verge.EnableSystemProxy;
        AppServices.Config.SaveVerge();
        try
        {
            await Task.Run(() => AppServices.SysProxy.Apply(verge));
        }
        catch (Exception ex)
        {
            verge.EnableSystemProxy = previous;
            AppServices.Config.SaveVerge();
            LogService.App(L10n.F("Tray_SysProxyToggleFailed", ex.Message), "error");
            ShowNotification(L10n.F("Tray_SysProxyToggleFailed", ex.Message));
        }
        finally { RebuildMenu(); }
    }

    private async Task ToggleTunAsync()
    {
        if (!IsElevated())
        {
            ShowNotification(L10n.T("VM_TunNeedAdmin"));
            return;
        }
        var verge = AppServices.Config.Verge;
        var previous = verge.EnableTunMode;
        verge.EnableTunMode = !verge.EnableTunMode;
        AppServices.Config.SaveVerge();
        if (!await AppServices.Core.ApplyConfigAsync())
        {
            verge.EnableTunMode = previous;
            AppServices.Config.SaveVerge();
            ShowNotification(L10n.T("VM_TunRejected"));
        }
        RebuildMenu();
    }

    public static bool IsElevated()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(identity)
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    public void ShowNotification(string message)
    {
        try { _tray?.ShowNotification("Flux", message, NotificationIcon.Info); }
        catch { }
    }

    public void ExitApp()
    {
        _ = Task.Run(async () =>
        {
            try { await AppServices.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(8)); }
            catch (Exception ex) { LogService.App(L10n.F("Tray_ExitCleanupIncomplete", ex.Message), "warn"); }
            finally { Environment.Exit(0); }
        });
    }
}
