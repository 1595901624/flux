using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Flux.Services;
using Flux.Utils;

namespace Flux.Views;

public sealed partial class SettingsPage : Page
{
    private bool _loading = true;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadFromConfig();
    }

    private void LoadFromConfig()
    {
        _loading = true;
        var verge = AppServices.Config.Verge;

        AutoLaunchSwitch.IsOn = AutoStartService.IsEnabled();
        SilentStartSwitch.IsOn = verge.EnableSilentStart;
        SysProxySwitch.IsOn = verge.EnableSystemProxy;
        ProxyGuardSwitch.IsOn = verge.EnableProxyGuard;
        BypassBox.Text = verge.SystemProxyBypass;
        MixedPortBox.Value = AppServices.Config.MixedPort;
        AllowLanSwitch.IsOn = AppServices.Config.GetBool("allow-lan", false);
        Ipv6Switch.IsOn = AppServices.Config.GetBool("ipv6", true);
        TunSwitch.IsOn = verge.EnableTunMode && TrayService.IsElevated();
        AutoCloseConnSwitch.IsOn = verge.AutoCloseConnection;
        EnableLogSwitch.IsOn = verge.EnableLog;

        SelectByTag(LogLevelBox, verge.LogLevel);
        SelectByTag(ThemeBox, verge.ThemeMode);
        _loading = false;
    }

    private static void SelectByTag(ComboBox box, string tag)
    {
        foreach (ComboBoxItem item in box.Items)
        {
            if ((string?)item.Tag == tag)
            {
                box.SelectedItem = item;
                return;
            }
        }
    }

    private static string? SelectedTag(ComboBox box) =>
        (box.SelectedItem as ComboBoxItem)?.Tag as string;

    private bool SaveVerge(Action<Flux.Models.VergeConfig> patch)
    {
        if (_loading) return false;
        var verge = AppServices.Config.Verge;
        patch(verge);
        AppServices.Config.SaveVerge();
        return true;
    }

    // ---------- 系统设置 ----------

    private void AutoLaunch_Toggled(object sender, RoutedEventArgs e)
    {
        try
        {
            AutoStartService.SetEnabled(AutoLaunchSwitch.IsOn, AppServices.Config.Verge.EnableSilentStart);
        }
        catch (Exception ex)
        {
            LogService.App("自启动设置失败: " + ex.Message, "warn");
        }
    }

    private void SilentStart_Toggled(object sender, RoutedEventArgs e)
    {
        if (SaveVerge(v => v.EnableSilentStart = SilentStartSwitch.IsOn))
            AutoStartService.SetEnabled(AutoLaunchSwitch.IsOn, SilentStartSwitch.IsOn);
    }

    private void SysProxy_Toggled(object sender, RoutedEventArgs e)
    {
        if (!SaveVerge(v => v.EnableSystemProxy = SysProxySwitch.IsOn)) return;
        var verge = AppServices.Config.Verge;
        _ = Task.Run(() => AppServices.SysProxy.Apply(verge));
    }

    private void ProxyGuard_Toggled(object sender, RoutedEventArgs e)
    {
        if (!SaveVerge(v => v.EnableProxyGuard = ProxyGuardSwitch.IsOn)) return;
        var verge = AppServices.Config.Verge;
        _ = Task.Run(() => AppServices.SysProxy.Apply(verge));
    }

    private void Bypass_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!SaveVerge(v => v.SystemProxyBypass = BypassBox.Text)) return;
        var verge = AppServices.Config.Verge;
        _ = Task.Run(() => AppServices.SysProxy.Apply(verge));
    }

    // ---------- Clash 设置 ----------

    private async void MixedPort_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading) return;
        var port = (int)MixedPortBox.Value;
        if (port < 1024 || port > 65535) return;
        AppServices.Config.PatchClashBase("mixed-port", port);
        await AppServices.Core.ApplyConfigAsync();
        var verge = AppServices.Config.Verge;
        await Task.Run(() => AppServices.SysProxy.Apply(verge));
    }

    private async void AllowLan_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AppServices.Config.PatchClashBase("allow-lan", AllowLanSwitch.IsOn);
        await AppServices.Core.ApplyConfigAsync();
    }

    private async void Ipv6_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AppServices.Config.PatchClashBase("ipv6", Ipv6Switch.IsOn);
        await AppServices.Core.ApplyConfigAsync();
    }

    private async void LogLevel_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        var level = SelectedTag(LogLevelBox);
        if (level is null) return;
        SaveVerge(v => v.LogLevel = level);
        AppServices.Config.PatchClashBase("log-level", level);
        await AppServices.Core.ApplyConfigAsync();
    }

    private async void Tun_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (TunSwitch.IsOn && !TrayService.IsElevated())
        {
            _loading = true;
            TunSwitch.IsOn = false;
            _loading = false;
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "需要管理员权限",
                Content = "TUN 模式需要以管理员身份运行 Flux。请右键应用选择「以管理员身份运行」后再开启。",
                CloseButtonText = "知道了",
                DefaultButton = ContentDialogButton.Close,
            };
            await dialog.ShowAsync();
            return;
        }
        SaveVerge(v => v.EnableTunMode = TunSwitch.IsOn);
        await AppServices.Core.ApplyConfigAsync();
    }

    private void AutoCloseConn_Toggled(object sender, RoutedEventArgs e)
    {
        SaveVerge(v => v.AutoCloseConnection = AutoCloseConnSwitch.IsOn);
    }

    private void EnableLog_Toggled(object sender, RoutedEventArgs e)
    {
        SaveVerge(v => v.EnableLog = EnableLogSwitch.IsOn);
    }

    // ---------- 应用 ----------

    private void Theme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        var mode = SelectedTag(ThemeBox);
        if (mode is null) return;
        SaveVerge(v => v.ThemeMode = mode);
        App.ApplyTheme(mode);
    }

    private async void RestartCore_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await AppServices.Core.RestartAsync();
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "重启失败",
                Content = ex.Message,
                CloseButtonText = "确定",
            };
            await dialog.ShowAsync();
        }
    }

    private void OpenData_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start("explorer.exe", Paths.AppDataDir); } catch { }
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start("explorer.exe", Paths.LogsDir); } catch { }
    }

    private void ExitApp_Click(object sender, RoutedEventArgs e)
    {
        AppServices.Tray.ExitApp();
    }
}
