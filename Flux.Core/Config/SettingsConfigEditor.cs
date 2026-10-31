using YamlDotNet.RepresentationModel;

namespace Flux.Core.Config;

/// <summary>设置页的 DNS/TUN 编辑与应用事务。</summary>
public static class SettingsConfigEditor
{
    public static string ReadDnsHijack(YamlMappingNode config)
    {
        if (config.Children.TryGetValue(new YamlScalarNode("tun"), out var tunNode) &&
            tunNode is YamlMappingNode tun &&
            tun.Children.TryGetValue(new YamlScalarNode("dns-hijack"), out var hijack))
        {
            // 兼容旧版误存的标量，下一次应用时统一改为序列。
            var items = hijack is YamlSequenceNode sequence
                ? sequence.Children.OfType<YamlScalarNode>().Select(s => s.Value ?? "")
                : new[] { (hijack as YamlScalarNode)?.Value ?? "" };
            var text = string.Join("; ", items.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (text.Length > 0) return text;
        }
        return "any:53";
    }

    public static void EditTun(YamlMappingNode tun, string stack, string hijack)
    {
        var items = hijack.Split([';', ',', '\r', '\n'],
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        tun.Children[new YamlScalarNode("stack")] = new YamlScalarNode(stack);
        tun.Children[new YamlScalarNode("dns-hijack")] = new YamlSequenceNode(
            (items.Length == 0 ? ["any:53"] : items).Select(s => new YamlScalarNode(s)));
    }

    public static void EditDns(YamlMappingNode dns, string mode, string fakeIpRange)
    {
        dns.Children[new YamlScalarNode("enable")] = new YamlScalarNode("true");
        dns.Children[new YamlScalarNode("enhanced-mode")] = new YamlScalarNode(mode);
        if (!string.IsNullOrWhiteSpace(fakeIpRange))
            dns.Children[new YamlScalarNode("fake-ip-range")] = new YamlScalarNode(fakeIpRange.Trim());
    }

    /// <summary>只回滚所编辑的节，保留应用期间其他设置的修改；调用方应避免同时编辑同一节。</summary>
    public static async Task<bool> ApplySectionAsync(YamlMappingNode config, string section,
        Action<YamlMappingNode> edit, Action persist, Func<Task<bool>> apply)
    {
        var key = new YamlScalarNode(section);
        var existed = config.Children.TryGetValue(key, out var previous);
        var candidate = previous is YamlMappingNode mapping
            ? (YamlMappingNode)YamlOps.Clone(mapping) : new YamlMappingNode();
        edit(candidate);
        config.Children[key] = candidate;
        try
        {
            persist();
            if (await apply()) return true;
        }
        catch
        {
            Restore();
            throw;
        }
        Restore();
        return false;

        void Restore()
        {
            if (existed) config.Children[key] = previous!;
            else config.Children.Remove(key);
            persist();
        }
    }
}
