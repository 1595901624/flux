using System.Text.Json;
using Flux.Services;
using Xunit;

namespace Flux.Tests;

public class MihomoApiServiceTests
{
    [Fact]
    public async Task ReconfigureAfterRequestsUsesNewAddressAndSecret()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        var api = new MihomoApiService(http);
        api.Configure("127.0.0.1:9097", "old");
        await api.GetVersionAsync();
        api.Configure("127.0.0.1:9098", "new");
        await api.GetVersionAsync();
        api.Configure("127.0.0.1:9097", "");
        await api.GetVersionAsync();

        Assert.Equal(new[] { ("http://127.0.0.1:9097/version", "Bearer old"),
            ("http://127.0.0.1:9098/version", "Bearer new"),
            ("http://127.0.0.1:9097/version", "") }, handler.Requests);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(string Address, string Authorization)> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.RequestUri!.AbsoluteUri, request.Headers.Authorization?.ToString() ?? ""));
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                { Content = new StringContent("{\"version\":\"test\"}") });
        }
    }

    [Fact]
    public void FindsOnlyConnectionsWhoseChainUsesPreviousProxy()
    {
        using var document = JsonDocument.Parse("""
            {
              "connections": [
                { "id": "old-direct", "chains": ["old-node", "main-group"] },
                { "id": "nested", "chains": ["leaf", "old-node", "main-group"] },
                { "id": "new", "chains": ["new-node", "main-group"] },
                { "id": "missing-chain" },
                { "chains": ["old-node"] }
              ]
            }
            """);

        var ids = MihomoApiService.FindConnectionIdsUsingProxy(
            document.RootElement, "old-node");

        Assert.Equal(["old-direct", "nested"], ids);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"connections\":null}")]
    public void ReturnsEmptyWhenConnectionsSnapshotIsUnavailable(string json)
    {
        using var document = JsonDocument.Parse(json);

        Assert.Empty(MihomoApiService.FindConnectionIdsUsingProxy(
            document.RootElement, "old-node"));
    }
}
