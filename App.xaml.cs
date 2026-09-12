using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Flux;

/// <summary>
/// 应用入口。启动引导见 <see cref="Services.AppBootstrapper"/>。
/// </summary>
public partial class App : Application
{
    public static App Instance { get; private set; } = null!;
    public static MainWindow? MainWindow { get; internal set; }

    /// <summary>UI 线程调度器（启动时捕获，供后台线程事件回调使用）。</summary>
    public static DispatcherQueue UiDispatcher { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        Instance = this;
        UiDispatcher = DispatcherQueue.GetForCurrentThread();

        UnhandledException += (_, e) =>
        {
            Services.LogService.App(Services.L10n.F("App_UnhandledUi", e.Message + "\n" + e.Exception), "error");
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Services.LogService.App(Services.L10n.F("App_UnhandledDomain", e.ExceptionObject), "error");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Services.LogService.App(Services.L10n.F("App_UnobservedTask", e.Exception), "warn");
            e.SetObserved();
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Program.Trace("onlaunched enter");
        _ = Services.AppBootstrapper.StartAsync();
        Program.Trace("onlaunched started-async");
    }

    /// <summary>轻量模式真实关闭窗口时由 LightweightManager 置位（否则关闭仅隐藏到托盘）。</summary>
    public static bool AllowWindowClose { get; set; }

    public static void ShowMainWindow()
    {
        Services.LightweightManager.OnWindowShown();
        try
        {
            if (MainWindow is null)
            {
                Program.Trace("mainwindow creating");
                MainWindow = new MainWindow();
                Program.Trace("mainwindow created");
            }
            MainWindow.Activate();
            Program.Trace("mainwindow activated");
        }
        catch (Exception ex)
        {
            Program.Trace("mainwindow FAILED: " + ex);
            throw;
        }
    }

    public static void ToggleMainWindowVisibility()
    {
        if (MainWindow?.AppWindow.IsVisible == true)
        {
            MainWindow.AppWindow.Hide();
            Services.LightweightManager.OnWindowHidden();
            return;
        }
        ShowMainWindow();
    }

    /// <summary>应用主题模式（system | light | dark）。</summary>
    public static void ApplyTheme(string themeMode)
    {
        if (MainWindow?.Content is not FrameworkElement root) return;
        root.RequestedTheme = themeMode switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }
}
