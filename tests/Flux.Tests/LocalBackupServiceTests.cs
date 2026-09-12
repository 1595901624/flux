using System.IO.Compression;
using Flux.Core.Backup;
using Xunit;

namespace Flux.Tests;

/// <summary>本地备份服务测试：创建/恢复/列表/删除/导入导出/目录穿越防护。</summary>
public class LocalBackupServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _dataDir;
    private readonly string _profilesDir;
    private readonly string _backupDir;
    private readonly LocalBackupService _service;

    public LocalBackupServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "flux-backup-tests", Guid.NewGuid().ToString("N"));
        _dataDir = Path.Combine(_root, "flux");
        _profilesDir = Path.Combine(_dataDir, "profiles");
        _backupDir = Path.Combine(_root, "backups");
        Directory.CreateDirectory(_dataDir);
        Directory.CreateDirectory(_profilesDir);
        Directory.CreateDirectory(_backupDir);
        File.WriteAllText(Path.Combine(_dataDir, "config.yaml"), "mixed-port: 7897\nsecret: abc\n");
        File.WriteAllText(Path.Combine(_dataDir, "verge.yaml"), "language: zh-CN\n");
        File.WriteAllText(Path.Combine(_dataDir, "profiles.yaml"), "current: u1\nitems: []\n");
        File.WriteAllText(Path.Combine(_profilesDir, "R111111.yaml"), "proxies: []\n");

        _service = new LocalBackupService(_backupDir, () => new BackupLayout
        {
            DataDir = _dataDir,
            ProfilesDir = _profilesDir,
        });
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public async Task CreateAndRestore_往返恢复内容()
    {
        var name = await _service.CreateAsync("测试备份");
        Assert.EndsWith(".zip", name);

        // 修改现场后恢复
        File.WriteAllText(Path.Combine(_dataDir, "config.yaml"), "mixed-port: 9999\n");
        File.WriteAllText(Path.Combine(_profilesDir, "R111111.yaml"), "proxies:\n  - changed\n");
        await _service.RestoreAsync(name);

        Assert.Equal("mixed-port: 7897\nsecret: abc\n", File.ReadAllText(Path.Combine(_dataDir, "config.yaml")));
        Assert.Equal("proxies: []\n", File.ReadAllText(Path.Combine(_profilesDir, "R111111.yaml")));
    }

    [Fact]
    public async Task List_列出备份并包含大小()
    {
        await _service.CreateAsync();
        var list = await _service.ListAsync();
        Assert.Single(list);
        Assert.True(list[0].Size > 0);
        Assert.Matches(@"^flux-\d{8}-\d{6}-\d{3}-[0-9a-f]{8}\.zip$", list[0].Name);
    }

    [Fact]
    public async Task Delete_删除备份文件()
    {
        var name = await _service.CreateAsync();
        Assert.True(await _service.DeleteAsync(name));
        Assert.False(await _service.DeleteAsync(name));
        Assert.Empty(await _service.ListAsync());
    }

    [Fact]
    public async Task Restore_不存在的备份_抛出()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => _service.RestoreAsync("flux-21000101-000000.zip"));
    }

    [Fact]
    public async Task Restore_缺少配置的ZIP_拒绝恢复()
    {
        var badPath = Path.Combine(_root, "empty.zip");
        using (var stream = File.Create(badPath))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("flux/only-log.txt");
            await File.WriteAllTextAsync(Path.Combine(_root, "log.txt"), "log");
        }
        var tempZip = Path.Combine(_backupDir, "flux-bad.zip");
        File.Copy(badPath, tempZip, true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RestoreAsync("flux-bad.zip"));
    }

    [Fact]
    public void ValidateEntryName_目录穿越_拒绝()
    {
        foreach (var evil in new[]
        {
            "../secret.txt", "flux/../../etc/passwd", "/abs/path.txt", "C:/Windows/win.ini", "flux/./x",
        })
        {
            Assert.Throws<InvalidOperationException>(() => LocalBackupService.ValidateEntryName(evil));
        }
        // 正常条目通过
        LocalBackupService.ValidateEntryName("flux/config.yaml");
        LocalBackupService.ValidateEntryName("flux/profiles/R1111.yaml");
    }

    [Fact]
    public async Task Restore_穿越ZIP_拒绝恢复()
    {
        var evilZip = Path.Combine(_backupDir, "flux-evil.zip");
        using (var stream = File.Create(evilZip))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("flux/../../../evil.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("pwned");
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RestoreAsync("flux-evil.zip"));
        // 未写入数据目录
        Assert.False(File.Exists(Path.Combine(_root, "evil.txt")));
    }

    [Fact]
    public async Task ImportExport_往返()
    {
        var name = await _service.CreateAsync();
        var exportDir = Path.Combine(_root, "export");
        var exported = await _service.ExportAsync(name, exportDir);
        Assert.True(File.Exists(exported));

        // 删除后导入
        await _service.DeleteAsync(name);
        var imported = await _service.ImportAsync(exported);
        Assert.EndsWith("-imported.zip", imported);
        Assert.Single(await _service.ListAsync());
    }

    [Fact]
    public async Task Import_非Flux备份_拒绝()
    {
        var foreign = Path.Combine(_root, "foreign.zip");
        using (var stream = File.Create(foreign))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            zip.CreateEntry("random.txt");
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ImportAsync(foreign));
    }

    [Fact]
    public async Task Restore_名称含路径_仅取文件名()
    {
        var name = await _service.CreateAsync();
        // 尝试通过文件名穿越（应被 Resolve 限制到备份目录内）
        var sneaky = Path.Combine("..", "..", name);
        await _service.RestoreAsync(sneaky); // 应成功（实际指向备份目录中的合法文件）
        Assert.Equal("mixed-port: 7897\nsecret: abc\n", File.ReadAllText(Path.Combine(_dataDir, "config.yaml")));
    }
}
