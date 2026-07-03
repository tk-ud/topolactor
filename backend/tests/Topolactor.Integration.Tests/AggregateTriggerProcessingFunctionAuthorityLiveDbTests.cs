using Topolactor.Repository;
using Topolactor.Runtime;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB proof that processing_function_scope.function_id is authorized against the real
/// topology.abstract_function_manifests registry — the same registry execute_abstract_function
/// resolves against — rather than accepted on safe-identifier syntax alone.
///
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set. Requires db/topology_tables.sql
/// (abstract_function_manifests.runtime_lane CHECK extended with 'aggregate_trigger_runtime') and
/// db/runtime_orchestration_tables.sql applied to the target database.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class AggregateTriggerProcessingFunctionAuthorityLiveDbTests
{
    [Fact]
    public async Task ValidateProcessingFunctionAuthorityAsync_FunctionNotRegistered_FailsClose()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var defId = Guid.NewGuid();
        var functionKey = $"live-auth-missing-{defId:N}";
        var definition = BuildDefinition(defId, functionKey, "some-operation");
        var repo = new NpgsqlAbstractFunctionManifestRepository(cs);

        var error = await AggregateTriggerDefinitionValidator.ValidateProcessingFunctionAuthorityAsync(definition, repo);

        Assert.NotNull(error);
        Assert.Equal("AGGREGATE_PROCESSING_FUNCTION_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task ValidateProcessingFunctionAuthorityAsync_FunctionInactive_FailsClose()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var defId = Guid.NewGuid();
        var functionKey = $"live-auth-inactive-{defId:N}";
        var operationDefinitionId = "some-operation";
        var definition = BuildDefinition(defId, functionKey, operationDefinitionId);
        try
        {
            await AggregateTriggerRepositoryLiveDbTests.SeedAbstractFunctionManifestAsync(
                cs, functionKey, "aggregate_trigger_runtime", operationDefinitionId, active: false);

            var error = await AggregateTriggerDefinitionValidator.ValidateProcessingFunctionAuthorityAsync(
                definition, new NpgsqlAbstractFunctionManifestRepository(cs));

            Assert.NotNull(error);
            Assert.Equal("AGGREGATE_PROCESSING_FUNCTION_INACTIVE", error!.Code);
        }
        finally
        {
            await AggregateTriggerRepositoryLiveDbTests.DeleteAbstractFunctionManifestAsync(cs, functionKey);
        }
    }

    [Fact]
    public async Task ValidateProcessingFunctionAuthorityAsync_WrongRuntimeLane_FailsClose()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var defId = Guid.NewGuid();
        var functionKey = $"live-auth-wronglane-{defId:N}";
        var operationDefinitionId = "some-operation";
        var definition = BuildDefinition(defId, functionKey, operationDefinitionId);
        try
        {
            // Registered for a different lane (admin_runtime), not aggregate_trigger_runtime.
            await AggregateTriggerRepositoryLiveDbTests.SeedAbstractFunctionManifestAsync(
                cs, functionKey, "admin_runtime", operationDefinitionId);

            var error = await AggregateTriggerDefinitionValidator.ValidateProcessingFunctionAuthorityAsync(
                definition, new NpgsqlAbstractFunctionManifestRepository(cs));

            Assert.NotNull(error);
            Assert.Equal("AGGREGATE_PROCESSING_FUNCTION_RUNTIME_LANE_INVALID", error!.Code);
        }
        finally
        {
            await AggregateTriggerRepositoryLiveDbTests.DeleteAbstractFunctionManifestAsync(cs, functionKey);
        }
    }

    [Fact]
    public async Task ValidateProcessingFunctionAuthorityAsync_AuthorityScopeMismatch_FailsClose()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var defId = Guid.NewGuid();
        var functionKey = $"live-auth-scopemismatch-{defId:N}";
        var definition = BuildDefinition(defId, functionKey, "expected-operation");
        try
        {
            // authority_scope registered for a different operation than the definition declares.
            await AggregateTriggerRepositoryLiveDbTests.SeedAbstractFunctionManifestAsync(
                cs, functionKey, "aggregate_trigger_runtime", "a-different-operation");

            var error = await AggregateTriggerDefinitionValidator.ValidateProcessingFunctionAuthorityAsync(
                definition, new NpgsqlAbstractFunctionManifestRepository(cs));

            Assert.NotNull(error);
            Assert.Equal("AGGREGATE_PROCESSING_FUNCTION_AUTHORITY_SCOPE_MISMATCH", error!.Code);
        }
        finally
        {
            await AggregateTriggerRepositoryLiveDbTests.DeleteAbstractFunctionManifestAsync(cs, functionKey);
        }
    }

    [Fact]
    public async Task ValidateProcessingFunctionAuthorityAsync_RegisteredActiveCorrectLaneAndScope_Passes()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var defId = Guid.NewGuid();
        var functionKey = $"live-auth-valid-{defId:N}";
        var operationDefinitionId = "matching-operation";
        var definition = BuildDefinition(defId, functionKey, operationDefinitionId);
        try
        {
            await AggregateTriggerRepositoryLiveDbTests.SeedAbstractFunctionManifestAsync(
                cs, functionKey, "aggregate_trigger_runtime", operationDefinitionId);

            var error = await AggregateTriggerDefinitionValidator.ValidateProcessingFunctionAuthorityAsync(
                definition, new NpgsqlAbstractFunctionManifestRepository(cs));

            Assert.Null(error);
        }
        finally
        {
            await AggregateTriggerRepositoryLiveDbTests.DeleteAbstractFunctionManifestAsync(cs, functionKey);
        }
    }

    private static Topolactor.Schema.AggregateTriggerDefinition BuildDefinition(Guid defId, string functionId, string operationDefinitionId)
    {
        var baseline = AggregateTriggerRepositoryLiveDbTests.BuildDefinition(defId);
        return baseline with
        {
            ProcessingFunctionScope = baseline.ProcessingFunctionScope with
            {
                FunctionId = functionId,
                OperationDefinitionId = operationDefinitionId,
            },
        };
    }

    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();
}
