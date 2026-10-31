using Flux.Models;
using Flux.Services;
using Xunit;

namespace Flux.Tests;

public sealed class ConfigSettingsNotificationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "flux-settings-events", Guid.NewGuid().ToString("N"));
    private readonly string _previousPath = Paths.AppSettingsFile;

    public ConfigSettingsNotificationTests()
    {
        Directory.CreateDirectory(_directory);
        Paths.AppSettingsFile = Path.Combine(_directory, "settings.yaml");
    }

    [Fact]
    public void ToggleAndRollbackNotifyWithLatestPersistedProxyState()
    {
        var config = new ConfigService();
        var displayed = new List<bool>();
        config.SettingsChanged += () =>
        {
            var persisted = AppSettings.Deserialize(File.ReadAllText(Paths.AppSettingsFile));
            Assert.Equal(config.Settings.EnableSystemProxy, persisted.EnableSystemProxy);
            displayed.Add(config.Settings.EnableSystemProxy);
        };
        config.Settings.EnableSystemProxy = true;
        config.SaveSettings();
        config.Settings.EnableSystemProxy = false;
        config.SaveSettings();
        // 模拟关闭失败后，调用方保存回滚值。
        config.Settings.EnableSystemProxy = true;
        config.SaveSettings();
        Assert.Equal(new[] { true, false, true }, displayed);
    }

    [Fact]
    public void FailedSaveDoesNotPublishSettingsChanged()
    {
        var config = new ConfigService();
        var notifications = 0;
        config.SettingsChanged += () => notifications++;
        Paths.AppSettingsFile = _directory;
        Assert.Throws<UnauthorizedAccessException>(() => config.SaveSettings());
        Assert.Equal(0, notifications);
    }

    public void Dispose()
    {
        Paths.AppSettingsFile = _previousPath;
        Directory.Delete(_directory, recursive: true);
    }
}
