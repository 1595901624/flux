using System.Text.Json.Nodes;
using Flux.Core.Contracts;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Flux.Core.Config;

/// <summary>
/// 确定性运行时配置流水线（对齐参考项目 enhance/mod.rs）：
/// 订阅 → 内置兼容增强 → 全局 Merge → 订阅 Merge → 全局 Script → 订阅 Script
/// → 规则/节点/代理组前后插入 → DNS 覆写 → TUN → 临时链式代理 → 控制面字段强制恢复 → 字段排序。
/// 任一步骤失败返回步骤名、文件名与消息；调用方应保留最后一个有效配置。
/// </summary>
public sealed class RuntimeConfigBuilder : IRuntimeConfigBuilder
{
    /// <summary>应用权威的控制面键：订阅与脚本不得覆盖，合成后强制恢复。</summary>
    public static readonly string[] ControlPlaneKeys =
    [
        "external-controller", "external-controller-pipe", "external-controller-cors",
        "secret", "mixed-port", "socks-port", "port", "redir-port", "tproxy-port",
        "mode", "allow-lan", "log-level", "ipv6", "unified-delay",
    ];

    /// <summary>代理组建组类型；成员合法性校验时这些组名总是放行。</summary>
    private static readonly string[] BuiltinProxies = ["DIRECT", "REJECT", "REJECT-DROP", "PASS", "COMPATIBLE", "GLOBAL"];

    private static readonly string[] HandleFields =
    [
        "mode", "mixed-port", "socks-port", "port", "redir-port", "tproxy-port",
        "allow-lan", "log-level", "ipv6", "external-controller", "external-controller-pipe",
        "external-controller-cors", "secret", "unified-delay",
    ];

    private static readonly string[] DefaultFieldsLast = ["proxies", "proxy-providers", "proxy-groups", "rule-providers", "rules"];

    private readonly ScriptSandbox _scripts = new();

    public OperationResult<RuntimeConfigOutput> Build(RuntimeConfigInput input)
    {
        var logs = new List<ChainLogEntry>();
        var existsKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. 订阅为底
        var config = input.Profile is null ? new YamlMappingNode() : (YamlMappingNode)YamlOps.Clone(input.Profile);
        YamlOps.LowercaseKeys(config);

        // 2. 控制面快照（来自应用基础配置，步骤 11 强制恢复）
        var snapshot = CaptureControlPlane(input.ClashBase);

        // 3. 增强文件按序应用：全局 Merge → 订阅 Merge → 全局 Script → 订阅 Script → Seq
        var mergeItems = input.ChainItems.Where(i => i.Item.Type == ChainType.Merge).ToList();
        foreach (var item in mergeItems)
        {
            var step = ApplyMerge(config, item, logs, existsKeys);
            if (step is not null) return FailStep(item, step);
        }

        var scriptItems = input.ChainItems.Where(i => i.Item.Type == ChainType.Script).ToList();
        foreach (var item in scriptItems)
        {
            var step = ApplyScript(config, item, logs, existsKeys);
            if (step is not null) return FailStep(item, step);
        }

        foreach (var type in new[] { ChainType.Rules, ChainType.Proxies, ChainType.Groups })
        {
            foreach (var item in input.ChainItems.Where(i => i.Item.Type == type))
            {
                var step = ApplySeq(config, item, logs, existsKeys);
                if (step is not null) return FailStep(item, step);
            }
        }

        // 4. 内置兼容增强（可关闭）
        if (input.EnableBuiltinEnhance)
            ApplyBuiltinEnhance(config, logs);

        // 5. DNS 覆写
        if (input.EnableDnsSettings && input.DnsOverride is not null)
            ApplyDnsOverride(config, input.DnsOverride, logs);

        // 6. TUN
        ApplyTun(config, input.EnableTun, logs);

        // 7. 临时链式代理（dialer-proxy）
        if (input.ChainProxy is { Count: > 0 })
        {
            var error = ApplyChainProxy(config, input.ChainProxy, logs);
            if (error is not null)
                return OperationResult<RuntimeConfigOutput>.Fail(error);
        }

        // 8. 控制面强制恢复 + allow-lan bind-address 放宽
        EnforceControlPlane(config, snapshot, logs);
        EnsureLanBindAddress(config);

        // 9. 代理组成员合法性清理
        CleanupProxyGroups(config, logs);

        // 10. 字段排序（控制面字段在前，默认数据字段在后）
        SortFields(config);

        return OperationResult<RuntimeConfigOutput>.Ok(new RuntimeConfigOutput(config, logs, existsKeys));
    }

    // ---------- 各步骤 ----------

    private static OperationError? ApplyMerge(
        YamlMappingNode config, ChainItemWithContent item, List<ChainLogEntry> logs, HashSet<string> existsKeys)
    {
        var overlay = YamlOps.ParseMapping(item.Content);
        if (overlay is null)
            return OperationError.Of("yaml_invalid", "Merge 文件不是合法的 YAML Mapping", "Merge", item.Item.File);

        YamlOps.DeepMerge(config, overlay);
        foreach (var key in overlay.Children.Keys)
            existsKeys.Add(key.ToString()?.ToLowerInvariant() ?? "");
        logs.Add(new ChainLogEntry("info", item.Item.Uid, item.Item.Name, $"Merge 已应用（{overlay.Children.Count} 个顶层键）"));
        return null;
    }

    private OperationError? ApplyScript(
        YamlMappingNode config, ChainItemWithContent item, List<ChainLogEntry> logs, HashSet<string> existsKeys)
    {
        var configJson = YamlOps.ToJson(config)?.ToJsonString() ?? "{}";
        ScriptResult result;
        try
        {
            result = _scripts.Execute(item.Content, configJson, item.Item.Name);
        }
        catch (ScriptSandboxException ex)
        {
            foreach (var log in ex.Logs)
                logs.Add(log with { Uid = item.Item.Uid, Name = item.Item.Name });
            return OperationError.Of(ex.Code, ex.Message, "Script", item.Item.File);
        }

        foreach (var log in result.Logs)
            logs.Add(log with { Uid = item.Item.Uid, Name = item.Item.Name });

        var parsed = JsonNode.Parse(result.ConfigJson);
        if (parsed is not JsonObject)
            return OperationError.Of("script_invalid_return", "main 返回值必须是配置对象", "Script", item.Item.File);

        if (YamlOps.ParseMapping(YamlDotNetSerialization.Serialize(YamlOps.FromJson(parsed))) is not { } newConfig)
            return OperationError.Of("script_invalid_return", "脚本输出无法转换为 YAML Mapping", "Script", item.Item.File);

        config.Children.Clear();
        foreach (var (key, value) in newConfig.Children)
            config.Children[key] = value;
        YamlOps.LowercaseKeys(config);
        existsKeys.Add("script:" + item.Item.Uid);
        logs.Add(new ChainLogEntry("info", item.Item.Uid, item.Item.Name, "Script 已应用"));
        return null;
    }

    private static OperationError? ApplySeq(
        YamlMappingNode config, ChainItemWithContent item, List<ChainLogEntry> logs, HashSet<string> existsKeys)
    {
        var root = YamlOps.ParseMapping(item.Content);
        if (root is null)
            return OperationError.Of("yaml_invalid", $"{item.Item.Type} 文件不是合法的 YAML Mapping", item.Item.Type.ToString(), item.Item.File);

        var key = item.Item.Type switch
        {
            ChainType.Rules => "rules",
            ChainType.Proxies => "proxies",
            ChainType.Groups => "proxy-groups",
            _ => throw new InvalidOperationException(item.Item.Type.ToString()),
        };

        var prepend = GetSeq(root, "prepend");
        var append = GetSeq(root, "append");
        var delete = GetSeq(root, "delete");

        if (prepend is null && append is null && delete is null)
        {
            logs.Add(new ChainLogEntry("warn", item.Item.Uid, item.Item.Name, "缺少 prepend/append/delete 段，未做任何修改"));
            return null;
        }

        YamlOps.ApplySeqMap(config, key, prepend, append, delete);

        // 节点增删同步到第一个 selector 组，避免出现悬空成员
        if (key == "proxies")
            SyncProxyNamesToGroups(config, prepend, delete);

        existsKeys.Add(key);
        logs.Add(new ChainLogEntry("info", item.Item.Uid, item.Item.Name,
            $"{key} 调整：prepend={Count(prepend)} append={Count(append)} delete={Count(delete)}"));
        return null;
    }

    private static void SyncProxyNamesToGroups(YamlMappingNode config, YamlSequenceNode? prepend, YamlSequenceNode? delete)
    {
        if (!config.Children.TryGetValue(new YamlScalarNode("proxy-groups"), out var groupsNode) ||
            groupsNode is not YamlSequenceNode groups)
            return;

        var added = prepend?.Children
            .OfType<YamlMappingNode>()
            .Select(n => YamlOps.GetScalar(n, "name"))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!)
            .ToList() ?? [];
        var removed = delete?.Children
            .OfType<YamlScalarNode>()
            .Select(n => n.Value)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!)
            .ToList() ?? [];

        if (added.Count == 0 && removed.Count == 0) return;

        var firstSelector = groups.Children
            .OfType<YamlMappingNode>()
            .FirstOrDefault(g => YamlOps.GetScalar(g, "type")?.Equals("select", StringComparison.OrdinalIgnoreCase) == true);
        if (firstSelector is null)
            firstSelector = groups.Children.OfType<YamlMappingNode>().FirstOrDefault();

        if (firstSelector is not null &&
            firstSelector.Children.TryGetValue(new YamlScalarNode("proxies"), out var membersNode) &&
            membersNode is YamlSequenceNode members)
        {
            foreach (var name in removed)
                RemoveMember(members, name);
            foreach (var name in added)
                members.Children.Add(new YamlScalarNode(name));
        }
    }

    private static void RemoveMember(YamlSequenceNode members, string name)
    {
        for (var i = members.Children.Count - 1; i >= 0; i--)
        {
            if (members.Children[i] is YamlScalarNode { Value: var v } && v == name)
                members.Children.RemoveAt(i);
        }
    }

    private static void ApplyBuiltinEnhance(YamlMappingNode config, List<ChainLogEntry> logs)
    {
        // hysteria: alpn 若为字符串则修正为数组（mihomo 要求数组）
        if (!config.Children.TryGetValue(new YamlScalarNode("proxies"), out var proxiesNode) ||
            proxiesNode is not YamlSequenceNode proxies)
            return;

        var fixedCount = 0;
        foreach (var proxy in proxies.Children.OfType<YamlMappingNode>())
        {
            if (YamlOps.GetScalar(proxy, "type")?.Equals("hysteria", StringComparison.OrdinalIgnoreCase) != true &&
                YamlOps.GetScalar(proxy, "type")?.Equals("hysteria2", StringComparison.OrdinalIgnoreCase) != true)
                continue;

            if (proxy.Children.TryGetValue(new YamlScalarNode("alpn"), out var alpn) &&
                alpn is YamlScalarNode { Value: not null } alpnScalar &&
                alpnScalar.Value.Contains(','))
            {
                var alpnSeq = new YamlSequenceNode();
                foreach (var part in alpnScalar.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    alpnSeq.Children.Add(new YamlScalarNode(part));
                proxy.Children[new YamlScalarNode("alpn")] = alpnSeq;
                fixedCount++;
            }
        }

        if (fixedCount > 0)
            logs.Add(new ChainLogEntry("info", "builtin", "内置兼容增强", $"修正 {fixedCount} 个 hysteria 节点的 alpn 字段"));
    }

    private static void ApplyDnsOverride(YamlMappingNode config, YamlMappingNode dnsOverride, List<ChainLogEntry> logs)
    {
        if (dnsOverride.Children.TryGetValue(new YamlScalarNode("dns"), out var dnsNode) &&
            dnsNode is YamlMappingNode dnsPatch)
        {
            if (!config.Children.TryGetValue(new YamlScalarNode("dns"), out var existingDns) ||
                existingDns is not YamlMappingNode)
                config.Children[new YamlScalarNode("dns")] = new YamlMappingNode();
            YamlOps.DeepMerge((YamlMappingNode)config.Children[new YamlScalarNode("dns")], dnsPatch);
        }

        if (dnsOverride.Children.TryGetValue(new YamlScalarNode("hosts"), out var hostsNode))
            config.Children[new YamlScalarNode("hosts")] = YamlOps.Clone(hostsNode);

        logs.Add(new ChainLogEntry("info", "dns", "DNS 设置", "DNS 覆写已应用"));
    }

    private static void ApplyTun(YamlMappingNode config, bool enable, List<ChainLogEntry> logs)
    {
        if (!config.Children.TryGetValue(new YamlScalarNode("tun"), out var tunNode) ||
            tunNode is not YamlMappingNode tun)
        {
            tun = new YamlMappingNode();
            config.Children[new YamlScalarNode("tun")] = tun;
        }
        tun.Children[new YamlScalarNode("enable")] = new YamlScalarNode(enable ? "true" : "false");

        if (!enable) return;

        // TUN 需要启用的 DNS（fake-ip），缺失时补默认值
        if (!config.Children.TryGetValue(new YamlScalarNode("dns"), out var dnsNode) ||
            dnsNode is not YamlMappingNode dns)
        {
            dns = new YamlMappingNode();
            config.Children[new YamlScalarNode("dns")] = dns;
        }
        dns.Children[new YamlScalarNode("enable")] = new YamlScalarNode("true");
        if (YamlOps.GetScalar(dns, "enhanced-mode")?.Equals("fake-ip", StringComparison.OrdinalIgnoreCase) == true ||
            YamlOps.GetScalar(dns, "enhanced-mode") is null)
        {
            if (!dns.Children.ContainsKey(new YamlScalarNode("enhanced-mode")))
                dns.Children[new YamlScalarNode("enhanced-mode")] = new YamlScalarNode("fake-ip");
            if (!dns.Children.ContainsKey(new YamlScalarNode("fake-ip-range")))
                dns.Children[new YamlScalarNode("fake-ip-range")] = new YamlScalarNode("198.18.0.1/16");
        }

        logs.Add(new ChainLogEntry("info", "tun", "TUN", "TUN 已启用并补齐 DNS 默认值"));
    }

    private static OperationError? ApplyChainProxy(
        YamlMappingNode config, IReadOnlyDictionary<string, string> chainProxy, List<ChainLogEntry> logs)
    {
        if (!config.Children.TryGetValue(new YamlScalarNode("proxies"), out var proxiesNode) ||
            proxiesNode is not YamlSequenceNode proxies)
            return OperationError.Of("chain_no_proxies", "链式代理需要订阅中存在节点定义", "链式代理");

        var knownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var proxy in proxies.Children.OfType<YamlMappingNode>())
        {
            var name = YamlOps.GetScalar(proxy, "name");
            if (name is not null) knownNames.Add(name);
        }

        foreach (var (exit, entry) in chainProxy)
        {
            if (!knownNames.Contains(exit))
                return OperationError.Of("chain_unknown_exit", $"链式代理出口节点不存在: {exit}", "链式代理");
            if (!knownNames.Contains(entry))
                return OperationError.Of("chain_unknown_entry", $"链式代理入口节点不存在: {entry}", "链式代理");
        }

        foreach (var proxy in proxies.Children.OfType<YamlMappingNode>())
        {
            var name = YamlOps.GetScalar(proxy, "name");
            if (name is null) continue;
            if (chainProxy.TryGetValue(name, out var entry))
                proxy.Children[new YamlScalarNode("dialer-proxy")] = new YamlScalarNode(entry);
            else
                proxy.Children.Remove(new YamlScalarNode("dialer-proxy"));
        }

        logs.Add(new ChainLogEntry("info", "chain-proxy", "链式代理", $"已为 {chainProxy.Count} 个节点设置 dialer-proxy"));
        return null;
    }

    // ---------- 控制面 / 清理 / 排序 ----------

    private static Dictionary<string, YamlNode> CaptureControlPlane(YamlMappingNode clashBase)
    {
        var snapshot = new Dictionary<string, YamlNode>();
        foreach (var key in ControlPlaneKeys)
        {
            if (clashBase.Children.TryGetValue(new YamlScalarNode(key), out var value))
                snapshot[key] = YamlOps.Clone(value);
        }

        // dns.ipv6 随 allow-lan/ipv6 一并由应用管理
        if (clashBase.Children.TryGetValue(new YamlScalarNode("dns"), out var dnsBase) &&
            dnsBase is YamlMappingNode dnsMap &&
            dnsMap.Children.TryGetValue(new YamlScalarNode("ipv6"), out var dnsIpv6))
        {
            snapshot["dns.ipv6"] = YamlOps.Clone(dnsIpv6);
        }

        return snapshot;
    }

    private static void EnforceControlPlane(
        YamlMappingNode config, Dictionary<string, YamlNode> snapshot, List<ChainLogEntry> logs)
    {
        foreach (var (key, value) in snapshot)
        {
            if (key == "dns.ipv6")
            {
                if (config.Children.TryGetValue(new YamlScalarNode("dns"), out var dnsNode) &&
                    dnsNode is YamlMappingNode dns)
                    dns.Children[new YamlScalarNode("ipv6")] = value;
                continue;
            }
            config.Children[new YamlScalarNode(key)] = value;
        }

        // 快照中不存在的控制面键必须移除（订阅携带的 external-controller 等不可泄漏到运行时）
        foreach (var key in ControlPlaneKeys)
        {
            if (!snapshot.ContainsKey(key))
                config.Children.Remove(new YamlScalarNode(key));
        }
    }

    private static void EnsureLanBindAddress(YamlMappingNode config)
    {
        var allowLan = YamlOps.GetScalar(config, "allow-lan")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        if (!allowLan) return;
        var bind = YamlOps.GetScalar(config, "bind-address");
        if (bind is null || bind.StartsWith("127.") || bind == "localhost")
            config.Children[new YamlScalarNode("bind-address")] = new YamlScalarNode("*");
    }

    private static void CleanupProxyGroups(YamlMappingNode config, List<ChainLogEntry> logs)
    {
        if (!config.Children.TryGetValue(new YamlScalarNode("proxy-groups"), out var groupsNode) ||
            groupsNode is not YamlSequenceNode groups)
            return;

        var knownProxies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (config.Children.TryGetValue(new YamlScalarNode("proxies"), out var proxiesNode) &&
            proxiesNode is YamlSequenceNode proxies)
        {
            foreach (var proxy in proxies.Children.OfType<YamlMappingNode>())
            {
                var name = YamlOps.GetScalar(proxy, "name");
                if (name is not null) knownProxies.Add(name);
            }
        }
        if (config.Children.TryGetValue(new YamlScalarNode("proxy-providers"), out var providersNode) &&
            providersNode is YamlMappingNode providers)
        {
            foreach (var key in providers.Children.Keys)
                knownProxies.Add(key.ToString() ?? "");
        }

        var pruned = 0;
        foreach (var group in groups.Children.OfType<YamlMappingNode>())
        {
            var usesProvider = group.Children.TryGetValue(new YamlScalarNode("use"), out _);

            if (group.Children.TryGetValue(new YamlScalarNode("proxies"), out var membersNode) &&
                membersNode is YamlSequenceNode members)
            {
                var valid = members.Children
                    .Where(m => m is YamlScalarNode { Value: not null } scalar &&
                                (knownProxies.Contains(scalar.Value) || BuiltinProxies.Contains(scalar.Value) ||
                                 groups.Children.OfType<YamlMappingNode>()
                                     .Any(g => YamlOps.GetScalar(g, "name") == scalar.Value)))
                    .ToList();
                if (valid.Count != members.Children.Count)
                {
                    pruned += members.Children.Count - valid.Count;
                    members.Children.Clear();
                    foreach (var item in valid)
                        members.Children.Add(item);
                }
            }

            // 引用不存在 provider 的 use 列表清理
            if (usesProvider &&
                group.Children.TryGetValue(new YamlScalarNode("use"), out var useNode) &&
                useNode is YamlSequenceNode use)
            {
                var validProviders = use.Children
                    .Where(u => u is YamlScalarNode { Value: not null } s &&
                                providersNode is not null &&
                                config.Children.TryGetValue(new YamlScalarNode("proxy-providers"), out var pn) &&
                                pn is YamlMappingNode pm && pm.Children.ContainsKey(new YamlScalarNode(s.Value)))
                    .ToList();
                if (validProviders.Count != use.Children.Count)
                {
                    pruned += use.Children.Count - validProviders.Count;
                    use.Children.Clear();
                    foreach (var item in validProviders)
                        use.Children.Add(item);
                }
            }
        }

        if (pruned > 0)
            logs.Add(new ChainLogEntry("warn", "cleanup", "代理组清理", $"移除 {pruned} 个无效的代理组成员/Provider 引用"));
    }

    private static void SortFields(YamlMappingNode config)
    {
        var handleEntries = new List<KeyValuePair<YamlNode, YamlNode>>();
        var middleEntries = new List<KeyValuePair<YamlNode, YamlNode>>();
        var defaultEntries = new List<KeyValuePair<YamlNode, YamlNode>>();
        foreach (var (key, value) in config.Children)
        {
            var name = key.ToString() ?? "";
            if (HandleFields.Contains(name, StringComparer.OrdinalIgnoreCase))
                handleEntries.Add(new(key, value));
            else if (DefaultFieldsLast.Contains(name, StringComparer.OrdinalIgnoreCase))
                defaultEntries.Add(new(key, value));
            else
                middleEntries.Add(new(key, value));
        }

        config.Children.Clear();
        foreach (var pair in handleEntries.Concat(middleEntries).Concat(defaultEntries))
            config.Children[pair.Key] = pair.Value;
    }

    private static bool KeyEquals(YamlNode key, string field) =>
        key is YamlScalarNode { Value: not null } scalar &&
        string.Equals(scalar.Value, field, StringComparison.OrdinalIgnoreCase);

    private static YamlSequenceNode? GetSeq(YamlMappingNode map, string key) =>
        map.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlSequenceNode seq
            ? seq
            : null;

    private static int Count(YamlSequenceNode? seq) => seq?.Children.Count ?? 0;

    private static OperationResult<RuntimeConfigOutput> FailStep(ChainItemWithContent item, OperationError error) =>
        OperationResult<RuntimeConfigOutput>.Fail(error);
}

internal static class YamlDotNetSerialization
{
    public static string Serialize(YamlNode node) => new Serializer().Serialize(node);
}
