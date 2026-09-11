using Microsoft.Windows.ApplicationModel.Resources;

namespace Flux.Services;

/// <summary>
/// 本地化加载器：从 resources.pri 读取 Strings/&lt;lang&gt;/Resources.resw。
/// 回退链：当前语言 → 默认语言（en-US，MRT 自动处理）→ 资源键本身。
/// </summary>
public static class L10n
{
    private static ResourceLoader? _loader;

    public static ResourceLoader Loader => _loader ??= new ResourceLoader();

    /// <summary>按键取文本；全部语言缺失时返回键名本身（不显示空文本）。</summary>
    public static string T(string key)
    {
        try
        {
            var value = Loader.GetString(key);
            return string.IsNullOrEmpty(value) ? key : value;
        }
        catch
        {
            return key;
        }
    }

    /// <summary>格式化文本（L10n.T 后 string.Format）。</summary>
    public static string F(string key, params object[] args) =>
        string.Format(T(key), args);
}
