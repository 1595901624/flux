using Flux.Services;
using Xunit;

namespace Flux.Tests;

public class SystemProxyOwnershipTests
{
    [Theory]
    [InlineData(true, "127.0.0.1:7897", "127.0.0.1:7897", true)]
    [InlineData(true, "127.0.0.1:7898", "127.0.0.1:7897", false)]
    [InlineData(false, "127.0.0.1:7897", "127.0.0.1:7897", false)]
    [InlineData(true, "OTHER:7897", "other:7897", true)]
    public void RecognizesOnlyTheProxyOwnedByFlux(
        bool enabled, string current, string expected, bool owned)
    {
        Assert.Equal(owned, SystemProxyOwnership.IsOwned(enabled, current, expected));
    }
}
