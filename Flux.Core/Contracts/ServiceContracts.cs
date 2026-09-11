namespace Flux.Core.Contracts;

/// <summary>设置存储契约：原子保存、版本迁移、字段校验、变更事件和敏感字段加密。</summary>
public interface ISettingsStore
{
    /// <summary>当前 schema 版本（写入文件的 schema-version）。</summary>
    int SchemaVersion { get; }

    /// <summary>加载设置并执行幂等迁移；损坏文件被隔离后恢复默认值。</summary>
    OperationResult<bool> Load();

    /// <summary>原子保存设置。敏感字段先加密。</summary>
    OperationResult<bool> Save();

    /// <summary>任意设置保存后触发；参数为变更的顶层子对象名（如 "general"、"proxy"）。</summary>
    event Action<string>? SettingsChanged;
}

/// <summary>订阅更新通道。</summary>
public enum ProfileUpdateChannel
{
    /// <summary>直连下载。</summary>
    Direct,
    /// <summary>经由系统代理下载。</summary>
    SystemProxy,
    /// <summary>经由本应用内核混合端口下载。</summary>
    CoreProxy,
    /// <summary>依次尝试：直连 → 内核代理 → 系统代理。</summary>
    Auto,
}

/// <summary>订阅上次更新状态。</summary>
public enum ProfileUpdateStatus
{
    Unknown,
    Success,
    NotModified,
    Failed,
}

/// <summary>
/// 订阅服务契约：导入、新建、编辑、选择、批量删除、排序、更新、全部更新、重新激活。
/// </summary>
public interface IProfileService
{
    /// <summary>订阅集合发生变化（导入/删除/更新/选择/排序）。</summary>
    event Action? ProfilesChanged;

    /// <summary>从 URL 导入远程订阅。</summary>
    Task<OperationResult<string>> ImportUrlAsync(string url, string? name, CancellationToken ct = default);

    /// <summary>导入本地 YAML 文件。</summary>
    Task<OperationResult<string>> ImportLocalFileAsync(string filePath, string? name, CancellationToken ct = default);

    /// <summary>新建空配置（本地订阅）。</summary>
    Task<OperationResult<string>> CreateEmptyAsync(string name, CancellationToken ct = default);

    /// <summary>更新订阅（按更新通道下载；304 视为未修改）。</summary>
    Task<OperationResult<bool>> UpdateAsync(string uid, CancellationToken ct = default);

    /// <summary>全部更新；返回每个订阅的更新结果。</summary>
    Task<IReadOnlyList<(string Uid, OperationResult<bool> Result)>> UpdateAllAsync(CancellationToken ct = default);

    /// <summary>删除订阅（支持批量）。</summary>
    Task<OperationResult<bool>> DeleteAsync(IReadOnlyList<string> uids, CancellationToken ct = default);

    /// <summary>选择当前订阅并重新激活运行时配置。</summary>
    Task<OperationResult<bool>> SelectAsync(string uid, CancellationToken ct = default);

    /// <summary>强制重新激活当前订阅（不重新下载）。</summary>
    Task<OperationResult<bool>> ReactivateAsync(CancellationToken ct = default);

    /// <summary>编辑订阅信息（名称、描述、URL、UA、超时、间隔、更新通道等）。</summary>
    Task<OperationResult<bool>> EditInfoAsync(string uid, Action<object> patch, CancellationToken ct = default);
}

/// <summary>备份元数据。</summary>
public sealed record BackupEntry(string FileName, long SizeBytes, DateTime CreatedAt, string? Comment);

/// <summary>备份服务契约：本地备份、导入导出、恢复、历史记录、WebDAV。</summary>
public interface IBackupService
{
    /// <summary>创建本地备份（ZIP），返回备份文件名。</summary>
    Task<OperationResult<string>> CreateLocalBackupAsync(string? comment, CancellationToken ct = default);

    /// <summary>列出本地备份。</summary>
    Task<OperationResult<IReadOnlyList<BackupEntry>>> ListLocalBackupsAsync(CancellationToken ct = default);

    /// <summary>删除本地备份。</summary>
    Task<OperationResult<bool>> DeleteLocalBackupAsync(string fileName, CancellationToken ct = default);

    /// <summary>从本地备份恢复；先验证 ZIP 再原子替换。</summary>
    Task<OperationResult<bool>> RestoreLocalBackupAsync(string fileName, CancellationToken ct = default);

    /// <summary>导出备份到指定目录。</summary>
    Task<OperationResult<string>> ExportAsync(string fileName, string targetDirectory, CancellationToken ct = default);

    /// <summary>从外部 ZIP 导入为备份。</summary>
    Task<OperationResult<string>> ImportAsync(string zipPath, CancellationToken ct = default);

    /// <summary>上传最新或指定备份到 WebDAV。</summary>
    Task<OperationResult<bool>> UploadToWebDavAsync(string? fileName, CancellationToken ct = default);

    /// <summary>列出 WebDAV 备份。</summary>
    Task<OperationResult<IReadOnlyList<BackupEntry>>> ListWebDavAsync(CancellationToken ct = default);

    /// <summary>从 WebDAV 下载并恢复。</summary>
    Task<OperationResult<bool>> RestoreFromWebDavAsync(string fileName, CancellationToken ct = default);

    /// <summary>删除 WebDAV 备份。</summary>
    Task<OperationResult<bool>> DeleteWebDavAsync(string fileName, CancellationToken ct = default);
}

/// <summary>更新检查结果。</summary>
public sealed record UpdateCheckResult(
    bool HasUpdate,
    string CurrentVersion,
    string LatestVersion,
    string? ReleaseNotes,
    string? ReleaseUrl,
    string? DownloadUrl,
    string? Sha256,
    string? TargetArchitecture);

/// <summary>更新服务契约：应用更新与内核更新。</summary>
public interface IUpdateService
{
    /// <summary>检查应用更新（GitHub Release 清单，区分架构）。</summary>
    Task<OperationResult<UpdateCheckResult>> CheckAppUpdateAsync(CancellationToken ct = default);

    /// <summary>检查内核更新（稳定/预览通道）。</summary>
    Task<OperationResult<UpdateCheckResult>> CheckCoreUpdateAsync(bool preview, CancellationToken ct = default);

    /// <summary>下载并验证（SHA-256）内核更新到版本化用户缓存；返回新内核路径。</summary>
    Task<OperationResult<string>> DownloadCoreAsync(UpdateCheckResult check, CancellationToken ct = default);
}

/// <summary>热键动作标识。</summary>
public static class HotkeyActions
{
    public const string ModeRule = "mode_rule";
    public const string ModeGlobal = "mode_global";
    public const string ModeDirect = "mode_direct";
    public const string ToggleSystemProxy = "toggle_system_proxy";
    public const string ToggleTun = "toggle_tun";
    public const string ShowHideWindow = "show_hide_window";
    public const string ReactivateProfile = "reactivate_profile";
    public const string LightweightMode = "lightweight_mode";
}

/// <summary>热键服务契约：注册全局热键，冲突时拒绝保存并报告冲突组合。</summary>
public interface IHotkeyService
{
    /// <summary>热键触发。</summary>
    event Action<string>? HotkeyPressed;

    /// <summary>应用热键映射（action → 组合键如 "Ctrl+Shift+F"）。返回失败映射与冲突组合。</summary>
    OperationResult<IReadOnlyList<string>> ApplyHotkeys(IReadOnlyDictionary<string, string> hotkeys);

    /// <summary>清除全部热键注册。</summary>
    void Clear();
}

/// <summary>解锁测试单项结果。</summary>
public sealed record UnlockTestItemResult(
    string Id,
    string Name,
    string Status,
    string? Region,
    string? Detail);

/// <summary>解锁测试服务契约：网站测试与流媒体/AI 服务解锁检测。</summary>
public interface IUnlockTestService
{
    /// <summary>运行全部解锁测试。并发受限；可通过 ct 取消。</summary>
    Task<IReadOnlyList<UnlockTestItemResult>> RunAllAsync(string testUrl, CancellationToken ct = default);

    /// <summary>运行单项测试。</summary>
    Task<UnlockTestItemResult> RunAsync(string id, string testUrl, CancellationToken ct = default);
}
