using System.Text;

namespace Flux.Core.Proxy;

/// <summary>
/// PAC（Proxy Auto-Config）脚本生成。默认按混合端口与绕过列表生成；
/// 绕过条目支持通配（*）转换为 shExpMatch，主机名/IP 判断均覆盖。
/// </summary>
public static class PacScriptGenerator
{
    /// <summary>生成 PAC 脚本内容。</summary>
    public static string Generate(int port, IReadOnlyList<string> bypassItems, string proxyHost = "127.0.0.1")
    {
        var sb = new StringBuilder();
        sb.AppendLine("// 由 Flux 自动生成的 PAC 脚本");
        sb.AppendLine("function FindProxyForURL(url, host) {");
        sb.AppendLine("  host = host.toLowerCase();");

        // localhost 与纯 IP 环回/链路本地始终直连
        sb.AppendLine("  if (host === 'localhost' || host === '127.0.0.1' || host === '::1' || host === '[::1]')");
        sb.AppendLine("    return 'DIRECT';");

        foreach (var rawItem in bypassItems)
        {
            var item = rawItem.Trim();
            if (item.Length == 0 || item == "<local>") continue;
            var lower = item.ToLowerInvariant();

            if (lower.Contains('*'))
            {
                // 通配模式：shExpMatch
                sb.AppendLine($"  if (shExpMatch(host, '{EscapeJs(lower)}')) return 'DIRECT';");
            }
            else if (IsCidr(lower))
            {
                // CIDR：isInNet 判断
                var parts = lower.Split('/');
                sb.AppendLine($"  if (isInNet(host, '{EscapeJs(parts[0])}', '{EscapeJs(parts[1])}')) return 'DIRECT';");
            }
            else if (lower.StartsWith('.'))
            {
                // .example.com → example.com 及全部子域
                var domain = EscapeJs(lower[1..]);
                sb.AppendLine($"  if (host === '{domain}' || shExpMatch(host, '*.{domain}')) return 'DIRECT';");
            }
            else if (IsPlainDomain(lower) || IsIpLiteral(lower))
            {
                var domain = EscapeJs(lower);
                sb.AppendLine($"  if (host === '{domain}') return 'DIRECT';");
                // 域名也匹配子域（与 WinINET bypass 行为一致）
                if (IsPlainDomain(lower))
                    sb.AppendLine($"  if (shExpMatch(host, '*.{domain}')) return 'DIRECT';");
            }
            else
            {
                sb.AppendLine($"  if (shExpMatch(host, '{EscapeJs(lower)}')) return 'DIRECT';");
            }
        }

        sb.AppendLine("  return 'PROXY " + proxyHost + ":" + port + "';");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static bool IsCidr(string value)
    {
        var parts = value.Split('/');
        if (parts.Length != 2 || !int.TryParse(parts[1], out var prefix)) return false;
        if (parts[0].Split('.').Length == 4) return prefix is >= 0 and <= 32;
        if (parts[0].Contains(':')) return prefix is >= 0 and <= 128;
        return false;
    }

    private static bool IsIpLiteral(string value) =>
        value.Split('.').Length == 4 || value.Contains(':');

    /// <summary>纯域名：不含通配、斜杠、冒号，且至少包含一个点或为单字主机名。</summary>
    private static bool IsPlainDomain(string value) =>
        value.All(c => char.IsLetterOrDigit(c) || c is '-' or '.') && value.Length > 0;

    private static string EscapeJs(string value) => value
        .Replace("\\", "\\\\")
        .Replace("'", "\\'");
}
