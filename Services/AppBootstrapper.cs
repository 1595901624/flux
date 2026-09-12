using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Flux.Services;

/// <summary>
/// 应用启动引导：初始化目录与配置 → 启动内核 → 初始化托盘 → 显示主窗口 → 应用系统代理。
/// </summary>
public static class AppBootstrapper
{
    public static bool Started { get; private set; }
    public static bool StartFailed { get; private set; }
    public static string StartError { get; private set; } = "";

    private static DispatcherQueue? _uiDispatcher;

    public static async Task StartAsync()
    {
        if (Started) return;
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        _uiDispatcher = dispatcher;

        Program.Trace("bootstrapper enter");
        try
        {
            AppServices.Initialize();
            Program.Trace("initialize ok");

            // 语言覆盖必须在窗口创建前生效（x:Uid 解析依赖）
            var lang = AppServices.Config.Verge.Language;
            try
            {
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride =
                    string.IsNullOrEmpty(lang) || lang == "system" ? "" : lang;
                L10n.Reset();
            }
            catch (Exception ex) { LogService.App(L10n.F("Boot_LanguageFailed", ex.Message), "warn"); }

            Program.Trace("lang ok");
            // 上次异常退出可能遗留指向本端口的系统代理（内核已死，代理会断网），先恢复
            AppServices.SysProxy.ClearStaleProxy();
            Program.Trace("stale-proxy ok");

            // 已保存的 TUN 状态只在有能力特权运行内核时生效：
            // 管理员进程或 Flux 服务可用（普通用户经服务模式 TUN），否则安全关闭避免断网。
            if (AppServices.Config.Verge.EnableTunMode && !TrayService.IsElevated()
                && !AppServices.Privilege.IsServiceReady())
            {
                AppServices.Config.Verge.EnableTunMode = false;
                AppServices.Config.SaveVerge();
                LogService.App(L10n.T("Boot_TunDisabledNoPrivilege"), "warn");
            }

            // 深链 / 二次实例转发参数
            AppServices.DeepLink.Initialize();
            SingleInstance.ForwardedArgsReceived += (_, args) =>
                dispatcher.TryEnqueue(() =>
                {
                    App.ShowMainWindow();
                    AppServices.DeepLink.HandleArgs(args);
                });

            Program.Trace("pre-core");
            var coreReady = await CoreServiceStartupAsync();
            Program.Trace("core started: " + coreReady);

            Program.Trace("tray init");
            AppServices.Tray.Initialize();
            App.ShowMainWindow();
            App.ApplyTheme(AppServices.Config.Verge.ThemeMode);

            // 静默启动：--silent 参数或设置开启时隐藏窗口
            var silentByArg = Program.Args.Any(a => a.Equals("--silent", StringComparison.OrdinalIgnoreCase));
            var hasDeepLink = Program.Args.Any(DeepLinkService.IsSupportedUri);
            if ((AppServices.Config.Verge.EnableSilentStart || silentByArg) && !hasDeepLink)
                App.MainWindow?.AppWindow.Hide();

            AppServices.DeepLink.HandleArgs(Program.Args);
            await ApplyStartupStateAsync(coreReady);

            Started = true;
        }
        catch (Exception ex)
        {
            StartFailed = true;
            StartError = ex.Message;
            LogService.App(L10n.F("Boot_StartFailed", ex.Message), "error");
            dispatcher.TryEnqueue(() => App.ShowMainWindow()); // 失败也显示窗口供查看日志
        }
    }

    /// <summary>启动内核并订阅实时数据流。</summary>
    private static async Task<bool> CoreServiceStartupAsync()
    {
        AppServices.Core.CoreStarted += AppServices.Streams.Start;
        try
        {
            await AppServices.Core.StartAsync();
            return true;
        }
        catch (Exception ex)
        {
            LogService.App(L10n.F("Boot_CoreStartFailed", ex.Message), "error");
            StartFailed = true;
            StartError = ex.Message;
            try { await AppServices.Core.StopAsync(); } catch { }
            return false;
        }
    }

    /// <summary>应用启动时状态：系统代理、自动更新订阅定时器。</summary>
    private static async Task ApplyStartupStateAsync(bool coreReady)
    {
        try
        {
            if (AppServices.Config.Verge.EnableSystemProxy && coreReady)
                AppServices.SysProxy.Apply(AppServices.Config.Verge);
            else if (AppServices.Config.Verge.EnableSystemProxy)
                LogService.App(L10n.T("Boot_SkipProxyNoCore"), "warn");
        }
        catch (Exception ex)
        {
            LogService.App(L10n.F("Boot_SysProxyApplyFailed", ex.Message), "warn");
        }

        AppServices.Subscription.StartAutoUpdateTimer();
        InitializeHotkeys();
        await Task.CompletedTask;
    }

    /// <summary>初始化全局热键：注册失败（冲突）的组合写入日志并对用户可见。</summary>
    private static void InitializeHotkeys()
    {
        try
        {
            var hotkey = AppServices.Hotkey;
            hotkey.Dispatcher = action => _uiDispatcher?.TryEnqueue(() => action());
            hotkey.HotkeyPressed += OnHotkeyPressed;
            LightweightManager.RunOnUiThread = action => _uiDispatcher?.TryEnqueue(() => action());
            var failures = hotkey.ApplyHotkeys(AppServices.Config.Verge.Hotkeys).Value ?? [];
            foreach (var failure in failures)
                LogService.App(L10n.F("Boot_HotkeyRegisterFailed", failure), "warn");
        }
        catch (Exception ex)
        {
            LogService.App(L10n.F("Boot_HotkeyInitFailed", ex.Message), "warn");
        }
    }

    private static void OnHotkeyPressed(string action)
    {
        _ = HandleHotkeyAsync(action);
    }

    private static async Task HandleHotkeyAsync(string action)
    {
        try
        {
            var verge = AppServices.Config.Verge;
            switch (action)
            {
                case "mode_rule":
                case "mode_global":
                case "mode_direct":
                {
                    var mode = action["mode_".Length..];
                    await AppServices.Api.PatchConfigsAsync(new Dictionary<string, object> { ["mode"] = mode });
                    AppServices.Config.Mode = mode;
                    LogService.App(L10n.F("Boot_HotkeyModeSwitch", mode));
                    break;
                }
                case "toggle_system_proxy":
                    verge.EnableSystemProxy = !verge.EnableSystemProxy;
                    AppServices.Config.SaveVerge();
                    AppServices.SysProxy.Apply(verge);
                    break;
                case "toggle_tun":
                {
                    var previous = verge.EnableTunMode;
                    var next = !previous;
                    if (next && !TrayService.IsElevated() && !AppServices.Privilege.IsServiceReady())
                    {
                        LogService.App(L10n.T("VM_TunNeedAdmin"), "warn");
                        break;
                    }
                    verge.EnableTunMode = next;
                    AppServices.Config.SaveVerge();
                    if (!await AppServices.Core.ApplyConfigAsync())
                    {
                        verge.EnableTunMode = previous;
                        AppServices.Config.SaveVerge();
                    }
                    break;
                }
                case "show_hide_window":
                    App.ToggleMainWindowVisibility();
                    break;
                case "lightweight_mode":
                    LightweightManager.Enter();
                    break;
                case "reactivate_profile":
                    if (AppServices.Config.Profiles.Current is { } uid)
                        await AppServices.Subscription.SelectAsync(uid);
                    break;
                default:
                    LogService.App(L10n.F("Boot_HotkeyNotImplemented", action), "warn");
                    break;
            }
        }
        catch (Exception ex)
        {
            LogService.App(L10n.F("Boot_HotkeyExecFailed", action, ex.Message), "warn");
        }
    }
}
