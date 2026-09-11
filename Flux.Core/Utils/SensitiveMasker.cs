using System.Text.RegularExpressions;

namespace Flux.Core.Utils;

/// <summary>
/// 敏感信息遮蔽：日志与诊断包必须遮蔽订阅 URL 参数、Basic Auth、控制器 Secret 与 WebDAV 凭据。
/// </summary>
public static partial class SensitiveMasker
{
    [GeneratedRegex(@"(?i)(token|secret|key|password|passwd|pwd|auth)=([^&\s\x22\x27]+)")]
    private static partial Regex QueryParamRegex();

    [GeneratedRegex(@"(?i)[a-z0-9+/\-_.]+:[^\s@/:\x22\x27]{3,}")]
    private static partial Regex BasicAuthRegex();

    [GeneratedRegex(@"(?i)\b(bearer|basic)\s+([a-z0-9._~+/=-]{8,})")]
    private static partial Regex AuthHeaderRegex();

    [GeneratedRegex(@"(?i)""(secret|password|passwd|token)""\s*:\s*""([^""]+)""")]
    private static partial Regex JsonFieldRegex();

    /// <summary>遮蔽文本中的敏感信息；每个敏感值替换为固定长度的 <c>***</c> 标记。</summary>
    public static string Mask(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";

        var result = text;
        result = QueryParamRegex().Replace(result, m => $"{m.Groups[1].Value}={MaskValue(m.Groups[2].Value)}");
        result = JsonFieldRegex().Replace(result, m => $"\"{m.Groups[1].Value}\":\"{MaskValue(m.Groups[2].Value)}\"");
        result = AuthHeaderRegex().Replace(result, m => $"{m.Groups[1].Value} {MaskValue(m.Groups[2].Value)}");
        // URL userinfo（Basic Auth / token）：scheme://user:pass@host
        result = MaskUrlUserInfos(result);
        return result;
    }

    private static string MaskUrlUserInfos(string text)
    {
        // 只处理 URL 形态的 userinfo，避免误伤普通文本
        return UrlWithUserInfoRegex().Replace(text, m =>
        {
            var scheme = m.Groups[1].Value;
            var host = m.Groups[4].Value;
            return $"{scheme}://{MaskValue("userinfo")}@{host}";
        });
    }

    [GeneratedRegex(@"(?i)\b(https?|ss|ssr|vmess|vless|trojan|hysteria|hysteria2|hy2|tuic|socks5?)://([^\s@/:""\x27]{1,64}):([^\s@/:""\x27]{1,128})@")]
    private static partial Regex UrlWithUserInfoRegex();

    /// <summary>遮蔽单个值：保留首尾各 1 个字符（长度允许时），其余替换为 ***。</summary>
    public static string MaskValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return "***";
        if (value.Length <= 2) return "***";
        return $"{value[0]}***{value[^1]}";
    }
}
