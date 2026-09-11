using Flux.Core.Service;

namespace Flux.Service;

/// <summary>
/// Flux 特权服务入口：LocalSystem 运行，命名管道提供 start_core/stop_core/status/version
/// 四个预定义操作。不执行任意命令。
/// </summary>
public sealed class FluxServiceWorker : BackgroundService
{
    private readonly ILogger<FluxServiceWorker> _logger;
    private PipeServer? _pipe;
    private PrivilegedCoreManager? _core;
    private RequestHandler? _handler;

    public FluxServiceWorker(ILogger<FluxServiceWorker> logger) => _logger = logger;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var installDir = AppContext.BaseDirectory;
        var dataDir = ServicePathValidator.ResolveDataDir(installDir);
        var serviceVersion = typeof(FluxServiceWorker).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

        void Log(string level, string message)
        {
            if (level == "error") _logger.LogError("{Message}", message);
            else if (level == "warn") _logger.LogWarning("{Message}", message);
            else _logger.LogInformation("{Message}", message);
        }

        _core = new PrivilegedCoreManager(Log);
        _handler = new RequestHandler(_core, Log, installDir, dataDir, serviceVersion);
        _pipe = new PipeServer(_handler.Handle, Log);
        _pipe.Start(stoppingToken);

        _logger.LogInformation("Flux 服务已启动（协议 v{Version}，数据目录 {DataDir}）",
            ServiceProtocol.Version, dataDir);

        // 保持运行直到关闭
        return Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(t => { }, stoppingToken);
    }

    public override void Dispose()
    {
        try
        {
            _pipe?.Stop();
            _core?.Dispose();
        }
        catch
        {
            // 关闭路径
        }
        base.Dispose();
    }
}
