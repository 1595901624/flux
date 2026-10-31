using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Flux.Services;
using Flux.Utils;

namespace Flux.Views;

/// <summary>可替换的主界面。窗口、标题栏和后台服务独立于此控件的生命周期。</summary>
public sealed partial class MainShell : UserControl, IDisposable
{
    private const int SampleCount = 80;
    private readonly Queue<double> _upSamples = new();
    private readonly Queue<double> _downSamples = new();
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _graphTimer;
    private readonly long _paneChangedToken;
    private bool _subscribed;
    private bool _disposed;
    private bool _starting;

    public MainShell() : this(null) { }

    internal MainShell(MainShellState? state)
    {
        InitializeComponent();
        FlowDirection = LanguageLayout.Current;
        _paneChangedToken = NavView.RegisterPropertyChangedCallback(NavigationView.IsPaneOpenProperty,
            (_, _) => UpdateTrafficVisibility());
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        NavigateTo(state?.SelectedPage ?? "home");
        if (ContentFrame.Content is null) NavigateTo("home");
        if (state is not null)
        {
            NavView.IsPaneOpen = state.IsPaneOpen;
            if (ContentFrame.Content is SettingsPage settings)
                settings.RestoreScrollOffset(state.SettingsScrollOffset);
            foreach (var sample in state.UploadSamples) _upSamples.Enqueue(sample);
            foreach (var sample in state.DownloadSamples) _downSamples.Enqueue(sample);
            UpText.Text = state.UploadText;
            DownText.Text = state.DownloadText;
        }
        UpdateTrafficVisibility();
    }

    internal MainShellState CaptureState() => new(
        (NavView.SelectedItem as NavigationViewItem)?.Tag as string ?? "home",
        NavView.IsPaneOpen,
        (ContentFrame.Content as SettingsPage)?.ScrollOffset ?? 0,
        _upSamples.ToArray(), _downSamples.ToArray(), UpText.Text, DownText.Text);

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (_disposed || _subscribed) return;
        _subscribed = true;
        AppServices.Streams.Traffic += OnTraffic;
        _graphTimer = DispatcherQueue.CreateTimer();
        _graphTimer.Interval = TimeSpan.FromSeconds(1);
        _graphTimer.Tick += OnGraphTick;
        _graphTimer.Start();
        RedrawGraph();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args) => Detach();

    private void Detach()
    {
        if (!_subscribed) return;
        _subscribed = false;
        AppServices.Streams.Traffic -= OnTraffic;
        _graphTimer?.Stop();
        if (_graphTimer is not null) _graphTimer.Tick -= OnGraphTick;
        _graphTimer = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Detach();
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        NavView.UnregisterPropertyChangedCallback(NavigationView.IsPaneOpenProperty, _paneChangedToken);
        // 卸载页面以停止其轮询、订阅和异步任务，并清除导航缓存。
        ContentFrame.Content = null;
        ContentFrame.BackStack.Clear();
        ContentFrame.ForwardStack.Clear();
        ContentFrame.CacheSize = 0;
    }

    private void UpdateTrafficVisibility() =>
        TrafficFooter.Visibility = NavView.IsPaneOpen ? Visibility.Visible : Visibility.Collapsed;

    private void OnGraphTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args) => RedrawGraph();

    internal void UpdateStartupState(bool starting)
    {
        var wasStarting = _starting;
        _starting = starting;
        NavView.IsEnabled = !starting;
        if (wasStarting && !starting && ContentFrame.Content is HomePage home) _ = home.Vm.RefreshAsync();
    }

    private void OnTraffic(double up, double down) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_subscribed || _disposed) return;
            UpText.Text = "↑ " + Format.Bytes(up) + "/s";
            DownText.Text = "↓ " + Format.Bytes(down) + "/s";
            PushSample(_upSamples, up);
            PushSample(_downSamples, down);
        });

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

    public void NavigateTo(string tag)
    {
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
            "unlock" => typeof(UnlockPage),
            "settings" => typeof(SettingsPage),
            _ => null
        };
        if (page is not null && (ContentFrame.Content?.GetType() != page))
        {
            ContentFrame.Navigate(page);
        }
    }
}

internal sealed record MainShellState(string SelectedPage, bool IsPaneOpen, double SettingsScrollOffset,
    double[] UploadSamples, double[] DownloadSamples, string UploadText, string DownloadText);
