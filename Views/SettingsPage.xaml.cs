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
        Loaded += async (_, _) => await LoadFromConfigAsync();
    }

    private static string GetAppVersion()
    {
        // MSIX 安装包的版本由 Package.appxmanifest 提供；CI 发布时会更新它。
        // 未打包的开发/便携版没有包身份，因此回退到 csproj 写入的程序集版本。
        try
        {
            var packageVersion = Windows.ApplicationModel.Package.Current.Id.Version;
            return packageVersion.Revision == 0
                ? $"{packageVersion.Major}.{packageVersion.Minor}.{packageVersion.Build}"
                : $"{packageVersion.Major}.{packageVersion.Minor}.{packageVersion.Build}.{packageVersion.Revision}";
        }
        catch (InvalidOperationException)
        {
            // 未打包运行时没有 Package.Current。
        }

        var assemblyVersion = typeof(SettingsPage).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion;

        return assemblyVersion?.Split('+')[0]
            ?? typeof(SettingsPage).Assembly.GetName().Version?.ToString(3)
            ?? "未知";
    }

    private async Task LoadFromConfigAsync()
    {
        _loading = true;
        var verge = AppServices.Config.Verge;

        try
        {
            AutoLaunchSwitch.IsOn = await AutoStartService.IsEnabledAsync();
        }
        catch (Exception ex)
        {
            AutoLaunchSwitch.IsOn = false;
            LogService.App("读取自启动状态失败: " + ex.Message, "warn");
        }
        SilentStartSwitch.IsOn = verge.EnableSilentStart;
        SysProxySwitch.IsOn = verge.EnableSystemProxy;
        PacSwitch.IsOn = verge.EnablePacMode;
        HotkeyWindowBox.Text = verge.Hotkeys.GetValueOrDefault("show_hide_window", "");
        HotkeySysproxyBox.Text = verge.Hotkeys.GetValueOrDefault("toggle_system_proxy", "");
        HotkeyTunBox.Text = verge.Hotkeys.GetValueOrDefault("toggle_tun", "");
        HotkeyReactivateBox.Text = verge.Hotkeys.GetValueOrDefault("reactivate_profile", "");
        LightweightSwitch.IsOn = verge.EnableLightweightMode;
        _loading = true;
        var savedLang = verge.Language is null or "" or "system" ? "system" : verge.Language;
        foreach (var item in LanguageBox.Items.OfType<ComboBoxItem>())
            if ((string)item.Tag == savedLang) { LanguageBox.SelectedItem = item; break; }
        if (LanguageBox.SelectedItem is null) LanguageBox.SelectedIndex = 0;
        _loading = false;
        LightweightMinutesBox.Text = verge.AutoLightweightMinutes.ToString();

        // TUN / DNS / 外部控制器 初始值（来自基础配置）
        var tunStack = Flux.Core.Config.YamlOps.GetScalar(AppServices.Config.ClashBase, "tun", "stack");
        SelectTag(TunStackBox, string.IsNullOrEmpty(tunStack) ? "gvisor" : tunStack);
        TunDnsHijackBox.Text = Flux.Core.Config.YamlOps.GetScalar(AppServices.Config.ClashBase, "tun", "dns-hijack") is { } hijack && hijack.Length > 0 ? hijack : "any:53";
        var dnsMode = Flux.Core.Config.YamlOps.GetScalar(AppServices.Config.ClashBase, "dns", "enhanced-mode");
        SelectTag(DnsModeBox, string.IsNullOrEmpty(dnsMode) ? "fake-ip" : dnsMode);
        DnsFakeIpRangeBox.Text = Flux.Core.Config.YamlOps.GetScalar(AppServices.Config.ClashBase, "dns", "fake-ip-range") ?? "";
        var (controllerInfo, secretInfo) = AppServices.Config.GetControllerInfo();
        ControllerBox.Text = controllerInfo;
        ControllerSecretBox.Password = secretInfo;
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

    private async void AutoLaunch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        try
        {
            await AutoStartService.SetEnabledAsync(AutoLaunchSwitch.IsOn, AppServices.Config.Verge.EnableSilentStart);
        }
        catch (Exception ex)
        {
            LogService.App("自启动设置失败: " + ex.Message, "warn");
            _loading = true;
            try { AutoLaunchSwitch.IsOn = await AutoStartService.IsEnabledAsync(); }
            finally { _loading = false; }
        }
    }

    private async void SilentStart_Toggled(object sender, RoutedEventArgs e)
    {
        if (!SaveVerge(v => v.EnableSilentStart = SilentStartSwitch.IsOn)) return;
        try
        {
            await AutoStartService.SetEnabledAsync(AutoLaunchSwitch.IsOn, SilentStartSwitch.IsOn);
        }
        catch (Exception ex)
        {
            LogService.App("更新自启动设置失败: " + ex.Message, "warn");
        }
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

    private async void Pac_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var verge = AppServices.Config.Verge;
        var previous = verge.EnablePacMode;
        if (!SaveVerge(v => v.EnablePacMode = PacSwitch.IsOn)) return;
        if (!verge.EnableSystemProxy) return; // PAC 仅在系统代理开启时生效，切换开关下次开启时应用
        try { await Task.Run(() => AppServices.SysProxy.Apply(verge)); }
        catch (Exception ex)
        {
            verge.EnablePacMode = previous;
            AppServices.Config.SaveVerge();
            _loading = true;
            PacSwitch.IsOn = previous;
            _loading = false;
            LogService.App("PAC 模式设置失败: " + ex.Message, "error");
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
        // 保存前检测端口占用（无效输入不写入配置）
        if (!Flux.Core.Utils.PortUtils.IsPortFree(port))
        {
            _loading = true;
            MixedPortBox.Value = previous;
            _loading = false;
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "端口已被占用",
                Content = $"端口 {port} 已被其他进程或本应用监听，请更换端口。",
                CloseButtonText = "确定",
            };
            await dialog.ShowAsync();
            return;
        }
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
        if (TunSwitch.IsOn && !TrayService.IsElevated() && !AppServices.Privilege.IsServiceReady())
        {
            _loading = true;
            TunSwitch.IsOn = false;
            _loading = false;
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "TUN 需要特权",
                Content = "TUN 模式需要特权运行内核。可以选择：\n\n" +
                          "1. 以管理员身份运行 Flux；\n" +
                          "2. 安装 Flux 服务（推荐，普通用户即可使用 TUN）。",
                PrimaryButtonText = "安装服务",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                await InstallServiceAsync();
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

    /// <summary>通过 UAC 安装 Flux 服务（普通用户即可 TUN 的前提）。</summary>
    private async Task InstallServiceAsync()
    {
        var result = await AppServices.Privilege.InstallServiceAsync();
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = result.Success ? "服务已安装" : "服务安装失败",
            Content = result.Success
                ? "Flux 服务已安装并启动，普通用户模式下即可开启 TUN。"
                : result.Error?.ToString() ?? "未知错误",
            CloseButtonText = "确定",
        };
        await dialog.ShowAsync();
    }

    private async void SaveHotkeys_Click(object sender, RoutedEventArgs e)
    {
        var hotkeys = new Dictionary<string, string>
        {
            ["show_hide_window"] = HotkeyWindowBox.Text.Trim(),
            ["toggle_system_proxy"] = HotkeySysproxyBox.Text.Trim(),
            ["toggle_tun"] = HotkeyTunBox.Text.Trim(),
            ["reactivate_profile"] = HotkeyReactivateBox.Text.Trim(),
        };

        var result = AppServices.Hotkey.ApplyHotkeys(hotkeys);
        var conflicts = result.Value ?? [];
        if (conflicts.Count > 0)
        {
            // 冲突：拒绝保存并显示冲突组合
            await ShowInfoAsync("热键保存被拒绝", string.Join(Environment.NewLine, conflicts));
            return;
        }

        AppServices.Config.Verge.Hotkeys = hotkeys;
        AppServices.Config.SaveVerge();
        await ShowInfoAsync("热键已保存", "全局热键已注册生效。");
    }

    private async void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (LanguageBox.SelectedItem is not ComboBoxItem { Tag: string lang }) return;
        var verge = AppServices.Config.Verge;
        if (!SaveVerge(v => v.Language = lang)) return;
        try
        {
            Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride =
                lang == "system" ? "" : lang;
        }
        catch { }
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "语言已更改",
            Content = "请重启应用以完整应用新语言设置。",
            PrimaryButtonText = "立即重启",
            CloseButtonText = "稍后",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            AppServices.ShutdownAsync().Wait(3000);
            Microsoft.Windows.AppLifecycle.AppInstance.Restart("");
        }
    }

    private void Lightweight_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (!SaveVerge(v => v.EnableLightweightMode = LightweightSwitch.IsOn)) return;
        ApplyLightweightMinutes();
    }

    private void ApplyLightweightMinutes()
    {
        if (!int.TryParse(LightweightMinutesBox.Text, out var minutes) || minutes < 0) minutes = 0;
        SaveVerge(v => v.AutoLightweightMinutes = minutes);
    }

    private static void SelectTag(ComboBox box, string tag)
    {
        foreach (var item in box.Items.OfType<ComboBoxItem>())
            if ((string)item.Tag == tag) { box.SelectedItem = item; return; }
    }

    private async void ApplyTunAdvanced_Click(object sender, RoutedEventArgs e)
    {
        var stack = TunStackBox.SelectedItem is ComboBoxItem si ? (string)si.Tag : "gvisor";
        var hijack = TunDnsHijackBox.Text.Trim();
        if (hijack.Length == 0) hijack = "any:53";
        AppServices.Config.PatchClashBase("tun.stack", stack);
        AppServices.Config.PatchClashBase("tun.dns-hijack", hijack);
        if (await AppServices.Core.ApplyConfigAsync())
            await ShowInfoAsync("已应用", "TUN 高级设置已重载。");
        else
            await ShowApplyFailureAsync();
    }

    private async void ApplyDns_Click(object sender, RoutedEventArgs e)
    {
        var mode = DnsModeBox.SelectedItem is ComboBoxItem mi ? (string)mi.Tag : "fake-ip";
        var fakeRange = DnsFakeIpRangeBox.Text.Trim();
        AppServices.Config.PatchClashBase("dns.enhanced-mode", mode);
        if (fakeRange.Length > 0)
            AppServices.Config.PatchClashBase("dns.fake-ip-range", fakeRange);
        if (await AppServices.Core.ApplyConfigAsync())
            await ShowInfoAsync("已应用", "DNS 设置已重载。");
        else
            await ShowApplyFailureAsync();
    }

    private async void ApplyController_Click(object sender, RoutedEventArgs e)
    {
        var address = ControllerBox.Text.Trim();
        var secret = ControllerSecretBox.Password;
        if (address.Length == 0)
        {
            await ShowInfoAsync("地址无效", "外部控制器地址不能为空。");
            return;
        }

        var (previousAddress, previousSecret) = AppServices.Config.GetControllerInfo();
        AppServices.Config.PatchClashBase("external-controller", address);
        AppServices.Config.PatchClashBase("secret", secret);
        // 先切换 API 客户端到新地址（内核启动等待依赖它），失败再回滚
        AppServices.Api.Configure(address, secret);
        AppServices.Streams.Configure(address, secret);
        try
        {
            await AppServices.Core.RestartAsync();
            var (newAddress, newSecret) = AppServices.Config.GetControllerInfo();
            AppServices.Api.Configure(newAddress, newSecret);
            AppServices.Streams.Configure(newAddress, newSecret);
            await ShowInfoAsync("已应用", "外部控制器已更新并重启内核。");
        }
        catch (Exception ex)
        {
            // 回滚
            AppServices.Config.PatchClashBase("external-controller", previousAddress);
            AppServices.Config.PatchClashBase("secret", previousSecret);
            try
            {
                await AppServices.Core.RestartAsync();
                AppServices.Api.Configure(previousAddress, previousSecret);
                AppServices.Streams.Configure(previousAddress, previousSecret);
            }
            catch { }
            await ShowInfoAsync("应用失败", ex.Message + "（已回滚）");
        }
    }

    private async void InstallService_Click(object sender, RoutedEventArgs e)
        => await InstallServiceAsync();

    private async void CopyEnvPowerShell_Click(object sender, RoutedEventArgs e)
        => await CopyEnvAsync("powershell");

    private async void CopyEnvCmd_Click(object sender, RoutedEventArgs e)
        => await CopyEnvAsync("cmd");

    /// <summary>复制 CMD/PowerShell 环境变量设置命令（对齐参考项目 copy_clash_env）。</summary>
    private async Task CopyEnvAsync(string format)
    {
        var port = AppServices.Config.MixedPort;
        var text = format switch
        {
            "powershell" => $"""
                $env:HTTP_PROXY = 'http://127.0.0.1:{port}'
                $env:HTTPS_PROXY = 'http://127.0.0.1:{port}'
                $env:ALL_PROXY = 'socks5://127.0.0.1:{port}'
                """,
            _ => $"""
                set HTTP_PROXY=http://127.0.0.1:{port}
                set HTTPS_PROXY=http://127.0.0.1:{port}
                set ALL_PROXY=socks5://127.0.0.1:{port}
                """,
        };

        var pack = new Windows.ApplicationModel.DataTransfer.DataPackage();
        pack.SetText(text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(pack);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "已复制",
            Content = $"{(format == "powershell" ? "PowerShell" : "CMD")} 环境变量已复制到剪贴板。",
            CloseButtonText = "确定",
        };
        await dialog.ShowAsync();
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

    // ---------- 备份与恢复 ----------

    private async void CreateBackup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var name = await AppServices.Backup.CreateAsync();
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "备份完成",
                Content = $"已创建备份 {name}",
                CloseButtonText = "确定",
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            await ShowInfoAsync("备份失败", ex.Message);
        }
    }

    private async void RestoreBackup_Click(object sender, RoutedEventArgs e)
    {
        List<(string Name, long Size, DateTime Created)> backups;
        try { backups = (await AppServices.Backup.ListAsync()).ToList(); }
        catch (Exception ex) { await ShowInfoAsync("读取备份失败", ex.Message); return; }

        if (backups.Count == 0)
        {
            await ShowInfoAsync("没有备份", "尚未创建任何备份。");
            return;
        }

        var listBox = new ListView { Height = 260, SelectionMode = ListViewSelectionMode.Single };
        foreach (var (name, size, created) in backups.OrderByDescending(b => b.Created))
        {
            listBox.Items.Add(new TextBlock
            {
                Text = $"{name}　({Format.Bytes(size)}, {created:yyyy-MM-dd HH:mm})",
                FontSize = 12,
            });
        }
        listBox.SelectedIndex = 0;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "恢复备份",
            Content = new StackPanel { Spacing = 8, Children =
            {
                new TextBlock { Text = "选择要恢复的备份（恢复后需要重启内核生效）：", TextWrapping = TextWrapping.Wrap },
                listBox,
            } },
            PrimaryButtonText = "恢复",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || listBox.SelectedIndex < 0) return;

        var selectedName = backups.OrderByDescending(b => b.Created).ToList()[listBox.SelectedIndex].Name;
        try
        {
            await Task.Run(() => AppServices.Backup.RestoreAsync(selectedName).GetAwaiter().GetResult());
            await AppServices.Core.RestartAsync();
            await ShowInfoAsync("恢复完成", $"已从 {selectedName} 恢复并重启内核。");
        }
        catch (Exception ex)
        {
            await ShowInfoAsync("恢复失败", ex.Message);
        }
    }

    // ---------- WebDAV 备份 ----------

    private Flux.Core.Backup.WebDavClient? CreateWebDavClient()
    {
        var verge = AppServices.Config.Verge;
        if (string.IsNullOrWhiteSpace(verge.WebDavUrl)) return null;
        return new Flux.Core.Backup.WebDavClient(
            verge.WebDavUrl,
            verge.WebDavUsername,
            Flux.Core.Utils.DataProtector.Unprotect(verge.WebDavPasswordEncrypted));
    }

    private async void WebDavConfig_Click(object sender, RoutedEventArgs e)
    {
        var verge = AppServices.Config.Verge;
        var urlBox = new TextBox { PlaceholderText = "https://dav.example.com/dav/", Text = verge.WebDavUrl, MinWidth = 360 };
        var userBox = new TextBox { PlaceholderText = "用户名", Text = verge.WebDavUsername, MinWidth = 360 };
        var passBox = new PasswordBox { PlaceholderText = "密码（保存后加密存储）", MinWidth = 360 };
        var dirBox = new TextBox { PlaceholderText = "flux-backups", Text = verge.WebDavDir, MinWidth = 360 };

        var form = new StackPanel { Spacing = 10, MinWidth = 380 };
        form.Children.Add(urlBox);
        form.Children.Add(userBox);
        form.Children.Add(passBox);
        form.Children.Add(dirBox);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "WebDAV 服务器设置",
            Content = form,
            PrimaryButtonText = "保存",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        if (!Uri.TryCreate(urlBox.Text.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            await ShowInfoAsync("地址无效", "WebDAV 地址必须是有效的 HTTP/HTTPS URL。");
            return;
        }

        verge.WebDavUrl = urlBox.Text.Trim();
        verge.WebDavUsername = userBox.Text.Trim();
        verge.WebDavDir = string.IsNullOrWhiteSpace(dirBox.Text) ? "flux-backups" : dirBox.Text.Trim();
        verge.WebDavPasswordEncrypted = Flux.Core.Utils.DataProtector.Protect(passBox.Password);
        AppServices.Config.SaveVerge();
        await ShowInfoAsync("已保存", "WebDAV 配置已保存（密码经 DPAPI 加密，明文不落盘）。");
    }

    private async void WebDavUpload_Click(object sender, RoutedEventArgs e)
    {
        var client = CreateWebDavClient();
        if (client is null)
        {
            await ShowInfoAsync("未配置", "请先设置 WebDAV 服务器。");
            return;
        }
        try
        {
            var name = await AppServices.Backup.CreateAsync("webdav upload");
            var bytes = await File.ReadAllBytesAsync(Path.Combine(Paths.DataBackupDir, name));
            var dir = AppServices.Config.Verge.WebDavDir;
            await client.UploadAsync(dir, name, bytes);
            await ShowInfoAsync("上传完成", name + " 已上传到 WebDAV。");
        }
        catch (Exception ex)
        {
            await ShowInfoAsync("上传失败", ex.Message);
        }
    }

    private async void WebDavRestore_Click(object sender, RoutedEventArgs e)
    {
        var client = CreateWebDavClient();
        if (client is null)
        {
            await ShowInfoAsync("未配置", "请先设置 WebDAV 服务器。");
            return;
        }
        try
        {
            var dir = AppServices.Config.Verge.WebDavDir;
            var remote = await client.ListAsync(dir);
            if (remote.Count == 0)
            {
                await ShowInfoAsync("无备份", "WebDAV 上没有备份文件。");
                return;
            }

            var listBox = new ListView { Height = 240, SelectionMode = ListViewSelectionMode.Single };
            foreach (var (name, size, modified) in remote.OrderByDescending(r => r.Modified))
                listBox.Items.Add(new TextBlock { Text = name + "　(" + Format.Bytes(size) + ")", FontSize = 12 });
            listBox.SelectedIndex = 0;

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "从 WebDAV 恢复",
                Content = listBox,
                PrimaryButtonText = "下载并恢复",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || listBox.SelectedIndex < 0) return;

            var selected = remote.OrderByDescending(r => r.Modified).ToList()[listBox.SelectedIndex].Name;
            var bytes = await client.DownloadAsync(dir, selected);
            var localPath = Path.Combine(Paths.DataBackupDir, Path.GetFileName(selected));
            await File.WriteAllBytesAsync(localPath, bytes);
            await Task.Run(() => AppServices.Backup.RestoreAsync(Path.GetFileName(selected)).GetAwaiter().GetResult());
            await AppServices.Core.RestartAsync();
            await ShowInfoAsync("恢复完成", "已从 " + selected + " 恢复并重启内核。");
        }
        catch (Exception ex)
        {
            await ShowInfoAsync("恢复失败", ex.Message);
        }
    }

    // ---------- 诊断与更新 ----------

    private async void ExportDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var service = new DiagnosticsService(Paths.AppDataDir, (level, msg) => LogService.App(msg, level));
            var path = await service.ExportAsync();
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "诊断包已导出",
                Content = path + "（内容仅保存在本地，可自行决定是否分享）",
                CloseButtonText = "确定",
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            await ShowInfoAsync("导出失败", ex.Message);
        }
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var client = new System.Net.Http.HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Flux-Update-Check");
            client.Timeout = TimeSpan.FromSeconds(15);
            var json = await client.GetStringAsync("https://api.github.com/repos/1595901624/flux/releases/latest");
            var current = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            var result = Flux.Core.Update.UpdateManifestParser.ParseLatest(
                json, current, System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString());

            if (result is null || !result.HasUpdate)
            {
                await ShowInfoAsync("检查更新", "当前已是最新版本（" + current + "）。");
                return;
            }

            var notes = result.ReleaseNotes ?? "";
            if (notes.Length > 600) notes = notes[..600] + "…";
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "发现新版本 " + result.LatestVersion,
                Content = new StackPanel { Spacing = 8, Children =
                {
                    new TextBlock { Text = notes, TextWrapping = TextWrapping.Wrap, MaxHeight = 240 },
                    new TextBlock { Text = "将打开 GitHub 发布页手动下载（MSIX/便携包）。", FontSize = 12, Opacity = 0.7, TextWrapping = TextWrapping.Wrap },
                } },
                PrimaryButtonText = "打开发布页",
                CloseButtonText = "关闭",
                DefaultButton = ContentDialogButton.Primary,
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary && result.ReleaseUrl is not null)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = result.ReleaseUrl,
                    UseShellExecute = true,
                });
            }
        }
        catch (Exception ex)
        {
            await ShowInfoAsync("检查更新失败", ex.Message);
        }
    }

    private void OpenBackupDir_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start("explorer.exe", Paths.DataBackupDir); } catch { }
    }

    private async Task ShowInfoAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "确定",
        };
        await dialog.ShowAsync();
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
