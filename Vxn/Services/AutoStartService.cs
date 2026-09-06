using Microsoft.Win32;

namespace Vxn.Services;

/// <summary>开机自启动（HKCU Run 键，免管理员）。</summary>
public class AutoStartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Vxn";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    /// <summary>更新自启动状态；silent 表示开机静默启动（不显示窗口）。</summary>
    public static void SetEnabled(bool enable, bool silent = false)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException("无法打开 Run 注册表键");
        if (enable)
        {
            var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Vxn.exe");
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
