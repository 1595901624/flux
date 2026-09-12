namespace Flux.Services;

/// <summary>核心服务集合（轻量服务定位器，应用级单例）。</summary>
public static class AppServices
{
    public static ConfigService Config { get; private set; } = null!;
    public static CoreProcessService Core { get; } = new();
    public static MihomoApiService Api { get; } = new();
    public static MihomoStreamService Streams { get; } = new();
    public static SubscriptionService Subscription { get; private set; } = null!;
    public static SysProxyService SysProxy { get; } = new();
    public static TrayService Tray { get; } = new();
    public static AutoStartService AutoStart { get; } = new();
    public static DeepLinkService DeepLink { get; } = new();
    public static PrivilegeBroker Privilege { get; } = new();

    /// <summary>本地备份服务（ZIP：应用配置 + 订阅 + 增强文件，不含日志/内核缓存）。</summary>
    public static Flux.Core.Backup.LocalBackupService Backup { get; private set; } = null!;
    public static HotkeyService Hotkey { get; } = new();

    public static bool Initialized { get; private set; }

    public static void Initialize()
    {
        if (Initialized) return;
        Paths.Initialize();
        Config = ConfigService.LoadOrCreate();
        Subscription = new SubscriptionService();
        Backup = new Flux.Core.Backup.LocalBackupService(
            Paths.DataBackupDir,
            () => new Flux.Core.Backup.BackupLayout
            {
                DataDir = Paths.AppDataDir,
                ProfilesDir = Paths.ProfilesDir,
                ExtraFiles = [Path.Combine(Paths.AppDataDir, "flux-settings.json")],
            },
            (level, message) => LogService.App(message, level));

        var (controller, secret) = Config.GetControllerInfo();
        Api.Configure(controller, secret);
        Streams.Configure(controller, secret);

        Initialized = true;
    }

    /// <summary>备份恢复后重新加载磁盘配置，并让 API 客户端切换到恢复后的控制器。</summary>
    public static void ReloadConfiguration()
    {
        Config = ConfigService.LoadOrCreate();
        var (controller, secret) = Config.GetControllerInfo();
        Api.Configure(controller, secret);
        Streams.Configure(controller, secret);
    }

    /// <summary>退出前清理：恢复系统代理、停止内核。</summary>
    public static async Task ShutdownAsync()
    {
        try
        {
            SysProxy.Reset();
            Streams.Stop();
            await Core.StopAsync();
        }
        catch (Exception ex)
        {
            LogService.App(L10n.F("App_ShutdownFailed", ex.Message), "warn");
        }
    }
}
