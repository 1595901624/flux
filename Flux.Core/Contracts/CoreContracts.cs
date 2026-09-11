using YamlDotNet.RepresentationModel;

namespace Flux.Core.Contracts;

/// <summary>内核运行模式与状态。</summary>
public enum CoreRunState
{
    /// <summary>未运行。</summary>
    Stopped,
    /// <summary>由应用直接以子进程方式运行。</summary>
    Sidecar,
    /// <summary>由 Flux.Service 以特权方式运行。</summary>
    Service,
    /// <summary>故障状态（崩溃退避中或启动失败）。</summary>
    Faulted,
}

/// <summary>内核状态变化事件参数。</summary>
public sealed record CoreStateChangedEventArgs(CoreRunState State, string? Reason = null);

/// <summary>
/// 内核生命周期管理契约：支持安全重启、并发操作串行化、崩溃检测与有限次数退避重启。
/// </summary>
public interface ICoreManager
{
    CoreRunState State { get; }

    /// <summary>当前是否可接受配置热重载。</summary>
    bool IsRunning { get; }

    /// <summary>内核状态变化；State 进入 Faulted 时 Reason 携带可诊断原因。</summary>
    event EventHandler<CoreStateChangedEventArgs>? CoreStateChanged;

    /// <summary>启动内核。重复调用为幂等操作。</summary>
    Task<OperationResult<bool>> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>停止内核并回收进程资源。</summary>
    Task<OperationResult<bool>> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>重启内核（先停止后启动，串行化执行）。</summary>
    Task<OperationResult<bool>> RestartAsync(CancellationToken cancellationToken = default);

    /// <summary>合成运行时配置、校验并热重载；失败时保留最后一个有效配置。</summary>
    Task<OperationResult<bool>> ApplyConfigAsync(CancellationToken cancellationToken = default);
}

/// <summary>外部控制器 REST API 契约。所有调用接收 CancellationToken，统一返回 OperationResult。</summary>
public interface IMihomoClient
{
    Task<OperationResult<string>> GetVersionAsync(CancellationToken ct = default);
    Task<OperationResult<string>> GetRawConfigsAsync(CancellationToken ct = default);
    Task<OperationResult<bool>> PatchConfigsAsync(string json, CancellationToken ct = default);
    Task<OperationResult<bool>> ReloadConfigAsync(string path, bool force = true, CancellationToken ct = default);

    Task<OperationResult<string>> GetProxiesAsync(CancellationToken ct = default);
    Task<OperationResult<bool>> SelectProxyAsync(string group, string name, CancellationToken ct = default);
    Task<OperationResult<int>> GetProxyDelayAsync(string name, string testUrl, int timeoutMs, CancellationToken ct = default);
    Task<OperationResult<string>> GetGroupDelayAsync(string group, string testUrl, int timeoutMs, CancellationToken ct = default);

    Task<OperationResult<string>> GetProxyProvidersAsync(CancellationToken ct = default);
    Task<OperationResult<bool>> UpdateProxyProviderAsync(string name, CancellationToken ct = default);
    Task<OperationResult<bool>> HealthCheckProviderAsync(string name, string testUrl, int timeoutMs, CancellationToken ct = default);

    Task<OperationResult<string>> GetRuleProvidersAsync(CancellationToken ct = default);
    Task<OperationResult<bool>> UpdateRuleProviderAsync(string name, CancellationToken ct = default);

    Task<OperationResult<string>> GetRulesAsync(CancellationToken ct = default);
    Task<OperationResult<string>> GetConnectionsAsync(CancellationToken ct = default);
    Task<OperationResult<bool>> CloseConnectionAsync(string id, CancellationToken ct = default);
    Task<OperationResult<bool>> CloseAllConnectionsAsync(CancellationToken ct = default);

    Task<OperationResult<bool>> UpdateGeoDataAsync(CancellationToken ct = default);
}

/// <summary>日志条目（WebSocket /logs 通道与应用日志共用）。</summary>
public sealed record StreamLogLine(DateTime Timestamp, string Type, string Payload);

/// <summary>连接快照（WebSocket /connections 通道）。</summary>
public sealed record StreamConnectionsSnapshot(string Json, double UploadTotal, double DownloadTotal);

/// <summary>
/// 外部控制器流式订阅契约：Traffic/Memory/Logs/Connections 四类通道。
/// 自动重连、指数退避；Stop 后不再向页面派发事件。
/// </summary>
public interface IMihomoStreamClient
{
    event Action<double, double>? Traffic;
    event Action<long>? Memory;
    event Action<StreamLogLine>? Log;
    event Action<StreamConnectionsSnapshot>? Connections;

    void Configure(string controller, string secret);
    void Start();
    void Stop();
}

/// <summary>Flux.Service 状态查询结果。</summary>
public enum ServiceInstallState
{
    NotInstalled,
    Stopped,
    Running,
    Broken,
    VersionMismatch,
}

/// <summary>
/// 特权操作契约：只允许预定义操作，不允许传递任意命令或可执行文件。
/// </summary>
public interface IPrivilegeBroker
{
    /// <summary>查询服务安装与运行状态。</summary>
    ServiceInstallState GetServiceState();

    /// <summary>查询服务端报告的协议版本；服务未运行时返回 null。</summary>
    string? GetServiceVersion();

    /// <summary>通过 UAC 安装服务。</summary>
    Task<OperationResult<bool>> InstallServiceAsync(CancellationToken ct = default);

    /// <summary>通过 UAC 修复/重装服务。</summary>
    Task<OperationResult<bool>> RepairServiceAsync(CancellationToken ct = default);

    /// <summary>通过 UAC 卸载服务。</summary>
    Task<OperationResult<bool>> UninstallServiceAsync(CancellationToken ct = default);

    /// <summary>请求服务以特权方式启动内核（服务模式）。</summary>
    Task<OperationResult<bool>> StartCoreViaServiceAsync(string configPath, string corePath, string configDir, CancellationToken ct = default);

    /// <summary>请求服务停止特权内核。</summary>
    Task<OperationResult<bool>> StopCoreViaServiceAsync(CancellationToken ct = default);

    /// <summary>服务是否可用（已安装且命名管道可连接）。</summary>
    bool IsServiceReady();
}

/// <summary>订阅增强文件类型。</summary>
public enum ChainType
{
    Merge,
    Script,
    Rules,
    Proxies,
    Groups,
}

/// <summary>链式增强项：全局（uid=Global）或订阅专属。</summary>
public sealed record ChainItem(ChainType Type, string Uid, string Name, string File, bool IsGlobal);

/// <summary>
/// 运行时配置合成契约。输入基础配置、订阅、全局/订阅增强、DNS/TUN 与临时链式代理，
/// 输出 YAML、验证结果、每个增强阶段的脚本日志。
/// </summary>
public interface IRuntimeConfigBuilder
{
    /// <summary>合成运行时配置。流水线任一步骤失败返回带步骤名和文件名的错误，不抛异常。</summary>
    OperationResult<RuntimeConfigOutput> Build(RuntimeConfigInput input);
}

/// <summary>运行时配置合成输入。</summary>
public sealed record RuntimeConfigInput
{
    /// <summary>当前订阅 YAML（local/remote），可为 null 表示空配置。</summary>
    public YamlMappingNode? Profile { get; init; }

    /// <summary>应用基础配置（config.yaml，控制面来源）。</summary>
    public required YamlMappingNode ClashBase { get; init; }

    /// <summary>按流水线顺序应用的增强文件。</summary>
    public IReadOnlyList<ChainItemWithContent> ChainItems { get; init; } = [];

    /// <summary>是否启用 TUN。</summary>
    public bool EnableTun { get; init; }

    /// <summary>是否启用 DNS 设置覆写。</summary>
    public bool EnableDnsSettings { get; init; }

    /// <summary>DNS 覆写配置（enable-dns-settings 时叠加到 dns/hosts 字段）。</summary>
    public YamlMappingNode? DnsOverride { get; init; }

    /// <summary>临时链式代理：出口节点名 → 入口节点（dialer-proxy）。</summary>
    public IReadOnlyDictionary<string, string>? ChainProxy { get; init; }

    /// <summary>是否启用内置兼容增强（hysteria alpn 修正等）。</summary>
    public bool EnableBuiltinEnhance { get; init; } = true;
}

/// <summary>带内容的增强文件项。</summary>
public sealed record ChainItemWithContent(ChainItem Item, string Content);

/// <summary>运行时配置合成输出：最终配置 + 增强日志 + 已触碰键。</summary>
public sealed record RuntimeConfigOutput(
    YamlMappingNode Config,
    IReadOnlyList<ChainLogEntry> ChainLogs,
    IReadOnlySet<string> ExistsKeys);

/// <summary>增强阶段日志：level 为 info/warn/error。</summary>
public sealed record ChainLogEntry(string Level, string Uid, string Name, string Message);
