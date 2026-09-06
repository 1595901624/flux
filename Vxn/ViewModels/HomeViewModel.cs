using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Vxn.Services;
using Vxn.Utils;

namespace Vxn.ViewModels;

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
    private string _profileName = "（未启用）";

    [ObservableProperty]
    private string _profileUsage = "";

    [ObservableProperty]
    private bool _systemProxyOn;

    [ObservableProperty]
    private bool _tunOn;

    [ObservableProperty]
    private bool _isAdmin;

    [ObservableProperty]
    private string _mode = "rule";

    [ObservableProperty]
    private string _modeText = "规则";

    [ObservableProperty]
    private string _coreVersion = "";

    [ObservableProperty]
    private string _mixedPort = "";

    [ObservableProperty]
    private string _upSpeed = "0 B/s";

    [ObservableProperty]
    private string _downSpeed = "0 B/s";

    [ObservableProperty]
    private string _upTotal = "0 B";

    [ObservableProperty]
    private string _downTotal = "0 B";

    [ObservableProperty]
    private string _memoryText = "";

    private double _profileRatio;
    public double ProfileRatio => _profileRatio;

    [ObservableProperty]
    private string _subscriptionStatusText = "点击卡片管理订阅";

    [ObservableProperty]
    private string _coreStatusText = "检查中…";

    public bool TunAvailable => IsAdmin;
    public string AdminHintVisibility => IsAdmin ? "Collapsed" : "Visible";
    public string UsageVisibility => string.IsNullOrEmpty(ProfileUsage) ? "Collapsed" : "Visible";

    partial void OnIsAdminChanged(bool value)
    {
        OnPropertyChanged(nameof(TunAvailable));
        OnPropertyChanged(nameof(AdminHintVisibility));
    }

    private DispatcherQueueTimer? _timer;

    public HomeViewModel()
    {
        SystemProxyOn = AppServices.Config.Verge.EnableSystemProxy;
        TunOn = AppServices.Config.Verge.EnableTunMode;
        IsAdmin = TrayService.IsElevated();
        Mode = AppServices.Config.Mode;
        ModeText = Format.ModeText(Mode);
        MixedPort = AppServices.Config.MixedPort.ToString();

        AppServices.Streams.Traffic += (up, down) =>
            App.UiDispatcher.TryEnqueue(() =>
            {
                UpSpeed = Format.Bytes(up) + "/s";
                DownSpeed = Format.Bytes(down) + "/s";
            });
        AppServices.Streams.Connections += s =>
            App.UiDispatcher.TryEnqueue(() =>
            {
                UpTotal = Format.Bytes(s.UploadTotal);
                DownTotal = Format.Bytes(s.DownloadTotal);
            });
        AppServices.Streams.Memory += mem =>
            App.UiDispatcher.TryEnqueue(() =>
                MemoryText = Format.Bytes(mem));
    }

    public void StartTimer()
    {
        var queue = DispatcherQueue.GetForCurrentThread();
        _timer ??= queue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(3);
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
        _ = RefreshAsync();
    }

    public void StopTimer() => _timer?.Stop();

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
        verge.EnableSystemProxy = on;
        AppServices.Config.SaveVerge();
        SystemProxyOn = on;
        await Task.Run(() => AppServices.SysProxy.Apply(verge));
    }

    public async Task ToggleTunAsync(bool on)
    {
        if (on && !TrayService.IsElevated())
        {
            TunOn = false;
            throw new InvalidOperationException("TUN 模式需要以管理员身份运行应用");
        }
        var verge = AppServices.Config.Verge;
        verge.EnableTunMode = on;
        AppServices.Config.SaveVerge();
        TunOn = on;
        await AppServices.Core.ApplyConfigAsync();
    }
}
