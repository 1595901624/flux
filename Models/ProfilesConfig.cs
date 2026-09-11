using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.ObjectFactories;

namespace Flux.Models;

public class ProfileOption
{
    [YamlMember(Alias = "user-agent")]
    public string? UserAgent { get; set; }

    /// <summary>自动更新间隔（分钟），0 表示不自动更新。</summary>
    [YamlMember(Alias = "update-interval")]
    public int UpdateInterval { get; set; } = 0;

    [YamlMember(Alias = "with-proxy")]
    public bool WithProxy { get; set; } = false;

    [YamlMember(Alias = "self-proxy")]
    public bool SelfProxy { get; set; } = false;

    [YamlMember(Alias = "timeout-seconds")]
    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>是否参与自动更新（默认 true，显式关闭后仅手动更新）。</summary>
    [YamlMember(Alias = "allow-auto-update")]
    public bool AllowAutoUpdate { get; set; } = true;

    /// <summary>危险选项：接受无效 TLS 证书。</summary>
    [YamlMember(Alias = "danger-accept-invalid-certs")]
    public bool DangerAcceptInvalidCerts { get; set; } = false;

    /// <summary>更新通道：direct | system | self | auto（auto = 直连→内核代理→系统代理回退）。</summary>
    [YamlMember(Alias = "update-channel")]
    public string UpdateChannel { get; set; } = "auto";
}

public class ProfileExtra
{
    [YamlMember(Alias = "upload")]
    public long Upload { get; set; }

    [YamlMember(Alias = "download")]
    public long Download { get; set; }

    [YamlMember(Alias = "total")]
    public long Total { get; set; }

    /// <summary>到期时间（Unix 秒），0 表示未知。</summary>
    [YamlMember(Alias = "expire")]
    public long Expire { get; set; }
}

/// <summary>订阅内代理组的已选节点，用于内核重载或重启后恢复。</summary>
public class ProfileSelected
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "";

    [YamlMember(Alias = "now")]
    public string Now { get; set; } = "";
}

public class ProfileItem
{
    [YamlMember(Alias = "uid")]
    public string Uid { get; set; } = "";

    /// <summary>remote | local</summary>
    [YamlMember(Alias = "type")]
    public string Type { get; set; } = "remote";

    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "";

    /// <summary>profiles 目录下的文件名（不含路径）。</summary>
    [YamlMember(Alias = "file")]
    public string File { get; set; } = "";

    [YamlMember(Alias = "desc")]
    public string Desc { get; set; } = "";

    [YamlMember(Alias = "url")]
    public string Url { get; set; } = "";

    [YamlMember(Alias = "updated")]
    public DateTime Updated { get; set; }

    /// <summary>首页“当前节点”在该订阅下优先展示的代理组。</summary>
    [YamlMember(Alias = "selected-proxy-group")]
    public string SelectedProxyGroup { get; set; } = "";

    [YamlMember(Alias = "selected")]
    public List<ProfileSelected> Selected { get; set; } = new();

    [YamlMember(Alias = "option")]
    public ProfileOption Option { get; set; } = new();

    [YamlMember(Alias = "extra")]
    public ProfileExtra? Extra { get; set; }

    /// <summary>远程订阅主页（profile-web-page-url）。</summary>
    [YamlMember(Alias = "home")]
    public string Home { get; set; } = "";

    /// <summary>上次更新状态：unknown | success | not-modified | failed。</summary>
    [YamlMember(Alias = "last-update-status")]
    public string LastUpdateStatus { get; set; } = "unknown";

    /// <summary>上次更新失败原因（成功时为空）。</summary>
    [YamlMember(Alias = "last-error")]
    public string LastError { get; set; } = "";

    /// <summary>下一次自动更新时间。</summary>
    [YamlMember(Alias = "next-update-at")]
    public DateTime? NextUpdateAt { get; set; }

    // ---------- 增强文件 ID（Merge/Script/Rules/Proxies/Groups） ----------

    [YamlMember(Alias = "merge")]
    public string? Merge { get; set; }

    [YamlMember(Alias = "script")]
    public string? Script { get; set; }

    [YamlMember(Alias = "rules")]
    public string? Rules { get; set; }

    [YamlMember(Alias = "proxies")]
    public string? Proxies { get; set; }

    [YamlMember(Alias = "groups")]
    public string? Groups { get; set; }

    // ---------- HTTP 条件请求缓存 ----------

    [YamlMember(Alias = "etag")]
    public string? ETag { get; set; }

    [YamlMember(Alias = "last-modified")]
    public string? LastModified { get; set; }

    [YamlIgnore]
    public string FilePath => Path.Combine(Models.ProfilesConfig.Dir, Path.GetFileName(File));

    /// <summary>更新间隔对应的下次更新时间；未启用时为 null。</summary>
    public DateTime? ComputeNextUpdateAt()
    {
        if (Type != "remote" || !Option.AllowAutoUpdate || Option.UpdateInterval <= 0)
            return null;
        var baseTime = Updated == default ? DateTime.Now : Updated;
        return baseTime.AddMinutes(Option.UpdateInterval);
    }
}

public class ProfilesConfig
{
    [YamlMember(Alias = "current")]
    public string? Current { get; set; }

    [YamlMember(Alias = "items")]
    public List<ProfileItem> Items { get; set; } = new();

    public static string Dir => Services.Paths.ProfilesDir;

    public ProfileItem? GetCurrent() => Items.FirstOrDefault(i => i.Uid == Current);

    public string Serialize()
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .WithIndentedSequences()
            .Build();
        return serializer.Serialize(this);
    }

    public static ProfilesConfig Deserialize(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        return deserializer.Deserialize<ProfilesConfig>(yaml) ?? new ProfilesConfig();
    }
}
