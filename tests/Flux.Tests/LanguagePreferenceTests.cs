using Flux.Core.Localization;
using Xunit;

namespace Flux.Tests;

public sealed class LanguagePreferenceTests
{
    [Theory]
    [InlineData("system")]
    [InlineData("SYSTEM")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void FollowSystemPassesARealLanguageTagToMrt(string? selection)
        => Assert.Equal("zh-CN", LanguagePreference.ResolveOverride(selection, ["zh-CN", "en-US"]));

    [Theory]
    [InlineData("en-US")]
    [InlineData("zh-TW")]
    [InlineData("ar")]
    public void ExplicitLanguageOverridesSystemPreference(string selection)
        => Assert.Equal(selection, LanguagePreference.ResolveOverride(selection, ["zh-CN"]));

    [Fact]
    public void ReturningToSystemUsesTheLatestSystemPreference()
    {
        Assert.Equal("en-US", LanguagePreference.ResolveOverride("en-US", ["zh-CN"]));
        Assert.Equal("zh-CN", LanguagePreference.ResolveOverride("system", ["zh-CN"]));
        Assert.Equal("fa-IR", LanguagePreference.ResolveOverride("system", ["fa-IR", "zh-CN"]));
    }

    [Fact]
    public void MissingSystemPreferencesNeverProduceAnEmptyOverride()
    {
        Assert.False(string.IsNullOrWhiteSpace(LanguagePreference.ResolveOverride("system", null)));
        Assert.False(string.IsNullOrWhiteSpace(LanguagePreference.ResolveOverride("system", [])));
        Assert.Equal("ar-SA", LanguagePreference.ResolveOverride("system", ["", " ", "ar-SA"]));
    }
}
