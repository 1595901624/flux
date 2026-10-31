using Flux.Core.Config;
using Flux.Core.Contracts;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using Xunit;

namespace Flux.Tests;

public class SettingsConfigEditorTests
{
    private static YamlMappingNode Parse(string yaml) => YamlOps.ParseMapping(yaml)!;
    private static string Serialize(YamlMappingNode config) => new Serializer().Serialize(config);

    [Theory]
    [InlineData("any:53; tcp://any:53")]
    [InlineData("any:53, tcp://any:53")]
    [InlineData("any:53\ntcp://any:53")]
    public async Task TunHijackRoundTripsAsAList(string input)
    {
        var config = Parse("tun:\n  enable: false\n  auto-route: true\n  dns-hijack: any:53");
        Assert.Equal("any:53", SettingsConfigEditor.ReadDnsHijack(config));
        Assert.True(await SettingsConfigEditor.ApplySectionAsync(config, "tun",
            tun => SettingsConfigEditor.EditTun(tun, "system", input), () => { }, () => Task.FromResult(true)));
        var tun = (YamlMappingNode)config.Children[new YamlScalarNode("tun")];
        var hijack = Assert.IsType<YamlSequenceNode>(tun.Children[new YamlScalarNode("dns-hijack")]);
        Assert.Equal(new[] { "any:53", "tcp://any:53" }, hijack.Children.Select(n => ((YamlScalarNode)n).Value));
        Assert.Equal("false", YamlOps.GetScalar(config, "tun", "enable"));
        Assert.Equal("true", YamlOps.GetScalar(config, "tun", "auto-route"));
        Assert.Equal("system", YamlOps.GetScalar(config, "tun", "stack"));
        Assert.Equal("any:53; tcp://any:53", SettingsConfigEditor.ReadDnsHijack(Parse(Serialize(config))));
    }

    [Fact]
    public async Task DnsApplyEnablesDnsEvenWithoutTun()
    {
        var config = Parse("dns:\n  enable: false\n  nameserver: [https://doh.pub/dns-query]");
        var persisted = "";
        Assert.True(await SettingsConfigEditor.ApplySectionAsync(config, "dns",
            dns => SettingsConfigEditor.EditDns(dns, "fake-ip", "198.18.0.1/16"),
            () => persisted = Serialize(config), () => Task.FromResult(true)));
        var result = new RuntimeConfigBuilder().Build(new RuntimeConfigInput { ClashBase = Parse(persisted), EnableTun = false });
        Assert.True(result.Success);
        Assert.Equal("true", YamlOps.GetScalar(result.Value!.Config, "dns", "enable"));
        Assert.Equal("fake-ip", YamlOps.GetScalar(result.Value.Config, "dns", "enhanced-mode"));
        Assert.Equal("198.18.0.1/16", YamlOps.GetScalar(result.Value.Config, "dns", "fake-ip-range"));
        Assert.Contains("https://doh.pub/dns-query", persisted);
    }

    [Theory]
    [InlineData("dns")]
    [InlineData("tun")]
    public async Task RejectedApplyRestoresPersistedAndInMemorySection(string section)
    {
        var config = Parse($"{section}:\n  enable: false\n  custom: [one, two]\nmixed-port: 7897");
        var oldSection = Serialize((YamlMappingNode)config.Children[new YamlScalarNode(section)]);
        var persisted = "";
        var applied = await SettingsConfigEditor.ApplySectionAsync(config, section,
            map => YamlOps.SetValue(map, "enable", "true"), () => persisted = Serialize(config),
            () =>
            {
                Assert.Equal("true", YamlOps.GetScalar(Parse(persisted), section, "enable"));
                YamlOps.SetValue(config, "mixed-port", "8888");
                return Task.FromResult(false);
            });
        Assert.False(applied);
        Assert.Equal(oldSection, Serialize((YamlMappingNode)Parse(persisted).Children[new YamlScalarNode(section)]));
        Assert.Equal("false", YamlOps.GetScalar(config, section, "enable"));
        Assert.Equal("8888", YamlOps.GetScalar(Parse(persisted), "mixed-port"));
    }

    [Fact]
    public async Task ExceptionRestoresMissingSectionAndPropagates()
    {
        var config = Parse("mixed-port: 7897");
        var original = Serialize(config);
        var persisted = "";
        await Assert.ThrowsAsync<InvalidOperationException>(() => SettingsConfigEditor.ApplySectionAsync(config, "dns",
            dns => SettingsConfigEditor.EditDns(dns, "fake-ip", ""), () => persisted = Serialize(config),
            () => throw new InvalidOperationException("core failure")));
        Assert.Equal(original, Serialize(config));
        Assert.Equal(original, persisted);
    }

    [Fact]
    public async Task FailedSaveRestoresOriginalWithoutCallingCore()
    {
        var config = Parse("tun:\n  stack: gvisor");
        var original = Serialize(config);
        var saves = 0;
        var coreCalled = false;
        await Assert.ThrowsAsync<IOException>(() => SettingsConfigEditor.ApplySectionAsync(config, "tun",
            tun => SettingsConfigEditor.EditTun(tun, "system", ""),
            () => { if (++saves == 1) throw new IOException("disk failure"); },
            () => { coreCalled = true; return Task.FromResult(true); }));
        Assert.False(coreCalled);
        Assert.Equal(original, Serialize(config));
    }
}
