using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Flux.Core.Backup;

/// <summary>备份包含的文件清单描述：相对路径 → 绝对路径来源。</summary>
public sealed class BackupLayout
{
    /// <summary>数据目录（config.yaml / verge.yaml / profiles.yaml 等所在目录）。</summary>
    public required string DataDir { get; init; }

    /// <summary>订阅/增强文件目录。</summary>
    public required string ProfilesDir { get; init; }

    /// <summary>额外包含的独立文件（如 flux-settings.json）绝对路径。</summary>
    public IReadOnlyList<string> ExtraFiles { get; init; } = [];

    /// <summary>ZIP 内顶层目录名（默认 "flux"）。</summary>
    public string Root { get; init; } = "flux";
}

/// <summary>
/// 本地备份：ZIP 包含应用配置、订阅与增强文件；不包含日志、内核缓存与明文凭据。
/// </summary>
public sealed class LocalBackupService
{
    private readonly string _backupDir;
    private readonly Func<BackupLayout> _layoutFactory;
    private readonly Action<string, string>? _log;

    public LocalBackupService(string backupDir, Func<BackupLayout> layoutFactory, Action<string, string>? log = null)
    {
        _backupDir = backupDir;
        _layoutFactory = layoutFactory;
        _log = log;
    }

    // ---------- 创建 / 列表 / 删除 ----------

    public async Task<string> CreateAsync(string? comment = null)
    {
        Directory.CreateDirectory(_backupDir);
        var layout = _layoutFactory();
        var name = $"flux-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
        var path = Path.Combine(_backupDir, name);
        var temp = path + ".tmp";

        using (var stream = new FileStream(temp, FileMode.Create))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            AddFile(zip, layout, Path.Combine(layout.DataDir, "config.yaml"));
            AddFile(zip, layout, Path.Combine(layout.DataDir, "verge.yaml"));
            AddFile(zip, layout, Path.Combine(layout.DataDir, "profiles.yaml"));
            foreach (var extra in layout.ExtraFiles)
                AddFile(zip, layout, extra);

            if (Directory.Exists(layout.ProfilesDir))
            {
                foreach (var file in Directory.GetFiles(layout.ProfilesDir))
                    AddFile(zip, layout, file);
            }

            // 备份元数据
            var manifest = zip.CreateEntry($"{layout.Root}/manifest.json");
            var manifestPayload = JsonSerializer.Serialize(new
            {
                app = "Flux",
                created_at = DateTime.Now,
                schema = 1,
                comment = comment ?? "",
            }, new JsonSerializerOptions { WriteIndented = true });
            using var writer = new StreamWriter(manifest.Open(), Encoding.UTF8);
            await writer.WriteAsync(manifestPayload);
        }

        File.Move(temp, path, overwrite: true);
        _log?.Invoke("info", $"已创建备份: {name}");
        return name;
    }

    private static void AddFile(ZipArchive zip, BackupLayout layout, string filePath)
    {
        if (!File.Exists(filePath)) return;
        var relative = filePath.StartsWith(layout.DataDir, StringComparison.OrdinalIgnoreCase)
            ? filePath[(layout.DataDir.Length + 1)..]
            : Path.GetFileName(filePath);
        zip.CreateEntryFromFile(filePath, $"{layout.Root}/{relative.Replace('\\', '/')}", CompressionLevel.Optimal);
    }

    public Task<IReadOnlyList<(string Name, long Size, DateTime Created)>> ListAsync()
    {
        var result = new List<(string, long, DateTime)>();
        if (Directory.Exists(_backupDir))
        {
            foreach (var file in Directory.GetFiles(_backupDir, "flux-*.zip"))
            {
                var info = new FileInfo(file);
                result.Add((info.Name, info.Length, info.CreationTime));
            }
        }
        return Task.FromResult<IReadOnlyList<(string, long, DateTime)>>(result);
    }

    public Task<bool> DeleteAsync(string name)
    {
        var path = Resolve(name);
        if (File.Exists(path))
        {
            File.Delete(path);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    // ---------- 恢复 ----------

    /// <summary>恢复备份：先验证 ZIP 条目安全（防目录穿越），解包到临时目录，再原子替换。</summary>
    public async Task RestoreAsync(string name)
    {
        var path = Resolve(name);
        if (!File.Exists(path))
            throw new FileNotFoundException("备份文件不存在", name);

        var layout = _layoutFactory();
        var tempDir = Path.Combine(Path.GetTempPath(), "flux-restore-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var stream = File.OpenRead(path))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (var entry in zip.Entries)
                {
                    ValidateEntryName(entry.FullName);
                    if (!entry.FullName.StartsWith(layout.Root + "/", StringComparison.OrdinalIgnoreCase))
                        continue; // 只恢复 Flux 前缀的内容
                    var target = Path.Combine(tempDir, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                    if (entry.FullName.EndsWith('/')) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    await using var entryStream = entry.Open();
                    await using var targetStream = File.Create(target);
                    await entryStream.CopyToAsync(targetStream);
                }
            }

            var restoredData = Path.Combine(tempDir, layout.Root);
            var hasConfig = File.Exists(Path.Combine(restoredData, "config.yaml")) ||
                            File.Exists(Path.Combine(restoredData, "verge.yaml"));
            if (!hasConfig)
                throw new InvalidOperationException("备份内容不完整（缺少应用配置文件），已取消恢复");

            // 原子替换：先把现有文件移入回滚目录，成功后再清理
            var rollbackDir = Path.Combine(Path.GetTempPath(), "flux-rollback-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rollbackDir);
            foreach (var file in new[] { "config.yaml", "verge.yaml", "profiles.yaml" })
            {
                var source = Path.Combine(restoredData, file);
                var target = Path.Combine(layout.DataDir, file);
                if (!File.Exists(source)) continue;
                if (File.Exists(target))
                {
                    var backupPath = Path.Combine(rollbackDir, file);
                    File.Move(target, backupPath, overwrite: true);
                }
                File.Move(source, target, overwrite: true);
            }

            var profilesSource = Path.Combine(restoredData, "profiles");
            if (Directory.Exists(profilesSource))
            {
                foreach (var file in Directory.GetFiles(profilesSource))
                {
                    var target = Path.Combine(layout.ProfilesDir, Path.GetFileName(file));
                    if (File.Exists(target))
                        File.Move(target, Path.Combine(rollbackDir, "profiles-" + Path.GetFileName(file)), overwrite: true);
                    File.Move(file, target, overwrite: true);
                }
            }

            // 回滚目录留待下一次清理（不立即删除，供失败回退排查）
            _log?.Invoke("info", $"备份已恢复: {name}（回滚数据: {rollbackDir}）");
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    /// <summary>校验 ZIP 条目名：拒绝绝对路径、盘符与目录穿越。</summary>
    public static void ValidateEntryName(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
            throw new InvalidOperationException("ZIP 包含空条目名");
        var normalized = entryName.Replace('\\', '/');
        if (normalized.StartsWith('/') || normalized.StartsWith('~'))
            throw new InvalidOperationException($"ZIP 条目为绝对路径: {entryName}");
        if (System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^[A-Za-z]:"))
            throw new InvalidOperationException($"ZIP 条目包含盘符: {entryName}");
        foreach (var segment in normalized.Split('/'))
        {
            if (segment is ".." or ".")
                throw new InvalidOperationException($"ZIP 条目包含目录穿越: {entryName}");
        }
    }

    private string Resolve(string name)
    {
        // 防穿越：仅取文件名部分
        return Path.Combine(_backupDir, Path.GetFileName(name));
    }

    // ---------- 导入 / 导出 ----------

    public async Task<string> ImportAsync(string sourceZipPath)
    {
        if (!File.Exists(sourceZipPath))
            throw new FileNotFoundException("找不到要导入的备份文件", sourceZipPath);

        // 先验证内容
        using (var stream = File.OpenRead(sourceZipPath))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            if (!zip.Entries.Any(e => e.FullName.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("不是有效的 Flux 备份（缺少 manifest.json）");
            foreach (var entry in zip.Entries)
                ValidateEntryName(entry.FullName);
        }

        Directory.CreateDirectory(_backupDir);
        var name = $"flux-{DateTime.Now:yyyyMMdd-HHmmss}-imported.zip";
        File.Copy(sourceZipPath, Path.Combine(_backupDir, name), overwrite: true);
        _log?.Invoke("info", $"已导入备份: {name}");
        return name;
    }

    public Task<string> ExportAsync(string name, string targetDirectory)
    {
        var source = Resolve(name);
        if (!File.Exists(source))
            throw new FileNotFoundException("找不到备份文件", name);
        Directory.CreateDirectory(targetDirectory);
        var target = Path.Combine(targetDirectory, Path.GetFileName(name));
        File.Copy(source, target, overwrite: true);
        return Task.FromResult(target);
    }
}
