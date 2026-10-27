using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Flux.ViewModels;

namespace Flux.Views;

public sealed partial class LogsPage : Page
{
    public LogsViewModel Vm { get; } = new();
    private readonly ObservableCollection<LogItemVm> _filtered = new();
    private readonly DispatcherQueueTimer _filterTimer;
    private ScrollViewer? _scrollViewer;
    private bool _isScrolling;

    public LogsPage()
    {
        InitializeComponent();
        LogList.ItemsSource = _filtered;

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

        Loaded += (_, _) =>
        {
            _scrollViewer = FindScrollViewer(LogList);
            if (_scrollViewer is not null) _scrollViewer.ViewChanged += OnViewChanged;
            Vm.Start();
            ApplyFilter(scrollToLatest: true);
        };
        Unloaded += (_, _) =>
        {
            Vm.Stop();
            _filterTimer.Stop();
            if (_scrollViewer is not null) _scrollViewer.ViewChanged -= OnViewChanged;
            _scrollViewer = null;
            _isScrolling = false;
        };
    }

    private void ApplyFilter(bool scrollToLatest = false)
    {
        if (LogList is null) return; // InitializeComponent 期间元素尚未就绪
        var followLatest = scrollToLatest || (!_isScrolling && (_scrollViewer is null ||
            (Vm.NewestFirst ? _scrollViewer.VerticalOffset <= 2 :
                _scrollViewer.ScrollableHeight - _scrollViewer.VerticalOffset <= 2)));
        var snapshot = Vm.Items.Where(Vm.Matches).ToArray();
        var visible = snapshot.ToHashSet();
        // 只通过可观察集合更新控件，保留仍可见的行及滚动位置。
        for (var i = _filtered.Count - 1; i >= 0; i--)
            if (!visible.Contains(_filtered[i])) _filtered.RemoveAt(i);
        for (var i = 0; i < snapshot.Length; i++)
        {
            if (i < _filtered.Count && _filtered[i] == snapshot[i]) continue;
            var existingIndex = _filtered.IndexOf(snapshot[i]);
            if (existingIndex >= 0) _filtered.Move(existingIndex, i);
            else _filtered.Insert(i, snapshot[i]);
        }
        EmptyPanel.Visibility = _filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (followLatest && _filtered.Count > 0)
        {
            LogList.ScrollIntoView(Vm.NewestFirst ? _filtered[0] : _filtered[^1]);
        }
    }

    private void OnViewChanged(object? sender, ScrollViewerViewChangedEventArgs e) => _isScrolling = e.IsIntermediate;

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer viewer) return viewer;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindScrollViewer(VisualTreeHelper.GetChild(root, i)) is { } child) return child;
        return null;
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
            if (panel.Children[1] is TextBlock text) text.Text = paused ? Flux.Services.L10n.T("Logs_Continue") : Flux.Services.L10n.T("Logs_Pause");
        }
    }

    private void NewestFirst_Changed(object sender, RoutedEventArgs e)
    {
        Vm.NewestFirst = NewestFirstToggle.IsChecked == true;
        ApplyFilter(scrollToLatest: true);
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        Vm.Clear();
        ApplyFilter();
    }
}
