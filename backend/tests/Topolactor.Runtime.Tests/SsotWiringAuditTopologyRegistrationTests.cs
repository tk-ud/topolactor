using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

// CI lane: topology/package/schema/relation/component reference continuity audit.
//
// SSOT contract (docs/design/pipeline-continuity-ssot.yaml, system-roadmap.yaml):
//   - Broken topology reference returns explicit error code; no silent fallback.
//   - Route missing returns canonical fallback jump events (Success=true), not an error response.
//   - Successful pipeline preserves StructureMapId/PackageId/SchemaId/ComponentIds in emission.
//   - ComponentIds is non-empty when a topology route is resolved.
//
// Test doubles isolate each failure mode. No DB access.
// Error codes tested: PACKAGE_RESOLVE_FAILED, SCHEMA_RESOLVE_FAILED.
public class SsotWiringAuditTopologyRegistrationTests
{
    // ─── Broken package reference: explicit error code ────────────────────────

    [Fact]
    public async Task TopologyRegistration_BrokenPackageReference_ReturnsPackageResolveFailedCode()
    {
        // Structure map resolves (default:entity:search) but package returns null.
        // SSOT requires explicit PACKAGE_RESOLVE_FAILED, not silence or a generic fallback.
        var executor = RuntimeExecutorTests.CreateExecutor(
            new PackageNullTopologyRepository());
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null, null);

        var response = await executor.ExecuteAsync(request);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "PACKAGE_RESOLVE_FAILED");
    }

    [Fact]
    public async Task TopologyRegistration_BrokenPackageReference_EmissionIsNull()
    {
        // When package resolution fails, emission must be null (no partial output).
        var executor = RuntimeExecutorTests.CreateExecutor(
            new PackageNullTopologyRepository());
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null, null);

        var response = await executor.ExecuteAsync(request);

        Assert.Null(response.Emission);
    }

    // ─── Broken schema reference: explicit error code ─────────────────────────

    [Fact]
    public async Task TopologyRegistration_BrokenSchemaReference_ReturnsSchemaResolveFailedCode()
    {
        // Package resolves but schema returns null.
        // SSOT requires explicit SCHEMA_RESOLVE_FAILED, not silence or generic fallback.
        var executor = RuntimeExecutorTests.CreateExecutor(
            new SchemaNullTopologyRepository());
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null, null);

        var response = await executor.ExecuteAsync(request);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "SCHEMA_RESOLVE_FAILED");
    }

    [Fact]
    public async Task TopologyRegistration_BrokenSchemaReference_EmissionIsNull()
    {
        var executor = RuntimeExecutorTests.CreateExecutor(
            new SchemaNullTopologyRepository());
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null, null);

        var response = await executor.ExecuteAsync(request);

        Assert.Null(response.Emission);
    }

    // ─── Successful pipeline: all four identity fields preserved ─────────────

    [Fact]
    public async Task TopologyRegistration_SuccessfulPipeline_StructureMapIdPreservedInEmission()
    {
        // StructureMapId must be present in emission when the topology route is resolved.
        var executor = RuntimeExecutorTests.CreateExecutor();
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null, null);

        var response = await executor.ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.Equal(TopologyRepository.DefaultStructureMapId, response.Emission?.StructureMapId);
    }

    [Fact]
    public async Task TopologyRegistration_SuccessfulPipeline_PackageIdPreservedInEmission()
    {
        var executor = RuntimeExecutorTests.CreateExecutor();
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null, null);

        var response = await executor.ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.Equal(TopologyRepository.DefaultPackageId, response.Emission?.PackageId);
    }

    [Fact]
    public async Task TopologyRegistration_SuccessfulPipeline_SchemaIdPreservedInEmission()
    {
        var executor = RuntimeExecutorTests.CreateExecutor();
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null, null);

        var response = await executor.ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.Equal(TopologyRepository.DefaultSchemaId, response.Emission?.SchemaId);
    }

    [Fact]
    public async Task TopologyRegistration_SuccessfulPipeline_ComponentIdsNonEmpty()
    {
        // ComponentIds must be non-empty in a resolved topology route.
        // An empty or null list indicates a broken component reference in the structure map.
        var executor = RuntimeExecutorTests.CreateExecutor();
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null, null);

        var response = await executor.ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.NotNull(response.Emission?.ComponentIds);
        Assert.NotEmpty(response.Emission!.ComponentIds!);
    }

    [Fact]
    public async Task TopologyRegistration_SuccessfulPipeline_DefaultComponentIdPresent()
    {
        // DefaultComponentId must appear in ComponentIds after successful pipeline execution.
        var executor = RuntimeExecutorTests.CreateExecutor();
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null, null);

        var response = await executor.ExecuteAsync(request);

        Assert.Contains(TopologyRepository.DefaultComponentId,
            response.Emission?.ComponentIds ?? []);
    }

    // ─── Route missing: canonical fallback (not error response) ──────────────

    [Fact]
    public async Task TopologyRegistration_RouteMissing_ReturnsSuccessNotError()
    {
        // Route missing is a canonical pipeline outcome per SSOT, not a system error.
        // Success=true with jump events, not Success=false with error codes.
        var executor = RuntimeExecutorTests.CreateExecutor();
        var request = new EndpointRequestDto(
            "Search", "missing_target", "entity", "Search", null, null, null);

        var response = await executor.ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.Empty(response.Errors);
    }

    [Fact]
    public async Task TopologyRegistration_RouteMissing_EmissionContainsJumpEventsForHubAndTopologyScopes()
    {
        // Canonical jump events for route_missing must cover both "hub" and "topology" scopes.
        var executor = RuntimeExecutorTests.CreateExecutor();
        var request = new EndpointRequestDto(
            "Search", "missing_target", "entity", "Search", null, null, null);

        var response = await executor.ExecuteAsync(request);

        var scopes = (response.Emission?.JumpEvents ?? []).Select(e => e.Scope).ToHashSet();
        Assert.Contains("hub", scopes);
        Assert.Contains("topology", scopes);
    }

    [Fact]
    public async Task TopologyRegistration_RouteMissing_JumpEventsHaveRouteMissingReason()
    {
        var executor = RuntimeExecutorTests.CreateExecutor();
        var request = new EndpointRequestDto(
            "Search", "missing_target", "entity", "Search", null, null, null);

        var response = await executor.ExecuteAsync(request);

        Assert.All(response.Emission?.JumpEvents ?? [],
            j => Assert.Equal("route_missing", j.Reason));
    }

    [Fact]
    public async Task TopologyRegistration_RouteMissing_TopologyIdentityFieldsAreNull()
    {
        // When route is missing, StructureMapId/PackageId/SchemaId are null (not zero-value Guids).
        // The SSOT requires explicit absence of identity, not a default/empty value.
        var executor = RuntimeExecutorTests.CreateExecutor();
        var request = new EndpointRequestDto(
            "Search", "missing_target", "entity", "Search", null, null, null);

        var response = await executor.ExecuteAsync(request);

        Assert.Null(response.Emission?.StructureMapId);
        Assert.Null(response.Emission?.PackageId);
        Assert.Null(response.Emission?.SchemaId);
    }
}

// Stub: returns null for package resolution (simulates a broken package reference).
internal sealed class PackageNullTopologyRepository()
    : TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double")
{
    public override Task<PackageRecord?> LoadPackageAsync(
        Guid packageId, CancellationToken ct = default)
        => Task.FromResult<PackageRecord?>(null);
}

// Stub: returns null for schema resolution (simulates a broken schema reference).
internal sealed class SchemaNullTopologyRepository()
    : TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double")
{
    public override Task<SchemaRecord?> LoadSchemaAsync(
        Guid schemaId, CancellationToken ct = default)
        => Task.FromResult<SchemaRecord?>(null);
}
