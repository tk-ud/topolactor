using Microsoft.Extensions.Logging;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// Authorization facts for a dispatchTargetRefByTrigger-referenced manifest, checked at the
/// layout_patch persistence boundary. Exists=false means the manifest_id does not exist at all
/// (IsActive/IsAdminRuntimeDestination are meaningless in that case). Mirrors the SAME two facts
/// ManifestDispatcher itself would otherwise only discover at dispatch time (manifest existence
/// via LoadByIdAsync, runtime_destination via ExtractRuntimeDestination) -- this surfaces them at
/// author/save time instead. Does not check capability_requirement (role) -- that is a per-request
/// fact, not statically checkable against an authored layout_patch.
/// </summary>
public record AdminRuntimeManifestAuthorizationResult(
    bool Exists,
    bool IsActive,
    bool IsAdminRuntimeDestination);

/// <summary>
/// Repository for ui_topology_tables: ui_component_bucket and the
/// package-generation pipeline tables defined in db/ui_topology_tables.sql.
///
/// No no-op fallback: all methods throw NotImplementedException to make unintended
/// injection an explicit failure rather than a silent no-op.
///
/// Production wiring: NpgsqlUiTopologyRepository overrides all methods.
/// Tests: override the required methods in the test subclass.
/// </summary>
public class UiTopologyRepository
{
    protected readonly ILogger<UiTopologyRepository> _logger;
    protected readonly string _connectionString;

    public UiTopologyRepository(ILogger<UiTopologyRepository> logger, string connectionString)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Lists bucket items with the given status (default 'bucketed').
    /// Production: overridden by NpgsqlUiTopologyRepository.
    /// </summary>
    public virtual Task<IReadOnlyList<UiComponentBucketRecord>> ListBucketItemsAsync(
        string status = "bucketed",
        CancellationToken ct = default)
    {
        throw new NotImplementedException(
            "UiTopologyRepository.ListBucketItemsAsync must be overridden by a production implementation.");
    }

    public virtual Task<UiComponentBucketCreateResult> CreateBucketItemAsync(
        string componentKey,
        string sourcePath,
        string componentKind,
        string? metadataJson = null,
        CancellationToken ct = default)
    {
        throw new NotImplementedException(
            "UiTopologyRepository.CreateBucketItemAsync must be overridden by a production implementation.");
    }


    public virtual Task<UiComponentBucketCreateResult> RegisterOrUpdateProjectionComponentAsync(
        string componentKey,
        string sourcePath,
        string componentKind,
        string? metadataJson = null,
        CancellationToken ct = default)
    {
        throw new NotImplementedException(
            "UiTopologyRepository.RegisterOrUpdateProjectionComponentAsync must be overridden by a production implementation.");
    }

    

    /// <summary>
    /// Transitions a bucket item from 'bucketed' to 'packaging'.
    /// No topology IDs are issued in this step.
    /// </summary>
    public virtual Task<PackageGenerateResult> GenerateFromBucketAsync(
        Guid bucketItemId,
        string routeKey,
        CancellationToken ct = default)
    {
        throw new NotImplementedException(
            "UiTopologyRepository.GenerateFromBucketAsync must be overridden by a production implementation.");
    }

    /// <summary>
    /// Atomically promotes a bucket item from 'packaging' to 'promoted' by:
    ///   1. SELECT with status precondition packaging (fail fast if not packaging)
    ///   2. INSERT ui_component_registry, ui_component_package, ui_package_component_map,
    ///      ui_layout_registry, ui_wiring_registry, ui_topology_tensor
    ///   3. UPDATE status packaging->promoted (verify rows==1; fail if not)
    ///   All steps execute in a single DB connection + transaction.
    ///   On any failure the transaction is rolled back — no partial state in DB.
    ///
    /// Key derivation:
    ///   component_key = bucket.component_key (verbatim)
    ///   package_key   = "{routeKey}:{bucket.component_key}:pkg"
    ///   layout_key    = "{routeKey}:{bucket.component_key}:layout"
    ///   wiring_key    = "{routeKey}:{bucket.component_key}:wiring"
    ///
    /// Returns PackageGenerateCode.NotFound         when bucket item does not exist.
    /// Returns PackageGenerateCode.NotBucketed      when bucket item is not in 'packaging' status.
    /// Returns PackageGenerateCode.ConstraintViolation when a unique key conflict occurs.
    /// Returns PackageGenerateCode.PromotionFailed  when final promoted update returns 0 rows.
    /// Returns PackageGenerateCode.DbUnavailable    when DB connection/transaction fails.
    /// Returns PackageGenerateCode.Success          with all issued IDs on success.
    ///
    /// Production: overridden by NpgsqlUiTopologyRepository.
    /// </summary>
    public virtual Task<PackageGenerateResult> PromoteBucketItemAsync(
        Guid bucketItemId,
        string routeKey,
        CancellationToken ct = default)
    {
        throw new NotImplementedException(
            "UiTopologyRepository.PromoteBucketItemAsync must be overridden by a production implementation.");
    }

    public virtual Task<LayoutPatchDraftDto?> GetLayoutPatchDraftAsync(
        Guid packageId,
        Guid layoutId,
        string routeKey,
        CancellationToken ct = default)
    {
        throw new NotImplementedException(
            "UiTopologyRepository.GetLayoutPatchDraftAsync must be overridden by a production implementation.");
    }

    /// <summary>Auto-saves canvas tmp draft to layout_draft_tmp_json. Cleared on apply.</summary>
    public virtual Task<ValidationError?> SaveLayoutDraftTmpAsync(
        Guid packageId,
        Guid layoutId,
        string routeKey,
        string tmpJson,
        CancellationToken ct = default)
    {
        throw new NotImplementedException(
            "UiTopologyRepository.SaveLayoutDraftTmpAsync must be overridden by a production implementation.");
    }

    public virtual Task<LayoutPatchResult> PreviewLayoutPatchAsync(
        Guid layoutId,
        string routeKey,
        string? tensorPatchJson,
        IReadOnlyList<string>? cssTokenRefs,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? responsiveTokenRefs,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("UiTopologyRepository.PreviewLayoutPatchAsync must be overridden by a production implementation.");
    }

    public virtual Task<LayoutPatchResult> ValidateLayoutPatchAsync(
        Guid layoutId,
        string routeKey,
        string? tensorPatchJson,
        IReadOnlyList<string>? cssTokenRefs,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? responsiveTokenRefs,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("UiTopologyRepository.ValidateLayoutPatchAsync must be overridden by a production implementation.");
    }

    public virtual Task<LayoutPatchResult> ApplyConfirmedLayoutPatchAsync(
        Guid packageId,
        Guid layoutId,
        string routeKey,
        string? tensorPatchJson,
        IReadOnlyList<string>? cssTokenRefs,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? responsiveTokenRefs,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("UiTopologyRepository.ApplyConfirmedLayoutPatchAsync must be overridden by a production implementation.");
    }

    /// <summary>
    /// Ensures layout_id + route_key belong to the given package via ui_topology_tensor.
    /// </summary>
    public virtual Task<ValidationError?> VerifyLayoutPatchPackageBindingAsync(
        Guid packageId,
        Guid layoutId,
        string routeKey,
        CancellationToken ct = default)
    {
        throw new NotImplementedException(
            "UiTopologyRepository.VerifyLayoutPatchPackageBindingAsync must be overridden by a production implementation.");
    }

    /// <summary>
    /// Ensures a selected canvas layout node belongs to the package effective layout draft before design writes.
    /// </summary>
    public virtual Task<ValidationError?> VerifyPackageLayoutNodeAsync(
        Guid packageId,
        string layoutNodeId,
        CancellationToken ct = default)
    {
        throw new NotImplementedException(
            "UiTopologyRepository.VerifyPackageLayoutNodeAsync must be overridden by a production implementation.");
    }

    public virtual Task<IReadOnlyList<PromotedPaletteEntryDto>> ListPromotedPaletteEntriesAsync(
        CancellationToken ct = default)
    {
        throw new NotImplementedException("UiTopologyRepository.ListPromotedPaletteEntriesAsync must be overridden by a production implementation.");
    }

    /// <summary>
    /// Lists distinct layout/route candidates with known slot keys for admin UI selectors.
    /// Production: overridden by NpgsqlUiTopologyRepository.
    /// </summary>
    public virtual Task<IReadOnlyList<LayoutCandidateDto>> ListLayoutCandidatesAsync(
        CancellationToken ct = default)
    {
        throw new NotImplementedException("UiTopologyRepository.ListLayoutCandidatesAsync must be overridden by a production implementation.");
    }

    /// <summary>
    /// Resolves package_id, route_key, and root layoutClassRefs for a layout_id tensor row.
    /// Returns null when no tensor row exists. Throws when multiple rows exist (ambiguous selector).
    /// </summary>
    public virtual Task<LayoutTensorContextDto?> ResolveLayoutTensorContextAsync(
        Guid layoutId,
        CancellationToken ct = default)
    {
        throw new NotImplementedException(
            "UiTopologyRepository.ResolveLayoutTensorContextAsync must be overridden by a production implementation.");
    }

    public virtual Task<IReadOnlyList<AdminPackageListItemDto>> ListAdminPackagesAsync(
        CancellationToken ct = default)
    {
        throw new NotImplementedException("UiTopologyRepository.ListAdminPackagesAsync must be overridden.");
    }

    public virtual Task<IReadOnlyList<ComponentStyleDesignListItemDto>> ListComponentStyleDesignsAsync(
        Guid? packageId,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("UiTopologyRepository.ListComponentStyleDesignsAsync must be overridden.");
    }

    public virtual Task<(Guid DesignId, ValidationError? Error)> UpsertComponentStyleDesignForPackageAsync(
        Guid packageId,
        Guid? componentId,
        string? layoutNodeId,
        string name,
        string designJson,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("UiTopologyRepository.UpsertComponentStyleDesignForPackageAsync must be overridden.");
    }

    public virtual Task<(Guid DesignId, ValidationError? Error)> SaveComponentStyleDesignDraftTmpForPackageAsync(
        Guid packageId,
        Guid? componentId,
        string? layoutNodeId,
        string name,
        string designTmpJson,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("UiTopologyRepository.SaveComponentStyleDesignDraftTmpForPackageAsync must be overridden.");
    }

    public virtual Task<IReadOnlyList<AdminPackageComponentDto>> ListPackageComponentsAsync(
        Guid packageId,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("UiTopologyRepository.ListPackageComponentsAsync must be overridden.");
    }

    public virtual Task<IReadOnlyList<ExternalPortAuthoringCandidateDto>> ListExternalPortAuthoringCandidatesAsync(
        CancellationToken ct = default)
    {
        throw new NotImplementedException("UiTopologyRepository.ListExternalPortAuthoringCandidatesAsync must be overridden.");
    }

    public virtual Task<IReadOnlyList<InstanceOperationAuthoringCandidateDto>> ListInstanceOperationAuthoringCandidatesAsync(
        CancellationToken ct = default)
    {
        throw new NotImplementedException("UiTopologyRepository.ListInstanceOperationAuthoringCandidatesAsync must be overridden.");
    }

    /// <summary>
    /// Lists every (active admin_runtime manifest, dispatcher_mapping layer/action) pair as an
    /// authorable dispatchTargetRefByTrigger candidate — the UI Builder authoring-side counterpart
    /// to ListExternalPortAuthoringCandidatesAsync/ListInstanceOperationAuthoringCandidatesAsync.
    /// A manifest with N dispatcher_mapping entries yields N candidates (one targetRef per
    /// layer/action pair it declares). Derived purely from manifest/dispatcher_mapping DB state —
    /// no per-surface (e.g. enum_dictionary) hardcoding.
    /// </summary>
    public virtual Task<IReadOnlyList<AdminRuntimeTargetRefAuthoringCandidateDto>> ListAdminRuntimeTargetRefAuthoringCandidatesAsync(
        CancellationToken ct = default)
    {
        throw new NotImplementedException("UiTopologyRepository.ListAdminRuntimeTargetRefAuthoringCandidatesAsync must be overridden.");
    }

    /// <summary>
    /// Loads the wiring_kind currently bound to layoutId (topology.ui_topology_tensor ->
    /// topology.ui_wiring_registry). Null when no tensor row exists yet or wiring_kind is null.
    /// Used by ValidateLayoutPatchAsync to gate the admin_runtime-only
    /// dispatchPayloadFromByTrigger/dispatchTargetRefByTrigger node-level fields. Virtual so unit
    /// tests can stub this without a live database — mirrors
    /// ListInstanceOperationAuthoringCandidatesAsync's own test-doubling pattern.
    /// </summary>
    public virtual Task<string?> LoadWiringKindForLayoutAsync(
        Guid layoutId, CancellationToken ct = default)
    {
        throw new NotImplementedException("UiTopologyRepository.LoadWiringKindForLayoutAsync must be overridden.");
    }

    /// <summary>
    /// Round 42 (admin-uibuilder wiring SSOT
    /// owner_decision_2026_08_14_general_dispatch_participation_contract_round42, schema-composed
    /// single-node spoof closure): true when layoutId's own
    /// topology.components_layout_design.layout_schema_json carries a non-empty records[] array —
    /// i.e. this layoutId is genuinely schema-composed, not tensor-only. A cheap, dedicated
    /// boolean check (never the full schema fetch) used to gate LoadLayoutSchemaJsonAsync/
    /// ResolveCatalogComponentKeysByNodeId generically for ANY admin_runtime dispatch-field-bearing
    /// save — including a single-node patch supplying its own componentKey for what may be a
    /// schema-tree catalog leaf nodeId — so a raw componentKey spoof for a schema-composed layout
    /// can never be reached, whether or not another node in the SAME patch happens to be missing
    /// componentKey. Virtual so unit tests can stub this without a live database — mirrors
    /// LoadWiringKindForLayoutAsync's own test-doubling pattern.
    /// </summary>
    public virtual Task<bool> LayoutHasSchemaComposedRecordsAsync(
        Guid layoutId, CancellationToken ct = default)
    {
        throw new NotImplementedException("UiTopologyRepository.LayoutHasSchemaComposedRecordsAsync must be overridden.");
    }

    /// <summary>
    /// Loads existence/active-status/runtime_destination authorization facts for a
    /// dispatchTargetRefByTrigger-referenced manifest_id (public.manifest, status +
    /// topology[runtime_mapping].runtime_destination -- the SAME facts ManifestDispatcher's
    /// LoadByIdAsync + ExtractRuntimeDestination check at dispatch time, surfaced here at
    /// layout_patch save time instead). Virtual so unit tests can stub this without a live
    /// database -- mirrors LoadWiringKindForLayoutAsync's own test-doubling pattern.
    /// </summary>
    public virtual Task<AdminRuntimeManifestAuthorizationResult> LoadAdminRuntimeManifestAuthorizationAsync(
        Guid manifestId, CancellationToken ct = default)
    {
        throw new NotImplementedException("UiTopologyRepository.LoadAdminRuntimeManifestAuthorizationAsync must be overridden.");
    }

    public virtual Task<AdminPackageWiringDto?> GetPackageWiringAsync(
        Guid packageId,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("UiTopologyRepository.GetPackageWiringAsync must be overridden.");
    }

    public virtual Task<ValidationError?> UpdatePackageWiringAsync(
        Guid packageId,
        Guid wiringId,
        string wiringKind,
        string targetSurface,
        string? targetRef,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("UiTopologyRepository.UpdatePackageWiringAsync must be overridden.");
    }

    /// <summary>
    /// Atomically promotes multiple bucket items into a single package for routeKey (1 route = 1 package).
    /// All items must be in 'packaging' status when this is called (call GenerateFromBucketAsync first).
    ///
    /// Key derivation:
    ///   package_key = "{routeKey}:pkg"
    ///   layout_key  = "{routeKey}:layout"
    ///   wiring_key  = "{routeKey}:wiring"
    ///
    /// package_schema_json stores { "bucketItemIds": [...], "componentKeys": [...] }.
    /// Idempotent: ON CONFLICT semantics handle pre-existing keys.
    ///
    /// Returns PackageGenerateBatchResult.Success with all IDs and member sets on success.
    /// Production: overridden by NpgsqlUiTopologyRepository.
    /// </summary>
    public virtual Task<PackageGenerateBatchResult> PromotePackageFromBucketItemsAsync(
        string routeKey,
        IReadOnlyList<Guid> bucketItemIds,
        CancellationToken ct = default)
    {
        throw new NotImplementedException(
            "UiTopologyRepository.PromotePackageFromBucketItemsAsync must be overridden by a production implementation.");
    }

    /// <summary>
    /// Removes component keys from the route package ({routeKey}:pkg).
    /// Updates package_schema_json, ui_package_component_map, and bucket status.
    /// </summary>
    public virtual Task<PackageDetachComponentsResult> DetachPackageComponentsAsync(
        string routeKey,
        IReadOnlyList<string> componentKeys,
        CancellationToken ct = default)
    {
        throw new NotImplementedException(
            "UiTopologyRepository.DetachPackageComponentsAsync must be overridden by a production implementation.");
    }
}
