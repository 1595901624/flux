using System.Globalization;

namespace Flux.Core.Localization;

/// <summary>按选定语言判断排版方向；跟随系统时使用有效应用语言。</summary>
public static class LanguageDirection
{
    public static bool IsRightToLeft(string? selectedLanguage, string? systemLanguage = null)
    {
        var tag = string.IsNullOrWhiteSpace(selectedLanguage) || selectedLanguage.Equals("system", StringComparison.OrdinalIgnoreCase)
            ? systemLanguage ?? CultureInfo.CurrentUICulture.Name
            : selectedLanguage;
        try { return CultureInfo.GetCultureInfo(tag).TextInfo.IsRightToLeft; }
        catch (CultureNotFoundException) { return false; }
    }
}
