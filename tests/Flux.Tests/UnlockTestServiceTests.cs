using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Flux.Core.Unlock;
using Xunit;

namespace Flux.Tests;

public sealed class UnlockTestServiceTests
{
    [Fact]
    public void BuiltinChecksUseCanonicalStatusesForSuccessAndRestrictions()
    {
        var service = new UnlockTestService(7897);
        var allowed = new[] { UnlockStatus.Supported, UnlockStatus.Unsupported, UnlockStatus.Unknown };
        foreach (var check in service.Checks)
            foreach (var status in new[] { 200, 403, 404, 451, 500 })
                Assert.Contains(check.Judge(status, "").Status, allowed);
        Assert.Equal(UnlockStatus.Supported, service.Checks.Single(c => c.Id == "chatgpt")
            .Judge(200, "{\"display_name\":\"ChatGPT\"}").Status);
        Assert.Equal(UnlockStatus.Unsupported, service.Checks.Single(c => c.Id == "youtube")
            .Judge(200, "Premium is not available in your country").Status);
        Assert.Equal(UnlockStatus.Unsupported, service.Checks.Single(c => c.Id == "gemini")
            .Judge(200, "not available in your country").Status);
    }

    [Fact]
    public async Task ProxyConnectionFailureProducesFailedResultAndReportsEachCheck()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var service = new UnlockTestService(port);
        var reports = new ConcurrentQueue<UnlockResult>();
        var results = await service.RunAllAsync(1000, progress: new InlineProgress(reports.Enqueue));
        Assert.Equal(service.Checks.Count, reports.Count);
        Assert.Equal(service.Checks.Count, reports.Select(r => r.Id).Distinct().Count());
        Assert.All(results, result =>
        {
            Assert.Equal(UnlockStatus.Failed, result.Status);
            Assert.False(string.IsNullOrWhiteSpace(result.Detail));
        });
    }

    [Fact]
    public async Task TimeoutProducesFailedResultRatherThanUntestedStatus()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var service = new UnlockTestService(((IPEndPoint)listener.LocalEndpoint).Port);
            var check = new UnlockCheck { Id = "timeout", Name = "Timeout", Url = "http://unlock-test.invalid/" };
            var pending = service.RunAsync(check, 1000);
            using var connection = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
            var result = await pending;
            Assert.Equal(UnlockStatus.Failed, result.Status);
            Assert.Equal("请求超时", result.Detail);
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public async Task CallerCancellationDoesNotBecomeFailureResult()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var service = new UnlockTestService(7897);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RunAsync(service.Checks[0], 1000, cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RunAllAsync(1000, cts.Token));
    }

    private sealed class InlineProgress(Action<UnlockResult> report) : IProgress<UnlockResult>
    {
        public void Report(UnlockResult value) => report(value);
    }
}
