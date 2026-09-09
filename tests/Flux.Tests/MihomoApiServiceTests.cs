using System.Text.Json;
using Flux.Services;
using Xunit;

namespace Flux.Tests;

public class MihomoApiServiceTests
{
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
