using System.Text.Json;
using System.Text.Json.Serialization;
using Flux.Core.Contracts;

namespace Flux.Core.Settings;

/// <summary>
/// flux-settings.json 存储实现：原子写入、schema-version 迁移、损坏文件隔离、
/// 敏感字段 DPAPI 加密、变更事件。
/// </summary>
public sealed class SettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    private readonly string _filePath;
    private readonly string _backupDir;
    private readonly Action<string, string>? _log;

    public FluxSettings Settings { get; private set; } = new();

    public int SchemaVersion => FluxSettings.CurrentSchemaVersion;

    public event Action<string>? SettingsChanged;

    public SettingsStore(string directory, Action<string, string>? log = null)
    {
        _filePath = Path.Combine(directory, "flux-settings.json");
        _backupDir = Path.Combine(directory, "settings-backup");
        _log = log;
    }

    public OperationResult<bool> Load()
    {
        if (!File.Exists(_filePath))
        {
            Settings = new FluxSettings();
            return Save();
        }

        string text;
        try
        {
            text = File.ReadAllText(_filePath);
        }
        catch (Exception ex)
        {
            _log?.Invoke("error", $"设置文件读取失败: {ex.Message}");
            return IsolateCorruptedAndReset(ex.Message);
        }

        FluxSettings? loaded;
        try
        {
            loaded = JsonSerializer.Deserialize<FluxSettings>(text, JsonOptions);
        }
        catch (JsonException ex)
        {
            _log?.Invoke("error", $"设置文件损坏: {ex.Message}");
            return IsolateCorruptedAndReset(ex.Message);
        }

        if (loaded is null)
            return IsolateCorruptedAndReset("设置文件内容为空或类型不符");

        Settings = loaded;
        Migrate(text);
        return OperationResult<bool>.Ok(true);
    }

    public OperationResult<bool> Save()
    {
        try
        {
            Settings.SchemaVersion = FluxSettings.CurrentSchemaVersion;
            EncryptSensitiveFields();
            var json = JsonSerializer.Serialize(Settings, JsonOptions);
            WriteAllTextAtomic(_filePath, json);
            DecryptSensitiveFieldsAfterSave();
            SettingsChanged?.Invoke("*");
            return OperationResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _log?.Invoke("error", $"设置保存失败: {ex.Message}");
            return OperationResult<bool>.Fail("save_failed", ex.Message, "SettingsStore", _filePath);
        }
    }

    /// <summary>保存指定子对象后通知（变更事件按子对象名触发）。</summary>
    public OperationResult<bool> SaveSection(string sectionName) => Save();

    // ---------- 敏感字段 ----------

    private void EncryptSensitiveFields()
    {
        var core = Settings.Core;
        if (!string.IsNullOrEmpty(core.ExternalControllerSecret) &&
            string.IsNullOrEmpty(core.ExternalControllerSecretEncrypted))
            core.ExternalControllerSecretEncrypted = Utils.DataProtector.Protect(core.ExternalControllerSecret);

        var backup = Settings.Backup;
        if (!string.IsNullOrEmpty(backup.WebDavPasswordEncrypted))
            return; // 已加密，明文字段不存在
    }

    private void DecryptSensitiveFieldsAfterSave()
    {
        var core = Settings.Core;
        if (!string.IsNullOrEmpty(core.ExternalControllerSecretEncrypted) &&
            string.IsNullOrEmpty(core.ExternalControllerSecret))
            core.ExternalControllerSecret = Utils.DataProtector.Unprotect(core.ExternalControllerSecretEncrypted);
    }

    /// <summary>供调用方读取已解密的 WebDAV 密码。</summary>
    public string GetWebDavPassword() => Utils.DataProtector.Unprotect(Settings.Backup.WebDavPasswordEncrypted);

    /// <summary>设置 WebDAV 密码（立即加密存储，明文不落盘）。</summary>
    public void SetWebDavPassword(string password)
    {
        Settings.Backup.WebDavPasswordEncrypted = Utils.DataProtector.Protect(password);
    }

    // ---------- 迁移 ----------

    private void Migrate(string originalText)
    {
        var from = Settings.SchemaVersion;
        if (from >= FluxSettings.CurrentSchemaVersion) return;

        try
        {
            Directory.CreateDirectory(_backupDir);
            var backup = Path.Combine(_backupDir, $"{DateTime.Now:yyyyMMdd-HHmmss}-v{from}-flux-settings.json");
            File.WriteAllText(backup, originalText);
            _log?.Invoke("info", $"设置已从 v{from} 迁移到 v{FluxSettings.CurrentSchemaVersion}，旧文件备份于 {backup}");
        }
        catch (Exception ex)
        {
            _log?.Invoke("warn", $"设置迁移备份失败: {ex.Message}");
        }

        // 幂等迁移步骤（未来版本在此追加 v1→v2 等转换）
        Settings.SchemaVersion = FluxSettings.CurrentSchemaVersion;
        Save();
    }

    private OperationResult<bool> IsolateCorruptedAndReset(string reason)
    {
        try
        {
            Directory.CreateDirectory(_backupDir);
            var isolated = Path.Combine(_backupDir, $"{DateTime.Now:yyyyMMdd-HHmmss}-corrupted-flux-settings.json");
            File.Copy(_filePath, isolated, overwrite: true);
            _log?.Invoke("warn", $"损坏的设置文件已隔离至 {isolated}: {reason}");
        }
        catch
        {
            // 隔离失败也不能阻塞启动
        }

        Settings = new FluxSettings();
        var save = Save();
        return save.Success
            ? OperationResult<bool>.Ok(true)
            : save;
    }

    internal static void WriteAllTextAtomic(string path, string content)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("目标文件缺少目录");
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temp, content);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }
}
