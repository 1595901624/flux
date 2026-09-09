using System.Net;
using System.Net.Http.Headers;
using Microsoft.UI.Dispatching;
using Flux.Models;
using YamlDotNet.RepresentationModel;

namespace Flux.Services;

/// <summary>
/// 订阅下载与管理（对应参考项目 PrfItem::from_url / update_profile）：
/// - 解析 subscription-userinfo（流量/到期）、content-disposition、profile-update-interval
/// - 更新通道：直连 → 走内核 → 走系统代理
/// </summary>
public class SubscriptionService
{
    private const int MaxProfileBytes = 20 * 1024 * 1024;

    public event Action? ProfilesChanged;

    private ConfigService Config => AppServices.Config;

    // ---------- 导入 / 更新 ----------

    /// <summary>从 URL 导入订阅。失败抛异常。</summary>
    public async Task<ProfileItem> ImportAsync(string url, string? name = null)
    {
        var (content, info) = await DownloadWithFallbackAsync(url, new ProfileOption()).ConfigureAwait(false);
        ValidateClashContent(content);
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
        await ApplyCurrentProfileIfRunningAsync(item).ConfigureAwait(false);
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
        await ApplyCurrentProfileIfRunningAsync(item).ConfigureAwait(false);
        return item;
    }

    /// <summary>更新远程订阅（直连 → 内核代理 → 系统代理 自动回退）。</summary>
    public async Task UpdateAsync(ProfileItem item)
    {
        if (item.Type != "remote" || string.IsNullOrEmpty(item.Url))
            throw new InvalidOperationException("仅远程订阅可更新");

        try
        {
            var (content, info) = await DownloadWithFallbackAsync(item.Url, item.Option).ConfigureAwait(false);
            ValidateClashContent(content);

            SaveProfileFile(item, content);
            item.Updated = DateTime.Now;
            if (info.Extra is not null) item.Extra = info.Extra;
            if (info.UpdateIntervalMinutes > 0) item.Option.UpdateInterval = info.UpdateIntervalMinutes;
            Config.SaveProfiles();
            ProfilesChanged?.Invoke();

            if (item.Uid == Config.Profiles.Current)
                await AppServices.Core.ApplyConfigAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("订阅更新失败: " + ex.Message, ex);
        }
    }

    // ---------- 下载实现 ----------

    private async Task<(string Content, DownloadInfo Info)> DownloadWithFallbackAsync(string url, ProfileOption option)
    {
        Exception? last = null;
        var transports = new List<DownloadTransport> { DownloadTransport.Direct };
        if (AppServices.Core.IsRunning) transports.Add(DownloadTransport.FluxCore);
        transports.Add(DownloadTransport.SystemProxy);

        foreach (var transport in transports)
        {
            try { return await DownloadAsync(url, option, transport).ConfigureAwait(false); }
            catch (Exception ex) { last = ex; }
        }
        throw new InvalidOperationException("下载失败: " + last?.Message, last);
    }

    private async Task<(string Content, DownloadInfo Info)> DownloadAsync(
        string url, ProfileOption option, DownloadTransport transport)
    {
        var fixedUrl = FixDirtyUrl(url);
        if (!Uri.TryCreate(fixedUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("订阅地址必须是有效的 HTTP/HTTPS URL");

        using var request = new HttpRequestMessage(HttpMethod.Get, RemoveUserInfo(uri));
        var ua = string.IsNullOrWhiteSpace(option.UserAgent)
            ? "clash-verge/v2.5.2" : option.UserAgent;
        request.Headers.UserAgent.ParseAdd(ua);
        // URL 携带 userinfo 时转换为 Basic 认证
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var authBytes = System.Text.Encoding.UTF8.GetBytes(Uri.UnescapeDataString(uri.UserInfo));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        }

        using var handler = BuildHandler(transport);
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Clamp(option.TimeoutSeconds, 5, 300)));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaxProfileBytes)
            throw new InvalidOperationException("订阅内容超过 20 MiB 限制");

        var bytes = await ReadLimitedAsync(response.Content, cts.Token).ConfigureAwait(false);
        var content = System.Text.Encoding.UTF8.GetString(bytes);
        if (content.Length > 0 && content[0] == '\uFEFF') content = content[1..];

        var info = new DownloadInfo();
        if (response.Headers.TryGetValues("subscription-userinfo", out var uiValues))
            info.Extra = ParseUserinfo(string.Join(";", uiValues));
        if (response.Headers.TryGetValues("profile-update-interval", out var ivValues) &&
            double.TryParse(ivValues.FirstOrDefault(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var hours))
            info.UpdateIntervalMinutes = (int)Math.Round(hours * 60);
        if (response.Content.Headers.ContentDisposition?.FileNameStar is { } fnStar)
            info.Name = fnStar;
        else if (response.Content.Headers.ContentDisposition?.FileName is { } fn)
            info.Name = fn.Trim('"');
        if (string.IsNullOrWhiteSpace(info.Name))
        {
            info.Name = uri.Segments.Length > 0
                ? Uri.UnescapeDataString(uri.Segments[^1].TrimEnd('/'))
                : "subscription";
        }
        info.Name = Path.GetFileNameWithoutExtension(info.Name);
        return (content, info);
    }

    private static HttpClientHandler BuildHandler(DownloadTransport transport)
    {
        return transport switch
        {
            DownloadTransport.Direct => new HttpClientHandler { UseProxy = false },
            DownloadTransport.FluxCore => new HttpClientHandler
            {
                Proxy = new WebProxy($"http://127.0.0.1:{AppServices.Config.MixedPort}"),
                UseProxy = true,
            },
            _ => new HttpClientHandler { Proxy = HttpClient.DefaultProxy, UseProxy = true },
        };
    }

    private static Uri RemoveUserInfo(Uri uri) => string.IsNullOrEmpty(uri.UserInfo)
        ? uri
        : new UriBuilder(uri) { UserName = "", Password = "" }.Uri;

    private static async Task<byte[]> ReadLimitedAsync(HttpContent content, CancellationToken ct)
    {
        await using var input = await content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await input.ReadAsync(buffer, ct).ConfigureAwait(false);
            if (read == 0) break;
            if (output.Length + read > MaxProfileBytes)
                throw new InvalidOperationException("订阅内容超过 20 MiB 限制");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
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

    internal static void ValidateClashContent(string content)
        => ProfileContentValidator.Validate(content);

    /// <summary>
    /// 首次导入会把该项设为当前订阅。内核可能已经用空配置启动，必须立即重载，
    /// 否则导入虽显示成功，<c>/proxies</c> 仍不会出现新订阅的代理组。
    /// </summary>
    private async Task ApplyCurrentProfileIfRunningAsync(ProfileItem item)
    {
        if (item.Uid != Config.Profiles.Current || !AppServices.Core.IsRunning) return;

        if (!await AppServices.Core.ApplyConfigAsync().ConfigureAwait(false))
            LogService.App($"导入后应用订阅失败: {item.Name}", "warn");
    }

    // ---------- 文件 / 列表操作 ----------

    private void SaveProfileFile(ProfileItem item, string content)
    {
        if (string.IsNullOrEmpty(item.File))
        {
            item.File = $"{(item.Type == "remote" ? "R" : "L")}{item.Uid}.yaml";
        }
        ConfigService.WriteAllTextAtomic(item.FilePath, content);
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
        if (Config.Profiles.Current != uid)
        {
            Config.Profiles.Current = uid;
            Config.SaveProfiles();
            ProfilesChanged?.Invoke();
        }

        // 重复选择当前项也应重新应用，便于恢复首次导入或上次热重载失败后的空内核。
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
    private readonly SemaphoreSlim _autoUpdateLock = new(1, 1);

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
        if (!await _autoUpdateLock.WaitAsync(0)) return;
        try
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
        finally { _autoUpdateLock.Release(); }
    }

    private class DownloadInfo
    {
        public string Name { get; set; } = "";
        public ProfileExtra? Extra { get; set; }
        public int UpdateIntervalMinutes { get; set; }
    }

    private enum DownloadTransport { Direct, FluxCore, SystemProxy }
}
