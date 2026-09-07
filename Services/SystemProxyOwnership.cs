namespace Flux.Services;

internal static class SystemProxyOwnership
{
    public static bool IsOwned(bool enabled, string currentServer, string fluxServer) =>
        enabled && !string.IsNullOrWhiteSpace(fluxServer) &&
        string.Equals(currentServer, fluxServer, StringComparison.OrdinalIgnoreCase);
}
