using Microsoft.Win32;
using Windows.ApplicationModel;

namespace Flux.Services;

/// <summary>开机自启动：MSIX 使用 StartupTask，未打包版本回退到 HKCU Run 键。</summary>
public class AutoStartService
{
    private const string StartupTaskId = "FluxStartup";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Flux";

    public static async Task<bool> IsEnabledAsync()
    {
        if (PackageIdentity.IsPackaged)
        {
            var task = await StartupTask.GetAsync(StartupTaskId);
            return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    /// <summary>更新自启动状态；silent 表示开机静默启动（不显示窗口）。</summary>
    public static async Task SetEnabledAsync(bool enable, bool silent = false)
    {
        if (PackageIdentity.IsPackaged)
        {
            var task = await StartupTask.GetAsync(StartupTaskId);
            if (!enable)
            {
                task.Disable();
                return;
            }

            var state = await task.RequestEnableAsync();
            if (state is not (StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy))
                throw new InvalidOperationException(state switch
                {
                    StartupTaskState.DisabledByUser => L10n.T("AutoStart_DisabledByUser"),
                    StartupTaskState.DisabledByPolicy => L10n.T("AutoStart_DisabledByPolicy"),
                    _ => L10n.T("AutoStart_NotAllowed"),
                });
            return;
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException(L10n.T("AutoStart_RunKeyFailed"));
        if (enable)
        {
            var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Flux.exe");
            key.SetValue(ValueName, $"\"{exe}\"" + (silent ? " --silent" : ""));
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    public static bool WasLaunchedSilently(string[] args) =>
        args.Any(a => a.Equals("--silent", StringComparison.OrdinalIgnoreCase));
}
