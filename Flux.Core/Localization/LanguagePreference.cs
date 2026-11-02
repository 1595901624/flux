using System.Globalization;

namespace Flux.Core.Localization;

/// <summary>MRT Core 需要有效的语言标签，不能用空字符串表示跟随系统。</summary>
public static class LanguagePreference
{
    public static bool FollowsSystem(string? language) =>
        string.IsNullOrWhiteSpace(language) || language.Equals("system", StringComparison.OrdinalIgnoreCase);

    public static string ResolveOverride(string? selectedLanguage, IEnumerable<string>? systemLanguages)
    {
        // 兼容旧版本保存的 tt；Windows 包资源要求明确使用西里尔文字标签。
        if (string.Equals(selectedLanguage, "tt", StringComparison.OrdinalIgnoreCase)) return "tt-Cyrl";
        if (!FollowsSystem(selectedLanguage)) return selectedLanguage!;

        var preferred = systemLanguages?.FirstOrDefault(language => !string.IsNullOrWhiteSpace(language));
        if (preferred is not null) return preferred;

        // InstalledUICulture 不受应用已选语言影响；不使用可能被覆盖的应用语言列表。
        var fallback = CultureInfo.InstalledUICulture.Name;
        return string.IsNullOrWhiteSpace(fallback) ? "en-US" : fallback;
    }
}
