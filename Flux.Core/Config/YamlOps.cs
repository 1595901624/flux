using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.RepresentationModel;

namespace Flux.Core.Config;

/// <summary>YAML 映射操作工具：键小写化、深合并、序列前后插入、JSON 互转。</summary>
public static class YamlOps
{
    /// <summary>解析 YAML 文本为 Mapping；失败返回 null。</summary>
    public static YamlMappingNode? ParseMapping(string yaml)
    {
        try
        {
            using var reader = new StringReader(yaml);
            var stream = new YamlStream();
            stream.Load(reader);
            return stream.Documents.Count > 0 &&
                   stream.Documents[0].RootNode is YamlMappingNode mapping
                ? mapping
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>递归小写化所有顶层及嵌套映射的标量键（mihomo 键不区分大小写）。</summary>
    public static void LowercaseKeys(YamlMappingNode mapping)
    {
        foreach (var (key, value) in mapping.Children.ToList())
        {
            if (value is YamlMappingNode child)
            {
                LowercaseKeys(child);
            }
            else if (value is YamlSequenceNode seq)
            {
                foreach (var item in seq.Children.OfType<YamlMappingNode>())
                    LowercaseKeys(item);
            }

            var text = key is YamlScalarNode scalar ? scalar.Value : null;
            if (text is not null && !string.Equals(text, text.ToLowerInvariant(), StringComparison.Ordinal))
            {
                mapping.Children.Remove(key);
                mapping.Children[new YamlScalarNode(text.ToLowerInvariant())] = value;
            }
        }
    }

    /// <summary>
    /// 深合并：both 为映射时递归合并，否则 right 覆盖 left。
    /// right 中的 YAML null（null/~）表示删除该键（Merge 语义支持 "key:" 置空移除）。
    /// </summary>
    public static void DeepMerge(YamlMappingNode target, YamlMappingNode overlay)
    {
        foreach (var (key, value) in overlay.Children)
        {
            if (value is YamlScalarNode scalar && IsYamlNull(scalar.Value))
            {
                target.Children.Remove(key);
                continue;
            }

            if (target.Children.TryGetValue(key, out var existing) &&
                existing is YamlMappingNode existingMap &&
                value is YamlMappingNode overlayMap)
            {
                DeepMerge(existingMap, overlayMap);
            }
            else
            {
                target.Children[key] = Clone(value);
            }
        }
    }

    public static YamlNode Clone(YamlNode node) => node switch
    {
        YamlMappingNode map => CloneMapping(map),
        YamlSequenceNode seq => CloneSequence(seq),
        YamlScalarNode scalar => new YamlScalarNode(scalar.Value) { Style = scalar.Style },
        _ => node,
    };

    private static bool IsYamlNull(string? value) =>
        value is null || value == "~" || value.Equals("null", StringComparison.OrdinalIgnoreCase) || value == "";

    private static YamlMappingNode CloneMapping(YamlMappingNode map)
    {
        var result = new YamlMappingNode();
        foreach (var (key, value) in map.Children)
            result.Children[Clone(key)] = Clone(value);
        return result;
    }

    private static YamlSequenceNode CloneSequence(YamlSequenceNode seq)
    {
        var result = new YamlSequenceNode();
        foreach (var item in seq.Children)
            result.Children.Add(Clone(item));
        return result;
    }

    /// <summary>对键应用 SeqMap 语义：prepend + (existing - delete) + append。返回是否命中该键。</summary>
    public static bool ApplySeqMap(
        YamlMappingNode config,
        string key,
        YamlSequenceNode? prepend,
        YamlSequenceNode? append,
        YamlSequenceNode? delete)
    {
        if (!config.Children.TryGetValue(new YamlScalarNode(key), out var node) ||
            node is not YamlSequenceNode seq)
        {
            // 目标键不存在且没有要插入的内容时跳过
            if ((prepend is null || prepend.Children.Count == 0) &&
                (append is null || append.Children.Count == 0))
                return false;

            seq = new YamlSequenceNode();
            config.Children[new YamlScalarNode(key)] = seq;
        }

        var items = seq.Children.ToList();

        if (delete is not null)
        {
            var deleteKeys = delete.Children
                .Select(ScalarKeyOf)
                .Where(k => k is not null)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (deleteKeys.Count > 0)
                items.RemoveAll(item => deleteKeys.Contains(ScalarKeyOf(item) ?? ""));
        }

        if (prepend is not null)
            items.InsertRange(0, prepend.Children.Select(Clone));

        if (append is not null)
            items.AddRange(append.Children.Select(Clone));

        seq.Children.Clear();
        foreach (var item in items)
            seq.Children.Add(item);

        return true;
    }

    /// <summary>取序列元素用于删除匹配的标量键（规则字符串或节点的 name 字段）。</summary>
    public static string? ScalarKeyOf(YamlNode node) => node switch
    {
        YamlScalarNode scalar => scalar.Value,
        YamlMappingNode map when map.Children.TryGetValue(new YamlScalarNode("name"), out var n) &&
                                 n is YamlScalarNode nameScalar => nameScalar.Value,
        _ => null,
    };

    private static YamlMappingNode CloneMapping2(YamlMappingNode map)
    {
        var result = new YamlMappingNode();
        foreach (var (key, value) in map.Children)
            result.Children[key] = Clone(value);
        return result;
    }

    /// <summary>把 YAML 映射转换为 JSON 节点（供脚本引擎使用）。</summary>
    public static JsonNode? ToJson(YamlNode node) => node switch
    {
        YamlMappingNode map => MapToJson(map),
        YamlSequenceNode seq => SeqToJson(seq),
        YamlScalarNode scalar => ScalarToJson(scalar.Value),
        _ => null,
    };

    private static JsonObject MapToJson(YamlMappingNode map)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in map.Children)
        {
            var name = key is YamlScalarNode s ? s.Value : key.ToString();
            if (name is null) continue;
            obj[name] = ToJson(value);
        }
        return obj;
    }

    private static JsonArray SeqToJson(YamlSequenceNode seq)
    {
        var arr = new JsonArray();
        foreach (var item in seq.Children)
            arr.Add(ToJson(item));
        return arr;
    }

    private static JsonNode? ScalarToJson(string? value)
    {
        if (value is null) return null;
        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)) return JsonValue.Create(true);
        if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)) return JsonValue.Create(false);
        if (value.Equals("null", StringComparison.OrdinalIgnoreCase) || value == "~") return null;
        if (long.TryParse(value, out var l)) return JsonValue.Create(l);
        if (double.TryParse(value, out var d)) return JsonValue.Create(d);
        return JsonValue.Create(value);
    }

    /// <summary>把 JSON 节点转换回 YAML 节点。</summary>
    public static YamlNode FromJson(JsonNode? node) => node switch
    {
        JsonObject obj => JsonObjToYaml(obj),
        JsonArray arr => JsonArrToYaml(arr),
        JsonValue v => JsonValueToYaml(v),
        _ => new YamlScalarNode("null"),
    };

    private static YamlMappingNode JsonObjToYaml(JsonObject obj)
    {
        var map = new YamlMappingNode();
        foreach (var (key, value) in obj)
            map.Children[new YamlScalarNode(key)] = FromJson(value);
        return map;
    }

    private static YamlSequenceNode JsonArrToYaml(JsonArray arr)
    {
        var seq = new YamlSequenceNode();
        foreach (var item in arr)
            seq.Children.Add(FromJson(item));
        return seq;
    }

    private static YamlNode JsonValueToYaml(JsonValue value)
    {
        if (value.TryGetValue<bool>(out var b)) return new YamlScalarNode(b ? "true" : "false");
        if (value.TryGetValue<long>(out var l)) return new YamlScalarNode(l.ToString());
        if (value.TryGetValue<double>(out var d)) return new YamlScalarNode(d.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        if (value.TryGetValue<string>(out var s)) return new YamlScalarNode(s);
        return new YamlScalarNode("null");
    }

    /// <summary>获取映射中标量字符串值。路径逐段给定（避免与含点的键名如域名冲突）。</summary>
    public static string? GetScalar(YamlMappingNode map, params string[] path)
    {
        YamlNode current = map;
        for (var i = 0; i < path.Length - 1; i++)
        {
            if (current is not YamlMappingNode mapping ||
                !mapping.Children.TryGetValue(new YamlScalarNode(path[i]), out var next))
                return null;
            current = next;
        }

        if (current is not YamlMappingNode last ||
            !last.Children.TryGetValue(new YamlScalarNode(path[^1]), out var leaf) ||
            leaf is not YamlScalarNode scalar)
            return null;
        return scalar.Value;
    }

    /// <summary>设置映射中路径（a.b.c）的值，自动创建中间映射。</summary>
    public static void SetValue(YamlMappingNode map, string path, string value)
    {
        var parts = path.Split('.');
        var current = map;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var key = new YamlScalarNode(parts[i]);
            if (current.Children.TryGetValue(key, out var next) && next is YamlMappingNode nextMap)
            {
                current = nextMap;
            }
            else
            {
                var created = new YamlMappingNode();
                current.Children[key] = created;
                current = created;
            }
        }
        current.Children[new YamlScalarNode(parts[^1])] = new YamlScalarNode(value);
    }
}
