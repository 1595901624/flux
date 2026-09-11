using Flux.Core.Config;
using Flux.Core.Contracts;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using Xunit;

namespace Flux.Tests;

/// <summary>运行时配置合成流水线测试：Merge 顺序、Seq 语义、控制面保护、DNS/TUN、链式代理。</summary>
public class RuntimeConfigBuilderTests
{
    private static YamlMappingNode ParseYaml(string yaml) =>
        YamlOps.ParseMapping(yaml) ?? throw new InvalidOperationException("测试 YAML 非法");

    private static string Serialize(YamlMappingNode node) => new Serializer().Serialize(node);

    private static RuntimeConfigInput Input(
        string? profile = null,
        string? clashBase = null,
        List<ChainItemWithContent>? chain = null,
        bool tun = false,
        bool dnsSettings = false,
        string? dnsOverride = null,
        Dictionary<string, string>? chainProxy = null)
        => new()
        {
            Profile = profile is null ? null : ParseYaml(profile),
            ClashBase = ParseYaml(clashBase ?? BaseYaml),
            ChainItems = chain ?? [],
            EnableTun = tun,
            EnableDnsSettings = dnsSettings,
            DnsOverride = dnsOverride is null ? null : ParseYaml(dnsOverride),
            ChainProxy = chainProxy,
        };

    private const string BaseYaml = """
        mode: rule
        mixed-port: 7897
        external-controller: 127.0.0.1:9097
        secret: app-secret
        allow-lan: false
        log-level: info
        """;

    private const string ProfileYaml = """
        mixed-port: 12345
        secret: profile-secret
        proxies:
          - name: node-a
            type: ss
            server: a.example.com
            port: 8388
          - name: node-b
            type: vmess
            server: b.example.com
            port: 443
        proxy-groups:
          - name: Group1
            type: select
            proxies:
              - node-a
              - node-b
        rules:
          - MATCH,Group1
        """;

    [Fact]
    public void Build_基础覆盖_订阅为底_控制面恢复()
    {
        var builder = new RuntimeConfigBuilder();
        var result = builder.Build(Input(profile: ProfileYaml));

        Assert.True(result.Success);
        var config = result.Value!.Config;
        Assert.Equal("7897", YamlOps.GetScalar(config, "mixed-port"));   // 控制面恢复
        Assert.Equal("app-secret", YamlOps.GetScalar(config, "secret")); // 订阅的 secret 被覆盖
        Assert.Equal("rule", YamlOps.GetScalar(config, "mode"));
    }

    [Fact]
    public void Build_Merge顺序_全局先于订阅()
    {
        var chain = new List<ChainItemWithContent>
        {
            new(new ChainItem(ChainType.Merge, "Global", "全局 Merge", "Merge.yaml", IsGlobal: true),
                "port-marker: global\nmode-override: no"),
            new(new ChainItem(ChainType.Merge, "p1", "订阅 Merge", "m1.yaml", IsGlobal: false),
                "port-marker: profile"),
        };

        var builder = new RuntimeConfigBuilder();
        var result = builder.Build(Input(profile: ProfileYaml, chain: chain));

        Assert.True(result.Success);
        Assert.Equal("profile", YamlOps.GetScalar(result.Value!.Config, "port-marker"));
    }

    [Fact]
    public void Build_Merge_深合并且null删除()
    {
        var chain = new List<ChainItemWithContent>
        {
            new(new ChainItem(ChainType.Merge, "p1", "订阅 Merge", "m1.yaml", false), """
                dns:
                  enable: true
                  ipv6: false
                rule-providers: ~
                """),
        };
        var profile = """
            dns:
              enable: false
              listen: 0.0.0.0:53
            rule-providers:
              provider-a:
                type: http
            """;

        var builder = new RuntimeConfigBuilder();
        var result = builder.Build(Input(profile: profile, chain: chain));

        Assert.True(result.Success);
        var config = result.Value!.Config;
        Assert.Equal("true", YamlOps.GetScalar(config, "dns", "enable"));
        Assert.Equal("0.0.0.0:53", YamlOps.GetScalar(config, "dns", "listen")); // 未覆盖的键保留
        Assert.Null(YamlOps.GetScalar(config, "rule-providers"));            // null 删除
        Assert.Equal("false", YamlOps.GetScalar(config, "dns", "ipv6"));
    }

    [Fact]
    public void Build_Script_修改配置并收集日志()
    {
        var chain = new List<ChainItemWithContent>
        {
            new(new ChainItem(ChainType.Script, "Global", "全局脚本", "Script.js", true), """
                function main(config, profileName) {
                  console.log("处理:", profileName);
                  config["script-marker"] = "applied";
                  config["mixed-port"] = 9999; // 会被控制面恢复覆盖
                  return config;
                }
                """),
        };

        var builder = new RuntimeConfigBuilder();
        var result = builder.Build(Input(profile: ProfileYaml, chain: chain));

        Assert.True(result.Success);
        var config = result.Value!.Config;
        Assert.Equal("applied", YamlOps.GetScalar(config, "script-marker"));
        Assert.Equal("7897", YamlOps.GetScalar(config, "mixed-port"));
        Assert.Contains(result.Value!.ChainLogs, l => l.Level == "info" && l.Message.Contains("处理"));
    }

    [Fact]
    public void Build_Script异常_返回步骤与文件名()
    {
        var chain = new List<ChainItemWithContent>
        {
            new(new ChainItem(ChainType.Script, "p1", "坏脚本", "bad.js", false), """
                function main(config) {
                  throw new Error("boom");
                }
                """),
        };

        var builder = new RuntimeConfigBuilder();
        var result = builder.Build(Input(profile: ProfileYaml, chain: chain));

        Assert.False(result.Success);
        Assert.Equal("Script", result.Error!.Step);
        Assert.Equal("bad.js", result.Error.File);
        Assert.Contains("boom", result.Error.Message);
    }

    [Fact]
    public void Build_Script缺少Main_报错()
    {
        var chain = new List<ChainItemWithContent>
        {
            new(new ChainItem(ChainType.Script, "p1", "无Main", "nom main.js", false), "var x = 1;"),
        };

        var builder = new RuntimeConfigBuilder();
        var result = builder.Build(Input(profile: ProfileYaml, chain: chain));

        Assert.False(result.Success);
        Assert.Equal("script_missing_main", result.Error!.Code);
    }

    [Fact]
    public void Build_Script无CLR访问()
    {
        var chain = new List<ChainItemWithContent>
        {
            new(new ChainItem(ChainType.Script, "p1", "越权", "clr.js", false), """
                function main(config) {
                  var f = System.IO.File; // 不应存在 System 对象
                  return config;
                }
                """),
        };

        var builder = new RuntimeConfigBuilder();
        var result = builder.Build(Input(profile: ProfileYaml, chain: chain));

        Assert.False(result.Success);
    }

    [Fact]
    public void Build_ScriptTimeout_拒绝死循环()
    {
        var chain = new List<ChainItemWithContent>
        {
            new(new ChainItem(ChainType.Script, "p1", "死循环", "loop.js", false), """
                function main(config) {
                  while (true) { }
                }
                """),
        };

        var builder = new RuntimeConfigBuilder();
        var result = builder.Build(Input(profile: ProfileYaml, chain: chain));

        Assert.False(result.Success);
        Assert.Contains(result.Error!.Code, new[] { "script_timeout", "script_error" });
    }

    [Fact]
    public void Build_Seq_规则前后插入与删除()
    {
        var chain = new List<ChainItemWithContent>
        {
            new(new ChainItem(ChainType.Rules, "p1", "规则增强", "r1.yaml", false), """
                prepend:
                  - DOMAIN,example.org,Group1
                append:
                  - MATCH,Group1
                delete:
                  - MATCH,Group1
                """),
        };

        var builder = new RuntimeConfigBuilder();
        var result = builder.Build(Input(profile: ProfileYaml, chain: chain));

        Assert.True(result.Success);
        var yaml = Serialize(result.Value!.Config);
        Assert.Contains("DOMAIN,example.org,Group1", yaml);
        // 原有 MATCH 在 delete 列表中被移除，append 又追加一个
        var rules = result.Value!.Config.Children[new YamlScalarNode("rules")] as YamlSequenceNode;
        Assert.Equal(2, rules!.Children.Count);
    }

    [Fact]
    public void Build_Seq_节点增删同步第一个Selector组()
    {
        var chain = new List<ChainItemWithContent>
        {
            new(new ChainItem(ChainType.Proxies, "p1", "节点增强", "p1.yaml", false), """
                prepend:
                  - name: node-c
                    type: ss
                    server: c.example.com
                    port: 8389
                delete:
                  - node-b
                """),
        };

        var builder = new RuntimeConfigBuilder();
        var result = builder.Build(Input(profile: ProfileYaml, chain: chain));

        Assert.True(result.Success);
        var groups = result.Value!.Config.Children[new YamlScalarNode("proxy-groups")] as YamlSequenceNode;
        var group1 = groups!.Children.OfType<YamlMappingNode>().First(g => YamlOps.GetScalar(g, "name") == "Group1");
        var members = (group1.Children[new YamlScalarNode("proxies")] as YamlSequenceNode)!
            .Children.OfType<YamlScalarNode>().Select(n => n.Value).ToList();
        Assert.Contains("node-c", members);
        Assert.DoesNotContain("node-b", members);
    }

    [Fact]
    public void Build_Tun启用_补齐Dns默认值()
    {
        var builder = new RuntimeConfigBuilder();
        var result = builder.Build(Input(profile: ProfileYaml, tun: true));

        Assert.True(result.Success);
        var config = result.Value!.Config;
        Assert.Equal("true", YamlOps.GetScalar(config, "tun", "enable"));
        Assert.Equal("true", YamlOps.GetScalar(config, "dns", "enable"));
        Assert.Equal("fake-ip", YamlOps.GetScalar(config, "dns", "enhanced-mode"));
        Assert.Equal("198.18.0.1/16", YamlOps.GetScalar(config, "dns", "fake-ip-range"));
    }

    [Fact]
    public void Build_Tun关闭_不强制Dns()
    {
        var builder = new RuntimeConfigBuilder();
        var result = builder.Build(Input(profile: ProfileYaml, tun: false));

        Assert.True(result.Success);
        Assert.Equal("false", YamlOps.GetScalar(result.Value!.Config, "tun", "enable"));
        Assert.Null(YamlOps.GetScalar(result.Value!.Config, "dns", "enable"));
    }

    [Fact]
    public void Build_Dns覆写_叠加dns与hosts()
    {
        var dnsOverride = """
            dns:
              nameserver:
                - https://1.1.1.1/dns-query
            hosts:
              example.com: 1.2.3.4
            """;

        var builder = new RuntimeConfigBuilder();
        var result = builder.Build(Input(profile: ProfileYaml, dnsSettings: true, dnsOverride: dnsOverride));

        Assert.True(result.Success);
        var config = result.Value!.Config;
        Console.WriteLine("CONFIG:\n" + Serialize(config));
        var nameservers = (config.Children[new YamlScalarNode("dns")] as YamlMappingNode)!
            .Children[new YamlScalarNode("nameserver")] as YamlSequenceNode;
        Assert.Contains("https://1.1.1.1/dns-query",
            nameservers!.Children.OfType<YamlScalarNode>().Select(n => n.Value));
        Assert.Equal("1.2.3.4", YamlOps.GetScalar(config, "hosts", "example.com"));
    }

    [Fact]
    public void Build_链式代理_设置并清理dialerProxy()
    {
        var builder = new RuntimeConfigBuilder();
        var result = builder.Build(Input(
            profile: ProfileYaml,
            chainProxy: new Dictionary<string, string> { ["node-b"] = "node-a" }));

        Assert.True(result.Success);
        var proxies = result.Value!.Config.Children[new YamlScalarNode("proxies")] as YamlSequenceNode;
        var nodeA = proxies!.Children.OfType<YamlMappingNode>().First(p => YamlOps.GetScalar(p, "name") == "node-a");
        var nodeB = proxies!.Children.OfType<YamlMappingNode>().First(p => YamlOps.GetScalar(p, "name") == "node-b");
        Assert.Null(YamlOps.GetScalar(nodeA, "dialer-proxy"));
        Assert.Equal("node-a", YamlOps.GetScalar(nodeB, "dialer-proxy"));
    }

    [Fact]
    public void Build_链式代理_未知节点报错()
    {
        var builder = new RuntimeConfigBuilder();
        var result = builder.Build(Input(
            profile: ProfileYaml,
            chainProxy: new Dictionary<string, string> { ["ghost"] = "node-a" }));

        Assert.False(result.Success);
        Assert.Equal("chain_unknown_exit", result.Error!.Code);
        Assert.Equal("链式代理", result.Error.Step);
    }

    [Fact]
    public void Build_内置增强_hysteriaAlpn字符串转数组()
    {
        var profile = """
            proxies:
              - name: hy
                type: hysteria2
                server: h.example.com
                port: 443
                alpn: h3,h3-29
            """;

        var builder = new RuntimeConfigBuilder();
        var result = builder.Build(Input(profile: profile));

        Assert.True(result.Success);
        var proxies = result.Value!.Config.Children[new YamlScalarNode("proxies")] as YamlSequenceNode;
        var hy = proxies!.Children.OfType<YamlMappingNode>().First();
        Assert.IsType<YamlSequenceNode>(hy.Children[new YamlScalarNode("alpn")]);
    }

    [Fact]
    public void Build_控制面字段_订阅携带外部控制器被移除()
    {
        // 基础配置不包含 external-controller 时，订阅携带的也不应泄漏
        var baseYaml = "mode: rule\nmixed-port: 7897";
        var profile = """
            external-controller: 0.0.0.0:9999
            secret: leaked
            proxies:
              - name: a
                type: ss
                server: s
                port: 1
            """;

        var builder = new RuntimeConfigBuilder();
        var result = builder.Build(Input(profile: profile, clashBase: baseYaml));

        Assert.True(result.Success);
        var config = result.Value!.Config;
        Assert.Null(YamlOps.GetScalar(config, "external-controller"));
        Assert.Null(YamlOps.GetScalar(config, "secret"));
    }

    [Fact]
    public void Build_AllowLan_循环回环bindAddress放宽为星号()
    {
        var baseYaml = "mode: rule\nmixed-port: 7897\nallow-lan: true\nbind-address: 127.0.0.1";

        var builder = new RuntimeConfigBuilder();
        var result = builder.Build(Input(profile: ProfileYaml, clashBase: baseYaml));

        Assert.True(result.Success);
        Assert.Equal("*", YamlOps.GetScalar(result.Value!.Config, "bind-address"));
    }

    [Fact]
    public void Build_基础配置非控制面字段_合并进运行时()
    {
        var baseYaml = """
            mode: rule
            mixed-port: 7897
            tcp-concurrent: true
            tun:
              enable: false
              stack: mixed
              mtu: 1500
            dns:
              listen: 0.0.0.0:1053
            """;

        var builder = new RuntimeConfigBuilder();
        var result = builder.Build(Input(profile: ProfileYaml, clashBase: baseYaml));

        Assert.True(result.Success);
        var config = result.Value!.Config;
        Assert.Equal("true", YamlOps.GetScalar(config, "tcp-concurrent"));
        Assert.Equal("mixed", YamlOps.GetScalar(config, "tun", "stack"));
        Assert.Equal("1500", YamlOps.GetScalar(config, "tun", "mtu"));
        Assert.Equal("0.0.0.0:1053", YamlOps.GetScalar(config, "dns", "listen"));
    }

    [Fact]
    public void Build_空订阅_仍输出合法配置()
    {
        var builder = new RuntimeConfigBuilder();
        var result = builder.Build(Input(profile: null));

        Assert.True(result.Success);
        Assert.Equal("rule", YamlOps.GetScalar(result.Value!.Config, "mode"));
    }

    [Fact]
    public void Build_MergeYaml非法_返回文件名错误()
    {
        var chain = new List<ChainItemWithContent>
        {
            new(new ChainItem(ChainType.Merge, "p1", "坏Merge", "broken.yaml", false), "- a\n- b"),
        };

        var builder = new RuntimeConfigBuilder();
        var result = builder.Build(Input(profile: ProfileYaml, chain: chain));

        Assert.False(result.Success);
        Assert.Equal("Merge", result.Error!.Step);
        Assert.Equal("broken.yaml", result.Error.File);
    }

    [Fact]
    public void Build_代理组清理_移除不存在成员但保留内置与组引用()
    {
        var profile = """
            proxies:
              - name: node-a
                type: ss
                server: a
                port: 1
            proxy-groups:
              - name: Group1
                type: select
                proxies:
                  - node-a
                  - node-ghost
                  - DIRECT
                  - Group1
            rules:
              - MATCH,Group1
            """;

        var builder = new RuntimeConfigBuilder();
        var result = builder.Build(Input(profile: profile));

        Assert.True(result.Success);
        var groups = result.Value!.Config.Children[new YamlScalarNode("proxy-groups")] as YamlSequenceNode;
        var members = (groups!.Children[0] as YamlMappingNode)!.Children[new YamlScalarNode("proxies")] as YamlSequenceNode;
        var names = members!.Children.OfType<YamlScalarNode>().Select(n => n.Value).ToList();
        Assert.Equal(new[] { "node-a", "DIRECT", "Group1" }, names);
    }
}
