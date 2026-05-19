using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class OperationVectorResolverTests
{
    [Fact]
    public void Resolve_NormalizesMixedCaseSegments_ToLowercaseAttractorKey()
    {
        var resolver = new OperationVectorResolver();
        var request = new EndpointRequestDto(
            OperationType: "Search",
            Target: "Default",
            Layer: "Entity",
            Action: "Search",
            IdOrHubId: null,
            Payload: null,
            Context: null);

        var vector = resolver.Resolve(request);

        Assert.Equal("default:entity:search", vector.AttractorKey);
    }

    [Fact]
    public void Resolve_ForwardsIdOrHubId_ToVector()
    {
        var resolver = new OperationVectorResolver();
        var hubId = Guid.NewGuid();
        var request = new EndpointRequestDto(
            OperationType: "Search",
            Target: "default",
            Layer: "entity",
            Action: "search",
            IdOrHubId: hubId,
            Payload: null,
            Context: null);

        var vector = resolver.Resolve(request);

        Assert.Equal(hubId, vector.IdOrHubId);
    }

    [Fact]
    public void Resolve_NullIdOrHubId_YieldsNullInVector()
    {
        var resolver = new OperationVectorResolver();
        var request = new EndpointRequestDto(
            OperationType: "Search",
            Target: "default",
            Layer: "entity",
            Action: "search",
            IdOrHubId: null,
            Payload: null,
            Context: null);

        var vector = resolver.Resolve(request);

        Assert.Null(vector.IdOrHubId);
    }
}
