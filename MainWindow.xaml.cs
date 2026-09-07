using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System.Runtime.InteropServices;
using Flux.Services;
using Flux.Utils;
using Flux.Views;
using Windows.UI;

namespace Flux;

public sealed partial class MainWindow : Window
{
    private readonly EndSessionHook _endSessionHook;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private const int SampleCount = 80;
    private readonly Queue<double> _upSamples = new();
    private readonly Queue<double> _downSamples = new();
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer? _graphTimer;

    public MainWindow()
    {
        InitializeComponent();

        Title = "Flux";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SystemBackdrop = new MicaBackdrop()
        {
            Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt
        };

        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(HomePage));

        // 侧边栏收起时隐藏左下角流量图
        TrafficFooter.Visibility = NavView.IsPaneOpen ? Visibility.Visible : Visibility.Collapsed;
        NavView.RegisterPropertyChangedCallback(NavigationView.IsPaneOpenProperty, (_, _) =>
            TrafficFooter.Visibility = NavView.IsPaneOpen ? Visibility.Visible : Visibility.Collapsed);

        // 关闭窗口 = 隐藏到托盘（托盘菜单“退出”才真正关闭）
        AppWindow.Closing += (_, e) =>
        {
            e.Cancel = true;
            AppWindow.Hide();
        };

        // 默认窗口 1080x720 逻辑像素（AppWindow.Resize 使用物理像素，需按 DPI 换算）
        var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        if (scale <= 0) scale = 1.0;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(
            (int)(1080 * scale), (int)(720 * scale)));
        AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));

        // 关机/注销时 Windows 直接结束进程，不走 AppWindow.Closing；
        // 在 WM_ENDSESSION 里同步恢复系统代理（内核由 Job Object 随进程终止一并回收）
        _endSessionHook = new EndSessionHook(WinRT.Interop.WindowNative.GetWindowHandle(this));

        // 实时流量订阅
        AppServices.Streams.Traffic += (up, down) =>
            DispatcherQueue.TryEnqueue(() =>
            {
                UpText.Text = "↑ " + Format.Bytes(up) + "/s";
                DownText.Text = "↓ " + Format.Bytes(down) + "/s";
                PushSample(_upSamples, up);
                PushSample(_downSamples, down);
            });

        _graphTimer = DispatcherQueue.CreateTimer();
        _graphTimer.Interval = TimeSpan.FromSeconds(1);
        _graphTimer.Tick += (_, _) => RedrawGraph();
        _graphTimer.Start();
    }

    private static void PushSample(Queue<double> queue, double value)
    {
        queue.Enqueue(value);
        while (queue.Count > SampleCount) queue.Dequeue();
    }

    private void RedrawGraph()
    {
        var w = GraphHost.ActualWidth;
        var h = GraphHost.ActualHeight;
        if (w < 10 || h < 10) return;

        DrawSeries(_upSamples, UpLine, UpPolygon, w, h);
        DrawSeries(_downSamples, DownLine, DownPolygon, w, h);
    }

    private static void DrawSeries(Queue<double> samples, Polyline line, Polygon area, double w, double h)

    {
        if (samples.Count < 2)
        {
            line.Points = [];
            area.Points = [];
            return;
        }
        var arr = samples.ToArray();
        var max = Math.Max(1024, arr.Max());

        var points = new List<Windows.Foundation.Point>(arr.Length);
        for (var i = 0; i < arr.Length; i++)
        {
            var x = w * i / (SampleCount - 1);
            var y = h - Math.Min(1.0, arr[i] / max) * (h - 2) - 1;
            points.Add(new Windows.Foundation.Point(x, y));
        }

        line.Points = [.. points];
        var areaPoints = new List<Windows.Foundation.Point>(points)
        {
            new(w, h),
            new(points[0].X, h),
        };
        area.Points = [.. areaPoints];
    }

    /// <summary>
    /// 主窗口 WndProc 子类化，监听 WM_ENDSESSION：系统关机/注销时把系统代理恢复为关闭。
    /// </summary>
    private sealed class EndSessionHook : IDisposable
    {
        private const int GWL_WNDPROC = -4;
        private const uint WM_ENDSESSION = 0x0016;

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProcW(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private readonly IntPtr _hwnd;
        private readonly IntPtr _oldProc;
        // 委托必须保活，否则原生回调指向已回收的委托
        private readonly WndProcDelegate _newProc;

        public EndSessionHook(IntPtr hwnd)
        {
            _hwnd = hwnd;
            _newProc = WndProc;
            _oldProc = GetWindowLongPtrW(hwnd, GWL_WNDPROC);
            SetWindowLongPtrW(hwnd, GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate(_newProc));
        }

        private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_ENDSESSION && wParam != IntPtr.Zero)
            {
                try { AppServices.SysProxy.Reset(); }
                catch { }
            }
            return CallWindowProcW(_oldProc, hwnd, msg, wParam, lParam);
        }

        public void Dispose() => SetWindowLongPtrW(_hwnd, GWL_WNDPROC, _oldProc);
    }

    public void NavigateTo(string tag)    {
        foreach (NavigationViewItem item in NavView.MenuItems)
        {
            if (item.Tag as string == tag)
            {
                NavView.SelectedItem = item;
                return;
            }
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;
        var tag = item.Tag as string;
        Type? page = tag switch
        {
            "home" => typeof(HomePage),
            "proxies" => typeof(ProxiesPage),
            "profiles" => typeof(ProfilesPage),
            "connections" => typeof(ConnectionsPage),
            "rules" => typeof(RulesPage),
            "logs" => typeof(LogsPage),
            "settings" => typeof(SettingsPage),
            _ => null
        };
        if (page is not null && (ContentFrame.Content?.GetType() != page))
        {
            ContentFrame.Navigate(page);
        }
    }
}
