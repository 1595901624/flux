using Microsoft.Windows.ApplicationModel.Resources;
using System.IO;

namespace Flux.Services;

/// <summary>
/// 本地化加载器：从 resources.pri 读取 Strings/&lt;lang&gt;/Resources.resw。
/// 回退链：当前语言 → 默认语言（en-US，MRT 自动处理）→ 资源键本身。
/// </summary>
public static class L10n
{
    private static ResourceLoader? _loader;

    // 单参数 ResourceLoader(string) 的参数是 PRI 文件名，不是资源子树。
    // 必须使用两参数构造器，把默认 PRI 与 "Resources" 子树分别传入。
    public static ResourceLoader Loader => _loader ??= CreateLoader();

    private static ResourceLoader CreateLoader()
    {
        var resourceFile = ResourceLoader.GetDefaultResourceFilePath();
        return new ResourceLoader(resourceFile, "Resources");
    }

    /// <summary>
    /// 语言覆盖发生变化后丢弃旧加载器，避免动态菜单继续使用创建时的语言上下文。
    /// </summary>
    public static void Reset() => _loader = null;

    /// <summary>按键取文本；全部语言缺失时返回键名本身（不显示空文本）。
    /// RESW 的 "Key.Text" 在 PRI 中会编译为 "Key/Text" 路径。</summary>
    public static string T(string key)
    {
        // ResourceLoader.GetString 使用 PRI 路径语法。XAML x:Uid 使用的点号属性
        // 在 PRI 中是子树分隔符，因此程序化查询必须优先使用 "Key/Text"。
        try
        {
            var value = Loader.GetString(key + "/Text");
            if (!string.IsNullOrEmpty(value)) return value;
        }
        catch { }
        // 兼容可能直接以点号命名的手工 PRI/旧资源包。
        try
        {
            var value = Loader.GetString(key + ".Text");
            if (!string.IsNullOrEmpty(value)) return value;
        }
        catch { }
        try
        {
            var value = Loader.GetString(key);
            if (!string.IsNullOrEmpty(value)) return value;
        }
        catch { }
        Program.Trace("l10n missing: " + key);
        return key;
    }

    /// <summary>格式化文本（L10n.T 后 string.Format）。</summary>
    public static string F(string key, params object[] args) =>
        string.Format(T(key), args);
}
