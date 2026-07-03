using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Scheduler;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB proof that cron, hook, and client triggers all reach the aggregate trigger
/// substrate through the same production route:
///   trigger -> RuntimeTimelineScheduler (trigger alignment) -> ManifestDispatcher
///   (role/target/layer/action axes) -> AggregateTriggerRuntime -> NpgsqlAggregateTriggerRepository
///   -> runtime_orchestration.* tables.
///
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set. Requires
/// db/runtime_orchestration_tables.sql applied to the target database.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class AggregateTriggerSubstrateRouteLiveDbTests
{
    [Fact]
    public async Task ClientTrigger_FullRoute_ReachesRepository_ThresholdNotSatisfiedThenMaterialized()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var defId = Guid.NewGuid();
        var scheduler = CreateScheduler(cs);
        await scheduler.StartAsync(CancellationToken.None);
        try
        {
            var r1 = await scheduler.AlignAndDispatchAsync(BuildRequest(defId, "event-1", "client", "client_operation_event", "conflict-client"));
            Assert.True(r1.Success);
            Assert.Contains("threshold_not_satisfied", r1.Emission!.Data!.Value.GetRawText());

            var r2 = await scheduler.AlignAndDispatchAsync(BuildRequest(defId, "event-2", "client", "client_operation_event", "conflict-client"));
            Assert.True(r2.Success);
            Assert.Contains("materialized", r2.Emission!.Data!.Value.GetRawText());

            var dup = await scheduler.AlignAndDispatchAsync(BuildRequest(defId, "event-2", "client", "client_operation_event", "conflict-client"));
            Assert.Contains("duplicate_event_evidence", dup.Emission!.Data!.Value.GetRawText());

            await AssertDbRowsAsync(cs, defId, "conflict-client", expectMaterialization: true);
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
            await AggregateTriggerRepositoryLiveDbTests.CleanupAsync(cs, defId);
        }
    }

    [Fact]
    public async Task CronTrigger_ReachesRepositoryThroughSchedulerQueue()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var defId = Guid.NewGuid();
        var scheduler = CreateScheduler(cs);
        await scheduler.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(scheduler.EnqueueCronTrigger(BuildRequest(defId, "cron-event-1", "cron", "scheduled_cron_event", "conflict-cron")));
            Assert.True(scheduler.EnqueueCronTrigger(BuildRequest(defId, "cron-event-2", "cron", "scheduled_cron_event", "conflict-cron")));

            await AssertDbRowsAsync(cs, defId, "conflict-cron", expectMaterialization: true, pollTimeout: TimeSpan.FromSeconds(10));
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
            await AggregateTriggerRepositoryLiveDbTests.CleanupAsync(cs, defId);
        }
    }

    [Fact]
    public async Task HookTrigger_ReachesRepositoryThroughSchedulerQueue()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var defId = Guid.NewGuid();
        var scheduler = CreateScheduler(cs);
        await scheduler.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(scheduler.EnqueueHookTrigger(BuildRequest(defId, "hook-event-1", "hook", "hook_event", "conflict-hook")));
            Assert.True(scheduler.EnqueueHookTrigger(BuildRequest(defId, "hook-event-2", "hook", "hook_event", "conflict-hook")));

            await AssertDbRowsAsync(cs, defId, "conflict-hook", expectMaterialization: true, pollTimeout: TimeSpan.FromSeconds(10));
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
            await AggregateTriggerRepositoryLiveDbTests.CleanupAsync(cs, defId);
        }
    }

    [Fact]
    public async Task ClientTrigger_ApprovalRequired_ThenGrantedOnFollowUpEvent_Materializes()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var defId = Guid.NewGuid();
        var scheduler = CreateScheduler(cs);
        await scheduler.StartAsync(CancellationToken.None);
        try
        {
            var definition = AggregateTriggerRepositoryLiveDbTests.BuildDefinition(defId) with
            {
                ApprovalPolicy = "require_backend_approval_before_materialization",
                ThresholdPolicy = new AggregateTriggerThresholdPolicy(1, "selected", "total", ">=", 0.5m),
            };

            var pending = await scheduler.AlignAndDispatchAsync(BuildRequest(definition, "approval-event-1", "conflict-approval", approvalGranted: false));
            Assert.Contains("approval_required", pending.Emission!.Data!.Value.GetRawText());

            var granted = await scheduler.AlignAndDispatchAsync(BuildRequest(definition, "approval-event-2", "conflict-approval", approvalGranted: true));
            Assert.Contains("materialized", granted.Emission!.Data!.Value.GetRawText());

            await AssertDbRowsAsync(cs, defId, "conflict-approval", expectMaterialization: true);
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
            await AggregateTriggerRepositoryLiveDbTests.CleanupAsync(cs, defId);
        }
    }

    private static async Task AssertDbRowsAsync(string cs, Guid defId, string conflictKey, bool expectMaterialization, TimeSpan? pollTimeout = null)
    {
        var deadline = DateTime.UtcNow + (pollTimeout ?? TimeSpan.Zero);
        while (true)
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT count(*) FROM runtime_orchestration.aggregate_trigger_current WHERE trigger_definition_id = @id AND conflict_key = @ck";
                cmd.Parameters.AddWithValue("id", defId);
                cmd.Parameters.AddWithValue("ck", conflictKey);
                var currentCount = (long)(await cmd.ExecuteScalarAsync())!;
                if (currentCount == 0 && DateTime.UtcNow < deadline) { await Task.Delay(200); continue; }
                Assert.Equal(1L, currentCount);
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT count(*) FROM runtime_orchestration.aggregate_trigger_materialization_evidence WHERE trigger_definition_id = @id AND conflict_key = @ck";
                cmd.Parameters.AddWithValue("id", defId);
                cmd.Parameters.AddWithValue("ck", conflictKey);
                var materializationCount = (long)(await cmd.ExecuteScalarAsync())!;
                if (expectMaterialization && materializationCount == 0 && DateTime.UtcNow < deadline) { await Task.Delay(200); continue; }
                Assert.Equal(expectMaterialization ? 1L : 0L, materializationCount);
            }

            return;
        }
    }

    private static RuntimeTimelineScheduler CreateScheduler(string connectionString)
    {
        var repository = new NpgsqlAggregateTriggerRepository(connectionString);
        var runtime = new AggregateTriggerRuntime(repository);
        var handlers = new Dictionary<string, IDispatchableRuntime> { ["aggregate_trigger_runtime"] = runtime };

        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        var adminRuntime = new AdminRuntime(NullLogger<AdminRuntime>.Instance, ctxRepo, registrar, pkg, uiRepo);
        var targetOverride = new TargetDispatchOverride(NullLogger<TargetDispatchOverride>.Instance, adminRuntime);

        var dispatcher = new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            handlers,
            new OperationVectorResolver(),
            targetOverride,
            manifestRepository: new FakeAggregateTriggerManifestRepository());

        return new RuntimeTimelineScheduler(NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher);
    }

    private static EndpointRequestDto BuildRequest(Guid defId, string eventId, string canonicalTriggerKind, string triggerSourceDetailKind, string conflictKey)
    {
        var definition = AggregateTriggerRepositoryLiveDbTests.BuildDefinition(defId) with
        {
            TriggerSource = new AggregateTriggerSource(canonicalTriggerKind, triggerSourceDetailKind),
        };
        return BuildRequest(definition, eventId, conflictKey, approvalGranted: false, triggerKind: canonicalTriggerKind);
    }

    private static EndpointRequestDto BuildRequest(AggregateTriggerDefinition definition, string eventId, string conflictKey, bool approvalGranted, string? triggerKind = null)
    {
        var runtimeRequest = new AggregateTriggerRuntimeRequest(
            definition,
            eventId,
            conflictKey,
            JsonSerializer.SerializeToElement(new { entity_id = conflictKey }),
            [definition.AggregateTargetBinding.TargetId],
            [definition.MaterializationTargetBinding.TargetId],
            "actor",
            "live-db-route-test",
            approvalGranted);
        var payload = JsonSerializer.SerializeToElement(runtimeRequest);
        return new("AggregateTrigger", "aggregate_trigger", "runtime", "execute", null, payload, null,
            triggerKind ?? definition.TriggerSource.CanonicalTriggerKind, "admin");
    }

    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();
}
