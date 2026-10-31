using Flux.Core.Compatibility;
using Flux.Models;
using Xunit;

namespace Flux.Tests;

public class SettingsMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "flux-settings-migration", Guid.NewGuid().ToString("N"));
    private string Legacy => Path.Combine(_root, LegacyCompatibility.SettingsFileName);
    private string Current => Path.Combine(_root, "settings.yaml");
    public SettingsMigrationTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void MigratesSettingsWithoutRemovingOriginalAndDoesNotRepeat()
    {
        const string content = "language: ja\nenable-tun-mode: true\nhotkeys:\n  show_hide_window: Ctrl+Shift+F1\n";
        File.WriteAllText(Legacy, content);
        LegacyCompatibility.MigrateSettingsFile(_root, text => AppSettings.Deserialize(text));
        Assert.Equal(content, File.ReadAllText(Current));
        Assert.Equal(content, File.ReadAllText(Legacy));
        File.WriteAllText(Current, "language: de");
        LegacyCompatibility.MigrateSettingsFile(_root);
        Assert.Equal("language: de", File.ReadAllText(Current));
    }

    [Theory]
    [InlineData("[not, a, mapping]")]
    [InlineData("language: [invalid")]
    public void InvalidLegacyFileDoesNotCreateNewSettings(string content)
    {
        File.WriteAllText(Legacy, content);
        Assert.Throws<InvalidDataException>(() => LegacyCompatibility.MigrateSettingsFile(_root));
        Assert.False(File.Exists(Current));
        Assert.Equal(content, File.ReadAllText(Legacy));
    }

    [Fact]
    public void ModelValidationFailureDoesNotWriteDestination()
    {
        File.WriteAllText(Legacy, "auto-lightweight-minutes: invalid");
        Assert.ThrowsAny<Exception>(() => LegacyCompatibility.MigrateSettingsFile(_root,
            text => AppSettings.Deserialize(text)));
        Assert.False(File.Exists(Current));
        Assert.True(File.Exists(Legacy));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
