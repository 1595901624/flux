using Flux.Core.Localization;

namespace Flux.Services;

public static class ApplicationLanguage
{
    public static void Apply(string? selectedLanguage)
    {
        // 每次重新读取系统首选项，避免从应用语言列表读回上一次手动选择的语言。
        IReadOnlyList<string>? systemLanguages = null;
        if (LanguagePreference.FollowsSystem(selectedLanguage))
        {
            try { systemLanguages = Windows.System.UserProfile.GlobalizationPreferences.Languages; }
            catch { /* 无法读取用户首选项时由 ResolveOverride 使用系统 UI 语言。 */ }
        }

        Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride =
            LanguagePreference.ResolveOverride(selectedLanguage, systemLanguages);
        L10n.Reset();
    }
}
