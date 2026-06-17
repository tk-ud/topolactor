using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live PostgreSQL proof for the production external port policy read substrate.
/// TOPOLACTOR_TEST_DB_CONNECTION unset means explicit local skip. Set
/// TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1 to require a live database in CI.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class ExternalPortPolicyRepositoryLiveDbTests
{
    [Fact]
    public async Task LoadPortRecordAsync_MapsAccessResponseHookAndIgnoresInactiveRowsAndCredentialVaultPayload()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var bundle = $"external-port-live-{suffix}";
        var routeKey = $"route-{suffix}";
        var accessId = Guid.NewGuid();
        var inactiveAccessId = Guid.NewGuid();
        var responseId = Guid.NewGuid();
        var hookId = Guid.NewGuid();
        var vaultId = Guid.NewGuid();
        var repo = new NpgsqlExternalPortPolicyRepository(NullLogger<NpgsqlExternalPortPolicyRepository>.Instance, cs);

        try
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await ExecuteAsync(conn, """
                INSERT INTO topology.external_access_ports
                    (access_port_id, required_by_bundle, provider_kind, url_or_env_reference, credential_kind, reference_key, active)
                VALUES
                    (@accessId, @bundle, 'generic-access-live', 'env:ACCESS_URL', 'external', 'vault-ref-access', true),
                    (@inactiveAccessId, @bundle, 'inactive-provider', 'env:INACTIVE_URL', 'external', 'inactive-vault-ref', false);

                INSERT INTO topology.external_response_ports
                    (response_port_id, required_by_bundle, provider_kind, url_or_env_reference, credential_kind, reference_key, active)
                VALUES
                    (@responseId, @bundle, 'generic-response-live', 'env:RESPONSE_URL', 'none', NULL, true);

                INSERT INTO topology.external_hook_ports
                    (hook_port_id, required_by_bundle, provider_kind, hook_path, header_key, route_key, credential_kind, reference_key, active)
                VALUES
                    (@hookId, @bundle, 'generic-hook-live', '/hooks/live', 'x-live-signature', @routeKey, 'external', 'vault-ref-hook', true);

                INSERT INTO topology.external_credential_vault
                    (credential_vault_id, provider_kind, required_by_bundle, token_kind, token_hash, encrypted_payload, encryption_key_reference, active)
                VALUES
                    (@vaultId, 'generic-access-live', @bundle, 'oauth_refresh_token', 'sha256:not-read-by-policy-repo', decode('CAFE', 'hex'), 'test-key-ref', true);
                """,
                ("accessId", accessId),
                ("inactiveAccessId", inactiveAccessId),
                ("responseId", responseId),
                ("hookId", hookId),
                ("vaultId", vaultId),
                ("bundle", bundle),
                ("routeKey", routeKey));

            var access = await repo.LoadPortRecordAsync(bundle, "access_port", null);
            var response = await repo.LoadPortRecordAsync(bundle, "response_port", null);
            var hook = await repo.LoadPortRecordAsync(bundle, "hook_port", routeKey);

            Assert.NotNull(access);
            Assert.Equal(accessId, access.PortId);
            Assert.Equal("access_port", access.PortKind);
            Assert.Equal("generic-access-live", access.ProviderKind);
            Assert.Equal("env:ACCESS_URL", access.UrlOrEnvReference);
            Assert.Equal("external", access.CredentialKind);
            Assert.Equal("vault-ref-access", access.ReferenceKey);

            Assert.NotNull(response);
            Assert.Equal(responseId, response.PortId);
            Assert.Equal("response_port", response.PortKind);
            Assert.Equal("generic-response-live", response.ProviderKind);
            Assert.Equal("env:RESPONSE_URL", response.UrlOrEnvReference);
            Assert.Equal("none", response.CredentialKind);
            Assert.Null(response.ReferenceKey);

            Assert.NotNull(hook);
            Assert.Equal(hookId, hook.PortId);
            Assert.Equal("hook_port", hook.PortKind);
            Assert.Equal("generic-hook-live", hook.ProviderKind);
            Assert.Equal("/hooks/live", hook.HookPath);
            Assert.Equal("x-live-signature", hook.HeaderKey);
            Assert.Equal(routeKey, hook.RouteKey);
            Assert.Equal("vault-ref-hook", hook.ReferenceKey);

            var projectedProperties = access.GetType().GetProperties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            Assert.DoesNotContain("EncryptedPayload", projectedProperties);
            Assert.DoesNotContain("TokenHash", projectedProperties);
        }
        finally
        {
            await CleanupAsync(cs, bundle);
        }
    }

    [Fact]
    public async Task LoadPortRecordAndPolicyAsync_FailClosesDuplicatesAndOrdersStepsWithJsonConfig()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var bundle = $"external-port-live-{suffix}";
        var accessId = Guid.NewGuid();
        var duplicateAccessId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        var duplicatePolicyId = Guid.NewGuid();
        var stepOne = Guid.NewGuid();
        var stepTwo = Guid.NewGuid();
        var duplicateStep = Guid.NewGuid();
        var repo = new NpgsqlExternalPortPolicyRepository(NullLogger<NpgsqlExternalPortPolicyRepository>.Instance, cs);

        try
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await ExecuteAsync(conn, """
                INSERT INTO topology.external_access_ports
                    (access_port_id, required_by_bundle, provider_kind, url_or_env_reference, credential_kind, reference_key, active)
                VALUES
                    (@accessId, @bundle, 'generic-access-live', 'env:ACCESS_URL', 'none', NULL, true);

                INSERT INTO topology.external_port_policies
                    (policy_id, policy_key, port_kind, required_by_bundle, active)
                VALUES
                    (@policyId, @policyKey, 'access_port', @bundle, true);

                INSERT INTO topology.external_port_policy_steps
                    (policy_step_id, policy_id, step_order, operation_key, step_config, active)
                VALUES
                    (@stepTwo, @policyId, 2, 'send_http', '{"retry":3}'::jsonb, true),
                    (@stepOne, @policyId, 1, 'build_http_request', '{"expected_signature":"sig-ok"}'::jsonb, true);
                """,
                ("accessId", accessId),
                ("policyId", policyId),
                ("policyKey", $"external_port_live_policy_{suffix}"),
                ("stepOne", stepOne),
                ("stepTwo", stepTwo),
                ("bundle", bundle));

            var port = await repo.LoadPortRecordAsync(bundle, "access_port", null);
            Assert.NotNull(port);

            var policy = await repo.LoadPolicyAsync(port);
            Assert.NotNull(policy);
            Assert.Equal(policyId, policy.PolicyId);
            Assert.Equal(new[] { 1, 2 }, policy.PolicySteps.Select(step => step.StepOrder).ToArray());
            Assert.Equal(new[] { "build_http_request", "send_http" }, policy.PolicySteps.Select(step => step.OperationKey).ToArray());
            Assert.Equal("sig-ok", policy.PolicySteps[0].StepConfig["expected_signature"]);
            Assert.Equal("3", policy.PolicySteps[1].StepConfig["retry"]);

            await ExecuteAsync(conn, """
                INSERT INTO topology.external_access_ports
                    (access_port_id, required_by_bundle, provider_kind, url_or_env_reference, credential_kind, reference_key, active)
                VALUES
                    (@duplicateAccessId, @bundle, 'generic-access-duplicate-live', 'env:DUPLICATE_URL', 'none', NULL, true);
                """,
                ("duplicateAccessId", duplicateAccessId),
                ("bundle", bundle));

            var duplicatePortError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                repo.LoadPortRecordAsync(bundle, "access_port", null));
            Assert.Equal("EXTERNAL_PORT_RECORD_AMBIGUOUS", duplicatePortError.Message);

            await ExecuteAsync(conn, "DELETE FROM topology.external_access_ports WHERE access_port_id = @duplicateAccessId;",
                ("duplicateAccessId", duplicateAccessId));

            await ExecuteAsync(conn, """
                INSERT INTO topology.external_port_policies
                    (policy_id, policy_key, port_kind, required_by_bundle, active)
                VALUES
                    (@duplicatePolicyId, @duplicatePolicyKey, 'access_port', @bundle, true);
                INSERT INTO topology.external_port_policy_steps
                    (policy_step_id, policy_id, step_order, operation_key, step_config, active)
                VALUES
                    (@duplicateStep, @duplicatePolicyId, 1, 'fail_close', '{}'::jsonb, true);
                """,
                ("duplicatePolicyId", duplicatePolicyId),
                ("duplicatePolicyKey", $"external_port_live_policy_duplicate_{suffix}"),
                ("duplicateStep", duplicateStep),
                ("bundle", bundle));

            var duplicatePolicyError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                repo.LoadPolicyAsync(port));
            Assert.Equal("EXTERNAL_PORT_POLICY_AMBIGUOUS", duplicatePolicyError.Message);
        }
        finally
        {
            await CleanupAsync(cs, bundle);
        }
    }

    [Fact]
    public async Task LoadPortRecordByCanonicalBindingAsync_HappyPath_AllThreePortKinds()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var manifestKey = $"test.canonical.binding.{suffix}";
        var manifestId = Guid.NewGuid();
        var hubId = Guid.NewGuid();
        var accessPortId = Guid.NewGuid();
        var responsePortId = Guid.NewGuid();
        var hookPortId = Guid.NewGuid();
        var bundle = $"canonical-binding-live-{suffix}";
        var routeKey = $"route-{suffix}";
        var repo = new NpgsqlExternalPortPolicyRepository(NullLogger<NpgsqlExternalPortPolicyRepository>.Instance, cs);

        try
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();

            await ExecuteAsync(conn, """
                INSERT INTO hubs.hub (hub_id, relation) VALUES (@hubId, '{}'::jsonb);
                INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status, topology_jsonb)
                VALUES (@manifestId, @hubId, @manifestKey, 'active', '{}'::jsonb);

                INSERT INTO topology.physical_tables (table_ref, schema_name, category, active)
                VALUES
                    ('topology.external_access_ports',   'topology', 'external_port_substrate', true),
                    ('topology.external_response_ports', 'topology', 'external_port_substrate', true),
                    ('topology.external_hook_ports',     'topology', 'external_port_substrate', true)
                ON CONFLICT (table_ref) DO NOTHING;

                INSERT INTO topology.physical_table_manifest_bindings (physical_table_id, topology_manifest_id, active, binding_evidence_json)
                SELECT pt.physical_table_id, @manifestId, true,
                       jsonb_build_object('portKind', CASE pt.table_ref
                           WHEN 'topology.external_access_ports'   THEN 'access_port'
                           WHEN 'topology.external_response_ports' THEN 'response_port'
                           WHEN 'topology.external_hook_ports'     THEN 'hook_port'
                       END, 'source', 'canonical_binding_live_test')
                FROM topology.physical_tables pt
                WHERE pt.table_ref IN (
                    'topology.external_access_ports',
                    'topology.external_response_ports',
                    'topology.external_hook_ports')
                ON CONFLICT (physical_table_id, topology_manifest_id) DO UPDATE SET active = true, updated_at = now();

                INSERT INTO topology.external_access_ports
                    (access_port_id, required_by_bundle, provider_kind, url_or_env_reference, credential_kind, reference_key, active)
                VALUES (@accessPortId, @bundle, 'generic-canonical-live', 'env:ACCESS_URL', 'none', NULL, true);

                INSERT INTO topology.external_response_ports
                    (response_port_id, required_by_bundle, provider_kind, url_or_env_reference, credential_kind, reference_key, active)
                VALUES (@responsePortId, @bundle, 'generic-canonical-live', 'env:RESPONSE_URL', 'none', NULL, true);

                INSERT INTO topology.external_hook_ports
                    (hook_port_id, required_by_bundle, provider_kind, hook_path, header_key, route_key, credential_kind, reference_key, active)
                VALUES (@hookPortId, @bundle, 'generic-canonical-live', '/hooks/canonical', 'x-canonical-sig', @routeKey, 'none', NULL, true);
                """,
                ("hubId", hubId), ("manifestId", manifestId), ("manifestKey", manifestKey),
                ("bundle", bundle), ("accessPortId", accessPortId),
                ("responsePortId", responsePortId), ("hookPortId", hookPortId), ("routeKey", routeKey));

            var access = await repo.LoadPortRecordByCanonicalBindingAsync(
                manifestKey, "topology.external_access_ports", "access_port", accessPortId, null);
            Assert.NotNull(access);
            Assert.Equal(accessPortId, access.PortId);
            Assert.Equal("access_port", access.PortKind);

            var response = await repo.LoadPortRecordByCanonicalBindingAsync(
                manifestKey, "topology.external_response_ports", "response_port", responsePortId, null);
            Assert.NotNull(response);
            Assert.Equal(responsePortId, response.PortId);
            Assert.Equal("response_port", response.PortKind);

            var hook = await repo.LoadPortRecordByCanonicalBindingAsync(
                manifestKey, "topology.external_hook_ports", "hook_port", hookPortId, routeKey);
            Assert.NotNull(hook);
            Assert.Equal(hookPortId, hook.PortId);
            Assert.Equal("hook_port", hook.PortKind);
        }
        finally
        {
            await CleanupCanonicalBindingAsync(cs, manifestId, hubId);
            await CleanupAsync(cs, bundle);
        }
    }

    [Fact]
    public async Task LoadPortRecordByCanonicalBindingAsync_InactiveBinding_FailsClose()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var manifestKey = $"test.canonical.inactive.{suffix}";
        var manifestId = Guid.NewGuid();
        var hubId = Guid.NewGuid();
        var responsePortId = Guid.NewGuid();
        var bundle = $"canonical-inactive-live-{suffix}";
        var repo = new NpgsqlExternalPortPolicyRepository(NullLogger<NpgsqlExternalPortPolicyRepository>.Instance, cs);

        try
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();

            await ExecuteAsync(conn, """
                INSERT INTO hubs.hub (hub_id, relation) VALUES (@hubId, '{}'::jsonb);
                INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status, topology_jsonb)
                VALUES (@manifestId, @hubId, @manifestKey, 'active', '{}'::jsonb);

                INSERT INTO topology.physical_tables (table_ref, schema_name, category, active)
                VALUES ('topology.external_response_ports', 'topology', 'external_port_substrate', true)
                ON CONFLICT (table_ref) DO NOTHING;

                INSERT INTO topology.physical_table_manifest_bindings (physical_table_id, topology_manifest_id, active, binding_evidence_json)
                SELECT pt.physical_table_id, @manifestId, false, '{}'::jsonb
                FROM topology.physical_tables pt
                WHERE pt.table_ref = 'topology.external_response_ports'
                ON CONFLICT (physical_table_id, topology_manifest_id) DO UPDATE SET active = false, updated_at = now();

                INSERT INTO topology.external_response_ports
                    (response_port_id, required_by_bundle, provider_kind, url_or_env_reference, credential_kind, reference_key, active)
                VALUES (@responsePortId, @bundle, 'generic-inactive-live', 'env:INACTIVE_URL', 'none', NULL, true);
                """,
                ("hubId", hubId), ("manifestId", manifestId), ("manifestKey", manifestKey),
                ("bundle", bundle), ("responsePortId", responsePortId));

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                repo.LoadPortRecordByCanonicalBindingAsync(
                    manifestKey, "topology.external_response_ports", "response_port", responsePortId, null));
            Assert.Equal("EXTERNAL_PORT_CANONICAL_BINDING_NOT_FOUND", error.Message);
        }
        finally
        {
            await CleanupCanonicalBindingAsync(cs, manifestId, hubId);
            await CleanupAsync(cs, bundle);
        }
    }

    [Fact]
    public async Task LoadPortRecordByCanonicalBindingAsync_MissingManifest_FailsClose()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var responsePortId = Guid.NewGuid();
        var repo = new NpgsqlExternalPortPolicyRepository(NullLogger<NpgsqlExternalPortPolicyRepository>.Instance, cs);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repo.LoadPortRecordByCanonicalBindingAsync(
                "no.such.manifest.key", "topology.external_response_ports", "response_port", responsePortId, null));
        Assert.Equal("EXTERNAL_PORT_CANONICAL_BINDING_NOT_FOUND", error.Message);
    }

    [Fact]
    public async Task LoadPortRecordByCanonicalBindingAsync_TableRefPortKindMismatch_FailsClose()
    {
        // Fail-close is a C# guard before any DB access — no live connection required.
        var repo = new NpgsqlExternalPortPolicyRepository(NullLogger<NpgsqlExternalPortPolicyRepository>.Instance, "Host=unused");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repo.LoadPortRecordByCanonicalBindingAsync(
                "auth.external.credential_management.projection",
                "topology.external_response_ports",
                "access_port",
                Guid.NewGuid(),
                null));
        Assert.Equal("EXTERNAL_PORT_CANONICAL_BINDING_TABLE_REF_PORT_KIND_MISMATCH", error.Message);
    }

    private static async Task CleanupCanonicalBindingAsync(string cs, Guid manifestId, Guid hubId)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await ExecuteAsync(conn, """
            DELETE FROM topology.physical_table_manifest_bindings WHERE topology_manifest_id = @manifestId;
            DELETE FROM hubs.topology_manifests WHERE topology_manifest_id = @manifestId;
            DELETE FROM hubs.hub WHERE hub_id = @hubId;
            """,
            ("manifestId", manifestId), ("hubId", hubId));
    }

    private static string? GetConnectionString()
    {
        var cs = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
                throw new InvalidOperationException(
                    "TOPOLACTOR_TEST_DB_CONNECTION is required when TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1.");
            return null;
        }

        return cs;
    }

    private static async Task ExecuteAsync(NpgsqlConnection conn, string sql, params (string Name, object? Value)[] args)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var (name, value) in args)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task CleanupAsync(string cs, string bundle)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await ExecuteAsync(conn, """
            DELETE FROM topology.external_port_policies WHERE required_by_bundle = @bundle;
            DELETE FROM topology.external_access_ports WHERE required_by_bundle = @bundle;
            DELETE FROM topology.external_response_ports WHERE required_by_bundle = @bundle;
            DELETE FROM topology.external_hook_ports WHERE required_by_bundle = @bundle;
            DELETE FROM topology.external_credential_vault WHERE required_by_bundle = @bundle;
            """,
            ("bundle", bundle));
    }
}
