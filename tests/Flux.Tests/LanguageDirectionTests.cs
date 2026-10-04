using Flux.Core.Localization;
using Xunit;

namespace Flux.Tests;

public sealed class LanguageDirectionTests
{
    [Theory]
    [InlineData("ar", true)]
    [InlineData("ar-SA", true)]
    [InlineData("fa", true)]
    [InlineData("fa-IR", true)]
    [InlineData("en-US", false)]
    [InlineData("zh-CN", false)]
    [InlineData("zh-TW", false)]
    [InlineData("ja", false)]
    [InlineData("ko", false)]
    [InlineData("de", false)]
    [InlineData("es", false)]
    [InlineData("ru", false)]
    [InlineData("tr", false)]
    [InlineData("id", false)]
    [InlineData("tt", false)]
    public void SelectedLanguageDeterminesDirection(string language, bool rtl)
        => Assert.Equal(rtl, LanguageDirection.IsRightToLeft(language, "ar"));

    [Theory]
    [InlineData("system", "ar-EG", true)]
    [InlineData("system", "fa-IR", true)]
    [InlineData("system", "en-US", false)]
    [InlineData(null, "ar", true)]
    [InlineData("", "zh-CN", false)]
    [InlineData("SYSTEM", "fa", true)]
    public void FollowSystemUsesEffectiveApplicationLanguage(string? selected, string system, bool rtl)
        => Assert.Equal(rtl, LanguageDirection.IsRightToLeft(selected, system));

    [Fact]
    public void InvalidSavedLanguageFallsBackWithoutThrowing()
        => Assert.False(LanguageDirection.IsRightToLeft("invalid_language_!"));
}
