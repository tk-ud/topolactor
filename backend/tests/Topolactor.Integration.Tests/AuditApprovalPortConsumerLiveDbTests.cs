using Npgsql;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live DB proof for audit_approval_bundle physical_table_manifest_bindings.
/// Verifies that after bootstrap/seed, the three audit_approval tables are:
///   - registered in topology.physical_tables with category audit_approval_bundle
///   - actively bound to manifest a7 in topology.physical_table_manifest_bindings
///   - carrying binding_evidence_json.bundle = audit_approval_bundle
/// TOPOLACTOR_TEST_DB_CONNECTION unset → explicit local skip.
/// Set TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1 to require a live database in CI.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class AuditApprovalPortConsumerLiveDbTests
{
    private const string ManifestId = "00000000-0000-0000-0000-0000000000a7";
    private const string ManifestKey = "audit_approval.response_port.evidence.projection";

    private static readonly string[] AuditApprovalTableRefs =
    [
        "topology.audit_approval_requests",
        "topology.audit_approval_evidence",
        "topology.audit_notification_evidence"
    ];

    // -----------------------------------------------------------------------
    // topology.physical_tables catalog registration
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("topology.audit_approval_requests")]
    [InlineData("topology.audit_approval_evidence")]
    [InlineData("topology.audit_notification_evidence")]
    public async Task AuditApprovalTable_IsRegisteredInPhysicalTables_WithCorrectCategory(string tableRef)
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
        Assert.Equal("audit_approval_bundle", reader.GetString(0));
        Assert.True(reader.GetBoolean(1), $"Expected {tableRef} to be active");
    }

    // -----------------------------------------------------------------------
    // topology.physical_table_manifest_bindings active binding to manifest a7
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("topology.audit_approval_requests")]
    [InlineData("topology.audit_approval_evidence")]
    [InlineData("topology.audit_notification_evidence")]
    public async Task AuditApprovalTable_IsActivelyBoundToManifestA7(string tableRef)
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

        var evidenceJson = reader.GetString(1);
        Assert.Contains("audit_approval_bundle", evidenceJson);
    }

    // -----------------------------------------------------------------------
    // binding_evidence_json fields: bundle / source / manifestKey
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AllThreeAuditApprovalTables_HaveCorrectBindingEvidenceJson()
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
        cmd.Parameters.AddWithValue("table_refs", AuditApprovalTableRefs);
        cmd.Parameters.AddWithValue("manifest_id", ManifestId);

        await using var reader = await cmd.ExecuteReaderAsync();

        var found = new List<string>();
        while (await reader.ReadAsync())
        {
            var tableRef = reader.GetString(0);
            var bundle = reader.IsDBNull(1) ? null : reader.GetString(1);
            var source = reader.IsDBNull(2) ? null : reader.GetString(2);
            var manifestKey = reader.IsDBNull(3) ? null : reader.GetString(3);

            Assert.Equal("audit_approval_bundle", bundle);
            Assert.Equal("audit-approval-bundle-form-preset-seed", source);
            Assert.Equal(ManifestKey, manifestKey);

            found.Add(tableRef);
        }

        Assert.Equal(3, found.Count);
        Assert.Contains("topology.audit_approval_requests", found);
        Assert.Contains("topology.audit_approval_evidence", found);
        Assert.Contains("topology.audit_notification_evidence", found);
    }

    // -----------------------------------------------------------------------
    // Helper
    // -----------------------------------------------------------------------

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
