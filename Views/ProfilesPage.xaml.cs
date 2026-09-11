using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Flux.ViewModels;

namespace Flux.Views;

public sealed partial class ProfilesPage : Page
{
    public ProfilesViewModel Vm { get; } = new();

    public ProfilesPage()
    {
        InitializeComponent();
        CardList.ItemsSource = Vm.Items;
        CardList.SizeChanged += (_, _) => UpdateCardColumns();
        CardList.LayoutUpdated += (_, _) => UpdateCardColumns();
        Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Vm.Busy) || e.PropertyName == nameof(Vm.StatusText))
                UpdateEmptyState();
        };
        Loaded += (_, _) => { Vm.Start(); Vm.Load(); UpdateEmptyState(); UpdateCardColumns(); };
        Unloaded += (_, _) => Vm.Stop();
    }

    private double _lastCardsWidth;

    /// <summary>订阅卡片按可用宽度自适应 1~3 列，撑满不留白。</summary>
    private void UpdateCardColumns()
    {
        if (CardList.ItemsPanelRoot is not ItemsWrapGrid panel) return;
        var w = CardList.ActualWidth - 6;
        if (w < 300 || Math.Abs(w - _lastCardsWidth) < 1) return;
        _lastCardsWidth = w;
        var cols = Math.Clamp((int)(w / 380.0), 1, 3);
        panel.ItemWidth = Math.Floor(w / cols);
    }

    private void UpdateEmptyState()
    {
        EmptyPanel.Visibility = Vm.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void UrlBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            Vm.ImportUrl = UrlBox.Text;
            await Vm.ImportAsync();
            UpdateEmptyState();
        }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        Vm.ImportUrl = UrlBox.Text;
        await Vm.ImportAsync();
        UpdateEmptyState();
    }

    private async void ImportLocal_Click(object sender, RoutedEventArgs e)
    {
        await Vm.ImportLocalAsync();
        UpdateEmptyState();
    }

    private async void CreateEmpty_Click(object sender, RoutedEventArgs e)
    {
        await Vm.CreateEmptyAsync();
        UpdateEmptyState();
    }

    private async void UpdateAll_Click(object sender, RoutedEventArgs e)
    {
        await Vm.UpdateAllAsync();
    }

    private async void CardList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ProfileItemVm vm)
            await Vm.SelectAsync(vm);
    }

    private ProfileItemVm? VmFromMenu(object sender)
    {
        if (sender is FrameworkElement { DataContext: ProfileItemVm vm }) return vm;
        return null;
    }

    private async void MenuSelect_Click(object sender, RoutedEventArgs e)
    {
        if (VmFromMenu(sender) is { } vm) await Vm.SelectAsync(vm);
    }

    private async void MenuUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (VmFromMenu(sender) is { } vm) await Vm.UpdateAsync(vm);
    }

    private async void MenuEdit_Click(object sender, RoutedEventArgs e)
    {
        if (VmFromMenu(sender) is not { } vm) return;
        var dialog = new ProfileEditDialog(vm.Item, XamlRoot);
        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;
        var error = dialog.Apply();
        if (error is not null)
        {
            var warn = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "保存失败",
                Content = error,
                CloseButtonText = "确定",
            };
            await warn.ShowAsync();
        }
        Vm.Load();
    }

    private async void MenuEnhance_Click(object sender, RoutedEventArgs e)
    {
        if (VmFromMenu(sender) is not { } vm) return;
        var dialog = new EnhanceEditorDialog(vm.Item, XamlRoot);
        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;
        try
        {
            dialog.SaveCurrent();
            await dialog.ApplyConfigAsync();
        }
        catch (Exception ex)
        {
            var warn = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "保存失败",
                Content = ex.Message,
                CloseButtonText = "确定",
            };
            await warn.ShowAsync();
        }
    }

    private async void MenuGlobalEnhance_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new EnhanceEditorDialog(null, XamlRoot);
        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;
        try
        {
            dialog.SaveCurrent();
            await dialog.ApplyConfigAsync();
        }
        catch (Exception ex)
        {
            var warn = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "保存失败",
                Content = ex.Message,
                CloseButtonText = "确定",
            };
            await warn.ShowAsync();
        }
    }

    private async void MenuUp_Click(object sender, RoutedEventArgs e)
    {
        if (VmFromMenu(sender) is { } vm) await Vm.MoveAsync(vm, -1);
    }

    private async void MenuDown_Click(object sender, RoutedEventArgs e)
    {
        if (VmFromMenu(sender) is { } vm) await Vm.MoveAsync(vm, +1);
    }

    private async void MenuDelete_Click(object sender, RoutedEventArgs e)
    {
        if (VmFromMenu(sender) is { } vm)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "删除订阅",
                Content = $"确定删除「{vm.DisplayName}」吗？",
                PrimaryButtonText = "删除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                await Vm.DeleteAsync(vm);
            UpdateEmptyState();
        }
    }
}
