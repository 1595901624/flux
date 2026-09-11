using Flux.Services;
using Xunit;

namespace Flux.Tests;

public class WinInetProxySettingsTests
{
    [Fact]
    public void ReadsCurrentLanProxyWithoutRegistryAccess()
    {
        if (!OperatingSystem.IsWindows()) return;

        var state = WinInetProxySettings.Read();

        Assert.NotNull(state.Server);
        Assert.NotNull(state.Bypass);
    }
}
