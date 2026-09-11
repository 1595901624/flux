using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Flux.Models;
using Flux.Services;
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

        private void SortBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SortBox.SelectedItem is ComboBoxItem item && item.Tag is string mode)
            Vm.SortMode = mode;
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
                PrimaryButtonText = L10n.T("Msg_CloseThisConn"),
                CloseButtonText = L10n.T("Msg_Back"),
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
            L10n.F("Msg_DetailNetwork", vm.Network, vm.Type),
            L10n.F("Msg_DetailHost", vm.Host),
            L10n.F("Msg_DetailDestination", vm.Destination),
            L10n.F("Msg_DetailSource", vm.Source),
            L10n.F("Msg_DetailRule", vm.Rule),
            L10n.F("Msg_DetailChains", vm.Chains),
            L10n.F("Msg_DetailProcess", vm.Process),
            L10n.F("Msg_DetailTraffic", vm.DownloadText, vm.UploadText),
            L10n.F("Msg_DetailTime", vm.StartText),
        };
        var panel = new StackPanel { Spacing = 6 };
        foreach (var line in lines)
        {
            panel.Children.Add(new TextBlock { Text = line, FontSize = 13, IsTextSelectionEnabled = true });
        }
        return panel;
    }
}
