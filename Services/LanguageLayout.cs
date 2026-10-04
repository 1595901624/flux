using Flux.Core.Localization;
using Microsoft.UI.Xaml;

namespace Flux.Services;

public static class LanguageLayout
{
    public static FlowDirection Current
    {
        get
        {
            string? systemLanguage = null;
            try { systemLanguage = Microsoft.Windows.Globalization.ApplicationLanguages.Languages.FirstOrDefault(); }
            catch { /* 无应用语言信息时回退到 CurrentUICulture。 */ }
            return LanguageDirection.IsRightToLeft(AppServices.Config?.Verge.Language, systemLanguage)
                ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }
    }
}
