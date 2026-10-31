using Flux.Services;
using Xunit;

namespace Flux.Tests;

public class DeepLinkParserTests
{
    [Theory]
    [InlineData("flux")]
    [InlineData("clash")]
    [InlineData(Flux.Core.Compatibility.LegacyCompatibility.ImportScheme)]
    public void SupportsCurrentAndCompatibleImportSchemes(string scheme)
    {
        Assert.True(DeepLinkParser.TryParseInstallUri(
            scheme + "://install-config?url=https%3A%2F%2Fexample.com%2Fsub.yaml",
            out var url, out _));
        Assert.Equal("https://example.com/sub.yaml", url);
    }
    [Fact]
    public void ParsesEncodedInstallUri()
    {
        var ok = DeepLinkParser.TryParseInstallUri(
            "clash://install-config?url=https%3A%2F%2Fexample.com%2Fsub.yaml%3Fa%3D1&name=My+Profile",
            out var url, out var name);

        Assert.True(ok);
        Assert.Equal("https://example.com/sub.yaml?a=1", url);
        Assert.Equal("My Profile", name);
    }

    [Theory]
    [InlineData("clash://install-config?url=ftp%3A%2F%2Fexample.com%2Fa.yaml")]
    [InlineData("clash://install-config?name=missing-url")]
    [InlineData("https://example.com/sub.yaml")]
    public void RejectsUnsupportedOrIncompleteUri(string value)
    {
        Assert.False(DeepLinkParser.TryParseInstallUri(value, out _, out _));
    }
}
