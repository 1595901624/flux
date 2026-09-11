using Flux.Core.Proxy;
using Flux.Services;
using Xunit;

namespace Flux.Tests;

public class PacScriptGeneratorTests
{
    [Fact]
    public void Generate_默认返回代理()
    {
        var pac = PacScriptGenerator.Generate(7897, []);
        Assert.Contains("return 'PROXY 127.0.0.1:7897';", pac);
        Assert.Contains("function FindProxyForURL(url, host)", pac);
    }

    [Fact]
    public void Generate_localhost始终直连()
    {
        var pac = PacScriptGenerator.Generate(7897, []);
        Assert.Contains("host === 'localhost'", pac);
        Assert.Contains("host === '127.0.0.1'", pac);
    }

    [Fact]
    public void Generate_通配条目_转shExpMatch()
    {
        var pac = PacScriptGenerator.Generate(7897, ["192.168.*", "10.*"]);
        Assert.Contains("shExpMatch(host, '192.168.*')", pac);
        Assert.Contains("shExpMatch(host, '10.*')", pac);
    }

    [Fact]
    public void Generate_CIDR条目_转isInNet()
    {
        var pac = PacScriptGenerator.Generate(7897, ["10.0.0.0/8"]);
        Assert.Contains("isInNet(host, '10.0.0.0', '8')", pac);
    }

    [Fact]
    public void Generate_纯域名_匹配自身与子域()
    {
        var pac = PacScriptGenerator.Generate(7897, ["example.com"]);
        Assert.Contains("host === 'example.com'", pac);
        Assert.Contains("shExpMatch(host, '*.example.com')", pac);
    }

    [Fact]
    public void Generate_忽略本地标记与空项()
    {
        var pac = PacScriptGenerator.Generate(7897, ["<local>", "", "  "]);
        Assert.DoesNotContain("<local>", pac);
    }

    [Fact]
    public void Generate_单引号被转义()
    {
        var pac = PacScriptGenerator.Generate(7897, ["it's.example.com"]);
        Assert.Contains(@"it\'s.example.com", pac);
    }
}

public class SystemProxyOwnershipPacTests
{
    [Fact]
    public void IsOwnedPac_URL匹配_算Flux所有()
    {
        var url = "file:///C:/Users/x/AppData/Roaming/flux/proxy.pac";
        Assert.True(SystemProxyOwnership.IsOwnedPac(true, url, url));
    }

    [Fact]
    public void IsOwnedPac_URL不同_不算()
    {
        Assert.False(SystemProxyOwnership.IsOwnedPac(true, "file:///other.pac", "file:///mine.pac"));
    }

    [Fact]
    public void IsOwnedPac_未启用PAC_不算()
    {
        Assert.False(SystemProxyOwnership.IsOwnedPac(false, "file:///mine.pac", "file:///mine.pac"));
    }

    [Fact]
    public void IsOwnedPac_FluxUrl为空_不算()
    {
        Assert.False(SystemProxyOwnership.IsOwnedPac(true, "file:///mine.pac", ""));
    }
}
