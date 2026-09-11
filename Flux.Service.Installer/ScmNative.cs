using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Flux.Service.Installer;

/// <summary>Windows 服务控制管理器（SCM）P/Invoke 封装。</summary>
internal static class ScmNative
{
    public const int SERVICE_WIN32_OWN_PROCESS = 0x00000010;
    public const int SERVICE_AUTO_START = 0x00000002;
    public const int SERVICE_DEMAND_START = 0x00000003;
    public const int SERVICE_ERROR_NORMAL = 0x00000001;
    public const int SERVICE_ALL_ACCESS = 0xF01FF;
    public const int SERVICE_STOP = 0x00000020;
    public const int SERVICE_QUERY_STATUS = 0x00000004;
    public const int SERVICE_CONTROL_STOP = 0x00000001;
    public const int SERVICE_STOPPED = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    public struct SERVICE_STATUS
    {
        public int dwServiceType;
        public int dwCurrentState;
        public int dwControlsAccepted;
        public int dwWin32ExitCode;
        public int dwServiceSpecificExitCode;
        public int dwCheckPoint;
        public int dwWaitHint;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr OpenSCManagerW(string? machineName, string? databaseName, int desiredAccess);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateServiceW(
        IntPtr scHandle, string serviceName, string displayName, int desiredAccess,
        int serviceType, int startType, int errorControl, string binaryPathName,
        string? loadOrderGroup, IntPtr tagId, string? dependencies, string? accountName, string? password);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr OpenServiceW(IntPtr scHandle, string serviceName, int desiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool ChangeServiceConfigW(
        IntPtr serviceHandle, int serviceType, int startType, int errorControl,
        string? binaryPathName, string? loadOrderGroup, IntPtr tagId, string? dependencies,
        string? accountName, string? password, string? displayName);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool StartServiceW(IntPtr serviceHandle, int argc, string?[]? argv);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool ControlService(IntPtr serviceHandle, int controlCode, ref SERVICE_STATUS status);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool DeleteService(IntPtr serviceHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool CloseServiceHandle(IntPtr handle);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool QueryServiceStatus(IntPtr serviceHandle, ref SERVICE_STATUS status);

    public static void ThrowWin32(string action)
    {
        var error = new Win32Exception(Marshal.GetLastWin32Error());
        throw new InvalidOperationException($"{action} 失败: {error.Message} (0x{error.NativeErrorCode:X8})", error);
    }
}

/// <summary>SCM 高层操作。</summary>
internal static class ScmClient
{
    public const string ServiceName = "FluxService";
    public const string DisplayName = "Flux Privileged Service";

    public static bool ServiceExists()
    {
        var sc = ScmNative.OpenSCManagerW(null, null, ScmNative.SERVICE_ALL_ACCESS);
        if (sc == IntPtr.Zero) ScmNative.ThrowWin32("打开服务控制管理器");
        try
        {
            var svc = ScmNative.OpenServiceW(sc, ServiceName, ScmNative.SERVICE_QUERY_STATUS);
            if (svc == IntPtr.Zero) return false;
            ScmNative.CloseServiceHandle(svc);
            return true;
        }
        finally
        {
            ScmNative.CloseServiceHandle(sc);
        }
    }

    public static void Create(string binaryPath)
    {
        var quoted = $"\"{binaryPath}\"";
        var sc = ScmNative.OpenSCManagerW(null, null, ScmNative.SERVICE_ALL_ACCESS);
        if (sc == IntPtr.Zero) ScmNative.ThrowWin32("打开服务控制管理器");
        try
        {
            var svc = ScmNative.CreateServiceW(
                sc, ServiceName, DisplayName, ScmNative.SERVICE_ALL_ACCESS,
                ScmNative.SERVICE_WIN32_OWN_PROCESS, ScmNative.SERVICE_DEMAND_START,
                ScmNative.SERVICE_ERROR_NORMAL, quoted, null, IntPtr.Zero, null, null, null);
            if (svc == IntPtr.Zero) ScmNative.ThrowWin32("创建服务");
            ScmNative.CloseServiceHandle(svc);
        }
        finally
        {
            ScmNative.CloseServiceHandle(sc);
        }
    }

    public static void UpdateBinaryPath(string binaryPath)
    {
        var svc = Open();
        try
        {
            if (!ScmNative.ChangeServiceConfigW(
                    svc, ScmNative.SERVICE_WIN32_OWN_PROCESS, ScmNative.SERVICE_DEMAND_START,
                    ScmNative.SERVICE_ERROR_NORMAL, $"\"{binaryPath}\"", null, IntPtr.Zero, null, null, null, null))
                ScmNative.ThrowWin32("更新服务配置");
        }
        finally
        {
            ScmNative.CloseServiceHandle(svc);
        }
    }

    public static void Start()
    {
        var svc = Open();
        try
        {
            if (!ScmNative.StartServiceW(svc, 0, null))
                ScmNative.ThrowWin32("启动服务");
        }
        finally
        {
            ScmNative.CloseServiceHandle(svc);
        }
    }

    public static void Stop()
    {
        var svc = Open();
        try
        {
            var status = new ScmNative.SERVICE_STATUS();
            if (!ScmNative.ControlService(svc, ScmNative.SERVICE_CONTROL_STOP, ref status))
            {
                var error = Marshal.GetLastWin32Error();
                if (error != 1062) // ERROR_SERVICE_NOT_ACTIVE
                    ScmNative.ThrowWin32("停止服务");
            }
            // 等待停止
            for (var i = 0; i < 80; i++)
            {
                if (!ScmNative.QueryServiceStatus(svc, ref status)) break;
                if (status.dwCurrentState == ScmNative.SERVICE_STOPPED) return;
                Thread.Sleep(100);
            }
        }
        finally
        {
            ScmNative.CloseServiceHandle(svc);
        }
    }

    public static void Delete()
    {
        var svc = Open();
        try
        {
            if (!ScmNative.DeleteService(svc))
                ScmNative.ThrowWin32("删除服务");
        }
        finally
        {
            ScmNative.CloseServiceHandle(svc);
        }
    }

    private static IntPtr Open()
    {
        var sc = ScmNative.OpenSCManagerW(null, null, ScmNative.SERVICE_ALL_ACCESS);
        if (sc == IntPtr.Zero) ScmNative.ThrowWin32("打开服务控制管理器");
        try
        {
            var svc = ScmNative.OpenServiceW(sc, ServiceName, ScmNative.SERVICE_ALL_ACCESS);
            if (svc == IntPtr.Zero) ScmNative.ThrowWin32("打开服务");
            return svc;
        }
        finally
        {
            ScmNative.CloseServiceHandle(sc);
        }
    }
}
