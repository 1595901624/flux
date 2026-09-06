using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Vxn.Services;

/// <summary>
/// 应用启动引导：初始化目录与配置 → 启动内核 → 初始化托盘 → 显示主窗口 → 应用系统代理。
/// </summary>
public static class AppBootstrapper
{
    public static bool Started { get; private set; }
    public static bool StartFailed { get; private set; }
    public static string StartError { get; private set; } = "";

    public static async Task StartAsync()
    {
        if (Started) return;
        var dispatcher = DispatcherQueue.GetForCurrentThread();

        try
        {
            AppServices.Initialize();

            // 深链 / 二次实例转发参数
            AppServices.DeepLink.Initialize();
            SingleInstance.ForwardedArgsReceived += (_, args) =>
                dispatcher.TryEnqueue(() => AppServices.DeepLink.HandleArgs(args));

            await CoreServiceStartupAsync();

            AppServices.Tray.Initialize();
            App.ShowMainWindow();
            App.ApplyTheme(AppServices.Config.Verge.ThemeMode);

            // 静默启动：--silent 参数或设置开启时隐藏窗口
            var silentByArg = Program.Args.Any(a => a.Equals("--silent", StringComparison.OrdinalIgnoreCase));
            if (AppServices.Config.Verge.EnableSilentStart || silentByArg)
                App.MainWindow?.AppWindow.Hide();

            await ApplyStartupStateAsync();

            Started = true;
        }
        catch (Exception ex)
        {
            StartFailed = true;
            StartError = ex.Message;
            LogService.App("启动失败: " + ex, "error");
            dispatcher.TryEnqueue(() => App.ShowMainWindow()); // 失败也显示窗口供查看日志
        }
    }

    /// <summary>启动内核并订阅实时数据流。</summary>
    private static async Task CoreServiceStartupAsync()
    {
        AppServices.Config.RuntimeInvalidated += async () => await AppServices.Core.ApplyConfigAsync();

        try
        {
            await AppServices.Core.StartAsync();
        }
        catch (Exception ex)
        {
            LogService.App("内核启动失败: " + ex.Message, "error");
            StartFailed = true;
            StartError = ex.Message;
        }

        AppServices.Streams.Start();
    }

    /// <summary>应用启动时状态：系统代理、自动更新订阅定时器。</summary>
    private static async Task ApplyStartupStateAsync()
    {
        try
        {
            if (AppServices.Config.Verge.EnableSystemProxy)
                AppServices.SysProxy.Apply(AppServices.Config.Verge);
        }
        catch (Exception ex)
        {
            LogService.App("系统代理应用失败: " + ex.Message, "warn");
        }

        AppServices.Subscription.StartAutoUpdateTimer();
        await Task.CompletedTask;
    }
}
