using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Flux.Services;

namespace Flux;

public static class Program
{
    public static string[] Args { get; private set; } = [];

    [STAThread]
    public static int Main(string[] args)
    {
        Args = args;
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (!SingleInstance.TryAcquireOrForward(args))
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
}
