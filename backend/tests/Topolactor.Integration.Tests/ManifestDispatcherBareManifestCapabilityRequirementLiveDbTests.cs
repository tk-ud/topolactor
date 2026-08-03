using Npgsql;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB proof for round 21's ManifestDispatcher.IsBareManifestNavigationReadTargetRefAsync fix
/// (docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml
/// admin_runtime_target_ref_override_contract.round_21_hardening): dispatching a target_ref that
/// resolves to a BARE navigation-selector manifest (no dispatcher_mapping, no ui_projection) must
/// also enforce the SEPARATE, axes-registered operation-authority manifest's OWN
/// capability_requirement — not skip it just because the bare manifest itself (by construction)
/// never declares one.
///
/// Uses a made-up (layer, action) pair unique to this test (never colliding with real seeded
/// axes registrations, e.g. hub_navigation:get_hub_relations/list_manifests) so this genuinely
/// proves the GENERIC mechanism against a real PostgreSQL manifest table, not a coincidence of
/// existing seed data. ManifestDispatcherTargetRefTests.cs already proves the same contract
/// against a test-double repository; this is the real-repository/real-SQL counterpart round 21's
/// own SSOT entry recorded as still missing.
///
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class ManifestDispatcherBareManifestCapabilityRequirementLiveDbTests
{
    [Fact]
    public async Task DispatchAsync_TargetRef_BareManifest_AxesManifestOwnCapabilityRequirement_RoleMismatch_FailsClosed_RealPostgres()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var layer = $"round22_capability_layer_{suffix}";
        var action = $"round22_capability_action_{suffix}";
        var bareManifestId = Guid.NewGuid();
        var axesManifestId = Guid.NewGuid();

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
            // The BARE target_ref manifest: runtime_mapping only, no dispatcher_mapping, no
            // ui_projection — exactly IsBareManifestNavigationReadTargetRefAsync's own
            // precondition for this whole exemption to even be considered.
            await ExecAsync(
                "INSERT INTO manifest (manifest_id, relation_registry_id, topology, status) VALUES " +
                "(@mid, NULL, ARRAY['{\"type\":\"runtime_mapping\",\"runtime_destination\":\"admin_runtime\"}'::jsonb], 'active')",
                ("mid", bareManifestId));

            // The SEPARATE axes-registered operation-authority manifest: declares
            // identity_selector_read:true (what makes the bare exemption apply at all) AND an
            // explicit capability_requirement stricter than the request's own role — this is the
            // manifest round 21's fix now actually consults.
            await ExecAsync(
                "INSERT INTO manifest (manifest_id, relation_registry_id, topology, status) VALUES " +
                "(@mid, NULL, ARRAY[" +
                "'{\"type\":\"dispatcher_mapping\",\"role\":\"admin\",\"target\":\"admin\",\"layer\":\"" + layer + "\",\"action\":\"" + action + "\",\"identity_selector_read\":true}'::jsonb," +
                "'{\"type\":\"capability_requirement\",\"required_role\":\"super_admin\"}'::jsonb" +
                "], 'active')",
                ("mid", axesManifestId));

            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

            var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{bareManifestId}:some_bare_wiring_key",
            });
            var request = new EndpointRequestDto(
                "round22_capability_test", "manifest", layer, action,
                IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "admin");
            var response = await dispatcher.DispatchAsync(request);

            Assert.False(response.Success);
            Assert.Contains(response.Errors, e => e.Code == "AUTH_CAPABILITY_DENIED");
        }
        finally
        {
            await ExecAsync("DELETE FROM manifest WHERE manifest_id = @mid", ("mid", bareManifestId));
            await ExecAsync("DELETE FROM manifest WHERE manifest_id = @mid", ("mid", axesManifestId));
        }
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_BareManifest_AxesManifestOwnCapabilityRequirement_RoleMatches_NotDeniedOnCapability_RealPostgres()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var layer = $"round22_capability_layer_{suffix}";
        var action = $"round22_capability_action_{suffix}";
        var bareManifestId = Guid.NewGuid();
        var axesManifestId = Guid.NewGuid();

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
                "INSERT INTO manifest (manifest_id, relation_registry_id, topology, status) VALUES " +
                "(@mid, NULL, ARRAY['{\"type\":\"runtime_mapping\",\"runtime_destination\":\"admin_runtime\"}'::jsonb], 'active')",
                ("mid", bareManifestId));

            // Same shape as the negative test, but required_role matches the request's own role —
            // proves the new check is a genuine comparison, not an unconditional fail-close.
            await ExecAsync(
                "INSERT INTO manifest (manifest_id, relation_registry_id, topology, status) VALUES " +
                "(@mid, NULL, ARRAY[" +
                "'{\"type\":\"dispatcher_mapping\",\"role\":\"admin\",\"target\":\"admin\",\"layer\":\"" + layer + "\",\"action\":\"" + action + "\",\"identity_selector_read\":true}'::jsonb," +
                "'{\"type\":\"capability_requirement\",\"required_role\":\"admin\"}'::jsonb" +
                "], 'active')",
                ("mid", axesManifestId));

            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

            var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{bareManifestId}:some_bare_wiring_key",
            });
            var request = new EndpointRequestDto(
                "round22_capability_test", "manifest", layer, action,
                IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "admin");
            var response = await dispatcher.DispatchAsync(request);

            // The made-up (layer, action) has no real AdminRuntime.ExecuteDataAsync case, so the
            // overall response is not expected to succeed — the point under test is specifically
            // that it is NOT rejected for capability reasons.
            Assert.DoesNotContain(response.Errors, e => e.Code == "AUTH_CAPABILITY_DENIED");
        }
        finally
        {
            await ExecAsync("DELETE FROM manifest WHERE manifest_id = @mid", ("mid", bareManifestId));
            await ExecAsync("DELETE FROM manifest WHERE manifest_id = @mid", ("mid", axesManifestId));
        }
    }

    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();
}
