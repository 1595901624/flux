using Flux.Core.Contracts;
using System.Security.Cryptography;

namespace Flux.Core.Service;

/// <summary>
/// 服务端路径校验：配置必须位于已认证用户的数据目录，内核只能是服务目录中的固定文件。
/// </summary>
public static class ServicePathValidator
{
    /// <summary>校验运行时配置路径。成功返回 null，失败返回带原因的错误。</summary>
    public static OperationError? ValidateConfigPath(string? configPath, string dataDir)
    {
        if (string.IsNullOrWhiteSpace(configPath))
            return OperationError.Of("invalid_path", "缺少运行时配置路径", "ServicePath");
        if (!Path.IsPathRooted(configPath))
            return OperationError.Of("invalid_path", "运行时配置路径必须是绝对路径", "ServicePath");

        var full = Path.GetFullPath(configPath);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataDir));
        var candidate = Path.TrimEndingDirectorySeparator(full);
        if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase))
            return OperationError.Of("invalid_path", $"运行时配置必须位于数据目录内: {dataDir}", "ServicePath");
        if (!string.Equals(Path.GetExtension(full), ".yaml", StringComparison.OrdinalIgnoreCase))
            return OperationError.Of("invalid_path", "运行时配置必须是 .yaml 文件", "ServicePath");
        if (!File.Exists(full))
            return OperationError.Of("invalid_path", "运行时配置文件不存在", "ServicePath");
        if (ContainsReparsePoint(root) || ContainsReparsePoint(full))
            return OperationError.Of("invalid_path", "运行时配置路径不能包含符号链接或重解析点", "ServicePath");
        return null;
    }

    public static OperationError? ValidateConfigDirectory(string? configDir, string dataDir)
    {
        if (string.IsNullOrWhiteSpace(configDir) || !Path.IsPathRooted(configDir))
            return OperationError.Of("invalid_path", "配置工作目录必须是绝对路径", "ServicePath");
        var candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configDir));
        var expected = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataDir));
        return string.Equals(candidate, expected, StringComparison.OrdinalIgnoreCase)
            ? null
            : OperationError.Of("invalid_path", "配置工作目录与当前用户数据目录不匹配", "ServicePath");
    }

    /// <summary>校验内核路径：只允许服务受保护目录中的固定 mihomo.exe。</summary>
    public static OperationError? ValidateCorePath(string? corePath, string installDir, string dataDir)
    {
        if (string.IsNullOrWhiteSpace(corePath))
            return OperationError.Of("invalid_path", "缺少内核路径", "ServicePath");
        if (!Path.IsPathRooted(corePath))
            return OperationError.Of("invalid_path", "内核路径必须是绝对路径", "ServicePath");

        var full = Path.GetFullPath(corePath);
        if (!string.Equals(Path.GetFileName(full), "mihomo.exe", StringComparison.OrdinalIgnoreCase))
            return OperationError.Of("invalid_core", "内核文件名必须是 mihomo.exe", "ServicePath");

        if (!string.Equals(full, GetTrustedCorePath(installDir), StringComparison.OrdinalIgnoreCase))
            return OperationError.Of("invalid_core", "服务只允许启动受保护目录中的内置内核", "ServicePath");
        if (!File.Exists(full))
            return OperationError.Of("invalid_core", "服务内置内核不存在", "ServicePath");
        if (ContainsReparsePoint(Path.GetFullPath(installDir)) || ContainsReparsePoint(full))
            return OperationError.Of("invalid_core", "服务内核路径不能包含符号链接或重解析点", "ServicePath");
        var hashFile = full + ".sha256";
        if (!File.Exists(hashFile))
            return OperationError.Of("invalid_core", "服务内核缺少可信哈希清单", "ServicePath");
        try
        {
            var expectedHash = File.ReadAllText(hashFile).Trim();
            using var stream = File.OpenRead(full);
            var actualHash = Convert.ToHexString(SHA256.HashData(stream));
            if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                return OperationError.Of("invalid_core", "服务内核哈希校验失败", "ServicePath");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return OperationError.Of("invalid_core", "服务内核哈希校验失败: " + ex.Message, "ServicePath");
        }
        return null;
    }

    public static string GetTrustedCorePath(string installDir) =>
        Path.GetFullPath(Path.Combine(installDir, "core", "mihomo.exe"));

    private static bool ContainsReparsePoint(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);
        if (string.IsNullOrEmpty(root)) return true;
        var current = root;
        foreach (var segment in full[root.Length..].Split(
                     Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current)) continue;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
        }
        return false;
    }

}
