using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Flux.ViewModels;

namespace Flux.Views;

public sealed partial class ProxiesPage : Page
{
    public ProxiesViewModel Vm { get; } = new();
    private readonly CollectionViewSource _groupedView = new();
    private bool _syncingMode;

    public ProxiesPage()
    {
        InitializeComponent();

        _groupedView.IsSourceGrouped = true;
        _groupedView.ItemsPath = new Microsoft.UI.Xaml.PropertyPath("Nodes");
        _groupedView.Source = Vm.Groups;
        GroupList.ItemsSource = _groupedView.View;

        Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Vm.IsEmpty))
                EmptyPanel.Visibility = Vm.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
            else if (e.PropertyName == nameof(Vm.Mode))
                SyncModeSegment();
        };

        GroupList.SizeChanged += (_, _) => UpdateCardColumns();
        GroupList.LayoutUpdated += (_, _) => UpdateCardColumns();
        Loaded += async (_, _) =>
        {
            UpdateCardColumns();
            SyncModeSegment();
            Vm.StartPolling();
            await Task.CompletedTask;
        };
        Unloaded += (_, _) => Vm.StopPolling();
    }

    private double _lastCardsWidth;

    /// <summary>节点卡片按可用宽度自适应 1~3 列，撑满不留白。</summary>
    private void UpdateCardColumns()
    {
        if (GroupList.ItemsPanelRoot is not ItemsWrapGrid panel) return;
        var w = GroupList.ActualWidth - 6; // 预留滚动条
        if (w < 240 || Math.Abs(w - _lastCardsWidth) < 1) return;
        _lastCardsWidth = w;
        var cols = Math.Clamp((int)(w / 300.0), 1, 3);
        panel.ItemWidth = Math.Floor(w / cols);
    }

    /// <summary>把 VM 的当前模式同步到分段控件；程序化选中需抑制事件，避免反向触发切换。</summary>
    private void SyncModeSegment()
    {
        _syncingMode = true;
        try
        {
            foreach (SegmentedItem item in ModeSegment.Items)
            {
                if ((string?)item.Tag == Vm.Mode)
                {
                    ModeSegment.SelectedItem = item;
                    break;
                }
            }
        }
        finally
        {
            _syncingMode = false;
        }
    }

    private async void ModeSegment_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingMode) return;
        if (ModeSegment.SelectedItem is SegmentedItem item && item.Tag is string mode)
        {
            await Vm.SetModeAsync(mode);
        }
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        Vm.FilterText = FilterBox.Text;
        Vm.ApplyFilter();
    }

    private async void GroupList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ProxiesNodeVm node)
        {
            await Vm.SelectNodeAsync(node.GroupName, node.Name);
        }
    }
}
