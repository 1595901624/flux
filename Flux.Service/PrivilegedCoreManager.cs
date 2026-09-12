using System.Diagnostics;
using Flux.Core.Service;

namespace Flux.Service;

/// <summary>
/// 特权内核进程管理：启动/停止 mihomo 子进程并跟踪句柄。
/// 服务退出时强制结束内核，避免特权进程残留。
/// </summary>
public sealed class PrivilegedCoreManager : IDisposable
{
    private Process? _process;
    private string? _ownerSid;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Action<string, string> _log;

    public PrivilegedCoreManager(Action<string, string> log) => _log = log;

    public ServiceCoreState State =>
        _process is { HasExited: false } ? ServiceCoreState.Running : ServiceCoreState.NotRunning;

    public int ProcessId => _process is { HasExited: false } p ? p.Id : 0;

    public async Task<ServiceResponse> StartAsync(ServiceRequest request, ServiceClientContext client, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (State == ServiceCoreState.Running)
                return ServiceResponse.Fail(request.RequestId, "内核已在服务内运行，请先停止");

            _process?.Dispose();
            _process = null;
            _ownerSid = null;

            var configPath = request.ConfigPath!;
            var corePath = request.CorePath!;
            var configDir = string.IsNullOrWhiteSpace(request.ConfigDir)
                ? Path.GetDirectoryName(configPath)!
                : request.ConfigDir!;

            _log("info", $"启动特权内核: {corePath} -d {configDir} -f {configPath}");
            var startInfo = new ProcessStartInfo
            {
                FileName = corePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = configDir,
            };
            startInfo.ArgumentList.Add("-d");
            startInfo.ArgumentList.Add(configDir);
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add(configPath);

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data)) _log("core", e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data)) _log("core", e.Data);
            };

            if (!process.Start())
                return ServiceResponse.Fail(request.RequestId, "内核进程启动失败");

            _process = process;
            _ownerSid = client.Sid;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _log("info", $"特权内核已启动 (PID {process.Id})");
            return ServiceResponse.Success(request.RequestId, ServiceCoreState.Running, process.Id);
        }
        catch (Exception ex)
        {
            _log("error", "特权内核启动失败: " + ex.Message);
            return ServiceResponse.Fail(request.RequestId, "内核启动失败: " + ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ServiceResponse> StopAsync(ServiceRequest request, ServiceClientContext client, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_ownerSid is not null && !string.Equals(_ownerSid, client.Sid, StringComparison.OrdinalIgnoreCase)
                && !client.IsAdministrator)
                return ServiceResponse.Fail(request.RequestId, "不能停止其他用户启动的内核");
            return StopCoreInternal(request.RequestId);
        }
        finally
        {
            _gate.Release();
        }
    }

    private ServiceResponse StopCoreInternal(string requestId)
    {
        try
        {
            if (_process is { HasExited: false } process)
            {
                _log("info", $"停止特权内核 (PID {process.Id})");
                process.Kill(entireProcessTree: true);
                if (!process.WaitForExit(8000))
                    _log("warn", "内核进程 8 秒内未退出，已放弃等待（句柄将随服务退出回收）");
            }
            return ServiceResponse.Success(requestId, ServiceCoreState.NotRunning);
        }
        catch (Exception ex)
        {
            _log("error", "停止内核失败: " + ex.Message);
            return ServiceResponse.Fail(requestId, "停止内核失败: " + ex.Message);
        }
        finally
        {
            _process?.Dispose();
            _process = null;
            _ownerSid = null;
        }
    }

    public void Dispose()
    {
        try
        {
            StopCoreInternal("shutdown");
        }
        catch
        {
            // 关闭路径上不再抛出
        }
        _gate.Dispose();
    }
}
