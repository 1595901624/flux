using Flux.Models;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Flux.Services;

/// <summary>
/// 配置管理：config.yaml（基础 Clash 配置）、verge.yaml（应用设置）、profiles.yaml（订阅列表）
/// 以及运行时配置生成（对应参考项目的 enhance 链：订阅 YAML → 覆盖基础配置 → TUN 补丁 → 控制面字段保护）。
/// </summary>
public class ConfigService
{
    private static readonly string[] ControlPlaneKeys =
    [
        "external-controller", "external-controller-pipe", "secret",
        "mixed-port", "socks-port", "port", "redir-port", "tproxy-port",
        "mode", "allow-lan", "log-level", "ipv6", "unified-delay"
    ];

    public VergeConfig Verge { get; private set; } = new();
    public ProfilesConfig Profiles { get; private set; } = new();
    public YamlMappingNode ClashBase { get; private set; } = new();

    /// <summary>持久化运行配置发生变化，供 UI 刷新显示；应用配置由调用方显式等待。</summary>
    public event Action? RuntimeInvalidated;

    // ---------- 加载 / 保存 ----------

    public static ConfigService LoadOrCreate()
    {
        var svc = new ConfigService();
        svc.LoadVerge();
        svc.LoadClashBase();
        svc.LoadProfiles();
        return svc;
    }

    private void LoadVerge()
    {
        try
        {
            if (File.Exists(Paths.VergeConfigFile))
            {
                Verge = VergeConfig.Deserialize(File.ReadAllText(Paths.VergeConfigFile));
                return;
            }
        }
        catch (Exception ex) { LogService.App("verge.yaml 加载失败: " + ex.Message, "error"); }
        Verge = new VergeConfig();
        SaveVerge();
    }

    public void SaveVerge()
    {
        WriteAllTextAtomic(Paths.VergeConfigFile, Verge.Serialize());
    }

    private void LoadClashBase()
    {
        try
        {
            if (File.Exists(Paths.ClashConfigFile))
            {
                var text = File.ReadAllText(Paths.ClashConfigFile);
                if (YamlHelper.ParseMapping(text) is { } node)
                {
                    ClashBase = node;
                    return;
                }
            }
        }
        catch (Exception ex) { LogService.App("config.yaml 加载失败: " + ex.Message, "error"); }

        ClashBase = YamlHelper.ParseMapping(BuildBaseTemplate())!;
        // 默认 secret：首次生成随机值
        if (!ClashBase.Children.ContainsKey(new YamlScalarNode("secret")) ||
            string.IsNullOrWhiteSpace(YamlHelper.GetScalar(ClashBase, "secret")))
        {
            YamlHelper.SetScalar(ClashBase, "secret", Guid.NewGuid().ToString("N")[..16]);
        }
        SaveClashBase();
    }

    public void SaveClashBase()
    {
        WriteAllTextAtomic(Paths.ClashConfigFile, new Serializer().Serialize(ClashBase));
    }

    private void LoadProfiles()
    {
        try
        {
            if (File.Exists(Paths.ProfilesConfigFile))
            {
                Profiles = ProfilesConfig.Deserialize(File.ReadAllText(Paths.ProfilesConfigFile));
                return;
            }
        }
        catch (Exception ex) { LogService.App("profiles.yaml 加载失败: " + ex.Message, "error"); }
        Profiles = new ProfilesConfig();
    }

    public void SaveProfiles()
    {
        WriteAllTextAtomic(Paths.ProfilesConfigFile, Profiles.Serialize());
    }

    // ---------- 基础配置模板 ----------

    private static string BuildBaseTemplate() => """
        # 基础 Clash 配置（由 Flux 生成与维护）
        mixed-port: 7897
        socks-port: 7898
        port: 7899
        allow-lan: false
        mode: rule
        log-level: info
        ipv6: true
        unified-delay: true
        external-controller: 127.0.0.1:9097
        tcp-concurrent: true
        profile:
          store-selected: true
          store-fake-ip: true
        tun:
          enable: false
          stack: gvisor
          auto-route: true
          strict-route: false
          auto-detect-interface: true
          dns-hijack:
            - any:53
        dns:
          enable: false
          listen: 0.0.0.0:1053
          enhanced-mode: fake-ip
          fake-ip-range: 198.18.0.1/16
          default-nameserver:
            - 223.5.5.5
            - 119.29.29.29
          nameserver:
            - https://doh.pub/dns-query
            - https://dns.alidns.com/dns-query
        """;

    // ---------- 基础配置读写（供设置页 patch） ----------

    public string GetScalar(string key, string fallback = "")
    {
        var v = YamlHelper.GetScalar(ClashBase, key);
        return string.IsNullOrEmpty(v) ? fallback : v;
    }

    public int GetPort(string key, int fallback)
        => int.TryParse(GetScalar(key), out var p) && p > 0 ? p : fallback;

    public bool GetBool(string key, bool fallback) =>
        GetScalar(key)?.ToLowerInvariant() switch
        {
            "true" => true,
            "false" => false,
            _ => fallback,
        };

    public void PatchClashBase(string key, object value)
    {
        YamlHelper.SetValue(ClashBase, key, value);
        SaveClashBase();
        RuntimeInvalidated?.Invoke();
    }

    public string Mode
    {
        get => GetScalar("mode", "rule");
        set => PatchClashBase("mode", value);
    }

    public (string Controller, string Secret) GetControllerInfo()
    {
        var controller = GetScalar("external-controller", "127.0.0.1:9097");
        var secret = GetScalar("secret");
        return (controller, secret);
    }

    public int MixedPort => GetPort("mixed-port", 7897);

    // ---------- 订阅文件 ----------

    public string? GetProfileFileContent(ProfileItem item)
    {
        try
        {
            var path = item.FilePath;
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch { return null; }
    }

    public YamlMappingNode? GetCurrentProfileNode()
    {
        var item = Profiles.GetCurrent();
        if (item is null) return null;
        var text = GetProfileFileContent(item);
        return text is null ? null : YamlHelper.ParseMapping(text);
    }

    // ---------- 运行时配置生成 ----------

    /// <summary>
    /// 生成运行时配置：当前订阅 YAML 为底 → 基础配置覆盖 → TUN 补丁 → 控制面字段恢复（不被订阅覆盖）。
    /// </summary>
    public YamlMappingNode GenerateRuntimeNode()
    {
        YamlMappingNode runtime = GetCurrentProfileNode() ?? new YamlMappingNode();
        YamlHelper.DeepOverlay(runtime, ClashBase);

        var snapshot = CaptureControlKeys();
        ApplyTunPatch(runtime, Verge.EnableTunMode);
        RestoreControlKeys(runtime, snapshot);

        return runtime;
    }

    private Dictionary<string, YamlNode> CaptureControlKeys()
    {
        var dict = new Dictionary<string, YamlNode>();
        foreach (var key in ControlPlaneKeys)
        {
            if (ClashBase.Children.TryGetValue(new YamlScalarNode(key), out var value))
                dict[key] = value;
        }
        return dict;
    }

    private void RestoreControlKeys(YamlMappingNode runtime, Dictionary<string, YamlNode> snapshot)
    {
        foreach (var (key, value) in snapshot)
        {
            runtime.Children[new YamlScalarNode(key)] = value;
        }
    }

    private void ApplyTunPatch(YamlMappingNode runtime, bool enable)
    {
        // tun 键已在基础配置覆盖时存在
        if (!runtime.Children.TryGetValue(new YamlScalarNode("tun"), out var tunNode) ||
            tunNode is not YamlMappingNode tun)
        {
            tun = new YamlMappingNode();
            runtime.Children[new YamlScalarNode("tun")] = tun;
        }
        tun.Children[new YamlScalarNode("enable")] = new YamlScalarNode(enable ? "true" : "false");

        if (enable)
        {
            // TUN 需要 DNS（fake-ip），缺失时补默认值
            if (!runtime.Children.TryGetValue(new YamlScalarNode("dns"), out var dnsNode) ||
                dnsNode is not YamlMappingNode dns)
            {
                dns = new YamlMappingNode();
                runtime.Children[new YamlScalarNode("dns")] = dns;
            }
            dns.Children[new YamlScalarNode("enable")] = new YamlScalarNode("true");
            if (!dns.Children.ContainsKey(new YamlScalarNode("enhanced-mode")))
                dns.Children[new YamlScalarNode("enhanced-mode")] = new YamlScalarNode("fake-ip");
            if (!dns.Children.ContainsKey(new YamlScalarNode("fake-ip-range")))
                dns.Children[new YamlScalarNode("fake-ip-range")] = new YamlScalarNode("198.18.0.1/16");
        }
    }

    public void WriteRuntimeFile(string path)
    {
        var node = GenerateRuntimeNode();
        var yaml = new Serializer().Serialize(node);
        WriteAllTextAtomic(path, yaml);
    }

    /// <summary>在同一目录写临时文件后替换，避免断电或崩溃留下半个 YAML 文件。</summary>
    internal static void WriteAllTextAtomic(string path, string content)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("目标文件缺少目录");
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temp, content);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    /// <summary>通知核心服务重新生成并应用运行时配置。</summary>
    public void InvalidateRuntime() => RuntimeInvalidated?.Invoke();
}
