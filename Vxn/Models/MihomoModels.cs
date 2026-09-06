using System.Text.Json;

namespace Vxn.Models;

/// <summary>来自 /proxies 的代理或代理组。</summary>
public class ProxyInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Now { get; set; } = "";
    public List<string> All { get; set; } = new();
    public bool Udp { get; set; }
    public bool Alive { get; set; } = true;
    /// <summary>最近一次测速延迟（毫秒），-1 未知，0 超时。</summary>
    public int Delay { get; set; } = -1;

    public bool IsGroup => Type is "Selector" or "URLTest" or "Fallback" or "LoadBalance" or "Relay";

    public static ProxyInfo FromJson(JsonProperty p)
    {
        var info = new ProxyInfo { Name = p.Name };
        var v = p.Value;
        if (v.ValueKind != JsonValueKind.Object) return info;
        if (v.TryGetProperty("type", out var t)) info.Type = t.GetString() ?? "";
        if (v.TryGetProperty("now", out var now)) info.Now = now.GetString() ?? "";
        if (v.TryGetProperty("udp", out var udp) && udp.ValueKind == JsonValueKind.True) info.Udp = true;
        if (v.TryGetProperty("alive", out var alive)) info.Alive = alive.ValueKind != JsonValueKind.False;
        if (v.TryGetProperty("all", out var all) && all.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in all.EnumerateArray())
            {
                if (a.GetString() is { } s) info.All.Add(s);
            }
        }
        if (v.TryGetProperty("history", out var history) && history.ValueKind == JsonValueKind.Array)
        {
            // 取最后一次测速记录
            foreach (var h in history.EnumerateArray())
            {
                if (h.TryGetProperty("delay", out var d) && d.ValueKind == JsonValueKind.Number)
                    info.Delay = d.GetInt32();
            }
        }
        return info;
    }
}

public class ConnectionMetadata
{
    public string Network { get; set; } = "";
    public string Type { get; set; } = "";
    public string SourceIp { get; set; } = "";
    public string SourcePort { get; set; } = "";
    public string DestinationIp { get; set; } = "";
    public string DestinationPort { get; set; } = "";
    public string Host { get; set; } = "";
    public string ProcessPath { get; set; } = "";
    public string Process { get; set; } = "";

    public string DisplayName => string.IsNullOrEmpty(Host)
        ? (string.IsNullOrEmpty(DestinationIp) ? "(unknown)" : DestinationIp)
        : Host;
}

/// <summary>来自 /connections 的单个连接。</summary>
public class ConnectionItem
{
    public string Id { get; set; } = "";
    public bool Closed { get; set; }
    public long Upload { get; set; }
    public long Download { get; set; }
    public long UploadSpeed { get; set; }
    public long DownloadSpeed { get; set; }
    public DateTime Start { get; set; }
    public List<string> Chains { get; set; } = new();
    public string Rule { get; set; } = "";
    public string RulePayload { get; set; } = "";
    public ConnectionMetadata Metadata { get; set; } = new();

    public static ConnectionItem FromJson(JsonElement v)
    {
        var c = new ConnectionItem();
        if (v.TryGetProperty("id", out var id)) c.Id = id.GetString() ?? "";
        if (v.TryGetProperty("upload", out var up)) c.Upload = up.GetInt64();
        if (v.TryGetProperty("download", out var down)) c.Download = down.GetInt64();
        if (v.TryGetProperty("start", out var start) && DateTime.TryParse(start.GetString(), out var st))
            c.Start = st.ToLocalTime();
        if (v.TryGetProperty("chains", out var chains) && chains.ValueKind == JsonValueKind.Array)
            foreach (var ch in chains.EnumerateArray())
                if (ch.GetString() is { } s) c.Chains.Add(s);
        if (v.TryGetProperty("rule", out var rule)) c.Rule = rule.GetString() ?? "";
        if (v.TryGetProperty("rulePayload", out var rp)) c.RulePayload = rp.GetString() ?? "";
        if (v.TryGetProperty("metadata", out var m))
        {
            var md = c.Metadata;
            if (m.TryGetProperty("network", out var x)) md.Network = x.GetString() ?? "";
            if (m.TryGetProperty("type", out x)) md.Type = x.GetString() ?? "";
            if (m.TryGetProperty("sourceIP", out x)) md.SourceIp = x.GetString() ?? "";
            if (m.TryGetProperty("sourcePort", out x)) md.SourcePort = x.GetString() ?? "";
            if (m.TryGetProperty("destinationIP", out x)) md.DestinationIp = x.GetString() ?? "";
            if (m.TryGetProperty("destinationPort", out x)) md.DestinationPort = x.GetString() ?? "";
            if (m.TryGetProperty("host", out x)) md.Host = x.GetString() ?? "";
            if (m.TryGetProperty("processPath", out x)) md.ProcessPath = x.GetString() ?? "";
            if (m.TryGetProperty("process", out x)) md.Process = x.GetString() ?? "";
        }
        return c;
    }
}

/// <summary>来自 /connections 的完整快照。</summary>
public class ConnectionsSnapshot
{
    public List<ConnectionItem> Connections { get; set; } = new();
    public long UploadTotal { get; set; }
    public long DownloadTotal { get; set; }
}
