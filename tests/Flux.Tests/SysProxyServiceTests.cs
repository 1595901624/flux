using System.Text.Json;
using Flux.Services;
using Xunit;

namespace Flux.Tests;

public sealed class SysProxyServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "flux-proxy-tests", Guid.NewGuid().ToString("N"));
    private string Snapshot => Path.Combine(_directory, "state.json");
    private WinInetProxySettings.State _state = new(false, true, "", "", "file:///flux/proxy.pac");

    public SysProxyServiceTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Snapshot, JsonSerializer.Serialize(new
        {
            OriginalEnable = true, OriginalServer = "company:8080", OriginalBypass = "intranet",
            FluxServer = "127.0.0.1:7897", FluxPacUrl = "file:///flux/proxy.pac",
        }));
    }

    private SysProxyService Create(bool writeFails = false) => new(Snapshot, () => _state,
        (enabled, server, bypass) =>
        {
            if (writeFails) throw new IOException("write failed");
            _state = new(enabled, false, server, bypass, "");
        },
        (enabled, url) => _state = new(false, enabled, "", "", url),
        state =>
        {
            if (writeFails) throw new IOException("restore failed");
            _state = state;
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RestoresOriginalPacAndAutoDetectOnResetOrNextStartup(bool startup)
    {
        var original = new WinInetProxySettings.State(true, true, "company:8080", "intranet",
            "https://company/proxy.pac", true);
        _state = original;
        File.Delete(Snapshot);
        var service = Create();
        service.Apply(new Flux.Models.VergeConfig { EnableSystemProxy = true, EnableProxyGuard = false });
        Assert.False(_state.PacEnabled);
        Assert.Equal("127.0.0.1:7897", _state.Server);
        if (startup) Create().ClearStaleProxy();
        else service.Reset();
        Assert.Equal(original, _state);
        Assert.False(File.Exists(Snapshot));
    }

    [Fact]
    public void LegacySnapshotPreservesCurrentAutoDetect()
    {
        _state = _state with { AutoDetect = true };
        Create().ClearStaleProxy();
        Assert.True(_state.AutoDetect);
    }

    [Fact]
    public void StartupRecoversOwnedPacAndRemovesSnapshotAfterRecovery()
    {
        Create().ClearStaleProxy();
        Assert.True(_state.Enable);
        Assert.False(_state.PacEnabled);
        Assert.Equal("company:8080", _state.Server);
        Assert.Equal("intranet", _state.Bypass);
        Assert.False(File.Exists(Snapshot));
    }

    [Fact]
    public void StartupDoesNotOverwritePacChangedByAnotherApp()
    {
        _state = _state with { AutoConfigUrl = "https://company/proxy.pac" };
        Create().ClearStaleProxy();
        Assert.True(_state.PacEnabled);
        Assert.Equal("https://company/proxy.pac", _state.AutoConfigUrl);
    }

    [Fact]
    public void StartupKeepsRecoverySnapshotWhenProxyWriteFails()
    {
        Create(writeFails: true).ClearStaleProxy();
        Assert.True(File.Exists(Snapshot));
        Assert.Equal("file:///flux/proxy.pac", _state.AutoConfigUrl);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
