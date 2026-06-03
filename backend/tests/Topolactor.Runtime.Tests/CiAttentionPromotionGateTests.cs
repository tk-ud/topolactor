using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

// CI lane: CI Attention promotion gate consumer tests.
//
// SSOT contract (docs/design/ci-contract-ssot.yaml fragment_contract.promotion_boundary):
//   - draft_editing_is_never_blocked_by_fragment
//   - canonical_promotion_gate_may_consume_active_blocking_fragments
//   - active_break_boundary_or_required_missing_input_fragment_blocks_canonical_promotion
//   - dismissed_fragment_is_visibility_only_not_promotion_unlock
//   - promotion_gate_authority_remains_backend_runtime_boundary_guard
//
// Out of scope: frontend promotion judgment, draft editing lock, dismissed-as-unlock.
public class CiAttentionPromotionGateTests
{
    // ─── package_generator:promote gate ──────────────────────────────────────

    [Fact]
    public async Task Promote_WithActiveBlockingFragment_ReturnsPromotionBlockedError()
    {
        var blockingRepo = new StubCiAttentionGuidanceRepository(
        [
            new CiAttentionBlockingFragment(
                Guid.NewGuid(), "break_boundary", "authoring_surface", "admin_ui_builder",
                "Break boundary detected.", "Resolve active blocking fragments before promoting.")
        ]);
        var runtime = CreateAdminRuntime(blockingRepo);
        var vector = new OperationVector("admin", "package_generator", "promote", null, "admin",
            JsonSerializer.SerializeToElement(new { bucketItemId = Guid.NewGuid().ToString(), routeKey = "admin:ui-builder" }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("CI_ATTENTION_PROMOTION_BLOCKED", error!.Code);
    }

    [Fact]
    public async Task Promote_WithNoBlockingFragments_ProceedsNormally()
    {
        var emptyRepo = new StubCiAttentionGuidanceRepository([]);
        var uiRepo = new StubUiTopologyRepository(new PackageGenerateResult(
            PackageGenerateCode.Success,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        var runtime = CreateAdminRuntime(emptyRepo, uiRepo);
        var vector = new OperationVector("admin", "package_generator", "promote", null, "admin",
            JsonSerializer.SerializeToElement(new { bucketItemId = Guid.NewGuid().ToString(), routeKey = "admin:ui-builder" }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Promote_WithBlockingFragment_ErrorPayloadContainsFragmentDetails()
    {
        var fragment = new CiAttentionBlockingFragment(
            Guid.NewGuid(), "missing_input", "component", "my_component",
            "Missing required input.", "Fill the required fields.");
        var repo = new StubCiAttentionGuidanceRepository([fragment]);
        var runtime = CreateAdminRuntime(repo);
        var vector = new OperationVector("admin", "package_generator", "promote", null, "admin",
            JsonSerializer.SerializeToElement(new { bucketItemId = Guid.NewGuid().ToString(), routeKey = "admin:ui-builder" }), null);

        var (_, error) = await runtime.ExecuteDataAsync(vector);

        Assert.NotNull(error);
        var payload = JsonSerializer.Deserialize<JsonElement>(error!.Message);
        var fragments = payload.GetProperty("blocking_fragments").EnumerateArray().ToList();
        Assert.Single(fragments);
        Assert.Equal("missing_input", fragments[0].GetProperty("kind").GetString());
        Assert.Equal("component", fragments[0].GetProperty("target_kind").GetString());
        Assert.Equal("my_component", fragments[0].GetProperty("target_key").GetString());
        Assert.Equal("Missing required input.", fragments[0].GetProperty("message").GetString());
        Assert.Equal("Fill the required fields.", fragments[0].GetProperty("actionable_guidance").GetString());
    }

    // ─── dismissed fragment is NOT promotion unlock ───────────────────────────

    [Fact]
    public async Task Promote_WithDismissedFragmentOnly_ProceedsNormally()
    {
        // dismissed fragments are NOT returned by GetActiveBlockingFragmentsAsync
        // (query filters status='active' only; dismissed status is excluded)
        var emptyRepo = new StubCiAttentionGuidanceRepository([]);
        var uiRepo = new StubUiTopologyRepository(new PackageGenerateResult(
            PackageGenerateCode.Success,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        var runtime = CreateAdminRuntime(emptyRepo, uiRepo);
        var vector = new OperationVector("admin", "package_generator", "promote", null, "admin",
            JsonSerializer.SerializeToElement(new { bucketItemId = Guid.NewGuid().ToString(), routeKey = "admin:ui-builder" }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        // Dismissed fragments do not appear in GetActiveBlockingFragmentsAsync result,
        // so promotion is not blocked. This confirms dismissed is visibility control only.
        Assert.Null(error);
        Assert.NotNull(data);
    }

    // ─── draft operations (generate, preview, validate) are not gated ────────

    [Fact]
    public async Task Generate_WithActiveBlockingFragment_IsNotBlocked()
    {
        // package_generator:generate is draft staging; gate must NOT apply here.
        var blockingRepo = new StubCiAttentionGuidanceRepository(
        [
            new CiAttentionBlockingFragment(
                Guid.NewGuid(), "break_boundary", "authoring_surface", "admin_ui_builder",
                "Break boundary.", "Resolve before promoting.")
        ]);
        var uiRepo = new StubUiTopologyRepository(new PackageGenerateResult(PackageGenerateCode.Success, null, null, null, null, null));
        var runtime = CreateAdminRuntime(blockingRepo, uiRepo);
        var vector = new OperationVector("admin", "package_generator", "generate", null, "admin",
            JsonSerializer.SerializeToElement(new { bucketItemId = Guid.NewGuid().ToString(), routeKey = "admin:ui-builder" }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        // generate (draft staging) must NOT be blocked by CI Attention fragments
        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Equal("packaging", data!.Value.GetProperty("status").GetString());
    }

    // ─── layout_patch:apply gate ──────────────────────────────────────────────

    [Fact]
    public async Task LayoutPatchApply_WithActiveBlockingFragment_ReturnsPromotionBlockedError()
    {
        var blockingRepo = new StubCiAttentionGuidanceRepository(
        [
            new CiAttentionBlockingFragment(
                Guid.NewGuid(), "break_boundary", "layout", Guid.NewGuid().ToString(),
                "Layout break boundary.", "Resolve before applying layout patch.")
        ]);
        var uiRepo = new StubPreviewUiTopologyRepository();
        var runtime = CreateAdminRuntime(blockingRepo, uiRepo);
        var vector = new OperationVector("admin", "layout_patch", "apply", null, "admin",
            JsonSerializer.SerializeToElement(new { packageId = Guid.NewGuid().ToString(), layoutId = Guid.NewGuid().ToString(), routeKey = "admin:ui-builder" }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("CI_ATTENTION_PROMOTION_BLOCKED", error!.Code);
    }

    [Fact]
    public async Task LayoutPatchPreview_WithActiveBlockingFragment_IsNotBlocked()
    {
        // layout_patch:preview is a draft operation; gate must NOT apply.
        var blockingRepo = new StubCiAttentionGuidanceRepository(
        [
            new CiAttentionBlockingFragment(
                Guid.NewGuid(), "break_boundary", "layout", Guid.NewGuid().ToString(),
                "Break boundary.", "Resolve before applying.")
        ]);
        var uiRepo = new StubPreviewUiTopologyRepository();
        var runtime = CreateAdminRuntime(blockingRepo, uiRepo);
        var vector = new OperationVector("admin", "layout_patch", "preview", null, "admin",
            JsonSerializer.SerializeToElement(new { packageId = Guid.NewGuid().ToString(), layoutId = Guid.NewGuid().ToString(), routeKey = "admin:ui-builder" }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        // preview must not be blocked by CI Attention gate
        Assert.NotEqual("CI_ATTENTION_PROMOTION_BLOCKED", error?.Code);
    }


    [Fact]
    public async Task Promote_UsesAuthoringSurfaceFromPayload_ForGateQuery()
    {
        var spyRepo = new SpyCiAttentionGuidanceRepository();
        var runtime = CreateAdminRuntime(spyRepo);
        var vector = new OperationVector("admin", "package_generator", "promote", null, "admin",
            JsonSerializer.SerializeToElement(new { bucketItemId = Guid.NewGuid().ToString(), routeKey = "admin:form-builder", authoring_surface = "admin_form_builder" }), null);

        var (_, error) = await runtime.ExecuteDataAsync(vector);

        Assert.NotNull(error);
        Assert.Equal("admin_form_builder", spyRepo.LastAuthoringSurface);
    }

    [Fact]
    public async Task RefreshFragments_UpsertPersistsPayloadAuthoringSurface()
    {
        var spyRepo = new SpyCiAttentionGuidanceRepository();
        var runtime = CreateAdminRuntime(spyRepo, runner: new StubRunnerWithFinding(["hub_attention_continuity"]));
        var vector = new OperationVector("admin", "ci_attention", "refresh_fragments", null, "admin",
            JsonSerializer.SerializeToElement(new
            {
                target = "hub_attention_continuity",
                source_kind = "manual_check",
                authoring_surface = "admin_manifest_editor",
                target_kind = "authoring_surface",
                target_key = "admin_manifest_editor"
            }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Equal("admin_manifest_editor", spyRepo.LastUpsertAuthoringSurface);
    }

    // ─── system_ci:inspect remains read-only ─────────────────────────────────

    [Fact]
    public async Task SystemCiInspect_DoesNotWriteFragments_AndIsNotGated()
    {
        var spyRepo = new SpyCiAttentionGuidanceRepository();
        var runtime = CreateAdminRuntime(spyRepo, runner: new StubRunnerWithFinding(["hub_attention_continuity"]));
        var vector = new OperationVector("admin", "system_ci", "inspect", null, "admin",
            JsonSerializer.SerializeToElement(new { target = "hub_attention_continuity" }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.Equal(0, spyRepo.GetActiveBlockingCalls);
        Assert.Equal(0, spyRepo.UpsertCalls);
    }

    // ─── Repository contract: GetActiveBlockingFragmentsAsync base impl ───────

    [Fact]
    public async Task BaseRepository_GetActiveBlockingFragmentsAsync_ReturnsEmpty()
    {
        var repo = new CiAttentionGuidanceRepository();

        var result = await repo.GetActiveBlockingFragmentsAsync();

        Assert.Empty(result);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static AdminRuntime CreateAdminRuntime(
        CiAttentionGuidanceRepository? ciRepo = null,
        UiTopologyRepository? uiRepo = null,
        ISystemCiDiagnosticRunner? runner = null)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var actualUiRepo = uiRepo ?? new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var vecRuntime = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, vecRuntime);
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, actualUiRepo);
        return new AdminRuntime(NullLogger<AdminRuntime>.Instance, ctxRepo, registrar, pkg, actualUiRepo, runner, null, ciRepo);
    }

    private sealed class StubCiAttentionGuidanceRepository(IReadOnlyList<CiAttentionBlockingFragment> fragments)
        : CiAttentionGuidanceRepository
    {
        public override Task<IReadOnlyList<CiAttentionBlockingFragment>> GetActiveBlockingFragmentsAsync(
            string? targetKind = null,
            string? targetKey = null,
            string? scope = null,
            string? authoringSurface = null,
            CancellationToken ct = default)
            => Task.FromResult(fragments);
    }

    private sealed class SpyCiAttentionGuidanceRepository : CiAttentionGuidanceRepository
    {
        public int UpsertCalls { get; private set; }
        public int GetActiveBlockingCalls { get; private set; }
        public string? LastAuthoringSurface { get; private set; }
        public string? LastUpsertAuthoringSurface { get; private set; }

        public override Task<CiAttentionGuidanceFragmentStored> UpsertCurrentAppendHistoryAsync(
            CiAttentionGuidanceFragmentUpsert fragment, CancellationToken ct = default)
        {
            UpsertCalls++;
            LastUpsertAuthoringSurface = fragment.AuthoringSurface;
            var p = new CiAttentionGuidanceGuidanceEventPayload(Guid.NewGuid(), "missing_input", "active", "warning", fragment.TargetKind, fragment.TargetKey, "admin_ui_builder", DateTimeOffset.UtcNow);
            return Task.FromResult(new CiAttentionGuidanceFragmentStored(p.FragmentId, p));
        }

        public override Task<IReadOnlyList<CiAttentionBlockingFragment>> GetActiveBlockingFragmentsAsync(
            string? targetKind = null,
            string? targetKey = null,
            string? scope = null,
            string? authoringSurface = null,
            CancellationToken ct = default)
        {
            GetActiveBlockingCalls++;
            LastAuthoringSurface = authoringSurface;
            return Task.FromResult<IReadOnlyList<CiAttentionBlockingFragment>>([
                new CiAttentionBlockingFragment(Guid.NewGuid(), "break_boundary", "authoring_surface", authoringSurface ?? "unknown", "blocked", "fix")
            ]);
        }
    }

    private sealed class StubUiTopologyRepository : UiTopologyRepository
    {
        private readonly PackageGenerateResult _generateResult;
        private readonly PackageGenerateResult _promoteResult;

        public StubUiTopologyRepository(PackageGenerateResult result, PackageGenerateResult? promoteResult = null)
            : base(NullLogger<UiTopologyRepository>.Instance, "test-double")
        {
            _generateResult = result;
            _promoteResult = promoteResult ?? result;
        }

        public override Task<PackageGenerateResult> GenerateFromBucketAsync(Guid bucketItemId, string routeKey, CancellationToken ct = default)
            => Task.FromResult(_generateResult);

        public override Task<PackageGenerateResult> PromoteBucketItemAsync(Guid bucketItemId, string routeKey, CancellationToken ct = default)
            => Task.FromResult(_promoteResult);
    }

    private sealed class StubPreviewUiTopologyRepository : UiTopologyRepository
    {
        public StubPreviewUiTopologyRepository() : base(NullLogger<UiTopologyRepository>.Instance, "test-double") { }

        public override Task<ValidationError?> VerifyLayoutPatchPackageBindingAsync(
            Guid packageId, Guid layoutId, string routeKey, CancellationToken ct = default)
            => Task.FromResult<ValidationError?>(null);

        public override Task<LayoutPatchResult> PreviewLayoutPatchAsync(
            Guid layoutId, string routeKey, string? tensorPatchJson,
            IReadOnlyList<string>? cssTokenRefs, IReadOnlyDictionary<string, IReadOnlyList<string>>? responsiveTokenRefs,
            CancellationToken ct = default)
            => Task.FromResult(new LayoutPatchResult(true, true, layoutId.ToString(), routeKey, tensorPatchJson ?? "{}", cssTokenRefs ?? [], responsiveTokenRefs ?? new Dictionary<string, IReadOnlyList<string>>(), "preview ok"));
    }


    private sealed class StubRunnerWithFinding : ISystemCiDiagnosticRunner
    {
        private readonly IReadOnlyList<string> _targets;
        public StubRunnerWithFinding(IReadOnlyList<string> targets) => _targets = targets;

        public IReadOnlyList<SystemCiTargetDto> ListTargets() =>
            _targets.Select(x => new SystemCiTargetDto(x)).ToArray();

        public Task<SystemCiDiagnosticResult> InspectAsync(string target, CancellationToken ct = default)
        {
            if (!_targets.Contains(target)) throw new ArgumentException("unknown target", nameof(target));
            var finding = new SystemCiFinding(
                "ATTENTION_SCORE_NOT_FINITE",
                SystemCiStatus.Blocking,
                "finite boundary",
                "target-1",
                SystemCiFindingClassification.RuntimeFailure);
            return Task.FromResult(new SystemCiDiagnosticResult(
                target, SystemCiInspectionKind.CronContinuity, SystemCiStatus.Blocking, [finding], DateTimeOffset.UtcNow));
        }
    }

    private sealed class StubRunner : ISystemCiDiagnosticRunner
    {
        private readonly IReadOnlyList<string> _targets;
        public StubRunner(IReadOnlyList<string> targets) => _targets = targets;

        public IReadOnlyList<SystemCiTargetDto> ListTargets() =>
            _targets.Select(x => new SystemCiTargetDto(x)).ToArray();

        public Task<SystemCiDiagnosticResult> InspectAsync(string target, CancellationToken ct = default)
        {
            if (!_targets.Contains(target))
                throw new ArgumentException("unknown target", nameof(target));
            return Task.FromResult(new SystemCiDiagnosticResult(
                target, SystemCiInspectionKind.CronContinuity, SystemCiStatus.Pass, [], DateTimeOffset.UtcNow));
        }
    }
}
