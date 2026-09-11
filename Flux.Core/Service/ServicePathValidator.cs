using Flux.Core.Contracts;

namespace Flux.Core.Service;

/// <summary>
/// 服务端路径校验：运行时配置必须位于 Flux 数据目录，内核必须是内置安装目录
/// 或数据目录 core-cache 下的版本化缓存文件。拒绝任意路径以防止特权滥用。
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
        return null;
    }

    /// <summary>校验内核路径：仅允许安装目录内置内核或数据目录 core-cache 下的缓存内核。</summary>
    public static OperationError? ValidateCorePath(string? corePath, string installDir, string dataDir)
    {
        if (string.IsNullOrWhiteSpace(corePath))
            return OperationError.Of("invalid_path", "缺少内核路径", "ServicePath");
        if (!Path.IsPathRooted(corePath))
            return OperationError.Of("invalid_path", "内核路径必须是绝对路径", "ServicePath");

        var full = Path.GetFullPath(corePath);
        if (!string.Equals(Path.GetFileName(full), "mihomo.exe", StringComparison.OrdinalIgnoreCase))
            return OperationError.Of("invalid_core", "内核文件名必须是 mihomo.exe", "ServicePath");

        var builtinRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installDir));
        var builtinCore = Path.TrimEndingDirectorySeparator(Path.Combine(builtinRoot, "core"));
        var cacheRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(dataDir, "core-cache")));

        var inBuiltin = full.StartsWith(builtinCore + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        var inCache = full.StartsWith(cacheRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        if (!inBuiltin && !inCache)
            return OperationError.Of("invalid_core", "内核必须位于安装目录 core 或数据目录 core-cache 内", "ServicePath");
        return null;
    }

    /// <summary>解析数据目录：便携标记存在时使用 exe 目录，否则使用 %APPDATA%\flux。</summary>
    public static string ResolveDataDir(string serviceExeDir)
    {
        var portableMarker = Path.Combine(serviceExeDir, ".config", "PORTABLE");
        if (File.Exists(portableMarker))
            return Path.Combine(serviceExeDir, ".config", "flux");
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "flux");
    }
}
