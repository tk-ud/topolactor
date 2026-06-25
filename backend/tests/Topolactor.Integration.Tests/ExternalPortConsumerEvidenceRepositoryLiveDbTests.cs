using System.Text.Json;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Runtime;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live PostgreSQL proof for correlated external_port_substrate consumer evidence.
/// TOPOLACTOR_TEST_DB_CONNECTION unset means explicit local skip. Set
/// TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1 to require a live database in CI.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class ExternalPortConsumerEvidenceRepositoryLiveDbTests
{
    public static IEnumerable<object[]> EvidenceTableRefs()
    {
        yield return ["topology.email_delivery_evidence", "send_success", "delivered"];
        yield return ["topology.webhook_intake_snapshots", "webhook_received", "received"];
        yield return ["topology.signature_verification_evidence", "signature_verification_success", "verified"];
        yield return ["topology.payment_state_projections", "payment_state_projected", "projected"];
        yield return ["topology.audit_approval_evidence", "approval_reviewed", "reviewed"];
        yield return ["topology.audit_notification_evidence", "notification_recorded", "notified"];
        yield return ["topology.sftp_transfer_log", "transfer_initiated", "transfer_initiated"];
    }

    [Theory]
    [MemberData(nameof(EvidenceTableRefs))]
    public async Task AppendEvidenceAsync_ThenLoadProjectionAsync_IsCorrelatedByDispatchIdAndSanitized(
        string tableRef,
        string eventType,
        string statusValue)
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatchId = Guid.NewGuid().ToString("N");
        var bundle = "external-port-evidence-live-test";
        var repo = new NpgsqlExternalPortConsumerEvidenceRepository(cs);
        var payload = JsonSerializer.SerializeToElement(new
        {
            customer = "cus_test",
            token = "should-not-be-projected",
            endpoint = "https://secret.example.invalid"
        });
        var context = new ExternalPortExecutionContext
        {
            DispatchId = dispatchId,
            RequiredByBundle = bundle,
            PortKind = tableRef.Contains("webhook", StringComparison.Ordinal) ? "hook_port" : "response_port",
            RouteKey = "route-live-test",
            RequestPayload = payload,
            PortRecord = new ExternalPortRecord(
                Guid.NewGuid(),
                tableRef.Contains("webhook", StringComparison.Ordinal) ? "hook_port" : "response_port",
                bundle,
                "generic-live-test",
                null,
                "/hooks/live-test",
                "x-signature",
                "route-live-test",
                "none",
                null,
                true)
        };

        try
        {
            await repo.AppendEvidenceAsync(
                tableRef,
                eventType,
                dispatchId,
                bundle,
                context,
                new Dictionary<string, string>
                {
                    ["status_value"] = statusValue,
                    ["projection_table_ref"] = tableRef
                });

            var rows = await repo.LoadProjectionAsync(tableRef, bundle, dispatchId, limit: 5);
            var unrelatedRows = await repo.LoadProjectionAsync(tableRef, bundle, Guid.NewGuid().ToString("N"), limit: 5);

            var row = Assert.Single(rows);
            Assert.Equal(tableRef, row.TableRef);
            Assert.Equal(dispatchId, row.EntityId);
            Assert.Equal(dispatchId, row.Evidence["dispatch_id"]);
            Assert.Equal(tableRef, row.Evidence["projection_table_ref"]);
            Assert.Empty(unrelatedRows);

            var rawEvidence = JsonSerializer.Serialize(row.Evidence);
            Assert.DoesNotContain("should-not-be-projected", rawEvidence, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret.example.invalid", rawEvidence, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", rawEvidence, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("endpoint", rawEvidence, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await CleanupDispatchAsync(cs, tableRef, dispatchId);
        }
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

    private static async Task CleanupDispatchAsync(string cs, string tableRef, string dispatchId)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = tableRef switch
        {
            "topology.email_delivery_evidence" => "DELETE FROM topology.email_delivery_evidence WHERE evidence_json->>'dispatch_id' = @dispatchId",
            "topology.webhook_intake_snapshots" => "DELETE FROM topology.webhook_intake_snapshots WHERE evidence_json->>'dispatch_id' = @dispatchId",
            "topology.signature_verification_evidence" => "DELETE FROM topology.signature_verification_evidence WHERE evidence_json->>'dispatch_id' = @dispatchId",
            "topology.payment_state_projections" => "DELETE FROM topology.payment_state_projections WHERE evidence_json->>'dispatch_id' = @dispatchId",
            "topology.audit_approval_evidence" => "DELETE FROM topology.audit_approval_evidence WHERE evidence_json->>'dispatch_id' = @dispatchId",
            "topology.audit_notification_evidence" => "DELETE FROM topology.audit_notification_evidence WHERE evidence_json->>'dispatch_id' = @dispatchId",
            "topology.sftp_transfer_log" => "DELETE FROM topology.sftp_transfer_log WHERE evidence_json->>'dispatch_id' = @dispatchId",
            _ => throw new InvalidOperationException("EXTERNAL_PORT_CONSUMER_EVIDENCE_TABLE_REF_UNSUPPORTED")
        };
        cmd.Parameters.AddWithValue("dispatchId", dispatchId);
        await cmd.ExecuteNonQueryAsync();
    }
}
