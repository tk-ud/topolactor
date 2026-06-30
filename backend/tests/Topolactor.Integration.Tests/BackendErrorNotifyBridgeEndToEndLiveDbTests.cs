using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Scheduler;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// End-to-end live-DB evidence for the post-notify bridge against the seeded error_notify hook port.
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set. Requires
/// db/backend_error_evidence_tables.sql and db/backend_error_notify_hook_port_seed.sql applied.
///
/// Proves: append logs.error -> AFTER INSERT trigger enqueues -> bridge claims -> dispatch resolves
/// the seeded error_notify hook_port + logger-sink policy through ExternalPortDispatchRuntime ->
/// accepted boundary result -> queue row transitions to 'acknowledged', and the logger sink records
/// a 'backend_error_notify_delivered' runtime_event_log consumer event.
///
/// (Production routes the dispatch via RuntimeTimelineScheduler hook trigger -> ManifestDispatcher ->
/// external_port_runtime; this test wires the same ExternalPortDispatchRuntime boundary directly.)
/// </summary>
[Collection("BackendErrorEvidenceLiveDb")]
public class BackendErrorNotifyBridgeEndToEndLiveDbTests
{
    [Fact]
    public async Task Bridge_DispatchesThroughSeededHookPort_ReachesAcknowledged()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var marker = "notify-e2e-" + Guid.NewGuid().ToString("N");
        var startedAt = DateTimeOffset.UtcNow;

        var appender = new NpgsqlBackendErrorEvidenceAppender(cs);
        var queueRepo = new NpgsqlBackendErrorNotifyQueueRepository(cs);
        var policyRepo = new NpgsqlExternalPortPolicyRepository(
            NullLogger<NpgsqlExternalPortPolicyRepository>.Instance, cs);
        var eventLogRepo = new NpgsqlExternalPortRuntimeEventLogRepository(cs);
        var stepExecutor = new ExternalPortPolicyStepExecutor(runtimeEventLogRepository: eventLogRepo);
        var dispatchRuntime = new ExternalPortDispatchRuntime(
            NullLogger<ExternalPortDispatchRuntime>.Instance, policyRepo, stepExecutor);
        var bridge = new BackendErrorNotifyBridge(
            NullLogger<BackendErrorNotifyBridge>.Instance, cs, queueRepo,
            new DirectDispatchRuntimeSeam(dispatchRuntime), new BackendErrorNotifyBridgeOptions());

        await appender.AppendAsync(new BackendErrorEvidence(
            OriginLayer: BackendErrorOriginLayer.AbstractFunctionExecutor,
            BoundaryKey: marker,
            ErrorKind: BackendErrorKind.SystemError,
            ErrorCode: "NOTIFY_E2E_TEST",
            MessagePublic: "notify e2e",
            StackHash: "sha256:" + marker,
            Severity: "error",
            Retryable: false,
            RuntimeLane: "external_port_runtime"));
        try
        {
            // The seed resolves the dispatch target the bridge addresses.
            var record = await policyRepo.LoadHookPortRecordAsync("/hooks/error_notify", "error_notify");
            Assert.NotNull(record);
            var policy = await policyRepo.LoadPolicyAsync(record!);
            Assert.NotNull(policy);
            Assert.True(policy!.Active);
            Assert.NotEmpty(policy.PolicySteps);

            var processed = await bridge.ProcessOnceAsync(CancellationToken.None);
            Assert.True(processed >= 1);

            Assert.Equal("acknowledged", await StatusForMarkerAsync(cs, marker));
            Assert.True(await EventLoggedSinceAsync(cs, "backend_error_notify_delivered", startedAt),
                "logger-sink step must record a backend_error_notify_delivered runtime_event_log event");
        }
        finally
        {
            await CleanupAsync(cs, marker, startedAt);
        }
    }

    private sealed class DirectDispatchRuntimeSeam : IBackendErrorNotifyHookDispatcher
    {
        private readonly ExternalPortDispatchRuntime _runtime;
        public DirectDispatchRuntimeSeam(ExternalPortDispatchRuntime runtime) => _runtime = runtime;
        public Task<EndpointResponseDto> DispatchAsync(EndpointRequestDto request, CancellationToken ct = default) =>
            _runtime.ExecuteAsync(request, manifestId: null, ct);
    }

    private static async Task<string> StatusForMarkerAsync(string cs, string marker)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT q.status FROM logs.error_notify_queue q
            JOIN logs.error e ON e.error_id = q.error_id
            WHERE e.boundary_key = @marker
            """;
        cmd.Parameters.AddWithValue("marker", marker);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<bool> EventLoggedSinceAsync(string cs, string eventType, DateTimeOffset since)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT count(*) FROM topology.runtime_event_log
            WHERE event_type = @eventType AND required_by_bundle = 'backend_error_notify_bundle'
              AND logged_at >= @since
            """;
        cmd.Parameters.AddWithValue("eventType", eventType);
        cmd.Parameters.AddWithValue("since", since);
        return (long)(await cmd.ExecuteScalarAsync())! >= 1;
    }

    private static async Task CleanupAsync(string cs, string marker, DateTimeOffset since)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM logs.error_notify_queue
            WHERE error_id IN (SELECT error_id FROM logs.error WHERE boundary_key = @marker);
            DELETE FROM logs.error WHERE boundary_key = @marker;
            DELETE FROM topology.runtime_event_log
            WHERE event_type = 'backend_error_notify_delivered'
              AND required_by_bundle = 'backend_error_notify_bundle'
              AND logged_at >= @since;
            """;
        cmd.Parameters.AddWithValue("marker", marker);
        cmd.Parameters.AddWithValue("since", since);
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
