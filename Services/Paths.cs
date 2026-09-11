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
    /// <summary>配置迁移与增强文件备份目录。</summary>
    public static string DataBackupDir { get; private set; } = "";

    public static string ExeDir { get; private set; } = "";
    /// <summary>随应用分发的内核路径（安装目录内，MSIX 下只读）。</summary>
    public static string CoreSourcePath => Path.Combine(CoreDir, "mihomo.exe");
    /// <summary>
    /// 实际启动的内核路径。直接运行安装包中的文件，以保留 MSIX 的完整性保护；
    /// 配置和运行时文件均通过 <c>-d</c> 参数写入数据目录，不需要修改安装目录。
    /// </summary>
    public static string CoreExePath => CoreSourcePath;
    /// <summary>旧版复制到数据目录的内核路径，仅用于升级时清理残留进程。</summary>
    public static string LegacyCoreExePath => Path.Combine(AppDataDir, "FluxCore.exe");
    public static string ClashConfigFile => Path.Combine(AppDataDir, "config.yaml");
    public static string VergeConfigFile => Path.Combine(AppDataDir, "verge.yaml");
    public static string ProfilesConfigFile => Path.Combine(AppDataDir, "profiles.yaml");
    public static string RuntimeConfigFile => Path.Combine(AppDataDir, "runtime.yaml");
    public static string CheckConfigFile => Path.Combine(AppDataDir, "check.yaml");
    public static string ProxyStateFile => Path.Combine(AppDataDir, "system-proxy-state.json");
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
        DataBackupDir = Path.Combine(AppDataDir, "backup");

        Directory.CreateDirectory(AppDataDir);
        Directory.CreateDirectory(LogsDir);
        Directory.CreateDirectory(ProfilesDir);
    }

    /// <summary>
    /// 确认内置内核存在且可执行。不要将其复制到数据目录：这会使 MSIX 内
    /// 已签名的文件脱离安装包，可能被另一台电脑上的应用控制策略拦截。
    /// </summary>
    public static void EnsureCoreExecutable()
    {
        if (!File.Exists(CoreSourcePath))
            throw new FileNotFoundException("未找到内置 mihomo 内核（core\\mihomo.exe）", CoreSourcePath);

    }
}
