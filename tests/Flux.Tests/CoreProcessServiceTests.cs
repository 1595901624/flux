using System.Diagnostics;
using System.Reflection;
using Flux.Services;
using Xunit;

namespace Flux.Tests;

public sealed class CoreProcessServiceTests
{
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
