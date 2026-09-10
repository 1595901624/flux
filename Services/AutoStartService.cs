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
                    StartupTaskState.DisabledByUser => "开机自启动已被用户禁用，请在任务管理器的“启动应用”中重新启用 Flux Proxy",
                    StartupTaskState.DisabledByPolicy => "开机自启动已被系统策略禁用",
                    _ => "用户未允许开机自启动",
                });
            return;
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException("无法打开 Run 注册表键");
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
