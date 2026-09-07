using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Flux.Models;
using Flux.ViewModels;

namespace Flux.Views;

public sealed partial class ConnectionsPage : Page
{
    public ConnectionsViewModel Vm { get; } = new();
    private readonly List<ConnectionVm> _filtered = new();

    public ConnectionsPage()
    {
        InitializeComponent();
        ConnList.ItemsSource = _filtered;
        Vm.Active.CollectionChanged += (_, _) => ApplyFilter();
        Vm.Closed.CollectionChanged += (_, _) => ApplyFilter();
        Loaded += (_, _) => Vm.Start();
        Unloaded += (_, _) => Vm.Stop();
    }

    private void ApplyFilter()
    {
        var source = (IEnumerable<ConnectionVm>?)(ClosedToggle.IsChecked == true ? Vm.Closed : Vm.Active);
        _filtered.Clear();
        if (source is not null)
        {
            var q = Vm.SearchText;
            foreach (var c in source)
            {
                if (string.IsNullOrEmpty(q) || Vm.Matches(c)) _filtered.Add(c);
            }
        }
        ConnList.ItemsSource = null;
        ConnList.ItemsSource = _filtered;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        Vm.SearchText = SearchBox.Text;
        ApplyFilter();
    }

    private void ClosedToggle_Changed(object sender, RoutedEventArgs e)
    {
        ApplyFilter();
    }

    private async void CloseAll_Click(object sender, RoutedEventArgs e)
    {
        await Vm.CloseAllAsync();
    }

    private async void ConnList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (ClosedToggle.IsChecked == true) return;
        if (e.ClickedItem is ConnectionVm vm)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = vm.Host,
                Content = BuildDetail(vm),
                PrimaryButtonText = "关闭此连接",
                CloseButtonText = "返回",
                DefaultButton = ContentDialogButton.Close,
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                await Vm.CloseAsync(vm);
        }
    }

    private object BuildDetail(ConnectionVm vm)
    {
        var lines = new List<string>
        {
            $"网络: {vm.Network} ({vm.Type})",
            $"主机: {vm.Host}",
            $"目标: {vm.Destination}",
            $"来源: {vm.Source}",
            $"规则: {vm.Rule}",
            $"链路: {vm.Chains}",
            $"进程: {vm.Process}",
            $"下载: {vm.DownloadText}   上传: {vm.UploadText}",
            $"时间: {vm.StartText}",
        };
        var panel = new StackPanel { Spacing = 6 };
        foreach (var line in lines)
        {
            panel.Children.Add(new TextBlock { Text = line, FontSize = 13, IsTextSelectionEnabled = true });
        }
        return panel;
    }
}
