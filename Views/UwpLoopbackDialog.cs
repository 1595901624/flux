using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Flux.Services;

namespace Flux.Views;

/// <summary>
/// UWP Loopback 工具：列出 UWP 应用并允许/取消其本地回环联网权限。
/// 通过 CheckNetIsolation.exe 应用更改（需要 UAC 管理员授权）。
/// </summary>
public sealed class UwpLoopbackDialog : ContentDialog
{
    private readonly StackPanel _list = new() { Spacing = 4 };
    private readonly TextBlock _status = new() { Opacity = 0.75, FontSize = 12 };
    private readonly Dictionary<string, CheckBox> _rows = new();
    private HashSet<string> _initialExempted = new(StringComparer.OrdinalIgnoreCase);

    public UwpLoopbackDialog(XamlRoot root)
    {
        XamlRoot = root;
        Title = L10n.T("Msg_LoopbackTitle");
        CloseButtonText = L10n.T("Common_Close");

        var header = new StackPanel { Spacing = 8, MinWidth = 480 };
        var hint = new TextBlock
        {
            Text = L10n.T("Msg_LoopbackHint"),
            FontSize = 12,
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
        };
        var apply = new Button { Content = L10n.T("Msg_LoopbackApply") };
        apply.Click += async (_, _) => await ApplyAsync();
        var reload = new Button { Content = L10n.T("Msg_RefreshList") };
        reload.Click += async (_, _) => await LoadAsync();

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        toolbar.Children.Add(apply);
        toolbar.Children.Add(reload);

        header.Children.Add(hint);
        header.Children.Add(toolbar);
        header.Children.Add(_status);
        header.Children.Add(new ScrollViewer { MaxHeight = 380, Content = _list });

        Content = header;
        Loaded += async (_, _) => await LoadAsync();
    }

    /// <summary>解析 CheckNetIsolation -s 输出，返回已获回环豁免的包系列名集合。</summary>
    internal static HashSet<string> ParseExemptedFamilies(string output)
    {
        // 家族名形如 Microsoft.Win32WebViewHost_cw5n1h2txyewy（发布者哈希为 13 位小写字母数字）
        var families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var match in Regex.Matches(output, @"[A-Za-z0-9.][A-Za-z0-9.\-]*_[a-z0-9]{13}\b"))
        {
            var value = match.ToString();
            if (!string.IsNullOrEmpty(value)) families.Add(value);
        }
        return families;
    }

    private static async Task<HashSet<string>> GetExemptedFamiliesAsync()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "CheckNetIsolation.exe",
            Arguments = "LoopbackExempt -s",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            StandardOutputEncoding = System.Text.Encoding.Unicode,
        };
        using var process = Process.Start(psi)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return ParseExemptedFamilies(output);
    }

    private async Task LoadAsync()
    {
        _status.Text = L10n.T("Msg_Loading");
        _list.Children.Clear();
        _rows.Clear();

        HashSet<string> exempted;
        Windows.ApplicationModel.Package[] packages;
        try
        {
            var sid = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value
                ?? throw new InvalidOperationException("no user sid");
            var manager = new Windows.Management.Deployment.PackageManager();
            packages = manager.FindPackagesForUser(sid).ToArray();
            exempted = await GetExemptedFamiliesAsync();
        }
        catch (Exception ex)
        {
            _status.Text = L10n.F("Msg_LoopbackLoadFailed", ex.Message);
            return;
        }
        _initialExempted = exempted;

        var shown = 0;
        foreach (var package in packages)
        {
            try
            {
                if (package.IsFramework || package.IsResourcePackage) continue;
                var family = package.Id.FamilyName;
                var name = string.IsNullOrEmpty(package.DisplayName) ? package.Id.Name : package.DisplayName;
                if (string.IsNullOrEmpty(name)) continue;

                var box = new CheckBox
                {
                    Content = new TextBlock
                    {
                        Text = name,
                        FontSize = 12,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    },
                    IsChecked = exempted.Contains(family),
                };
                _rows[family] = box;
                _list.Children.Add(box);
                shown++;
            }
            catch { }
        }

        _status.Text = shown == 0
            ? L10n.T("Msg_NoBackups")
            : L10n.F("Msg_ProviderCount", shown);
    }

    private async Task ApplyAsync()
    {
        // 相对进入对话框时的初始豁免集合计算增删
        var toAdd = new List<string>();
        var toRemove = new List<string>();
        foreach (var (family, box) in _rows)
        {
            var want = box.IsChecked == true;
            var initial = _initialExempted.Contains(family);
            if (want && !initial) toAdd.Add(family);
            else if (!want && initial) toRemove.Add(family);
        }

        if (toAdd.Count == 0 && toRemove.Count == 0)
        {
            _status.Text = L10n.T("Msg_LoopbackApplied");
            return;
        }

        _status.Text = L10n.T("Msg_LoopbackApplying");
        try
        {
            var sbArgs = new System.Text.StringBuilder("LoopbackExempt");
            if (toAdd.Count > 0)
            {
                sbArgs.Append(" -a");
                foreach (var f in toAdd) sbArgs.Append(" -n=").Append(f);
            }
            if (toRemove.Count > 0)
            {
                sbArgs.Append(" -d");
                foreach (var f in toRemove) sbArgs.Append(" -n=").Append(f);
            }

            var psi = new ProcessStartInfo
            {
                FileName = "CheckNetIsolation.exe",
                Arguments = sbArgs.ToString(),
                UseShellExecute = true,
                Verb = "runas", // UAC 提权
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi);
            if (process is null) throw new InvalidOperationException("CheckNetIsolation launch failed");
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"CheckNetIsolation exit {process.ExitCode}");

            _initialExempted = await GetExemptedFamiliesAsync();
            _status.Text = L10n.T("Msg_LoopbackApplied");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            _status.Text = L10n.T("Msg_UacCancelled");
        }
        catch (Exception ex)
        {
            _status.Text = L10n.F("Msg_LoopbackFailed", ex.Message);
        }
    }
}
