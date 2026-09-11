using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Flux.Models;
using Flux.Services;
using Flux.Utils;

namespace Flux.ViewModels;

/// <summary>连接显示模型。</summary>
public class ConnectionVm : ObservableObject
{
    public ConnectionItem Item { get; }

    public ConnectionVm(ConnectionItem item)
    {
        Item = item;
        _downloadSpeed = item.DownloadSpeed;
        _uploadSpeed = item.UploadSpeed;
    }

    public string Id => Item.Id;
    public string Host => Item.Metadata.DisplayName;
    public string Network => Item.Metadata.Network.ToUpperInvariant();
    public string Type => Item.Metadata.Type;
    public string Rule => string.IsNullOrEmpty(Item.RulePayload)
        ? Item.Rule : $"{Item.Rule} · {Item.RulePayload}";
    public string Chains => string.Join(" / ", Enumerable.Reverse(Item.Chains));
    public string Process
    {
        get
        {
            var p = Item.Metadata.Process;
            if (!string.IsNullOrEmpty(p)) return p;
            p = Item.Metadata.ProcessPath;
            return string.IsNullOrEmpty(p) ? "" : Path.GetFileName(p);
        }
    }
    public string Source => $"{Item.Metadata.SourceIp}:{Item.Metadata.SourcePort}";
    public string Destination => $"{Item.Metadata.DestinationIp}:{Item.Metadata.DestinationPort}";
    public string StartText => Item.Start == default ? "" : Item.Start.ToString("HH:mm:ss");

    public string DownloadText => Format.Bytes(Item.Download);
    public string UploadText => Format.Bytes(Item.Upload);

    private long _downloadSpeed;
    public long DownloadSpeed
    {
        get => _downloadSpeed;
        set { if (SetProperty(ref _downloadSpeed, value)) OnPropertyChanged(nameof(DownloadSpeedText)); }
    }

    private long _uploadSpeed;
    public long UploadSpeed
    {
        get => _uploadSpeed;
        set { if (SetProperty(ref _uploadSpeed, value)) OnPropertyChanged(nameof(UploadSpeedText)); }
    }

    public string DownloadSpeedText => Format.Bytes(DownloadSpeed) + "/s";
    public string UploadSpeedText => Format.Bytes(UploadSpeed) + "/s";

    public void Refresh()
    {
        OnPropertyChanged(nameof(DownloadText));
        OnPropertyChanged(nameof(UploadText));
        OnPropertyChanged(nameof(Chains));
    }
}

public partial class ConnectionsViewModel : ObservableObject
{
    public ObservableCollection<ConnectionVm> Active { get; } = new();
    public ObservableCollection<ConnectionVm> Closed { get; } = new();

    [ObservableProperty]
    public partial bool ShowClosed { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial string TotalsText { get; set; } = "";

    [ObservableProperty]
    public partial string CountText { get; set; } = "";

    /// <summary>活跃连接排序：default（最新优先）| upload | download。</summary>
    [ObservableProperty]
    public partial string SortMode { get; set; } = "default";

    private readonly Dictionary<string, (long Up, long Down)> _last = new();
    private DateTime _lastTick = DateTime.UtcNow;
    private DispatcherQueueTimer? _flushTimer;
    private readonly List<ConnectionsSnapshot> _pending = [];
    private bool _subscribed;

    partial void OnShowClosedChanged(bool value) => RefreshView();
    partial void OnSearchTextChanged(string value) => RefreshView();

    public void Start()
    {
        if (_subscribed) return;
        AppServices.Streams.Connections += OnSnapshot;
        _subscribed = true;
    }

    public void Stop()
    {
        if (!_subscribed) return;
        AppServices.Streams.Connections -= OnSnapshot;
        _subscribed = false;
        _flushTimer?.Stop();
    }

    private void OnSnapshot(ConnectionsSnapshot snapshot)
    {
        App.UiDispatcher.TryEnqueue(() => QueueSnapshot(snapshot));
    }

    private void QueueSnapshot(ConnectionsSnapshot snapshot)
    {
        _pending.Add(snapshot);
        _flushTimer ??= App.UiDispatcher.CreateTimer();
        if (!_flushTimer.IsRunning)
        {
            _flushTimer.Interval = TimeSpan.FromMilliseconds(500);
            _flushTimer.Tick += async (_, _) => { _flushTimer.Stop(); await FlushAsync(); };
            _flushTimer.Start();
        }
    }

    private Task FlushAsync()
    {
        ConnectionsSnapshot merged;
        if (_pending.Count == 0) return Task.CompletedTask;
        merged = _pending[^1];
        _pending.Clear();

        var now = DateTime.UtcNow;
        var dt = Math.Max(0.2, (now - _lastTick).TotalSeconds);
        _lastTick = now;

        // 速率计算
        foreach (var c in merged.Connections)
        {
            var (pu, pd) = _last.TryGetValue(c.Id, out var prev) ? prev : (c.Upload, c.Download);
            c.UploadSpeed = (long)Math.Max(0, (c.Upload - pu) / dt);
            c.DownloadSpeed = (long)Math.Max(0, (c.Download - pd) / dt);
            _last[c.Id] = (c.Upload, c.Download);
        }

        var activeIds = merged.Connections.Select(c => c.Id).ToHashSet();

        // 关闭的连接移入 Closed（环形 500）
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            if (!activeIds.Contains(Active[i].Id))
            {
                var vm = Active[i];
                Active.RemoveAt(i);
                vm.DownloadSpeed = 0;
                vm.UploadSpeed = 0;
                Closed.Insert(0, vm);
            }
        }
        while (Closed.Count > 500) Closed.RemoveAt(Closed.Count - 1);

        // 新增/更新
        foreach (var c in merged.Connections)
        {
            var vm = Active.FirstOrDefault(x => x.Id == c.Id);
            if (vm is null)
            {
                Active.Insert(0, new ConnectionVm(c));
            }
            else
            {
                vm.Item.Upload = c.Upload;
                vm.Item.Download = c.Download;
                vm.Item.UploadSpeed = c.UploadSpeed;
                vm.Item.DownloadSpeed = c.DownloadSpeed;
                vm.UploadSpeed = c.UploadSpeed;
                vm.DownloadSpeed = c.DownloadSpeed;
                vm.Refresh();
            }
        }

        TotalsText = $"↑ {Format.Bytes(merged.UploadTotal)}　　↓ {Format.Bytes(merged.DownloadTotal)}";
        ApplySort();
        RefreshView();
        return Task.CompletedTask;
    }

    private void RefreshView()
    {
        CountText = $"活跃 {Active.Count} · 已关闭 {Closed.Count}";
    }

    partial void OnSortModeChanged(string value) => ApplySort();

    /// <summary>按上传/下载速率或流量排序活跃连接。</summary>
    private void ApplySort()
    {
        if (SortMode is not ("upload" or "download")) return;
        List<ConnectionVm> sorted = SortMode switch
        {
            "upload" => Active.OrderByDescending(x => x.UploadSpeed + x.Item.Upload).ToList(),
            "download" => Active.OrderByDescending(x => x.DownloadSpeed + x.Item.Download).ToList(),
            _ => [],
        };
        for (var i = 0; i < sorted.Count; i++)
        {
            var idx = Active.IndexOf(sorted[i]);
            if (idx > i)
                Active.Move(idx, i);
        }
    }

    public async Task CloseAsync(ConnectionVm vm)
    {
        try
        {
            await AppServices.Api.CloseConnectionAsync(vm.Id);
        }
        catch (Exception ex)
        {
            LogService.App("关闭连接失败: " + ex.Message, "warn");
        }
    }

    public async Task CloseAllAsync()
    {
        try
        {
            await AppServices.Api.CloseAllConnectionsAsync();
        }
        catch (Exception ex)
        {
            LogService.App("关闭全部连接失败: " + ex.Message, "warn");
        }
    }

    public bool Matches(ConnectionVm vm)
    {
        var q = SearchText?.Trim();
        if (string.IsNullOrEmpty(q)) return true;
        return vm.Host.Contains(q, StringComparison.OrdinalIgnoreCase) ||
               vm.Rule.Contains(q, StringComparison.OrdinalIgnoreCase) ||
               vm.Process.Contains(q, StringComparison.OrdinalIgnoreCase) ||
               vm.Type.Contains(q, StringComparison.OrdinalIgnoreCase);
    }
}
