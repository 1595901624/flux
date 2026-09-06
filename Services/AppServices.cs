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

    public static bool Initialized { get; private set; }

    public static void Initialize()
    {
        if (Initialized) return;
        Paths.Initialize();
        Config = ConfigService.LoadOrCreate();
        Subscription = new SubscriptionService();

        var (controller, secret) = Config.GetControllerInfo();
        Api.Configure(controller, secret);
        Streams.Configure(controller, secret);

        Initialized = true;
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
        catch { }
    }
}
