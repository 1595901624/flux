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
    public static MainWindow? MainWindow { get; private set; }

    /// <summary>UI 线程调度器（启动时捕获，供后台线程事件回调使用）。</summary>
    public static DispatcherQueue UiDispatcher { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        Instance = this;
        UiDispatcher = DispatcherQueue.GetForCurrentThread();

        UnhandledException += (_, e) =>
        {
            Services.LogService.App("UI 未处理异常: " + e.Message + "\n" + e.Exception, "error");
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Services.LogService.App("进程未处理异常: " + e.ExceptionObject, "error");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Services.LogService.App("未观察任务异常: " + e.Exception, "warn");
            e.SetObserved();
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _ = Services.AppBootstrapper.StartAsync();
    }

    public static void ShowMainWindow()
    {
        if (MainWindow is null)
        {
            MainWindow = new MainWindow();
        }
        MainWindow.Activate();
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
