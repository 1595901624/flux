using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using Flux.Services;
using Xunit;

namespace Flux.Tests;

public sealed class MihomoStreamServiceTests
{
    [Fact]
    public async Task ReconfigureAndRestartReplaceConnectionsCredentialsAndLogLevel()
    {
        await using var first = new WebSocketServer();
        await using var second = new WebSocketServer();
        var streams = new MihomoStreamService();
        var oldLevel = AppServices.Config.Verge.LogLevel;
        try
        {
            streams.Configure(first.Address, "old");
            streams.Start();
            await WaitAsync(() => first.Requests.Count == 4);
            Assert.All(first.Requests, r => Assert.Equal("Bearer old", r.Authorization));

            streams.Configure(first.Address, "new");
            await WaitAsync(() => first.Requests.Count == 8 && first.Closed >= 4);
            Assert.All(first.Requests.Skip(4), r => Assert.Equal("Bearer new", r.Authorization));

            streams.Configure(second.Address, "next");
            await WaitAsync(() => second.Requests.Count == 4 && first.Closed == 8);
            Assert.All(second.Requests, r => Assert.Equal("Bearer next", r.Authorization));
            AppServices.Config.Verge.LogLevel = "debug";
            streams.Restart();
            await WaitAsync(() => second.Requests.Count == 8 && second.Closed >= 4);
            Assert.Contains(second.Requests.Skip(4), r => r.Path == "/logs?level=debug");
            streams.Stop();
            await WaitAsync(() => second.Closed == 8);
            // 等待超过重连间隔，确认已停止的旧通道不会被新的 Start 复活。
            await Task.Delay(2200);
            Assert.Equal(8, first.Requests.Count);
            Assert.Equal(8, second.Requests.Count);
        }
        finally
        {
            streams.Stop();
            AppServices.Config.Verge.LogLevel = oldLevel;
        }
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition()) await Task.Delay(20, timeout.Token);
    }

    private sealed class WebSocketServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentBag<Task> _clients = [];
        private readonly Task _accept;
        private int _closed;
        public ConcurrentQueue<(string Path, string Authorization)> Requests { get; } = new();
        public int Closed => Volatile.Read(ref _closed);
        public string Address { get; }

        public WebSocketServer()
        {
            _listener.Start();
            Address = $"127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
            _accept = AcceptAsync();
        }

        private async Task AcceptAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                    _clients.Add(HandleAsync(await _listener.AcceptTcpClientAsync(_cts.Token)));
            }
            catch (OperationCanceledException) { }
        }

        private async Task HandleAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    var stream = client.GetStream();
                    var headers = new StringBuilder();
                    var one = new byte[1];
                    while (!headers.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
                    {
                        if (await stream.ReadAsync(one, _cts.Token) == 0) return;
                        headers.Append((char)one[0]);
                    }
                    var lines = headers.ToString().Split("\r\n");
                    string Header(string name) => lines.FirstOrDefault(l => l.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase))?
                        .Split(':', 2)[1].Trim() ?? "";
                    var key = Header("Sec-WebSocket-Key");
                    var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                    await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n"), _cts.Token);
                    using var socket = WebSocket.CreateFromStream(stream, isServer: true, subProtocol: null, TimeSpan.FromSeconds(30));
                    Requests.Enqueue((lines[0].Split(' ')[1], Header("Authorization")));
                    var buffer = new byte[1024];
                    while ((await socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token)).MessageType != WebSocketMessageType.Close) { }
                }
                catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or IOException) { }
                finally { Interlocked.Increment(ref _closed); }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            await _accept;
            _listener.Stop();
            await Task.WhenAll(_clients);
            _cts.Dispose();
        }
    }
}
