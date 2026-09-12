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
    private readonly Func<ServiceClientContext, IReadOnlyList<string>> _dataDirResolver;

    public string ServiceVersion { get; }

    public RequestHandler(PrivilegedCoreManager core, Action<string, string> log, string installDir,
        Func<ServiceClientContext, IReadOnlyList<string>> dataDirResolver, string serviceVersion)
    {
        _core = core;
        _log = log;
        _installDir = installDir;
        _dataDirResolver = dataDirResolver;
        ServiceVersion = serviceVersion;
    }

    public ServiceResponse Handle(ServiceRequest request, ServiceClientContext client, CancellationToken ct)
    {
        if (request.Version != ServiceProtocol.Version)
            return ServiceResponse.Fail(request.RequestId,
                $"协议版本不匹配: 请求 v{request.Version}，服务 v{ServiceProtocol.Version}");

        return request.Operation switch
        {
            "version" => ServiceResponse.Success(request.RequestId) with { ServiceVersion = ServiceVersion },
            "status" => ServiceResponse.Success(request.RequestId, _core.State, _core.ProcessId)
                with { ServiceVersion = ServiceVersion },
            "start_core" => HandleStartCore(request, client, ct),
            "stop_core" => _core.StopAsync(request, client, ct).GetAwaiter().GetResult(),
            _ => ServiceResponse.Fail(request.RequestId, $"未知操作: {request.Operation}"),
        };
    }

    private ServiceResponse HandleStartCore(ServiceRequest request, ServiceClientContext client, CancellationToken ct)
    {
        var allowedDataDirs = _dataDirResolver(client);
        var dataDir = allowedDataDirs.FirstOrDefault(candidate =>
            ServicePathValidator.ValidateConfigDirectory(request.ConfigDir, candidate) is null);
        if (string.IsNullOrWhiteSpace(dataDir))
            return ServiceResponse.Fail(request.RequestId, "无法解析当前 Windows 用户的数据目录");

        var configError = ServicePathValidator.ValidateConfigPath(request.ConfigPath, dataDir);
        if (configError is not null)
        {
            _log("warn", $"拒绝非法配置路径: {request.ConfigPath}");
            return ServiceResponse.Fail(request.RequestId, configError.Message);
        }

        var trustedCore = ServicePathValidator.GetTrustedCorePath(_installDir);
        var coreError = ServicePathValidator.ValidateCorePath(trustedCore, _installDir, dataDir);
        if (coreError is not null)
        {
            _log("warn", $"拒绝非法内核路径: {request.CorePath}");
            return ServiceResponse.Fail(request.RequestId, coreError.Message);
        }

        var trustedRequest = request with { CorePath = trustedCore, ConfigDir = dataDir };
        _log("info", $"接受用户 {client.Name} ({client.Sid}) 的内核启动请求");
        return _core.StartAsync(trustedRequest, client, ct).GetAwaiter().GetResult();
    }
}
