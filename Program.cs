using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using Flux.Services;
using System.IO;

namespace Flux;

public static class Program
{
    public static string[] Args { get; private set; } = [];

    public static void Trace(string message)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(Path.GetTempPath(), "flux-startup-trace.txt"),
                $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch { }
    }

    [STAThread]
    public static int Main(string[] args)
    {
        Trace("main enter");
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Trace("comwrappers ok");

        // 本项目启用了 WindowsAppSDKSelfContained。构建系统会生成自包含运行时的
        // UndockedRegFreeWinRT 初始化代码，不能再调用 Bootstrap.TryInitialize；
        // 后者会加载机器上安装的另一套 Windows App Runtime，导致 CoreMessagingXP
        // 在 Application.Start 前以 0xc0000602 fail-fast 终止。
        Trace(PackageIdentity.IsPackaged ? "packaged runtime" : "self-contained runtime");

        Args = GetEffectiveArgs(args);
        Trace("args ok");

        if (!SingleInstance.TryAcquireOrForward(Args))
        {
            Trace("single instance: forwarded, exit");
            return 0;
        }
        Trace("mutex ok");

        Application.Start(p =>
        {
            Trace("application.start enter");
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
            Trace("app ctor ok");
        });

        Trace("application.start exit");
        return 0;
    }

    private static string[] GetEffectiveArgs(string[] commandLineArgs)
    {
        if (!PackageIdentity.IsPackaged) return commandLineArgs;

        try
        {
            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            if (activation.Kind == ExtendedActivationKind.Protocol &&
                activation.Data is ProtocolActivatedEventArgs protocolArgs)
            {
                return [protocolArgs.Uri.AbsoluteUri];
            }
        }
        catch
        {
            // 激活参数读取失败时仍允许普通启动。
        }

        return commandLineArgs;
    }
}
