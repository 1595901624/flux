using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flux.Core.Settings;

/// <summary>通用设置：语言、主题、启动页、静默启动、自动启动、轻量模式。</summary>
public sealed class GeneralSettings
{
    public string Language { get; set; } = "zh-CN";
    public string ThemeMode { get; set; } = "system"; // system | light | dark
    public string ThemeColor { get; set; } = "";
    public string StartPage { get; set; } = "home";
    public bool SilentStart { get; set; }
    public bool AutoLaunch { get; set; }
    public bool EnableLightweightMode { get; set; }
    public int AutoLightweightMinutes { get; set; } = 10;
    public string TrayClickBehavior { get; set; } = "show"; // show | menu | disabled
    public string EnvVariableFormat { get; set; } = "powershell"; // powershell | cmd
}

/// <summary>代理设置：系统代理、PAC、代理主机、绕过、守护间隔。</summary>
public sealed class ProxySettings
{
    public bool EnableSystemProxy { get; set; }
    public bool EnableProxyGuard { get; set; } = true;
    public int ProxyGuardIntervalSeconds { get; set; } = 30;
    public bool UsePacMode { get; set; }
    public string PacFileContent { get; set; } = "";
    public string ProxyHost { get; set; } = "127.0.0.1";
    public bool UseDefaultBypass { get; set; } = true;
    public string SystemProxyBypass { get; set; } = "";
    public bool EnableBypassCheck { get; set; } = true;
}

/// <summary>内核设置：通道、端口、LAN、IPv6、日志、接口、外部控制器和 CORS。</summary>
public sealed class CoreSettings
{
    public string CoreChannel { get; set; } = "stable"; // stable | preview
    public int MixedPort { get; set; } = 7897;
    public bool SocksEnabled { get; set; } = true;
    public int SocksPort { get; set; } = 7898;
    public bool HttpEnabled { get; set; } = true;
    public int HttpPort { get; set; } = 7899;
    public bool AllowLan { get; set; }
    public bool Ipv6 { get; set; } = true;
    public string LogLevel { get; set; } = "info"; // debug|info|warning|error|silent
    public bool EnableFileLog { get; set; }
    public string ExternalController { get; set; } = "127.0.0.1:9097";
    public string ExternalControllerSecret { get; set; } = "";
    public bool EnableExternalController { get; set; } = true;
    public bool EnableExternalControllerCors { get; set; }
    public string ExternalControllerCorsOrigins { get; set; } = "";
    public bool EnablePrivateNetworkAccess { get; set; } = true;
    public bool UnifiedDelay { get; set; } = true;
    public bool TcpConcurrent { get; set; } = true;
    public bool AutoCloseConnection { get; set; } = true;
    public string DefaultLatencyTest { get; set; } = "https://www.gstatic.com/generate_204";
    public int DefaultLatencyTimeoutMs { get; set; } = 5000;
    public bool EnableBuiltinEnhance { get; set; } = true;
    /// <summary>加密存储的控制器 Secret（DPAPI CurrentUser，Base64）。</summary>
    public string ExternalControllerSecretEncrypted { get; set; } = "";
}

/// <summary>TUN 设置。</summary>
public sealed class TunSettings
{
    public bool Enable { get; set; }
    public string Stack { get; set; } = "gvisor"; // gvisor | system | mixed
    public string Device { get; set; } = "Flux";
    public bool AutoRoute { get; set; } = true;
    public bool StrictRoute { get; set; }
    public bool AutoDetectInterface { get; set; } = true;
    public string DnsHijack { get; set; } = "any:53";
    public int Mtu { get; set; } = 1500;
}

/// <summary>DNS 设置覆写。</summary>
public sealed class DnsSettings
{
    public bool EnableDnsSettings { get; set; }
    public bool Enable { get; set; } = true;
    public bool Ipv6 { get; set; }
    public string EnhancedMode { get; set; } = "fake-ip"; // fake-ip | redir-host
    public string FakeIpRange { get; set; } = "198.18.0.1/16";
    public string Listen { get; set; } = "0.0.0.0:1053";
    public List<string> DefaultNameservers { get; set; } = ["223.5.5.5", "119.29.29.29"];
    public List<string> Nameservers { get; set; } = ["https://doh.pub/dns-query", "https://dns.alidns.com/dns-query"];
    public List<string> Fallback { get; set; } = [];
    /// <summary>高级用户直接编辑的 dns 覆写 YAML（优先于字段化配置）。</summary>
    public string RawDnsOverrideYaml { get; set; } = "";
}

/// <summary>隧道配置。</summary>
public sealed class TunnelSettings
{
    public bool Enable { get; set; }
    public List<TunnelEntry> Entries { get; set; } = [];
}

public sealed class TunnelEntry
{
    public string Network { get; set; } = "tcp";
    public string Address { get; set; } = "";
    public string Target { get; set; } = "";
    public string Proxy { get; set; } = "";
}

/// <summary>UI 设置：首页卡片、导航顺序、折叠、代理列数、通知位置和图标。</summary>
public sealed class UiSettings
{
    public List<string> HiddenHomeCards { get; set; } = [];
    public List<string> NavigationOrder { get; set; } = [];
    public bool CollapseNavbar { get; set; }
    public string ProxyLayoutColumn { get; set; } = "auto"; // auto | 1..6
    public string NoticePosition { get; set; } = "bottom-right";
    public bool EnableGroupIcon { get; set; } = true;
    public bool CommonTrayIcon { get; set; }
    public bool SysproxyTrayIcon { get; set; }
    public bool TunTrayIcon { get; set; }
    public bool TrafficGraph { get; set; } = true;
    public bool EnableMemoryUsage { get; set; } = true;
    public bool PauseTrafficOnBlur { get; set; } = true;
    public string FontFamily { get; set; } = "";
}

/// <summary>自动化任务设置。</summary>
public sealed class AutomationSettings
{
    public bool AutoProfileUpdate { get; set; } = true;
    public bool AutoDelayDetection { get; set; }
    public int AutoDelayDetectionIntervalMinutes { get; set; } = 60;
    public string AutoLogClean { get; set; } = "7d"; // off | 1d | 7d | 30d | 90d
    public bool EnableAutoBackupSchedule { get; set; }
    public int AutoBackupIntervalHours { get; set; } = 24;
    public bool AutoBackupOnChange { get; set; }
    public bool AutoCheckAppUpdate { get; set; } = true;
}

/// <summary>备份设置。WebDAV 凭据经 DPAPI 加密。</summary>
public sealed class BackupSettings
{
    public string WebDavUrl { get; set; } = "";
    public string WebDavUsername { get; set; } = "";
    /// <summary>DPAPI 加密后的 WebDAV 密码（Base64）。</summary>
    public string WebDavPasswordEncrypted { get; set; } = "";
    public string WebDavBackupDir { get; set; } = "flux-backups";
}

/// <summary>热键设置：action → 组合键。</summary>
public sealed class HotkeySettings
{
    public Dictionary<string, string> Hotkeys { get; set; } = new();
}

/// <summary>测试设置：网站测试列表。</summary>
public sealed class TestSettings
{
    public List<TestItem> TestList { get; set; } =
    [
        new TestItem { Uid = "google", Name = "Google", Url = "https://www.google.com/generate_204" },
        new TestItem { Uid = "github", Name = "GitHub", Url = "https://github.com" },
    ];
}

public sealed class TestItem
{
    public string Uid { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Url { get; set; } = "";
}

/// <summary>
/// Flux 应用设置（flux-settings.json），带 schema-version 的强类型子对象。
/// </summary>
public sealed class FluxSettings
{
    public const int CurrentSchemaVersion = 1;

    [JsonPropertyName("schema-version")]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public GeneralSettings General { get; set; } = new();
    public ProxySettings Proxy { get; set; } = new();
    public CoreSettings Core { get; set; } = new();
    public TunSettings Tun { get; set; } = new();
    public DnsSettings Dns { get; set; } = new();
    public TunnelSettings Tunnel { get; set; } = new();
    public UiSettings Ui { get; set; } = new();
    public AutomationSettings Automation { get; set; } = new();
    public BackupSettings Backup { get; set; } = new();
    public HotkeySettings Hotkey { get; set; } = new();
    public TestSettings Test { get; set; } = new();
}
