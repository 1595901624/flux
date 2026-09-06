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
    /// <summary>随应用分发的内核原始路径（安装目录内，MSIX 下只读）。</summary>
    public static string CoreSourcePath => Path.Combine(CoreDir, "mihomo.exe");
    /// <summary>实际启动的内核路径。使用专属名称 FluxCore.exe，避免与用户自装的 mihomo.exe 冲突。</summary>
    public static string CoreExePath => Path.Combine(AppDataDir, "FluxCore.exe");
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

    /// <summary>
    /// 把内置内核复制为专属名称 FluxCore.exe（在可写的数据目录），供启动使用。
    /// 若目标已是当前内核的同版本副本则跳过；目标被占用时给出明确报错。
    /// </summary>
    public static void EnsureCoreExecutable()
    {
        if (!File.Exists(CoreSourcePath))
            throw new FileNotFoundException("未找到内置 mihomo 内核（core\\mihomo.exe）", CoreSourcePath);

        var source = new FileInfo(CoreSourcePath);
        if (File.Exists(CoreExePath))
        {
            var target = new FileInfo(CoreExePath);
            if (target.Length == source.Length &&
                Math.Abs((target.LastWriteTimeUtc - source.LastWriteTimeUtc).TotalSeconds) < 2)
                return;
        }

        try
        {
            if (File.Exists(CoreExePath)) File.Delete(CoreExePath);
            File.Copy(CoreSourcePath, CoreExePath);
        }
        catch (IOException)
        {
            throw new IOException(
                $"无法更新内核副本 {CoreExePath}，可能正在被占用，请先关闭 Flux 或结束 FluxCore 进程后重试");
        }
        File.SetLastWriteTimeUtc(CoreExePath, source.LastWriteTimeUtc);
    }
}
