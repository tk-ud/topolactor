using Npgsql;
using Topolactor.Repository;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB proof for credential-management's external_api_credential.consumer_reference_binding
/// (docs/design/admin-normal-surface-projection-seed-ssot.yaml surface_axes.admin.surfaces.
/// credentials.categories.external_api_credential.consumer_reference_binding;
/// docs/design/scheduler-job-manifest-ssot.yaml credential_boundary /
/// authoring_surface.admin_surface_split_2026_07_22.credential_and_external_port_binding).
///
/// Proves, against real Postgres:
///   - the real admin_runtime dispatch (layer=credential_management,
///     action=configure_scheduler_job_credential_or_port_binding) mutation_confirmation_contract:
///     dryRun preview is non-mutating -> unconfirmed write fails closed -> confirmed write persists.
///   - mutation_boundary: only scheduler_jobs.credential_requirement_ref / .external_port_ref
///     change; job_key/trigger_kind/schedule_policy_kind/authority_scope are untouched, and the
///     vault/port tables themselves are untouched (never written through this path).
///   - fail-close on an unknown reference_key (no partial write) and on an unknown scheduler_job_id.
///   - diff_log (AdminMasterRosterAudit.AppendAsync -> logs.diff) persists the credential-reference
///     identifiers only -- never plaintext secret material, since none is ever accepted by this
///     payload shape in the first place.
///
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class CredentialManagementSchedulerBindingLiveDbTests
{
    [Fact]
    public async Task ConfigureSchedulerJobCredentialOrPortBinding_DryRunThenConfirmedWrite_OnlyReferenceColumnsChange()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var schedulerJobId = Guid.NewGuid();
        var vaultId = Guid.NewGuid();
        var accessPortId = Guid.NewGuid();
        var credentialRef = $"live-db-cred-binding-vault-{suffix}";
        var portRef = $"live-db-cred-binding-port-{suffix}";

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        async Task ExecAsync(string sql, params (string Name, object Value)[] parms)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parms) cmd.Parameters.AddWithValue(name, value);
            await cmd.ExecuteNonQueryAsync();
        }

        try
        {
            await ExecAsync(
                "INSERT INTO topology.scheduler_jobs (scheduler_job_id, job_key, trigger_kind, schedule_policy_kind, authority_scope, active) " +
                "VALUES (@id, @key, 'cron', 'manual_only', @scope, false)",
                ("id", schedulerJobId), ("key", $"live-db-cred-binding-job-{suffix}"), ("scope", $"live-db-cred-binding-scope-{suffix}"));
            await ExecAsync(
                "INSERT INTO topology.external_credential_vault (credential_vault_id, provider_kind, required_by_bundle, token_kind, reference_key, active) " +
                "VALUES (@id, 'test_provider', 'live-db-cred-binding', 'runtime_connection_ref', @ref, true)",
                ("id", vaultId), ("ref", credentialRef));
            await ExecAsync(
                "INSERT INTO topology.external_access_ports (access_port_id, required_by_bundle, provider_kind, url_or_env_reference, credential_kind, reference_key, active) " +
                "VALUES (@id, 'live-db-cred-binding', 'test_provider', 'env:LIVE_DB_CRED_BINDING_REF', 'external', @ref, true)",
                ("id", accessPortId), ("ref", portRef));

            var t0 = DateTimeOffset.UtcNow.AddSeconds(-1);
            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

            // STEP 1: dryRun preview -- non-mutating.
            var dryRunPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                schedulerJobId = schedulerJobId.ToString(),
                credentialRequirementRef = credentialRef,
                externalPortRef = portRef,
                dryRun = true,
            });
            var dryRunResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "CredentialManagementScenario", "admin", "credential_management",
                "configure_scheduler_job_credential_or_port_binding",
                IdOrHubId: null, Payload: dryRunPayload, Context: null, TriggerKind: "client", Role: "admin"));
            Assert.True(dryRunResponse.Success, string.Join(";", dryRunResponse.Errors.Select(e => e.Code + ":" + e.Message)));

            var (afterDryRunCredRef, afterDryRunPortRef, _, _, _, _) = await ReadSchedulerJobAsync(conn, schedulerJobId);
            Assert.Null(afterDryRunCredRef);
            Assert.Null(afterDryRunPortRef);

            // STEP 2: write without payload.confirmed=true -- fails closed, no mutation.
            var unconfirmedPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                schedulerJobId = schedulerJobId.ToString(),
                credentialRequirementRef = credentialRef,
                externalPortRef = portRef,
            });
            var unconfirmedResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "CredentialManagementScenario", "admin", "credential_management",
                "configure_scheduler_job_credential_or_port_binding",
                IdOrHubId: null, Payload: unconfirmedPayload, Context: null, TriggerKind: "client", Role: "admin"));
            Assert.False(unconfirmedResponse.Success);
            Assert.Contains(unconfirmedResponse.Errors, e => e.Code == "CREDENTIAL_MANAGEMENT_SCHEDULER_BINDING_WRITE_NOT_CONFIRMED");

            var (afterUnconfirmedCredRef, afterUnconfirmedPortRef, _, _, _, _) = await ReadSchedulerJobAsync(conn, schedulerJobId);
            Assert.Null(afterUnconfirmedCredRef);
            Assert.Null(afterUnconfirmedPortRef);

            // STEP 3: confirmed write -- persists exactly the two reference columns.
            var confirmedPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                schedulerJobId = schedulerJobId.ToString(),
                credentialRequirementRef = credentialRef,
                externalPortRef = portRef,
                confirmed = true,
            });
            var confirmedResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "CredentialManagementScenario", "admin", "credential_management",
                "configure_scheduler_job_credential_or_port_binding",
                IdOrHubId: null, Payload: confirmedPayload, Context: null, TriggerKind: "client", Role: "admin"));
            Assert.True(confirmedResponse.Success, string.Join(";", confirmedResponse.Errors.Select(e => e.Code + ":" + e.Message)));

            var (finalCredRef, finalPortRef, jobKey, triggerKind, schedulePolicyKind, authorityScope) =
                await ReadSchedulerJobAsync(conn, schedulerJobId);
            Assert.Equal(credentialRef, finalCredRef);
            Assert.Equal(portRef, finalPortRef);
            // mutation_boundary: every other /admin/contents-authored field is untouched.
            Assert.Equal($"live-db-cred-binding-job-{suffix}", jobKey);
            Assert.Equal("cron", triggerKind);
            Assert.Equal("manual_only", schedulePolicyKind);
            Assert.Equal($"live-db-cred-binding-scope-{suffix}", authorityScope);

            // mutation_boundary: the vault/port tables themselves are never written through this path.
            var vaultRow = await ReadVaultRowAsync(conn, vaultId);
            Assert.Equal(credentialRef, vaultRow.ReferenceKey);
            Assert.Equal("test_provider", vaultRow.ProviderKind);
            var portRow = await ReadAccessPortRowAsync(conn, accessPortId);
            Assert.Equal(portRef, portRow.ReferenceKey);
            Assert.Equal("test_provider", portRow.ProviderKind);

            // diff_log persists reference identifiers only.
            var diffCount = await CountLogsDiffRowsAsync(
                cs, "topology.scheduler_jobs", schedulerJobId.ToString(), "configure_credential_binding", t0);
            Assert.True(diffCount > 0, "expected a logs.diff row for the confirmed credential binding write");
        }
        finally
        {
            await ExecAsync("DELETE FROM logs.diff WHERE record_id = @id", ("id", schedulerJobId.ToString()));
            await ExecAsync("DELETE FROM topology.scheduler_jobs WHERE scheduler_job_id = @id", ("id", schedulerJobId));
            await ExecAsync("DELETE FROM topology.external_access_ports WHERE access_port_id = @id", ("id", accessPortId));
            await ExecAsync("DELETE FROM topology.external_credential_vault WHERE credential_vault_id = @id", ("id", vaultId));
        }
    }

    [Fact]
    public async Task ConfigureSchedulerJobCredentialOrPortBinding_UnknownReferenceKey_FailsClosed_NoPartialWrite()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var schedulerJobId = Guid.NewGuid();

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        async Task ExecAsync(string sql, params (string Name, object Value)[] parms)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parms) cmd.Parameters.AddWithValue(name, value);
            await cmd.ExecuteNonQueryAsync();
        }

        try
        {
            await ExecAsync(
                "INSERT INTO topology.scheduler_jobs (scheduler_job_id, job_key, trigger_kind, schedule_policy_kind, authority_scope, active) " +
                "VALUES (@id, @key, 'cron', 'manual_only', @scope, false)",
                ("id", schedulerJobId), ("key", $"live-db-cred-binding-unknown-job-{suffix}"), ("scope", $"live-db-cred-binding-unknown-scope-{suffix}"));

            var repo = new NpgsqlSchedulerJobManifestRepository(cs);

            var unknownCredResult = await repo.UpdateCredentialBindingAsync(
                schedulerJobId, "does-not-exist-in-vault", null);
            Assert.Equal(SchedulerJobCredentialBindingOutcome.CredentialRequirementRefNotFound, unknownCredResult.Outcome);

            var unknownPortResult = await repo.UpdateCredentialBindingAsync(
                schedulerJobId, null, "does-not-exist-in-any-port-table");
            Assert.Equal(SchedulerJobCredentialBindingOutcome.ExternalPortRefNotFound, unknownPortResult.Outcome);

            var missingJobResult = await repo.UpdateCredentialBindingAsync(
                Guid.NewGuid(), null, null);
            Assert.Equal(SchedulerJobCredentialBindingOutcome.SchedulerJobNotFound, missingJobResult.Outcome);

            // No partial write from either failed attempt.
            var (credRef, portRef, _, _, _, _) = await ReadSchedulerJobAsync(conn, schedulerJobId);
            Assert.Null(credRef);
            Assert.Null(portRef);
        }
        finally
        {
            await ExecAsync("DELETE FROM topology.scheduler_jobs WHERE scheduler_job_id = @id", ("id", schedulerJobId));
        }
    }

    private static async Task<(string? CredentialRequirementRef, string? ExternalPortRef, string JobKey, string TriggerKind, string SchedulePolicyKind, string AuthorityScope)>
        ReadSchedulerJobAsync(NpgsqlConnection conn, Guid schedulerJobId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT credential_requirement_ref, external_port_ref, job_key, trigger_kind, schedule_policy_kind, authority_scope " +
            "FROM topology.scheduler_jobs WHERE scheduler_job_id = @id";
        cmd.Parameters.AddWithValue("id", schedulerJobId);
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), $"expected topology.scheduler_jobs row {schedulerJobId}");
        return (
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5));
    }

    private static async Task<(string ReferenceKey, string ProviderKind)> ReadVaultRowAsync(NpgsqlConnection conn, Guid vaultId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT reference_key, provider_kind FROM topology.external_credential_vault WHERE credential_vault_id = @id";
        cmd.Parameters.AddWithValue("id", vaultId);
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetString(0), reader.GetString(1));
    }

    private static async Task<(string ReferenceKey, string ProviderKind)> ReadAccessPortRowAsync(NpgsqlConnection conn, Guid accessPortId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT reference_key, provider_kind FROM topology.external_access_ports WHERE access_port_id = @id";
        cmd.Parameters.AddWithValue("id", accessPortId);
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetString(0), reader.GetString(1));
    }

    private static async Task<int> CountLogsDiffRowsAsync(
        string cs, string physicalTableName, string recordId, string operationKind, DateTimeOffset since)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*)::int FROM logs.diff
            WHERE source_set_id = 'admin_master_roster'
              AND physical_table_name = @t
              AND record_id = @r
              AND operation_kind = @op
              AND observed_at >= @since";
        cmd.Parameters.AddWithValue("t", physicalTableName);
        cmd.Parameters.AddWithValue("r", recordId);
        cmd.Parameters.AddWithValue("op", operationKind);
        cmd.Parameters.AddWithValue("since", since);
        return (int)(await cmd.ExecuteScalarAsync() ?? 0);
    }

    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();
}
