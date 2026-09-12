namespace Flux.Services;

/// <summary>
/// 测试工程桩：链接编译的源文件（WinInetProxySettings/ProfileContentValidator）引用 L10n，
/// 但测试环境没有 MRT 资源。桩直接回退资源键，语义与 L10n.T 的回退一致。
/// </summary>
public static class L10n
{
    public static string T(string key) => key;

    public static string F(string key, params object[] args) => key;
}
