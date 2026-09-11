using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Flux.Core.Utils;

namespace Flux.Services;

/// <summary>
/// 诊断包导出：系统信息、应用与内核版本、配置结构、运行模式与最近日志。
/// 所有内容经 SensitiveMasker 遮蔽（订阅 URL 参数、Secret、密码）；
/// 仅导出到本地文件，不自动上传。
/// </summary>
public sealed class DiagnosticsService
{
    private readonly string _outputDir;
    private readonly Action<string, string>? _log;

    public DiagnosticsService(string outputDir, Action<string, string>? log = null)
    {
        _outputDir = outputDir;
        _log = log;
    }

    public async Task<string> ExportAsync()
    {
        Directory.CreateDirectory(_outputDir);
        var fileName = $"flux-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
        var path = Path.Combine(_outputDir, fileName);

        var sb = new StringBuilder();
        sb.AppendLine("=== Flux 诊断包 ===");
        sb.AppendLine($"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // 系统与应用信息
        sb.AppendLine("--- 系统信息 ---");
        sb.AppendLine($"OS 版本: {Environment.OSVersion.VersionString}");
        sb.AppendLine($"架构: {RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine($"处理器数: {Environment.ProcessorCount}");
        sb.AppendLine($".NET: {Environment.Version}");
        sb.AppendLine($"Flux 版本: {typeof(DiagnosticsService).Assembly.GetName().Version}");
        sb.AppendLine($"运行模式: {(PackageIdentity.IsPackaged ? "MSIX" : "便携")}");
        sb.AppendLine($"管理员: {TrayService.IsElevated()}");
        sb.AppendLine();

        // 内核与运行模式
        sb.AppendLine("--- 内核 ---");
        try
        {
            var coreVersion = await AppServices.Api.GetVersionAsync();
            sb.AppendLine($"mihomo 版本: {coreVersion ?? "未运行"}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"mihomo 版本: 获取失败 ({SensitiveMasker.Mask(ex.Message)})");
        }
        sb.AppendLine($"内核模式: {AppServices.Core.Mode}");
        sb.AppendLine($"服务状态: {AppServices.Privilege.GetServiceState()}");
        sb.AppendLine();

        // 配置结构（遮蔽 secret / url）
        sb.AppendLine("--- 配置结构（敏感字段已遮蔽） ---");
        AppendMaskedConfig(sb, Paths.ClashConfigFile);
        sb.AppendLine($"verge.yaml 键: {string.Join(", ", ReadTopLevelKeys(Paths.VergeConfigFile))}");
        sb.AppendLine($"profiles.yaml 键: {string.Join(", ", ReadTopLevelKeys(Paths.ProfilesConfigFile))}");
        sb.AppendLine($"当前订阅: {SensitiveMasker.Mask(AppServices.Config.Profiles.GetCurrent()?.Name)}");
        sb.AppendLine();

        // 运行设置摘要
        var verge = AppServices.Config.Verge;
        sb.AppendLine("--- 运行设置摘要 ---");
        sb.AppendLine($"系统代理: {verge.EnableSystemProxy} (PAC: {verge.EnablePacMode})");
        sb.AppendLine($"TUN: {verge.EnableTunMode}");
        sb.AppendLine($"模式: {AppServices.Config.Mode}");
        sb.AppendLine($"内核: {AppServices.Core.Mode}");
        sb.AppendLine();

        // 最近日志（遮蔽）
        sb.AppendLine("--- 最近应用日志（200 条，敏感信息已遮蔽） ---");
        try
        {
            if (File.Exists(Paths.AppLogFile))
            {
                var lines = File.ReadAllLines(Paths.AppLogFile, Encoding.UTF8);
                foreach (var line in lines.TakeLast(200))
                    sb.AppendLine(SensitiveMasker.Mask(line));
            }
            else
            {
                sb.AppendLine("(无应用日志文件)");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"(日志读取失败: {SensitiveMasker.Mask(ex.Message)})");
        }

        await File.WriteAllTextAsync(path, SensitiveMasker.Mask(sb.ToString()), Encoding.UTF8);
        _log?.Invoke("info", $"诊断包已导出: {path}");
        return path;
    }

    /// <summary>输出配置文件的键结构（两层级），标量值全部遮蔽。</summary>
    private static void AppendMaskedConfig(StringBuilder sb, string path)
    {
        try
        {
            sb.AppendLine($"{Path.GetFileName(path)}:");
            var node = YamlHelper.ParseMapping(File.ReadAllText(path));
            if (node is null)
            {
                sb.AppendLine("  (无法解析)");
                return;
            }
            foreach (var (key, value) in node.Children)
            {
                if (value is YamlDotNet.RepresentationModel.YamlMappingNode map)
                {
                    var keys = string.Join(", ", map.Children.Keys.Select(k => k.ToString()));
                    sb.AppendLine($"  {key}: {{{keys}}}");
                }
                else
                {
                    // 标量一律遮蔽，避免 secret/端口外泄端口虽不敏感但保持一致性
                    sb.AppendLine($"  {key}: ***");
                }
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  (读取失败: {SensitiveMasker.Mask(ex.Message)})");
        }
    }

    private static IReadOnlyList<string> ReadTopLevelKeys(string path)
    {
        try
        {
            if (!File.Exists(path)) return [];
            var node = YamlHelper.ParseMapping(File.ReadAllText(path));
            return node?.Children.Keys.Select(k => k.ToString() ?? "").ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }
}
