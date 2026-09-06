using System.Reflection;

namespace Flux.Services;

/// <summary>
/// 数据目录与文件路径管理。支持便携模式：exe 目录下存在 .config\PORTABLE 时数据存于 exe 目录。
/// </summary>
public static class Paths
{
    public static string AppDataDir { get; private set; } = "";
    public static string CoreDir { get; private set; } = "";
    public static string LogsDir { get; private set; } = "";
    public static string ProfilesDir { get; private set; } = "";

    public static string ExeDir { get; private set; } = "";
    public static string CoreExePath => Path.Combine(CoreDir, "mihomo.exe");
    public static string ClashConfigFile => Path.Combine(AppDataDir, "config.yaml");
    public static string VergeConfigFile => Path.Combine(AppDataDir, "verge.yaml");
    public static string ProfilesConfigFile => Path.Combine(AppDataDir, "profiles.yaml");
    public static string RuntimeConfigFile => Path.Combine(AppDataDir, "runtime.yaml");
    public static string CheckConfigFile => Path.Combine(AppDataDir, "check.yaml");
    public static string AppLogFile => Path.Combine(LogsDir, "app.log");
    public static string CoreLogFile => Path.Combine(LogsDir, "core.log");

    public static void Initialize()
    {
        ExeDir = AppContext.BaseDirectory;
        CoreDir = Path.Combine(ExeDir, "core");

        var portableMarker = Path.Combine(ExeDir, ".config", "PORTABLE");
        if (File.Exists(portableMarker))
        {
            AppDataDir = Path.Combine(ExeDir, ".config", "flux");
        }
        else
        {
            AppDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "flux");
        }

        LogsDir = Path.Combine(AppDataDir, "logs");
        ProfilesDir = Path.Combine(AppDataDir, "profiles");

        Directory.CreateDirectory(AppDataDir);
        Directory.CreateDirectory(LogsDir);
        Directory.CreateDirectory(ProfilesDir);
    }
}
