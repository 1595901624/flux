using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.ObjectFactories;

namespace Vxn.Models;

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

    [YamlMember(Alias = "option")]
    public ProfileOption Option { get; set; } = new();

    [YamlMember(Alias = "extra")]
    public ProfileExtra? Extra { get; set; }

    [YamlIgnore]
    public string FilePath => Path.Combine(Models.ProfilesConfig.Dir, File);
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
