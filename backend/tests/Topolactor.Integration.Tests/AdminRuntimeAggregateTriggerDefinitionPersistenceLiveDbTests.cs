using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB proof that AdminRuntime.assign_screen_data_shape persists a validator-accepted
/// aggregate trigger definition to the canonical runtime_orchestration.aggregate_trigger_definitions
/// table (db-schema.yaml aggregate_trigger_table_contracts), in addition to the screen_data_shape
/// authoring-draft projection already covered by AdminRuntimeManifestManagementTests
/// (in-memory / non-DB unit test).
///
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set. Requires
/// db/runtime_orchestration_tables.sql applied to the target database.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class AdminRuntimeAggregateTriggerDefinitionPersistenceLiveDbTests
{
    [Fact]
    public async Task AssignScreenDataShape_PersistsAggregateTriggerDefinition_ToCanonicalTable()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var manifestId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var functionKey = $"aggregate-trigger-authoring-function-{definitionId:N}";
        var manifestRepo = new FakeAdminScreenDataShapeManifestRepository();
        manifestRepo.Seed(new ManifestDetailRecord(
            manifestId, null, DraftTopologyWithLogicalTables("orders", "id"), "draft",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var aggregateTriggerRepo = new NpgsqlAggregateTriggerRepository(cs);
        var abstractFunctionRepo = new NpgsqlAbstractFunctionManifestRepository(cs);
        var runtime = CreateAdminRuntime(manifestRepo, aggregateTriggerRepo, abstractFunctionRepo);

        try
        {
            // processing_function_scope.function_id authority: registered specifically for
            // aggregate_trigger_runtime and scoped (authority_scope) to this operation's
            // topologySystemName ("orders") — the backend-derived operation_definition_id.
            await AggregateTriggerRepositoryLiveDbTests.SeedAbstractFunctionManifestAsync(
                cs, functionKey, "aggregate_trigger_runtime", "orders");

            var payload = JsonSerializer.SerializeToElement(new
            {
                manifestId = manifestId.ToString(),
                topologySystemName = "orders",
                logicalTables = new[]
                {
                    new { tableName = "orders", columns = new[] { new { name = "id", dataType = "text", nullable = false } } },
                },
                aggregateTriggerDefinitions = new[] { ValidAggregateTriggerDefinition(definitionId, functionKey, "orders", "orders") },
            });

            var (data, error) = await runtime.ExecuteDataAsync(
                new OperationVector("admin", "manifest", "assign_screen_data_shape", null, "admin", payload, null),
                default);

            Assert.Null(error);
            Assert.True(data.HasValue);

            var loaded = await aggregateTriggerRepo.LoadDefinitionAsync(definitionId);
            Assert.NotNull(loaded);
            Assert.Equal("orders", loaded!.AggregateTargetBinding.TargetId);
            Assert.Equal("orders", loaded.MaterializationTargetBinding.TargetId);
            // processing_function_scope required fields round-trip through the canonical table,
            // not restored empty (accepted_event_schema_ref / allowed_source_kinds / materialization_policy_ref
            // are dedicated columns, per docs/design/db-schema.yaml aggregate_trigger_definitions.required_columns).
            Assert.Equal(functionKey, loaded.ProcessingFunctionScope.FunctionId);
            // operation_definition_id is backend authority, derived from topologySystemName, and
            // overrides whatever the client submitted (the request below submits a different value).
            Assert.Equal("orders", loaded.ProcessingFunctionScope.OperationDefinitionId);
            Assert.Equal("contents.step3.aggregate_trigger.event.v1", loaded.ProcessingFunctionScope.AcceptedEventSchemaRef);
            Assert.Equal(["function_input_event"], loaded.ProcessingFunctionScope.AllowedSourceKinds);
            Assert.Equal("backend_runtime_authority_required", loaded.ProcessingFunctionScope.MaterializationPolicyRef);

            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM runtime_orchestration.aggregate_trigger_definitions WHERE trigger_definition_id = @id AND active";
            cmd.Parameters.AddWithValue("id", definitionId);
            Assert.Equal(1L, (long)(await cmd.ExecuteScalarAsync())!);
        }
        finally
        {
            await AggregateTriggerRepositoryLiveDbTests.CleanupAsync(cs, definitionId);
            await AggregateTriggerRepositoryLiveDbTests.DeleteAbstractFunctionManifestAsync(cs, functionKey);
        }
    }

    [Fact]
    public async Task AssignScreenDataShape_UnregisteredFunctionId_FailsCloseAndPersistsNothing()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var manifestId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var unregisteredFunctionKey = $"unregistered-function-{definitionId:N}";
        var manifestRepo = new FakeAdminScreenDataShapeManifestRepository();
        manifestRepo.Seed(new ManifestDetailRecord(
            manifestId, null, DraftTopologyWithLogicalTables("orders", "id"), "draft",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var aggregateTriggerRepo = new NpgsqlAggregateTriggerRepository(cs);
        var abstractFunctionRepo = new NpgsqlAbstractFunctionManifestRepository(cs);
        var runtime = CreateAdminRuntime(manifestRepo, aggregateTriggerRepo, abstractFunctionRepo);

        try
        {
            var payload = JsonSerializer.SerializeToElement(new
            {
                manifestId = manifestId.ToString(),
                topologySystemName = "orders",
                logicalTables = new[]
                {
                    new { tableName = "orders", columns = new[] { new { name = "id", dataType = "text", nullable = false } } },
                },
                aggregateTriggerDefinitions = new[] { ValidAggregateTriggerDefinition(definitionId, unregisteredFunctionKey, "orders", "orders") },
            });

            var (data, error) = await runtime.ExecuteDataAsync(
                new OperationVector("admin", "manifest", "assign_screen_data_shape", null, "admin", payload, null),
                default);

            Assert.Null(data);
            Assert.NotNull(error);
            Assert.Equal("AGGREGATE_PROCESSING_FUNCTION_NOT_FOUND", error!.Code);

            var loaded = await aggregateTriggerRepo.LoadDefinitionAsync(definitionId);
            Assert.Null(loaded);
        }
        finally
        {
            await AggregateTriggerRepositoryLiveDbTests.CleanupAsync(cs, definitionId);
        }
    }

    private static AdminRuntime CreateAdminRuntime(
        ManifestRepository manifestRepo,
        AggregateTriggerRepository aggregateTriggerRepo,
        IAbstractFunctionManifestRepository? abstractFunctionRepo = null)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            ctxRepo,
            registrar,
            pkg,
            uiRepo,
            manifestRepository: manifestRepo,
            topologyRepository: topoRepo,
            aggregateTriggerRepository: aggregateTriggerRepo,
            abstractFunctionManifestRepository: abstractFunctionRepo);
    }

    private static IReadOnlyList<JsonElement> DraftTopologyWithLogicalTables(string tableName, string columnName)
    {
        var list = ManifestTopologyValidator.BuildTopology("admin", "tgt", "screen_list", "Read", "topology_transform_runtime", null).ToList();
        list.Add(JsonSerializer.SerializeToElement(new
        {
            type = "screen_data_shape",
            logicalTables = new[] { new { tableName, columns = new[] { new { name = columnName, dataType = "text", nullable = true } } } },
        }));
        return list;
    }

    private static object ValidAggregateTriggerDefinition(Guid definitionId, string functionId, string aggregateTargetId, string materializationTargetId) => new
    {
        trigger_definition_id = definitionId,
        trigger_source = new { canonical_trigger_kind = "client", trigger_source_detail_kind = "client_operation_event" },
        processing_function_scope = new
        {
            function_id = functionId,
            // Intentionally different from topologySystemName ("orders") to prove the backend
            // overrides operation_definition_id rather than trusting this client-submitted value.
            operation_definition_id = "contents_step3_operation",
            accepted_event_schema_ref = "contents.step3.aggregate_trigger.event.v1",
            allowed_source_kinds = new[] { "function_input_event" },
            materialization_policy_ref = "backend_runtime_authority_required",
        },
        execution_scope = "single_event",
        transaction_boundary = "event_append_aggregate_upsert_and_materialization",
        aggregate_target_binding = new { target_source = "step2_logical_entity_definition", target_id = aggregateTargetId },
        conflict_key_fields = new[] { "operation_definition_id" },
        delta_map = new Dictionary<string, decimal> { ["event_count"] = 1m },
        threshold_policy = new
        {
            minimum_trial_count = 1,
            ratio_numerator_field = "event_count",
            ratio_denominator_field = "event_count",
            comparison_operator = ">=",
            target_ratio = 1m,
        },
        materialization_target_binding = new { target_source = "step2_logical_entity_definition", target_id = materializationTargetId },
        materialization_payload_map = new[]
        {
            new { target_field = "operation_definition_id", source = "function_input_event", source_field = "operation_definition_id" },
        },
        approval_policy = "auto_materialize_when_threshold_passes",
        evidence_policy = "append_evidence",
    };

    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();
}
