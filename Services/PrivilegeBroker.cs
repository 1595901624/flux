using System.Diagnostics;
using System.IO.Pipes;
using Flux.Core.Contracts;
using Flux.Core.Service;

namespace Flux.Services;

/// <summary>
/// IPrivilegeBroker 实现：通过命名管道与 Flux.Service 通信；
/// 安装/修复/卸载通过提权启动 Flux.Service.Installer 完成，错误对用户可见。
/// </summary>
public sealed class PrivilegeBroker : IPrivilegeBroker
{
    private string InstallerPath => Path.Combine(Paths.ExeDir, "Flux.Service.Installer.exe");

    // ---------- 状态查询 ----------

    public bool IsServiceReady()
    {
        var response = TryRequest(ServiceRequest.Status(), timeoutMs: 1500);
        return response is { Ok: true };
    }

    public ServiceInstallState GetServiceState()
    {
        var response = TryRequest(ServiceRequest.Status(), timeoutMs: 1500);
        if (response is null)
            return ServiceInstallState.NotInstalled;
        if (!response.Ok)
            return ServiceInstallState.Broken;
        return response.State == ServiceCoreState.Running
            ? ServiceInstallState.Running
            : ServiceInstallState.Stopped;
    }

    public string? GetServiceVersion()
    {
        var response = TryRequest(ServiceRequest.GetVersion(), timeoutMs: 1500);
        return response is { Ok: true } ? response.ServiceVersion : null;
    }

    // ---------- 服务生命周期（UAC） ----------

    public async Task<OperationResult<bool>> InstallServiceAsync(CancellationToken ct = default) =>
        await RunInstallerAsync("install", ct).ConfigureAwait(false);

    public async Task<OperationResult<bool>> RepairServiceAsync(CancellationToken ct = default) =>
        await RunInstallerAsync("reinstall", ct).ConfigureAwait(false);

    public async Task<OperationResult<bool>> UninstallServiceAsync(CancellationToken ct = default) =>
        await RunInstallerAsync("uninstall", ct).ConfigureAwait(false);

    // ---------- 内核生命周期 ----------

    public async Task<OperationResult<bool>> StartCoreViaServiceAsync(
        string configPath, string corePath, string configDir, CancellationToken ct = default)
    {
        var response = await RequestAsync(ServiceRequest.StartCore(configPath, corePath, configDir), ct)
            .ConfigureAwait(false);
        return ToResult(response, "通过服务启动内核");
    }

    public Task<OperationResult<bool>> StopCoreViaServiceAsync(CancellationToken ct = default) =>
        RequestAndCheckAsync(ServiceRequest.StopCore(), "通过服务停止内核", ct);

    // ---------- 内部 ----------

    private static ServiceResponse? TryRequest(ServiceRequest request, int timeoutMs)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            return RequestAsync(request, cts.Token).GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
    }

    private static async Task<ServiceResponse?> RequestAsync(ServiceRequest request, CancellationToken ct)
    {
        await using var pipe = new NamedPipeClientStream(
            ".", ServiceProtocol.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(5000, ct).ConfigureAwait(false);
        await pipe.WriteAsync(ServiceFrame.Encode(request), ct).ConfigureAwait(false);
        await pipe.FlushAsync(ct).ConfigureAwait(false);
        return await ServiceFrame.DecodeAsync<ServiceResponse>(pipe, ct).ConfigureAwait(false);
    }

    private async Task<OperationResult<bool>> RequestAndCheckAsync(
        ServiceRequest request, string action, CancellationToken ct)
    {
        var response = await RequestAsync(request, ct).ConfigureAwait(false);
        return ToResult(response, action);
    }

    private static OperationResult<bool> ToResult(ServiceResponse? response, string action)
    {
        if (response is null)
            return OperationResult<bool>.Fail("service_unavailable", "无法连接 Flux 服务", action);
        if (response.Version != ServiceProtocol.Version)
            return OperationResult<bool>.Fail("version_mismatch",
                $"服务协议版本不匹配（服务 v{response.Version}，应用 v{ServiceProtocol.Version}），请重启服务", action);
        return response.Ok
            ? OperationResult<bool>.Ok(true)
            : OperationResult<bool>.Fail("service_error", response.Error ?? "未知服务错误", action);
    }

    private async Task<OperationResult<bool>> RunInstallerAsync(string operation, CancellationToken ct)
    {
        if (!File.Exists(InstallerPath))
            return OperationResult<bool>.Fail("installer_missing",
                "未找到服务安装器，请重新安装应用", "服务安装");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = InstallerPath,
                Arguments = operation,
                UseShellExecute = true,
                Verb = "runas", // UAC 提权
                CreateNoWindow = true,
            };
            using var process = Process.Start(startInfo);
            if (process is null)
                return OperationResult<bool>.Fail("installer_failed", "无法启动安装器", "服务安装");

            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            if (process.ExitCode != 0)
                return OperationResult<bool>.Fail("installer_failed",
                    $"安装器退出码 {process.ExitCode}（用户取消或安装失败）", "服务安装");

            // 安装后等待服务就绪
            for (var i = 0; i < 30; i++)
            {
                if (IsServiceReady())
                    return OperationResult<bool>.Ok(true);
                await Task.Delay(300, ct).ConfigureAwait(false);
            }
            return OperationResult<bool>.Fail("service_not_ready", "服务已安装但未就绪", "服务安装");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return OperationResult<bool>.Fail("uac_cancelled", "用户取消了 UAC 授权", "服务安装");
        }
        catch (Exception ex)
        {
            return OperationResult<bool>.Fail("installer_error", ex.Message, "服务安装");
        }
    }
}
