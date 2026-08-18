using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Endpoint;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB proof for the scheduler-settings subBundle (admin-surface-topology-seed-conversion),
/// db/seed_empty.sql manifest 00000000-0000-0000-0000-00000005c100
/// ("scheduler.settings.projection") and its own hub 00000000-0000-0000-0000-00000005c101.
///
/// 2026-07-22 Owner-confirmed design (.agent/tasks/todo.md "scheduler-settings 3分割設計の確定"):
/// this manifest is a seed-conversion projection artifact that proves the Manifest/hub/package/
/// layout/wiring/tensor and mutation_confirmation_contract for this surface are real and
/// dispatchable -- it is NOT frontend/routes/admin/scheduler.tsx's own route body. That route
/// keeps mounting the existing (scope-reduced, not deleted) frontend/islands/
/// SchedulerJobSettingsPanel.tsx, which dispatches the SAME scheduler_jobs:list_settings/enable/
/// disable actions this manifest's own wiring targets, directly against the axes-routed f0/f3/f4
/// dispatcher_mapping rows (role=admin, target=admin) -- never through this manifest's target_ref.
/// This test file proves the manifest's own reachability/authoring/mutation chain lives and is
/// correct; it does not assert or require the frontend route to consume it.
///
/// Reuses HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync (manifest-agnostic,
/// already wires schedulerJobManifestRepository + sqlAttentionLogsRepository from the earlier
/// credential-management round -- no modification needed) the same way admin-enum / team-dashboard
/// / credential-management prove their own manifests.
///
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set. Requires db/seed_empty.sql
/// applied to the target database.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class SchedulerSettingsHubRelationUiProjectionLiveDbTests
{
    private static readonly Guid SchedulerSettingsManifestId =
        new("00000000-0000-0000-0000-00000005c100");
    private static readonly Guid SchedulerSettingsHubId =
        new("00000000-0000-0000-0000-00000005c101");

    /// <summary>
    /// Combined resolution_chain + hub_navigation:create authoring dispatch proof, per the
    /// navigation_binding_authoring_and_verification resolution criterion confirmed in
    /// .agent/tasks/todo.md (2026-07-22): the SAME live-DB test authors a hub_relation FROM this
    /// subBundle's own manifest via the real hub_navigation:create dispatch action (never a raw
    /// SQL insert standing in for the authoring path), then re-dispatches this subBundle's own
    /// manifest and confirms the full resolution chain (hub_relation -> topology_manifest ->
    /// hub_ids[]/package_ids[] -> package/layout/wiring/tensor -> ManifestDispatcher.DispatchAsync
    /// -> scalar Emission) reflects the authored relation. Mirrors admin-enum's own combined test
    /// (the reference pattern for this criterion). Deliberately does NOT seed a hubs.hub_relations
    /// row in db/seed_empty.sql -- same convention as admin-enum/credential-management/
    /// team-dashboard: navigation_binding_authoring_and_verification is proven live, at test time,
    /// through this real dispatch, not by a static seed row.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_SchedulerSettingsManifest_HubNavigationCreate_RealAuthoringPath_ThenResolutionChainReflectsIt()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var targetHubId = Guid.NewGuid();
        var targetManifestId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        async Task ExecAsync(string sql, params (string Name, object Value)[] parms)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parms) cmd.Parameters.AddWithValue(name, value);
            await cmd.ExecuteNonQueryAsync();
        }

        Guid? createdHubRelationId = null;
        try
        {
            // Ordinary target for the relation -- any existing manifest an admin could pick via
            // /admin/manifests.
            await ExecAsync("INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", targetHubId));
            await ExecAsync(
                "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) VALUES (@mid, @hid, @key, 'active')",
                ("mid", targetManifestId), ("hid", targetHubId), ("key", $"live-db-scheduler-settings-nav-target-{suffix}"));

            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

            // STEP 1: author the relation FROM scheduler-settings' own manifest via the REAL
            // hub_navigation:create dispatch action (frontend/api/adminApi.ts createHubRelation()
            // -> HubNavigationAdmin.tsx path, /admin/manifests) -- never a raw SQL insert.
            var createPayload = JsonSerializer.SerializeToElement(new
            {
                topologyManifestId = SchedulerSettingsManifestId.ToString(),
                relatedHubId = targetHubId.ToString(),
                sequencePosition = 1,
            });
            var createRequest = new EndpointRequestDto(
                OperationType: "HubNavigationAdminScenario",
                Target: "admin",
                Layer: "hub_navigation",
                Action: "create",
                IdOrHubId: null, Payload: createPayload, Context: null, TriggerKind: "client", Role: "admin");
            var createResponse = await dispatcher.DispatchAsync(createRequest);

            Assert.True(
                createResponse.Success,
                string.Join(";", createResponse.Errors.Select(e => e.Code + ":" + e.Message)));

            var contentBundleRepo = new NpgsqlContentBundleRepository(NullLogger<NpgsqlContentBundleRepository>.Instance, cs);
            var relations = await contentBundleRepo.ListHubRelationsByManifestAsync(SchedulerSettingsManifestId);
            var created = Assert.Single(relations, r => r.RelatedHubId == targetHubId.ToString());
            createdHubRelationId = Guid.Parse(created.HubRelationId);
            Assert.Equal(1, created.SequencePosition);
            Assert.Equal("active", created.Status);

            // STEP 2: resolution chain -- dispatch scheduler-settings' own manifest via target_ref
            // (projection_entry) and confirm BOTH halves in one call: (a) the full SSOT
            // component_tree resolves (package/layout/wiring/tensor -> ManifestDispatcher.
            // DispatchAsync -> scalar Emission.LayoutNodes, no unresolved leaves), and (b)
            // Emission.NavigationSequence reflects the relation just authored through the real
            // hub_navigation:create action above.
            var payload = JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{SchedulerSettingsManifestId}:projection_entry",
            });
            var request = new EndpointRequestDto(
                "Search", "default", "screen_list", "Search",
                IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "admin");
            var response = await dispatcher.DispatchAsync(request);

            Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.NotNull(response.Emission);
            var emission = response.Emission!;

            var unresolvedLeaves = emission.LayoutNodes!
                .Where(n => n.NodeKind == "catalog_component" && n.ComponentId is null && n.ComponentKind is null)
                .ToList();
            Assert.Empty(unresolvedLeaves);

            // Structural leaves this subBundle's seed declares: filter bar, table, and the
            // enable/disable button+confirm-modal pairs. Confirms the seed adopted the generated
            // tensor's real node set, not a stub.
            Assert.Contains(emission.LayoutNodes!, n => n.NodeId == "scheduler_search");
            Assert.Contains(emission.LayoutNodes!, n => n.NodeId == "scheduler_filter_trigger_kind");
            Assert.Contains(emission.LayoutNodes!, n => n.NodeId == "scheduler_filter_schedule_policy_kind");
            Assert.Contains(emission.LayoutNodes!, n => n.NodeId == "scheduler_filter_active");
            Assert.Contains(emission.LayoutNodes!, n => n.NodeId == "scheduler_job_list");
            Assert.Contains(emission.LayoutNodes!, n => n.NodeId == "scheduler_enable_button");
            Assert.Contains(emission.LayoutNodes!, n => n.NodeId == "scheduler_disable_button");

            HubRelationUiProjectionResolutionChainProof.AssertNavigationSequenceResolvesHubVector(
                emission,
                SchedulerSettingsManifestId,
                [new HubRelationUiProjectionResolutionChainProof.ExpectedHubVectorEntry(targetHubId, 1, targetManifestId)]);
        }
        finally
        {
            if (createdHubRelationId is not null)
                await ExecAsync("DELETE FROM hubs.hub_relations WHERE hub_relation_id = @rid", ("rid", createdHubRelationId.Value));
            await ExecAsync("DELETE FROM hubs.topology_manifests WHERE topology_manifest_id = @mid", ("mid", targetManifestId));
            await ExecAsync("DELETE FROM hubs.hub WHERE hub_id = @hid", ("hid", targetHubId));
        }
    }

    /// <summary>
    /// Generic route→Manifest identity resolution proof (manifest_key_target_ref_resolution_
    /// contract, established by the team-dashboard subBundle's closure round): the SAME production
    /// 5c100 manifest, reached WITHOUT its UUID appearing anywhere in the request -- target_ref
    /// names it only by hubs.topology_manifests.manifest_key ("manifest_key:
    /// scheduler.settings.projection:projection_entry"). This is a GENERIC resolvability proof
    /// only: it does not imply, and this round does NOT make, frontend/routes/admin/scheduler.tsx
    /// consume this manifest by manifest_key or otherwise -- the route stays hardcoded per the
    /// Owner-confirmed hub_navigation_only design.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_SchedulerSettingsManifest_ProjectionEntry_ByManifestKey_ResolvesSameProductionManifest()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var payload = JsonSerializer.SerializeToElement(new
        {
            target_ref = "manifest_key:scheduler.settings.projection:projection_entry",
        });
        var request = new EndpointRequestDto(
            "Search", "default", "screen_list", "Search",
            IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "admin");

        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
        var emission = response.Emission!;
        Assert.Equal(SchedulerSettingsManifestId.ToString(), emission.ManifestId);
        Assert.Contains(emission.LayoutNodes!, n => n.NodeId == "scheduler_job_list");
    }

    /// <summary>
    /// default_screen_read (2026-08-17 mechanism, runtime-orchestration-ssot.yaml
    /// dispatcher_contract.default_screen_read_override): scheduler-settings' list_settings wiring
    /// row declares it, so an initial-mount-style structural dispatch (the SAME "...projection_
    /// entry" request every other test in this file uses) returns REAL scheduler_jobs data on
    /// Emission.Data, not the structural-render-only fallback's empty/absent data. Uses a real
    /// topology.scheduler_jobs row (this test's own, cleaned up after) so the assertion proves a
    /// genuine data dispatch reached AdminRuntime.DataListSchedulerJobsSettingsAsync, not a
    /// coincidence of an already-empty table.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_SchedulerSettingsManifest_ProjectionEntry_DefaultScreenRead_ReturnsRealData()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var schedulerJobId = Guid.NewGuid();
        var jobKey = $"live-db-scheduler-settings-dsr-{suffix}";

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
                "INSERT INTO topology.scheduler_jobs (scheduler_job_id, job_key, trigger_kind, schedule_policy_kind, schedule_interval_seconds, authority_scope, active) " +
                "VALUES (@id, @key, 'cron', 'interval_seconds', 300, @scope, true)",
                ("id", schedulerJobId), ("key", jobKey), ("scope", $"live-db-scheduler-settings-dsr-scope-{suffix}"));

            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
            var payload = JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{SchedulerSettingsManifestId}:projection_entry",
            });
            var request = new EndpointRequestDto(
                "Search", "default", "screen_list", "Search",
                IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "admin");
            var response = await dispatcher.DispatchAsync(request);

            Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
            var emission = response.Emission!;
            Assert.NotNull(emission.Data);
            var dataText = emission.Data!.Value.GetRawText();
            Assert.Contains(jobKey, dataText);

            // Forbidden fields (docs/design/admin-normal-surface-projection-seed-ssot.yaml
            // surface_axes.admin.surfaces.scheduler.forbidden_projection_fields) never appear in
            // this projection, even on the default_screen_read path.
            Assert.DoesNotContain("credentialRequirementRef", dataText);
            Assert.DoesNotContain("externalPortRef", dataText);
            Assert.DoesNotContain("authorityScope", dataText);
        }
        finally
        {
            await ExecAsync("DELETE FROM topology.scheduler_jobs WHERE scheduler_job_id = @id", ("id", schedulerJobId));
        }
    }

    /// <summary>
    /// mutation_confirmation_contract exercised through the manifest's own real target_ref
    /// dispatch (not the axes route): dryRun preview is non-mutating -> unconfirmed write fails
    /// closed -> confirmed write persists and appends a real logs.diff row. Mirrors credential-
    /// management's own dryRun/unconfirmed/confirmed live-DB pattern for the SAME
    /// AdminMasterRosterAudit.AppendAsync -> logs.diff authority scheduler_jobs:enable/disable
    /// share with every other admin mutation.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_SchedulerSettingsManifest_Disable_DryRunThenConfirmedWrite_AppendsDiffLog()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var schedulerJobId = Guid.NewGuid();
        var jobKey = $"live-db-scheduler-settings-mut-{suffix}";

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
                "INSERT INTO topology.scheduler_jobs (scheduler_job_id, job_key, trigger_kind, schedule_policy_kind, schedule_interval_seconds, authority_scope, active) " +
                "VALUES (@id, @key, 'cron', 'interval_seconds', 300, @scope, true)",
                ("id", schedulerJobId), ("key", jobKey), ("scope", $"live-db-scheduler-settings-mut-scope-{suffix}"));

            var t0 = DateTimeOffset.UtcNow.AddSeconds(-1);
            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

            // AuthenticatedRole (the gate SetSchedulerJobActiveWithConfirmationAsync checks) is
            // sourced only from the JWT-verified DispatchAuthContext.AuthenticatedRolesKey context
            // entry -- EndpointRequestDto.Role is a separate axis (manifest-level capability_
            // requirement / target_ref role check), not a stand-in for it.
            var adminContext = new Dictionary<string, string> { [DispatchAuthContext.AuthenticatedRolesKey] = "admin" };

            // STEP 1: dryRun preview -- non-mutating.
            var dryRunPayload = JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{SchedulerSettingsManifestId}:scheduler_jobs:disable",
                schedulerJobId = schedulerJobId.ToString(),
                dryRun = true,
            });
            var dryRunResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "disable", "manifest", "scheduler_jobs", "disable",
                IdOrHubId: null, Payload: dryRunPayload, Context: adminContext, TriggerKind: "client", Role: "admin"));
            Assert.True(dryRunResponse.Success, string.Join(";", dryRunResponse.Errors.Select(e => e.Code + ":" + e.Message)));
            var previewText = dryRunResponse.Emission!.Data!.Value.GetRawText();
            Assert.Contains("\"activeBefore\":true", previewText);
            Assert.Contains("\"activeAfter\":false", previewText);

            Assert.True((bool)(await ScalarAsync(conn, $"SELECT active FROM topology.scheduler_jobs WHERE scheduler_job_id='{schedulerJobId}'"))!);

            // STEP 2: unconfirmed write -- fails closed, no mutation.
            var unconfirmedPayload = JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{SchedulerSettingsManifestId}:scheduler_jobs:disable",
                schedulerJobId = schedulerJobId.ToString(),
            });
            var unconfirmedResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "disable", "manifest", "scheduler_jobs", "disable",
                IdOrHubId: null, Payload: unconfirmedPayload, Context: adminContext, TriggerKind: "client", Role: "admin"));
            Assert.False(unconfirmedResponse.Success);
            Assert.Contains(unconfirmedResponse.Errors, e => e.Code == "SCHEDULER_JOB_DISABLE_NOT_CONFIRMED");
            Assert.True((bool)(await ScalarAsync(conn, $"SELECT active FROM topology.scheduler_jobs WHERE scheduler_job_id='{schedulerJobId}'"))!);

            // STEP 3: non-admin role fails closed even when confirmed.
            var nonAdminPayload = JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{SchedulerSettingsManifestId}:scheduler_jobs:disable",
                schedulerJobId = schedulerJobId.ToString(),
                confirmed = true,
            });
            var userContext = new Dictionary<string, string> { [DispatchAuthContext.AuthenticatedRolesKey] = "user" };
            var nonAdminResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "disable", "manifest", "scheduler_jobs", "disable",
                IdOrHubId: null, Payload: nonAdminPayload, Context: userContext, TriggerKind: "client", Role: "user"));
            Assert.False(nonAdminResponse.Success);
            Assert.True((bool)(await ScalarAsync(conn, $"SELECT active FROM topology.scheduler_jobs WHERE scheduler_job_id='{schedulerJobId}'"))!);

            // STEP 4: confirmed write -- persists and appends a real logs.diff row.
            var confirmedPayload = JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{SchedulerSettingsManifestId}:scheduler_jobs:disable",
                schedulerJobId = schedulerJobId.ToString(),
                confirmed = true,
            });
            var confirmedResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "disable", "manifest", "scheduler_jobs", "disable",
                IdOrHubId: null, Payload: confirmedPayload, Context: adminContext, TriggerKind: "client", Role: "admin"));
            Assert.True(confirmedResponse.Success, string.Join(";", confirmedResponse.Errors.Select(e => e.Code + ":" + e.Message)));

            Assert.False((bool)(await ScalarAsync(conn, $"SELECT active FROM topology.scheduler_jobs WHERE scheduler_job_id='{schedulerJobId}'"))!);

            var diffCount = await CountLogsDiffRowsAsync(cs, "topology.scheduler_jobs", schedulerJobId.ToString(), "update", t0);
            Assert.True(diffCount > 0, "expected a logs.diff row for the confirmed scheduler_jobs:disable write");
        }
        finally
        {
            await ExecAsync("DELETE FROM logs.diff WHERE record_id = @id", ("id", schedulerJobId.ToString()));
            await ExecAsync("DELETE FROM topology.scheduler_jobs WHERE scheduler_job_id = @id", ("id", schedulerJobId));
        }
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return await cmd.ExecuteScalarAsync();
    }

    private static async Task<int> CountLogsDiffRowsAsync(
        string cs, string physicalTableName, string recordId, string operationKind, DateTimeOffset since)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*)::int FROM logs.diff
            WHERE source_set_id = 'admin_master_roster'
              AND physical_table_name = @t
              AND record_id = @r
              AND operation_kind = @op
              AND observed_at >= @since";
        cmd.Parameters.AddWithValue("t", physicalTableName);
        cmd.Parameters.AddWithValue("r", recordId);
        cmd.Parameters.AddWithValue("op", operationKind);
        cmd.Parameters.AddWithValue("since", since);
        return (int)(await cmd.ExecuteScalarAsync() ?? 0);
    }

    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();
}
