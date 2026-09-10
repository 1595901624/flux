using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using Flux.Services;

namespace Flux;

public static class Program
{
    public static string[] Args { get; private set; } = [];

    [STAThread]
    public static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Args = GetEffectiveArgs(args);

        if (!SingleInstance.TryAcquireOrForward(Args))
        {
            return 0;
        }

        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });

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
