using Npgsql;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live DB proof for the email_bundle port consumer implementation.
///
/// Binding proof (mirrors the SSOT physical_table_manifest_binding requirement):
///   - the three email tables are registered in topology.physical_tables with
///     category email_bundle
///   - they are actively bound to manifest a3 in
///     topology.physical_table_manifest_bindings (real binding rows, not just
///     topology_jsonb.screen_data_shape)
///   - binding_evidence_json carries bundle/source/manifestKey
///
/// Approval-gate functional proof (no send without explicit human approval):
///   - em_record_dispatch_initiated fail-closes on an unapproved draft
///   - after em_record_approval, dispatch_initiated succeeds
///   - a second dispatch_initiated fail-closes (double-send guard)
///   - em_record_send_evidence records explicit send_success / send_failure
///
/// TOPOLACTOR_TEST_DB_CONNECTION unset → explicit local skip.
/// Set TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1 to require a live database in CI.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class EmailPortConsumerLiveDbTests
{
    private const string ManifestId = "00000000-0000-0000-0000-0000000000a3";
    private const string ManifestKey = "email.response_port.approval.delivery.projection";

    private static readonly string[] EmailTableRefs =
    [
        "topology.email_drafts",
        "topology.email_approval_records",
        "topology.email_delivery_evidence"
    ];

    // -----------------------------------------------------------------------
    // topology.physical_tables catalog registration
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("topology.email_drafts")]
    [InlineData("topology.email_approval_records")]
    [InlineData("topology.email_delivery_evidence")]
    public async Task EmailTable_IsRegisteredInPhysicalTables_WithCorrectCategory(string tableRef)
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT category, active
            FROM topology.physical_tables
            WHERE table_ref = @table_ref
            """;
        cmd.Parameters.AddWithValue("table_ref", tableRef);

        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), $"Expected {tableRef} in topology.physical_tables");
        Assert.Equal("email_bundle", reader.GetString(0));
        Assert.True(reader.GetBoolean(1), $"Expected {tableRef} to be active");
    }

    // -----------------------------------------------------------------------
    // topology.physical_table_manifest_bindings active binding to manifest a3
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("topology.email_drafts")]
    [InlineData("topology.email_approval_records")]
    [InlineData("topology.email_delivery_evidence")]
    public async Task EmailTable_IsActivelyBoundToManifestA3(string tableRef)
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT ptmb.active, ptmb.binding_evidence_json
            FROM topology.physical_table_manifest_bindings ptmb
            JOIN topology.physical_tables pt ON pt.physical_table_id = ptmb.physical_table_id
            WHERE pt.table_ref = @table_ref
              AND ptmb.topology_manifest_id = @manifest_id::uuid
            """;
        cmd.Parameters.AddWithValue("table_ref", tableRef);
        cmd.Parameters.AddWithValue("manifest_id", ManifestId);

        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(),
            $"Expected {tableRef} bound to manifest {ManifestId} in topology.physical_table_manifest_bindings");
        Assert.True(reader.GetBoolean(0), $"Expected active=true for {tableRef} binding");
        Assert.Contains("email_bundle", reader.GetString(1));
    }

    // -----------------------------------------------------------------------
    // binding_evidence_json fields: bundle / source / manifestKey
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AllThreeEmailTables_HaveCorrectBindingEvidenceJson()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT pt.table_ref,
                   ptmb.binding_evidence_json->>'bundle'      AS bundle,
                   ptmb.binding_evidence_json->>'source'      AS source,
                   ptmb.binding_evidence_json->>'manifestKey' AS manifest_key
            FROM topology.physical_table_manifest_bindings ptmb
            JOIN topology.physical_tables pt ON pt.physical_table_id = ptmb.physical_table_id
            WHERE pt.table_ref = ANY(@table_refs)
              AND ptmb.topology_manifest_id = @manifest_id::uuid
            ORDER BY pt.table_ref
            """;
        cmd.Parameters.AddWithValue("table_refs", EmailTableRefs);
        cmd.Parameters.AddWithValue("manifest_id", ManifestId);

        await using var reader = await cmd.ExecuteReaderAsync();
        var found = new List<string>();
        while (await reader.ReadAsync())
        {
            Assert.Equal("email_bundle", reader.IsDBNull(1) ? null : reader.GetString(1));
            Assert.Equal("email-bundle-form-preset-seed", reader.IsDBNull(2) ? null : reader.GetString(2));
            Assert.Equal(ManifestKey, reader.IsDBNull(3) ? null : reader.GetString(3));
            found.Add(reader.GetString(0));
        }

        Assert.Equal(3, found.Count);
        foreach (var t in EmailTableRefs) Assert.Contains(t, found);
    }

    [Fact]
    public async Task EmailManifestA3_HasScreenDataShape_BoundToEmailTables()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT topology_jsonb->'screen_data_shape'->>'tableRef' AS table_ref,
                   topology_jsonb->'screen_data_shape'->>'forbiddenProjectionFields' AS forbidden
            FROM hubs.topology_manifests
            WHERE topology_manifest_id = @manifest_id::uuid
            """;
        cmd.Parameters.AddWithValue("manifest_id", ManifestId);

        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Expected manifest a3 row");
        Assert.Equal("topology.email_drafts", reader.GetString(0));
        var forbidden = reader.GetString(1);
        Assert.Contains("credential", forbidden);
        Assert.Contains("smtp_host", forbidden);
        Assert.Contains("raw_provider_response", forbidden);
    }

    // -----------------------------------------------------------------------
    // Approval-gate + double-send guard + explicit delivery evidence
    // -----------------------------------------------------------------------

    [Fact]
    public async Task EmailDispatch_FailsClosed_WithoutApproval_ThenSucceeds_AfterApproval_AndPreventsDoubleSend()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatchKey = "test-email-" + Guid.NewGuid().ToString("N");
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        try
        {
            var draftId = await ScalarGuidAsync(conn,
                "SELECT topology.em_record_draft(@k, @r, @s, NULL)",
                ("k", dispatchKey), ("r", "rcpt-ref"), ("s", "subj-ref"));

            // 1. Dispatch before approval must fail closed.
            var ex = await Assert.ThrowsAsync<PostgresException>(() =>
                ExecAsync(conn, "SELECT topology.em_record_dispatch_initiated(@id::uuid)", ("id", draftId.ToString())));
            Assert.Contains("EMAIL_DISPATCH_NOT_APPROVED", ex.MessageText);

            // 2. Record explicit approval.
            await ExecAsync(conn, "SELECT topology.em_record_approval(@id::uuid, 'approved', 'tester')", ("id", draftId.ToString()));

            // 3. Dispatch now succeeds and marks the draft dispatch_initiated.
            await ExecAsync(conn, "SELECT topology.em_record_dispatch_initiated(@id::uuid)", ("id", draftId.ToString()));
            Assert.Equal("dispatch_initiated", await DraftStatusAsync(conn, draftId));

            // 4. A second dispatch fails closed (double-send guard / idempotency).
            var ex2 = await Assert.ThrowsAsync<PostgresException>(() =>
                ExecAsync(conn, "SELECT topology.em_record_dispatch_initiated(@id::uuid)", ("id", draftId.ToString())));
            Assert.Contains("EMAIL_DISPATCH_ALREADY_INITIATED", ex2.MessageText);

            // 5. Explicit send_success evidence recorded; draft delivered; the
            //    result-driven outcome lands in runtime_event_log (not send_dispatched).
            await ExecAsync(conn, "SELECT topology.em_record_send_evidence(@id::uuid, 'sent')", ("id", draftId.ToString()));
            Assert.Equal("delivered", await DraftStatusAsync(conn, draftId));
            Assert.True(await EvidenceExistsAsync(conn, draftId, "send_success", "delivered"));
            Assert.True(await RuntimeEventLogExistsAsync(conn, draftId, "send_success"));
        }
        finally
        {
            await CleanupAsync(conn, dispatchKey);
        }
    }

    [Fact]
    public async Task EmailSendEvidence_RecordsExplicitFailure_NoSilentFallback()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatchKey = "test-email-fail-" + Guid.NewGuid().ToString("N");
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        try
        {
            var draftId = await ScalarGuidAsync(conn,
                "SELECT topology.em_record_draft(@k, @r, @s, NULL)",
                ("k", dispatchKey), ("r", "rcpt-ref"), ("s", "subj-ref"));

            await ExecAsync(conn, "SELECT topology.em_record_send_evidence(@id::uuid, 'failed')", ("id", draftId.ToString()));
            Assert.Equal("failed", await DraftStatusAsync(conn, draftId));
            Assert.True(await EvidenceExistsAsync(conn, draftId, "send_failure", "failed"));
            // Explicit failure is recorded to runtime_event_log — no silent fallback.
            Assert.True(await RuntimeEventLogExistsAsync(conn, draftId, "send_failure"));
        }
        finally
        {
            await CleanupAsync(conn, dispatchKey);
        }
    }

    [Fact]
    public async Task EmailRedispatch_AfterSendFailure_FailsClosed_RequiringNewApprovalFlow()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatchKey = "test-email-redispatch-" + Guid.NewGuid().ToString("N");
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        try
        {
            var draftId = await ScalarGuidAsync(conn,
                "SELECT topology.em_record_draft(@k, @r, @s, NULL)",
                ("k", dispatchKey), ("r", "rcpt-ref"), ("s", "subj-ref"));

            // approval → dispatch → send_failure
            await ExecAsync(conn, "SELECT topology.em_record_approval(@id::uuid, 'approved', 'tester')", ("id", draftId.ToString()));
            await ExecAsync(conn, "SELECT topology.em_record_dispatch_initiated(@id::uuid)", ("id", draftId.ToString()));
            await ExecAsync(conn, "SELECT topology.em_record_send_evidence(@id::uuid, 'failed')", ("id", draftId.ToString()));
            Assert.Equal("failed", await DraftStatusAsync(conn, draftId));

            // Re-dispatch of the same approved draft after failure must fail closed.
            var ex = await Assert.ThrowsAsync<PostgresException>(() =>
                ExecAsync(conn, "SELECT topology.em_record_dispatch_initiated(@id::uuid)", ("id", draftId.ToString())));
            Assert.Contains("EMAIL_DISPATCH_ALREADY_INITIATED", ex.MessageText);
        }
        finally
        {
            await CleanupAsync(conn, dispatchKey);
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static async Task<Guid> ScalarGuidAsync(NpgsqlConnection conn, string sql, params (string, object)[] ps)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task ExecAsync(NpgsqlConnection conn, string sql, params (string, object)[] ps)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v);
        await cmd.ExecuteScalarAsync();
    }

    private static async Task<string?> DraftStatusAsync(NpgsqlConnection conn, Guid draftId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT delivery_status FROM topology.email_drafts WHERE email_draft_id = @id";
        cmd.Parameters.AddWithValue("id", draftId);
        return (string?)await cmd.ExecuteScalarAsync();
    }

    private static async Task<bool> EvidenceExistsAsync(NpgsqlConnection conn, Guid draftId, string eventType, string deliveryStatus)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT 1 FROM topology.email_delivery_evidence
            WHERE email_draft_id = @id AND event_type = @e AND delivery_status = @s LIMIT 1
            """;
        cmd.Parameters.AddWithValue("id", draftId);
        cmd.Parameters.AddWithValue("e", eventType);
        cmd.Parameters.AddWithValue("s", deliveryStatus);
        return await cmd.ExecuteScalarAsync() is not null;
    }

    private static async Task<bool> RuntimeEventLogExistsAsync(NpgsqlConnection conn, Guid draftId, string eventType)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT 1 FROM topology.runtime_event_log
            WHERE entity_id = @id AND event_type = @e AND required_by_bundle = 'email_bundle' LIMIT 1
            """;
        cmd.Parameters.AddWithValue("id", draftId.ToString());
        cmd.Parameters.AddWithValue("e", eventType);
        return await cmd.ExecuteScalarAsync() is not null;
    }

    private static async Task CleanupAsync(NpgsqlConnection conn, string dispatchKey)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM topology.runtime_event_log
             WHERE required_by_bundle = 'email_bundle'
               AND entity_id IN (SELECT email_draft_id::text FROM topology.email_drafts WHERE dispatch_idempotency_key = @k);
            DELETE FROM topology.email_delivery_evidence
             WHERE email_draft_id IN (SELECT email_draft_id FROM topology.email_drafts WHERE dispatch_idempotency_key = @k);
            DELETE FROM topology.email_approval_records
             WHERE email_draft_id IN (SELECT email_draft_id FROM topology.email_drafts WHERE dispatch_idempotency_key = @k);
            DELETE FROM topology.email_drafts WHERE dispatch_idempotency_key = @k;
            """;
        cmd.Parameters.AddWithValue("k", dispatchKey);
        await cmd.ExecuteNonQueryAsync();
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
