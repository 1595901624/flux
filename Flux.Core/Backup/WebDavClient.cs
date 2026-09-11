using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;

namespace Flux.Core.Backup;

/// <summary>WebDAV 备份存储客户端（凭据由调用方经 DPAPI 加密存储，本类不落盘）。</summary>
public sealed class WebDavClient
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly string _baseUrl;
    private readonly string _username;
    private readonly string _password;

    public WebDavClient(string baseUrl, string username, string password)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("WebDAV 地址必须是有效的 HTTP/HTTPS URL", nameof(baseUrl));
        _baseUrl = uri.ToString().TrimEnd('/') + "/";
        _username = username;
        _password = password;
    }

    private void ApplyAuth(HttpRequestMessage request)
    {
        if (string.IsNullOrEmpty(_username) && string.IsNullOrEmpty(_password)) return;
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
    }

    private string DirUrl(string remoteDir) => $"{_baseUrl}{remoteDir.Trim('/')}/";
    private string FileUrl(string remoteDir, string fileName) =>
        $"{_baseUrl}{remoteDir.Trim('/')}/{Uri.EscapeDataString(fileName)}";

    /// <summary>确保远程目录存在（MKCOL，已存在忽略 405）。</summary>
    public async Task EnsureDirectoryAsync(string remoteDir, CancellationToken ct = default)
    {
        foreach (var segment in remoteDir.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var partial = segment;
            using var request = new HttpRequestMessage(new HttpMethod("MKCOL"), DirUrl(partial));
            ApplyAuth(request);
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode && (int)response.StatusCode != 405)
                throw new InvalidOperationException($"创建 WebDAV 目录失败: {response.StatusCode}");
        }
    }

    /// <summary>上传文件。</summary>
    public async Task UploadAsync(string remoteDir, string fileName, byte[] content, CancellationToken ct = default)
    {
        await EnsureDirectoryAsync(remoteDir, ct);
        using var request = new HttpRequestMessage(HttpMethod.Put, FileUrl(remoteDir, fileName))
        {
            Content = new ByteArrayContent(content),
        };
        ApplyAuth(request);
        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"WebDAV 上传失败: {response.StatusCode}");
    }

    /// <summary>下载文件。</summary>
    public async Task<byte[]> DownloadAsync(string remoteDir, string fileName, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, FileUrl(remoteDir, fileName));
        ApplyAuth(request);
        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new FileNotFoundException("WebDAV 上不存在该备份", fileName);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"WebDAV 下载失败: {response.StatusCode}");
        using var ms = new MemoryStream();
        await response.Content.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    /// <summary>列出目录中的 .zip 文件（PROPFIND 解析）。</summary>
    public async Task<IReadOnlyList<(string Name, long Size, DateTime Modified)>> ListAsync(
        string remoteDir, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), DirUrl(remoteDir));
        request.Headers.Add("Depth", "1");
        ApplyAuth(request);
        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return [];
        if (!response.IsSuccessStatusCode && (int)response.StatusCode != 207)
            throw new InvalidOperationException($"WebDAV 列表失败: {response.StatusCode}");

        var xml = await response.Content.ReadAsStringAsync(ct);
        var result = new List<(string, long, DateTime)>();
        XNamespace d = "DAV:";
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch
        {
            return result;
        }
        foreach (var responseEl in doc.Descendants(d + "response"))
        {
            var href = responseEl.Element(d + "href")?.Value ?? "";
            var name = Uri.UnescapeDataString(href.TrimEnd('/').Split('/').LastOrDefault() ?? "");
            if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
            long size = 0;
            var sizeText = responseEl.Descendants(d + "getcontentlength").FirstOrDefault()?.Value;
            long.TryParse(sizeText, out size);
            DateTime modified = default;
            var modifiedText = responseEl.Descendants(d + "getlastmodified").FirstOrDefault()?.Value;
            DateTime.TryParse(modifiedText, out modified);
            result.Add((name, size, modified));
        }
        return result;
    }

    /// <summary>删除文件。</summary>
    public async Task DeleteAsync(string remoteDir, string fileName, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, FileUrl(remoteDir, fileName));
        ApplyAuth(request);
        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
            throw new InvalidOperationException($"WebDAV 删除失败: {response.StatusCode}");
    }

    /// <summary>验证连接与凭据。</summary>
    public async Task<bool> TestConnectionAsync(string remoteDir, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), DirUrl(remoteDir));
            request.Headers.Add("Depth", "0");
            ApplyAuth(request);
            using var response = await _http.SendAsync(request, ct);
            return response.IsSuccessStatusCode || (int)response.StatusCode == 207 || response.StatusCode == System.Net.HttpStatusCode.NotFound;
        }
        catch
        {
            return false;
        }
    }
}

internal static class HttpMethodExtensions
{
    public static HttpMethod Custom(string method) => new(method);
}
