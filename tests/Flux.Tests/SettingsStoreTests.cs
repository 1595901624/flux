using Flux.Core.Settings;
using Xunit;

namespace Flux.Tests;

/// <summary>设置存储测试：默认创建、往返、迁移备份、损坏隔离、原子写入。</summary>
public class SettingsStoreTests : IDisposable
{
    private readonly string _dir;

    public SettingsStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "flux-settings-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void Load_无文件_创建默认设置()
    {
        var store = new SettingsStore(_dir);
        var result = store.Load();

        Assert.True(result.Success);
        Assert.Equal(FluxSettings.CurrentSchemaVersion, store.Settings.SchemaVersion);
        Assert.True(File.Exists(Path.Combine(_dir, "flux-settings.json")));
        Assert.Equal(7897, store.Settings.Core.MixedPort);
    }

    [Fact]
    public void SaveLoad_往返保留全部子对象()
    {
        var store = new SettingsStore(_dir);
        store.Load();
        store.Settings.General.Language = "en";
        store.Settings.Core.MixedPort = 8000;
        store.Settings.Tun.Enable = true;
        store.Settings.Tun.Stack = "system";
        store.Settings.Test.TestList.Add(new TestItem { Uid = "x", Name = "X", Url = "https://x" });
        Assert.True(store.Save().Success);

        var store2 = new SettingsStore(_dir);
        Assert.True(store2.Load().Success);
        Assert.Equal("en", store2.Settings.General.Language);
        Assert.Equal(8000, store2.Settings.Core.MixedPort);
        Assert.True(store2.Settings.Tun.Enable);
        Assert.Equal("system", store2.Settings.Tun.Stack);
        Assert.Equal(3, store2.Settings.Test.TestList.Count); // 默认 2 + 新增 1
    }

    [Fact]
    public void Load_旧版本文件_执行迁移并备份()
    {
        var oldJson = """
            {
              "schema-version": 0,
              "general": { "language": "ja" },
              "core": { "mixedPort": 9999 }
            }
            """;
        File.WriteAllText(Path.Combine(_dir, "flux-settings.json"), oldJson);

        var store = new SettingsStore(_dir);
        Assert.True(store.Load().Success);

        Assert.Equal(FluxSettings.CurrentSchemaVersion, store.Settings.SchemaVersion);
        Assert.Equal("ja", store.Settings.General.Language);
        Assert.Equal(9999, store.Settings.Core.MixedPort);

        var backups = Directory.GetFiles(Path.Combine(_dir, "settings-backup"), "*-v0-flux-settings.json");
        Assert.NotEmpty(backups);
    }

    [Fact]
    public void Load_损坏文件_隔离并恢复默认()
    {
        File.WriteAllText(Path.Combine(_dir, "flux-settings.json"), "{ this is not json !!!");

        var store = new SettingsStore(_dir);
        Assert.True(store.Load().Success);

        Assert.Equal(7897, store.Settings.Core.MixedPort); // 默认值
        var isolated = Directory.GetFiles(Path.Combine(_dir, "settings-backup"), "*corrupted*");
        Assert.NotEmpty(isolated);
        // 隔离后文件重新生成
        Assert.True(File.Exists(Path.Combine(_dir, "flux-settings.json")));
    }

    [Fact]
    public void Save_原子写入_不留临时文件()
    {
        var store = new SettingsStore(_dir);
        store.Load();
        store.Save();

        var leftovers = Directory.GetFiles(_dir, ".*tmp*");
        Assert.Empty(leftovers);
    }

    [Fact]
    public void Save_触发变更事件()
    {
        var store = new SettingsStore(_dir);
        store.Load();
        var fired = false;
        store.SettingsChanged += _ => fired = true;
        store.Save();
        Assert.True(fired);
    }

    [Fact]
    public void WebDav密码_DPAPI往返且明文不落盘()
    {
        var store = new SettingsStore(_dir);
        store.Load();
        store.SetWebDavPassword("secret-password-123");
        Assert.True(store.Save().Success);

        var onDisk = File.ReadAllText(Path.Combine(_dir, "flux-settings.json"));
        Assert.DoesNotContain("secret-password-123", onDisk);

        var store2 = new SettingsStore(_dir);
        store2.Load();
        Assert.Equal("secret-password-123", store2.GetWebDavPassword());
    }

    [Fact]
    public void 未知字段_反序列化时被忽略()
    {
        var json = """
            {
              "schema-version": 1,
              "general": { "language": "de", "unknownFutureField": 42 },
              "core": { "mixedPort": 7000 }
            }
            """;
        File.WriteAllText(Path.Combine(_dir, "flux-settings.json"), json);

        var store = new SettingsStore(_dir);
        Assert.True(store.Load().Success);
        Assert.Equal("de", store.Settings.General.Language);
        Assert.Equal(7000, store.Settings.Core.MixedPort);
    }
}
