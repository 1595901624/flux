using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Flux.Models;

/// <summary>
/// 应用设置（verge.yaml），命名采用 kebab-case 与参考项目保持一致。
/// </summary>
public class VergeConfig
{
    /// <summary>配置 schema 版本；升级时由 ConfigService 执行幂等迁移。</summary>
    [YamlMember(Alias = "schema-version")]
    public int SchemaVersion { get; set; } = 1;

    [YamlMember(Alias = "language")]
    public string Language { get; set; } = "zh-CN";

    [YamlMember(Alias = "theme-mode")]
    public string ThemeMode { get; set; } = "system"; // system | light | dark

    [YamlMember(Alias = "theme-color")]
    public string ThemeColor { get; set; } = ""; // 空 = 跟随系统强调色

    [YamlMember(Alias = "enable-system-proxy")]
    public bool EnableSystemProxy { get; set; } = false;

    /// <summary>PAC 模式：系统代理经由自动配置脚本而非手动代理服务器。</summary>
    [YamlMember(Alias = "enable-pac-mode")]
    public bool EnablePacMode { get; set; } = false;

    [YamlMember(Alias = "enable-proxy-guard")]
    public bool EnableProxyGuard { get; set; } = true;

    [YamlMember(Alias = "use-default-bypass")]
    public bool UseDefaultBypass { get; set; } = true;

    [YamlMember(Alias = "system-proxy-bypass")]
    public string SystemProxyBypass { get; set; } = "";

    [YamlMember(Alias = "enable-tun-mode")]
    public bool EnableTunMode { get; set; } = false;

    [YamlMember(Alias = "enable-auto-launch")]
    public bool EnableAutoLaunch { get; set; } = false;

    [YamlMember(Alias = "enable-silent-start")]
    public bool EnableSilentStart { get; set; } = false;

    [YamlMember(Alias = "auto-close-connection")]
    public bool AutoCloseConnection { get; set; } = true;

    [YamlMember(Alias = "default-latency-test")]
    public string DefaultLatencyTest { get; set; } = "https://www.gstatic.com/generate_204";

    [YamlMember(Alias = "default-latency-timeout")]
    public int DefaultLatencyTimeout { get; set; } = 5000;

    [YamlMember(Alias = "enable-external-controller")]
    public bool EnableExternalController { get; set; } = true;

    [YamlMember(Alias = "log-level")]
    public string LogLevel { get; set; } = "info"; // debug|info|warning|error|silent

    [YamlMember(Alias = "enable-log")]
    public bool EnableLog { get; set; } = false;

    [YamlMember(Alias = "enable-memory-usage")]
    public bool EnableMemoryUsage { get; set; } = true;

    [YamlMember(Alias = "traffic-graph")]
    public bool TrafficGraph { get; set; } = true;

    /// <summary>关闭窗口若干分钟后自动进入轻量模式（0 = 关闭自动进入）。</summary>
    [YamlMember(Alias = "enable-lightweight-mode")]
    public bool EnableLightweightMode { get; set; } = false;

    /// <summary>自动进入轻量模式的分钟数。</summary>
    [YamlMember(Alias = "auto-lightweight-minutes")]
    public int AutoLightweightMinutes { get; set; } = 10;

    /// <summary>全局热键：action → 组合键文本（如 "Ctrl+Shift+F"）。空串表示未设置。</summary>
    [YamlMember(Alias = "hotkeys")]
    public Dictionary<string, string> Hotkeys { get; set; } = new();

    /// <summary>保存到 verge.yaml。</summary>
    public string Serialize()
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .WithIndentedSequences()
            .Build();
        return serializer.Serialize(this);
    }

    public static VergeConfig Deserialize(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        return deserializer.Deserialize<VergeConfig>(yaml) ?? new VergeConfig();
    }
}
