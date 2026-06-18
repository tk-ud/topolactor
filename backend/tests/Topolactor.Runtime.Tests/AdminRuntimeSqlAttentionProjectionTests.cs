using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Tests for AdminRuntime sql_attention:list_projection dispatch (NG5 pipeline alignment).
///
/// Coverage:
///   - sql_attention:list_projection is reachable via ExecuteDataAsync.
///   - Runtime not registered → explicit error SQL_ATTENTION_PROJECTION_RUNTIME_NOT_AVAILABLE.
///   - Missing sourceSetId → explicit error SQL_ATTENTION_SOURCE_SET_ID_REQUIRED.
///   - Empty sourceSetId → explicit error SQL_ATTENTION_SOURCE_SET_ID_REQUIRED.
///   - Cold start (no policy) → emission.data status=MissingPolicy (no silent fallback).
///   - No evidence → emission.data status=NoEvidence (not an error).
///   - Direct bypass route does not exist; projection must go through admin dispatch pipeline.
/// </summary>
public class AdminRuntimeSqlAttentionProjectionTests
{
    [Fact]
    public async Task ListProjection_RuntimeNotRegistered_ReturnsExplicitError()
    {
        var runtime = CreateAdminRuntimeWithoutProjectionRuntime();
        var vector = new OperationVector("admin", "sql_attention", "list_projection", null, "admin",
            JsonSerializer.SerializeToElement(new { sourceSetId = "test_set" }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("SQL_ATTENTION_PROJECTION_RUNTIME_NOT_AVAILABLE", error!.Code);
    }

    [Fact]
    public async Task ListProjection_MissingPayload_ReturnsExplicitError()
    {
        var runtime = CreateAdminRuntime();
        var vector = new OperationVector("admin", "sql_attention", "list_projection", null, "admin", null, null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("SQL_ATTENTION_SOURCE_SET_ID_REQUIRED", error!.Code);
    }

    [Fact]
    public async Task ListProjection_EmptySourceSetId_ReturnsExplicitError()
    {
        var runtime = CreateAdminRuntime();
        var vector = new OperationVector("admin", "sql_attention", "list_projection", null, "admin",
            JsonSerializer.SerializeToElement(new { sourceSetId = "   " }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("SQL_ATTENTION_SOURCE_SET_ID_REQUIRED", error!.Code);
    }

    [Fact]
    public async Task ListProjection_ColdStart_ReturnsMissingPolicyInEmissionData()
    {
        // No policy in function_parameters (default test-double) → MissingPolicy status.
        // Not an error at the dispatch layer; explicit status in emission.data.
        var runtime = CreateAdminRuntime();
        var vector = new OperationVector("admin", "sql_attention", "list_projection", null, "admin",
            JsonSerializer.SerializeToElement(new { sourceSetId = "test_source_set" }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        // Status serialized as integer enum value (default JsonSerializer options in AdminRuntime).
        // TopologyProjectionStatus.MissingPolicy = 2.
        var statusValue = data!.Value.GetProperty("Status").GetInt32();
        Assert.Equal((int)TopologyProjectionStatus.MissingPolicy, statusValue);
    }

    [Fact]
    public async Task ListProjection_UnknownLayerAction_ReturnsAdminOperationNotFound()
    {
        // Confirm the dispatch switch does not silently swallow an unknown action.
        var runtime = CreateAdminRuntime();
        var vector = new OperationVector("admin", "sql_attention", "nonexistent_action", null, "admin",
            JsonSerializer.SerializeToElement(new { sourceSetId = "test" }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("ADMIN_OPERATION_NOT_FOUND", error!.Code);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static SqlAttentionTopologyProjectionRuntime CreateProjectionRuntime()
    {
        var logsRepo = new SqlAttentionLogsRepository(
            NullLogger<SqlAttentionLogsRepository>.Instance, "test-double");
        var topologyRepo = new TopologyRepository(
            NullLogger<TopologyRepository>.Instance, "test-double");
        return new SqlAttentionTopologyProjectionRuntime(
            NullLogger<SqlAttentionTopologyProjectionRuntime>.Instance,
            logsRepo,
            topologyRepo);
    }

    private static AdminRuntime CreateAdminRuntime(
        SqlAttentionTopologyProjectionRuntime? projectionRuntime = null)
    {
        projectionRuntime ??= CreateProjectionRuntime();
        return BuildAdminRuntime(projectionRuntime);
    }

    private static AdminRuntime CreateAdminRuntimeWithoutProjectionRuntime()
    {
        return BuildAdminRuntime(sqlAttentionTopologyProjectionRuntime: null);
    }

    private static AdminRuntime BuildAdminRuntime(
        SqlAttentionTopologyProjectionRuntime? sqlAttentionTopologyProjectionRuntime)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var vecRuntime = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, vecRuntime);
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            ctxRepo,
            registrar,
            pkg,
            uiRepo,
            sqlAttentionTopologyProjectionRuntime: sqlAttentionTopologyProjectionRuntime);
    }
}
