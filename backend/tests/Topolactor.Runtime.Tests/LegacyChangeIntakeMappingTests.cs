using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class LegacyChangeIntakeMappingTests
{
    private static TargetDispatchOverride CreateTargetDispatchOverride()
    {
        var topologyRepository = new Topolactor.Repository.TopologyRepository(NullLogger<Topolactor.Repository.TopologyRepository>.Instance, "test-double");
        var contextRouteRepository = new Topolactor.Repository.ContextRouteRepository(NullLogger<Topolactor.Repository.ContextRouteRepository>.Instance, "test-double");
        var topologyVectorRuntime = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, contextRouteRepository);
        var adminRuntime = new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            contextRouteRepository,
            new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topologyRepository, topologyVectorRuntime),
            new PackageGeneratorRuntime(
                NullLogger<PackageGeneratorRuntime>.Instance,
                new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double")),
            new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double"));
        return new TargetDispatchOverride(NullLogger<TargetDispatchOverride>.Instance, topologyRepository, adminRuntime);
    }

    [Fact]
    public void BuildLegacyHookRequest_MapsTableNameAsTargetAndHookTrigger()
    {
        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            RuntimeExecutorTests.CreateExecutor(),
            new OperationVectorResolver(),
            CreateTargetDispatchOverride());

        var intake = new LegacyChangeIntakeRequestDto(
            TableName: "orders",
            RowId: "42",
            Operation: "update",
            ChangedDataJsonb: System.Text.Json.JsonDocument.Parse("{\"status\":\"done\"}").RootElement,
            DiffJsonb: null,
            Actor: "system",
            Source: "legacy_api",
            OccurredAt: null,
            Role: null);

        var (request, errors) = dispatcher.BuildLegacyHookRequest(intake);

        Assert.Empty(errors);
        Assert.NotNull(request);
        Assert.Equal("orders", request!.Target);
        Assert.Equal("hook", request.TriggerKind);
        Assert.Equal("legacy_mirror", request.Layer);
        Assert.Equal("update", request.Action);
    }

    [Fact]
    public void BuildLegacyHookRequest_ReturnsExplicitValidationErrors_WhenIdentityMissing()
    {
        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            RuntimeExecutorTests.CreateExecutor(),
            new OperationVectorResolver(),
            CreateTargetDispatchOverride());

        var intake = new LegacyChangeIntakeRequestDto(
            TableName: null,
            RowId: null,
            Operation: null,
            ChangedDataJsonb: null,
            DiffJsonb: null);

        var (request, errors) = dispatcher.BuildLegacyHookRequest(intake);

        Assert.Null(request);
        Assert.Contains(errors, e => e.Code == "LEGACY_TABLE_NAME_REQUIRED");
        Assert.Contains(errors, e => e.Code == "LEGACY_ROW_ID_REQUIRED");
        Assert.Contains(errors, e => e.Code == "LEGACY_OPERATION_REQUIRED");
        Assert.Contains(errors, e => e.Code == "LEGACY_CHANGE_PAYLOAD_REQUIRED");
    }

    [Fact]
    public void BuildLegacyHookRequest_ReturnsExplicitValidationError_WhenOperationUnsupported()
    {
        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            RuntimeExecutorTests.CreateExecutor(),
            new OperationVectorResolver(),
            CreateTargetDispatchOverride());

        var intake = new LegacyChangeIntakeRequestDto(
            TableName: "orders",
            RowId: "42",
            Operation: "upsert",
            ChangedDataJsonb: System.Text.Json.JsonDocument.Parse("{\"status\":\"done\"}").RootElement,
            DiffJsonb: null);

        var (request, errors) = dispatcher.BuildLegacyHookRequest(intake);

        Assert.Null(request);
        Assert.Contains(errors, e => e.Code == "LEGACY_OPERATION_UNSUPPORTED");
    }
}
