using System.IO.Pipes;

namespace Vxn.Services;

/// <summary>
/// 单实例守卫：第二个实例通过命名管道把命令行参数（如 clash:// 深链）转发给主实例后退出。
/// </summary>
public static class SingleInstance
{
    private const string MutexName = @"Local\Vxn.SingleInstance";
    private const string PipeName = "Vxn.SingleInstance.Pipe";

    /// <summary>主实例收到其他实例转发来的参数（原始 argv 项）。</summary>
    public static event EventHandler<string[]>? ForwardedArgsReceived;

    private static Mutex? _mutex;

    public static bool TryAcquireOrForward(string[] args)
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (createdNew)
        {
            _ = Task.Run(ListenPipeAsync);
            return true;
        }

        if (args.Length > 0)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(1000);
                using var writer = new StreamWriter(client) { AutoFlush = true };
                writer.WriteLine(string.Join('\0', args));
            }
            catch
            {
                // 主实例可能正在退出，忽略
            }
        }

        return false;
    }

    private static async Task ListenPipeAsync()
    {
        while (true)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                await server.WaitForConnectionAsync().ConfigureAwait(false);
                using var reader = new StreamReader(server);
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(line))
                {
                    ForwardedArgsReceived?.Invoke(null, line.Split('\0'));
                }
            }
            catch
            {
                // 管道异常时重建继续监听
            }
            finally
            {
                server?.Dispose();
            }
        }
    }
}
