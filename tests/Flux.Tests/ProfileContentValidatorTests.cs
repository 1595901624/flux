using Flux.Services;
using Xunit;

namespace Flux.Tests;

public class ProfileContentValidatorTests
{
    [Theory]
    [InlineData("proxies:\n  - name: demo\n    type: direct")]
    [InlineData("proxy-providers:\n  provider:\n    type: http\n    url: https://example.com/sub")]
    public void AcceptsClashProfiles(string yaml) => ProfileContentValidator.Validate(yaml);

    [Theory]
    [InlineData("rules:\n  - MATCH,DIRECT")]
    [InlineData("not: [valid")]
    [InlineData("- just\n- a\n- list")]
    public void RejectsInvalidProfiles(string yaml) =>
        Assert.Throws<InvalidOperationException>(() => ProfileContentValidator.Validate(yaml));
}
