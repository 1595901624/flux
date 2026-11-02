using Windows.ApplicationModel.DataTransfer;

namespace Flux.Services;

/// <summary>复制用于终端当前会话的代理环境变量命令。</summary>
public static class ProxyEnvironmentService
{
    public static void Copy(bool powerShell)
    {
        var port = AppServices.Config.MixedPort;
        var text = powerShell
            ? $"""
                $env:HTTP_PROXY = 'http://127.0.0.1:{port}'
                $env:HTTPS_PROXY = 'http://127.0.0.1:{port}'
                $env:ALL_PROXY = 'socks5://127.0.0.1:{port}'
                """
            : $"""
                set HTTP_PROXY=http://127.0.0.1:{port}
                set HTTPS_PROXY=http://127.0.0.1:{port}
                set ALL_PROXY=socks5://127.0.0.1:{port}
                """;

        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }
}
