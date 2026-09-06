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
            _icon = new System.Drawing.Icon(iconPath);
            _tray = new TrayIconWithContextMenu
            {
                Icon = _icon.Handle,
                ToolTip = "Flux",
            };
            _tray.MessageWindow.MouseEventReceived += OnMouseEvent;
            _tray.Create();
            RebuildMenu();
            AppServices.Config.RuntimeInvalidated += RebuildMenuOnUiThread;
        }
        catch (Exception ex)
        {
            LogService.App("托盘初始化失败: " + ex.Message, "warn");
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

    public void RebuildMenu()
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
                    new PopupMenuItem("显示主窗口", (_, _) => _dispatcher?.TryEnqueue(() => App.ShowMainWindow())),
                    new PopupMenuSeparator(),
                    CreateModeItem("规则模式", "rule", mode),
                    CreateModeItem("全局模式", "global", mode),
                    CreateModeItem("直连模式", "direct", mode),
                    new PopupMenuSeparator(),
                    CreateProfilesSubMenu(),
                    new PopupMenuSeparator(),
                    new PopupMenuItem("系统代理", (_, _) => _dispatcher?.TryEnqueue(async () => await ToggleSystemProxyAsync()))
                        { Checked = verge.EnableSystemProxy },
                    new PopupMenuItem("TUN 模式", (_, _) => _dispatcher?.TryEnqueue(async () => await ToggleTunAsync()))
                        { Checked = verge.EnableTunMode },
                    new PopupMenuSeparator(),
                    new PopupMenuItem("重启内核", (_, _) => _dispatcher?.TryEnqueue(async () =>
                        await AppServices.Core.RestartAsync())),
                    new PopupMenuItem("退出", (_, _) => _dispatcher?.TryEnqueue(ExitApp)),
                }
            };
            _tray.ContextMenu = menu;
        }
        catch (Exception ex)
        {
            LogService.App("托盘菜单刷新失败: " + ex.Message, "warn");
        }
    }

    private PopupSubMenu CreateProfilesSubMenu()
    {
        var sub = new PopupSubMenu("订阅");
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
            sub.Items.Add(new PopupMenuItem("(暂无订阅)", null) { Enabled = false });
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
            LogService.App("订阅切换失败: " + ex.Message, "warn");
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
            LogService.App("模式切换失败: " + ex.Message, "warn");
        }
    }

    // ---------- 动作 ----------

    private async Task ToggleSystemProxyAsync()
    {
        var verge = AppServices.Config.Verge;
        verge.EnableSystemProxy = !verge.EnableSystemProxy;
        AppServices.Config.SaveVerge();
        AppServices.SysProxy.Apply(verge);
        RebuildMenu();
        await Task.CompletedTask;
    }

    private async Task ToggleTunAsync()
    {
        if (!IsElevated())
        {
            ShowNotification("TUN 模式需要以管理员身份运行应用");
            return;
        }
        var verge = AppServices.Config.Verge;
        verge.EnableTunMode = !verge.EnableTunMode;
        AppServices.Config.SaveVerge();
        await AppServices.Core.ApplyConfigAsync();
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
            await AppServices.ShutdownAsync();
            Environment.Exit(0);
        });
    }
}
