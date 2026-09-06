using System.Net;
using System.Net.Http.Headers;
using Microsoft.UI.Dispatching;
using Vxn.Models;
using YamlDotNet.RepresentationModel;

namespace Vxn.Services;

/// <summary>
/// 订阅下载与管理（对应参考项目 PrfItem::from_url / update_profile）：
/// - 解析 subscription-userinfo（流量/到期）、content-disposition、profile-update-interval
/// - 更新通道：直连 → 走内核 → 走系统代理
/// </summary>
public class SubscriptionService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    static SubscriptionService()
    {
        ServicePointManager.DefaultConnectionLimit = 32;
    }

    public event Action? ProfilesChanged;

    private ConfigService Config => AppServices.Config;

    // ---------- 导入 / 更新 ----------

    /// <summary>从 URL 导入订阅。失败抛异常。</summary>
    public async Task<ProfileItem> ImportAsync(string url, string? name = null)
    {
        var (content, info) = await DownloadAsync(url, new ProfileOption()).ConfigureAwait(false);
        var item = new ProfileItem
        {
            Uid = NewUid(),
            Type = "remote",
            Name = string.IsNullOrWhiteSpace(name) ? info.Name : name,
            Url = url,
            File = "",
            Updated = DateTime.Now,
            Extra = info.Extra,
        };
        item.Option.UpdateInterval = info.UpdateIntervalMinutes;
        SaveProfileFile(item, content);
        item.File ??= "";

        Config.Profiles.Items.Add(item);
        if (Config.Profiles.Current is null)
            Config.Profiles.Current = item.Uid;
        Config.SaveProfiles();
        ProfilesChanged?.Invoke();
        return item;
    }

    /// <summary>导入本地 YAML 文件。</summary>
    public async Task<ProfileItem> ImportLocalAsync(string filePath, string? name = null)
    {
        var content = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        if (YamlHelper.ParseMapping(content) is null)
            throw new InvalidOperationException("文件不是有效的 YAML 配置");
        ValidateClashContent(content);

        var item = new ProfileItem
        {
            Uid = NewUid(),
            Type = "local",
            Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(filePath) : name,
            File = "",
            Updated = DateTime.Now,
        };
        SaveProfileFile(item, content);
        Config.Profiles.Items.Add(item);
        if (Config.Profiles.Current is null)
            Config.Profiles.Current = item.Uid;
        Config.SaveProfiles();
        ProfilesChanged?.Invoke();
        return item;
    }

    /// <summary>更新远程订阅（直连 → 内核代理 → 系统代理 自动回退）。</summary>
    public async Task UpdateAsync(ProfileItem item)
    {
        if (item.Type != "remote" || string.IsNullOrEmpty(item.Url))
            throw new InvalidOperationException("仅远程订阅可更新");

        Exception? last = null;
        foreach (var useProxy in new[] { false, true })
        {
            try
            {
                var option = new ProfileOption
                {
                    UserAgent = item.Option.UserAgent,
                    TimeoutSeconds = item.Option.TimeoutSeconds,
                    SelfProxy = useProxy && !item.Option.WithProxy,
                    WithProxy = useProxy && item.Option.WithProxy,
                };
                var (content, info) = await DownloadAsync(item.Url, option).ConfigureAwait(false);
                ValidateClashContent(content);

                SaveProfileFile(item, content);
                item.Updated = DateTime.Now;
                if (info.Extra is not null) item.Extra = info.Extra;
                if (info.UpdateIntervalMinutes > 0) item.Option.UpdateInterval = info.UpdateIntervalMinutes;
                Config.SaveProfiles();
                ProfilesChanged?.Invoke();

                if (item.Uid == Config.Profiles.Current)
                    await AppServices.Core.ApplyConfigAsync();
                return;
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }
        throw new InvalidOperationException("订阅更新失败: " + last?.Message);
    }

    // ---------- 下载实现 ----------

    private async Task<(string Content, DownloadInfo Info)> DownloadAsync(string url, ProfileOption option)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, FixDirtyUrl(url));
        var ua = string.IsNullOrWhiteSpace(option.UserAgent)
            ? "clash-verge/v2.5.2" : option.UserAgent;
        request.Headers.UserAgent.ParseAdd(ua);
        // URL 携带 userinfo 时转换为 Basic 认证
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.UserInfo))
        {
            var authBytes = System.Text.Encoding.UTF8.GetBytes(uri.UserInfo);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(5, option.TimeoutSeconds)));

        HttpResponseMessage? response = null;
        Exception? last = null;
        foreach (var handler in new[] { (HttpMessageHandler?)null, BuildProxyHandler(option) })
        {
            if (handler == null && option.SelfProxy) continue; // 首次直连尝试
            try
            {
                using var client = handler is null ? Http : new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(Math.Max(5, option.TimeoutSeconds)) };
                response = await client.SendAsync(request.Clone(), cts.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode) break;
                last = new HttpRequestException($"HTTP {(int)response.StatusCode}");
                response.Dispose();
                response = null;
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        if (response is null) throw new InvalidOperationException("下载失败: " + last?.Message);

        using (response)
        {
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false);
            var content = System.Text.Encoding.UTF8.GetString(bytes);
            if (content.Length > 0 && content[0] == '\uFEFF')
                content = content[1..];

            var info = new DownloadInfo();
            // subscription-userinfo
            if (response.Headers.TryGetValues("subscription-userinfo", out var uiValues))
            {
                var ui = string.Join(";", uiValues);
                info.Extra = ParseUserinfo(ui);
            }
            // profile-update-interval（小时）
            if (response.Headers.TryGetValues("profile-update-interval", out var ivValues) &&
                double.TryParse(ivValues.FirstOrDefault(), out var hours))
            {
                info.UpdateIntervalMinutes = (int)Math.Round(hours * 60);
            }
            // 文件名
            if (response.Content.Headers.ContentDisposition?.FileNameStar is { } fnStar)
                info.Name = fnStar;
            else if (response.Content.Headers.ContentDisposition?.FileName is { } fn)
                info.Name = fn.Trim('"');
            if (string.IsNullOrWhiteSpace(info.Name))
                info.Name = Uri.TryCreate(url, UriKind.Absolute, out var u2) && u2.Segments.Length > 0
                    ? Uri.UnescapeDataString(u2.Segments[^1].TrimEnd('/'))
                    : "subscription";
            info.Name = Path.GetFileNameWithoutExtension(info.Name);
            return (content, info);
        }
    }

    private static HttpClientHandler? BuildProxyHandler(ProfileOption option)
    {
        try
        {
            if (option.SelfProxy)
            {
                return new HttpClientHandler
                {
                    Proxy = new WebProxy($"http://127.0.0.1:{AppServices.Config.MixedPort}"),
                    UseProxy = true,
                };
            }
            if (option.WithProxy)
            {
                return new HttpClientHandler
                {
                    Proxy = HttpClient.DefaultProxy,
                    UseProxy = true,
                };
            }
        }
        catch { }
        return null;
    }

    /// <summary>修复误把查询参数接在路径里的 URL。</summary>
    private static string FixDirtyUrl(string url) =>
        url.Contains("&params=") && !url.Contains('?') ? url.Replace("&params=", "?params=", StringComparison.Ordinal) : url;

    private static ProfileExtra ParseUserinfo(string ui)
    {
        var extra = new ProfileExtra();
        foreach (var part in ui.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            if (!long.TryParse(kv[1], out var value)) continue;
            switch (kv[0].Trim().ToLowerInvariant())
            {
                case "upload": extra.Upload = value; break;
                case "download": extra.Download = value; break;
                case "total": extra.Total = value; break;
                case "expire": extra.Expire = value; break;
            }
        }
        return extra;
    }

    private static void ValidateClashContent(string content)
    {
        var mapping = YamlHelper.ParseMapping(content)
            ?? throw new InvalidOperationException("内容不是有效的 YAML");
        var hasProxies = mapping.Children.ContainsKey(new YamlScalarNode("proxies"));
        var hasProviders = mapping.Children.ContainsKey(new YamlScalarNode("proxy-providers"));
        if (!hasProxies && !hasProviders)
            throw new InvalidOperationException("配置中缺少 proxies / proxy-providers，不是有效的 Clash 订阅");
    }

    // ---------- 文件 / 列表操作 ----------

    private void SaveProfileFile(ProfileItem item, string content)
    {
        if (string.IsNullOrEmpty(item.File))
        {
            item.File = $"{(item.Type == "remote" ? "R" : "L")}{item.Uid}.yaml";
        }
        File.WriteAllText(Path.Combine(Paths.ProfilesDir, item.File), content);
    }

    public static string NewUid() => Guid.NewGuid().ToString("N")[..12];

    public async Task DeleteAsync(ProfileItem item)
    {
        Config.Profiles.Items.Remove(item);
        if (Config.Profiles.Current == item.Uid)
            Config.Profiles.Current = Config.Profiles.Items.FirstOrDefault()?.Uid;
        Config.SaveProfiles();
        try
        {
            if (!string.IsNullOrEmpty(item.File) && File.Exists(item.FilePath))
                File.Delete(item.FilePath);
        }
        catch { }
        ProfilesChanged?.Invoke();
        await AppServices.Core.ApplyConfigAsync();
    }

    /// <summary>切换当前订阅并应用。</summary>
    public async Task SelectAsync(string uid)
    {
        if (Config.Profiles.Current == uid) return;
        Config.Profiles.Current = uid;
        Config.SaveProfiles();
        ProfilesChanged?.Invoke();
        await AppServices.Core.ApplyConfigAsync();
    }

    public void Reorder(string activeUid, string overUid)
    {
        var items = Config.Profiles.Items;
        var active = items.FirstOrDefault(i => i.Uid == activeUid);
        var over = items.FirstOrDefault(i => i.Uid == overUid);
        if (active is null || over is null || active == over) return;
        items.Remove(active);
        items.Insert(items.IndexOf(over), active);
        Config.SaveProfiles();
        ProfilesChanged?.Invoke();
    }

    // ---------- 自动更新定时 ----------

    private DispatcherQueueTimer? _timer;

    public void StartAutoUpdateTimer()
    {
        var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _timer = queue.CreateTimer();
        _timer.Interval = TimeSpan.FromMinutes(30);
        _timer.Tick += async (_, _) => await UpdateDueAsync();
        _timer.Start();
    }

    private async Task UpdateDueAsync()
    {
        foreach (var item in Config.Profiles.Items.ToList())
        {
            if (item.Type != "remote" || item.Option.UpdateInterval <= 0) continue;
            if ((DateTime.Now - item.Updated).TotalMinutes < item.Option.UpdateInterval) continue;
            try
            {
                await UpdateAsync(item);
                LogService.App($"订阅自动更新成功: {item.Name}");
            }
            catch (Exception ex)
            {
                LogService.App($"订阅自动更新失败: {item.Name}: {ex.Message}", "warn");
            }
        }
    }

    private class DownloadInfo
    {
        public string Name { get; set; } = "";
        public ProfileExtra? Extra { get; set; }
        public int UpdateIntervalMinutes { get; set; }
    }
}

/// <summary>HttpRequestMessage 不支持直接克隆，这里手动复制必要部分。</summary>
internal static class HttpRequestMessageExtensions
{
    public static HttpRequestMessage Clone(this HttpRequestMessage req)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri)
        {
            Version = req.Version,
        };
        foreach (var h in req.Headers)
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);
        return clone;
    }
}
