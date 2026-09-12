using Flux.Core.Config;
using Flux.Core.Contracts;
using Flux.Models;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Flux.Services;

/// <summary>
/// 配置管理：config.yaml（基础 Clash 配置）、verge.yaml（应用设置）、profiles.yaml（订阅列表）
/// 以及运行时配置生成（委托 Flux.Core 的 RuntimeConfigBuilder 流水线）。
/// </summary>
public class ConfigService
{
    private readonly RuntimeConfigBuilder _runtimeBuilder = new();

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
                MigrateVerge();
                return;
            }
        }
        catch (Exception ex) { LogService.App(L10n.F("Config_VergeLoadFailed", ex.Message), "error"); }
        Verge = new VergeConfig();
        SaveVerge();
    }

    /// <summary>幂等迁移：升级 schema 版本前先备份旧文件。</summary>
    private void MigrateVerge()
    {
        if (Verge.SchemaVersion >= 1) return;
        try
        {
            Directory.CreateDirectory(Paths.DataBackupDir);
            var backup = Path.Combine(Paths.DataBackupDir,
                $"{DateTime.Now:yyyyMMdd-HHmmss}-v{Verge.SchemaVersion}-verge.yaml");
            if (File.Exists(Paths.VergeConfigFile))
                File.Copy(Paths.VergeConfigFile, backup, overwrite: true);
            LogService.App(L10n.F("Config_VergeMigrated", 1, backup), "info");
        }
        catch (Exception ex)
        {
            LogService.App(L10n.F("Config_VergeMigrateBackupFailed", ex.Message), "warn");
        }
        Verge.SchemaVersion = 1;
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
        catch (Exception ex) { LogService.App(L10n.F("Config_ClashLoadFailed", ex.Message), "error"); }

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
        catch (Exception ex) { LogService.App(L10n.F("Config_ProfilesLoadFailed", ex.Message), "error"); }
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

    /// <summary>按路径（支持 a.b.c）写入字符串序列（如 dns.nameserver，每行一项）。</summary>
    public void PatchClashList(string key, IReadOnlyList<string> items)
    {
        var parts = key.Split('.');
        var current = ClashBase;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var k = new YamlScalarNode(parts[i]);
            if (current.Children.TryGetValue(k, out var next) && next is YamlMappingNode nextMap)
            {
                current = nextMap;
            }
            else
            {
                var newMap = new YamlMappingNode();
                current.Children[k] = newMap;
                current = newMap;
            }
        }

        var seq = new YamlSequenceNode();
        foreach (var item in items.Where(s => !string.IsNullOrWhiteSpace(s)))
            seq.Children.Add(new YamlScalarNode(item.Trim()));
        current.Children[new YamlScalarNode(parts[^1])] = seq;
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

    /// <summary>
    /// 返回当前订阅中 <c>proxy-groups</c> 的声明顺序。
    /// mihomo 的 <c>/proxies</c> 响应是对象，序列化后不能依赖其字段顺序。
    /// </summary>
    public IReadOnlyList<string> GetCurrentProxyGroupOrder()
    {
        var profile = GetCurrentProfileNode();
        if (profile is null ||
            !profile.Children.TryGetValue(new YamlScalarNode("proxy-groups"), out var node) ||
            node is not YamlSequenceNode groups)
            return [];

        var names = new List<string>();
        foreach (var group in groups.Children.OfType<YamlMappingNode>())
        {
            var name = YamlHelper.GetScalar(group, "name");
            if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
        }
        return names;
    }

    /// <summary>
    /// 读取规则模式的兜底 <c>MATCH</c> 规则所指向的代理组。该组通常代表未命中
    /// 其他规则时的主路由，适合作为首页“当前节点”的展示对象。
    /// </summary>
    public string? GetCurrentRuleDefaultProxyGroup()
    {
        var profile = GetCurrentProfileNode();
        if (profile is null ||
            !profile.Children.TryGetValue(new YamlScalarNode("rules"), out var node) ||
            node is not YamlSequenceNode rules)
            return null;

        foreach (var rule in rules.Children.OfType<YamlScalarNode>().Reverse())
        {
            var parts = (rule.Value ?? "").Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length >= 2 && parts[0].Equals("MATCH", StringComparison.OrdinalIgnoreCase))
                return parts[1];
        }
        return null;
    }

    /// <summary>保存当前订阅的节点选择，供配置重载与内核重启后恢复。</summary>
    public void SaveCurrentProxySelection(string group, string node)
    {
        var profile = Profiles.GetCurrent();
        if (profile is null) return;

        profile.SelectedProxyGroup = group;
        var selected = profile.Selected.FirstOrDefault(item =>
            string.Equals(item.Name, group, StringComparison.Ordinal));
        if (selected is null)
            profile.Selected.Add(new ProfileSelected { Name = group, Now = node });
        else
            selected.Now = node;
        SaveProfiles();
    }

    /// <summary>读取当前订阅需要恢复的代理组选择。</summary>
    public IReadOnlyList<ProfileSelected> GetCurrentProxySelections() =>
        Profiles.GetCurrent()?.Selected
            .Where(item => !string.IsNullOrWhiteSpace(item.Name) && !string.IsNullOrWhiteSpace(item.Now))
            .ToList()
        ?? [];

    // ---------- 运行时配置生成 ----------

    /// <summary>
    /// 生成运行时配置（含增强日志）：委托 Flux.Core 流水线 ——
    /// 订阅 → 内置兼容增强 → Merge/Script → Seq → DNS/TUN → 控制面字段强制恢复。
    /// 失败时抛出 InvalidOperationException，由调用方决定保留最后一个有效配置。
    /// </summary>
    public YamlMappingNode GenerateRuntimeNode(out IReadOnlyList<ChainLogEntry> chainLogs)
    {
        var input = new RuntimeConfigInput
        {
            Profile = GetCurrentProfileNode(),
            ClashBase = ClashBase,
            ChainItems = ProfileEnhanceService.BuildChainItems(Profiles.GetCurrent()),
            EnableTun = Verge.EnableTunMode,
            EnableBuiltinEnhance = true,
        };
        var result = _runtimeBuilder.Build(input);
        if (!result.Success)
        {
            chainLogs = [];
            throw new InvalidOperationException(result.Error?.ToString() ?? L10n.T("Config_RuntimeGenFailed"));
        }
        chainLogs = result.Value!.ChainLogs;
        return result.Value!.Config;
    }

    /// <summary>兼容入口：不含增强日志的运行时配置生成。</summary>
    public YamlMappingNode GenerateRuntimeNode()
    {
        var node = GenerateRuntimeNode(out _);
        return node;
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
            ?? throw new InvalidOperationException(L10n.T("Config_TargetDirMissing"));
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
