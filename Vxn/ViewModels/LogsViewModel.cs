using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Vxn.Services;
using Windows.UI;

namespace Vxn.ViewModels;

/// <summary>日志显示模型。</summary>
public class LogItemVm
{
    public LogLine Line { get; }
    public LogItemVm(LogLine line) => Line = line;

    public string TimeText => Line.Time.ToString("HH:mm:ss");
    public string TypeText => Line.Type.ToUpperInvariant();
    public string Payload => Line.Payload;

    public SolidColorBrush TypeBrush => Line.Type.ToLowerInvariant() switch
    {
        "warning" => new SolidColorBrush(Colors.DarkOrange),
        "error" or "fatal" => new SolidColorBrush(Colors.IndianRed),
        "debug" => new SolidColorBrush(Colors.Gray),
        _ => new SolidColorBrush(Color.FromArgb(255, 0, 120, 212)),
    };
}

public partial class LogsViewModel : ObservableObject
{
    private const int MaxCount = 1000;

    public ObservableCollection<LogItemVm> Items { get; } = new();

    [ObservableProperty]
    private string _level = "info"; // all|debug|info|warning|error|silent

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private bool _paused;

    [ObservableProperty]
    private string _countText = "";

    private readonly Queue<LogItemVm> _buffer = new();
    private DispatcherQueueTimer? _flushTimer;

    partial void OnLevelChanged(string value) { }
    partial void OnPausedChanged(bool value) { }

    public LogsViewModel()
    {
        AppServices.Streams.Log += OnLog;
        // 已缓冲的内核启动日志
        foreach (var line in LogService.GetCoreLogs().TakeLast(200))
        {
            Items.Add(new LogItemVm(line));
        }
    }

    private void OnLog(LogLine line)
    {
        if (Paused) return;
        _flushTimer ??= App.UiDispatcher.CreateTimer();
        _pending.Enqueue(line);
        if (!_flushTimer.IsRunning)
        {
            _flushTimer.Interval = TimeSpan.FromMilliseconds(200);
            _flushTimer.Tick += (_, _) => { _flushTimer.Stop(); Flush(); };
            _flushTimer.Start();
        }
    }

    private readonly Queue<LogLine> _pending = new();

    private void Flush()
    {
        while (_pending.Count > 0)
        {
            var line = _pending.Dequeue();
            var vm = new LogItemVm(line);
            _buffer.Enqueue(vm);
            Items.Add(vm);
        }
        while (Items.Count > MaxCount)
        {
            Items.RemoveAt(0);
            _buffer.Dequeue();
        }
        CountText = $"{Items.Count} 条";
    }

    [RelayCommand]
    public void Clear()
    {
        Items.Clear();
        _buffer.Clear();
        CountText = "";
    }

    public void TogglePause() => Paused = !Paused;

    public bool Matches(LogItemVm item)
    {
        // 级别过滤（内核 stdout 日志 type=core 始终显示）
        if (Level != "all" && item.Line.Type != "core")
        {
            var t = item.Line.Type.ToLowerInvariant();
            var ok = Level switch
            {
                "debug" => true,
                "info" => t is "info" or "warning" or "error" or "fatal",
                "warning" => t is "warning" or "error" or "fatal",
                "error" => t is "error" or "fatal",
                _ => true,
            };
            if (!ok) return false;
        }
        var q = SearchText?.Trim();
        if (string.IsNullOrEmpty(q)) return true;
        return item.Payload.Contains(q, StringComparison.OrdinalIgnoreCase);
    }
}
