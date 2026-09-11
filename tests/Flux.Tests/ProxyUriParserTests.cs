using Flux.Core.Config;
using Flux.Core.Proxies;
using YamlDotNet.RepresentationModel;
using Xunit;

namespace Flux.Tests;

/// <summary>代理分享链接解析测试：各协议、Base64 订阅格式、异常输入。</summary>
public class ProxyUriParserTests
{
    private static YamlMappingNode Parse(string yaml) =>
        YamlOps.ParseMapping(yaml) ?? throw new InvalidOperationException(yaml);

    private static string? Scalar(YamlMappingNode node, string key) =>
        node.Children.TryGetValue(new YamlScalarNode(key), out var v) && v is YamlScalarNode s ? s.Value : null;

    [Fact]
    public void Parse_ss_SIP002格式()
    {
        // aes-128-gcm:pass1234@1.2.3.4:8388
        var userInfo = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("aes-128-gcm:pass1234"));
        var result = ProxyUriParser.ParseOne($"ss://{userInfo}@1.2.3.4:8388#TestNode");

        Assert.True(result.Success, result.Error);
        Assert.Equal("TestNode", result.Name);
        Assert.Equal("ss", Scalar(result.Node, "type"));
        Assert.Equal("1.2.3.4", Scalar(result.Node, "server"));
        Assert.Equal("8388", Scalar(result.Node, "port"));
        Assert.Equal("aes-128-gcm", Scalar(result.Node, "cipher"));
        Assert.Equal("pass1234", Scalar(result.Node, "password"));
    }

    [Fact]
    public void Parse_ss_旧版整体Base64()
    {
        var payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("rc4-md5:mypass@example.com:443"));
        var result = ProxyUriParser.ParseOne($"ss://{payload}");

        Assert.True(result.Success, result.Error);
        Assert.Equal("example.com", Scalar(result.Node, "server"));
        Assert.Equal("443", Scalar(result.Node, "port"));
        Assert.Equal("rc4-md5", Scalar(result.Node, "cipher"));
    }

    [Fact]
    public void Parse_vmess_标准Base64JSON()
    {
        var json = """{"v":"2","ps":"VM节点","add":"v.example.com","port":"443","id":"b831381d-6324-4d53-ad4f-8cda48b30811","aid":"0","net":"ws","host":"cdn.example.com","path":"/ws","tls":"tls","sni":"sni.example.com"}""";
        var payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
        var result = ProxyUriParser.ParseOne($"vmess://{payload}");

        Assert.True(result.Success, result.Error);
        Assert.Equal("VM节点", result.Name);
        Assert.Equal("vmess", Scalar(result.Node, "type"));
        Assert.Equal("v.example.com", Scalar(result.Node, "server"));
        Assert.Equal("443", Scalar(result.Node, "port"));
        Assert.Equal("b831381d-6324-4d53-ad4f-8cda48b30811", Scalar(result.Node, "uuid"));
        Assert.Equal("ws", Scalar(result.Node, "network"));
        Assert.Equal("true", Scalar(result.Node, "tls"));
        Assert.Equal("sni.example.com", Scalar(result.Node, "servername"));
        var wsOpts = result.Node.Children[new YamlScalarNode("ws-opts")] as YamlMappingNode;
        Assert.NotNull(wsOpts);
        Assert.Equal("/ws", Scalar(wsOpts, "path"));
    }

    [Fact]
    public void Parse_trojan_带Sni与Insecure()
    {
        var result = ProxyUriParser.ParseOne("trojan://pass%40word@t.example.com:443?sni=s.example.com&allowInsecure=1#Trojan");
        Assert.True(result.Success, result.Error);
        Assert.Equal("trojan", Scalar(result.Node, "type"));
        Assert.Equal("t.example.com", Scalar(result.Node, "server"));
        Assert.Equal("pass@word", Scalar(result.Node, "password"));
        Assert.Equal("s.example.com", Scalar(result.Node, "sni"));
        Assert.Equal("1", Scalar(result.Node, "skip-cert-verify"));
    }

    [Fact]
    public void Parse_vless_Reality流字段()
    {
        var result = ProxyUriParser.ParseOne("vless://b831381d-6324-4d53-ad4f-8cda48b30811@v.example.com:443?encryption=none&security=tls&sni=v.example.com&type=ws&path=%2Fvless&host=v.example.com&flow=xtls-rprx-vision#VLESS");
        Assert.True(result.Success, result.Error);
        Assert.Equal("vless", Scalar(result.Node, "type"));
        Assert.Equal("xtls-rprx-vision", Scalar(result.Node, "flow"));
        Assert.Equal("ws", Scalar(result.Node, "network"));
        Assert.Equal("true", Scalar(result.Node, "tls"));
        var wsOpts = result.Node.Children[new YamlScalarNode("ws-opts")] as YamlMappingNode;
        Assert.Equal("/vless", Scalar(wsOpts!, "path"));
    }

    [Fact]
    public void Parse_hysteria2_带密码与混淆()
    {
        var result = ProxyUriParser.ParseOne("hysteria2://secretpass@h.example.com:8443?sni=h.example.com&obfs=salamander&obfs-password=obfsPass&insecure=1#HY2");
        Assert.True(result.Success, result.Error);
        Assert.Equal("hysteria2", Scalar(result.Node, "type"));
        Assert.Equal("secretpass", Scalar(result.Node, "password"));
        Assert.Equal("salamander", Scalar(result.Node, "obfs"));
        Assert.Equal("obfsPass", Scalar(result.Node, "obfs-password"));
        Assert.Equal("1", Scalar(result.Node, "skip-cert-verify"));
    }

    [Fact]
    public void Parse_hy2别名()
    {
        var result = ProxyUriParser.ParseOne("hy2://pass@h.example.com:443#节点");
        Assert.True(result.Success, result.Error);
        Assert.Equal("hysteria2", Scalar(result.Node, "type"));
        Assert.Equal("节点", result.Name);
    }

    [Fact]
    public void Parse_tuic_uuid与密码()
    {
        var result = ProxyUriParser.ParseOne("tuic://b831381d-6324-4d53-ad4f-8cda48b30811:tuicpass@t.example.com:443?congestion_control=bbr&alpn=h3&sni=t.example.com#TUIC");
        Assert.True(result.Success, result.Error);
        Assert.Equal("tuic", Scalar(result.Node, "type"));
        Assert.Equal("b831381d-6324-4d53-ad4f-8cda48b30811", Scalar(result.Node, "uuid"));
        Assert.Equal("tuicpass", Scalar(result.Node, "password"));
        Assert.Equal("bbr", Scalar(result.Node, "congestion-controller"));
        Assert.Equal("h3", Scalar(result.Node, "alpn"));
    }

    [Fact]
    public void Parse_anytls()
    {
        var result = ProxyUriParser.ParseOne("anytls://pass@a.example.com:8443?sni=a.example.com#AnyTLS");
        Assert.True(result.Success, result.Error);
        Assert.Equal("anytls", Scalar(result.Node, "type"));
        Assert.Equal("pass", Scalar(result.Node, "password"));
    }

    [Fact]
    public void Parse_socks5_带认证()
    {
        var result = ProxyUriParser.ParseOne("socks5://user:pass@s.example.com:1080#SOCKS");
        Assert.True(result.Success, result.Error);
        Assert.Equal("socks5", Scalar(result.Node, "type"));
        Assert.Equal("user", Scalar(result.Node, "username"));
        Assert.Equal("pass", Scalar(result.Node, "password"));
    }

    [Fact]
    public void Parse_IPv6地址()
    {
        var result = ProxyUriParser.ParseOne("trojan://pass@[2001:db8::1]:443#v6");
        Assert.True(result.Success, result.Error);
        Assert.Equal("2001:db8::1", Scalar(result.Node, "server"));
        Assert.Equal("443", Scalar(result.Node, "port"));
    }

    [Fact]
    public void Parse_多行文本_含失败行()
    {
        var ss = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("aes-128-gcm:p@a.com:8388"));
        var text = $"ss://{ss}@a.example.com:8388#A\nssr://!!!!badbase64\ntrojan://pass@b.example.com:443#B";
        var results = ProxyUriParser.Parse(text);

        Assert.Equal(3, results.Count);
        Assert.Equal(2, results.Count(r => r.Success));
        Assert.Single(results, r => !r.Success);
        Assert.Equal("A", results[0].Name);
        Assert.Equal("B", results[2].Name);
    }

    [Fact]
    public void Parse_Base64订阅格式整体解码()
    {
        var content = "trojan://pass@x.example.com:443#X\ntrojan://pass@y.example.com:443#Y\n";
        var payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content));
        var results = ProxyUriParser.Parse($"base64content\n{payload}\nmore");

        Assert.Equal(2, results.Count(r => r.Success));
        Assert.Equal("X", results[0].Name);
        Assert.Equal("Y", results[1].Name);
    }

    [Fact]
    public void Parse_空文本_返回空列表()
    {
        Assert.Empty(ProxyUriParser.Parse(""));
        Assert.Empty(ProxyUriParser.Parse("   \n  "));
    }

    [Fact]
    public void Parse_端口非法_报错()
    {
        var result = ProxyUriParser.ParseOne("trojan://pass@b.example.com:notaport#B");
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Parse_未知协议_报错()
    {
        var result = ProxyUriParser.ParseOne("unknown://whatever");
        Assert.False(result.Success);
        Assert.Contains("不支持的协议", result.Error);
    }
}
