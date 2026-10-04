namespace Flux.Services;

public partial class ConfigService
{
    /// <summary>应用设置已持久化，供托盘等 UI 刷新；回滚保存也会通知。</summary>
    public event Action? SettingsChanged;

    public void SaveVerge()
    {
        WriteAllTextAtomic(Paths.VergeConfigFile, Verge.Serialize());
        SettingsChanged?.Invoke();
    }
}
