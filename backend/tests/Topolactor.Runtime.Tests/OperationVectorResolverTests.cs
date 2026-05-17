using Topolactor.Runtime;
using Topolactor.Schema;

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
}
