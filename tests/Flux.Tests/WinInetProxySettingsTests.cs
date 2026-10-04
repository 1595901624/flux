using Flux.Services;
using Xunit;

namespace Flux.Tests;

public class WinInetProxySettingsTests
{
    [Theory]
    [InlineData(false, false, false, 1)]
    [InlineData(true, false, false, 3)]
    [InlineData(false, true, false, 5)]
    [InlineData(true, true, true, 15)]
    public void RestoreFlagsPreserveAllProxyModes(bool manual, bool pac, bool autoDetect, int expected)
    {
        var state = new WinInetProxySettings.State(manual, pac, "server", "bypass", "pac", autoDetect);
        Assert.Equal(expected, WinInetProxySettings.GetRestoreFlags(state));
    }

    [Fact]
    public void ReadsCurrentLanProxyWithoutRegistryAccess()
    {
        if (!OperatingSystem.IsWindows()) return;

        var state = WinInetProxySettings.Read();

        Assert.NotNull(state.Server);
        Assert.NotNull(state.Bypass);
    }
}
