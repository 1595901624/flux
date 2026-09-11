using System.Text.Json;
using Flux.Core.Contracts;
using Flux.Core.Service;
using Xunit;

namespace Flux.Tests;

/// <summary>服务协议帧与请求/响应序列化测试。</summary>
public class ServiceProtocolTests
{
    [Fact]
    public async Task Frame_编解码往返()
    {
        var request = ServiceRequest.StartCore(@"C:\data\flux\runtime.yaml", @"C:\app\core\mihomo.exe", @"C:\data\flux");
        var frame = ServiceFrame.Encode(request);

        // 前缀为 4 字节小端长度
        var length = BitConverter.ToInt32(frame, 0);
        Assert.Equal(frame.Length - 4, length);

        using var stream = new MemoryStream(frame);
        var decoded = await ServiceFrame.DecodeAsync<ServiceRequest>(stream, CancellationToken.None);
        Assert.NotNull(decoded);
        Assert.Equal("start_core", decoded!.Operation);
        Assert.Equal(request.ConfigPath, decoded.ConfigPath);
        Assert.Equal(request.CorePath, decoded.CorePath);
        Assert.Equal(request.RequestId, decoded.RequestId);
    }

    [Fact]
    public async Task Frame_响应往返_保留状态与错误()
    {
        var ok = ServiceResponse.Success("id-1", ServiceCoreState.Running, 4321) with { ServiceVersion = "1.0.0" };
        var fail = ServiceResponse.Fail("id-2", "拒绝执行");

        var ok2 = (await ServiceFrame.DecodeAsync<ServiceResponse>(
            new MemoryStream(ServiceFrame.Encode(ok)), CancellationToken.None))!;
        var fail2 = (await ServiceFrame.DecodeAsync<ServiceResponse>(
            new MemoryStream(ServiceFrame.Encode(fail)), CancellationToken.None))!;

        Assert.True(ok2.Ok);
        Assert.Equal(ServiceCoreState.Running, ok2.State);
        Assert.Equal(4321, ok2.ProcessId);
        Assert.Equal("1.0.0", ok2.ServiceVersion);
        Assert.False(fail2.Ok);
        Assert.Equal("拒绝执行", fail2.Error);
    }

    [Fact]
    public void Frame_非法长度_抛出异常()
    {
        var frame = new byte[4 + 2];
        BitConverter.GetBytes(int.MaxValue).CopyTo(frame, 0);
        using var stream = new MemoryStream(frame);
        Assert.Throws<InvalidDataException>(() =>
        {
            try
            {
                ServiceFrame.DecodeAsync<ServiceRequest>(stream, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (AggregateException ex)
            {
                throw ex.GetBaseException();
            }
        });
    }

    [Fact]
    public void Request_序列化包含协议版本()
    {
        var json = JsonSerializer.Serialize(ServiceRequest.Status());
        Assert.Contains("\"Version\"", json);
        var deserialized = JsonSerializer.Deserialize<ServiceRequest>(json);
        Assert.Equal(ServiceProtocol.Version, deserialized!.Version);
        Assert.Equal(ServiceProtocol.Version, 1); // 当前协议版本
    }

    [Fact]
    public void PipeName_包含协议版本()
    {
        Assert.Contains("v" + ServiceProtocol.Version, ServiceProtocol.PipeName);
    }
}

/// <summary>服务端路径校验测试：拒绝数据目录外配置与任意内核路径。</summary>
public class ServicePathValidatorTests
{
    private readonly string _dataDir;
    private readonly string _installDir;

    public ServicePathValidatorTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "flux-svc-tests", Guid.NewGuid().ToString("N"), "flux");
        _installDir = Path.Combine(Path.GetTempPath(), "flux-svc-tests", Guid.NewGuid().ToString("N"), "app");
        Directory.CreateDirectory(Path.Combine(_dataDir, "core-cache", "v1.19.30"));
        Directory.CreateDirectory(Path.Combine(_installDir, "core"));
    }

    [Fact]
    public void ValidateConfigPath_数据目录内_通过()
    {
        var error = ServicePathValidator.ValidateConfigPath(
            Path.Combine(_dataDir, "runtime.yaml"), _dataDir);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateConfigPath_数据目录外_拒绝()
    {
        var error = ServicePathValidator.ValidateConfigPath(@"C:\Windows\system.ini", _dataDir);
        Assert.NotNull(error);
        Assert.Equal("invalid_path", error!.Code);
    }

    [Fact]
    public void ValidateConfigPath_目录穿越_拒绝()
    {
        var error = ServicePathValidator.ValidateConfigPath(
            Path.Combine(_dataDir, "..", "..", "secret.yaml"), _dataDir);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("runtime.yaml")] // 相对路径
    [InlineData("C:\\x\\runtime.txt")] // 扩展名不符
    public void ValidateConfigPath_非法输入_拒绝(string path)
    {
        Assert.NotNull(ServicePathValidator.ValidateConfigPath(path, _dataDir));
    }

    [Fact]
    public void ValidateCorePath_内置内核_通过()
    {
        var error = ServicePathValidator.ValidateCorePath(
            Path.Combine(_installDir, "core", "mihomo.exe"), _installDir, _dataDir);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateCorePath_版本化缓存内核_通过()
    {
        var error = ServicePathValidator.ValidateCorePath(
            Path.Combine(_dataDir, "core-cache", "v1.19.30", "mihomo.exe"), _installDir, _dataDir);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateCorePath_任意路径_拒绝()
    {
        var error = ServicePathValidator.ValidateCorePath(@"C:\evil\mihomo.exe", _installDir, _dataDir);
        Assert.NotNull(error);
        Assert.Equal("invalid_core", error!.Code);
    }

    [Fact]
    public void ValidateCorePath_非mihomo文件名_拒绝()
    {
        var error = ServicePathValidator.ValidateCorePath(
            Path.Combine(_installDir, "core", "cmd.exe"), _installDir, _dataDir);
        Assert.NotNull(error);
    }
}
