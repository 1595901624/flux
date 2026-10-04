using Flux.Models;
using Flux.Services;
using Xunit;

namespace Flux.Tests;

public sealed class ConfigSettingsNotificationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "flux-settings-events", Guid.NewGuid().ToString("N"));
    private readonly string _previousPath = Paths.VergeConfigFile;

    public ConfigSettingsNotificationTests()
    {
        Directory.CreateDirectory(_directory);
        Paths.VergeConfigFile = Path.Combine(_directory, "verge.yaml");
    }

    [Fact]
    public void ToggleAndRollbackNotifyWithLatestPersistedProxyState()
    {
        var config = new ConfigService();
        var displayed = new List<bool>();
        config.SettingsChanged += () =>
        {
            var persisted = VergeConfig.Deserialize(File.ReadAllText(Paths.VergeConfigFile));
            Assert.Equal(config.Verge.EnableSystemProxy, persisted.EnableSystemProxy);
            displayed.Add(config.Verge.EnableSystemProxy);
        };
        config.Verge.EnableSystemProxy = true;
        config.SaveVerge();
        config.Verge.EnableSystemProxy = false;
        config.SaveVerge();
        // 模拟关闭失败后，调用方保存回滚值。
        config.Verge.EnableSystemProxy = true;
        config.SaveVerge();
        Assert.Equal(new[] { true, false, true }, displayed);
    }

    [Fact]
    public void FailedSaveDoesNotPublishSettingsChanged()
    {
        var config = new ConfigService();
        var notifications = 0;
        config.SettingsChanged += () => notifications++;
        Paths.VergeConfigFile = _directory;
        Assert.Throws<UnauthorizedAccessException>(() => config.SaveVerge());
        Assert.Equal(0, notifications);
    }

    public void Dispose()
    {
        Paths.VergeConfigFile = _previousPath;
        Directory.Delete(_directory, recursive: true);
    }
}
