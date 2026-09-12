using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Flux.Services;

/// <summary>YAML 节点操作辅助（深合并、读写标量）。</summary>
public static class YamlHelper
{
    public static YamlMappingNode? ParseMapping(string text)
    {
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(text));
            return stream.Documents.Count > 0 &&
                   stream.Documents[0].RootNode is YamlMappingNode mapping
                ? mapping
                : null;
        }
        catch (Exception ex)
        {
            LogService.App(Flux.Services.L10n.F("Yaml_ParseFailed", ex.Message), "error");
        }
        return null;
    }

    public static string? GetScalar(YamlMappingNode mapping, string key)
    {
        if (mapping.Children.TryGetValue(new YamlScalarNode(key), out var node) &&
            node is YamlScalarNode scalar)
            return scalar.Value;
        return null;
    }

    public static void SetScalar(YamlMappingNode mapping, string key, string value)
    {
        mapping.Children[new YamlScalarNode(key)] = new YamlScalarNode(value);
    }

    /// <summary>按 key（支持 a.b.c 嵌套）设置标量值；bool/int 自动输出为无引号标量。</summary>
    public static void SetValue(YamlMappingNode mapping, string key, object value)
    {
        YamlNode ValueToNode(object v) => v switch
        {
            bool b => new YamlScalarNode(b ? "true" : "false"),
            int i => new YamlScalarNode(i.ToString()),
            long l => new YamlScalarNode(l.ToString()),
            string s => new YamlScalarNode(s),
            _ => new YamlScalarNode(v.ToString() ?? "")
        };

        var parts = key.Split('.');
        var current = mapping;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var k = new YamlScalarNode(parts[i]);
            if (current.Children.TryGetValue(k, out var next) && next is YamlMappingNode nextMap)
            {
                current = nextMap;
            }
            else
            {
                var newMap = new YamlMappingNode();
                current.Children[k] = newMap;
                current = newMap;
            }
        }
        current.Children[new YamlScalarNode(parts[^1])] = ValueToNode(value);
    }

    /// <summary>source 深度覆盖 target：两侧均为映射时递归合并，否则整体替换（深拷贝）。</summary>
    public static void DeepOverlay(YamlMappingNode target, YamlMappingNode source)
    {
        foreach (var (key, value) in source.Children)
        {
            if (target.Children.TryGetValue(key, out var existing) &&
                existing is YamlMappingNode existingMap &&
                value is YamlMappingNode valueMap)
            {
                DeepOverlay(existingMap, valueMap);
            }
            else
            {
                target.Children[key] = CloneNode(value);
            }
        }
    }

    /// <summary>YAML 节点深拷贝（YamlDotNet 18 无 Clone API）。</summary>
    public static YamlNode CloneNode(YamlNode node) => node switch
    {
        YamlMappingNode mapping => new YamlMappingNode(
            mapping.Children.ToDictionary(kv => kv.Key, kv => CloneNode(kv.Value))),
        YamlSequenceNode sequence => new YamlSequenceNode(
            sequence.Children.Select(CloneNode)),
        _ => node,
    };
}
