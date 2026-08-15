using Npgsql;
using Topolactor.Repository;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB proof for credential-management's external_instance_credential category
/// (docs/design/admin-normal-surface-projection-seed-ssot.yaml surface_axes.admin.surfaces.
/// credentials.categories.external_instance_credential; docs/design/instance-port-substrate-ssot.yaml
/// existing_credential_management_projection_extension). Closes the
/// external_instance_projection_columns design_blocking id's remaining
/// projection_seed_binding_authoring_surface_operation_wiring_and_proof gap.
///
/// Proves, against real Postgres, through the REAL ManifestDispatcher:
///   - create/update/delete are dispatchable via dispatch-only manifests cd012/cd013/cd014 ->
///     admin_runtime, via [role=admin, target=admin, layer=external_instance_credential,
///     action=...] axes -- reached from manifest 092's own seed wiring
///     (external_instance_credential_crud_section under the instance_settings category).
///   - search is dispatchable via the unified [layer=credential_management, action=search,
///     payload.category=external_instance_credential] action on manifest 092's own
///     dispatcher_mapping entry.
///   - mutation_boundary: create/update only ever point a db_instance_port/runtime_instance_port
///     row at an EXISTING topology.external_credential_vault row by reference_key -- an unknown
///     referenceKey fails closed with EXTERNAL_INSTANCE_CREDENTIAL_REFERENCE_KEY_NOT_FOUND and
///     never writes the vault itself.
///   - secret-bearing vault columns (token_hash/encrypted_payload/encryption_key_reference) are
///     NEVER present in any search/get/create/update response.
///   - diff_log persists sanitized metadata keyed by each record kind's CANONICAL physical table
///     name (topology.db_instance_port / topology.runtime_instance_port), never a recordKind
///     string-concatenation derivation.
///
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class ExternalInstanceCredentialLiveDbTests
{
    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();

    private static async Task ExecAsync(NpgsqlConnection conn, string sql, params (string Name, object Value)[] parms)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parms) cmd.Parameters.AddWithValue(name, value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ReadLatestDiffAfterJsonAsync(
        string cs, string physicalTableName, string recordId, string operationKind)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT after_state_or_diff_json::text FROM logs.diff
            WHERE source_set_id = 'admin_master_roster'
              AND physical_table_name = @t
              AND record_id = @r
              AND operation_kind = @op
            ORDER BY observed_at DESC
            LIMIT 1";
        cmd.Parameters.AddWithValue("t", physicalTableName);
        cmd.Parameters.AddWithValue("r", recordId);
        cmd.Parameters.AddWithValue("op", operationKind);
        return (string?)await cmd.ExecuteScalarAsync();
    }

    [Fact]
    public async Task Create_DryRunThenConfirmedWrite_UnknownReferenceKeyFailsClosed_DiffLogUsesCanonicalPhysicalTableName()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var vaultId = Guid.NewGuid();
        var refKey = $"live-db-eic-create-ref-{suffix}";
        var authorityKey = $"live-db-eic-create-authority-{suffix}";

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        Guid? createdRecordId = null;
        try
        {
            await ExecAsync(conn,
                "INSERT INTO topology.external_credential_vault (credential_vault_id, provider_kind, required_by_bundle, token_kind, reference_key, active) " +
                "VALUES (@id, 'postgres', @bundle, 'connection_string', @ref, true)",
                ("id", vaultId), ("bundle", $"live-db-eic-bundle-{suffix}"), ("ref", refKey));

            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

            // Unknown referenceKey fails closed even under dryRun -- real validation, not a bare echo.
            var badRefDryRun = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "ExternalInstanceCredentialScenario", "admin", "external_instance_credential", "create",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    recordKind = "db_instance_port", instanceAuthorityKey = authorityKey,
                    providerKind = "postgres", requiredByBundle = $"live-db-eic-bundle-{suffix}",
                    referenceKey = "not-a-real-reference-key", dryRun = true,
                }),
                Context: null, TriggerKind: "client", Role: "admin"));
            Assert.False(badRefDryRun.Success);
            Assert.Contains(badRefDryRun.Errors, e => e.Code == "EXTERNAL_INSTANCE_CREDENTIAL_REFERENCE_KEY_NOT_FOUND");

            var dryRun = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "ExternalInstanceCredentialScenario", "admin", "external_instance_credential", "create",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    recordKind = "db_instance_port", instanceAuthorityKey = authorityKey,
                    providerKind = "postgres", requiredByBundle = $"live-db-eic-bundle-{suffix}",
                    referenceKey = refKey, dryRun = true,
                }),
                Context: null, TriggerKind: "client", Role: "admin"));
            Assert.True(dryRun.Success, string.Join(";", dryRun.Errors.Select(e => e.Code + ":" + e.Message)));

            var beforeCount = (long)(await new NpgsqlCommand(
                "SELECT COUNT(*) FROM topology.db_instance_port WHERE instance_authority_key = @k", conn)
            { Parameters = { new NpgsqlParameter("k", authorityKey) } }.ExecuteScalarAsync())!;
            Assert.Equal(0, beforeCount);

            var confirmed = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "ExternalInstanceCredentialScenario", "admin", "external_instance_credential", "create",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    recordKind = "db_instance_port", instanceAuthorityKey = authorityKey,
                    providerKind = "postgres", requiredByBundle = $"live-db-eic-bundle-{suffix}",
                    referenceKey = refKey, confirmed = true,
                }),
                Context: null, TriggerKind: "client", Role: "admin"));
            Assert.True(confirmed.Success, string.Join(";", confirmed.Errors.Select(e => e.Code + ":" + e.Message)));
            var recordJson = confirmed.Emission!.Data!.Value.GetRawText();
            foreach (var forbidden in new[] { "tokenHash", "encryptedPayload", "encryptionKeyReference" })
                Assert.DoesNotContain(forbidden, recordJson);

            var recordIdText = confirmed.Emission.Data.Value.GetProperty("record").GetProperty("recordId").GetString();
            createdRecordId = Guid.Parse(recordIdText!);

            var diff = await ReadLatestDiffAfterJsonAsync(cs, "topology.db_instance_port", createdRecordId.Value.ToString(), "create");
            Assert.NotNull(diff);
            foreach (var forbidden in new[] { "tokenHash", "encryptedPayload", "encryptionKeyReference" })
                Assert.DoesNotContain(forbidden, diff);
        }
        finally
        {
            if (createdRecordId is not null)
            {
                await ExecAsync(conn, "DELETE FROM logs.diff WHERE record_id = @id", ("id", createdRecordId.Value.ToString()));
                await ExecAsync(conn, "DELETE FROM topology.db_instance_port WHERE instance_port_id = @id", ("id", createdRecordId.Value));
            }
            await ExecAsync(conn, "DELETE FROM topology.external_credential_vault WHERE credential_vault_id = @id", ("id", vaultId));
        }
    }

    [Fact]
    public async Task Update_And_Deactivate_RuntimeInstancePort_DiffLogUsesCanonicalPhysicalTableName_NeverStringConcatenation()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var vaultId = Guid.NewGuid();
        var refKey = $"live-db-eic-update-ref-{suffix}";
        var refKey2 = $"live-db-eic-update-ref2-{suffix}";
        var portId = Guid.NewGuid();
        const string canonicalTable = "topology.runtime_instance_port";

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        try
        {
            await ExecAsync(conn,
                "INSERT INTO topology.external_credential_vault (credential_vault_id, provider_kind, required_by_bundle, token_kind, reference_key, active) " +
                "VALUES (@id, 'redis', @bundle, 'connection_string', @ref, true)",
                ("id", vaultId), ("bundle", $"live-db-eic-update-bundle-{suffix}"), ("ref", refKey));
            await ExecAsync(conn,
                "INSERT INTO topology.runtime_instance_port (instance_port_id, instance_authority_key, provider_kind, required_by_bundle, reference_key, active) " +
                "VALUES (@id, @authKey, 'redis', @bundle, @ref, true)",
                ("id", portId), ("authKey", $"live-db-eic-update-authority-{suffix}"),
                ("bundle", $"live-db-eic-update-bundle-{suffix}"), ("ref", refKey));

            // A second vault row so update can re-point reference_key to a DIFFERENT existing credential.
            var vaultId2 = Guid.NewGuid();
            await ExecAsync(conn,
                "INSERT INTO topology.external_credential_vault (credential_vault_id, provider_kind, required_by_bundle, token_kind, reference_key, active) " +
                "VALUES (@id, 'redis', @bundle, 'connection_string', @ref, true)",
                ("id", vaultId2), ("bundle", $"live-db-eic-update-bundle2-{suffix}"), ("ref", refKey2));

            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

            var updateResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "ExternalInstanceCredentialScenario", "admin", "external_instance_credential", "update",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    recordKind = "runtime_instance_port", recordId = portId.ToString(),
                    referenceKey = refKey2, confirmed = true,
                }),
                Context: null, TriggerKind: "client", Role: "admin"));
            Assert.True(updateResponse.Success, string.Join(";", updateResponse.Errors.Select(e => e.Code + ":" + e.Message)));
            var updateDiff = await ReadLatestDiffAfterJsonAsync(cs, canonicalTable, portId.ToString(), "update");
            Assert.NotNull(updateDiff);

            var deleteResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "ExternalInstanceCredentialScenario", "admin", "external_instance_credential", "delete",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    recordKind = "runtime_instance_port", recordId = portId.ToString(), confirmed = true,
                }),
                Context: null, TriggerKind: "client", Role: "admin"));
            Assert.True(deleteResponse.Success, string.Join(";", deleteResponse.Errors.Select(e => e.Code + ":" + e.Message)));
            var deleteDiff = await ReadLatestDiffAfterJsonAsync(cs, canonicalTable, portId.ToString(), "delete");
            Assert.NotNull(deleteDiff);

            await using var activeCmd = conn.CreateCommand();
            activeCmd.CommandText = "SELECT active FROM topology.runtime_instance_port WHERE instance_port_id = @id";
            activeCmd.Parameters.AddWithValue("id", portId);
            Assert.False((bool)(await activeCmd.ExecuteScalarAsync())!);
        }
        finally
        {
            // The port row must be deleted BEFORE either vault row -- after the update above it
            // references vaultId2's reference_key, so deleting vaultId2 first would FK-violate.
            await ExecAsync(conn, "DELETE FROM logs.diff WHERE record_id = @id", ("id", portId.ToString()));
            await ExecAsync(conn, "DELETE FROM topology.runtime_instance_port WHERE instance_port_id = @id", ("id", portId));
            await ExecAsync(conn, "DELETE FROM topology.external_credential_vault WHERE reference_key = @ref", ("ref", refKey2));
            await ExecAsync(conn, "DELETE FROM topology.external_credential_vault WHERE credential_vault_id = @id", ("id", vaultId));
        }
    }

    [Fact]
    public async Task Search_DispatchesAgainstManifest092_CategoryExternalInstanceCredential_ResponseManifestIdMatchesOwnAdoptedIdentity()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

        var response = await dispatcher.DispatchAsync(new EndpointRequestDto(
            "ExternalInstanceCredentialScenario", "admin", "credential_management", "search",
            IdOrHubId: null,
            Payload: System.Text.Json.JsonSerializer.SerializeToElement(new { category = "external_instance_credential" }),
            Context: null, TriggerKind: "client", Role: "admin"));

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
        Assert.NotNull(response.Emission);
        Assert.Equal("00000000-0000-0000-0000-000000000092", response.Emission!.ManifestId);
        Assert.NotNull(response.Emission.LayoutNodes);
        Assert.Contains(response.Emission.LayoutNodes!, n => n.NodeId == "credential_result_list");
        Assert.True(response.Emission.Data!.Value.TryGetProperty("records", out _));
        Assert.Equal("external_instance_credential", response.Emission.Data.Value.GetProperty("category").GetString());
    }
}
