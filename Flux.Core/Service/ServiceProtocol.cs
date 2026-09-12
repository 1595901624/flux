using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Flux.Core.Service;

/// <summary>Flux 服务命名管道协议常量与消息模型。</summary>
public static class ServiceProtocol
{
    /// <summary>协议版本：请求与响应必须匹配，否则客户端报告 VersionMismatch。</summary>
    public const int Version = 2;

    /// <summary>命名管道名称（含协议版本，避免新旧进程互连）。</summary>
    public const string PipeName = "flux-service-v" + "2";

    /// <summary>单条消息上限（字节），防止内存耗尽。</summary>
    public const int MaxMessageBytes = 64 * 1024;

    /// <summary>连接读超时。</summary>
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
}

/// <summary>服务请求。只允许预定义操作，不允许任意命令。</summary>
public sealed record ServiceRequest
{
    public int Version { get; init; } = ServiceProtocol.Version;
    public string RequestId { get; init; } = Guid.NewGuid().ToString("N");
    public string Operation { get; init; } = "";

    // start_core 参数
    public string? ConfigPath { get; init; }
    public string? CorePath { get; init; }
    public string? ConfigDir { get; init; }

    public static ServiceRequest StartCore(string configPath, string corePath, string configDir) => new()
    {
        Operation = "start_core",
        ConfigPath = configPath,
        CorePath = corePath,
        ConfigDir = configDir,
    };

    public static ServiceRequest StopCore() => new() { Operation = "stop_core" };
    public static ServiceRequest Status() => new() { Operation = "status" };
    public static ServiceRequest GetVersion() => new() { Operation = "version" };
}

/// <summary>内核在服务内的运行状态。</summary>
public enum ServiceCoreState
{
    NotRunning = 0,
    Running = 1,
}

/// <summary>服务响应。</summary>
public sealed record ServiceResponse
{
    public int Version { get; init; } = ServiceProtocol.Version;
    public string RequestId { get; init; } = "";
    public bool Ok { get; init; }
    public string? Error { get; init; }

    public ServiceCoreState State { get; init; }
    public string? ServiceVersion { get; init; }
    public int ProcessId { get; init; }

    public static ServiceResponse Success(string requestId, ServiceCoreState state = ServiceCoreState.NotRunning, int pid = 0) =>
        new() { RequestId = requestId, Ok = true, State = state, ProcessId = pid };

    public static ServiceResponse Fail(string requestId, string error) =>
        new() { RequestId = requestId, Ok = false, Error = error };
}

/// <summary>协议帧：4 字节小端长度 + UTF-8 JSON。</summary>
public static class ServiceFrame
{
    public static byte[] Encode<T>(T message, JsonSerializerOptions? options = null)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(message, options);
        if (json.Length > ServiceProtocol.MaxMessageBytes)
            throw new InvalidOperationException("协议消息超过大小上限");
        var frame = new byte[4 + json.Length];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(frame, json.Length);
        Buffer.BlockCopy(json, 0, frame, 4, json.Length);
        return frame;
    }

    public static async Task<T?> DecodeAsync<T>(Stream stream, CancellationToken ct)
    {
        var lengthBytes = await ReadExactlyAsync(stream, 4, ct).ConfigureAwait(false);
        var length = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
        if (length is < 2 or > ServiceProtocol.MaxMessageBytes)
            throw new InvalidDataException($"非法的协议帧长度: {length}");
        var payload = await ReadExactlyAsync(stream, length, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private static async Task<byte[]> ReadExactlyAsync(Stream stream, int count, CancellationToken ct)
    {
        var buffer = new byte[count];
        await stream.ReadExactlyAsync(buffer.AsMemory(0, count), ct).ConfigureAwait(false);
        return buffer;
    }
}
