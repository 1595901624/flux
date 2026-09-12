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

    // Resources.resw 的内容位于 PRI 的 "Resources" 子树；
    // 无作用域的 ResourceLoader 查找裸路径会抛 NamedResource 异常。
    public static ResourceLoader Loader => _loader ??= new ResourceLoader("Resources");

    /// <summary>按键取文本；全部语言缺失时返回键名本身（不显示空文本）。
    /// 回退链：&lt;键&gt; → &lt;键&gt;.Text → 键名。</summary>
    public static string T(string key)
    {
        // 代码消费的键在 resw 中为 "<键>.Text"；裸键名仅少数场景存在。
        // GetString 对缺失资源会抛异常（而非返回空），因此两次查找各自捕获。
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
        return key;
    }

    /// <summary>格式化文本（L10n.T 后 string.Format）。</summary>
    public static string F(string key, params object[] args) =>
        string.Format(T(key), args);
}
