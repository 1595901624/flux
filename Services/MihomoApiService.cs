using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Flux.Services;

/// <summary>
/// mihomo External Controller REST 客户端。
/// </summary>
public class MihomoApiService
{
    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri("http://127.0.0.1:9097"),
        Timeout = TimeSpan.FromSeconds(15),
    };

    public void Configure(string controller, string secret)
    {
        var host = controller.StartsWith(':') ? "127.0.0.1" + controller : controller;
        if (!host.StartsWith("http")) host = "http://" + host;
        _http.BaseAddress = new Uri(host.TrimEnd('/') + "/");
        _http.DefaultRequestHeaders.Authorization =
            string.IsNullOrEmpty(secret) ? null : new AuthenticationHeaderValue("Bearer", secret);
    }

    private async Task<JsonElement> SendAsync(
        HttpMethod method, string path, object? body = null, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            req.Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(text)
            ? JsonSerializer.Deserialize<JsonElement>("{}")
            : JsonSerializer.Deserialize<JsonElement>(text);
    }

    // ---------- 基础 ----------

    /// <summary>GET /version — 也用于启动就绪探测。</summary>
    public async Task<string?> GetVersionAsync()
    {
        try
        {
            var json = await SendAsync(HttpMethod.Get, "version").ConfigureAwait(false);
            return json.TryGetProperty("version", out var v) ? v.GetString() : null;
        }
        catch { return null; }
    }

    public Task<JsonElement> GetConfigsAsync() =>
        SendAsync(HttpMethod.Get, "configs");

    public Task<JsonElement> PatchConfigsAsync(Dictionary<string, object> patch) =>
        SendAsync(HttpMethod.Patch, "configs", patch);

    /// <summary>PUT /configs?force=true — 热重载配置文件。</summary>
    public Task<JsonElement> ReloadConfigAsync(string path) =>
        SendAsync(HttpMethod.Put, "configs?force=true", new { path });

    public Task<JsonElement> UpdateGeoAsync() =>
        SendAsync(HttpMethod.Post, "configs/geo");

    // ---------- 代理 ----------

    public Task<JsonElement> GetProxiesAsync() =>
        SendAsync(HttpMethod.Get, "proxies");

    /// <summary>PUT /proxies/{group} — 切换组内节点。</summary>
    public Task<JsonElement> SelectProxyAsync(string group, string name) =>
        SendAsync(HttpMethod.Put, "proxies/" + Uri.EscapeDataString(group), new { name });

    /// <summary>GET /proxies/{name}/delay — 单节点延迟测试。</summary>
    public async Task<int> GetProxyDelayAsync(string name, string testUrl, int timeoutMs)
    {
        try
        {
            var json = await SendAsync(HttpMethod.Get,
                $"proxies/{Uri.EscapeDataString(name)}/delay?timeout={timeoutMs}&url={Uri.EscapeDataString(testUrl)}",
                ct: new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs + 8000)).Token)
                .ConfigureAwait(false);
            return json.TryGetProperty("delay", out var d) ? d.GetInt32() : -1;
        }
        catch { return -1; }
    }

    /// <summary>GET /group/{name}/delay — 整组延迟测试，返回 {节点名: 延迟}。</summary>
    public async Task<Dictionary<string, int>> GetGroupDelayAsync(string group, string testUrl, int timeoutMs)
    {
        try
        {
            var json = await SendAsync(HttpMethod.Get,
                $"group/{Uri.EscapeDataString(group)}/delay?timeout={timeoutMs}&url={Uri.EscapeDataString(testUrl)}",
                ct: new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs + 30000)).Token)
                .ConfigureAwait(false);
            var result = new Dictionary<string, int>();
            foreach (var p in json.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.Number)
                    result[p.Name] = p.Value.GetInt32();
            }
            return result;
        }
        catch { return new Dictionary<string, int>(); }
    }

    // ---------- 连接 ----------

    public Task<JsonElement> GetConnectionsAsync() =>
        SendAsync(HttpMethod.Get, "connections");

    public Task<JsonElement> CloseConnectionAsync(string id) =>
        SendAsync(HttpMethod.Delete, "connections/" + Uri.EscapeDataString(id));

    public Task<JsonElement> CloseAllConnectionsAsync() =>
        SendAsync(HttpMethod.Delete, "connections");

    /// <summary>
    /// 关闭仍使用指定节点的已有连接。切换节点不会迁移已经建立的 TCP 连接；
    /// 此操作与 Clash Verge Rev 的 auto-close-connection 行为一致。
    /// </summary>
    public async Task<int> CloseConnectionsUsingProxyAsync(string proxyName)
    {
        var snapshot = await GetConnectionsAsync().ConfigureAwait(false);
        var ids = FindConnectionIdsUsingProxy(snapshot, proxyName);

        await Task.WhenAll(ids.Select(async id =>
        {
            try { await CloseConnectionAsync(id).ConfigureAwait(false); }
            catch { /* 连接可能已自然结束 */ }
        })).ConfigureAwait(false);
        return ids.Count;
    }

    internal static IReadOnlyList<string> FindConnectionIdsUsingProxy(
        JsonElement snapshot, string proxyName)
    {
        if (!snapshot.TryGetProperty("connections", out var connections) ||
            connections.ValueKind != JsonValueKind.Array)
            return [];

        var ids = new List<string>();
        foreach (var connection in connections.EnumerateArray())
        {
            if (connection.ValueKind != JsonValueKind.Object ||
                !connection.TryGetProperty("id", out var id) ||
                string.IsNullOrEmpty(id.GetString()) ||
                !connection.TryGetProperty("chains", out var chains) ||
                chains.ValueKind != JsonValueKind.Array)
                continue;

            if (chains.EnumerateArray().Any(chain => chain.GetString() == proxyName))
                ids.Add(id.GetString()!);
        }
        return ids;
    }

    // ---------- 规则 / Provider ----------

    public Task<JsonElement> GetRulesAsync() =>
        SendAsync(HttpMethod.Get, "rules");

    public Task<JsonElement> GetProxyProvidersAsync() =>
        SendAsync(HttpMethod.Get, "providers/proxies");

    public Task<JsonElement> HealthCheckProviderAsync(string name) =>
        SendAsync(HttpMethod.Get, $"providers/proxies/{Uri.EscapeDataString(name)}/healthcheck");
}
