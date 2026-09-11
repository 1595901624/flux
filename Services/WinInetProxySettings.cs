using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Flux.Services;

/// <summary>
/// 通过 WinINet 的公开 API 读取和修改当前用户的 LAN 代理设置。
/// 不直接写 HKCU，因此不依赖 MSIX 的 unvirtualizedResources 能力。
/// </summary>
internal static class WinInetProxySettings
{
    private const int InternetOptionRefresh = 37;
    private const int InternetOptionSettingsChanged = 39;
    private const int InternetOptionPerConnectionOption = 75;

    private const int InternetPerConnFlags = 1;
    private const int InternetPerConnProxyServer = 2;
    private const int InternetPerConnProxyBypass = 3;
    private const int InternetPerConnAutoConfigUrl = 4;
    private const int InternetPerConnFlagsUi = 10;

    private const int ProxyTypeDirect = 0x00000001;
    private const int ProxyTypeProxy = 0x00000002;
    private const int ProxyTypeAutoProxyUrl = 0x00000004;
    private const int ProxyTypeAutoDetect = 0x00000008;

    public static State Read()
    {
        var options = QueryOptions();
        return new State(
            (options.Flags & ProxyTypeProxy) != 0,
            (options.Flags & ProxyTypeAutoProxyUrl) != 0,
            options.Server,
            options.Bypass,
            options.AutoConfigUrl);
    }

    /// <summary>设置/关闭手动代理。关闭时同时清除 PAC URL，避免指向已失效的脚本。</summary>
    public static void Write(bool enable, string server, string bypass)
    {
        var current = QueryOptions();
        var flags = enable
            ? (current.Flags | ProxyTypeDirect | ProxyTypeProxy) & ~ProxyTypeAutoProxyUrl
            : (current.Flags & ~(ProxyTypeProxy | ProxyTypeAutoProxyUrl)) | ProxyTypeDirect;

        SetOptions(flags, server, bypass, autoConfigUrl: enable ? null : "");
        NotifyChanged();
    }

    /// <summary>设置/关闭 PAC（自动配置脚本 URL）。开启时关闭手动代理位。</summary>
    public static void WritePac(bool enable, string autoConfigUrl)
    {
        var current = QueryOptions();
        var flags = enable
            ? (current.Flags | ProxyTypeDirect | ProxyTypeAutoProxyUrl) & ~ProxyTypeProxy
            : (current.Flags & ~ProxyTypeAutoProxyUrl) | ProxyTypeDirect;

        SetOptions(flags, server: "", bypass: "", autoConfigUrl: enable ? autoConfigUrl : "");
        NotifyChanged();
    }

    private static void NotifyChanged()
    {
        if (!InternetSetOption(IntPtr.Zero, InternetOptionSettingsChanged, IntPtr.Zero, 0))
            throw CreateWin32Exception("无法通知 Windows 系统代理设置已更改");
        if (!InternetSetOption(IntPtr.Zero, InternetOptionRefresh, IntPtr.Zero, 0))
            throw CreateWin32Exception("无法刷新 Windows 系统代理设置");
    }

    private static QueryResult QueryOptions()
    {
        var optionSize = Marshal.SizeOf<InternetPerConnOption>();
        var optionBuffer = Marshal.AllocHGlobal(optionSize * 4);
        try
        {
            WriteOption(optionBuffer, optionSize, 0, new InternetPerConnOption
            {
                Option = InternetPerConnFlagsUi,
            });
            WriteOption(optionBuffer, optionSize, 1, new InternetPerConnOption
            {
                Option = InternetPerConnProxyServer,
            });
            WriteOption(optionBuffer, optionSize, 2, new InternetPerConnOption
            {
                Option = InternetPerConnProxyBypass,
            });
            WriteOption(optionBuffer, optionSize, 3, new InternetPerConnOption
            {
                Option = InternetPerConnAutoConfigUrl,
            });

            var list = new InternetPerConnOptionList
            {
                Size = Marshal.SizeOf<InternetPerConnOptionList>(),
                OptionCount = 4,
                Options = optionBuffer,
            };
            var listSize = list.Size;
            if (!InternetQueryOption(IntPtr.Zero, InternetOptionPerConnectionOption, ref list, ref listSize))
                throw CreateWin32Exception("无法读取 Windows 系统代理设置");

            var flags = ReadOption(optionBuffer, optionSize, 0).Value.IntValue;
            var serverOption = ReadOption(optionBuffer, optionSize, 1);
            var bypassOption = ReadOption(optionBuffer, optionSize, 2);
            var autoConfigOption = ReadOption(optionBuffer, optionSize, 3);
            try
            {
                return new QueryResult(
                    flags,
                    Marshal.PtrToStringUni(serverOption.Value.StringValue) ?? "",
                    Marshal.PtrToStringUni(bypassOption.Value.StringValue) ?? "",
                    Marshal.PtrToStringUni(autoConfigOption.Value.StringValue) ?? "");
            }
            finally
            {
                FreeWinInetString(serverOption.Value.StringValue);
                FreeWinInetString(bypassOption.Value.StringValue);
                FreeWinInetString(autoConfigOption.Value.StringValue);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(optionBuffer);
        }
    }

    private static void SetOptions(int flags, string server, string bypass, string? autoConfigUrl)
    {
        var optionCount = autoConfigUrl is null ? 3 : 4;
        var optionSize = Marshal.SizeOf<InternetPerConnOption>();
        var optionBuffer = Marshal.AllocHGlobal(optionSize * optionCount);
        var serverPtr = Marshal.StringToHGlobalUni(server ?? "");
        var bypassPtr = Marshal.StringToHGlobalUni(bypass ?? "");
        var autoConfigPtr = autoConfigUrl is null ? IntPtr.Zero : Marshal.StringToHGlobalUni(autoConfigUrl);
        try
        {
            WriteOption(optionBuffer, optionSize, 0, new InternetPerConnOption
            {
                Option = InternetPerConnFlags,
                Value = new InternetPerConnOptionValue { IntValue = flags },
            });
            WriteOption(optionBuffer, optionSize, 1, new InternetPerConnOption
            {
                Option = InternetPerConnProxyServer,
                Value = new InternetPerConnOptionValue { StringValue = serverPtr },
            });
            WriteOption(optionBuffer, optionSize, 2, new InternetPerConnOption
            {
                Option = InternetPerConnProxyBypass,
                Value = new InternetPerConnOptionValue { StringValue = bypassPtr },
            });
            if (autoConfigUrl is not null)
            {
                WriteOption(optionBuffer, optionSize, 3, new InternetPerConnOption
                {
                    Option = InternetPerConnAutoConfigUrl,
                    Value = new InternetPerConnOptionValue { StringValue = autoConfigPtr },
                });
            }

            var list = new InternetPerConnOptionList
            {
                Size = Marshal.SizeOf<InternetPerConnOptionList>(),
                OptionCount = optionCount,
                Options = optionBuffer,
            };
            if (!InternetSetOption(
                    IntPtr.Zero,
                    InternetOptionPerConnectionOption,
                    ref list,
                    list.Size))
            {
                throw CreateWin32Exception("无法修改 Windows 系统代理设置");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(serverPtr);
            Marshal.FreeHGlobal(bypassPtr);
            if (autoConfigPtr != IntPtr.Zero) Marshal.FreeHGlobal(autoConfigPtr);
            Marshal.FreeHGlobal(optionBuffer);
        }
    }

    private static void WriteOption(IntPtr buffer, int size, int index, InternetPerConnOption option) =>
        Marshal.StructureToPtr(option, IntPtr.Add(buffer, size * index), false);

    private static InternetPerConnOption ReadOption(IntPtr buffer, int size, int index) =>
        Marshal.PtrToStructure<InternetPerConnOption>(IntPtr.Add(buffer, size * index));

    private static void FreeWinInetString(IntPtr value)
    {
        if (value != IntPtr.Zero)
            _ = GlobalFree(value);
    }

    private static Win32Exception CreateWin32Exception(string message) =>
        new(Marshal.GetLastWin32Error(), message);

    internal sealed record State(bool Enable, bool PacEnabled, string Server, string Bypass, string AutoConfigUrl);
    private sealed record QueryResult(int Flags, string Server, string Bypass, string AutoConfigUrl);

    [StructLayout(LayoutKind.Sequential)]
    private struct InternetPerConnOptionList
    {
        public int Size;
        public IntPtr Connection;
        public int OptionCount;
        public int OptionError;
        public IntPtr Options;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InternetPerConnOption
    {
        public int Option;
        public InternetPerConnOptionValue Value;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InternetPerConnOptionValue
    {
        [FieldOffset(0)] public int IntValue;
        [FieldOffset(0)] public IntPtr StringValue;
        [FieldOffset(0)] public System.Runtime.InteropServices.ComTypes.FILETIME FileTimeValue;
    }

    [DllImport("wininet.dll", EntryPoint = "InternetQueryOptionW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InternetQueryOption(
        IntPtr internet,
        int option,
        ref InternetPerConnOptionList buffer,
        ref int bufferLength);

    [DllImport("wininet.dll", EntryPoint = "InternetSetOptionW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InternetSetOption(
        IntPtr internet,
        int option,
        ref InternetPerConnOptionList buffer,
        int bufferLength);

    [DllImport("wininet.dll", EntryPoint = "InternetSetOptionW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InternetSetOption(
        IntPtr internet,
        int option,
        IntPtr buffer,
        int bufferLength);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalFree(IntPtr memory);
}
