using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Scheduler;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Production-route end-to-end evidence for the post-notify bridge.
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set. Requires the FULL init.sql
/// (so the external_port dispatch manifest and the error_notify hook port seed both exist).
///
/// Unlike the direct-seam test, this exercises the SSOT-required production route:
///   append logs.error -> AFTER INSERT trigger enqueue -> BackendErrorNotifyBridge.ProcessOnceAsync
///   -> SchedulerBackendErrorNotifyHookDispatcher -> RuntimeTimelineScheduler.AlignAndDispatchAsync
///   -> ManifestDispatcher -> external_port_runtime (ExternalPortDispatchRuntime)
///   -> seeded error_notify hook_port + logger-sink policy resolve -> accepted
///   -> logs.error_notify_queue.status = acknowledged.
///
/// Only the runtime scheduler is started as a hosted service; every boundary on the route is the
/// real component wired against the live database.
/// </summary>
[Collection("BackendErrorEvidenceLiveDb")]
public class BackendErrorNotifyBridgeProductionRouteLiveDbTests
{
    [Fact]
    public async Task FullSchedulerDispatcherRoute_ReachesAcknowledged()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var marker = "notify-prod-" + Guid.NewGuid().ToString("N");
        var startedAt = DateTimeOffset.UtcNow;

        var appender = new NpgsqlBackendErrorEvidenceAppender(cs);
        var queueRepo = new NpgsqlBackendErrorNotifyQueueRepository(cs);

        // Real external_port_runtime boundary against the live-seeded hook port + logger-sink policy.
        var policyRepo = new NpgsqlExternalPortPolicyRepository(
            NullLogger<NpgsqlExternalPortPolicyRepository>.Instance, cs);
        var eventLogRepo = new NpgsqlExternalPortRuntimeEventLogRepository(cs);
        var stepExecutor = new ExternalPortPolicyStepExecutor(runtimeEventLogRepository: eventLogRepo);
        var dispatchRuntime = new ExternalPortDispatchRuntime(
            NullLogger<ExternalPortDispatchRuntime>.Instance, policyRepo, stepExecutor);

        // Real manifest-driven dispatcher (resolves external_port -> external_port_runtime from seed).
        var manifestRepo = new NpgsqlManifestRepository(NullLogger<NpgsqlManifestRepository>.Instance, cs);
        var handlers = new Dictionary<string, IDispatchableRuntime>
        {
            ["external_port_runtime"] = dispatchRuntime,
        };
        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance, handlers, new OperationVectorResolver(),
            BuildUnusedTargetDispatchOverride(cs), manifestRepo);

        // Real runtime scheduler hosted service + the production hook dispatch seam.
        var scheduler = new RuntimeTimelineScheduler(NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher);
        await scheduler.StartAsync(CancellationToken.None);
        try
        {
            var hookDispatcher = new SchedulerBackendErrorNotifyHookDispatcher(scheduler);
            var bridge = new BackendErrorNotifyBridge(
                NullLogger<BackendErrorNotifyBridge>.Instance, cs, queueRepo, hookDispatcher,
                new BackendErrorNotifyBridgeOptions());

            // Sanity: the real manifest repo resolves the production destination for the bridge axes.
            var manifest = await manifestRepo.ResolveActiveManifestAsync(
                role: null, target: "external_port", layer: "external_port", action: "dispatchExternalPort");
            Assert.NotNull(manifest);

            await appender.AppendAsync(new BackendErrorEvidence(
                OriginLayer: BackendErrorOriginLayer.AbstractFunctionExecutor,
                BoundaryKey: marker,
                ErrorKind: BackendErrorKind.SystemError,
                ErrorCode: "NOTIFY_PROD_ROUTE_TEST",
                MessagePublic: "notify prod route",
                StackHash: "sha256:" + marker,
                Severity: "error",
                Retryable: false,
                RuntimeLane: "external_port_runtime"));

            var processed = await bridge.ProcessOnceAsync(CancellationToken.None);
            Assert.True(processed >= 1);

            Assert.Equal("acknowledged", await StatusForMarkerAsync(cs, marker));
            Assert.True(await EventLoggedSinceAsync(cs, "backend_error_notify_delivered", startedAt),
                "production route must reach the logger sink and record backend_error_notify_delivered");
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
            await CleanupAsync(cs, marker, startedAt);
        }
    }

    // TargetDispatchOverride is NOT consulted on the production ManifestDispatcher path (non-null
    // manifest repository), but the constructor requires a non-null instance. Built from cheap
    // Npgsql constructors that never open a connection here.
    private static TargetDispatchOverride BuildUnusedTargetDispatchOverride(string cs)
    {
        var contextRouteRepository = new NpgsqlContextRouteRepository(NullLogger<NpgsqlContextRouteRepository>.Instance, cs);
        var topologyRepository = new NpgsqlTopologyRepository(NullLogger<NpgsqlTopologyRepository>.Instance, cs);
        var topologyVectorRuntime = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, contextRouteRepository);
        var uiTopologyRepository = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, cs);
        var adminRuntime = new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            contextRouteRepository,
            new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topologyRepository, topologyVectorRuntime),
            new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiTopologyRepository),
            uiTopologyRepository);
        return new TargetDispatchOverride(NullLogger<TargetDispatchOverride>.Instance, adminRuntime);
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
