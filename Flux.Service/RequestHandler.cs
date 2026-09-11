using Flux.Core.Service;

namespace Flux.Service;

/// <summary>
/// 请求处理器：协议版本校验、预定义操作分发、路径校验（防特权滥用）。
/// </summary>
public sealed class RequestHandler
{
    private readonly PrivilegedCoreManager _core;
    private readonly Action<string, string> _log;
    private readonly string _installDir;
    private readonly string _dataDir;

    public string ServiceVersion { get; }

    public RequestHandler(PrivilegedCoreManager core, Action<string, string> log, string installDir, string dataDir, string serviceVersion)
    {
        _core = core;
        _log = log;
        _installDir = installDir;
        _dataDir = dataDir;
        ServiceVersion = serviceVersion;
    }

    public ServiceResponse Handle(ServiceRequest request, CancellationToken ct)
    {
        if (request.Version != ServiceProtocol.Version)
            return ServiceResponse.Fail(request.RequestId,
                $"协议版本不匹配: 请求 v{request.Version}，服务 v{ServiceProtocol.Version}");

        return request.Operation switch
        {
            "version" => ServiceResponse.Success(request.RequestId) with { ServiceVersion = ServiceVersion },
            "status" => ServiceResponse.Success(request.RequestId, _core.State, _core.ProcessId)
                with { ServiceVersion = ServiceVersion },
            "start_core" => HandleStartCore(request, ct),
            "stop_core" => _core.StopAsync(request, ct).GetAwaiter().GetResult(),
            _ => ServiceResponse.Fail(request.RequestId, $"未知操作: {request.Operation}"),
        };
    }

    private ServiceResponse HandleStartCore(ServiceRequest request, CancellationToken ct)
    {
        var configError = ServicePathValidator.ValidateConfigPath(request.ConfigPath, _dataDir);
        if (configError is not null)
        {
            _log("warn", $"拒绝非法配置路径: {request.ConfigPath}");
            return ServiceResponse.Fail(request.RequestId, configError.Message);
        }

        var coreError = ServicePathValidator.ValidateCorePath(request.CorePath, _installDir, _dataDir);
        if (coreError is not null)
        {
            _log("warn", $"拒绝非法内核路径: {request.CorePath}");
            return ServiceResponse.Fail(request.RequestId, coreError.Message);
        }

        return _core.StartAsync(request, ct).GetAwaiter().GetResult();
    }
}
