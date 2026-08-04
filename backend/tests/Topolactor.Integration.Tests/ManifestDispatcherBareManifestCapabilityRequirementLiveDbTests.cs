using Npgsql;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB proof for the bare-manifest-navigation-read exemption
/// (ManifestDispatcher.IsBareManifestNavigationReadTargetRefAsync;
/// docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml
/// admin_runtime_target_ref_override_contract.round_21_hardening): dispatching a target_ref that
/// resolves to a BARE navigation-selector manifest (no dispatcher_mapping, no ui_projection) must
/// enforce every axis the SEPARATE, axes-registered operation-authority manifest declares --
/// active status, identity_selector_read classification, dispatcher_mapping.role, and
/// capability_requirement -- via the real production repository (NpgsqlManifestRepository) and
/// ManifestDispatcher, not a test-double stand-in. ManifestDispatcherTargetRefTests.cs already
/// proves each of these against a test-double repository; this file is the real-repository/
/// real-SQL counterpart (round 22/23) for the axes needing an actually-persisted manifest row to
/// exercise meaningfully (capability_requirement, active-status filtering, axis ambiguity).
///
/// Uses a made-up (layer, action) pair unique to each test (never colliding with real seeded axes
/// registrations, e.g. hub_navigation:get_hub_relations/list_manifests) so every test genuinely
/// proves the GENERIC mechanism against real PostgreSQL, not a coincidence of existing seed data.
/// The "valid path reaches a real dispatch outcome" positive proof for the REAL, existing
/// hub_navigation:get_hub_relations bare-manifest path is already covered end-to-end (bare source
/// manifest -> target_ref -> real hub_navigation:get_hub_relations dispatch ->
/// Emission.NavigationSequence reflecting an actually-authored relation) by
/// CredentialManagementHubRelationUiProjectionLiveDbTests and
/// AdminEnumHubRelationUiProjectionLiveDbTests's own HubNavigationCreate tests -- not duplicated
/// here.
///
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set. Each test uses a unique
/// Guid-suffixed (layer, action) pair and cleans up its own inserted manifest rows in a finally
/// block -- no shared/persistent seed state is mutated.
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

    [Fact]
    public async Task DispatchAsync_TargetRef_BareManifest_AxesManifest_Inactive_FailsClosed_NotFoundAsIdentitySelectorRead_RealPostgres()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var layer = $"round23_inactive_layer_{suffix}";
        var action = $"round23_inactive_action_{suffix}";
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

            // The axes manifest declares identity_selector_read:true, but its status is NOT
            // 'active' -- NpgsqlManifestRepository.ResolveActiveManifestAsync's own
            // "WHERE status = 'active'" filter means this manifest is never even a candidate, so
            // IsBareManifestNavigationReadTargetRefAsync's axesManifest resolution returns null,
            // exactly as if no axes registration existed at all (not a capability question -- an
            // eligibility one).
            await ExecAsync(
                "INSERT INTO manifest (manifest_id, relation_registry_id, topology, status) VALUES " +
                "(@mid, NULL, ARRAY[" +
                "'{\"type\":\"dispatcher_mapping\",\"role\":\"admin\",\"target\":\"admin\",\"layer\":\"" + layer + "\",\"action\":\"" + action + "\",\"identity_selector_read\":true}'::jsonb" +
                "], 'draft')",
                ("mid", axesManifestId));

            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

            var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{bareManifestId}:some_bare_wiring_key",
            });
            var request = new EndpointRequestDto(
                "round23_test", "manifest", layer, action,
                IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "admin");
            var response = await dispatcher.DispatchAsync(request);

            Assert.False(response.Success);
            Assert.Contains(response.Errors, e => e.Code == "TARGET_REF_ADMIN_RUNTIME_LAYER_ACTION_MISSING");
        }
        finally
        {
            await ExecAsync("DELETE FROM manifest WHERE manifest_id = @mid", ("mid", bareManifestId));
            await ExecAsync("DELETE FROM manifest WHERE manifest_id = @mid", ("mid", axesManifestId));
        }
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_BareManifest_AxesManifest_MissingIdentitySelectorReadDeclaration_FailsClosed_RealPostgres()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var layer = $"round23_noflag_layer_{suffix}";
        var action = $"round23_noflag_action_{suffix}";
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

            // Active, axes-registered, role matches -- but identity_selector_read is simply
            // absent (mirrors real hub_navigation:create/update/deprecate/reorder, which are
            // axes-registered under target=admin too but deliberately declare no such field).
            await ExecAsync(
                "INSERT INTO manifest (manifest_id, relation_registry_id, topology, status) VALUES " +
                "(@mid, NULL, ARRAY[" +
                "'{\"type\":\"dispatcher_mapping\",\"role\":\"admin\",\"target\":\"admin\",\"layer\":\"" + layer + "\",\"action\":\"" + action + "\"}'::jsonb" +
                "], 'active')",
                ("mid", axesManifestId));

            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

            var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{bareManifestId}:some_bare_wiring_key",
            });
            var request = new EndpointRequestDto(
                "round23_test", "manifest", layer, action,
                IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "admin");
            var response = await dispatcher.DispatchAsync(request);

            Assert.False(response.Success);
            Assert.Contains(response.Errors, e => e.Code == "TARGET_REF_ADMIN_RUNTIME_LAYER_ACTION_MISSING");
        }
        finally
        {
            await ExecAsync("DELETE FROM manifest WHERE manifest_id = @mid", ("mid", bareManifestId));
            await ExecAsync("DELETE FROM manifest WHERE manifest_id = @mid", ("mid", axesManifestId));
        }
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_BareManifest_AxesManifest_DispatcherMappingRoleMismatch_FailsClosed_RealPostgres()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var layer = $"round23_rolemismatch_layer_{suffix}";
        var action = $"round23_rolemismatch_action_{suffix}";
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

            // The axes manifest is registered for role="user", but the request presents
            // role="admin" -- ResolveActiveManifestAsync's own MatchesAxes role comparison means
            // this axes manifest is simply never found for an admin request, exactly as if no
            // axes registration for this (layer, action) existed for that role at all.
            await ExecAsync(
                "INSERT INTO manifest (manifest_id, relation_registry_id, topology, status) VALUES " +
                "(@mid, NULL, ARRAY[" +
                "'{\"type\":\"dispatcher_mapping\",\"role\":\"user\",\"target\":\"admin\",\"layer\":\"" + layer + "\",\"action\":\"" + action + "\",\"identity_selector_read\":true}'::jsonb" +
                "], 'active')",
                ("mid", axesManifestId));

            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

            var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{bareManifestId}:some_bare_wiring_key",
            });
            var request = new EndpointRequestDto(
                "round23_test", "manifest", layer, action,
                IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "admin");
            var response = await dispatcher.DispatchAsync(request);

            Assert.False(response.Success);
            Assert.Contains(response.Errors, e => e.Code == "TARGET_REF_ADMIN_RUNTIME_LAYER_ACTION_MISSING");
        }
        finally
        {
            await ExecAsync("DELETE FROM manifest WHERE manifest_id = @mid", ("mid", bareManifestId));
            await ExecAsync("DELETE FROM manifest WHERE manifest_id = @mid", ("mid", axesManifestId));
        }
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_BareManifest_AxesAmbiguity_TwoActiveManifestsSameAxes_FailsClosed_RealPostgres()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var layer = $"round23_ambiguous_layer_{suffix}";
        var action = $"round23_ambiguous_action_{suffix}";
        var bareManifestId = Guid.NewGuid();
        var axesManifestId1 = Guid.NewGuid();
        var axesManifestId2 = Guid.NewGuid();

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

            // TWO active manifests both axes-registered for the exact same (role, target=admin,
            // layer, action) -- NpgsqlManifestRepository.ResolveActiveManifestAsync throws
            // MANIFEST_AMBIGUOUS (InvalidOperationException) when this happens; ManifestDispatcher.
            // DispatchAsync's own top-level catch converts it into a structured MANIFEST_AMBIGUOUS
            // response even though it originated from deep inside
            // IsBareManifestNavigationReadTargetRefAsync's internal axes lookup, not the main
            // resolution path.
            await ExecAsync(
                "INSERT INTO manifest (manifest_id, relation_registry_id, topology, status) VALUES " +
                "(@mid, NULL, ARRAY[" +
                "'{\"type\":\"dispatcher_mapping\",\"role\":\"admin\",\"target\":\"admin\",\"layer\":\"" + layer + "\",\"action\":\"" + action + "\",\"identity_selector_read\":true}'::jsonb" +
                "], 'active')",
                ("mid", axesManifestId1));
            await ExecAsync(
                "INSERT INTO manifest (manifest_id, relation_registry_id, topology, status) VALUES " +
                "(@mid, NULL, ARRAY[" +
                "'{\"type\":\"dispatcher_mapping\",\"role\":\"admin\",\"target\":\"admin\",\"layer\":\"" + layer + "\",\"action\":\"" + action + "\",\"identity_selector_read\":true}'::jsonb" +
                "], 'active')",
                ("mid", axesManifestId2));

            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

            var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{bareManifestId}:some_bare_wiring_key",
            });
            var request = new EndpointRequestDto(
                "round23_test", "manifest", layer, action,
                IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "admin");
            var response = await dispatcher.DispatchAsync(request);

            Assert.False(response.Success);
            Assert.Contains(response.Errors, e => e.Code == "MANIFEST_AMBIGUOUS");
        }
        finally
        {
            await ExecAsync("DELETE FROM manifest WHERE manifest_id = @mid", ("mid", bareManifestId));
            await ExecAsync("DELETE FROM manifest WHERE manifest_id = @mid", ("mid", axesManifestId1));
            await ExecAsync("DELETE FROM manifest WHERE manifest_id = @mid", ("mid", axesManifestId2));
        }
    }

    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();
}
