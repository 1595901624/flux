using Flux.Core.Config;
using Flux.Core.Contracts;
using Flux.Models;
using YamlDotNet.RepresentationModel;

namespace Flux.Services;

/// <summary>增强文件类型与文件名前缀。</summary>
public static class EnhanceFileTypes
{
    public static readonly (ChainType Type, string Prefix, string Ext)[] All =
    [
        (ChainType.Merge, "m", ".yaml"),
        (ChainType.Script, "s", ".js"),
        (ChainType.Rules, "r", ".yaml"),
        (ChainType.Proxies, "p", ".yaml"),
        (ChainType.Groups, "g", ".yaml"),
    ];

    public static string FileNameFor(ChainType type, string uid) =>
        All.First(a => a.Type == type) is var (t, prefix, ext) ? $"{prefix}{uid}{ext}" : "";

    public static ChainType? TypeOfFileName(string fileName)
    {
        foreach (var (type, prefix, _) in All)
        {
            if (fileName.StartsWith(prefix, StringComparison.Ordinal))
                return type;
        }
        return null;
    }
}

/// <summary>
/// 订阅增强文件服务：每个订阅的 Merge/Script/Rules/Proxies/Groups 五类文件
/// 与全局 Merge/Script。可视化编辑器与原始文本编辑读写同一模型（本服务）。
/// </summary>
public static class ProfileEnhanceService
{
    public const string GlobalUid = "Global";
    private const string GlobalMergeFile = "mGlobal.yaml";
    private const string GlobalScriptFile = "sGlobal.js";

    private static ConfigService Config => AppServices.Config;

    // ---------- 全局增强 ----------

    public static string GetGlobalFile(ChainType type) => type switch
    {
        ChainType.Merge => GlobalMergeFile,
        ChainType.Script => GlobalScriptFile,
        _ => "",
    };

    public static string? GetGlobalContent(ChainType type)
    {
        var file = GetGlobalFile(type);
        if (file == "") return null;
        var path = Path.Combine(Paths.ProfilesDir, file);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public static void SetGlobalContent(ChainType type, string content)
    {
        var file = GetGlobalFile(type);
        if (file == "") throw new InvalidOperationException("该类型不支持全局增强");
        ValidateEnhanceContent(type, content, file);
        ConfigService.WriteAllTextAtomic(Path.Combine(Paths.ProfilesDir, file), content);
    }

    // ---------- 订阅增强 ----------

    /// <summary>确保订阅的 5 类增强文件存在（对齐参考项目导入时自动创建）。</summary>
    public static void EnsureCompanionFiles(ProfileItem item)
    {
        foreach (var (type, _, _) in EnhanceFileTypes.All)
        {
            var fileField = FileFieldOf(item, type);
            if (string.IsNullOrEmpty(fileField))
            {
                var fileName = EnhanceFileTypes.FileNameFor(type, item.Uid);
                var path = Path.Combine(Paths.ProfilesDir, fileName);
                if (!File.Exists(path))
                    ConfigService.WriteAllTextAtomic(path, DefaultTemplate(type));
                SetFileField(item, type, fileName);
            }
        }
        Config.SaveProfiles();
    }

    public static string? GetContent(ProfileItem item, ChainType type)
    {
        var fileName = FileFieldOf(item, type);
        if (string.IsNullOrEmpty(fileName)) return null;
        var path = Path.Combine(Paths.ProfilesDir, Path.GetFileName(fileName));
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>保存增强文件。校验失败抛异常，不覆盖有效文件。</summary>
    public static void SetContent(ProfileItem item, ChainType type, string content)
    {
        var fileName = FileFieldOf(item, type);
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = EnhanceFileTypes.FileNameFor(type, item.Uid);
            SetFileField(item, type, fileName);
        }
        ValidateEnhanceContent(type, content, fileName);
        ConfigService.WriteAllTextAtomic(Path.Combine(Paths.ProfilesDir, Path.GetFileName(fileName)), content);
    }

    // ---------- 流水线链构建 ----------

    /// <summary>
    /// 构建运行时流水线的增强链（顺序对齐参考项目）：
    /// 全局 Merge → 订阅 Merge → 全局 Script → 订阅 Script → Rules → Proxies → Groups。
    /// </summary>
    public static IReadOnlyList<ChainItemWithContent> BuildChainItems(ProfileItem? item)
    {
        var chain = new List<ChainItemWithContent>();

        var globalMerge = GetGlobalContent(ChainType.Merge);
        if (!string.IsNullOrWhiteSpace(globalMerge))
            chain.Add(new ChainItemWithContent(
                new ChainItem(ChainType.Merge, GlobalUid, "全局 Merge", GlobalMergeFile, true), globalMerge));

        var globalScript = GetGlobalContent(ChainType.Script);
        if (!string.IsNullOrWhiteSpace(globalScript))
            chain.Add(new ChainItemWithContent(
                new ChainItem(ChainType.Script, GlobalUid, "全局 Script", GlobalScriptFile, true), globalScript));

        if (item is not null)
        {
            foreach (var (type, prefix, _) in EnhanceFileTypes.All)
            {
                var fileName = FileFieldOf(item, type);
                if (string.IsNullOrEmpty(fileName)) continue;
                var path = Path.Combine(Paths.ProfilesDir, Path.GetFileName(fileName));
                if (!File.Exists(path)) continue;
                var content = SafeRead(path);
                if (string.IsNullOrWhiteSpace(content)) continue;
                chain.Add(new ChainItemWithContent(
                    new ChainItem(type, item.Uid, $"{type} 增强", Path.GetFileName(fileName), false), content));
            }
        }

        return chain;
    }

    /// <summary>删除订阅时清理其增强文件。</summary>
    public static void DeleteCompanionFiles(ProfileItem item)
    {
        foreach (var (type, fileName) in FileFields(item))
        {
            if (string.IsNullOrEmpty(fileName)) continue;
            try
            {
                var path = Path.Combine(Paths.ProfilesDir, Path.GetFileName(fileName));
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }
    }

    // ---------- 内部 ----------

    private static IEnumerable<(ChainType Type, string? FileName)> FileFields(ProfileItem item)
    {
        yield return (ChainType.Merge, item.Merge);
        yield return (ChainType.Script, item.Script);
        yield return (ChainType.Rules, item.Rules);
        yield return (ChainType.Proxies, item.Proxies);
        yield return (ChainType.Groups, item.Groups);
    }

    private static string? FileFieldOf(ProfileItem item, ChainType type) => type switch
    {
        ChainType.Merge => item.Merge,
        ChainType.Script => item.Script,
        ChainType.Rules => item.Rules,
        ChainType.Proxies => item.Proxies,
        ChainType.Groups => item.Groups,
        _ => null,
    };

    private static void SetFileField(ProfileItem item, ChainType type, string fileName)
    {
        switch (type)
        {
            case ChainType.Merge: item.Merge = fileName; break;
            case ChainType.Script: item.Script = fileName; break;
            case ChainType.Rules: item.Rules = fileName; break;
            case ChainType.Proxies: item.Proxies = fileName; break;
            case ChainType.Groups: item.Groups = fileName; break;
        }
    }

    private static string SafeRead(string path)
    {
        try { return File.ReadAllText(path); }
        catch { return ""; }
    }

    private static void ValidateEnhanceContent(ChainType type, string content, string fileName)
    {
        switch (type)
        {
            case ChainType.Script:
                // 基本检查：必须包含 main 定义；语法校验由 Jint 编译错误捕获
                if (content.Length > 0 && !content.Contains("main", StringComparison.Ordinal))
                    throw new InvalidOperationException("Script 文件必须定义 main(config, profileName) 函数");
                break;
            case ChainType.Merge:
            case ChainType.Rules:
            case ChainType.Proxies:
            case ChainType.Groups:
                if (content.Length == 0) return; // 空文件 = 未启用
                if (YamlOps.ParseMapping(content) is null)
                    throw new InvalidOperationException($"YAML 语法错误: {fileName}（必须是键值映射）");
                break;
        }
    }

    private static string DefaultTemplate(ChainType type)
    {
        switch (type)
        {
            case ChainType.Merge:
                return """
                    # 全局/订阅 Merge：深合并到运行时配置（键值为 ~ 表示删除）
                    # 例：
                    # dns:
                    #   ipv6: false
                    """;
            case ChainType.Script:
                return """
                    // JavaScript 增强：main(config, profileName) 返回修改后的配置
                    function main(config, profileName) {
                      // config["mixed-port"] = 7897;
                      return config;
                    }
                    """;
            case ChainType.Rules:
                return """
                    # 规则前后插入
                    # prepend:
                    #   - DOMAIN-SUFFIX,example.org,PROXY
                    # append:
                    #   - MATCH,PROXY
                    # delete:
                    #   - MATCH,DIRECT
                    """;
            case ChainType.Proxies:
                return """
                    # 节点前后插入（prepend 为完整节点定义，delete 为节点名列表）
                    # prepend:
                    #   - name: my-node
                    #     type: ss
                    #     server: 1.2.3.4
                    #     port: 8388
                    #     cipher: aes-128-gcm
                    #     password: "pass"
                    # delete: []
                    """;
            case ChainType.Groups:
                return """
                    # 代理组前后插入
                    # prepend:
                    #   - name: My-Group
                    #     type: select
                    #     proxies: [自动选择, DIRECT]
                    # delete: []
                    """;
            default:
                return "";
        }
    }
}
