using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Flux.Services;

public enum RunningMode { NotRunning, Sidecar }

/// <summary>
/// mihomo 内核进程管理（对应参考项目 CoreManager 的 sidecar 模式）：
/// - 启动前用 `mihomo -t` 校验配置
/// - Windows Job Object 保证主进程退出时内核被一并终止
/// - stdout/stderr 汇入日志
/// </summary>
public class CoreProcessService : IDisposable
{
    public RunningMode Mode { get; private set; } = RunningMode.NotRunning;
    public bool IsRunning => Mode == RunningMode.Sidecar && _process is { HasExited: false };

    private Process? _process;
    private IntPtr _jobHandle = IntPtr.Zero;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly SemaphoreSlim _applyLock = new(1, 1);

    /// <summary>内核启动成功且 External Controller 就绪。</summary>
    public event Action? CoreStarted;
    /// <summary>内核退出（含异常退出）。</summary>
    public event Action? CoreStopped;

    private ConfigService Config => AppServices.Config;

    // ---------- 生命周期 ----------

    public async Task StartAsync()
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            if (IsRunning) return;
            KillLeftoverCores();
            Paths.EnsureCoreExecutable();

            Config.WriteRuntimeFile(Paths.RuntimeConfigFile);
            await ValidateConfigAsync(Paths.RuntimeConfigFile);

            try
            {
                StartSidecar(Paths.RuntimeConfigFile, Paths.AppDataDir);
                await WaitForControllerAsync(15000);
                Mode = RunningMode.Sidecar;
                LogService.App("mihomo 内核已启动");
                CoreStarted?.Invoke();
            }
            catch
            {
                await StopCoreUnsafeAsync();
                throw;
            }
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync()
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            await StopCoreUnsafeAsync();
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task RestartAsync()
    {
        await StopAsync();
        await StartAsync();
    }

    private void StartSidecar(string configPath, string configDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Paths.CoreExePath,
            Arguments = $"-d \"{configDir}\" -f \"{configPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = configDir,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, e) => { if (e.Data != null) LogService.Core(e.Data); };
        _process.ErrorDataReceived += (_, e) => { if (e.Data != null) LogService.Core(e.Data); };
        _process.Exited += (_, _) => { Mode = RunningMode.NotRunning; CoreStopped?.Invoke(); };

        if (!_process.Start())
            throw new InvalidOperationException("mihomo 进程启动失败");

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        AssignJobObject(_process.Handle);
        LogService.App($"mihomo PID: {_process.Id}");
    }

    private async Task StopCoreUnsafeAsync()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync(new CancellationTokenSource(5000).Token);
            }
        }
        catch { }
        finally
        {
            ReleaseJobObject();
            _process?.Dispose();
            _process = null;
            Mode = RunningMode.NotRunning;
            CoreStopped?.Invoke();
        }
    }

    // ---------- 残留清理 ----------

    /// <summary>结束上次异常退出遗留的内核进程（残留会占用端口导致新内核启动失败）。</summary>
    private static void KillLeftoverCores()
    {
        try
        {
            var knownCorePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Path.GetFullPath(Paths.CoreExePath),
                Path.GetFullPath(Paths.LegacyCoreExePath),
            };
            var processNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Path.GetFileNameWithoutExtension(Paths.CoreExePath),
                Path.GetFileNameWithoutExtension(Paths.LegacyCoreExePath),
            };

            foreach (var processName in processNames)
            {
                foreach (var leftover in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        var executable = leftover.MainModule?.FileName;
                        if (!knownCorePaths.Contains(Path.GetFullPath(executable ?? "")))
                            continue;
                        leftover.Kill(entireProcessTree: true);
                        LogService.App($"已清理残留内核进程 PID {leftover.Id}", "warn");
                    }
                    catch { }
                    finally { leftover.Dispose(); }
                }
            }
        }
        catch { }
    }

    // ---------- 配置校验与应用 ----------

    /// <summary>用内核 `-t` 参数校验配置文件，失败抛异常。</summary>
    private async Task ValidateConfigAsync(string configFile)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Paths.CoreExePath,
            Arguments = $"-t -d \"{Paths.AppDataDir}\" -f \"{configFile}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var p = Process.Start(psi)!;
        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(15000);
        await p.WaitForExitAsync(timeout.Token);
        var output = await stdoutTask + await stderrTask;
        if (!string.IsNullOrWhiteSpace(output)) LogService.Core(output.TrimEnd());
        if (p.ExitCode != 0 || output.Contains("FATA", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("配置校验失败: " + output.Trim());
        }
    }

    /// <summary>重新生成运行时配置并应用到运行中的内核（热重载，失败则重启内核）。</summary>
    public async Task<bool> ApplyConfigAsync()
    {
        await _applyLock.WaitAsync();
        try
        {
            if (!IsRunning) return false;
            try
            {
                Config.WriteRuntimeFile(Paths.RuntimeConfigFile);
                await ValidateConfigAsync(Paths.RuntimeConfigFile);
            }
            catch (Exception ex)
            {
                LogService.App("配置应用被拒绝: " + ex.Message, "error");
                return false;
            }
            try
            {
                await AppServices.Api.ReloadConfigAsync(Paths.RuntimeConfigFile);
                LogService.App("运行时配置已热重载");
                return true;
            }
            catch (Exception ex)
            {
                LogService.App("热重载失败，重启内核: " + ex.Message, "warn");
                await RestartAsync();
                return true;
            }
        }
        finally
        {
            _applyLock.Release();
        }
    }

    // ---------- 等待 External Controller 就绪 ----------

    private async Task WaitForControllerAsync(int timeoutMs)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (_process is { HasExited: true })
                throw new InvalidOperationException("mihomo 进程异常退出，请查看日志");
            try
            {
                if (await AppServices.Api.GetVersionAsync() is not null) return;
            }
            catch { }
            await Task.Delay(250);
        }
        throw new TimeoutException("等待 External Controller 就绪超时");
    }

    // ---------- Job Object ----------

    private void AssignJobObject(IntPtr processHandle)
    {
        _jobHandle = NativeMethods.CreateJobObjectW(IntPtr.Zero, null);
        if (_jobHandle == IntPtr.Zero) return;

        var info = new NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
        info.BasicLimitInformation.LimitFlags =
            NativeMethods.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
        NativeMethods.SetInformationJobObject(
            _jobHandle,
            NativeMethods.JobObjectInfoClass.JobObjectExtendedLimitInformation,
            ref info,
            (uint)Marshal.SizeOf<NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION>());

        // 关联失败时（如进程已属于其他 Job）KILL_ON_JOB_CLOSE 兜底失效，内核会残留
        if (!NativeMethods.AssignProcessToJobObject(_jobHandle, processHandle))
            LogService.App("Job Object 关联失败，主进程异常退出时内核可能残留", "warn");
    }

    private void ReleaseJobObject()
    {
        if (_jobHandle != IntPtr.Zero)
        {
            NativeMethods.CloseHandle(_jobHandle);
            _jobHandle = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        try { StopAsync().Wait(3000); } catch { }
        ReleaseJobObject();
        _lifecycleLock.Dispose();
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateJobObjectW(IntPtr lpJobAttributes, string? lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetInformationJobObject(
            IntPtr hJob, JobObjectInfoClass infoClass,
            ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION lpInfo, uint cbInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        public enum JobObjectInfoClass
        {
            JobObjectExtendedLimitInformation = 9
        }

        public const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }
    }
}
