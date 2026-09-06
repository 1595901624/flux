using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Vxn.ViewModels;

namespace Vxn.Views;

public sealed partial class RulesPage : Page
{
    public RulesViewModel Vm { get; } = new();

    public RulesPage()
    {
        InitializeComponent();
        RuleList.ItemsSource = Vm.Items;
        Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Vm.CountText))
                EmptyPanel.Visibility = Vm.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        };
        Loaded += async (_, _) =>
        {
            if (Vm.Items.Count == 0)
                await Vm.LoadAsync();
        };
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await Vm.LoadAsync();

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        Vm.SearchText = SearchBox.Text;
    }
}
