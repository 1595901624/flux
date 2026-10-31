using Flux.Core.Config;

namespace Flux.Core.Compatibility;

/// <summary>旧版文件及导入协议，仅在兼容边界保留历史名称。</summary>
public static class LegacyCompatibility
{
    public const string SettingsFileName = "verge.yaml";
    public const string ImportScheme = "clash-verge";

    public static void MigrateSettingsFile(string directory, Action<string>? validate = null)
    {
        var target = Path.Combine(directory, "settings.yaml");
        var source = Path.Combine(directory, SettingsFileName);
        if (File.Exists(target) || !File.Exists(source)) return;
        var content = File.ReadAllText(source);
        if (YamlOps.ParseMapping(content) is null)
            throw new InvalidDataException("Invalid legacy application settings; original file retained.");
        validate?.Invoke(content);
        var temp = Path.Combine(directory, $".settings-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temp, content);
            File.Move(temp, target, overwrite: false);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        // 原文件保留为升级备份；新文件存在时不再读取它。
    }
}
