using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Flux.Services;
using Flux.ViewModels;

namespace Flux.Views;

public sealed partial class HomePage : Page
{
    public HomeViewModel Vm { get; } = new();
    private bool _syncingMode;

    public HomePage()
    {
        InitializeComponent();
        Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HomeViewModel.Mode))
                SyncModeRadios();
        };
        Loaded += async (_, _) =>
        {
            Vm.StartTimer();
            SyncModeRadios();
            await Task.CompletedTask;
        };
        Unloaded += (_, _) => Vm.StopTimer();
    }

    private void SyncModeRadios()
    {
        _syncingMode = true;
        try
        {
            var mode = Vm.Mode;
            for (var i = 0; i < ModeRadios.Items.Count; i++)
            {
                if (ModeRadios.Items[i] is RadioButton rb && (string?)rb.Tag == mode)
                {
                    ModeRadios.SelectedIndex = i;
                    break;
                }
            }
        }
        finally
        {
            _syncingMode = false;
        }
    }

    private async void SysProxy_Toggled(object sender, RoutedEventArgs e)
    {
        try
        {
            await Vm.ToggleSystemProxyAsync(((ToggleSwitch)sender).IsOn);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("系统代理切换失败", ex.Message);
        }
    }

    private async void Tun_Toggled(object sender, RoutedEventArgs e)
    {
        try
        {
            await Vm.ToggleTunAsync(((ToggleSwitch)sender).IsOn);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("TUN 切换失败", ex.Message);
        }
    }

    private async void Mode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingMode) return;
        if (ModeRadios.SelectedItem is RadioButton rb && rb.Tag is string mode)
        {
            try
            {
                await AppServices.Api.PatchConfigsAsync(new() { ["mode"] = mode });
                AppServices.Config.PatchClashBase("mode", mode);
            }
            catch { }
        }
    }

    private async void RestartCore_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await AppServices.Core.RestartAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("内核重启失败", ex.Message);
        }
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", Paths.LogsDir);
        }
        catch { }
    }

    private void NavProxies_Click(object sender, RoutedEventArgs e)
    {
        App.MainWindow?.NavigateTo("proxies");
    }

    private void NavProfiles_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        App.MainWindow?.NavigateTo("profiles");
    }

    private async Task ShowErrorAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "确定",
            DefaultButton = ContentDialogButton.Close,
        };
        await dialog.ShowAsync();
    }
}
