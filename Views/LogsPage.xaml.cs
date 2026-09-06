using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Flux.ViewModels;

namespace Flux.Views;

public sealed partial class LogsPage : Page
{
    public LogsViewModel Vm { get; } = new();
    private readonly List<LogItemVm> _filtered = new();
    private readonly DispatcherQueueTimer _filterTimer;

    public LogsPage()
    {
        InitializeComponent();

        _filterTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _filterTimer.Interval = TimeSpan.FromMilliseconds(300);
        _filterTimer.Tick += (_, _) =>
        {
            _filterTimer.Stop();
            ApplyFilter();
        };

        // VM 构造时可能已带入内核启动日志，先全量刷一次
        Vm.Items.CollectionChanged += (_, _) =>
        {
            if (!_filterTimer.IsRunning) _filterTimer.Start();
        };

        Loaded += (_, _) => ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (LogList is null) return; // InitializeComponent 期间元素尚未就绪
        _filtered.Clear();
        foreach (var item in Vm.Items)
        {
            if (Vm.Matches(item)) _filtered.Add(item);
        }
        LogList.ItemsSource = _filtered;
        if (_filtered.Count > 0)
        {
            LogList.ScrollIntoView(_filtered[^1]);
        }
        EmptyPanel.Visibility = _filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LevelBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LogList is null) return; // XAML 解析期间 SelectedIndex 提前触发
        if (LevelBox.SelectedItem is ComboBoxItem item && item.Tag is string level)
        {
            Vm.Level = level;
            ApplyFilter();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        Vm.SearchText = SearchBox.Text;
        ApplyFilter();
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        Vm.TogglePause();
        var paused = Vm.Paused;
        if (PauseButton.Content is StackPanel panel)
        {
            if (panel.Children[0] is FontIcon icon) icon.Glyph = paused ? "\uE768" : "\uE769";
            if (panel.Children[1] is TextBlock text) text.Text = paused ? "继续" : "暂停";
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        Vm.Clear();
        ApplyFilter();
    }
}
