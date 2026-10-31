using System.Diagnostics;
using System.Reflection;
using Flux.Services;
using Xunit;

namespace Flux.Tests;

public sealed class CoreProcessServiceTests
{
    [Fact]
    public async Task RestartKeepsBusyStateThroughStopAndClearsItOnStartupFailure()
    {
        using var core = new CoreProcessService();
        var states = new List<bool>();
        core.RestartStateChanged += () => states.Add(core.IsRestarting);
        core.CoreStopped += () => Assert.True(core.IsRestarting);

        // 测试依赖不允许启动真实内核；即使启动失败，界面也必须能够解除锁定。
        await Assert.ThrowsAsync<NotSupportedException>(() => core.RestartAsync());

        Assert.False(core.IsRestarting);
        Assert.Equal(new[] { true, false }, states);
    }

    [Fact]
    public async Task OverlappingRestartsDoNotClearBusyStatePrematurely()
    {
        using var core = new CoreProcessService();
        var lifecycle = (SemaphoreSlim)typeof(CoreProcessService).GetField("_lifecycleLock",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(core)!;
        var states = new List<bool>();
        core.RestartStateChanged += () => states.Add(core.IsRestarting);
        await lifecycle.WaitAsync();
        Task first;
        Task second;
        try
        {
            first = core.RestartAsync();
            second = core.RestartAsync();
            Assert.True(core.IsRestarting);
            Assert.False(first.IsCompleted);
            Assert.False(second.IsCompleted);
            Assert.Equal(new[] { true }, states);
        }
        finally { lifecycle.Release(); }

        await Assert.ThrowsAsync<NotSupportedException>(() => first);
        await Assert.ThrowsAsync<NotSupportedException>(() => second);
        Assert.False(core.IsRestarting);
        Assert.Equal(new[] { true, false }, states);
    }

    [Fact]
    public async Task ControllerDeadlineCancelsAnUnresponsiveRequest()
    {
        using var http = new HttpClient(new UnresponsiveHandler()) { Timeout = TimeSpan.FromSeconds(15) };
        var previous = AppServices.Api;
        AppServices.Api = new MihomoApiService(http);
        try
        {
            using var core = new CoreProcessService();
            var wait = (Task)typeof(CoreProcessService).GetMethod("WaitForControllerAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(core, [100, false])!;
            // A stalled HTTP request must obey the controller deadline, not the 15s client timeout.
            await Assert.ThrowsAsync<TimeoutException>(() => wait.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.True(wait.IsFaulted); // The controller task itself timed out, not only the test's WaitAsync.
        }
        finally { AppServices.Api = previous; }
    }

    private sealed class UnresponsiveHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("Unreachable");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApplyingConfigSkipsServiceProbeWithoutTunAndAwaitsItWithTun(bool tun)
    {
        var core = new CoreProcessService();
        Track(core, null, RunningMode.Service);
        typeof(CoreProcessService).GetField("_serviceCoreRunning", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(core, true);
        var previousTun = AppServices.Config.Verge.EnableTunMode;
        var probe = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        AppServices.Privilege.ServiceReadyResult = probe.Task;
        AppServices.Privilege.ServiceReadyChecks = 0;
        AppServices.Config.Verge.EnableTunMode = tun;
        try
        {
            var apply = core.ApplyConfigAsync();
            Assert.Equal(tun ? 1 : 0, AppServices.Privilege.ServiceReadyChecks);
            if (tun)
            {
                // The caller must get control back while a slow/unavailable service is being queried.
                Assert.False(apply.IsCompleted);
                probe.SetResult(false);
            }
            Assert.False(await apply.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            probe.TrySetResult(false);
            AppServices.Config.Verge.EnableTunMode = previousTun;
            AppServices.Privilege.ServiceReadyResult = null;
        }
    }

    private static Task ExitAsync(CoreProcessService core, Process exited) =>
        (Task)typeof(CoreProcessService).GetMethod("HandleSidecarExitedAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(core, [exited])!;

    private static void Track(CoreProcessService core, Process? process, RunningMode mode)
    {
        typeof(CoreProcessService).GetField("_process", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(core, process);
        typeof(CoreProcessService).GetField("<Mode>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(core, mode);
    }

    [Fact]
    public async Task UnexpectedExitRestoresProxyAndStopsStreamsOnce()
    {
        var core = new CoreProcessService();
        using var process = new Process();
        Track(core, process, RunningMode.Sidecar);
        AppServices.SysProxy.Resets = AppServices.Streams.Stops = 0;
        var stopped = 0;
        core.CoreStopped += () => stopped++;
        await ExitAsync(core, process);
        await ExitAsync(core, process);
        Assert.Equal(RunningMode.NotRunning, core.Mode);
        Assert.Equal(1, AppServices.SysProxy.Resets);
        Assert.Equal(1, AppServices.Streams.Stops);
        Assert.Equal(1, stopped);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlannedStopAndStaleExitDoNotResetProxy(bool newProcessRunning)
    {
        var core = new CoreProcessService();
        using var oldProcess = new Process();
        using var newProcess = new Process();
        var mode = newProcessRunning ? RunningMode.Sidecar : RunningMode.NotRunning;
        Track(core, newProcessRunning ? newProcess : null, mode);
        AppServices.SysProxy.Resets = AppServices.Streams.Stops = 0;
        await ExitAsync(core, oldProcess);
        Assert.Equal(mode, core.Mode);
        Assert.Equal(0, AppServices.SysProxy.Resets);
        Assert.Equal(0, AppServices.Streams.Stops);
    }
}
