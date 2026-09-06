using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Vxn.Models;

namespace Vxn.Services;

/// <summary>
/// mihomo External Controller WebSocket 实时通道：/traffic、/memory、/logs、/connections。
/// 每条通道独立连接，断线自动重连。
/// </summary>
public class MihomoStreamService
{
    private string _wsBase = "ws://127.0.0.1:9097";
    private string _secret = "";

    public event Action<double, double>? Traffic;
    public event Action<long>? Memory;
    public event Action<LogLine>? Log;
    public event Action<ConnectionsSnapshot>? Connections;

    private readonly List<ChannelRunner> _runners = [];
    private volatile bool _running;

    public void Configure(string controller, string secret)
    {
        var host = controller.StartsWith(':') ? "127.0.0.1" + controller : controller;
        host = host.Replace("http://", "").Replace("https://", "").TrimEnd('/');
        _wsBase = "ws://" + host;
        _secret = secret;
    }

    public void Start()
    {
        _running = true;
        StartChannel("traffic", HandleTraffic);
        StartChannel("memory", HandleMemory);
        StartChannel($"logs?level={Uri.EscapeDataString(AppServices.Config.Verge.LogLevel)}", HandleLog);
        StartChannel("connections", HandleConnections);
    }

    public void Stop()
    {
        _running = false;
        foreach (var r in _runners) r.Stop();
        _runners.Clear();
    }

    private void StartChannel(string path, Func<JsonElement, Task> handler)
    {
        var runner = new ChannelRunner(_wsBase + "/" + path, _secret, () => _running, handler);
        _runners.Add(runner);
        _ = Task.Run(runner.RunAsync);
    }

    private Task HandleTraffic(JsonElement json)
    {
        var up = json.TryGetProperty("up", out var u) ? u.GetDouble() : 0;
        var down = json.TryGetProperty("down", out var d) ? d.GetDouble() : 0;
        Traffic?.Invoke(up, down);
        return Task.CompletedTask;
    }

    private Task HandleMemory(JsonElement json)
    {
        var inuse = json.TryGetProperty("inuse", out var v) ? v.GetInt64() : 0;
        Memory?.Invoke(inuse);
        return Task.CompletedTask;
    }

    private Task HandleLog(JsonElement json)
    {
        var type = json.TryGetProperty("type", out var t) ? t.GetString() ?? "info" : "info";
        var payload = json.TryGetProperty("payload", out var p) ? p.GetString() ?? "" : "";
        Log?.Invoke(new LogLine(DateTime.Now, type, payload));
        return Task.CompletedTask;
    }

    private Task HandleConnections(JsonElement json)
    {
        var snapshot = new ConnectionsSnapshot();
        if (json.TryGetProperty("uploadTotal", out var ut)) snapshot.UploadTotal = ut.GetInt64();
        if (json.TryGetProperty("downloadTotal", out var dt)) snapshot.DownloadTotal = dt.GetInt64();
        if (json.TryGetProperty("connections", out var conns) && conns.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in conns.EnumerateArray())
                snapshot.Connections.Add(ConnectionItem.FromJson(c));
        }
        Connections?.Invoke(snapshot);
        return Task.CompletedTask;
    }

    private sealed class ChannelRunner
    {
        private readonly string _url;
        private readonly string _secret;
        private readonly Func<JsonElement, Task> _handler;
        private readonly Func<bool> _running;
        private ClientWebSocket? _ws;
        private CancellationTokenSource? _cts;

        public ChannelRunner(string url, string secret, Func<bool> running, Func<JsonElement, Task> handler)
        {
            _url = url;
            _secret = secret;
            _running = running;
            _handler = handler;
        }

        public void Stop()
        {
            try { _cts?.Cancel(); _ws?.Abort(); } catch { }
        }

        public async Task RunAsync()
        {
            var buffer = new byte[64 * 1024];
            while (_running())
            {
                try
                {
                    _cts = new CancellationTokenSource();
                    _ws = new ClientWebSocket();
                    if (!string.IsNullOrEmpty(_secret))
                        _ws.Options.SetRequestHeader("Authorization", "Bearer " + _secret);
                    await _ws.ConnectAsync(new Uri(_url), _cts.Token);

                    var message = new StringBuilder();
                    while (_running() && _ws.State == WebSocketState.Open)
                    {
                        message.Clear();
                        WebSocketReceiveResult result;
                        do
                        {
                            result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                            if (result.MessageType == WebSocketMessageType.Close)
                                throw new WebSocketException("closed by remote");
                            message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                        }
                        while (!result.EndOfMessage);

                        if (message.Length == 0) continue;
                        try
                        {
                            var json = JsonSerializer.Deserialize<JsonElement>(message.ToString());
                            await _handler(json);
                        }
                        catch (JsonException)
                        {
                            // 忽略无法解析的消息
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch
                {
                    // 断线重连
                }
                finally
                {
                    try { _ws?.Abort(); _ws?.Dispose(); } catch { }
                }

                if (_running()) await Task.Delay(2000);
            }
        }
    }
}
