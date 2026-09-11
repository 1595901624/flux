using Microsoft.UI.Dispatching;

namespace Flux.Services;

/// <summary>
/// 轻量模式：销毁主窗口并释放页面资源，仅保留托盘、热键、内核与定时任务；
/// 从托盘或热键恢复时重建窗口。支持关闭窗口 N 分钟后自动进入。
/// </summary>
public static class LightweightManager
{
    private static CancellationTokenSource? _autoCts;

    /// <summary>当前是否处于轻量模式（主窗口已销毁）。</summary>
    public static bool IsLightweight { get; private set; }

    /// <summary>UI 线程投递（由启动器赋值）。</summary>
    public static Action<Action>? RunOnUiThread { get; set; }

    /// <summary>状态变化（进入/退出），托盘等据此刷新。</summary>
    public static event Action? StateChanged;

    /// <summary>进入轻量模式：真实关闭主窗口。</summary>
    public static void Enter()
    {
        if (IsLightweight) return;
        RunOnUiThread?.Invoke(() =>
        {
            if (IsLightweight) return;
            IsLightweight = true;
            var window = App.MainWindow;
            App.MainWindow = null;
            if (window is not null)
            {
                App.AllowWindowClose = true;
                window.Close();
            }
            LogService.App(L10n.T("Lightweight_Entered"));
            StateChanged?.Invoke();
        });
    }

    /// <summary>退出轻量模式：重建并显示主窗口。</summary>
    public static void Exit()
    {
        if (!IsLightweight) return;
        RunOnUiThread?.Invoke(() =>
        {
            IsLightweight = false;
            CancelAutoTimer();
            App.ShowMainWindow();
            LogService.App(L10n.T("Lightweight_Exited"));
            StateChanged?.Invoke();
        });
    }

    /// <summary>主窗口被显示（用户点开窗口）时调用：取消自动进入定时器。</summary>
    public static void OnWindowShown()
    {
        CancelAutoTimer();
        if (IsLightweight)
        {
            IsLightweight = false;
            StateChanged?.Invoke();
        }
    }

    /// <summary>主窗口被隐藏（关闭到托盘）时调用：按设置启动自动进入定时器。</summary>
    public static void OnWindowHidden()
    {
        var verge = AppServices.Config.Verge;
        if (!verge.EnableLightweightMode || verge.AutoLightweightMinutes <= 0) return;

        CancelAutoTimer();
        _autoCts = new CancellationTokenSource();
        var token = _autoCts.Token;
        var minutes = verge.AutoLightweightMinutes;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(minutes), token);
                LogService.App(L10n.F("Boot_LightweightAuto", minutes));
                Enter();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LogService.App(L10n.F("Boot_LightweightAutoFailed", ex.Message), "warn");
            }
        }, token);
    }

    private static void CancelAutoTimer()
    {
        _autoCts?.Cancel();
        _autoCts = null;
    }
}
