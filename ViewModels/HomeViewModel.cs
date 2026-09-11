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
    public partial string ProfileName { get; set; } = "";

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
    public partial string ModeText { get; set; } = "";

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
    public partial string SubscriptionStatusText { get; set; } = "";

    [ObservableProperty]
    public partial string CoreStatusText { get; set; } = "";

    [ObservableProperty]
    public partial string UptimeText { get; set; } = "";

    [ObservableProperty]
    public partial string RuleCountText { get; set; } = "";

    private DateTime? _coreStartedAt;
    private DateTime? _lastRuleCountFetch;

    public bool TunAvailable => IsAdmin;
    public string AdminHintVisibility => IsAdmin ? "Collapsed" : "Visible";
    public string UsageVisibility => string.IsNullOrEmpty(ProfileUsage) ? "Collapsed" : "Visible";
    public string RuleCountVisibility => string.IsNullOrEmpty(RuleCountText) ? "Collapsed" : "Visible";

    partial void OnRuleCountTextChanged(string value) => OnPropertyChanged(nameof(RuleCountVisibility));

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

    /// <summary>运行时间与规则数量展示。</summary>
    private void UpdateUptime()
    {
        if (AppServices.Core.IsRunning)
        {
            if (_coreStartedAt is null)
                _coreStartedAt = DateTime.Now;
            var up = DateTime.Now - _coreStartedAt.Value;
            UptimeText = up.TotalHours >= 1
                ? L10n.F("VM_UptimeHours", (int)up.TotalHours, up.Minutes)
                : L10n.F("VM_UptimeMinutes", up.Minutes, up.Seconds);
        }
        else
        {
            _coreStartedAt = null;
            UptimeText = L10n.T("VM_NotRunning");
        }
    }

    /// <summary>规则数量每 60 秒刷新一次。</summary>
    private async Task RefreshRuleCountAsync()
    {
        if (_lastRuleCountFetch is { } last && (DateTime.Now - last).TotalSeconds < 60) return;
        _lastRuleCountFetch = DateTime.Now;
        try
        {
            var json = await AppServices.Api.GetRulesAsync();
            var count = json.TryGetProperty("rules", out var rules) &&
                        rules.ValueKind == System.Text.Json.JsonValueKind.Array
                ? rules.GetArrayLength()
                : 0;
            RuleCountText = count > 0 ? L10n.F("VM_RulesCount", count) : "";
        }
        catch
        {
            RuleCountText = "";
        }
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
        UpdateUptime();
        await RefreshRuleCountAsync();
        var verge = AppServices.Config.Verge;
        SystemProxyOn = verge.EnableSystemProxy;
        TunOn = verge.EnableTunMode;
        Mode = AppServices.Config.Mode;
        ModeText = Format.ModeText(Mode);
        MixedPort = AppServices.Config.MixedPort.ToString();

        var current = AppServices.Config.Profiles.GetCurrent();
        ProfileName = current?.Name ?? L10n.T("VM_ProfileNoSubscription");
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
            ? L10n.T("VM_SubNoProfile")
            : current.Type == "remote" ? L10n.T("VM_SubRemoteHint") : L10n.T("VM_SubLocalHint");

        try
        {
            var version = await AppServices.Api.GetVersionAsync();
            if (version is not null)
            {
                CoreVersion = "mihomo " + version;
                CoreStatusText = L10n.T("VM_CoreRunning");
            }
            else
            {
                CoreVersion = "";
                CoreStatusText = L10n.T("VM_CoreNotRunning");
            }
        }
        catch
        {
            CoreVersion = "";
            CoreStatusText = L10n.T("VM_CoreNotRunning");
        }

        // 与 Clash Verge Rev 一致：规则模式优先恢复该订阅上次选择的代理组；
        // 无保存值时依次选择常见主组名称、MATCH 兜底组和配置中的第一个可选组。
        try
        {
            var json = await AppServices.Api.GetProxiesAsync();
            if (json.TryGetProperty("proxies", out var proxies) && proxies.ValueKind == JsonValueKind.Object)
            {
                var groups = new Dictionary<string, (string Now, string Type)>(StringComparer.Ordinal);
                foreach (var p in proxies.EnumerateObject())
                {
                    if (p.Value.ValueKind != JsonValueKind.Object) continue;
                    if (!p.Value.TryGetProperty("all", out var all) || all.ValueKind != JsonValueKind.Array)
                        continue;
                    var now = p.Value.TryGetProperty("now", out var n) ? n.GetString() ?? "" : "";
                    var type = p.Value.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
                    groups[p.Name] = (now, type);
                }

                CurrentNodes.Clear();
                if (Mode == "direct")
                {
                    CurrentNodes.Add(new CurrentNodeItem { Group = "DIRECT", Node = "DIRECT" });
                    return;
                }

                if (Mode == "global")
                {
                    if (groups.TryGetValue("GLOBAL", out var global))
                        CurrentNodes.Add(new CurrentNodeItem { Group = "GLOBAL", Node = string.IsNullOrEmpty(global.Now) ? "—" : global.Now });
                    return;
                }

                var selectableGroups = groups
                    .Where(x => x.Value.Type is "Selector" or "URLTest")
                    .Select(x => x.Key)
                    .ToList();
                var savedGroup = AppServices.Config.Profiles.GetCurrent()?.SelectedProxyGroup;
                var primaryKeywords = new[] { "auto", "select", "proxy", "节点选择", "自动选择" };
                var preferredGroup = !string.IsNullOrWhiteSpace(savedGroup) && selectableGroups.Contains(savedGroup)
                    ? savedGroup
                    : selectableGroups.FirstOrDefault(name => primaryKeywords.Any(keyword =>
                        name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                        ?? AppServices.Config.GetCurrentRuleDefaultProxyGroup()
                        ?? AppServices.Config.GetCurrentProxyGroupOrder().FirstOrDefault(name => selectableGroups.Contains(name))
                        ?? selectableGroups.FirstOrDefault();

                if (preferredGroup is not null && groups.TryGetValue(preferredGroup, out var selected))
                    CurrentNodes.Add(new CurrentNodeItem { Group = preferredGroup, Node = string.IsNullOrEmpty(selected.Now) ? "—" : selected.Now });
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
            throw new InvalidOperationException(L10n.T("VM_TunNeedAdmin"));
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
        throw new InvalidOperationException(L10n.T("VM_TunRejected"));
    }
}
