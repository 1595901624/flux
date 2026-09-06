using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Flux.Models;
using Flux.Services;

namespace Flux.ViewModels;

public partial class ProxiesViewModel : ObservableObject
{
    public ObservableCollection<ProxiesGroupHeader> Groups { get; } = new();

    [ObservableProperty]
    private string _mode = "rule";

    [ObservableProperty]
    private string _filterText = "";

    [ObservableProperty]
    private string _testUrl = "";

    [ObservableProperty]
    private bool _isEmpty = true;

    private DispatcherQueueTimer? _pollTimer;
    private volatile bool _testing;
    private bool _switchingMode;

    public ProxiesViewModel()
    {
        TestUrl = AppServices.Config.Verge.DefaultLatencyTest;
        Mode = AppServices.Config.Mode;
    }

    public void StartPolling()
    {
        var queue = DispatcherQueue.GetForCurrentThread();
        _pollTimer ??= queue.CreateTimer();
        _pollTimer.Interval = TimeSpan.FromSeconds(3);
        _pollTimer.Tick += async (_, _) => await RefreshAsync();
        _pollTimer.Start();
        _ = RefreshAsync();
    }

    public void StopPolling() => _pollTimer?.Stop();

    public void ApplyFilter()
    {
        _ = RefreshAsync();
    }

    // ---------- 数据加载 ----------

    public async Task RefreshAsync()
    {
        if (_testing) return;
        // 模式以持久化配置为准（对齐 verge：UI 状态跟随配置而非页面本地状态）
        if (!_switchingMode) Mode = AppServices.Config.Mode;
        try
        {
            var json = await AppServices.Api.GetProxiesAsync();
            ParseProxies(json);
        }
        catch
        {
            // 内核未就绪时静默
        }
    }

    private void ParseProxies(JsonElement json)
    {
        if (!json.TryGetProperty("proxies", out var proxies) ||
            proxies.ValueKind != JsonValueKind.Object) return;

        var groups = new List<ProxyInfo>();
        var nodeMap = new Dictionary<string, ProxyInfo>();
        foreach (var p in proxies.EnumerateObject())
        {
            var info = ProxyInfo.FromJson(p);
            if (info.IsGroup && info.Name != "GLOBAL" &&
                info.Type is not ("Direct" or "Reject" or "Compatible" or "Pass"))
            {
                groups.Add(info);
                nodeMap[info.Name] = info; // GLOBAL 组的节点列表包含其他组
            }
            else if (!info.IsGroup)
            {
                nodeMap[info.Name] = info;
            }
        }

        if (Mode == "global" && proxies.TryGetProperty("GLOBAL", out var globalElem))
        {
            var g = new ProxyInfo { Name = "GLOBAL", Type = "Selector" };
            if (globalElem.TryGetProperty("now", out var nowEl)) g.Now = nowEl.GetString() ?? "";
            if (globalElem.TryGetProperty("all", out var allEl) && allEl.ValueKind == JsonValueKind.Array)
                foreach (var a in allEl.EnumerateArray())
                    if (a.GetString() is { } s) g.All.Add(s);
            groups.Insert(0, g);
        }

        var filter = FilterText?.Trim() ?? "";

        // 移除已消失的组
        for (int i = Groups.Count - 1; i >= 0; i--)
        {
            if (!groups.Any(g => g.Name == Groups[i].Name))
                Groups.RemoveAt(i);
        }

        var seenKeys = new HashSet<string>();
        foreach (var g in groups)
        {
            var header = Groups.FirstOrDefault(x => x.Name == g.Name);
            if (header is null)
            {
                header = new ProxiesGroupHeader
                {
                    Name = g.Name,
                    Type = g.Type,
                    TestDelayCommand = new AsyncRelayCommand(() => TestGroupDelayAsync(g.Name)),
                };
                Groups.Add(header);
            }
            header.Now = g.Now;

            foreach (var nodeName in g.All)
            {
                if (!nodeMap.TryGetValue(nodeName, out var node))
                {
                    if (nodeName is not ("DIRECT" or "REJECT")) continue;
                    node = new ProxyInfo { Name = nodeName, Type = nodeName };
                }

                if (!string.IsNullOrEmpty(filter) &&
                    !node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                    !node.Type.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;

                seenKeys.Add($"{g.Name}|{node.Name}");

                var vm = header.Nodes.FirstOrDefault(x => x.Name == node.Name && x.GroupName == g.Name);
                if (vm is null)
                {
                    vm = new ProxiesNodeVm { Name = node.Name, GroupName = g.Name };
                    header.Nodes.Add(vm);
                }
                vm.Type = node.Type;
                vm.Udp = node.Udp;
                vm.Delay = node.Delay;
                vm.IsSelected = node.Name == g.Now;
            }

            // 移除已消失/被过滤的节点
            for (int i = header.Nodes.Count - 1; i >= 0; i--)
            {
                var n = header.Nodes[i];
                if (!seenKeys.Contains($"{g.Name}|{n.Name}"))
                    header.Nodes.RemoveAt(i);
            }
        }

        IsEmpty = Groups.Count == 0;
    }

    // ---------- 动作 ----------

    [RelayCommand]
    public async Task SetModeAsync(string mode)
    {
        if (Mode == mode) return;
        _switchingMode = true;
        try
        {
            await AppServices.Api.PatchConfigsAsync(new() { ["mode"] = mode });
            AppServices.Config.PatchClashBase("mode", mode);
            if (AppServices.Config.Verge.AutoCloseConnection)
                await AppServices.Api.CloseAllConnectionsAsync();
        }
        catch (Exception ex)
        {
            LogService.App("模式切换失败: " + ex.Message, "warn");
        }
        finally
        {
            _switchingMode = false;
        }
        await RefreshAsync();
    }

    public async Task SelectNodeAsync(string group, string name)
    {
        try
        {
            await AppServices.Api.SelectProxyAsync(group, name);
            var header = Groups.FirstOrDefault(g => g.Name == group);
            if (header is not null)
            {
                header.Now = name;
                foreach (var n in header.Nodes)
                    n.IsSelected = n.Name == name;
            }
        }
        catch (Exception ex)
        {
            LogService.App($"切换节点失败 [{group} → {name}]: " + ex.Message, "warn");
        }
    }

    public async Task TestGroupDelayAsync(string group)
    {
        _testing = true;
        try
        {
            var header = Groups.FirstOrDefault(g => g.Name == group);
            if (header is not null)
                foreach (var n in header.Nodes) n.Delay = -2;

            var result = await AppServices.Api.GetGroupDelayAsync(
                group, TestUrl, AppServices.Config.Verge.DefaultLatencyTimeout);
            if (header is not null)
            {
                foreach (var n in header.Nodes)
                    n.Delay = result.TryGetValue(n.Name, out var d) ? d : 0;
            }
        }
        finally
        {
            _testing = false;
        }
    }

    public async Task TestNodeAsync(ProxiesNodeVm node)
    {
        node.Delay = -2;
        var delay = await AppServices.Api.GetProxyDelayAsync(
            node.Name, TestUrl, AppServices.Config.Verge.DefaultLatencyTimeout);
        node.Delay = delay;
    }
}
