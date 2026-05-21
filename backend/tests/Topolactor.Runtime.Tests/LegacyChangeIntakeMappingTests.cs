using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class LegacyChangeIntakeMappingTests
{
    [Fact]
    public void BuildLegacyHookRequest_MapsTableNameAsTargetAndHookTrigger()
    {
        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            RuntimeExecutorTests.CreateExecutor());

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
            RuntimeExecutorTests.CreateExecutor());

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
}
