using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Tests for registry_attractor_runtime manifest dispatch routing and handler behavior.
///
/// SSOT completion conditions (docs/system-roadmap.yaml product.registry_attractor_runtime_dispatch_handler):
///   - IDispatchableRuntime_handler_for_registry_attractor_runtime_exists
///   - handler_registered_in_Program_cs_handler_dictionary
///   - manifest_with_runtime_mapping_registry_attractor_runtime_routes_correctly
///   - unknown_destination_returns_RUNTIME_DESTINATION_UNKNOWN_not_silent_fallback
///   - dispatch_boundary_covered_by_tests
/// </summary>
public class RegistryAttractorDispatchRuntimeTests
{
    // ─── Manifest dispatch routing ────────────────────────────────────────────

    [Fact]
    public async Task DispatchAsync_RegistryAttractorRuntime_RoutesToRegisteredHandler()
    {
        // Prove that ManifestDispatcher selects the registry_attractor_runtime handler
        // when the manifest's runtime_mapping.runtime_destination = "registry_attractor_runtime".
        var sentinel = JsonSerializer.SerializeToElement(new { dispatchedBy = "registry_attractor_dispatch_test" });
        var fakeHandler = new FakeDispatchableRuntime(sentinel);
        var manifestRepo = new StubManifestRepository(
            new ManifestRecord(
                Guid.NewGuid(),
                RelationRegistryId: null,
                Topology: BuildRuntimeMappingTopology("registry_attractor_runtime"),
                Status: "active"));
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo,
            extraHandlers: new Dictionary<string, IDispatchableRuntime>
            {
                ["registry_attractor_runtime"] = fakeHandler
            });
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);

        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success);
        Assert.True(fakeHandler.WasCalled,
            "registry_attractor_runtime handler must be called when manifest runtime_destination matches.");
    }

    [Fact]
    public async Task DispatchAsync_UnknownDestination_ReturnsRuntimeDestinationUnknown()
    {
        // Unknown runtime_destination must return RUNTIME_DESTINATION_UNKNOWN.
        // No silent fallback permitted. This invariant must hold after registry_attractor_runtime registration.
        var manifestRepo = new StubManifestRepository(
            new ManifestRecord(
                Guid.NewGuid(),
                RelationRegistryId: null,
                Topology: BuildRuntimeMappingTopology("nonexistent_destination_xyz"),
                Status: "active"));
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo,
            extraHandlers: new Dictionary<string, IDispatchableRuntime>
            {
                ["registry_attractor_runtime"] = new FakeDispatchableRuntime(null)
            });
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);

        var response = await dispatcher.DispatchAsync(request);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "RUNTIME_DESTINATION_UNKNOWN");
    }

    // ─── Handler validation: required payload fields ──────────────────────────

    [Fact]
    public async Task ExecuteAsync_MissingSourceSetId_ReturnsExplicitError()
    {
        var handler = BuildHandler();
        var payload = JsonSerializer.SerializeToElement(new { basis_window = "1h" });
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, payload, null);

        var response = await handler.ExecuteAsync(request, manifestId: null);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "REGISTRY_ATTRACTOR_SOURCE_SET_ID_REQUIRED");
    }

    [Fact]
    public async Task ExecuteAsync_MissingBasisWindow_ReturnsExplicitError()
    {
        var handler = BuildHandler();
        var payload = JsonSerializer.SerializeToElement(new { source_set_id = "set_a" });
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, payload, null);

        var response = await handler.ExecuteAsync(request, manifestId: null);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "REGISTRY_ATTRACTOR_BASIS_WINDOW_REQUIRED");
    }

    [Fact]
    public async Task ExecuteAsync_MissingPayload_ReturnsExplicitError()
    {
        var handler = BuildHandler();
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);

        var response = await handler.ExecuteAsync(request, manifestId: null);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "REGISTRY_ATTRACTOR_SOURCE_SET_ID_REQUIRED");
    }

    [Fact]
    public async Task ExecuteAsync_ValidPayload_ReturnsSuccessWithObservationData()
    {
        // Handler with test-double repos: no candidates → NoChange status. Still returns Success.
        var handler = BuildHandler();
        var payload = JsonSerializer.SerializeToElement(new { source_set_id = "set_a", basis_window = "1h" });
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, payload, null);

        var response = await handler.ExecuteAsync(request, manifestId: null);

        Assert.True(response.Success);
        Assert.NotNull(response.Emission);
        Assert.NotNull(response.Emission!.Data);
        var data = response.Emission.Data!.Value;
        Assert.Equal(JsonValueKind.Object, data.ValueKind);
        Assert.True(data.TryGetProperty("status", out _), "Emission data must include 'status'.");
        Assert.True(data.TryGetProperty("source_set_id", out _), "Emission data must include 'source_set_id'.");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static RegistryAttractorDispatchRuntime BuildHandler()
    {
        var topologyRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var sqlAttentionRepo = new SqlAttentionLogsRepository(
            NullLogger<SqlAttentionLogsRepository>.Instance, "test-double");
        var explorationRuntime = new HubAttractorExplorationRuntime(
            NullLogger<HubAttractorExplorationRuntime>.Instance,
            topologyRepo,
            sqlAttentionRepo);
        return new RegistryAttractorDispatchRuntime(
            NullLogger<RegistryAttractorDispatchRuntime>.Instance,
            explorationRuntime,
            sqlAttentionRepo);
    }

    private static IReadOnlyList<JsonElement> BuildRuntimeMappingTopology(string runtimeDestination)
    {
        var entry = JsonSerializer.SerializeToElement(new
        {
            type = "runtime_mapping",
            runtime_destination = runtimeDestination
        });
        return [entry];
    }
}
