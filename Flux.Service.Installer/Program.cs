namespace Flux.Service.Installer;

using System.Security.Cryptography;

/// <summary>
/// Flux 服务安装器入口。仅接受 install | repair | reinstall | uninstall 四个预定义操作，
/// 服务二进制固定为安装器同目录下的 Flux.Service.exe，不接受任意路径或命令参数。
/// </summary>
internal static class Program
{
    private static readonly string InstallDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Flux", "Service");

    private static int Main(string[] args)
    {
        var operation = args.Length > 0 ? args[0].ToLowerInvariant() : "";
        Console.WriteLine($"Flux 服务安装器 v{typeof(Program).Assembly.GetName().Version?.ToString(3)}");

        try
        {
            switch (operation)
            {
                case "install":
                    Install(updateExisting: false);
                    break;
                case "repair":
                case "reinstall":
                    Install(updateExisting: true);
                    break;
                case "uninstall":
                    Uninstall();
                    break;
                default:
                    Console.Error.WriteLine("用法: Flux.Service.Installer <install|repair|reinstall|uninstall>");
                    return 2;
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("错误: " + ex.Message);
            return 1;
        }
    }

    private static string ServiceExePath()
    {
        var path = Path.Combine(InstallDirectory, "Flux.Service.exe");
        if (!File.Exists(path))
            throw new InvalidOperationException($"未找到服务程序 {path}，请重新安装应用");
        return path;
    }

    private static void CopyServicePayload()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "service-payload");
        if (!Directory.Exists(source) || !File.Exists(Path.Combine(source, "Flux.Service.exe")) ||
            !File.Exists(Path.Combine(source, "core", "mihomo.exe")))
            throw new InvalidOperationException($"服务安装包不完整: {source}");

        Directory.CreateDirectory(InstallDirectory);
        foreach (var sourceFile in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, sourceFile);
            var targetFile = Path.GetFullPath(Path.Combine(InstallDirectory, relative));
            var installRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(InstallDirectory));
            if (!targetFile.StartsWith(installRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("服务安装包包含非法路径");
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(sourceFile, targetFile, overwrite: true);
        }

        var corePath = Path.Combine(InstallDirectory, "core", "mihomo.exe");
        using var coreStream = File.OpenRead(corePath);
        File.WriteAllText(corePath + ".sha256", Convert.ToHexString(SHA256.HashData(coreStream)));
    }

    private static void Install(bool updateExisting)
    {
        var exists = ScmClient.ServiceExists();

        if (exists)
        {
            Console.WriteLine("停止现有服务");
            try { ScmClient.Stop(); } catch (InvalidOperationException) { /* 服务未运行 */ }
        }

        CopyServicePayload();
        var exe = ServiceExePath();

        if (exists && updateExisting)
        {
            Console.WriteLine("修复服务：停止并更新二进制路径");
            ScmClient.UpdateBinaryPath(exe);
        }
        else if (exists && !updateExisting)
        {
            Console.WriteLine("服务已存在，更新二进制路径");
            ScmClient.UpdateBinaryPath(exe);
        }
        else
        {
            Console.WriteLine("创建服务 FluxService");
            ScmClient.Create(exe);
        }

        Console.WriteLine("启动服务");
        ScmClient.Start();
        Console.WriteLine("完成：Flux 特权服务已运行");
    }

    private static void Uninstall()
    {
        if (!ScmClient.ServiceExists())
        {
            Console.WriteLine("服务未安装，无需卸载");
            return;
        }
        Console.WriteLine("停止服务");
        try { ScmClient.Stop(); } catch (InvalidOperationException ex) { Console.WriteLine("停止服务: " + ex.Message); }
        Console.WriteLine("删除服务");
        ScmClient.Delete();
        try
        {
            if (Directory.Exists(InstallDirectory))
                Directory.Delete(InstallDirectory, recursive: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine("清理服务文件失败，可稍后手动删除: " + ex.Message);
        }
        Console.WriteLine("完成：Flux 特权服务已卸载");
    }
}
