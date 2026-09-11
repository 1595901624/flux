using System.Text.Json;
using System.Text.Json.Serialization;
using Flux.Core.Contracts;

namespace Flux.Core.Update;

/// <summary>GitHub Release 清单解析与架构选择（纯逻辑，可单测）。</summary>
public static class UpdateManifestParser
{
    /// <summary>从 GitHub API releases/latest JSON 中选择匹配架构的更新。</summary>
    public static UpdateCheckResult? ParseLatest(string json, string currentVersion, string architecture)
    {
        UpdateManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize(json, UpdateJsonContext.Default.UpdateManifest);
        }
        catch (JsonException)
        {
            return null;
        }
        if (manifest is null || string.IsNullOrEmpty(manifest.TagName))
            return null;

        var latest = NormalizeVersion(manifest.TagName);
        var current = NormalizeVersion(currentVersion);
        var hasUpdate = CompareVersions(latest, current) > 0;
        var asset = SelectAsset(manifest, architecture);
        if (!hasUpdate)
            return new UpdateCheckResult(false, current, latest, manifest.Body, manifest.HtmlUrl, null, null, architecture);

        return new UpdateCheckResult(
            HasUpdate: true,
            CurrentVersion: current,
            LatestVersion: latest,
            ReleaseNotes: manifest.Body,
            ReleaseUrl: manifest.HtmlUrl,
            DownloadUrl: asset?.BrowserDownloadUrl,
            Sha256: asset?.Digest?.StartsWith("sha256:", StringComparison.Ordinal) == true
                ? asset.Digest["sha256:".Length..]
                : null,
            TargetArchitecture: asset?.Name);
    }

    /// <summary>选择与架构匹配的资源名（win-x64 / win-arm64 / win-x86，MSIX 或 ZIP 均可）。</summary>
    public static UpdateAsset? SelectAsset(UpdateManifest manifest, string architecture)
    {
        foreach (var asset in manifest.Assets ?? [])
        {
            var name = asset.Name?.ToLowerInvariant() ?? "";
            var archKey = architecture.ToLowerInvariant() switch
            {
                "x64" or "amd64" => "x64",
                "arm64" => "arm64",
                "x86" => "x86",
                _ => architecture.ToLowerInvariant(),
            };
            if (name.Contains(archKey, StringComparison.Ordinal) &&
                (name.EndsWith(".zip") || name.EndsWith(".msix") || name.EndsWith(".appx")))
                return asset;
        }
        return null;
    }

    /// <summary>去掉 tag 前缀 v/V 并取前三个数字段。</summary>
    public static string NormalizeVersion(string tag)
    {
        var value = tag.TrimStart('v', 'V');
        var plus = value.IndexOf('+');
        if (plus >= 0) value = value[..plus];
        var dash = value.IndexOf('-');
        if (dash >= 0) value = value[..dash];
        return value.TrimStart('.');
    }

    /// <summary>比较语义化版本；返回 >0 表示 a 更新。</summary>
    public static int CompareVersions(string a, string b)
    {
        var pa = ParseParts(a);
        var pb = ParseParts(b);
        for (var i = 0; i < 3; i++)
        {
            if (pa[i] != pb[i]) return pa[i].CompareTo(pb[i]);
        }
        return 0;
    }

    private static int[] ParseParts(string version)
    {
        var parts = version.Split('.');
        var result = new int[3];
        for (var i = 0; i < 3 && i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out var n)) n = 0;
            result[i] = n;
        }
        return result;
    }
}

public sealed class UpdateManifest
{
    public string TagName { get; set; } = "";
    public string? Body { get; set; }
    public string? HtmlUrl { get; set; }
    public UpdateAsset[]? Assets { get; set; }
}

public sealed class UpdateAsset
{
    public string Name { get; set; } = "";
    public string BrowserDownloadUrl { get; set; } = "";
    /// <summary>GitHub API 的 digest 字段（如 "sha256:abcdef"）。</summary>
    public string? Digest { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(UpdateManifest))]
public sealed partial class UpdateJsonContext : JsonSerializerContext
{
}
