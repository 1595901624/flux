using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Flux.Core.Service;

namespace Flux.Service;

/// <summary>
/// 命名管道服务器：Windows ACL 仅允许 SYSTEM、管理员与交互式登录用户连接；
/// 每个连接串行处理一个请求后关闭（一问一答协议）。
/// </summary>
public sealed class PipeServer
{
    private readonly Func<ServiceRequest, ServiceClientContext, CancellationToken, ServiceResponse> _handler;
    private readonly Action<string, string> _log;
    private CancellationTokenSource? _cts;

    public PipeServer(Func<ServiceRequest, ServiceClientContext, CancellationToken, ServiceResponse> handler, Action<string, string> log)
    {
        _handler = handler;
        _log = log;
    }

    public void Start(CancellationToken shutdown)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
        var ct = _cts.Token;
        _ = Task.Run(() => AcceptLoopAsync(ct), ct);
    }

    public void Stop() => _cts?.Cancel();

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = CreatePipe();
                await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
                ServiceClientContext? client = null;
                pipe.RunAsClient(() => client = UserDataDirectoryResolver.Capture());
                if (client is null)
                    throw new UnauthorizedAccessException("无法识别管道客户端");
                client = client with { ExecutablePath = GetClientExecutablePath(pipe.SafePipeHandle) };
                using var registration = ct.Register(() => { try { pipe.Disconnect(); } catch { } });

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(ServiceProtocol.RequestTimeout);

                var request = await ServiceFrame.DecodeAsync<ServiceRequest>(pipe, timeoutCts.Token).ConfigureAwait(false);
                var response = request is null
                    ? ServiceResponse.Fail("", "请求为空")
                    : _handler(request, client, ct);
                await pipe.WriteAsync(ServiceFrame.Encode(response), timeoutCts.Token).ConfigureAwait(false);
                await pipe.FlushAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log("warn", "管道连接处理失败: " + ex.Message);
            }
            finally
            {
                try { pipe?.Dispose(); } catch { }
            }
        }
    }

    private static string? GetClientExecutablePath(SafePipeHandle pipeHandle)
    {
        if (!GetNamedPipeClientProcessId(pipeHandle, out var processId)) return null;
        try
        {
            using var process = Process.GetProcessById(checked((int)processId));
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint clientProcessId);

    /// <summary>创建带 ACL 的服务端管道：SYSTEM 与管理员完全控制，交互式用户读写。</summary>
    public static NamedPipeServerStream CreatePipe()
    {
        var security = new PipeSecurity();

        var system = new NTAccount("NT AUTHORITY", "SYSTEM");
        var admins = new NTAccount("BUILTIN", "Administrators");
        var interactive = new SecurityIdentifier(WellKnownSidType.InteractiveSid, null);

        security.AddAccessRule(new PipeAccessRule(system, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(admins, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(interactive,
            PipeAccessRights.ReadWrite, AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            ServiceProtocol.PipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 4,
            System.IO.Pipes.PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            security);
    }
}
