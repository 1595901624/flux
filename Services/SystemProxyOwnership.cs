namespace Flux.Services;

internal static class SystemProxyOwnership
{
    public static bool IsOwned(bool enabled, string currentServer, string fluxServer) =>
        enabled && !string.IsNullOrWhiteSpace(fluxServer) &&
        string.Equals(currentServer, fluxServer, StringComparison.OrdinalIgnoreCase);

    /// <summary>PAC 模式所有权：自动配置 URL 指向 Flux 生成的 PAC 文件。</summary>
    public static bool IsOwnedPac(bool pacEnabled, string currentUrl, string fluxPacUrl) =>
        pacEnabled && !string.IsNullOrWhiteSpace(fluxPacUrl) &&
        string.Equals(currentUrl, fluxPacUrl, StringComparison.OrdinalIgnoreCase);
}
