using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live DB proof for the stripe_bundle port consumer (hook consumer) implementation.
///
/// Proves the generic external_port_substrate hook lane end to end against a real database:
///   hook_path/route_key -> active hook_port record -> credential_requirement/reference_key
///   resolution -> verify_signature_by_config -> execute_abstract_function (call_postgres_function)
///   -> webhook_intake_snapshots / signature_verification_evidence / payment_state_projections
///   -> runtime_event_log + DB projection response.
///
/// Boundary invariants (docs/design/runtime-bundle-stripe-ssot.yaml):
///   - signature verification success only -> scheduler enqueue + payment_state projection
///   - signature verification failure -> explicit rejection, verification_failure in
///     runtime_event_log + signature_verification_evidence, payment_state unchanged
///   - audit_log_boundary scope (webhook_received / verification_success / verification_failure /
///     payment_state_projected / ledger_binding_completed) present in runtime_event_log
///   - same stripe_event_id projects payment state idempotently
///   - DB projection response (not inline-only EndpointResponseDto)
///   - no Stripe provider-specific client/runtime; provider_kind is DB data only
///
/// TOPOLACTOR_TEST_DB_CONNECTION unset -> explicit local skip.
/// Set TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1 to require a live database in CI.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class StripePortConsumerLiveDbTests
{
    private const string StripeManifestId = "00000000-0000-0000-0000-0000000000a4";
    private const string SigningKeyReference = "vault:ref:stripe_webhook_signing_key";

    private static readonly string[] StripeTableRefs =
    [
        "topology.webhook_intake_snapshots",
        "topology.signature_verification_evidence",
        "topology.payment_state_projections"
    ];

    // -----------------------------------------------------------------------
    // physical_table_manifest_bindings: the three stripe evidence/projection
    // tables are actively bound to an active manifest (real binding rows).
    // -----------------------------------------------------------------------
    [Theory]
    [InlineData("topology.webhook_intake_snapshots")]
    [InlineData("topology.signature_verification_evidence")]
    [InlineData("topology.payment_state_projections")]
    public async Task StripeTable_IsBoundToActiveManifest(string tableRef)
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT 1
            FROM topology.physical_tables pt
            JOIN topology.physical_table_manifest_bindings pmb
              ON pmb.physical_table_id = pt.physical_table_id AND pmb.active
            JOIN hubs.topology_manifests tm
              ON tm.topology_manifest_id = pmb.topology_manifest_id AND tm.status = 'active'
            WHERE pt.active AND pt.table_ref = @t
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("t", tableRef);
        Assert.NotNull(await cmd.ExecuteScalarAsync());
    }

    [Fact]
    public async Task StripeManifest_HasScreenDataShape_WithForbiddenProjectionFields()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT topology_jsonb FROM hubs.topology_manifests WHERE topology_manifest_id = @id";
        cmd.Parameters.AddWithValue("id", Guid.Parse(StripeManifestId));
        var json = (string?)await cmd.ExecuteScalarAsync();
        Assert.NotNull(json);
        using var doc = JsonDocument.Parse(json!);
        Assert.True(doc.RootElement.TryGetProperty("screen_data_shape", out var shape));
        Assert.True(shape.TryGetProperty("forbiddenProjectionFields", out var forbidden));
        var fields = forbidden.EnumerateArray().Select(e => e.GetString()).ToHashSet();
        Assert.Contains("credential", fields);
        Assert.Contains("webhook_secret", fields);
        Assert.Contains("raw_provider_payload", fields);
    }

    // -----------------------------------------------------------------------
    // PostgreSQL function proof: verified-event authority gate + idempotency.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PaymentState_FailsClosed_BeforeVerification()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        var eventId = "evt_" + Guid.NewGuid().ToString("N");

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await Exec(conn, "SELECT topology.stripe_record_webhook_intake(@e, 'stripe', '/hooks/stripe', 'sha256:x')", ("e", eventId));

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Exec(conn, "SELECT topology.stripe_record_payment_state(@e, 'payment_intent.succeeded')", ("e", eventId)));
        Assert.Contains("STRIPE_PAYMENT_STATE_NOT_VERIFIED", ex.MessageText);

        // payment_state_projections must remain empty for this event id.
        Assert.Equal(0L, await Scalar(conn, "SELECT count(*) FROM topology.payment_state_projections WHERE stripe_event_id = @e", ("e", eventId)));
    }

    [Fact]
    public async Task PaymentState_IsIdempotent_OnSameStripeEventId()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        var eventId = "evt_" + Guid.NewGuid().ToString("N");

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await Exec(conn, "SELECT topology.stripe_record_webhook_intake(@e, 'stripe', '/hooks/stripe', 'sha256:x')", ("e", eventId));
        await Exec(conn, "SELECT topology.stripe_record_verification_success(@e)", ("e", eventId));
        await Exec(conn, "SELECT topology.stripe_record_payment_state(@e, 'payment_intent.succeeded')", ("e", eventId));
        await Exec(conn, "SELECT topology.stripe_record_payment_state(@e, 'payment_intent.succeeded')", ("e", eventId));
        await Exec(conn, "SELECT topology.stripe_record_ledger_binding(@e)", ("e", eventId));
        await Exec(conn, "SELECT topology.stripe_record_ledger_binding(@e)", ("e", eventId));

        Assert.Equal(1L, await Scalar(conn, "SELECT count(*) FROM topology.payment_state_projections WHERE stripe_event_id = @e", ("e", eventId)));
        Assert.Equal(1L, await Scalar(conn, "SELECT count(*) FROM topology.runtime_event_log WHERE entity_id = @e AND event_type = 'payment_state_projected'", ("e", eventId)));
        Assert.Equal(1L, await Scalar(conn, "SELECT count(*) FROM topology.runtime_event_log WHERE entity_id = @e AND event_type = 'ledger_binding_completed'", ("e", eventId)));
    }

    // -----------------------------------------------------------------------
    // Full dispatch lane proof through the production runtime chain.
    // ExternalPortDispatchRuntime -> CompatibilityShim -> ExternalPortPolicyStepExecutor
    // -> AbstractFunctionExecutor -> call_postgres_function -> live DB.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Dispatch_SignatureVerified_WritesEvidencePaymentStateAndProjection()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        var eventId = "evt_" + Guid.NewGuid().ToString("N");
        const string sig = "whsec_test_signature_value";

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await EnsureSigningKeyVault(conn, sig);

        var runtime = BuildRuntime(cs);
        var response = await runtime.ExecuteAsync(StripeHookRequest(eventId, "payment_intent.succeeded", sig), null);

        Assert.True(response.Success, string.Join(",", response.Errors.Select(e => e.Code)));

        // Physical writes happened only after verification.
        Assert.Equal(1L, await Scalar(conn, "SELECT count(*) FROM topology.webhook_intake_snapshots WHERE stripe_event_id = @e", ("e", eventId)));
        Assert.Equal("verified", (string?)await Scalar(conn,
            "SELECT verification_status FROM topology.signature_verification_evidence sve JOIN topology.webhook_intake_snapshots wis ON wis.webhook_intake_snapshot_id = sve.webhook_intake_snapshot_id WHERE wis.stripe_event_id = @e", ("e", eventId)));
        Assert.Equal("payment_intent.succeeded", (string?)await Scalar(conn,
            "SELECT payment_state FROM topology.payment_state_projections WHERE stripe_event_id = @e", ("e", eventId)));

        // Full SSOT audit_log_boundary scope present in runtime_event_log.
        foreach (var evt in new[] { "webhook_received", "verification_success", "payment_state_projected", "ledger_binding_completed" })
            Assert.Equal(1L, await Scalar(conn, "SELECT count(*) FROM topology.runtime_event_log WHERE entity_id = @e AND event_type = @t", ("e", eventId), ("t", evt)));

        // Projection response is sourced from the DB (payment_state + verification evidence),
        // not an inline-only EndpointResponseDto.
        var raw = response.Emission?.Data?.GetRawText() ?? "";
        Assert.Contains("projection_response", raw);
        Assert.Contains("payment_intent.succeeded", raw);
        Assert.Contains("verified", raw);
    }

    [Fact]
    public async Task Dispatch_SignatureFailure_RejectsAndRecordsFailure_NoPaymentState()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        var eventId = "evt_" + Guid.NewGuid().ToString("N");

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await EnsureSigningKeyVault(conn, "whsec_correct_value");

        var runtime = BuildRuntime(cs);
        // Wrong signature -> explicit rejection.
        var response = await runtime.ExecuteAsync(StripeHookRequest(eventId, "payment_intent.succeeded", "whsec_WRONG"), null);

        Assert.False(response.Success);
        Assert.Equal("EXTERNAL_SIGNATURE_VERIFICATION_FAILED", response.Errors[0].Code);

        // verification_failure recorded to runtime_event_log + evidence (at least the one just
        // produced; exact single-append / no-double-log is asserted in the unit contract test).
        Assert.True((long)(await Scalar(conn, "SELECT count(*) FROM topology.runtime_event_log WHERE event_type = 'verification_failure' AND required_by_bundle = 'stripe_bundle' AND logged_at > now() - interval '1 minute'"))! >= 1);
        Assert.True((long)(await Scalar(conn, "SELECT count(*) FROM topology.signature_verification_evidence WHERE verification_status = 'verification_failed' AND required_by_bundle = 'stripe_bundle' AND recorded_at > now() - interval '1 minute'"))! >= 1);

        // payment_state must NOT exist and no intake snapshot for this event id.
        Assert.Equal(0L, await Scalar(conn, "SELECT count(*) FROM topology.payment_state_projections WHERE stripe_event_id = @e", ("e", eventId)));
        Assert.Equal(0L, await Scalar(conn, "SELECT count(*) FROM topology.webhook_intake_snapshots WHERE stripe_event_id = @e", ("e", eventId)));
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------
    private static ExternalPortDispatchRuntime BuildRuntime(string cs)
    {
        var policyRepo = new NpgsqlExternalPortPolicyRepository(NullLogger<NpgsqlExternalPortPolicyRepository>.Instance, cs);
        var eventLogRepo = new NpgsqlExternalPortRuntimeEventLogRepository(cs);
        var evidenceRepo = new NpgsqlExternalPortConsumerEvidenceRepository(cs);
        var manifestRepo = new NpgsqlAbstractFunctionManifestRepository(cs);
        var vaultRepo = new NpgsqlExternalCredentialVaultRepository(NullLogger<NpgsqlExternalCredentialVaultRepository>.Instance, cs);
        var credentialResolver = new ExternalPortCredentialReferenceResolver(vaultRepo);

        var adapters = new IAbstractFunctionPrimitiveAdapter[]
        {
            new CallPostgresFunctionPrimitiveAdapter(cs),
            new ProjectionPrimitiveAdapter(),
            new EventLogPrimitiveAdapter(eventLogRepo),
            new SchedulerEnqueuePrimitiveAdapter(),
            new FailClosePrimitiveAdapter()
        };
        var abstractExecutor = new AbstractFunctionExecutor(manifestRepo, adapters);

        var inner = new ExternalPortPolicyStepExecutor(
            credentialReferenceResolver: credentialResolver,
            abstractFunctionExecutor: abstractExecutor,
            runtimeEventLogRepository: eventLogRepo,
            consumerEvidenceRepository: evidenceRepo);
        var shim = new ExternalPortPolicyStepExecutorCompatibilityShim(inner, abstractExecutor);

        return new ExternalPortDispatchRuntime(
            NullLogger<ExternalPortDispatchRuntime>.Instance, policyRepo, shim, null, eventLogRepo);
    }

    private static EndpointRequestDto StripeHookRequest(string stripeEventId, string paymentState, string signature)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            hook_path = "/hooks/stripe",
            route_key = "stripe",
            signature_input = new { signature },
            dispatch_payload = new
            {
                stripe_event_id = stripeEventId,
                payment_state = paymentState,
                payload_hash = "sha256:deadbeef"
            }
        });
        return new EndpointRequestDto(
            "dispatchExternalPort", "external_port", "external_port", "dispatchExternalPort",
            null, payload, null, "hook");
    }

    private static async Task EnsureSigningKeyVault(NpgsqlConnection conn, string tokenHash)
    {
        // Seed (or refresh) the stripe webhook signing key vault record so
        // resolve_credential_reference + verify_signature_by_config have a TokenHash to compare.
        // token_hash is a hash/reference value — never a plaintext secret.
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM topology.external_credential_vault WHERE reference_key = @ref;
            INSERT INTO topology.external_credential_vault
                (provider_kind, required_by_bundle, token_kind, token_hash, reference_key, active)
            VALUES ('stripe', 'stripe_bundle', 'webhook_signing_key', @h, @ref, true);
            """;
        cmd.Parameters.AddWithValue("h", tokenHash);
        cmd.Parameters.AddWithValue("ref", SigningKeyReference);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task Exec(NpgsqlConnection conn, string sql, params (string, object)[] ps)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in ps) cmd.Parameters.AddWithValue(k, v);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<object?> Scalar(NpgsqlConnection conn, string sql, params (string, object)[] ps)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in ps) cmd.Parameters.AddWithValue(k, v);
        return await cmd.ExecuteScalarAsync();
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
}
