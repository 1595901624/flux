using System.Security.Cryptography;
using System.Text;
using YamlDotNet.RepresentationModel;

namespace Flux.Core.Proxies;

/// <summary>单个解析结果。</summary>
public sealed record ParsedProxy(YamlMappingNode Node, string? Name, string? Error = null)
{
    public bool Success => Error is null && Node.Children.Count > 0;
}

/// <summary>
/// 代理分享链接批量解析：支持 ss / ssr / vmess / vless / trojan / hysteria / hysteria2(hy2) /
/// tuic / anytls / socks(socks5) / http(s) 代理节点。输出 mihomo proxies 条目（YAML 映射）。
/// 支持普通文本与 Base64 订阅格式（逐行/整体自动识别）。
/// </summary>
public static partial class ProxyUriParser
{
    /// <summary>解析分享链接文本（可多行、可整体 Base64）。返回全部成功与失败条目。</summary>
    public static IReadOnlyList<ParsedProxy> Parse(string text)
    {
        var results = new List<ParsedProxy>();
        if (string.IsNullOrWhiteSpace(text)) return results;

        var lines = ExtractProxyLines(text);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            results.Add(ParseOne(trimmed));
        }
        return results;
    }

    /// <summary>从纯文本或 Base64 订阅内容中提取分享链接行。</summary>
    public static IReadOnlyList<string> ExtractProxyLines(string text)
    {
        var lines = new List<string>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim().TrimEnd('\r');
            if (IsProxyUri(line)) lines.Add(line);
        }
        if (lines.Count > 0) return lines;

        // 整体或逐行尝试 Base64 解码（订阅格式）
        foreach (var candidate in TryDecodeBase64(text))
        {
            foreach (var raw in candidate.Split('\n'))
            {
                var line = raw.Trim().TrimEnd('\r');
                if (IsProxyUri(line)) lines.Add(line);
            }
            if (lines.Count > 0) return lines;
        }
        return lines;
    }

    private static bool IsProxyUri(string line)
    {
        foreach (var scheme in Schemes)
        {
            if (line.StartsWith(scheme + "://", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static readonly string[] Schemes =
    [
        "ss", "ssr", "vmess", "vless", "trojan", "hysteria", "hysteria2", "hy2", "tuic", "anytls", "socks", "socks5", "http", "https",
    ];

    private static IEnumerable<string> TryDecodeBase64(string text)
    {
        var cleaned = text.Trim().Replace("\r", "").Replace("\n", "");
        if (cleaned.Length % 4 == 0 && LooksLikeBase64(cleaned))
        {
            var decoded = DecodeBase64Any(cleaned);
            if (decoded is not null) yield return decoded;
        }
        foreach (var line in text.Split('\n'))
        {
            var t = line.Trim().Replace("\r", "");
            if (t.Length % 4 == 0 && LooksLikeBase64(t))
            {
                var decoded = DecodeBase64Any(t);
                if (decoded is not null) yield return decoded;
            }
        }
    }

    private static bool LooksLikeBase64(string s) =>
        s.All(c => char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '=' || c == '-' || c == '_');

    private static string? DecodeBase64Any(string s)
    {
        foreach (var candidate in new[] { s, s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=') })
        {
            try
            {
                var bytes = Convert.FromBase64String(candidate);
                return Encoding.UTF8.GetString(bytes);
            }
            catch { }
        }
        return null;
    }

    public static ParsedProxy ParseOne(string uri)
    {
        try
        {
            var schemeEnd = uri.IndexOf("://", StringComparison.Ordinal);
            if (schemeEnd < 0) return Fail(uri, "缺少 ://");
            var scheme = uri[..schemeEnd].ToLowerInvariant();
            var body = uri[(schemeEnd + 3)..];

            return scheme switch
            {
                "ss" => ParseSs(body),
                "ssr" => ParseSsr(body),
                "vmess" => ParseVmess(body),
                "vless" => ParseVless(body),
                "trojan" => ParseTrojan(body),
                "hysteria" => ParseHysteria1(body),
                "hysteria2" or "hy2" => ParseHysteria2(body, scheme),
                "tuic" => ParseTuic(body),
                "anytls" => ParseAnytls(body),
                "socks" or "socks5" => ParseSocks(body, "socks5"),
                "http" or "https" => ParseHttp(body, scheme),
                _ => Fail(uri, $"不支持的协议: {scheme}"),
            };
        }
        catch (Exception ex)
        {
            return Fail(uri, ex.Message);
        }
    }

    private static ParsedProxy Ok(YamlMappingNode node) => new(node, Scalar(node, "name"));
    private static ParsedProxy Fail(string uri, string message) =>
        new(new YamlMappingNode(), null, $"{message}（{Truncate(uri)}）");

    private static string Truncate(string s) => s.Length <= 48 ? s : s[..45] + "...";

    private static string? Scalar(YamlMappingNode node, string key) =>
        node.Children.TryGetValue(new YamlScalarNode(key), out var v) && v is YamlScalarNode s ? s.Value : null;

    private static void Set(YamlMappingNode node, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var keyNode = new YamlScalarNode(key);
        node.Children.Remove(keyNode);
        node.Children[keyNode] = new YamlScalarNode(value);
    }

    private static void SetInt(YamlMappingNode node, string key, int? value)
    {
        if (value is { } v)
        {
            var keyNode = new YamlScalarNode(key);
            node.Children.Remove(keyNode);
            node.Children[keyNode] = new YamlScalarNode(v.ToString());
        }
    }

    // ---------- ss ----------

    private static ParsedProxy ParseSs(string body)
    {
        // SIP002: base64(method:password)@host:port/?plugin=...#name 或 旧版整体 base64
        string fragment = "", query = "";

        var hashIndex = body.IndexOf('#');
        if (hashIndex >= 0)
        {
            fragment = Uri.UnescapeDataString(body[(hashIndex + 1)..]);
            body = body[..hashIndex];
        }
        var queryIndex = body.IndexOf('?');
        if (queryIndex >= 0)
        {
            query = body[(queryIndex + 1)..];
            body = body[..queryIndex];
        }

        YamlMappingNode node = new();
        string method, password, host, portStr;

        var atIndex = FindLastSplit(body, '@');
        if (atIndex >= 0)
        {
            // SIP002
            var userInfo = body[..atIndex];
            var hostPart = body[(atIndex + 1)..];
            if (LooksLikeBase64(userInfo) && !userInfo.Contains(':'))
            {
                var decoded = DecodeBase64Any(userInfo);
                if (decoded is null) return Fail("ss://" + body, "userinfo Base64 解码失败");
                userInfo = decoded;
            }
            var colon = userInfo.IndexOf(':');
            if (colon < 0) return Fail("ss://" + body, "userinfo 缺少 method:password");
            method = userInfo[..colon];
            password = userInfo[(colon + 1)..];
            (host, portStr) = SplitHostPort(hostPart);
        }
        else
        {
            // 旧版整体 base64: base64(method:password@host:port)
            var decoded = DecodeBase64Any(body);
            if (decoded is null) return Fail("ss://" + body, "ss 链接 Base64 解码失败");
            var at = decoded.LastIndexOf('@');
            if (at < 0) return Fail("ss://" + body, "ss 链接缺少 @host:port");
            var userInfo = decoded[..at];
            var colon = userInfo.IndexOf(':');
            if (colon < 0) return Fail("ss://" + body, "ss 链接缺少 method:password");
            method = userInfo[..colon];
            password = userInfo[(colon + 1)..];
            (host, portStr) = SplitHostPort(decoded[(at + 1)..]);
        }

        if (!int.TryParse(portStr, out var port) || port is <= 0 or > 65535)
            return Fail("ss://" + body, "端口非法");
        if (string.IsNullOrEmpty(host)) return Fail("ss://" + body, "缺少服务器地址");

        Set(node, "name", fragment is "" ? $"ss-{host}" : fragment);
        Set(node, "type", "ss");
        Set(node, "server", host);
        SetInt(node, "port", port);
        Set(node, "cipher", method);
        Set(node, "password", password);

        if (query.Length > 0)
        {
            var q = ParseQuery(query);
            if (q.TryGetValue("plugin", out var plugin) && plugin.Contains("obfs-local"))
            {
                var pluginParts = plugin.Split(';');
                node.Children.Add(new YamlScalarNode("plugin"), new YamlScalarNode("obfs"));
                foreach (var p in pluginParts.Skip(1))
                {
                    var kv = p.Split('=', 2);
                    if (kv.Length != 2) continue;
                    if (kv[0] == "obfs") node.Children.Add(new YamlScalarNode("plugin-opts"), "obfs", kv[1]);
                    if (kv[0] == "obfs-host") node.Children.Add(new YamlScalarNode("plugin-opts"), "host", kv[1]);
                }
            }
        }
        return Ok(node);
    }

    // ---------- ssr ----------

    private static ParsedProxy ParseSsr(string body)
    {
        var decoded = DecodeBase64Any(body.Replace("-", "+").Replace("_", "/").PadRight(body.Length + (4 - body.Length % 4) % 4, '='));
        if (decoded is null) return Fail("ssr://" + body, "ssr 链接 Base64 解码失败");
        // host:port:protocol:method:obfs:base64pass/?params
        var main = decoded.Split("/?")[0];
        var parts = main.Split(':');
        if (parts.Length < 6) return Fail("ssr://" + body, "ssr 链接格式非法");

        var password = DecodeBase64Any(parts[5]) ?? "";
        var name = "ssr-" + parts[0];
        var query = decoded.Contains("/?") ? decoded[(decoded.IndexOf("/?", StringComparison.Ordinal) + 2)..] : "";
        var q = ParseQuery(query);
        if (q.TryGetValue("remarks", out var remarks) && remarks.Length > 0)
            name = DecodeBase64Any(remarks.Replace('-', '+').Replace('_', '/')) ?? name;

        var node = new YamlMappingNode();
        Set(node, "name", name);
        Set(node, "type", "ssr");
        Set(node, "server", parts[0]);
        if (int.TryParse(parts[1], out var port)) SetInt(node, "port", port);
        Set(node, "protocol", parts[2]);
        Set(node, "cipher", parts[3]);
        Set(node, "obfs", parts[4]);
        Set(node, "password", password);
        if (q.TryGetValue("protoparam", out var protoParam))
            Set(node, "protocol-param", DecodeBase64Any(protoParam.Replace('-', '+').Replace('_', '/')));
        if (q.TryGetValue("obfsparam", out var obfsParam))
            Set(node, "obfs-param", DecodeBase64Any(obfsParam.Replace('-', '+').Replace('_', '/')));
        return Ok(node);
    }

    // ---------- vmess ----------

    private static ParsedProxy ParseVmess(string body)
    {
        var json = DecodeBase64Any(body.Replace('-', '+').Replace('_', '/'));
        if (json is null) return Fail("vmess://" + body, "vmess 链接 Base64 解码失败");
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        string S(string name) => root.TryGetProperty(name, out var e) && e.ValueKind == System.Text.Json.JsonValueKind.String
            ? e.GetString() ?? ""
            : e.ValueKind == System.Text.Json.JsonValueKind.Number ? e.GetRawText() : "";

        var node = new YamlMappingNode();
        var name = S("ps");
        var add = S("add");
        if (string.IsNullOrEmpty(add)) return Fail("vmess://" + body, "vmess 链接缺少 add");
        int port = 0;
        if (root.TryGetProperty("port", out var p))
        {
            if (p.ValueKind == System.Text.Json.JsonValueKind.Number && p.TryGetInt32(out var n))
                port = n;
            else if (!int.TryParse(p.ToString(), out port))
                port = 0;
        }
        var id = S("id");
        if (string.IsNullOrEmpty(id)) return Fail("vmess://" + body, "vmess 链接缺少 id");

        Set(node, "name", name is "" ? $"vmess-{add}" : name);
        Set(node, "type", "vmess");
        Set(node, "server", add);
        SetInt(node, "port", port);
        Set(node, "uuid", id);
        Set(node, "alterId", S("aid") is "" or "0" ? "0" : S("aid"));
        Set(node, "cipher", S("scy") is "" ? "auto" : S("scy"));
        Set(node, "udp", "true");

        var net = S("net");
        var tls = S("tls");
        if (net is "ws" or "h2" or "grpc")
        {
            node.Children.Add(new YamlScalarNode("network"), new YamlScalarNode(net));
            var opts = new YamlMappingNode();
            if (S("path") is { Length: > 0 } path) opts.Children.Add(new YamlScalarNode("path"), new YamlScalarNode(path));
            if (S("host") is { Length: > 0 } host) opts.Children.Add(new YamlScalarNode("host"), new YamlScalarNode(host));
            if (S("servicename") is { Length: > 0 } sn) opts.Children.Add(new YamlScalarNode("serviceName"), new YamlScalarNode(sn));
            if (opts.Children.Count > 0)
                node.Children.Add(new YamlScalarNode(net + "-opts"), opts);
        }
        if (tls.Equals("tls", StringComparison.OrdinalIgnoreCase))
        {
            node.Children.Add(new YamlScalarNode("tls"), new YamlScalarNode("true"));
            if (S("sni") is { Length: > 0 } sni) Set(node, "servername", sni);
        }
        if (S("type").Equals("http", StringComparison.OrdinalIgnoreCase))
            Set(node, "skip-cert-verify", "true");
        return Ok(node);
    }

    // ---------- vless / trojan / hysteria2 / tuic / anytls（@host:port?query#name 族） ----------

    private static ParsedProxy ParseVless(string body)
    {
        var (node, host, port, query, fragment) = ParseUriFamily(body, "vless", "vless");
        if (node is null) return Fail("vless://" + body, "vless 链接格式非法");

        var q = ParseQuery(query);
        Set(node!, "name", fragment is "" ? $"vless-{host}" : fragment);
        Set(node!, "type", "vless");
        SetInt(node!, "port", port);
        Set(node!, "uuid", UserInfoOf(body));
        Set(node!, "udp", "true");

        var flow = q.TryGetValue("flow", out var f) ? f : "";
        if (flow.Length > 0) Set(node!, "flow", flow);
        ApplyCommonTransport(node!, q, defaultNetwork: "tcp");
        return Ok(node!);
    }

    private static ParsedProxy ParseTrojan(string body)
    {
        var (node, host, port, query, fragment) = ParseUriFamily(body, "trojan", "trojan");
        if (node is null) return Fail("trojan://" + body, "trojan 链接格式非法");

        var q = ParseQuery(query);
        Set(node!, "name", fragment is "" ? $"trojan-{host}" : fragment);
        Set(node!, "type", "trojan");
        SetInt(node!, "port", port);
        Set(node!, "password", Uri.UnescapeDataString(UserInfoOf(body)));
        Set(node!, "udp", "true");
        if (q.TryGetValue("sni", out var sni)) Set(node!, "sni", sni);
        if (q.TryGetValue("allowInsecure", out var insecure) || q.TryGetValue("insecure", out insecure))
            Set(node!, "skip-cert-verify", insecure);
        ApplyCommonTransport(node!, q, defaultNetwork: "tcp");
        return Ok(node!);
    }

    private static ParsedProxy ParseHysteria1(string body)
    {
        var (node, host, port, query, fragment) = ParseUriFamily(body, "hysteria", "hysteria");
        if (node is null) return Fail("hysteria://" + body, "hysteria 链接格式非法");

        var q = ParseQuery(query);
        Set(node!, "name", fragment is "" ? $"hysteria-{host}" : fragment);
        Set(node!, "type", "hysteria");
        SetInt(node!, "port", port);
        if (q.TryGetValue("auth", out var auth)) Set(node!, "auth-str", auth);
        if (q.TryGetValue("peer", out var peer)) Set(node!, "sni", peer);
        if (q.TryGetValue("insecure", out var insecure)) Set(node!, "skip-cert-verify", insecure);
        if (q.TryGetValue("up", out var up)) Set(node!, "up", up);
        if (q.TryGetValue("down", out var down)) Set(node!, "down", down);
        if (q.TryGetValue("alpn", out var alpn)) Set(node!, "alpn", alpn);
        if (q.TryGetValue("obfs", out var obfs)) Set(node!, "obfs", obfs);
        Set(node!, "protocol", "udp");
        return Ok(node!);
    }

    private static ParsedProxy ParseHysteria2(string body, string scheme)
    {
        var (node, host, port, query, fragment) = ParseUriFamily(body, scheme, "hysteria2");
        if (node is null) return Fail(scheme + "://" + body, $"{scheme} 链接格式非法");

        var q = ParseQuery(query);
        Set(node!, "name", fragment is "" ? $"hysteria2-{host}" : fragment);
        Set(node!, "type", "hysteria2");
        SetInt(node!, "port", port);
        Set(node!, "password", Uri.UnescapeDataString(UserInfoOf(body)));
        if (q.TryGetValue("sni", out var sni)) Set(node!, "sni", sni);
        if (q.TryGetValue("insecure", out var insecure) || q.TryGetValue("allowInsecure", out insecure))
            Set(node!, "skip-cert-verify", insecure);
        if (q.TryGetValue("obfs", out var obfs)) Set(node!, "obfs", obfs);
        if (q.TryGetValue("obfs-password", out var obfsPass)) Set(node!, "obfs-password", obfsPass);
        if (q.TryGetValue("pinSHA256", out var pin)) Set(node!, "fingerprint", pin);
        return Ok(node!);
    }

    private static ParsedProxy ParseTuic(string body)
    {
        var (node, host, port, query, fragment) = ParseUriFamily(body, "tuic", "tuic");
        if (node is null) return Fail("tuic://" + body, "tuic 链接格式非法");

        var q = ParseQuery(query);
        Set(node!, "name", fragment is "" ? $"tuic-{host}" : fragment);
        Set(node!, "type", "tuic");
        SetInt(node!, "port", port);
        var userInfo = Uri.UnescapeDataString(UserInfoOf(body));
        var colon = userInfo.IndexOf(':');
        if (colon >= 0)
        {
            Set(node!, "uuid", userInfo[..colon]);
            Set(node!, "password", userInfo[(colon + 1)..]);
        }
        else
        {
            Set(node!, "uuid", userInfo);
        }
        if (q.TryGetValue("congestion_control", out var cc)) Set(node!, "congestion-controller", cc);
        if (q.TryGetValue("alpn", out var alpn)) Set(node!, "alpn", alpn);
        if (q.TryGetValue("sni", out var sni)) Set(node!, "sni", sni);
        if (q.TryGetValue("allow_insecure", out var insecure)) Set(node!, "skip-cert-verify", insecure);
        if (q.TryGetValue("udp_relay_mode", out var udpMode)) Set(node!, "udp-relay-mode", udpMode);
        return Ok(node!);
    }

    private static ParsedProxy ParseAnytls(string body)
    {
        var (node, host, port, query, fragment) = ParseUriFamily(body, "anytls", "anytls");
        if (node is null) return Fail("anytls://" + body, "anytls 链接格式非法");

        var q = ParseQuery(query);
        Set(node!, "name", fragment is "" ? $"anytls-{host}" : fragment);
        Set(node!, "type", "anytls");
        SetInt(node!, "port", port);
        Set(node!, "password", Uri.UnescapeDataString(UserInfoOf(body)));
        if (q.TryGetValue("sni", out var sni)) Set(node!, "sni", sni);
        if (q.TryGetValue("insecure", out var insecure)) Set(node!, "skip-cert-verify", insecure);
        return Ok(node!);
    }

    // ---------- socks / http ----------

    private static ParsedProxy ParseSocks(string body, string type)
    {
        var (node, host, port, query, fragment) = ParseUriFamily(body, type, type);
        if (node is null) return Fail(type + "://" + body, $"{type} 链接格式非法");

        Set(node!, "name", fragment is "" ? $"{type}-{host}" : fragment);
        SetInt(node!, "port", port);
        var userInfo = Uri.UnescapeDataString(UserInfoOf(body));
        if (userInfo.Length > 0)
        {
            var colon = userInfo.IndexOf(':');
            Set(node!, "username", colon >= 0 ? userInfo[..colon] : userInfo);
            if (colon >= 0) Set(node!, "password", userInfo[(colon + 1)..]);
        }
        return Ok(node!);
    }

    private static ParsedProxy ParseHttp(string body, string scheme)
    {
        var type = scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "http" : "http";
        var (node, host, port, query, fragment) = ParseUriFamily(body, scheme, type);
        if (node is null) return Fail(scheme + "://" + body, $"{scheme} 链接格式非法");

        Set(node!, "name", fragment is "" ? $"http-{host}" : fragment);
        SetInt(node!, "port", port);
        if (scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            Set(node!, "tls", "true");
        var userInfo = Uri.UnescapeDataString(UserInfoOf(body));
        if (userInfo.Length > 0)
        {
            var colon = userInfo.IndexOf(':');
            Set(node!, "username", colon >= 0 ? userInfo[..colon] : userInfo);
            if (colon >= 0) Set(node!, "password", userInfo[(colon + 1)..]);
        }
        return Ok(node!);
    }

    // ---------- 公共工具 ----------

    /// <summary>解析 user@host:port?query#name 家族；返回节点骨架（name/server 已填）。</summary>
    private static (YamlMappingNode? node, string host, int port, string query, string fragment) ParseUriFamily(
        string body, string scheme, string type)
    {
        var main = body;
        var fragment = "";
        var hash = main.IndexOf('#');
        if (hash >= 0)
        {
            fragment = Uri.UnescapeDataString(main[(hash + 1)..]);
            main = main[..hash];
        }
        var query = "";
        var qIndex = main.IndexOf('?');
        if (qIndex >= 0)
        {
            query = main[(qIndex + 1)..];
            main = main[..qIndex];
        }

        var atIndex = FindLastSplit(main, '@');
        var hostPart = atIndex >= 0 ? main[(atIndex + 1)..] : main;
        (var host, var portStr) = SplitHostPort(hostPart);
        if (host.Length == 0 || !int.TryParse(portStr, out var port) || port <= 0)
            return (null, "", 0, "", "");

        var node = new YamlMappingNode();
        node.Children.Add(new YamlScalarNode("type"), new YamlScalarNode(type));
        node.Children.Add(new YamlScalarNode("server"), new YamlScalarNode(host));
        return (node, host, port, query, fragment);
    }

    private static void ApplyCommonTransport(YamlMappingNode node, Dictionary<string, string> q, string defaultNetwork)
    {
        var network = q.TryGetValue("type", out var t) && t.Length > 0 ? t : defaultNetwork;
        if (network != "ws" && network != "grpc" && network != "h2")
        {
            if (network == "tcp") return;
            node.Children.Add(new YamlScalarNode("network"), new YamlScalarNode(network));
            return;
        }

        node.Children.Add(new YamlScalarNode("network"), new YamlScalarNode(network));
        var opts = new YamlMappingNode();
        if (q.TryGetValue("path", out var path)) opts.Children.Add(new YamlScalarNode("path"), new YamlScalarNode(path));
        if (q.TryGetValue("host", out var host)) opts.Children.Add(new YamlScalarNode("host"), new YamlScalarNode(host));
        if (q.TryGetValue("serviceName", out var sn)) opts.Children.Add(new YamlScalarNode("serviceName"), new YamlScalarNode(sn));
        if (opts.Children.Count > 0)
        {
            var key = network switch
            {
                "ws" => "ws-opts",
                "grpc" => "grpc-opts",
                "h2" => "h2-opts",
                _ => "ws-opts",
            };
            node.Children.Add(new YamlScalarNode(key), opts);
        }

        var security = q.TryGetValue("security", out var s) ? s : "";
        if (security.Equals("tls", StringComparison.OrdinalIgnoreCase) ||
            q.ContainsKey("sni") || q.ContainsKey("fp"))
        {
            node.Children.Add(new YamlScalarNode("tls"), new YamlScalarNode("true"));
            if (q.TryGetValue("sni", out var sni)) node.Children.Add(new YamlScalarNode("servername"), new YamlScalarNode(sni));
            if (q.TryGetValue("fp", out var fp)) node.Children.Add(new YamlScalarNode("client-fingerprint"), new YamlScalarNode(fp));
        }
    }

    /// <summary>取 @ 之前的部分（未做 URL 解码的原始值；vmess/uuid 族按需解码）。</summary>
    private static string UserInfoOf(string body)
    {
        var main = body;
        var hash = main.IndexOf('#');
        if (hash >= 0) main = main[..hash];
        var qIndex = main.IndexOf('?');
        if (qIndex >= 0) main = main[..qIndex];
        var atIndex = FindLastSplit(main, '@');
        return atIndex >= 0 ? main[..atIndex] : "";
    }

    /// <summary>找最后一个 '@'（IPv6 地址含 ':'，'@' 只出现在 userinfo 分隔）。</summary>
    private static int FindLastSplit(string text, char c)
    {
        // 分享链接中 '@' 不会出现在 IPv6 字面量里（v1 格式已对 [] 使用），取最后一个
        return text.LastIndexOf(c);
    }

    private static (string host, string port) SplitHostPort(string hostPart)
    {
        hostPart = hostPart.TrimEnd('/');
        if (hostPart.StartsWith('['))
        {
            var close = hostPart.IndexOf(']');
            if (close < 0) return ("", "");
            var host = hostPart[1..close];
            var rest = hostPart[(close + 1)..];
            var port = rest.StartsWith(':') ? rest[1..] : "";
            return (host, port);
        }
        var colon = hostPart.LastIndexOf(':');
        if (colon < 0) return (hostPart, "");
        return (hostPart[..colon], hostPart[(colon + 1)..]);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(kv[0]).Trim();
            var value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
            result[key] = value;
        }
        return result;
    }
}

/// <summary>YamlMappingNode 子键写入扩展（两级键如 plugin-opts.obfs）。</summary>
internal static class YamlNodeChildExtensions
{
    public static void Add(this IDictionary<YamlNode, YamlNode> children, YamlScalarNode key, string subKey, string value)
    {
        if (!children.TryGetValue(key, out var existing) || existing is not YamlMappingNode map)
        {
            map = new YamlMappingNode();
            children[key] = map;
        }
        map.Children[new YamlScalarNode(subKey)] = new YamlScalarNode(value);
    }
}
