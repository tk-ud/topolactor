using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class ExistingSystemChangeIntakeMappingTests
{
    private const string TestRole = "existing_system_operator";

    [Fact]
    public void BuildExistingSystemHookRequest_MapsTableNameAsTargetAndHookTrigger()
    {
        var topologyRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);

        var intake = new ExistingSystemChangeIntakeRequestDto(
            TableName: "orders",
            RowId: "42",
            Operation: "update",
            ChangedDataJsonb: System.Text.Json.JsonDocument.Parse("{\"status\":\"done\"}").RootElement,
            DiffJsonb: null,
            Actor: "system",
            Source: "existing_system_api",
            OccurredAt: null);

        var (request, errors) = dispatcher.BuildExistingSystemHookRequest(intake, TestRole);

        Assert.Empty(errors);
        Assert.NotNull(request);
        Assert.Equal("orders", request!.Target);
        Assert.Equal("hook", request.TriggerKind);
        Assert.Equal("existing_system_intake", request.Layer);
        Assert.Equal("update", request.Action);
        Assert.Equal(TestRole, request.Role);
        Assert.Equal("ExistingSystemChangeIntake", request.OperationType);
    }

    [Fact]
    public void BuildExistingSystemHookRequest_ReturnsExplicitValidationErrors_WhenIdentityMissing()
    {
        var topologyRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);

        var intake = new ExistingSystemChangeIntakeRequestDto(
            TableName: null,
            RowId: null,
            Operation: null,
            ChangedDataJsonb: null,
            DiffJsonb: null);

        var (request, errors) = dispatcher.BuildExistingSystemHookRequest(intake, TestRole);

        Assert.Null(request);
        Assert.Contains(errors, e => e.Code == "LEGACY_TABLE_NAME_REQUIRED");
        Assert.Contains(errors, e => e.Code == "LEGACY_ROW_ID_REQUIRED");
        Assert.Contains(errors, e => e.Code == "LEGACY_OPERATION_REQUIRED");
        Assert.Contains(errors, e => e.Code == "LEGACY_CHANGE_PAYLOAD_REQUIRED");
    }

    [Fact]
    public void BuildExistingSystemHookRequest_ReturnsExplicitValidationError_WhenOperationUnsupported()
    {
        var topologyRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);

        var intake = new ExistingSystemChangeIntakeRequestDto(
            TableName: "orders",
            RowId: "42",
            Operation: "upsert",
            ChangedDataJsonb: System.Text.Json.JsonDocument.Parse("{\"status\":\"done\"}").RootElement,
            DiffJsonb: null);

        var (request, errors) = dispatcher.BuildExistingSystemHookRequest(intake, TestRole);

        Assert.Null(request);
        Assert.Contains(errors, e => e.Code == "LEGACY_OPERATION_UNSUPPORTED");
    }
}
