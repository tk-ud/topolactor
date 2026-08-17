using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Endpoint;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB E2E proof for the scheduler-settings hub relation / navigation / ui_projection bundle,
/// per runtime-orchestration-ssot.yaml dispatcher_contract.ui_projection_render_reachability_contract
/// .test_proof_contract — the same combined-proof pattern
/// AdminEnumHubRelationUiProjectionLiveDbTests.cs / TeamDashboardHubRelationUiProjectionLiveDbTests.cs
/// established:
///   (1) author a hubs.hub_relations row via the REAL hub_navigation:create dispatch action targeting
///       this surface's own hub, never a raw SQL insert standing in for authoring, and walk that SAME
///       authored relation's resolved TargetManifestId onward to a scalar Emission;
///   (2) dispatch this manifest's own projection_entry and assert REAL topology.scheduler_jobs rows
///       project into Emission.Data (default_screen_read_override invoking scheduler_jobs:list_settings
///       for real), with the SSOT's forbidden_projection_fields absent from that same real payload;
///   (3) resolve the same production manifest by manifest_key instead of UUID — the exact shape
///       frontend/routes/admin/scheduler.tsx sends (ProjectionShell manifestKey);
///   (4) exercise the enable/disable mutation_confirmation_contract through the real dispatch path,
///       including the persisted logs.diff row and the non-admin fail-close.
///
/// Deliberately seeds NO hubs.hub_relations row into db/seed_empty.sql: the design_blocking criterion
/// (admin-normal-surface-projection-seed-ssot.yaml design_blocking.target_surface_manifest_readiness
/// .navigation_binding_authoring_and_verification) is satisfied by authoring AND resolving the
/// relation for real at test time, which is exactly what admin-enum, team-dashboard and
/// credential-management do — none of them ships a seeded relation row either.
///
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set. Requires db/seed_empty.sql applied
/// to the target database (manifest 5c100 + its ui_projection rows).
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class SchedulerSettingsHubRelationUiProjectionLiveDbTests
{
    private static readonly Guid SchedulerManifestId = new("00000000-0000-0000-0000-00000005c100");
    private static readonly Guid SchedulerHubId = new("00000000-0000-0000-0000-00000005c101");
    private const string SchedulerManifestKey = "scheduler.settings.projection";

    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();

    private static async Task ExecAsync(NpgsqlConnection conn, string sql, params (string Name, object Value)[] parms)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parms) cmd.Parameters.AddWithValue(name, value);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Inserts a throwaway scheduler job so enable/disable toggles never touch seeded rows.</summary>
    private static async Task<Guid> InsertTestJobAsync(NpgsqlConnection conn, string jobKey, bool active)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO topology.scheduler_jobs
                (job_key, trigger_kind, schedule_policy_kind, cron_expression, manual_run_allowed,
                 active, authority_scope, created_by, updated_at)
            VALUES (@jobKey, 'cron', 'cron', '0 * * * *', true, @active, 'live_db_scheduler_settings_test',
                    'live-db-test', now())
            RETURNING scheduler_job_id
            """;
        cmd.Parameters.AddWithValue("jobKey", jobKey);
        cmd.Parameters.AddWithValue("active", active);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static EndpointRequestDto SchedulerMutationRequest(
        string action, object payload, string? authenticatedRole = "admin")
    {
        var context = authenticatedRole is null
            ? null
            : new Dictionary<string, string> { [DispatchAuthContext.AuthenticatedRolesKey] = authenticatedRole };
        return new EndpointRequestDto(
            "SchedulerSettingsScenario", "admin", "scheduler_jobs", action,
            IdOrHubId: null,
            Payload: System.Text.Json.JsonSerializer.SerializeToElement(payload),
            Context: context, TriggerKind: "client", Role: "admin");
    }

    /// <summary>
    /// Closes admin-normal-surface-projection-seed-ssot.yaml design_blocking
    /// .target_surface_manifest_readiness.navigation_binding_authoring_and_verification
    /// (subbundle_status.scheduler-settings): authors a hub_relations row via the REAL
    /// hub_navigation:create dispatch action targeting the scheduler manifest's own hub, then walks
    /// that SAME authored relation's resolved TargetManifestId onward through manifest 5c100's real
    /// package/layout/wiring/tensor rows to a scalar Emission.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_HubNavigationCreate_AuthorsRelationTargetingSchedulerManifest_ResolutionChainReachesScalarEmission()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var sourceHubId = Guid.NewGuid();
        var sourceManifestId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        Guid? createdHubRelationId = null;
        try
        {
            await ExecAsync(conn, "INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", sourceHubId));
            await ExecAsync(
                conn,
                "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) VALUES (@mid, @hid, @key, 'active')",
                ("mid", sourceManifestId), ("hid", sourceHubId), ("key", $"live-db-scheduler-settings-source-{suffix}"));
            await ExecAsync(
                conn,
                "INSERT INTO manifest (manifest_id, relation_registry_id, topology, status) VALUES " +
                "(@mid, NULL, ARRAY['{\"type\":\"runtime_mapping\",\"runtime_destination\":\"admin_runtime\"}'::jsonb], 'active')",
                ("mid", sourceManifestId));

            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

            // STEP 1: author the relation through the REAL hub_navigation:create dispatch action.
            var createRequest = new EndpointRequestDto(
                OperationType: "HubNavigationAdminScenario",
                Target: "admin",
                Layer: "hub_navigation",
                Action: "create",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    topologyManifestId = sourceManifestId.ToString(),
                    relatedHubId = SchedulerHubId.ToString(),
                    sequencePosition = 1,
                }),
                Context: null, TriggerKind: "client", Role: "admin");
            var createResponse = await dispatcher.DispatchAsync(createRequest);
            Assert.True(createResponse.Success, string.Join(";", createResponse.Errors.Select(e => e.Code + ":" + e.Message)));

            var contentBundleRepo = new NpgsqlContentBundleRepository(
                NullLogger<NpgsqlContentBundleRepository>.Instance, cs);
            var relations = await contentBundleRepo.ListHubRelationsByManifestAsync(sourceManifestId);
            var created = Assert.Single(relations, r => r.RelatedHubId == SchedulerHubId.ToString());
            createdHubRelationId = Guid.Parse(created.HubRelationId);
            Assert.Equal(1, created.SequencePosition);
            Assert.Equal("active", created.Status);

            // STEP 2: the SAME authored relation resolves the source manifest's own
            // Emission.NavigationSequence onto the scheduler manifest itself.
            var sourceRequest = new EndpointRequestDto(
                OperationType: "HubNavigationAdminScenario",
                Target: "admin",
                Layer: "hub_navigation",
                Action: "get_hub_relations",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    target_ref = $"manifest:{sourceManifestId}:hub_relations_read",
                    topologyManifestId = sourceManifestId.ToString(),
                }),
                Context: null, TriggerKind: "client", Role: "admin");
            var sourceResponse = await dispatcher.DispatchAsync(sourceRequest);
            Assert.True(sourceResponse.Success, string.Join(";", sourceResponse.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.NotNull(sourceResponse.Emission);
            HubRelationUiProjectionResolutionChainProof.AssertNavigationSequenceResolvesHubVector(
                sourceResponse.Emission!,
                sourceManifestId,
                [new HubRelationUiProjectionResolutionChainProof.ExpectedHubVectorEntry(
                    SchedulerHubId, 1, SchedulerManifestId)]);

            // STEP 3: walk onward from that SAME resolved target to a real scalar Emission.
            var targetRequest = new EndpointRequestDto(
                "Search", "default", "screen_list", "Search",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    target_ref = $"manifest:{SchedulerManifestId}:projection_entry",
                }),
                Context: null, TriggerKind: "client", Role: "admin");
            var targetResponse = await dispatcher.DispatchAsync(targetRequest);
            Assert.True(targetResponse.Success, string.Join(";", targetResponse.Errors.Select(e => e.Code + ":" + e.Message)));

            var emission = targetResponse.Emission!;
            Assert.Equal(SchedulerManifestId.ToString(), emission.ManifestId);
            Assert.NotNull(emission.LayoutNodes);

            // seed_contract.component_tree, structurally present on the real composed tensor.
            foreach (var nodeId in new[]
            {
                "scheduler_search",
                "scheduler_filter_trigger_kind",
                "scheduler_filter_schedule_policy_kind",
                "scheduler_filter_active",
                "scheduler_job_list",
                "scheduler_enable_button",
                "scheduler_enable_confirm_modal",
                "scheduler_enable_confirm_button",
                "scheduler_enable_cancel_button",
                "scheduler_disable_button",
                "scheduler_disable_confirm_modal",
                "scheduler_disable_confirm_button",
                "scheduler_disable_cancel_button",
            })
            {
                Assert.Contains(emission.LayoutNodes!, n => n.NodeId == nodeId);
            }

            // OUT of scope for this surface: no create/edit/step-chain or credential/port-binding
            // dispatch exists anywhere on this manifest's own composed layout.
            foreach (var node in emission.LayoutNodes!.Where(n => n.DispatchTargetRefByTrigger is not null))
            {
                var raw = node.DispatchTargetRefByTrigger!.Value.GetRawText();
                Assert.DoesNotContain("scheduler_jobs:create", raw);
                Assert.DoesNotContain("scheduler_jobs:edit", raw);
                Assert.DoesNotContain("credential_management:", raw);
            }

            // mutation_confirmation_contract, both directions: preview half sends dryRun, the
            // modal's confirm half re-resolves the SAME identity source and sends confirmed.
            foreach (var (previewNode, confirmNode, action) in new[]
            {
                ("scheduler_enable_button", "scheduler_enable_confirm_button", "enable"),
                ("scheduler_disable_button", "scheduler_disable_confirm_button", "disable"),
            })
            {
                var preview = Assert.Single(emission.LayoutNodes!, n => n.NodeId == previewNode);
                Assert.Equal("admin_runtime", preview.WiringKind);
                var previewTargetRef = preview.DispatchTargetRefByTrigger!.Value.GetProperty("click").GetString();
                Assert.Equal($"manifest:{SchedulerManifestId}:scheduler_jobs:{action}", previewTargetRef);
                var previewPayload = preview.DispatchPayloadFromByTrigger!.Value.GetProperty("click");
                Assert.Equal("literal:true", previewPayload.GetProperty("dryRun").GetString());
                Assert.Equal(
                    "node:scheduler_job_list.value.schedulerJobId",
                    previewPayload.GetProperty("schedulerJobId").GetString());

                var confirm = Assert.Single(emission.LayoutNodes!, n => n.NodeId == confirmNode);
                Assert.Equal(previewTargetRef, confirm.DispatchTargetRefByTrigger!.Value.GetProperty("click").GetString());
                var confirmPayload = confirm.DispatchPayloadFromByTrigger!.Value.GetProperty("click");
                Assert.Equal("literal:true", confirmPayload.GetProperty("confirmed").GetString());
                Assert.False(confirmPayload.TryGetProperty("dryRun", out _));
                // Identity is re-resolved fresh from the table's own tracked selected row at
                // confirm-click time, never a captured value.
                Assert.Equal(
                    "node:scheduler_job_list.value.schedulerJobId",
                    confirmPayload.GetProperty("schedulerJobId").GetString());
            }

            var unresolvedLeaves = emission.LayoutNodes!
                .Where(n => n.NodeKind == "catalog_component" && n.ComponentId is null)
                .ToList();
            Assert.Empty(unresolvedLeaves);
            Assert.Empty(emission.Errors);
        }
        finally
        {
            if (createdHubRelationId is not null)
                await ExecAsync(conn, "DELETE FROM hubs.hub_relations WHERE hub_relation_id = @rid", ("rid", createdHubRelationId.Value));
            await ExecAsync(conn, "DELETE FROM manifest WHERE manifest_id = @mid", ("mid", sourceManifestId));
            await ExecAsync(conn, "DELETE FROM hubs.topology_manifests WHERE topology_manifest_id = @mid", ("mid", sourceManifestId));
            await ExecAsync(conn, "DELETE FROM hubs.hub WHERE hub_id = @hid", ("hid", sourceHubId));
        }
    }

    /// <summary>
    /// default_screen_read_override proof: the bare structural dispatch ALSO carries real
    /// topology.scheduler_jobs rows in Emission.Data (what scheduler_job_list's
    /// propBindings.rows source "emission.data.schedulerJobs" resolves from), and that same real
    /// payload contains none of the surface's forbidden_projection_fields.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_SchedulerManifest_ProjectionEntry_CarriesRealJobRowsWithoutForbiddenFields()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var jobKey = $"live_db_scheduler_settings_read_{Guid.NewGuid():N}"[..40];
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        var jobId = await InsertTestJobAsync(conn, jobKey, active: true);
        try
        {
            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
            var response = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "Search", "default", "screen_list", "Search",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    target_ref = $"manifest:{SchedulerManifestId}:projection_entry",
                }),
                Context: null, TriggerKind: "client", Role: "admin"));

            Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
            var data = response.Emission!.Data;
            Assert.NotNull(data);
            var jobs = data!.Value.GetProperty("schedulerJobs").EnumerateArray().ToList();
            var row = Assert.Single(jobs, j => j.GetProperty("jobKey").GetString() == jobKey);
            Assert.Equal(jobId.ToString(), row.GetProperty("schedulerJobId").GetString());
            Assert.True(row.GetProperty("active").GetBoolean());
            // allowed projection fields the table's own columns bind to
            Assert.Equal("cron", row.GetProperty("triggerKind").GetString());
            Assert.Equal("UTC", row.GetProperty("timezone").GetString());
            Assert.False(string.IsNullOrEmpty(row.GetProperty("updatedAt").GetString()));

            // forbidden_projection_fields: absent from the REAL payload, not just from a unit double.
            var raw = data!.Value.GetRawText();
            foreach (var forbidden in new[]
            {
                "credentialRequirementRef", "externalPortRef", "authorityScope",
                "inputTableRef", "inputStatusColumn", "outputTableRef",
                "retryPolicy", "projectionPolicy", "maxBatchSize", "leaseSeconds",
            })
            {
                Assert.DoesNotContain(forbidden, raw);
            }

            // The three filter selects' option domains arrive with the same read.
            Assert.Equal(3, data!.Value.GetProperty("triggerKindOptions").GetArrayLength());
            Assert.Equal(3, data!.Value.GetProperty("schedulePolicyKindOptions").GetArrayLength());
            Assert.Equal(2, data!.Value.GetProperty("activeOptions").GetArrayLength());
        }
        finally
        {
            await ExecAsync(conn, "DELETE FROM topology.scheduler_jobs WHERE scheduler_job_id = @id", ("id", jobId));
        }
    }

    /// <summary>
    /// Generic route→Manifest identity resolution (mirrors
    /// TeamDashboardHubRelationUiProjectionLiveDbTests
    /// .DispatchAsync_NormalManifest_ProjectionEntry_ByManifestKey_ResolvesSameProductionManifest):
    /// the SAME production manifest reached WITHOUT its UUID appearing anywhere in the request —
    /// target_ref names it only by hubs.topology_manifests.manifest_key, the exact shape
    /// frontend/routes/admin/scheduler.tsx sends (ProjectionShell manifestKey prop).
    /// </summary>
    [Fact]
    public async Task DispatchAsync_SchedulerManifest_ProjectionEntry_ByManifestKey_ResolvesSameProductionManifest()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var response = await dispatcher.DispatchAsync(new EndpointRequestDto(
            "Search", "default", "screen_list", "Search",
            IdOrHubId: null,
            Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest_key:{SchedulerManifestKey}:projection_entry",
            }),
            Context: null, TriggerKind: "client", Role: "admin"));

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
        var emission = response.Emission!;
        Assert.Equal(SchedulerManifestId.ToString(), emission.ManifestId);
        Assert.Contains(emission.LayoutNodes!, n => n.NodeId == "scheduler_job_list");
        Assert.NotNull(emission.Data);
        Assert.True(emission.Data!.Value.TryGetProperty("schedulerJobs", out _));
    }

    /// <summary>
    /// The same manifest's structural read as a non-admin role stays fail-closed: this surface
    /// declares no scoped-open capability_requirement (unlike dd020's Normal read), so
    /// runtime_mapping.runtime_destination=admin_runtime's inferred admin requirement applies.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_SchedulerManifest_ProjectionEntry_AsNormalRole_FailsClosedWithAuthCapabilityDenied()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var response = await dispatcher.DispatchAsync(new EndpointRequestDto(
            "Search", "default", "screen_list", "Search",
            IdOrHubId: null,
            Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{SchedulerManifestId}:projection_entry",
            }),
            Context: null, TriggerKind: "client", Role: "normal"));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_CAPABILITY_DENIED");
    }

    /// <summary>
    /// mutation_confirmation_contract end to end against a real row, through the real dispatch path
    /// the seeded confirm buttons declare: dryRun previews without writing, an unconfirmed write
    /// fails closed, and only the confirmed dispatch flips topology.scheduler_jobs.active AND
    /// persists the logs.diff envelope. Enable and disable are both exercised (symmetry).
    /// </summary>
    [Fact]
    public async Task DispatchAsync_EnableThenDisable_ObeysConfirmationContractAndPersistsDiffLog()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var jobKey = $"live_db_scheduler_settings_toggle_{Guid.NewGuid():N}"[..40];
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        var jobId = await InsertTestJobAsync(conn, jobKey, active: false);

        async Task<bool> ReadActiveAsync()
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT active FROM topology.scheduler_jobs WHERE scheduler_job_id = @id";
            cmd.Parameters.AddWithValue("id", jobId);
            return (bool)(await cmd.ExecuteScalarAsync())!;
        }

        async Task<int> CountDiffRowsAsync()
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT count(*) FROM logs.diff WHERE physical_table_name = 'topology.scheduler_jobs' AND record_id = @rid";
            cmd.Parameters.AddWithValue("rid", jobId.ToString());
            return (int)(long)(await cmd.ExecuteScalarAsync())!;
        }

        try
        {
            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
            var enableTargetRef = $"manifest:{SchedulerManifestId}:scheduler_jobs:enable";
            var disableTargetRef = $"manifest:{SchedulerManifestId}:scheduler_jobs:disable";

            // dryRun preview: valid, non-mutating, no diff row.
            var preview = await dispatcher.DispatchAsync(SchedulerMutationRequest("enable", new
            {
                target_ref = enableTargetRef,
                schedulerJobId = jobId.ToString(),
                dryRun = true,
            }));
            Assert.True(preview.Success, string.Join(";", preview.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.True(preview.Emission!.Data!.Value.GetProperty("dryRun").GetBoolean());
            Assert.False(await ReadActiveAsync());
            Assert.Equal(0, await CountDiffRowsAsync());

            // Neither dryRun nor confirmed: fail closed, still no write.
            var unconfirmed = await dispatcher.DispatchAsync(SchedulerMutationRequest("enable", new
            {
                target_ref = enableTargetRef,
                schedulerJobId = jobId.ToString(),
            }));
            Assert.False(unconfirmed.Success);
            Assert.Contains(unconfirmed.Errors, e => e.Code == "SCHEDULER_JOB_ENABLE_NOT_CONFIRMED");
            Assert.False(await ReadActiveAsync());
            Assert.Equal(0, await CountDiffRowsAsync());

            // Non-admin authenticated role: fail closed even with confirmed=true.
            var denied = await dispatcher.DispatchAsync(SchedulerMutationRequest("enable", new
            {
                target_ref = enableTargetRef,
                schedulerJobId = jobId.ToString(),
                confirmed = true,
            }, authenticatedRole: "normal"));
            Assert.False(denied.Success);
            Assert.Contains(denied.Errors, e => e.Code == "AUTH_CAPABILITY_DENIED");
            Assert.False(await ReadActiveAsync());
            Assert.Equal(0, await CountDiffRowsAsync());

            // Confirmed enable: real write + real diff_log row.
            var enabled = await dispatcher.DispatchAsync(SchedulerMutationRequest("enable", new
            {
                target_ref = enableTargetRef,
                schedulerJobId = jobId.ToString(),
                confirmed = true,
            }));
            Assert.True(enabled.Success, string.Join(";", enabled.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.True(enabled.Emission!.Data!.Value.GetProperty("active").GetBoolean());
            Assert.True(await ReadActiveAsync());
            Assert.Equal(1, await CountDiffRowsAsync());

            // Enabling an already-active job fails closed rather than writing a no-op.
            var alreadyActive = await dispatcher.DispatchAsync(SchedulerMutationRequest("enable", new
            {
                target_ref = enableTargetRef,
                schedulerJobId = jobId.ToString(),
                confirmed = true,
            }));
            Assert.False(alreadyActive.Success);
            Assert.Contains(alreadyActive.Errors, e => e.Code == "SCHEDULER_JOB_ALREADY_ACTIVE");
            Assert.Equal(1, await CountDiffRowsAsync());

            // Symmetric confirmed disable: flips back, appends its own diff row.
            var disabled = await dispatcher.DispatchAsync(SchedulerMutationRequest("disable", new
            {
                target_ref = disableTargetRef,
                schedulerJobId = jobId.ToString(),
                confirmed = true,
            }));
            Assert.True(disabled.Success, string.Join(";", disabled.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.False(disabled.Emission!.Data!.Value.GetProperty("active").GetBoolean());
            Assert.False(await ReadActiveAsync());
            Assert.Equal(2, await CountDiffRowsAsync());

            await using var diffCmd = conn.CreateCommand();
            diffCmd.CommandText = """
                SELECT before_state_or_diff_json::text, after_state_or_diff_json::text, changed_fields_json::text
                FROM logs.diff
                WHERE physical_table_name = 'topology.scheduler_jobs' AND record_id = @rid
                ORDER BY observed_at DESC
                LIMIT 1
                """;
            diffCmd.Parameters.AddWithValue("rid", jobId.ToString());
            await using var reader = await diffCmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Contains("true", reader.GetString(0));
            Assert.Contains("false", reader.GetString(1));
            Assert.Contains("active", reader.GetString(2));
        }
        finally
        {
            await ExecAsync(conn, "DELETE FROM logs.diff WHERE physical_table_name = 'topology.scheduler_jobs' AND record_id = @rid",
                ("rid", jobId.ToString()));
            await ExecAsync(conn, "DELETE FROM topology.scheduler_jobs WHERE scheduler_job_id = @id", ("id", jobId));
        }
    }

    /// <summary>
    /// search/filter reach the real query, not just the runtime: dispatching this manifest's own
    /// scheduler_jobs:list_settings with payload.search / payload.triggerKind narrows real
    /// topology.scheduler_jobs rows, and an invalid filter value fails closed.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_ListSettings_SearchAndFilterNarrowRealRows_InvalidFilterFailsClosed()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var marker = Guid.NewGuid().ToString("N")[..8];
        var jobKey = $"live_db_sched_filter_{marker}";
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        var jobId = await InsertTestJobAsync(conn, jobKey, active: true);
        try
        {
            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
            var targetRef = $"manifest:{SchedulerManifestId}:scheduler_jobs:list_settings";

            var searched = await dispatcher.DispatchAsync(SchedulerMutationRequest("list_settings", new
            {
                target_ref = targetRef,
                search = marker,
            }));
            Assert.True(searched.Success, string.Join(";", searched.Errors.Select(e => e.Code + ":" + e.Message)));
            var searchedRow = Assert.Single(searched.Emission!.Data!.Value.GetProperty("schedulerJobs").EnumerateArray());
            Assert.Equal(jobKey, searchedRow.GetProperty("jobKey").GetString());

            // Filtering the same search on the row's OWN trigger kind keeps it; the other kind drops it.
            var keptByFilter = await dispatcher.DispatchAsync(SchedulerMutationRequest("list_settings", new
            {
                target_ref = targetRef,
                search = marker,
                triggerKind = "cron",
                active = "true",
            }));
            Assert.True(keptByFilter.Success, string.Join(";", keptByFilter.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.Equal(1, keptByFilter.Emission!.Data!.Value.GetProperty("schedulerJobs").GetArrayLength());

            var droppedByFilter = await dispatcher.DispatchAsync(SchedulerMutationRequest("list_settings", new
            {
                target_ref = targetRef,
                search = marker,
                triggerKind = "hook",
            }));
            Assert.True(droppedByFilter.Success, string.Join(";", droppedByFilter.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.Equal(0, droppedByFilter.Emission!.Data!.Value.GetProperty("schedulerJobs").GetArrayLength());

            var invalid = await dispatcher.DispatchAsync(SchedulerMutationRequest("list_settings", new
            {
                target_ref = targetRef,
                triggerKind = "not_a_trigger_kind",
            }));
            Assert.False(invalid.Success);
            Assert.Contains(invalid.Errors, e => e.Code == "SCHEDULER_JOB_TRIGGER_KIND_INVALID");
        }
        finally
        {
            await ExecAsync(conn, "DELETE FROM topology.scheduler_jobs WHERE scheduler_job_id = @id", ("id", jobId));
        }
    }
}
