using Flux.Core.Update;
using Xunit;

namespace Flux.Tests;

/// <summary>GitHub Release 清单解析与架构选择测试。</summary>
public class UpdateManifestParserTests
{
    private const string ManifestJson = """
        {
          "tag_name": "v0.4.0",
          "body": "## 更新说明\\n- 修复若干问题",
          "html_url": "https://github.com/1595901624/flux/releases/tag/v0.4.0",
          "assets": [
            { "name": "Flux-0.4.0-win-x64.zip", "browser_download_url": "https://example.com/Flux-0.4.0-win-x64.zip", "digest": "sha256:abcdef1234" },
            { "name": "Flux-0.4.0-win-arm64.zip", "browser_download_url": "https://example.com/Flux-0.4.0-win-arm64.zip" },
            { "name": "checksums.txt", "browser_download_url": "https://example.com/checksums.txt" }
          ]
        }
        """;

    [Fact]
    public void Parse_新版本_选择匹配架构资源()
    {
        var result = UpdateManifestParser.ParseLatest(ManifestJson, "0.3.0", "x64");
        Assert.NotNull(result);
        Assert.True(result!.HasUpdate);
        Assert.Equal("0.4.0", result.LatestVersion);
        Assert.Equal("0.3.0", result.CurrentVersion);
        Assert.NotNull(result.DownloadUrl);
        Assert.Contains("win-x64", result.DownloadUrl);
        Assert.Equal("abcdef1234", result.Sha256);
        Assert.Contains("更新说明", result.ReleaseNotes);
    }

    [Fact]
    public void Parse_arm64_选择arm64资源()
    {
        var result = UpdateManifestParser.ParseLatest(ManifestJson, "0.3.0", "ARM64");
        Assert.NotNull(result);
        Assert.Contains("win-arm64", result!.DownloadUrl);
    }

    [Fact]
    public void Parse_同版本_无更新()
    {
        var result = UpdateManifestParser.ParseLatest(ManifestJson, "0.4.0", "x64");
        Assert.NotNull(result);
        Assert.False(result!.HasUpdate);
        Assert.Null(result.DownloadUrl);
    }

    [Fact]
    public void Parse_旧版本_无更新()
    {
        var result = UpdateManifestParser.ParseLatest(ManifestJson, "1.0.0", "x64");
        Assert.False(result!.HasUpdate);
    }

    [Fact]
    public void Parse_非法JSON_返回null()
    {
        Assert.Null(UpdateManifestParser.ParseLatest("not json {", "0.3.0", "x64"));
        Assert.Null(UpdateManifestParser.ParseLatest("{}", "0.3.0", "x64"));
    }

    [Theory]
    [InlineData("v0.4.0", "0.4.0")]
    [InlineData("V1.2.3-beta", "1.2.3")]
    [InlineData("1.0.0+build.5", "1.0.0")]
    public void NormalizeVersion_去除前缀后缀(string tag, string expected)
    {
        Assert.Equal(expected, UpdateManifestParser.NormalizeVersion(tag));
    }

    [Theory]
    [InlineData("0.4.0", "0.3.9", 1)]
    [InlineData("0.3.0", "0.4.0", -1)]
    [InlineData("1.2.3", "1.2.3", 0)]
    [InlineData("0.10.0", "0.9.0", 1)]
    public void CompareVersions_语义化比较(string a, string b, int expectedSign)
    {
        var result = Math.Sign(UpdateManifestParser.CompareVersions(a, b));
        Assert.Equal(expectedSign, result);
    }

    [Fact]
    public void SelectAsset_无匹配架构_返回null()
    {
        var manifest = System.Text.Json.JsonSerializer.Deserialize(ManifestJson, UpdateJsonContext.Default.UpdateManifest)!;
        Assert.Null(UpdateManifestParser.SelectAsset(manifest, "riscv64"));
    }
}
