using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Flux.Services;
using Flux.Utils;

namespace Flux.Views;

public sealed partial class SettingsPage : Page
{
    private bool _loading = true;

    public string AppVersion => $"基于 WinUI 3 的 Clash/Mihomo 客户端 · v{GetAppVersion()}";

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadFromConfig();
    }

    private static string GetAppVersion()
    {
        var version = typeof(SettingsPage).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion;

        return version?.Split('+')[0]
            ?? typeof(SettingsPage).Assembly.GetName().Version?.ToString(3)
            ?? "未知";
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

    private async void SysProxy_Toggled(object sender, RoutedEventArgs e)
    {
        var verge = AppServices.Config.Verge;
        var previous = verge.EnableSystemProxy;
        if (!SaveVerge(v => v.EnableSystemProxy = SysProxySwitch.IsOn)) return;
        try { await Task.Run(() => AppServices.SysProxy.Apply(verge)); }
        catch (Exception ex)
        {
            verge.EnableSystemProxy = previous;
            AppServices.Config.SaveVerge();
            _loading = true;
            SysProxySwitch.IsOn = previous;
            _loading = false;
            LogService.App("系统代理设置失败: " + ex.Message, "error");
        }
    }

    private async void ProxyGuard_Toggled(object sender, RoutedEventArgs e)
    {
        if (!SaveVerge(v => v.EnableProxyGuard = ProxyGuardSwitch.IsOn)) return;
        var verge = AppServices.Config.Verge;
        try { await Task.Run(() => AppServices.SysProxy.Apply(verge)); }
        catch (Exception ex) { LogService.App("代理守护设置失败: " + ex.Message, "error"); }
    }

    private async void Bypass_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!SaveVerge(v => v.SystemProxyBypass = BypassBox.Text)) return;
        var verge = AppServices.Config.Verge;
        try { await Task.Run(() => AppServices.SysProxy.Apply(verge)); }
        catch (Exception ex) { LogService.App("代理绕过设置失败: " + ex.Message, "error"); }
    }

    // ---------- Clash 设置 ----------

    private async void MixedPort_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading) return;
        var port = (int)MixedPortBox.Value;
        if (port < 1024 || port > 65535) return;
        var previous = AppServices.Config.MixedPort;
        if (port == previous) return;
        AppServices.Config.PatchClashBase("mixed-port", port);
        if (await AppServices.Core.ApplyConfigAsync())
        {
            var verge = AppServices.Config.Verge;
            await Task.Run(() => AppServices.SysProxy.Apply(verge));
            return;
        }
        AppServices.Config.PatchClashBase("mixed-port", previous);
        _loading = true;
        MixedPortBox.Value = previous;
        _loading = false;
        await ShowApplyFailureAsync();
    }

    private async void AllowLan_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var previous = AppServices.Config.GetBool("allow-lan", false);
        AppServices.Config.PatchClashBase("allow-lan", AllowLanSwitch.IsOn);
        if (!await AppServices.Core.ApplyConfigAsync())
        {
            AppServices.Config.PatchClashBase("allow-lan", previous);
            _loading = true; AllowLanSwitch.IsOn = previous; _loading = false;
            await ShowApplyFailureAsync();
        }
    }

    private async void Ipv6_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var previous = AppServices.Config.GetBool("ipv6", true);
        AppServices.Config.PatchClashBase("ipv6", Ipv6Switch.IsOn);
        if (!await AppServices.Core.ApplyConfigAsync())
        {
            AppServices.Config.PatchClashBase("ipv6", previous);
            _loading = true; Ipv6Switch.IsOn = previous; _loading = false;
            await ShowApplyFailureAsync();
        }
    }

    private async void LogLevel_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        var level = SelectedTag(LogLevelBox);
        if (level is null) return;
        var previous = AppServices.Config.GetScalar("log-level", "info");
        var previousVerge = AppServices.Config.Verge.LogLevel;
        SaveVerge(v => v.LogLevel = level);
        AppServices.Config.PatchClashBase("log-level", level);
        if (!await AppServices.Core.ApplyConfigAsync())
        {
            SaveVerge(v => v.LogLevel = previousVerge);
            AppServices.Config.PatchClashBase("log-level", previous);
            _loading = true; SelectByTag(LogLevelBox, previousVerge); _loading = false;
            await ShowApplyFailureAsync();
        }
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
        var previous = AppServices.Config.Verge.EnableTunMode;
        SaveVerge(v => v.EnableTunMode = TunSwitch.IsOn);
        if (!await AppServices.Core.ApplyConfigAsync())
        {
            SaveVerge(v => v.EnableTunMode = previous);
            _loading = true; TunSwitch.IsOn = previous; _loading = false;
            await ShowApplyFailureAsync();
        }
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

    private void OpenGitHub_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/1595901624/flux",
                UseShellExecute = true,
            });
        }
        catch { }
    }

    private void ExitApp_Click(object sender, RoutedEventArgs e)
    {
        AppServices.Tray.ExitApp();
    }

    private async Task ShowApplyFailureAsync()
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "配置未应用",
            Content = "内核未运行或拒绝了新配置，设置已恢复。请查看日志后重试。",
            CloseButtonText = "确定",
        };
        await dialog.ShowAsync();
    }
}
