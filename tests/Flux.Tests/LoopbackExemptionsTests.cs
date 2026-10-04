using System.Text;
using Flux.Core.Proxy;
using Xunit;

namespace Flux.Tests;

public sealed class LoopbackExemptionsTests
{
    private const string Store = "Microsoft.WindowsStore_8wekyb3d8bbwe";
    private const string Weather = "Microsoft.BingWeather_8wekyb3d8bbwe";

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf16")]
    [InlineData("utf16bom")]
    [InlineData("utf16be")]
    [InlineData("local")]
    public void ReopenedListRecognizesSavedFamiliesRegardlessOfOutputEncoding(string format)
    {
        var text = $"[1]\r\n名称: {Store.ToLowerInvariant()}\r\nSID: S-1-15-2-123\r\n";
        byte[] bytes = format switch
        {
            "utf16" => Encoding.Unicode.GetBytes(text),
            "utf16bom" => [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(text)],
            "utf16be" => [.. Encoding.BigEndianUnicode.GetPreamble(), .. Encoding.BigEndianUnicode.GetBytes(text)],
            // Actual Chinese Windows output starts with GBK bytes, followed by ASCII family names.
            "local" => [0xc1, 0xd0, .. Encoding.ASCII.GetBytes($"\r\nName: {Store}\r\n")],
            _ => Encoding.UTF8.GetBytes(text),
        };
        Assert.Contains(LoopbackExemptions.ParseOutput(bytes), family => family.Equals(Store, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EachChangeHasItsOwnCommandAndStopsOnFailure()
    {
        var script = LoopbackExemptions.BuildScript(@"C:\Windows\System32\CheckNetIsolation.exe", [Store, Weather], ["Example.App_abcdefghijklm"]);
        var commands = script.Split("\r\n").Where(line => line.Contains("LoopbackExempt")).ToArray();
        Assert.Equal(3, commands.Length);
        Assert.All(commands, line => Assert.Equal(1, line.Split("-n=").Length - 1));
        Assert.Contains(" -a -n=" + Store, commands[0]);
        Assert.Contains(" -d -n=Example.App_abcdefghijklm", commands[2]);
        Assert.Equal(3, script.Split("if errorlevel 1 exit /b %errorlevel%").Length - 1);
    }

    [Theory]
    [InlineData("Example.App_abcdefghijklm & calc")]
    [InlineData("Example.App_abcdefghijklm\r\nexit")]
    [InlineData("Example.App_abcdefghijklm%PATH%")]
    public void RejectsShellCharactersInPackageNames(string family)
        => Assert.Throws<ArgumentException>(() => LoopbackExemptions.BuildScript("CheckNetIsolation.exe", [family], []));

    [Fact]
    public void VerificationDetectsPartialApplyWithoutRemovingUnlistedExemptions()
    {
        var selected = new Dictionary<string, bool> { [Store] = true, [Weather] = false };
        var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Store.ToLowerInvariant(), "Unlisted.App_abcdefghijklm" };
        Assert.True(LoopbackExemptions.MatchesSelection(selected, actual));
        actual.Add(Weather);
        Assert.False(LoopbackExemptions.MatchesSelection(selected, actual));
        actual.Remove(Weather);
        actual.Remove(Store);
        Assert.False(LoopbackExemptions.MatchesSelection(selected, actual));
    }
}
