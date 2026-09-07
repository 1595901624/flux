using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Flux.Models;
using Flux.Services;
using Flux.Utils;

namespace Flux.ViewModels;

public class CurrentNodeItem
{
    public string Group { get; init; } = "";
    public string Node { get; init; } = "";
}

/// <summary>首页仪表盘数据。</summary>
public partial class HomeViewModel : ObservableObject
{
    public ObservableCollection<CurrentNodeItem> CurrentNodes { get; } = new();

    [ObservableProperty]
    public partial string ProfileName { get; set; } = "（未启用）";

    [ObservableProperty]
    public partial string ProfileUsage { get; set; } = "";

    [ObservableProperty]
    public partial bool SystemProxyOn { get; set; }

    [ObservableProperty]
    public partial bool TunOn { get; set; }

    [ObservableProperty]
    public partial bool IsAdmin { get; set; }

    [ObservableProperty]
    public partial string Mode { get; set; } = "rule";

    [ObservableProperty]
    public partial string ModeText { get; set; } = "规则";

    [ObservableProperty]
    public partial string CoreVersion { get; set; } = "";

    [ObservableProperty]
    public partial string MixedPort { get; set; } = "";

    [ObservableProperty]
    public partial string UpSpeed { get; set; } = "0 B/s";

    [ObservableProperty]
    public partial string DownSpeed { get; set; } = "0 B/s";

    [ObservableProperty]
    public partial string UpTotal { get; set; } = "0 B";

    [ObservableProperty]
    public partial string DownTotal { get; set; } = "0 B";

    [ObservableProperty]
    public partial string MemoryText { get; set; } = "";

    private double _profileRatio;
    public double ProfileRatio => _profileRatio;

    [ObservableProperty]
    public partial string SubscriptionStatusText { get; set; } = "点击卡片管理订阅";

    [ObservableProperty]
    public partial string CoreStatusText { get; set; } = "检查中…";

    public bool TunAvailable => IsAdmin;
    public string AdminHintVisibility => IsAdmin ? "Collapsed" : "Visible";
    public string UsageVisibility => string.IsNullOrEmpty(ProfileUsage) ? "Collapsed" : "Visible";

    partial void OnIsAdminChanged(bool value)
    {
        OnPropertyChanged(nameof(TunAvailable));
        OnPropertyChanged(nameof(AdminHintVisibility));
    }

    private DispatcherQueueTimer? _timer;
    private bool _subscribed;

    public HomeViewModel()
    {
        SystemProxyOn = AppServices.Config.Verge.EnableSystemProxy;
        TunOn = AppServices.Config.Verge.EnableTunMode;
        IsAdmin = TrayService.IsElevated();
        Mode = AppServices.Config.Mode;
        ModeText = Format.ModeText(Mode);
        MixedPort = AppServices.Config.MixedPort.ToString();

    }

    public void StartTimer()
    {
        if (!_subscribed)
        {
            AppServices.Streams.Traffic += OnTraffic;
            AppServices.Streams.Connections += OnConnections;
            AppServices.Streams.Memory += OnMemory;
            _subscribed = true;
        }
        var queue = DispatcherQueue.GetForCurrentThread();
        if (_timer is null)
        {
            _timer = queue.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(3);
            _timer.Tick += async (_, _) => await RefreshAsync();
        }
        _timer.Start();
        _ = RefreshAsync();
    }

    public void StopTimer()
    {
        _timer?.Stop();
        if (!_subscribed) return;
        AppServices.Streams.Traffic -= OnTraffic;
        AppServices.Streams.Connections -= OnConnections;
        AppServices.Streams.Memory -= OnMemory;
        _subscribed = false;
    }

    private void OnTraffic(double up, double down) => App.UiDispatcher.TryEnqueue(() =>
    {
        UpSpeed = Format.Bytes(up) + "/s";
        DownSpeed = Format.Bytes(down) + "/s";
    });

    private void OnConnections(ConnectionsSnapshot snapshot) => App.UiDispatcher.TryEnqueue(() =>
    {
        UpTotal = Format.Bytes(snapshot.UploadTotal);
        DownTotal = Format.Bytes(snapshot.DownloadTotal);
    });

    private void OnMemory(long memory) =>
        App.UiDispatcher.TryEnqueue(() => MemoryText = Format.Bytes(memory));

    public async Task RefreshAsync()
    {
        var verge = AppServices.Config.Verge;
        SystemProxyOn = verge.EnableSystemProxy;
        TunOn = verge.EnableTunMode;
        Mode = AppServices.Config.Mode;
        ModeText = Format.ModeText(Mode);
        MixedPort = AppServices.Config.MixedPort.ToString();

        var current = AppServices.Config.Profiles.GetCurrent();
        ProfileName = current?.Name ?? "（未启用订阅）";
        var e = current?.Extra;
        if (e is { Total: > 0 })
        {
            ProfileUsage = $"{Format.Bytes(e.Upload + e.Download)} / {Format.Bytes(e.Total)}";
            _profileRatio = Math.Clamp((e.Upload + e.Download) / (double)e.Total, 0, 1);
        }
        else
        {
            ProfileUsage = "";
            _profileRatio = 0;
        }
        OnPropertyChanged(nameof(ProfileRatio));
        OnPropertyChanged(nameof(UsageVisibility));
        SubscriptionStatusText = current is null
            ? "尚未导入订阅，点击前往"
            : current.Type == "remote" ? "远程订阅 · 点击卡片管理" : "本地配置 · 点击卡片管理";

        try
        {
            var version = await AppServices.Api.GetVersionAsync();
            if (version is not null)
            {
                CoreVersion = "mihomo " + version;
                CoreStatusText = "内核运行中";
            }
            else
            {
                CoreVersion = "";
                CoreStatusText = "内核未运行";
            }
        }
        catch
        {
            CoreVersion = "";
            CoreStatusText = "内核未运行";
        }

        // 主选择组当前节点
        try
        {
            var json = await AppServices.Api.GetProxiesAsync();
            if (json.TryGetProperty("proxies", out var proxies))
            {
                var groups = new List<(string Name, string Now, List<string> All)>();
                foreach (var p in proxies.EnumerateObject())
                {
                    if (p.Value.ValueKind != JsonValueKind.Object) continue;
                    var type = p.Value.TryGetProperty("type", out var t) ? t.GetString() : null;
                    if (type != "Selector" || p.Name == "GLOBAL") continue;
                    var now = p.Value.TryGetProperty("now", out var n) ? n.GetString() ?? "" : "";
                    var all = new List<string>();
                    if (p.Value.TryGetProperty("all", out var a) && a.ValueKind == JsonValueKind.Array)
                        foreach (var x in a.EnumerateArray())
                            if (x.GetString() is { } s) all.Add(s);
                    groups.Add((p.Name, now, all));
                }

                var top = groups.OrderByDescending(g => g.All.Count).Take(3).ToList();
                CurrentNodes.Clear();
                foreach (var g in top)
                    CurrentNodes.Add(new CurrentNodeItem { Group = g.Name, Node = string.IsNullOrEmpty(g.Now) ? "—" : g.Now });
            }
        }
        catch { }
    }

    public async Task ToggleSystemProxyAsync(bool on)
    {
        var verge = AppServices.Config.Verge;
        var previous = verge.EnableSystemProxy;
        verge.EnableSystemProxy = on;
        AppServices.Config.SaveVerge();
        SystemProxyOn = on;
        try
        {
            await Task.Run(() => AppServices.SysProxy.Apply(verge));
        }
        catch
        {
            verge.EnableSystemProxy = previous;
            AppServices.Config.SaveVerge();
            SystemProxyOn = previous;
            throw;
        }
    }

    public async Task ToggleTunAsync(bool on)
    {
        if (on && !TrayService.IsElevated())
        {
            TunOn = false;
            throw new InvalidOperationException("TUN 模式需要以管理员身份运行应用");
        }
        var verge = AppServices.Config.Verge;
        var previous = verge.EnableTunMode;
        verge.EnableTunMode = on;
        AppServices.Config.SaveVerge();
        TunOn = on;
        if (await AppServices.Core.ApplyConfigAsync()) return;
        verge.EnableTunMode = previous;
        AppServices.Config.SaveVerge();
        TunOn = previous;
        throw new InvalidOperationException("内核未运行或拒绝了 TUN 配置");
    }
}
