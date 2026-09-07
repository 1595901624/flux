namespace Flux.Services;

internal static class DeepLinkParser
{
    public static bool IsSupportedUri(string value) =>
        value.StartsWith("clash://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("clash-verge://", StringComparison.OrdinalIgnoreCase);

    public static bool TryParseInstallUri(string value, out string importUrl, out string? name)
    {
        importUrl = "";
        name = null;
        if (!IsSupportedUri(value) || !Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;

        foreach (var item in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = item.Split('=', 2);
            if (pair.Length != 2) continue;
            var key = Uri.UnescapeDataString(pair[0]);
            var decoded = Uri.UnescapeDataString(pair[1].Replace('+', ' '));
            if (key.Equals("url", StringComparison.OrdinalIgnoreCase)) importUrl = decoded;
            else if (key.Equals("name", StringComparison.OrdinalIgnoreCase)) name = decoded;
        }

        if (importUrl.Contains("%3A", StringComparison.OrdinalIgnoreCase))
            importUrl = Uri.UnescapeDataString(importUrl);
        return Uri.TryCreate(importUrl, UriKind.Absolute, out var target) &&
               target.Scheme is "http" or "https";
    }
}
