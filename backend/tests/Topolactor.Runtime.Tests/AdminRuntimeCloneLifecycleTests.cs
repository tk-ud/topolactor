using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Bundle admin-topology-clone-draft-lifecycle backend authority tests.
/// SSOT: docs/design/admin-console-workflow-ssot.yaml admin_contents_step1_entry_modes /
///       replacement_clone_merge_lifecycle.
/// </summary>
public class AdminRuntimeCloneLifecycleTests
{
    private static readonly HashSet<string> KnownDestinations = new(StringComparer.OrdinalIgnoreCase)
    {
        "topology_transform_runtime",
        "admin_runtime",
        "sse_projection_runtime",
    };

    // ── Step 1 entry-mode draft creation ─────────────────────────────────────

    [Fact]
    public async Task CreateNewTopologyDraft_StampsManualNewCloneOff()
    {
        var repo = new InMemoryManifestAdminRepository();
        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            role = "admin", target = "tgt", layer = "screen_list", action = "Read",
            runtimeDestination = "topology_transform_runtime",
        });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "create_new_topology_draft", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        var detail = repo.Manifests.Single();
        var meta = CloneDraftMetadata.Extract(detail.Topology);
        Assert.NotNull(meta);
        Assert.Equal(CloneDraftMetadata.OriginManualNew, meta!.DraftOrigin);
        Assert.Equal(CloneDraftMetadata.CloneModeNone, meta.CloneMode);
        Assert.Null(meta.SourceActiveManifestId); // clone_off keeps source absent
    }

    [Fact]
    public async Task CreateCloneReplacementDraft_StampsReplacementOriginAndSourceEvidence()
    {
        var repo = new InMemoryManifestAdminRepository();
        var sourceId = SeedActive(repo, "admin", "tgt", "screen_list", "Read");
        var runtime = CreateRuntime(repo);

        var payload = JsonSerializer.SerializeToElement(new { sourceActiveManifestId = sourceId.ToString() });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "create_clone_replacement_draft_from_active", null, "admin", payload, null), default);

        Assert.Null(error);
        var draft = repo.Manifests.Single(m => m.Status == "draft");
        var meta = CloneDraftMetadata.Extract(draft.Topology);
        Assert.Equal(CloneDraftMetadata.OriginManualCloneReplacement, meta!.DraftOrigin);
        Assert.Equal(CloneDraftMetadata.CloneModeReplacement, meta.CloneMode);
        Assert.Equal(sourceId, meta.SourceActiveManifestId);
        Assert.NotNull(meta.SourceEvidence);
        Assert.Equal(sourceId, meta.SourceEvidence!.SourceActiveManifestId);
    }

    [Fact]
    public async Task CreateCloneNewTopologyDraft_IsLineageOnlyWithNewIdentity()
    {
        var repo = new InMemoryManifestAdminRepository();
        var sourceId = SeedActive(repo, "admin", "tgt", "screen_list", "Read", topologySystemName: "source-topo");
        var runtime = CreateRuntime(repo);

        var payload = JsonSerializer.SerializeToElement(new
        {
            sourceActiveManifestId = sourceId.ToString(),
            newTopologySystemName = "cloned-topo",
            userFacingTopologyLabel = "クローン",
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "create_clone_new_topology_draft_from_active", null, "admin", payload, null), default);

        Assert.Null(error);
        var draft = repo.Manifests.Single(m => m.Status == "draft");
        var meta = CloneDraftMetadata.Extract(draft.Topology);
        Assert.Equal(CloneDraftMetadata.OriginManualCloneNewTopology, meta!.DraftOrigin);
        Assert.Equal(CloneDraftMetadata.CloneModeNewTopology, meta.CloneMode);
        Assert.Equal(sourceId, meta.SourceActiveManifestId); // lineage only
        Assert.Equal("cloned-topo", CloneDraftMetadata.ExtractTopologySystemName(draft.Topology));
    }

    [Fact]
    public async Task CreateCloneNewTopologyDraft_WithoutNewName_FailsClose()
    {
        var repo = new InMemoryManifestAdminRepository();
        var sourceId = SeedActive(repo, "admin", "tgt", "screen_list", "Read");
        var runtime = CreateRuntime(repo);

        var payload = JsonSerializer.SerializeToElement(new { sourceActiveManifestId = sourceId.ToString() });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "create_clone_new_topology_draft_from_active", null, "admin", payload, null), default);

        Assert.NotNull(error);
        Assert.Equal("TOPOLOGY_SYSTEM_NAME_REQUIRED", error!.Code);
    }

    // ── Replacement merge backend authority ──────────────────────────────────

    [Fact]
    public async Task MergeReplacementDraft_UpdatesActiveAndDeletesDraft_WithDiffEvidence()
    {
        var repo = new InMemoryManifestAdminRepository();
        var sourceId = SeedActive(repo, "admin", "tgt", "screen_list", "Read");
        var draftId = SeedReplacementDraft(repo, sourceId, changedRuntime: "admin_runtime");
        var runtime = CreateRuntime(repo);

        var payload = JsonSerializer.SerializeToElement(new { manifestId = draftId.ToString() });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "merge_clone_replacement_draft_to_active", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Equal(sourceId.ToString(), data.Value.GetProperty("activeManifestId").GetString());

        // existing active row updated in place; working draft deleted.
        Assert.DoesNotContain(repo.Manifests, m => m.ManifestId == draftId);
        var active = repo.Manifests.Single(m => m.ManifestId == sourceId);
        Assert.Equal("active", active.Status);
        // merged active topology carries no clone_metadata bookkeeping.
        Assert.Null(CloneDraftMetadata.Extract(active.Topology));
        // diff/log evidence preserved in edit-log surface (not the draft row).
        Assert.Single(repo.MergeEditLog);
        Assert.Equal("merge_clone_replacement_draft_to_active", repo.MergeEditLog[0].Operation);
    }

    [Fact]
    public async Task MergeReplacementDraft_StaleSource_FailsClose()
    {
        var repo = new InMemoryManifestAdminRepository();
        var sourceId = SeedActive(repo, "admin", "tgt", "screen_list", "Read");
        var draftId = SeedReplacementDraft(repo, sourceId, changedRuntime: "admin_runtime");

        // Source active mutates after the clone captured its evidence hash.
        var idx = repo.Manifests.ToList().FindIndex(m => m.ManifestId == sourceId);
        var src = repo.Manifests[idx];
        await repo.UpdateActiveTopologyForTest(sourceId, MutateRuntime(src.Topology, "sse_projection_runtime"));

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { manifestId = draftId.ToString() });
        var (data, _) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "merge_clone_replacement_draft_to_active", null, "admin", payload, null), default);

        Assert.False(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("STALE_SOURCE_ACTIVE", data.Value.GetProperty("errorCode").GetString());
        Assert.Contains(repo.Manifests, m => m.ManifestId == draftId); // draft preserved on failure
    }

    [Fact]
    public async Task MergeReplacementDraft_ActiveIdentityConflict_FailsClose()
    {
        var repo = new InMemoryManifestAdminRepository();
        var sourceId = SeedActive(repo, "admin", "tgt", "screen_list", "Read");
        var draftId = SeedReplacementDraft(repo, sourceId, changedRuntime: "admin_runtime");
        // A second active row shares the merge-target dispatcher axes.
        SeedActive(repo, "admin", "tgt", "screen_list", "Read");

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { manifestId = draftId.ToString() });
        var (data, _) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "merge_clone_replacement_draft_to_active", null, "admin", payload, null), default);

        Assert.False(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("ACTIVE_IDENTITY_CONFLICT", data.Value.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task MergeReplacementDraft_NoDiff_FailsClose()
    {
        var repo = new InMemoryManifestAdminRepository();
        var sourceId = SeedActive(repo, "admin", "tgt", "screen_list", "Read");
        // Draft identical to source (no changedRuntime) → no diff evidence.
        var draftId = SeedReplacementDraft(repo, sourceId, changedRuntime: null);

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { manifestId = draftId.ToString() });
        var (data, _) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "merge_clone_replacement_draft_to_active", null, "admin", payload, null), default);

        Assert.False(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("REPLACEMENT_MERGE_NO_DIFF", data.Value.GetProperty("errorCode").GetString());
    }

    // ── SQL Attention candidate / lineage authority regression guards ─────────

    [Fact]
    public async Task SqlAttentionCandidateDraft_CannotMergeToActive_EvenWithSourceEvidence()
    {
        var repo = new InMemoryManifestAdminRepository();
        var sourceId = SeedActive(repo, "admin", "tgt", "screen_list", "Read");
        // A draft carrying full source evidence but a candidate origin (not manual replacement).
        var draftId = SeedCloneDraft(
            repo, sourceId,
            origin: CloneDraftMetadata.OriginSqlAttentionCandidate,
            mode: CloneDraftMetadata.CloneModeReplacement,
            changedRuntime: "admin_runtime");

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { manifestId = draftId.ToString() });
        var (data, _) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "merge_clone_replacement_draft_to_active", null, "admin", payload, null), default);

        Assert.False(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("REPLACEMENT_AUTHORITY_DENIED", data.Value.GetProperty("errorCode").GetString());
        Assert.Empty(repo.MergeEditLog);
        Assert.Contains(repo.Manifests, m => m.ManifestId == draftId);
    }

    [Fact]
    public async Task CloneNewTopologyDraft_CannotMergeToActive()
    {
        var repo = new InMemoryManifestAdminRepository();
        var sourceId = SeedActive(repo, "admin", "tgt", "screen_list", "Read");
        var draftId = SeedCloneDraft(
            repo, sourceId,
            origin: CloneDraftMetadata.OriginManualCloneNewTopology,
            mode: CloneDraftMetadata.CloneModeNewTopology,
            changedRuntime: "admin_runtime");

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { manifestId = draftId.ToString() });
        var (data, _) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "merge_clone_replacement_draft_to_active", null, "admin", payload, null), default);

        Assert.False(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("REPLACEMENT_AUTHORITY_DENIED", data.Value.GetProperty("errorCode").GetString());
    }

    // ── validate readiness (read-only; frontend display only) ────────────────

    [Fact]
    public async Task ValidateReplacementDraft_ReportsStaleBlocker()
    {
        var repo = new InMemoryManifestAdminRepository();
        var sourceId = SeedActive(repo, "admin", "tgt", "screen_list", "Read");
        var draftId = SeedReplacementDraft(repo, sourceId, changedRuntime: "admin_runtime");
        await repo.UpdateActiveTopologyForTest(sourceId, MutateRuntime(
            repo.Manifests.Single(m => m.ManifestId == sourceId).Topology, "sse_projection_runtime"));

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { manifestId = draftId.ToString() });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "validate_clone_replacement_draft", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data!.Value.GetProperty("sourceStale").GetBoolean());
        Assert.False(data.Value.GetProperty("mergeReady").GetBoolean());
        Assert.Contains(
            data.Value.GetProperty("mergeBlockers").EnumerateArray(),
            b => b.GetProperty("code").GetString() == "STALE_SOURCE_ACTIVE");
    }

    [Fact]
    public async Task LoadCloneSourceEvidence_NonActive_FailsClose()
    {
        var repo = new InMemoryManifestAdminRepository();
        var draftId = Guid.NewGuid();
        repo.Seed(new ManifestDetailRecord(draftId, null,
            ValidTopology("admin", "tgt", "screen_list", "Read", "topology_transform_runtime"),
            "draft", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var runtime = CreateRuntime(repo);

        var payload = JsonSerializer.SerializeToElement(new { sourceActiveManifestId = draftId.ToString() });
        var (_, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "load_clone_source_evidence", null, "admin", payload, null), default);

        Assert.NotNull(error);
        Assert.Equal("CLONE_SOURCE_NOT_ACTIVE", error!.Code);
    }

    // ── transaction-shape regression guard ──────────────────────────────────
    // PR#534 review: the working draft row must be locked FOR UPDATE INSIDE the merge
    // transaction (not read out-of-transaction), so a concurrent draft mutation cannot drive
    // active UPDATE + draft DELETE from a stale snapshot.

    [Fact]
    public void MergeCloneReplacement_LocksDraftInsideTransaction_NoOutOfTxDraftRead()
    {
        var method = ExtractMergeMethodSource();

        // The merge must NOT read the working draft out-of-transaction.
        Assert.DoesNotContain("LoadDetailByIdAsync(draftManifestId", method);

        // The transaction must begin before any row is locked.
        var txIdx = method.IndexOf("BeginTransactionAsync", StringComparison.Ordinal);
        var firstLockIdx = method.IndexOf("FOR UPDATE", StringComparison.Ordinal);
        Assert.True(txIdx >= 0, "merge must open a transaction");
        Assert.True(firstLockIdx > txIdx, "the first FOR UPDATE lock must occur after BeginTransactionAsync");

        // The draft row is locked (its id parameter precedes the source id parameter — draft-first
        // deterministic lock ordering).
        var draftParamIdx = method.IndexOf("AddWithValue(\"id\", draftManifestId)", StringComparison.Ordinal);
        var sourceParamIdx = method.IndexOf("AddWithValue(\"id\", sourceId)", StringComparison.Ordinal);
        Assert.True(draftParamIdx > 0, "draft row must be locked by id inside the transaction");
        Assert.True(sourceParamIdx > draftParamIdx, "draft must be locked before the source active row");
    }

    private static string ExtractMergeMethodSource()
    {
        var src = ReadRepositorySource("NpgsqlManifestRepository.cs");
        var start = src.IndexOf("MergeCloneReplacementDraftToActiveAsync(\n", StringComparison.Ordinal);
        if (start < 0) start = src.IndexOf("MergeCloneReplacementDraftToActiveAsync(", StringComparison.Ordinal);
        Assert.True(start > 0, "merge method must exist in NpgsqlManifestRepository.cs");
        var end = src.IndexOf("private static CloneReplacementMergeResult MergeFail", start, StringComparison.Ordinal);
        Assert.True(end > start, "merge method end marker must exist");
        return src.Substring(start, end - start);
    }

    private static string ReadRepositorySource(
        string fileName,
        [System.Runtime.CompilerServices.CallerFilePath] string callerPath = "")
    {
        var testDir = System.IO.Path.GetDirectoryName(callerPath)!;
        var path = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(testDir, "..", "..", "repository", fileName));
        return System.IO.File.ReadAllText(path);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static Guid SeedActive(
        InMemoryManifestAdminRepository repo,
        string role, string target, string layer, string action,
        string? topologySystemName = null)
    {
        var id = Guid.NewGuid();
        var topology = ValidTopology(role, target, layer, action, "topology_transform_runtime").ToList();
        if (topologySystemName is not null)
        {
            topology.Add(JsonSerializer.SerializeToElement(new
            {
                type = "screen_data_shape",
                topologySystemName,
            }));
        }
        repo.Seed(new ManifestDetailRecord(id, null, topology, "active",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        return id;
    }

    private static Guid SeedReplacementDraft(
        InMemoryManifestAdminRepository repo, Guid sourceId, string? changedRuntime) =>
        SeedCloneDraft(repo, sourceId,
            CloneDraftMetadata.OriginManualCloneReplacement, CloneDraftMetadata.CloneModeReplacement, changedRuntime);

    private static Guid SeedCloneDraft(
        InMemoryManifestAdminRepository repo, Guid sourceId, string origin, string mode, string? changedRuntime)
    {
        var source = repo.Manifests.Single(m => m.ManifestId == sourceId);
        var evidence = new CloneSourceEvidence(
            sourceId,
            CloneDraftMetadata.ExtractTopologySystemName(source.Topology),
            null,
            ManifestTopologyValidator.ExtractSummary(source.Topology).DispatcherMapping,
            source.UpdatedAt,
            CloneDraftMetadata.ComputeTopologyHash(source.Topology),
            "active");

        var topology = (changedRuntime is null ? source.Topology : MutateRuntime(source.Topology, changedRuntime)).ToList();
        topology.Add(CloneDraftMetadata.BuildEntry(origin, mode, sourceId, evidence));

        var draftId = Guid.NewGuid();
        repo.Seed(new ManifestDetailRecord(draftId, null, topology, "draft",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        return draftId;
    }

    private static IReadOnlyList<JsonElement> MutateRuntime(IReadOnlyList<JsonElement> topology, string runtimeDestination)
    {
        var list = new List<JsonElement>();
        foreach (var entry in topology)
        {
            if (entry.ValueKind == JsonValueKind.Object &&
                entry.TryGetProperty("type", out var t) && t.GetString() == "runtime_mapping")
            {
                list.Add(JsonSerializer.SerializeToElement(new { type = "runtime_mapping", runtime_destination = runtimeDestination }));
            }
            else
            {
                list.Add(entry);
            }
        }
        return list;
    }

    private static IReadOnlyList<JsonElement> ValidTopology(
        string role, string target, string layer, string action, string runtimeDestination) =>
        ManifestTopologyValidator.BuildTopology(role, target, layer, action, runtimeDestination, null);

    private static AdminRuntime CreateRuntime(InMemoryManifestAdminRepository manifestRepo)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "Host=localhost");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(
            NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            ctxRepo, registrar, pkg, uiRepo,
            null, null, null, null, null,
            manifestRepo, null, topoRepo, null);
    }
}
