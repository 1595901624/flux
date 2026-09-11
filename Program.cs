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

        // 仅未打包（self-contained portable）部署需要初始化 Bootstrap；
        // 打包（MSIX）应用由包依赖自动解析运行时，调用 Bootstrap 反而失败。
        if (!PackageIdentity.IsPackaged)
        {
            Trace("bootstrap begin");
            // 0x00010008 = WinAppSDK 1.8（major=1, minor=8）。
            if (!Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.TryInitialize(
                    0x00010008, out var bootstrapHresult))
            {
                Trace($"bootstrap failed 0x{bootstrapHresult:X}");
                return bootstrapHresult != 0 ? bootstrapHresult : -1;
            }
            Trace("bootstrap ok");
        }
        else
        {
            Trace("packaged, bootstrap skipped");
        }

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
