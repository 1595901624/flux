using System.Net;
using System.Text.RegularExpressions;

namespace Flux.Core.Unlock;

/// <summary>单项检测结果。Status: 支持 | 不支持 | 未知 | 失败。</summary>
public sealed record UnlockResult(string Id, string Name, string Status, string? Region = null, string? Detail = null);

/// <summary>解锁检测规则：请求 + 响应判定。解析不出结论时必须返回"未知"，不误报。</summary>
public sealed class UnlockCheck
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Url { get; init; } = "";
    public string? Body { get; init; }
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>响应判定：body 与 status → (status, region, detail)。</summary>
    public Func<int, string, (string Status, string? Region, string? Detail)> Judge { get; init; } =
        (_, _) => ("unknown", null, null);
}

/// <summary>
/// 流媒体/AI 服务解锁检测：所有请求经本应用内核混合端口（127.0.0.1:port）发出。
/// 每项独立超时；并发受限；外部响应变化时返回"未知"。
/// </summary>
public sealed class UnlockTestService
{
    private readonly string _proxyHost;
    private readonly int _proxyPort;

    public UnlockTestService(int proxyPort, string proxyHost = "127.0.0.1")
    {
        _proxyPort = proxyPort;
        _proxyHost = proxyHost;
    }

    public IReadOnlyList<UnlockCheck> Checks { get; } = BuildChecks();

    /// <summary>运行全部检测；并发受 4 限制，ct 取消全部请求。</summary>
    public async Task<IReadOnlyList<UnlockResult>> RunAllAsync(int timeoutMs, CancellationToken ct = default)
    {
        using var gate = new SemaphoreSlim(4, 4);
        var tasks = Checks.Select(async check =>
        {
            await gate.WaitAsync(ct);
            try { return await RunAsync(check, timeoutMs, ct); }
            catch (OperationCanceledException) { throw; }
            finally { gate.Release(); }
        });
        return await Task.WhenAll(tasks);
    }

    public async Task<UnlockResult> RunAsync(UnlockCheck check, int timeoutMs, CancellationToken ct = default)
    {
        try
        {
            using var handler = new HttpClientHandler
            {
                Proxy = new WebProxy($"http://{_proxyHost}:{_proxyPort}"),
                UseProxy = true,
                AllowAutoRedirect = true,
            };
            using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Math.Clamp(timeoutMs, 1000, 30000));

            using var request = new HttpRequestMessage(HttpMethod.Get, check.Url);
            if (check.Headers is not null)
            {
                foreach (var (key, value) in check.Headers)
                    request.Headers.TryAddWithoutValidation(key, value);
            }
            request.Headers.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36");

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            var body = await response.Content.ReadAsStringAsync(cts.Token);
            var (status, region, detail) = check.Judge((int)response.StatusCode, body);
            return new UnlockResult(check.Id, check.Name, status, region, detail);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new UnlockResult(check.Id, check.Name, "失败", null, "请求超时");
        }
        catch (Exception ex)
        {
            return new UnlockResult(check.Id, check.Name, "失败", null, ex.Message);
        }
    }

    private static IReadOnlyList<UnlockCheck> BuildChecks() =>
    [
        new UnlockCheck
        {
            Id = "netflix", Name = "Netflix",
            Url = "https://www.netflix.com/title/81280792",
            Judge = (status, body) =>
            {
                if (status == 200)
                {
                    // 标题页可访问 = 非自制内容解锁；地区从页面 JS 常量提取
                    var region = Extract(body, @"""currentCountry"":\s*""([A-Za-z]{2})""") ??
                                 Extract(body, @"requestCountry"":""([A-Za-z]{2})""");
                    return ("supported", region, "original-only unlocked");
                }
                if (status == 403) return ("unsupported", null, "403");
                if (status == 404) return ("supported", null, "originals only");
                return ("unknown", null, $"HTTP {status}");
            },
        },
        new UnlockCheck
        {
            Id = "youtube", Name = "YouTube Premium",
            Url = "https://www.youtube.com/premium",
            Judge = (status, body) =>
            {
                if (status != 200) return ("unknown", null, $"HTTP {status}");
                if (body.Contains("Premium is not available in your country", StringComparison.OrdinalIgnoreCase))
                    return ("不支持", null, null);
                var region = Extract(body, @"""gl"":\s*""([A-Za-z]{2})""");
                return ("supported", region, null);
            },
        },
        new UnlockCheck
        {
            Id = "disney", Name = "Disney+",
            Url = "https://www.disneyplus.com/home",
            Judge = (status, body) =>
            {
                if (status == 200 && (body.Contains("disneyplus", StringComparison.OrdinalIgnoreCase)))
                {
                    var region = Extract(body, @"""region"":\s*""([A-Za-z]{2})""");
                    return ("supported", region, null);
                }
                if (status == 403 || status == 404) return ("不支持", null, $"HTTP {status}");
                return ("unknown", null, $"HTTP {status}");
            },
        },
        new UnlockCheck
        {
            Id = "chatgpt", Name = "ChatGPT",
            Url = "https://ios.chat.openai.com/public-api/mobile/server_status/v1",
            Judge = (status, body) =>
            {
                if (status == 200 && body.Contains("display_name", StringComparison.OrdinalIgnoreCase))
                    return ("支持", null, null);
                if (status == 403 || status == 451) return ("unsupported", null, $"HTTP {status}");
                return ("unknown", null, $"HTTP {status}");
            },
        },
        new UnlockCheck
        {
            Id = "claude", Name = "Claude",
            Url = "https://claude.ai/login",
            Judge = (status, _) =>
            {
                if (status == 200) return ("supported", null, null);
                if (status == 403) return ("unsupported", null, "403");
                return ("unknown", null, $"HTTP {status}");
            },
        },
        new UnlockCheck
        {
            Id = "gemini", Name = "Gemini",
            Url = "https://gemini.google.com/",
            Judge = (status, body) =>
            {
                if (status == 200)
                {
                    if (body.Contains("not available in your country", StringComparison.OrdinalIgnoreCase))
                        return ("不支持", null, null);
                    var region = Extract(body, @"""countryCode"":\s*""([A-Za-z]{2})""");
                    return ("supported", region, null);
                }
                return ("unknown", null, $"HTTP {status}");
            },
        },
        new UnlockCheck
        {
            Id = "spotify", Name = "Spotify",
            Url = "https://spclient.wg.spotify.com/signup/public/v1/account",
            Judge = (status, body) =>
            {
                if (status == 200)
                {
                    var region = Extract(body, @"""country_code"":\s*""([A-Za-z]{2})""");
                    return string.IsNullOrEmpty(region) ? ("unknown", null, "no region") : ("supported", region, null);
                }
                return ("unknown", null, $"HTTP {status}");
            },
        },
        new UnlockCheck
        {
            Id = "tiktok", Name = "TikTok",
            Url = "https://www.tiktok.com/",
            Judge = (status, body) =>
            {
                if (status == 200)
                {
                    var region = Extract(body, @"""region"":\s*""([A-Za-z]{2})""");
                    return ("supported", region, null);
                }
                if (status == 403) return ("unsupported", null, "403");
                return ("unknown", null, $"HTTP {status}");
            },
        },
        new UnlockCheck
        {
            Id = "bahamut", Name = "巴哈姆特动画疯",
            Url = "https://ani.gamer.com.tw/ajax/token.php?adsn=",
            Judge = (status, body) =>
            {
                if (status != 200) return ("unknown", null, $"HTTP {status}");
                if (body.Contains("\"sn\":2", StringComparison.Ordinal) || body.Contains("error", StringComparison.OrdinalIgnoreCase))
                    return ("unknown", null, body.Length > 60 ? body[..60] : body);
                return ("supported", "TW", null);
            },
        },
        new UnlockCheck
        {
            Id = "bilibili", Name = "Bilibili 港澳台",
            Url = "https://api.bilibili.com/pgc/player/web/v2/playurl?ep_id=1&cid=1",
            Judge = (status, body) =>
            {
                if (status != 200) return ("unknown", null, $"HTTP {status}");
                if (body.Contains("仅限港澳台", StringComparison.Ordinal) || body.Contains("area limit", StringComparison.OrdinalIgnoreCase))
                    return ("unsupported", null, null);
                if (body.Contains("\"code\":0", StringComparison.Ordinal) || body.Contains(" dash", StringComparison.Ordinal))
                    return ("supported", null, null);
                return ("unknown", null, null);
            },
        },
        new UnlockCheck
        {
            Id = "primevideo", Name = "Prime Video",
            Url = "https://www.primevideo.com/",
            Judge = (status, body) =>
            {
                if (status == 200)
                {
                    var region = Extract(body, @"""currentTerritory"":\s*""([A-Za-z]{2})""");
                    return ("supported", region, null);
                }
                return ("unknown", null, $"HTTP {status}");
            },
        },
    ];

    private static string? Extract(string body, string pattern)
    {
        try
        {
            var match = Regex.Match(body, pattern, RegexOptions.None, TimeSpan.FromSeconds(2));
            return match.Success ? match.Groups[1].Value : null;
        }
        catch
        {
            return null;
        }
    }
}
