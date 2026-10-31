using Flux.Services;
using Xunit;

namespace Flux.Tests;

// 使用真实 Win32 注册验证窗口线程与事务行为，不发送键盘输入。
public class HotkeyServiceTests
{
    private const string Old = "Ctrl+Alt+Shift+F20";
    private const string New = "Ctrl+Alt+Shift+F21";
    private const string Occupied = "Ctrl+Alt+Shift+F22";

    private static IReadOnlyList<string> Apply(HotkeyService service, params (string Action, string Combo)[] entries)
        => service.ApplyHotkeys(entries.ToDictionary(e => e.Action, e => e.Combo)).GetValueOrThrow();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidOrDuplicateInputKeepsOldRegistration(bool duplicate)
    {
        using var service = new HotkeyService();
        using var other = new HotkeyService();
        Assert.Empty(Apply(service, ("old", Old)));
        var errors = duplicate
            ? Apply(service, ("one", New), ("two", New))
            : Apply(service, ("one", "invalid"));
        Assert.NotEmpty(errors);
        Assert.NotEmpty(Apply(other, ("probe", Old)));
        Assert.Empty(Apply(other, ("probe", New)));
    }

    [Fact]
    public void NativeConflictKeepsOldRegistrationAndReleasesStagedKeys()
    {
        using var service = new HotkeyService();
        using var blocker = new HotkeyService();
        using var probe = new HotkeyService();
        Assert.Empty(Apply(service, ("old", Old)));
        Assert.Empty(Apply(blocker, ("occupied", Occupied)));
        Assert.NotEmpty(Apply(service, ("new", New), ("conflict", Occupied)));
        Assert.NotEmpty(Apply(probe, ("old-probe", Old)));
        Assert.Empty(Apply(probe, ("new-probe", New)));
    }

    [Fact]
    public void SuccessfulReplacementReusesCombinationAndReleasesRemovedKeys()
    {
        using var service = new HotkeyService();
        using var probe = new HotkeyService();
        Assert.Empty(Apply(service, ("first", Old), ("second", New)));
        Assert.Empty(Apply(service, ("reassigned", New)));
        Assert.Empty(Apply(probe, ("released", Old)));
        Assert.NotEmpty(Apply(probe, ("still-used", New)));
        service.Clear();
        Assert.Empty(Apply(probe, ("now-free", New)));
    }

    [Fact]
    public void DisposeReleasesNativeRegistrations()
    {
        using var service = new HotkeyService();
        Assert.Empty(Apply(service, ("first", Old)));
        service.Dispose();
        using var probe = new HotkeyService();
        Assert.Empty(Apply(probe, ("released", Old)));
        Assert.Throws<ObjectDisposedException>(() => Apply(service, ("invalid", New)));
    }
}
