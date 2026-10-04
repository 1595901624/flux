using System.Diagnostics;
using Flux.Core.Proxy;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Flux.Services;

namespace Flux.Views;

/// <summary>
/// UWP Loopback 工具：列出 UWP 应用并允许/取消其本地回环联网权限。
/// 通过 CheckNetIsolation.exe 应用更改（需要 UAC 管理员授权）。
/// </summary>
public sealed class UwpLoopbackDialog : LocalizedContentDialog
{
    private readonly StackPanel _list = new() { Spacing = 4 };
    private readonly TextBlock _status = new() { Opacity = 0.75, FontSize = 12 };
    private readonly Dictionary<string, CheckBox> _rows = new();
    private HashSet<string> _initialExempted = new(StringComparer.OrdinalIgnoreCase);
    private bool _busy;
    private readonly Button _apply = new() { Content = L10n.T("Msg_LoopbackApply") };
    private readonly Button _reload = new() { Content = L10n.T("Msg_RefreshList") };

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
        _apply.Click += async (_, _) => await ApplyAsync();
        _reload.Click += async (_, _) => await LoadAsync();

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        toolbar.Children.Add(_apply);
        toolbar.Children.Add(_reload);

        header.Children.Add(hint);
        header.Children.Add(toolbar);
        header.Children.Add(_status);
        header.Children.Add(new ScrollViewer { MaxHeight = 380, Content = _list });

        Content = header;
        Loaded += async (_, _) => await LoadAsync();
    }

    /// <summary>解析 CheckNetIsolation -s 输出，返回已获回环豁免的包系列名集合。</summary>
    internal static HashSet<string> ParseExemptedFamilies(string output)
        => LoopbackExemptions.ParseFamilies(output);

    private static string CheckNetIsolationPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System), "CheckNetIsolation.exe");

    private static async Task<HashSet<string>> GetExemptedFamiliesAsync()
    {
        var psi = new ProcessStartInfo
        {
            FileName = CheckNetIsolationPath,
            Arguments = "LoopbackExempt -s",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(psi)!;
        using var output = new MemoryStream();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.StandardOutput.BaseStream.CopyToAsync(output);
        await process.WaitForExitAsync();
        await stderr;
        if (process.ExitCode != 0)
            throw new InvalidOperationException(L10n.F("Msg_LoopbackExitFailed", process.ExitCode));
        return LoopbackExemptions.ParseOutput(output.ToArray());
    }

    private async Task LoadAsync()
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            _status.Text = L10n.T("Msg_Loading");
            _list.Children.Clear();
            _rows.Clear();

            HashSet<string> exempted;
            Windows.ApplicationModel.Package[] packages;
            try
            {
                var sid = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value
                    ?? throw new InvalidOperationException(L10n.T("Msg_LoopbackMissingSid"));
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
                ? L10n.T("Msg_LoopbackEmpty")
                : L10n.F("Msg_LoopbackCount", shown);
        }
        finally { SetBusy(false); }
    }

    private async Task ApplyAsync()
    {
        if (_busy || _rows.Count == 0) return;
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

        var selected = _rows.ToDictionary(row => row.Key, row => row.Value.IsChecked == true);
        var scriptPath = Path.Combine(Path.GetTempPath(), $"flux-loopback-{Guid.NewGuid():N}.cmd");
        SetBusy(true);
        _status.Text = L10n.T("Msg_LoopbackApplying");
        try
        {
            var script = LoopbackExemptions.BuildScript(CheckNetIsolationPath, toAdd, toRemove);
            await File.WriteAllTextAsync(scriptPath, script, System.Text.Encoding.ASCII);
            var psi = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                Arguments = $"/d /c \"\"{scriptPath}\"\"",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            using var process = Process.Start(psi);
            if (process is null) throw new InvalidOperationException(L10n.T("Msg_LoopbackLaunchFailed"));
            await process.WaitForExitAsync();
            // 即使批处理中途失败，也显示已实际写入的状态，避免下一次增删使用旧基线。
            _initialExempted = await GetExemptedFamiliesAsync();
            foreach (var (family, box) in _rows) box.IsChecked = _initialExempted.Contains(family);
            if (process.ExitCode != 0)
                throw new InvalidOperationException(L10n.F("Msg_LoopbackExitFailed", process.ExitCode));
            if (!LoopbackExemptions.MatchesSelection(selected, _initialExempted))
                throw new InvalidOperationException(L10n.T("Msg_LoopbackVerifyFailed"));
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
        finally
        {
            try { File.Delete(scriptPath); } catch { }
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _apply.IsEnabled = !busy && _rows.Count > 0;
        _reload.IsEnabled = !busy;
        _list.IsHitTestVisible = !busy;
        foreach (var box in _rows.Values) box.IsEnabled = !busy;
    }

}
