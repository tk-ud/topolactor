using Microsoft.Extensions.Logging;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Scheduler;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Canonical admin runtime. Owns all admin business logic.
/// AdminEndpoint delegates to the typed methods here.
/// In manifest-driven production path, AdminRuntimeDispatchAdapter calls ExecuteDataAsync
/// when runtime_destination=admin_runtime is resolved from the active manifest.
/// </summary>
public partial class AdminRuntime
{
    private readonly ILogger<AdminRuntime> _logger;
    private readonly ContextRouteRepository _contextRouteRepository;
    private readonly RegistrarValidationService _registrarValidationService;
    private readonly PackageGeneratorRuntime _packageGeneratorRuntime;
    private readonly UiTopologyRepository _uiTopologyRepository;
    private readonly ISystemCiDiagnosticRunner? _systemCiDiagnosticRunner;
    private readonly CiAttentionGuidanceRepository _ciAttentionGuidanceRepository;
    private readonly SseEventBroadcaster? _sseEventBroadcaster;
    private readonly SeedRuntime? _seedRuntime;
    private readonly AdminImportRuntime? _adminImportRuntime;
    private readonly ManifestRepository? _manifestRepository;
    private readonly ContentBundleRepository? _contentBundleRepository;
    private readonly TopologyRepository? _topologyRepository;
    private readonly EnumDictionaryRepository? _enumDictionaryRepository;
    private readonly AuthMasterRepository? _authMasterRepository;
    private readonly SqlAttentionLogsRepository? _sqlAttentionLogsRepository;
    private readonly MockPresetRepository? _mockPresetRepository;
    private readonly TeamMarkdownRepository? _teamMarkdownRepository;

    private static readonly HashSet<string> KnownRuntimeDestinations = new(StringComparer.OrdinalIgnoreCase)
    {
        "topology_transform_runtime",
        "admin_runtime",
        "sse_projection_runtime",
    };

    public AdminRuntime(
        ILogger<AdminRuntime> logger,
        ContextRouteRepository contextRouteRepository,
        RegistrarValidationService registrarValidationService,
        PackageGeneratorRuntime packageGeneratorRuntime,
        UiTopologyRepository uiTopologyRepository,
        ISystemCiDiagnosticRunner? systemCiDiagnosticRunner = null,
        SeedRuntime? seedRuntime = null,
        CiAttentionGuidanceRepository? ciAttentionGuidanceRepository = null,
        SseEventBroadcaster? sseEventBroadcaster = null,
        AdminImportRuntime? adminImportRuntime = null,
        ManifestRepository? manifestRepository = null,
        ContentBundleRepository? contentBundleRepository = null,
        TopologyRepository? topologyRepository = null,
        EnumDictionaryRepository? enumDictionaryRepository = null,
        AuthMasterRepository? authMasterRepository = null,
        SqlAttentionLogsRepository? sqlAttentionLogsRepository = null,
        MockPresetRepository? mockPresetRepository = null,
        TeamMarkdownRepository? teamMarkdownRepository = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contextRouteRepository = contextRouteRepository ?? throw new ArgumentNullException(nameof(contextRouteRepository));
        _registrarValidationService = registrarValidationService ?? throw new ArgumentNullException(nameof(registrarValidationService));
        _packageGeneratorRuntime = packageGeneratorRuntime ?? throw new ArgumentNullException(nameof(packageGeneratorRuntime));
        _uiTopologyRepository = uiTopologyRepository ?? throw new ArgumentNullException(nameof(uiTopologyRepository));
        _systemCiDiagnosticRunner = systemCiDiagnosticRunner;
        _seedRuntime = seedRuntime;
        _ciAttentionGuidanceRepository = ciAttentionGuidanceRepository ?? new CiAttentionGuidanceRepository();
        _sseEventBroadcaster = sseEventBroadcaster;
        _adminImportRuntime = adminImportRuntime;
        _manifestRepository = manifestRepository;
        _contentBundleRepository = contentBundleRepository;
        _topologyRepository = topologyRepository;
        _enumDictionaryRepository = enumDictionaryRepository;
        _authMasterRepository = authMasterRepository;
        _sqlAttentionLogsRepository = sqlAttentionLogsRepository;
        _mockPresetRepository = mockPresetRepository;
        _teamMarkdownRepository = teamMarkdownRepository;
    }

    // ---------------------------------------------------------------------------
    // Typed methods — called by AdminEndpoint (thin wrapper)
    // ---------------------------------------------------------------------------

    public async Task<IReadOnlyList<AdminContextTokenDto>> ListTokensAsync(CancellationToken ct = default)
    {
        var records = await _contextRouteRepository.ListAllContextTokensAsync(ct);
        return records
            .Select(r => new AdminContextTokenDto(r.TokenId, r.Label, r.Group, r.Value, r.Status))
            .ToList();
    }

    public async Task<AdminCreateTokenResponseDto> CreateTokenAsync(
        AdminCreateTokenRequestDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
            return new AdminCreateTokenResponseDto(false, null, "label is required.", "LABEL_REQUIRED");

        if (request.Value < -1.0f || request.Value > 1.0f)
            return new AdminCreateTokenResponseDto(false, null, "value must be in [-1.0, 1.0].", "VALUE_OUT_OF_RANGE");

        _logger.LogDebug("AdminRuntime.CreateTokenAsync: label={Label}", request.Label);

        var result = await _contextRouteRepository.CreateContextTokenAsync(
            request.Label, request.Group, request.Value, ct);

        return result.Code switch
        {
            CreateTokenCode.Success =>
                new AdminCreateTokenResponseDto(true, result.TokenId!.Value.ToString(), "Token created."),
            CreateTokenCode.Conflict =>
                new AdminCreateTokenResponseDto(false, null,
                    "A token with this label and group already exists.", "DUPLICATE_LABEL_GROUP"),
            _ =>
                new AdminCreateTokenResponseDto(false, null, "Unexpected error.", "UNEXPECTED_ERROR"),
        };
    }

    public async Task<(AdminDeprecateTokenResponseDto Response, bool Found)> DeprecateTokenAsync(
        Guid tokenId, CancellationToken ct = default)
    {
        _logger.LogDebug("AdminRuntime.DeprecateTokenAsync: tokenId={TokenId}", tokenId);

        var found = await _contextRouteRepository.DeprecateContextTokenAsync(tokenId, ct);
        if (!found)
            return (new AdminDeprecateTokenResponseDto(false, "Token not found."), false);

        return (new AdminDeprecateTokenResponseDto(true, "Token deprecated."), true);
    }

    public async Task<(AdminRegistryVectorValidateResponseDto Response, int StatusCode)>
        ValidateRegistryVectorAsync(
            AdminRegistryVectorValidateRequestDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RegistryTable))
        {
            return (new AdminRegistryVectorValidateResponseDto(
                ValidationClass: "explicit_error",
                IsBlocking: true,
                Neighbors: [],
                StatusDetail: "REGISTRY_TABLE_REQUIRED"), 400);
        }

        var parsedIds = new List<Guid>();
        foreach (var raw in request.QueryIds ?? [])
        {
            if (!Guid.TryParse(raw, out var id))
            {
                return (new AdminRegistryVectorValidateResponseDto(
                    ValidationClass: "explicit_error",
                    IsBlocking: true,
                    Neighbors: [],
                    StatusDetail: $"INVALID_QUERY_ID:{raw}"), 400);
            }
            parsedIds.Add(id);
        }

        _logger.LogDebug(
            "AdminRuntime.ValidateRegistryVectorAsync: table={Table} queryIds={Count}",
            request.RegistryTable, parsedIds.Count);

        var result = await _registrarValidationService.ValidateAsync(
            request.RegistryTable, parsedIds, ct);

        var validationClass = result.ValidationClass switch
        {
            RegistryVectorValidationClass.Pass                    => "pass",
            RegistryVectorValidationClass.RelatedExistingRegistry => "related_existing_registry",
            RegistryVectorValidationClass.NearDuplicateVector     => "near_duplicate_vector",
            RegistryVectorValidationClass.DuplicateVector         => "duplicate_vector",
            RegistryVectorValidationClass.ZeroVector              => "zero_vector",
            RegistryVectorValidationClass.ExplicitError           => "explicit_error",
            _ => "explicit_error"
        };

        var neighbors = result.Neighbors
            .Select(n => new AdminRegistryVectorNeighborDto(
                RegistryId:  n.RegistryId.ToString(),
                Name:        n.Name,
                CosineScore: n.CosineScore,
                MatchedIds:  n.MatchedIds.Select(id => id.ToString()).ToList(),
                Reason:      n.Reason))
            .ToList();

        var statusCode = result.IsBlocking && result.ValidationClass == RegistryVectorValidationClass.ExplicitError
            ? 422
            : 200;

        return (new AdminRegistryVectorValidateResponseDto(
            ValidationClass: validationClass,
            IsBlocking:      result.IsBlocking,
            Neighbors:       neighbors,
            StatusDetail:    result.StatusDetail), statusCode);
    }

    // ---------------------------------------------------------------------------
    // Manifest-driven dispatch entry — called by AdminRuntimeDispatchAdapter
    // when runtime_destination=admin_runtime is resolved from the active manifest.
    // In dev/demo bypass (null manifest repo), also called via TargetDispatchOverride.
    // Returns (data, null) on success or (null, error) on failure.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Executes the admin operation data step for the given vector's layer+action.
    /// </summary>
    public async Task<(JsonElement? data, ValidationError? error)> ExecuteDataAsync(
        OperationVector vector, CancellationToken ct = default)
    {
        var layerAction = $"{vector.Layer?.ToLowerInvariant()}:{vector.Action?.ToLowerInvariant()}";
        _logger.LogInformation("AdminRuntime.ExecuteDataAsync: layerAction={LayerAction}", layerAction);

        return layerAction switch
        {
            "context_token_registry:list"      => await DataListTokensAsync(ct),
            "context_token_registry:create"    => await DataCreateTokenAsync(vector, ct),
            "context_token_registry:deprecate" => await DataDeprecateTokenAsync(vector, ct),
            "registry_vector:validate"         => await DataValidateRegistryVectorAsync(vector, ct),
            "ui_component_bucket:create"       => await DataCreateBucketItemAsync(vector, ct),
            "ui_component_bucket:list"         => await DataListBucketItemsAsync(vector, ct),
            "package_generator:generate"        => await DataGenerateAsync(vector, ct),
            "package_generator:promote"         => await DataPromoteAsync(vector, ct),
            "package_generator:promote_package" => await DataPromotePackageAsync(vector, ct),
            "package_generator:detach_package_components" => await DataDetachPackageComponentsAsync(vector, ct),
            "ui_topology:promoted_palette"      => await DataPromotedPaletteAsync(ct),
            "ui_topology:layout_candidates"    => await DataLayoutCandidatesAsync(ct),
            "ui_topology:list_packages"        => await DataListAdminPackagesAsync(ct),
            "ui_topology:list_package_components" => await DataListPackageComponentsAsync(vector, ct),
            "ui_topology:get_package_wiring"   => await DataGetPackageWiringAsync(vector, ct),
            "ui_topology:get_layout_patch_draft" => await DataGetLayoutPatchDraftAsync(vector, ct),
            "ui_topology:update_package_wiring" => await DataUpdatePackageWiringAsync(vector, ct),
            "component_style_design:list"      => await DataListComponentStyleDesignsAsync(vector, ct),
            "component_style_design:save_tmp"  => await DataSaveComponentStyleDesignTmpAsync(vector, ct),
            "component_style_design:upsert"    => await DataUpsertComponentStyleDesignAsync(vector, ct),
            "layout_patch:preview"             => await DataLayoutPatchPreviewAsync(vector, ct),
            "layout_patch:validate"            => await DataLayoutPatchValidateAsync(vector, ct),
            "layout_patch:apply"               => await DataLayoutPatchApplyAsync(vector, ct),
            "layout_patch:save_tmp"            => await DataLayoutPatchSaveTmpAsync(vector, ct),
            "seed_runtime:save"                => await DataSeedSaveAsync(vector, ct),
            "seed_runtime:load"                => await DataSeedLoadAsync(ct),
            "seed_runtime:validate"            => await DataSeedValidateAsync(ct),
            "seed_runtime:preview"             => await DataSeedPreviewAsync(ct),
            "seed_runtime:import"              => await DataSeedImportAsync(ct),
            "system_ci:list_targets"                    => await DataSystemCiListTargetsAsync(ct),
            "system_ci:inspect"                         => await DataSystemCiInspectAsync(vector, ct),
            "ci_attention:refresh_fragments"            => await DataCiAttentionRefreshFragmentsAsync(vector, ct),
            "admin_csv_json_import:upload_preview"      => await DataImportUploadPreviewAsync(vector, ct),
            "admin_csv_json_import:apply"               => await DataImportApplyAsync(vector, ct),
            "admin_csv_json_import:list_manifests"      => await DataImportListManifestsAsync(ct),
            "admin_csv_json_import:list_schemas"        => await DataImportListSchemasAsync(ct),
            "admin_csv_json_import:list_snapshot_records" => await DataImportListSnapshotRecordsAsync(vector, ct),
            "manifest:list"                             => await DataManifestListAsync(vector, ct),
            "manifest:get"                              => await DataManifestGetAsync(vector, ct),
            "manifest:validate"                         => await DataManifestValidateAsync(vector, ct),
            "manifest:create_draft"                     => await DataManifestCreateDraftAsync(vector, ct),
            "manifest:update_draft"                     => await DataManifestUpdateDraftAsync(vector, ct),
            "manifest:promote"                          => await DataManifestPromoteAsync(vector, ct),
            "manifest:deprecate"                        => await DataManifestDeprecateAsync(vector, ct),
            "manifest:assign_hub_grouping"                => await DataManifestAssignHubGroupingAsync(vector, ct),
            "manifest:assign_screen_data_shape"           => await DataManifestAssignScreenDataShapeAsync(vector, ct),
            "manifest:list_screen_read_query_wiring"      => await DataManifestListScreenReadQueryWiringAsync(vector, ct),
            "manifest:list_relationship_remote_targets"   => await DataManifestListRelationshipRemoteTargetsAsync(vector, ct),
            "enum_dictionary:list_groups"                 => await DataEnumDictionaryListGroupsAsync(ct),
            "enum_dictionary:get_group"                   => await DataEnumDictionaryGetGroupAsync(vector, ct),
            "enum_dictionary:create_group"                => await DataEnumDictionaryCreateGroupAsync(vector, ct),
            "enum_dictionary:update_group"                => await DataEnumDictionaryUpdateGroupAsync(vector, ct),
            "enum_dictionary:delete_group"                => await DataEnumDictionaryDeleteGroupAsync(vector, ct),
            "enum_dictionary:create_item"                 => await DataEnumDictionaryCreateItemAsync(vector, ct),
            "enum_dictionary:update_item"                 => await DataEnumDictionaryUpdateItemAsync(vector, ct),
            "enum_dictionary:delete_item"                 => await DataEnumDictionaryDeleteItemAsync(vector, ct),
            "enum_dictionary:set_group_items"             => await DataEnumDictionarySetGroupItemsAsync(vector, ct),
            "auth_users:list"                             => await DataAuthUsersListAsync(vector, ct),
            "auth_users:search"                           => await DataAuthUsersSearchAsync(vector, ct),
            "auth_users:get"                              => await DataAuthUsersGetAsync(vector, ct),
            "auth_users:create"                           => await DataAuthUsersCreateAsync(vector, ct),
            "auth_users:update"                           => await DataAuthUsersUpdateAsync(vector, ct),
            "auth_users:delete"                           => await DataAuthUsersDeleteAsync(vector, ct),
            "promotion_manifest:list"                   => await DataPromotionManifestListAsync(vector, ct),
            "promotion_manifest:get"                    => await DataPromotionManifestGetAsync(vector, ct),
            "promotion_manifest:validate"               => await DataPromotionManifestValidateAsync(vector, ct),
            "promotion_manifest:update_draft"           => await DataPromotionManifestUpdateDraftAsync(vector, ct),
            "content_bundle:list_hubs"                  => await DataContentBundleListHubsAsync(ct),
            "content_bundle:list_entities"              => await DataContentBundleListEntitiesAsync(ct),
            "content_bundle:list_relations"             => await DataContentBundleListRelationsAsync(ct),
            "content_bundle:list_states"                => await DataContentBundleListStatesAsync(ct),
            "content_bundle:get_entity"                 => await DataContentBundleGetEntityAsync(vector, ct),
            "content_bundle:search"                     => await DataContentBundleSearchAsync(vector, ct),
            "content_bundle:create_entity_draft"        => await DataContentBundleCreateDraftAsync(vector, ct),
            "content_bundle:validate_draft"             => await DataContentBundleValidateDraftAsync(vector, ct),
            "content_bundle:preview_draft"              => await DataContentBundlePreviewDraftAsync(vector, ct),
            "content_bundle:promote_draft"              => await DataContentBundlePromoteDraftAsync(vector, ct),
            "content_bundle:get_hub"                    => await DataContentBundleGetHubAsync(vector, ct),
            "content_bundle:get_relation"               => await DataContentBundleGetRelationAsync(vector, ct),
            "content_bundle:update_entity_draft"        => await DataContentBundleUpdateDraftAsync(vector, ct),
            "content_bundle:list_hub_relations"         => await DataContentBundleListHubRelationsAsync(ct),
            "physical_record:list_history"             => await DataPhysicalRecordListHistoryAsync(vector, ct),
            "hub_navigation:list_manifests"             => await HubNavigationListManifestsAsync(ct),
            "hub_navigation:get_hub_relations"          => await HubNavigationGetHubRelationsAsync(vector, ct),
            "hub_navigation:create"                     => await HubNavigationCreateAsync(vector, ct),
            "hub_navigation:update"                     => await HubNavigationUpdateAsync(vector, ct),
            "hub_navigation:deprecate"                  => await HubNavigationDeprecateAsync(vector, ct),
            "hub_navigation:reorder"                    => await HubNavigationReorderAsync(vector, ct),
            "mock_preset:create"                        => await DataMockPresetCreateAsync(vector, ct),
            "mock_preset:list"                          => await DataMockPresetListAsync(vector, ct),
            "mock_preset:get"                           => await DataMockPresetGetAsync(vector, ct),
            "mock_preset:compile"                       => await DataMockPresetCompileAsync(vector, ct),
            "mock_preset:bind"                          => await DataMockPresetBindAsync(vector, ct),
            "mock_preset:save_mappings"                 => await DataMockPresetSaveMappingsAsync(vector, ct),
            var a when a.StartsWith("team_markdown:", StringComparison.OrdinalIgnoreCase)
                                                        => await ExecuteTeamMarkdownAsync(vector with { Action = a["team_markdown:".Length..] }, ct),
            _ => (null, new ValidationError("ADMIN_OPERATION_NOT_FOUND",
                $"Unknown admin operation: {layerAction}"))
        };
    }
    private async Task<(JsonElement? data, ValidationError? error)> DataCiAttentionRefreshFragmentsAsync(OperationVector vector, CancellationToken ct)
    {
        if (_systemCiDiagnosticRunner is null) return (null, new ValidationError("SYSTEM_CI_DIAGNOSTIC_NOT_AVAILABLE", "system_ci diagnostic runner is not registered"));
        if (vector.Payload is null) return (null, new ValidationError("CI_ATTENTION_PAYLOAD_REQUIRED", "payload is required"));
        var payload = vector.Payload.Value;
        var target = payload.TryGetProperty("target", out var t) ? t.GetString() : null;
        var sourceKindRaw = payload.TryGetProperty("source_kind", out var sk) ? sk.GetString() : null;
        if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(sourceKindRaw))
            return (null, new ValidationError("CI_ATTENTION_TARGET_OR_SOURCE_KIND_REQUIRED", "payload.target and payload.source_kind are required"));
        if (!TryParseSourceKind(sourceKindRaw!, out var sourceKind))
            return (null, new ValidationError("CI_ATTENTION_SOURCE_KIND_INVALID", "source_kind must be one of entity_diff,draft_diff,runtime_event,manual_check"));
        var result = await _systemCiDiagnosticRunner.InspectAsync(target!, ct);
        var stored = new List<CiAttentionGuidanceGuidanceEventPayload>();
        foreach (var f in result.Findings)
        {
            var upsert = MapToFragment(sourceKind, target!, result, f, payload);
            var saved = await _ciAttentionGuidanceRepository.UpsertCurrentAppendHistoryAsync(upsert, ct);
            stored.Add(saved.EventPayload);
            _sseEventBroadcaster?.Broadcast(new SseEvent("projection", JsonSerializer.Serialize(saved.EventPayload)));
        }
        return (JsonSerializer.SerializeToElement(new { target, count = stored.Count, fragments = stored }), null);
    }
    private static bool TryParseSourceKind(string raw, out CiAttentionSourceKind kind){kind=raw switch{"entity_diff"=>CiAttentionSourceKind.EntityDiff,"draft_diff"=>CiAttentionSourceKind.DraftDiff,"runtime_event"=>CiAttentionSourceKind.RuntimeEvent,"manual_check"=>CiAttentionSourceKind.ManualCheck,_=>CiAttentionSourceKind.ManualCheck};return raw is "entity_diff" or "draft_diff" or "runtime_event" or "manual_check";}
    private const string DefaultAuthoringSurface = "admin_ui_builder";

    private static string ResolveAuthoringSurface(JsonElement payload)
    {
        if (payload.TryGetProperty("authoring_surface", out var surfaceEl))
        {
            var surface = surfaceEl.GetString();
            if (!string.IsNullOrWhiteSpace(surface)) return surface;
        }

        return DefaultAuthoringSurface;
    }

    private static string ResolveAuthoringSurface(OperationVector vector)
    {
        if (vector.Payload is { } payload) return ResolveAuthoringSurface(payload);
        return DefaultAuthoringSurface;
    }

    private static CiAttentionGuidanceFragmentUpsert MapToFragment(CiAttentionSourceKind sourceKind,string target,SystemCiDiagnosticResult result,SystemCiFinding finding,JsonElement payload)
    {
        var kind = finding.Classification switch { SystemCiFindingClassification.MissingRequired => CiAttentionGuidanceKind.MissingInput, SystemCiFindingClassification.NotCovered=>CiAttentionGuidanceKind.ValidCandidate, SystemCiFindingClassification.InvalidShape=>CiAttentionGuidanceKind.StructuralViolation, _=>CiAttentionGuidanceKind.BreakBoundary};
        var severity = finding.Status == SystemCiStatus.Blocking ? CiAttentionGuidanceSeverity.Error : CiAttentionGuidanceSeverity.Warning;
        var sourceId = payload.TryGetProperty("source_id", out var sid) ? sid.GetString() : null;
        var targetKind = payload.TryGetProperty("target_kind", out var tk) ? tk.GetString() : null;
        var targetKey = payload.TryGetProperty("target_key", out var tkey) ? tkey.GetString() : null;
        var evidence = JsonSerializer.SerializeToElement(new { check_name = finding.CheckName, detail = finding.Detail, classification = finding.Classification.ToString(), inspection_kind = result.InspectionKind.ToString(), inspected_at = result.InspectedAt });
        return new CiAttentionGuidanceFragmentUpsert(sourceKind, sourceId ?? target, payload.TryGetProperty("source_table_or_surface", out var sts) ? sts.GetString() ?? "system_ci" : "system_ci", targetKind ?? "authoring_surface", targetKey ?? target, finding.TargetId, "system_ci_diagnostic", "admin_runtime:ci_attention:refresh_fragments", ResolveAuthoringSurface(payload), kind, CiAttentionGuidanceStatus.Active, severity, finding.Status == SystemCiStatus.Blocking, finding.Detail, $"Inspect check {finding.CheckName} for target {target}.", evidence, result.InspectedAt);
    }

    /// <summary>
    /// Reads active blocking CI Attention fragments for the given authoring surface.
    /// Returns a ValidationError if blocking fragments exist; null if the gate is clear.
    /// dismissed fragments are not included (dismissed is visibility control, not promotion unlock).
    /// </summary>
    private async Task<ValidationError?> CheckCiAttentionPromotionGateAsync(string authoringSurface, CancellationToken ct)
    {
        var blocking = await _ciAttentionGuidanceRepository.GetActiveBlockingFragmentsAsync(
            authoringSurface: authoringSurface, ct: ct);
        if (blocking.Count == 0) return null;
        var fragments = blocking.Select(f => new
        {
            kind = f.Kind,
            target_kind = f.TargetKind,
            target_key = f.TargetKey,
            message = f.Message,
            actionable_guidance = f.ActionableGuidance,
        });
        return new ValidationError("CI_ATTENTION_PROMOTION_BLOCKED",
            JsonSerializer.Serialize(new { blocking_fragments = fragments }));
    }

    private Task<(JsonElement? data, ValidationError? error)> DataSystemCiListTargetsAsync(CancellationToken ct)
    {
        _ = ct;
        if (_systemCiDiagnosticRunner is null)
            return Task.FromResult<(JsonElement?, ValidationError?)>((null, new ValidationError("SYSTEM_CI_DIAGNOSTIC_NOT_AVAILABLE", "system_ci diagnostic runner is not registered")));
        return Task.FromResult<(JsonElement?, ValidationError?)>((JsonSerializer.SerializeToElement(_systemCiDiagnosticRunner.ListTargets()), null));
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataSystemCiInspectAsync(OperationVector vector, CancellationToken ct)
    {
        if (_systemCiDiagnosticRunner is null)
            return (null, new ValidationError("SYSTEM_CI_DIAGNOSTIC_NOT_AVAILABLE", "system_ci diagnostic runner is not registered"));
        if (vector.Payload is null)
            return (null, new ValidationError("SYSTEM_CI_TARGET_REQUIRED", "payload.target is required for system_ci:inspect"));
        if (!vector.Payload.Value.TryGetProperty("target", out var targetEl) || string.IsNullOrWhiteSpace(targetEl.GetString()))
            return (null, new ValidationError("SYSTEM_CI_TARGET_REQUIRED", "payload.target is required for system_ci:inspect"));
        var target = targetEl.GetString()!;
        try
        {
            var result = await _systemCiDiagnosticRunner.InspectAsync(target, ct);
            return (JsonSerializer.SerializeToElement(result), null);
        }
        catch (ArgumentException)
        {
            return (null, new ValidationError("SYSTEM_CI_TARGET_NOT_FOUND", $"Unknown system_ci target: {target}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdminRuntime.DataSystemCiInspectAsync failed for target={Target}", target);
            return (null, new ValidationError("SYSTEM_CI_DIAGNOSTIC_FAILED", ex.Message));
        }
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataPhysicalRecordListHistoryAsync(
        OperationVector vector,
        CancellationToken ct)
    {
        if (_sqlAttentionLogsRepository is null)
            return (null, new ValidationError("SQL_ATTENTION_LOGS_REPOSITORY_UNAVAILABLE", "SqlAttentionLogsRepository is not registered for physical_record:list_history"));
        if (vector.Payload is null)
            return (null, new ValidationError("PAYLOAD_REQUIRED", "payload.tableId and payload.recordId are required for physical_record:list_history"));

        var payload = vector.Payload.Value;
        var tableId = payload.TryGetProperty("tableId", out var tableIdEl) ? tableIdEl.GetString() : null;
        tableId ??= payload.TryGetProperty("physicalTableId", out var physicalTableIdEl) ? physicalTableIdEl.GetString() : null;
        var recordId = payload.TryGetProperty("recordId", out var recordIdEl) ? recordIdEl.GetString() : null;

        if (string.IsNullOrWhiteSpace(tableId))
            return (null, new ValidationError("PHYSICAL_RECORD_TABLE_ID_REQUIRED", "payload.tableId is required for physical_record:list_history"));
        if (string.IsNullOrWhiteSpace(recordId))
            return (null, new ValidationError("PHYSICAL_RECORD_RECORD_ID_REQUIRED", "payload.recordId is required for physical_record:list_history"));

        try
        {
            var history = await _sqlAttentionLogsRepository.LoadPhysicalRecordHistoryAsync(tableId, recordId, ct);
            var responseHistory = history.Select(entry => new
            {
                diff_id = entry.DiffId,
                tableId = entry.TableId,
                tableName = entry.TableName,
                recordId = entry.RecordId,
                operation_kind = entry.OperationKind,
                before_state_or_diff_json = entry.BeforeStateOrDiffJson,
                after_state_or_diff_json = entry.AfterStateOrDiffJson,
                observed_at = entry.ObservedAt,
                actor_or_source = entry.ActorOrSource,
                archive_policy = entry.ArchivePolicy,
            }).ToList();
            var response = new
            {
                ok = true,
                status = responseHistory.Count == 0 ? "empty_history" : "ok",
                tableId,
                recordId,
                history = responseHistory,
            };
            return (JsonSerializer.SerializeToElement(response), null);
        }
        catch (ArgumentException ex)
        {
            return (null, new ValidationError("PHYSICAL_RECORD_HISTORY_REQUEST_INVALID", ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdminRuntime.DataPhysicalRecordListHistoryAsync failed for tableId={TableId} recordId={RecordId}", tableId, recordId);
            return (null, new ValidationError("PHYSICAL_RECORD_HISTORY_READ_FAILED", ex.Message));
        }
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataListTokensAsync(
        CancellationToken ct)
    {
        var tokens = await ListTokensAsync(ct);
        return (JsonSerializer.SerializeToElement(tokens), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataCreateTokenAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (vector.Payload is null)
            return (null, new ValidationError("PAYLOAD_REQUIRED",
                "payload is required for context_token_registry:create"));

        AdminCreateTokenRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<AdminCreateTokenRequestDto>(
                vector.Payload.Value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            return (null, new ValidationError("MALFORMED_PAYLOAD", ex.Message));
        }

        if (request is null)
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload could not be deserialized"));

        var result = await CreateTokenAsync(request, ct);
        return (JsonSerializer.SerializeToElement(result), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataDeprecateTokenAsync(
        OperationVector vector, CancellationToken ct)
    {
        Guid tokenId;
        if (vector.IdOrHubId.HasValue)
        {
            tokenId = vector.IdOrHubId.Value;
        }
        else if (vector.Payload.HasValue &&
                 vector.Payload.Value.TryGetProperty("id", out var idEl) &&
                 Guid.TryParse(idEl.GetString(), out var parsedFromPayload))
        {
            tokenId = parsedFromPayload;
        }
        else
        {
            return (null, new ValidationError("TOKEN_ID_REQUIRED",
                "idOrHubId or payload.id UUID is required for deprecate"));
        }

        var (response, found) = await DeprecateTokenAsync(tokenId, ct);
        if (!found)
            return (null, new ValidationError("TOKEN_NOT_FOUND", response.Message));

        return (JsonSerializer.SerializeToElement(response), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataValidateRegistryVectorAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (vector.Payload is null)
            return (null, new ValidationError("PAYLOAD_REQUIRED",
                "payload is required for registry_vector:validate"));

        AdminRegistryVectorValidateRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<AdminRegistryVectorValidateRequestDto>(
                vector.Payload.Value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            return (null, new ValidationError("MALFORMED_PAYLOAD", ex.Message));
        }

        if (request is null)
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload could not be deserialized"));

        var (response, statusCode) = await ValidateRegistryVectorAsync(request, ct);
        if (statusCode >= 400)
            return (null, new ValidationError(response.StatusDetail ?? "VALIDATION_ERROR",
                response.ValidationClass));

        return (JsonSerializer.SerializeToElement(response), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataListBucketItemsAsync(
        OperationVector vector, CancellationToken ct)
    {
        string status = "bucketed";
        if (vector.Payload.HasValue &&
            vector.Payload.Value.TryGetProperty("status", out var statusEl) &&
            !string.IsNullOrWhiteSpace(statusEl.GetString()))
        {
            status = statusEl.GetString()!;
        }

        IReadOnlyList<UiComponentBucketRecord> records;
        try
        {
            records = await _uiTopologyRepository.ListBucketItemsAsync(status, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdminRuntime.DataListBucketItemsAsync: repository unavailable.");
            return (null, new ValidationError("REPOSITORY_UNAVAILABLE", ex.Message));
        }

        var items = records
            .Select(r => new UiComponentBucketItemDto(
                r.BucketItemId.ToString(),
                r.ComponentKey,
                r.SourcePath,
                r.ComponentKind,
                r.Status))
            .ToList();

        return (JsonSerializer.SerializeToElement(items), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataCreateBucketItemAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (vector.Payload is null)
            return (null, new ValidationError("PAYLOAD_REQUIRED",
                "payload is required for ui_component_bucket:create"));
        UiComponentBucketCreateRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<UiComponentBucketCreateRequestDto>(
                vector.Payload.Value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            return (null, new ValidationError("MALFORMED_PAYLOAD", ex.Message));
        }

        if (request is null)
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload could not be deserialized"));
        if (string.IsNullOrWhiteSpace(request.ComponentKey))
            return (null, new ValidationError("COMPONENT_KEY_REQUIRED", "componentKey is required"));
        if (string.IsNullOrWhiteSpace(request.SourcePath))
            return (null, new ValidationError("SOURCE_PATH_REQUIRED", "sourcePath is required"));
        if (string.IsNullOrWhiteSpace(request.ComponentKind))
            return (null, new ValidationError("COMPONENT_KIND_REQUIRED", "componentKind is required"));

        var result = await _uiTopologyRepository.CreateBucketItemAsync(
            request.ComponentKey, request.SourcePath, request.ComponentKind, request.MetadataJson, ct);
        if (result.Code != UiComponentBucketCreateCode.Success || result.Record is null)
        {
            var code = result.Code switch
            {
                UiComponentBucketCreateCode.ConstraintViolation => "CONSTRAINT_VIOLATION",
                UiComponentBucketCreateCode.MalformedMetadataJson => "MALFORMED_METADATA_JSON",
                UiComponentBucketCreateCode.DbUnavailable => "REPOSITORY_UNAVAILABLE",
                _ => "BUCKET_CREATE_FAILED",
            };
            return (null, new ValidationError(code, result.Message ?? "Bucket create failed."));
        }
        var row = result.Record;
        return (JsonSerializer.SerializeToElement(new UiComponentBucketItemDto(
            row.BucketItemId.ToString(), row.ComponentKey, row.SourcePath, row.ComponentKind, row.Status
        )), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataGenerateAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (vector.Payload is null)
            return (null, new ValidationError("PAYLOAD_REQUIRED",
                "payload is required for package_generator:generate"));

        PackageGenerateRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<PackageGenerateRequestDto>(
                vector.Payload.Value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            return (null, new ValidationError("MALFORMED_PAYLOAD", ex.Message));
        }

        if (request is null)
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload could not be deserialized"));

        if (string.IsNullOrWhiteSpace(request.BucketItemId))
            return (null, new ValidationError("BUCKET_ITEM_ID_REQUIRED", "bucketItemId is required"));

        if (!Guid.TryParse(request.BucketItemId, out var bucketItemGuid))
            return (null, new ValidationError("MALFORMED_BUCKET_ITEM_ID",
                "bucketItemId must be a valid UUID"));

        if (string.IsNullOrWhiteSpace(request.RouteKey))
            return (null, new ValidationError("ROUTE_KEY_REQUIRED", "routeKey is required"));

        _logger.LogDebug(
            "AdminRuntime.DataGenerateAsync: bucketItemId={Id}, routeKey={Route}",
            bucketItemGuid, request.RouteKey);

        var result = await _packageGeneratorRuntime.GenerateFromBucketAsync(bucketItemGuid, request.RouteKey, ct);

        if (result.Code != PackageGenerateCode.Success)
        {
            var errorCode = result.Code switch
            {
                PackageGenerateCode.NotFound            => "PACKAGE_NOT_FOUND",
                PackageGenerateCode.NotBucketed         => "PACKAGE_NOT_BUCKETED",
                PackageGenerateCode.ConstraintViolation => "CONSTRAINT_VIOLATION",
                _                                       => "PACKAGE_GENERATE_FAILED"
            };
            return (null, new ValidationError(errorCode, result.Message ?? "Operation failed."));
        }

        var response = new
        {
            ok = true,
            bucketItemId = request.BucketItemId,
            routeKey = request.RouteKey,
            status = "packaging",
            message = "Package generation staged successfully."
        };
        return (JsonSerializer.SerializeToElement(response), null);
    }



    private async Task<(JsonElement? data, ValidationError? error)> DataPromoteAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (vector.Payload is null)
            return (null, new ValidationError("PAYLOAD_REQUIRED",
                "payload is required for package_generator:promote"));

        PackageGenerateRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<PackageGenerateRequestDto>(
                vector.Payload.Value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            return (null, new ValidationError("MALFORMED_PAYLOAD", ex.Message));
        }

        if (request is null)
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload could not be deserialized"));
        if (string.IsNullOrWhiteSpace(request.BucketItemId))
            return (null, new ValidationError("BUCKET_ITEM_ID_REQUIRED", "bucketItemId is required"));
        if (!Guid.TryParse(request.BucketItemId, out var bucketItemGuid))
            return (null, new ValidationError("MALFORMED_BUCKET_ITEM_ID", "bucketItemId must be a valid UUID"));
        if (string.IsNullOrWhiteSpace(request.RouteKey))
            return (null, new ValidationError("ROUTE_KEY_REQUIRED", "routeKey is required"));

        var gateError = await CheckCiAttentionPromotionGateAsync(ResolveAuthoringSurface(vector), ct);
        if (gateError is not null) return (null, gateError);

        var result = await _packageGeneratorRuntime.PromoteBucketItemAsync(bucketItemGuid, request.RouteKey, ct);
        if (result.Code != PackageGenerateCode.Success)
        {
            var errorCode = result.Code switch
            {
                PackageGenerateCode.NotFound            => "PACKAGE_NOT_FOUND",
                PackageGenerateCode.NotBucketed         => "PACKAGE_NOT_BUCKETED",
                PackageGenerateCode.ConstraintViolation => "CONSTRAINT_VIOLATION",
                PackageGenerateCode.PromotionFailed     => "PROMOTION_FAILED",
                _                                       => "PACKAGE_PROMOTE_FAILED"
            };
            return (null, new ValidationError(errorCode, result.Message ?? "Promotion failed."));
        }

        var responseDto = new PackageGenerateResponseDto(
            true,
            result.TensorId!.Value.ToString(),
            result.ComponentId!.Value.ToString(),
            result.PackageId!.Value.ToString(),
            result.LayoutId!.Value.ToString(),
            result.WiringId!.Value.ToString(),
            "Package promoted successfully.");

        return (JsonSerializer.SerializeToElement(responseDto), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataPromotePackageAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (vector.Payload is null)
            return (null, new ValidationError("PAYLOAD_REQUIRED",
                "payload is required for package_generator:promote_package"));

        PackageGenerateBatchRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<PackageGenerateBatchRequestDto>(
                vector.Payload.Value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            return (null, new ValidationError("MALFORMED_PAYLOAD", ex.Message));
        }

        if (request is null)
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload could not be deserialized"));
        if (string.IsNullOrWhiteSpace(request.RouteKey))
            return (null, new ValidationError("ROUTE_KEY_REQUIRED", "routeKey is required"));
        if (request.BucketItemIds is null)
            return (null, new ValidationError("BUCKET_ITEM_IDS_REQUIRED", "bucketItemIds is required"));

        var parsedIds = new List<Guid>();
        foreach (var raw in request.BucketItemIds)
        {
            if (!Guid.TryParse(raw, out var id))
                return (null, new ValidationError("MALFORMED_BUCKET_ITEM_ID", $"bucketItemId '{raw}' is not a valid UUID"));
            parsedIds.Add(id);
        }

        var gateError = await CheckCiAttentionPromotionGateAsync(ResolveAuthoringSurface(vector), ct);
        if (gateError is not null) return (null, gateError);

        var result = await _packageGeneratorRuntime.PromotePackageAsync(request.RouteKey, parsedIds, ct);
        if (result.Code != PackageGenerateCode.Success)
        {
            var errorCode = result.Code switch
            {
                PackageGenerateCode.NotFound            => "PACKAGE_NOT_FOUND",
                PackageGenerateCode.NotBucketed         => "PACKAGE_NOT_PACKAGED",
                PackageGenerateCode.ConstraintViolation => "CONSTRAINT_VIOLATION",
                PackageGenerateCode.PromotionFailed     => "PROMOTION_FAILED",
                _                                       => "PACKAGE_PROMOTE_FAILED"
            };
            return (null, new ValidationError(errorCode, result.Message ?? "Batch promotion failed."));
        }

        var responseDto = new PackageGenerateBatchResponseDto(
            true,
            result.TensorId!.Value.ToString(),
            result.PackageId!.Value.ToString(),
            result.LayoutId!.Value.ToString(),
            result.WiringId!.Value.ToString(),
            result.BucketItemIds.Select(id => id.ToString()).ToList(),
            result.ComponentIds.Select(id => id.ToString()).ToList(),
            "Package promoted successfully.");

        return (JsonSerializer.SerializeToElement(responseDto), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataDetachPackageComponentsAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (vector.Payload is null)
            return (null, new ValidationError("PAYLOAD_REQUIRED",
                "payload is required for package_generator:detach_package_components"));

        PackageDetachComponentsRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<PackageDetachComponentsRequestDto>(
                vector.Payload.Value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            return (null, new ValidationError("MALFORMED_PAYLOAD", ex.Message));
        }

        if (request is null)
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload could not be deserialized"));
        if (string.IsNullOrWhiteSpace(request.RouteKey))
            return (null, new ValidationError("ROUTE_KEY_REQUIRED", "routeKey is required"));
        if (request.ComponentKeys is null || request.ComponentKeys.Count == 0)
            return (null, new ValidationError("COMPONENT_KEYS_REQUIRED", "componentKeys must contain at least one item"));

        var gateError = await CheckCiAttentionPromotionGateAsync(ResolveAuthoringSurface(vector), ct);
        if (gateError is not null) return (null, gateError);

        var result = await _packageGeneratorRuntime.DetachPackageComponentsAsync(
            request.RouteKey,
            request.ComponentKeys,
            ct);
        if (result.Code != PackageGenerateCode.Success)
        {
            var errorCode = result.Code switch
            {
                PackageGenerateCode.NotFound            => "PACKAGE_NOT_FOUND",
                PackageGenerateCode.ConstraintViolation => "CONSTRAINT_VIOLATION",
                _                                       => "PACKAGE_DETACH_FAILED"
            };
            return (null, new ValidationError(errorCode, result.Message ?? "Component detach failed."));
        }

        var responseDto = new PackageDetachComponentsResponseDto(
            true,
            result.PackageId!.Value.ToString(),
            result.DetachedComponentKeys.ToList(),
            "Components detached from package.");

        return (JsonSerializer.SerializeToElement(responseDto), null);
    }

    private static bool TryParseLayoutPatchRequest(OperationVector vector, out LayoutPatchRequestDto? request, out ValidationError? error)
    {
        request = null;
        error = null;
        if (vector.Payload is null)
        {
            error = new ValidationError("PAYLOAD_REQUIRED", "payload is required for layout_patch operation");
            return false;
        }
        try
        {
            request = JsonSerializer.Deserialize<LayoutPatchRequestDto>(vector.Payload.Value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            error = new ValidationError("MALFORMED_PAYLOAD", ex.Message);
            return false;
        }
        if (request is null) { error = new ValidationError("MALFORMED_PAYLOAD", "payload could not be deserialized"); return false; }
        if (string.IsNullOrWhiteSpace(request.PackageId) || !Guid.TryParse(request.PackageId, out _))
        {
            error = new ValidationError("PACKAGE_ID_REQUIRED", "packageId is required.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(request.LayoutId)) { error = new ValidationError("LAYOUT_ID_REQUIRED", "layoutId is required"); return false; }
        if (string.IsNullOrWhiteSpace(request.RouteKey)) { error = new ValidationError("ROUTE_KEY_REQUIRED", "routeKey is required"); return false; }
        if (!Guid.TryParse(request.LayoutId, out _)) { error = new ValidationError("MALFORMED_LAYOUT_ID", "layoutId must be valid UUID"); return false; }
        return true;
    }

    private async Task<ValidationError?> VerifyLayoutPatchPackageBindingAsync(LayoutPatchRequestDto request, CancellationToken ct)
    {
        var packageId = Guid.Parse(request.PackageId);
        var layoutId = Guid.Parse(request.LayoutId);
        return await _uiTopologyRepository.VerifyLayoutPatchPackageBindingAsync(packageId, layoutId, request.RouteKey, ct);
    }

    /// <summary>layout_patch persists placement/tensor only; design tokens use component_style_design:upsert.</summary>
    private static IReadOnlyList<string> LayoutPatchCssTokenRefsStripped(IReadOnlyList<string>? _) =>
        Array.Empty<string>();

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> LayoutPatchResponsiveTokenRefsStripped(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? _) =>
        new Dictionary<string, IReadOnlyList<string>>();

    private async Task<(JsonElement? data, ValidationError? error)> DataPromotedPaletteAsync(CancellationToken ct)
    {
        try
        {
            var entries = await _uiTopologyRepository.ListPromotedPaletteEntriesAsync(ct);
            return (JsonSerializer.SerializeToElement(entries), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DataPromotedPaletteAsync failed.");
            return (null, new ValidationError("DB_UNAVAILABLE", ex.Message));
        }
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataLayoutCandidatesAsync(CancellationToken ct)
    {
        try
        {
            var candidates = await _uiTopologyRepository.ListLayoutCandidatesAsync(ct);
            return (JsonSerializer.SerializeToElement(candidates), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DataLayoutCandidatesAsync failed.");
            return (null, new ValidationError("DB_UNAVAILABLE", ex.Message));
        }
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataListPackageComponentsAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (vector.Payload is null ||
            !vector.Payload.Value.TryGetProperty("packageId", out var pkgEl) ||
            !Guid.TryParse(pkgEl.GetString(), out var packageId))
        {
            return (null, new ValidationError("PACKAGE_ID_REQUIRED", "packageId is required."));
        }
        try
        {
            var components = await _uiTopologyRepository.ListPackageComponentsAsync(packageId, ct);
            return (JsonSerializer.SerializeToElement(components), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DataListPackageComponentsAsync failed.");
            return (null, new ValidationError("DB_UNAVAILABLE", ex.Message));
        }
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataGetLayoutPatchDraftAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (vector.Payload is null ||
            !vector.Payload.Value.TryGetProperty("packageId", out var pkgEl) ||
            !Guid.TryParse(pkgEl.GetString(), out var packageId) ||
            !vector.Payload.Value.TryGetProperty("layoutId", out var layoutEl) ||
            !Guid.TryParse(layoutEl.GetString(), out var layoutId) ||
            !vector.Payload.Value.TryGetProperty("routeKey", out var routeEl) ||
            string.IsNullOrWhiteSpace(routeEl.GetString()))
        {
            return (null, new ValidationError(
                "LAYOUT_PATCH_DRAFT_PAYLOAD_INVALID",
                "packageId, layoutId, and routeKey are required."));
        }
        var routeKey = routeEl.GetString()!;
        try
        {
            var bindingError = await _uiTopologyRepository.VerifyLayoutPatchPackageBindingAsync(
                packageId, layoutId, routeKey, ct);
            if (bindingError is not null) return (null, bindingError);
            var draft = await _uiTopologyRepository.GetLayoutPatchDraftAsync(
                packageId, layoutId, routeKey, ct);
            if (draft is null)
            {
                return (null, new ValidationError(
                    "LAYOUT_PATCH_DRAFT_NOT_FOUND",
                    $"No layout_patch_json for package {packageId}, layout {layoutId}, route {routeKey}."));
            }
            return (JsonSerializer.SerializeToElement(draft), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DataGetLayoutPatchDraftAsync failed.");
            return (null, new ValidationError("DB_UNAVAILABLE", ex.Message));
        }
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataLayoutPatchSaveTmpAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (vector.Payload is null)
            return (null, new ValidationError("PAYLOAD_REQUIRED", "payload is required for layout_patch:save_tmp"));
        LayoutPatchSaveTmpRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<LayoutPatchSaveTmpRequestDto>(
                vector.Payload.Value.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            return (null, new ValidationError("MALFORMED_PAYLOAD", ex.Message));
        }
        if (request is null ||
            !Guid.TryParse(request.PackageId, out var packageId) ||
            !Guid.TryParse(request.LayoutId, out var layoutId) ||
            string.IsNullOrWhiteSpace(request.RouteKey) ||
            string.IsNullOrWhiteSpace(request.TmpJson))
        {
            return (null, new ValidationError(
                "LAYOUT_PATCH_SAVE_TMP_PAYLOAD_INVALID",
                "packageId, layoutId, routeKey, and tmpJson are required."));
        }
        try
        {
            var saveError = await _uiTopologyRepository.SaveLayoutDraftTmpAsync(
                packageId, layoutId, request.RouteKey, request.TmpJson, ct);
            if (saveError is not null) return (null, saveError);
            return (JsonSerializer.SerializeToElement(new { ok = true }), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DataLayoutPatchSaveTmpAsync failed.");
            return (null, new ValidationError("DB_UNAVAILABLE", ex.Message));
        }
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataGetPackageWiringAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (vector.Payload is null ||
            !vector.Payload.Value.TryGetProperty("packageId", out var pkgEl) ||
            !Guid.TryParse(pkgEl.GetString(), out var packageId))
        {
            return (null, new ValidationError("PACKAGE_ID_REQUIRED", "packageId is required."));
        }
        try
        {
            var wiring = await _uiTopologyRepository.GetPackageWiringAsync(packageId, ct);
            if (wiring is null)
            {
                return (null, new ValidationError(
                    "PACKAGE_WIRING_NOT_FOUND",
                    $"No wiring tensor linked to package {packageId}."));
            }
            return (JsonSerializer.SerializeToElement(wiring), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DataGetPackageWiringAsync failed.");
            return (null, new ValidationError("DB_UNAVAILABLE", ex.Message));
        }
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataUpdatePackageWiringAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (vector.Payload is null)
            return (null, new ValidationError("PAYLOAD_REQUIRED", "payload is required for ui_topology:update_package_wiring"));
        PackageWiringUpdateRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<PackageWiringUpdateRequestDto>(
                vector.Payload.Value.GetRawText());
        }
        catch (JsonException ex)
        {
            return (null, new ValidationError("MALFORMED_PAYLOAD", ex.Message));
        }
        if (request is null ||
            !Guid.TryParse(request.PackageId, out var packageId) ||
            !Guid.TryParse(request.WiringId, out var wiringId) ||
            string.IsNullOrWhiteSpace(request.WiringKind) ||
            string.IsNullOrWhiteSpace(request.TargetSurface))
        {
            return (null, new ValidationError(
                "PACKAGE_WIRING_PAYLOAD_INVALID",
                "packageId, wiringId, wiringKind, and targetSurface are required."));
        }
        // For manifest surface, targetRef must be manifest:<uuid>:<wiringKey> with non-empty wiringKey,
        // the manifest must exist, screen_data_shape must be resolved, and wiringKey must be a known candidate.
        if (request.TargetSurface.Trim().Equals("manifest", StringComparison.OrdinalIgnoreCase))
        {
            var targetRefValue = request.TargetRef?.Trim() ?? "";
            var parts = targetRefValue.Split(':', 3);
            var validFormat = parts.Length == 3
                && parts[0].Equals("manifest", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(parts[1], out var manifestTargetId)
                && !string.IsNullOrWhiteSpace(parts[2]);
            if (!validFormat)
            {
                return (null, new ValidationError(
                    "MANIFEST_WIRING_KEY_MISSING",
                    "targetRef for manifest surface must be manifest:<manifestId>:<wiringKey> with a non-empty wiringKey."));
            }
            // Re-parse now that validFormat is confirmed.
            Guid.TryParse(parts[1], out manifestTargetId);
            var wiringKeyStr = parts[2];

            if (_manifestRepository is null) return (null, ManifestRepositoryNotAvailable());

            var manifestDetail = await _manifestRepository.LoadDetailByIdAsync(manifestTargetId, ct);
            if (manifestDetail is null)
                return (null, new ValidationError("MANIFEST_NOT_FOUND",
                    $"Manifest {manifestTargetId} was not found. Cannot save wiring to an unresolved manifest."));

            var shapeEntry = ScreenDataShapeTopologyReader.FindScreenDataShapeEntry(manifestDetail.Topology);
            if (shapeEntry is null)
                return (null, new ValidationError("SCREEN_DATA_SHAPE_UNRESOLVED",
                    "Manifest has no screen_data_shape entry. Configure Step 3 before connecting wiring."));

            if (!shapeEntry.Value.TryGetProperty("screenReadQueryWiring", out var wiringRoot)
                || wiringRoot.ValueKind != JsonValueKind.Object)
                return (null, new ValidationError("SCREEN_DATA_SHAPE_UNRESOLVED",
                    "Manifest screen_data_shape has no screenReadQueryWiring. Define read/query wiring in Step 3 first."));

            var knownKeys = ScreenReadQueryWiringCandidates.GetWiringKeys(wiringRoot);
            if (knownKeys.Count == 0)
                return (null, new ValidationError("SCREEN_DATA_SHAPE_UNRESOLVED",
                    "Manifest screenReadQueryWiring has no candidates. Add searchConditions, aggregationMeasures, or displayColumns in Step 3."));

            if (!knownKeys.Contains(wiringKeyStr))
                return (null, new ValidationError("MANIFEST_WIRING_KEY_UNRESOLVED",
                    $"wiringKey '{wiringKeyStr}' is not a known candidate in the manifest's screenReadQueryWiring."));
        }
        try
        {
            var error = await _uiTopologyRepository.UpdatePackageWiringAsync(
                packageId,
                wiringId,
                request.WiringKind.Trim(),
                request.TargetSurface.Trim(),
                string.IsNullOrWhiteSpace(request.TargetRef) ? null : request.TargetRef.Trim(),
                ct);
            if (error is not null) return (null, error);
            var wiring = await _uiTopologyRepository.GetPackageWiringAsync(packageId, ct);
            return (JsonSerializer.SerializeToElement(new { ok = true, wiring }), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DataUpdatePackageWiringAsync failed.");
            return (null, new ValidationError("DB_UNAVAILABLE", ex.Message));
        }
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataListAdminPackagesAsync(CancellationToken ct)
    {
        try
        {
            var packages = await _uiTopologyRepository.ListAdminPackagesAsync(ct);
            return (JsonSerializer.SerializeToElement(packages), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DataListAdminPackagesAsync failed.");
            return (null, new ValidationError("DB_UNAVAILABLE", ex.Message));
        }
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataListComponentStyleDesignsAsync(
        OperationVector vector, CancellationToken ct)
    {
        Guid? packageId = null;
        if (vector.Payload.HasValue &&
            vector.Payload.Value.TryGetProperty("packageId", out var pkgEl) &&
            Guid.TryParse(pkgEl.GetString(), out var parsed))
        {
            packageId = parsed;
        }
        try
        {
            var designs = await _uiTopologyRepository.ListComponentStyleDesignsAsync(packageId, ct);
            return (JsonSerializer.SerializeToElement(designs), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DataListComponentStyleDesignsAsync failed.");
            return (null, new ValidationError("DB_UNAVAILABLE", ex.Message));
        }
    }


    private static object BuildComponentStyleDesignObject(
        Guid? componentId,
        string? layoutNodeId,
        string? classname,
        string? tailwind,
        IReadOnlyList<string>? cssTokenRefs,
        Dictionary<string, IReadOnlyList<string>>? responsiveTokenRefs,
        string? inlineText,
        string? linkHref,
        string? linkTarget,
        string? reactionIntent) => new
    {
        componentId = componentId?.ToString(),
        layoutNodeId,
        classname = classname ?? "",
        tailwind = tailwind ?? "",
        cssTokenRefs = cssTokenRefs ?? Array.Empty<string>(),
        responsiveTokenRefs = responsiveTokenRefs ?? new Dictionary<string, IReadOnlyList<string>>(),
        inlineText = inlineText ?? "",
        linkHref = linkHref ?? "",
        linkTarget = linkTarget ?? "",
        reactionIntent = reactionIntent ?? "",
    };

    private static ValidationError? ValidateComponentStyleDesignTarget(
        string packageIdText,
        string? componentIdText,
        string? layoutNodeIdText,
        string name,
        out Guid packageId,
        out Guid? componentId,
        out string? layoutNodeId)
    {
        packageId = Guid.Empty;
        componentId = null;
        layoutNodeId = string.IsNullOrWhiteSpace(layoutNodeIdText) ? null : layoutNodeIdText.Trim();
        if (!Guid.TryParse(packageIdText, out packageId) || string.IsNullOrWhiteSpace(name))
        {
            return new ValidationError("COMPONENT_DESIGN_PAYLOAD_INVALID", "packageId and name are required.");
        }
        if (!string.IsNullOrWhiteSpace(componentIdText))
        {
            if (!Guid.TryParse(componentIdText, out var parsedComponentId))
            {
                return new ValidationError("COMPONENT_DESIGN_PAYLOAD_INVALID", "componentId must be a valid UUID when provided.");
            }
            componentId = parsedComponentId;
        }
        if (componentId is null && layoutNodeId is null)
        {
            return new ValidationError("COMPONENT_DESIGN_PAYLOAD_INVALID", "componentId or layoutNodeId is required.");
        }
        return null;
    }

    private async Task<ValidationError?> VerifyComponentStyleLayoutNodeTargetAsync(
        Guid packageId,
        string? layoutNodeId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(layoutNodeId))
            return null;
        return await _uiTopologyRepository.VerifyPackageLayoutNodeAsync(
            packageId, layoutNodeId.Trim(), ct);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataSaveComponentStyleDesignTmpAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (vector.Payload is null)
            return (null, new ValidationError("PAYLOAD_REQUIRED", "payload is required for component_style_design:save_tmp"));
        ComponentStyleDesignSaveTmpRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<ComponentStyleDesignSaveTmpRequestDto>(
                vector.Payload.Value.GetRawText());
        }
        catch (JsonException ex)
        {
            return (null, new ValidationError("MALFORMED_PAYLOAD", ex.Message));
        }
        if (request is null)
        {
            return (null, new ValidationError("COMPONENT_DESIGN_PAYLOAD_INVALID", "payload could not be deserialized"));
        }
        var targetError = ValidateComponentStyleDesignTarget(
            request.PackageId, request.ComponentId, request.LayoutNodeId, request.Name,
            out var packageId, out var componentId, out var layoutNodeId);
        if (targetError is not null) return (null, targetError);

        var designJson = JsonSerializer.Serialize(BuildComponentStyleDesignObject(
            componentId, layoutNodeId, request.Classname, request.Tailwind, request.CssTokenRefs,
            request.ResponsiveTokenRefs, request.InlineText, request.LinkHref, request.LinkTarget, request.ReactionIntent));
        try
        {
            var layoutNodeError = await VerifyComponentStyleLayoutNodeTargetAsync(packageId, layoutNodeId, ct);
            if (layoutNodeError is not null) return (null, layoutNodeError);
            var (designId, error) = await _uiTopologyRepository.SaveComponentStyleDesignDraftTmpForPackageAsync(
                packageId, componentId, layoutNodeId, request.Name.Trim(), designJson, ct);
            if (error is not null) return (null, error);
            return (JsonSerializer.SerializeToElement(new { ok = true, designId = designId.ToString() }), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DataSaveComponentStyleDesignTmpAsync failed.");
            return (null, new ValidationError("DB_UNAVAILABLE", ex.Message));
        }
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataUpsertComponentStyleDesignAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (vector.Payload is null)
            return (null, new ValidationError("PAYLOAD_REQUIRED", "payload is required for component_style_design:upsert"));
        ComponentStyleDesignUpsertRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<ComponentStyleDesignUpsertRequestDto>(
                vector.Payload.Value.GetRawText());
        }
        catch (JsonException ex)
        {
            return (null, new ValidationError("MALFORMED_PAYLOAD", ex.Message));
        }
        if (request is null)
        {
            return (null, new ValidationError("COMPONENT_DESIGN_PAYLOAD_INVALID", "payload could not be deserialized"));
        }
        var targetError = ValidateComponentStyleDesignTarget(
            request.PackageId, request.ComponentId, request.LayoutNodeId, request.Name,
            out var packageId, out var componentId, out var layoutNodeId);
        if (targetError is not null) return (null, targetError);

        var designJson = JsonSerializer.Serialize(BuildComponentStyleDesignObject(
            componentId, layoutNodeId, request.Classname, request.Tailwind, request.CssTokenRefs,
            request.ResponsiveTokenRefs, request.InlineText, request.LinkHref, request.LinkTarget, request.ReactionIntent));
        try
        {
            var layoutNodeError = await VerifyComponentStyleLayoutNodeTargetAsync(packageId, layoutNodeId, ct);
            if (layoutNodeError is not null) return (null, layoutNodeError);
            var (designId, error) = await _uiTopologyRepository.UpsertComponentStyleDesignForPackageAsync(
                packageId, componentId, layoutNodeId, request.Name.Trim(), designJson, ct);
            if (error is not null) return (null, error);
            return (JsonSerializer.SerializeToElement(new { ok = true, designId = designId.ToString() }), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DataUpsertComponentStyleDesignAsync failed.");
            return (null, new ValidationError("DB_UNAVAILABLE", ex.Message));
        }
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataLayoutPatchPreviewAsync(OperationVector vector, CancellationToken ct)
    {
        if (!TryParseLayoutPatchRequest(vector, out var req, out var err)) return (null, err);
        var bindingError = await VerifyLayoutPatchPackageBindingAsync(req!, ct);
        if (bindingError is not null) return (null, bindingError);
        var result = await _uiTopologyRepository.PreviewLayoutPatchAsync(
            Guid.Parse(req!.LayoutId), req.RouteKey, req.TensorPatchJson,
            LayoutPatchCssTokenRefsStripped(req.CssTokenRefs),
            LayoutPatchResponsiveTokenRefsStripped(req.ResponsiveTokenRefs), ct);
        return (JsonSerializer.SerializeToElement(result), null);
    }
    private async Task<(JsonElement? data, ValidationError? error)> DataLayoutPatchValidateAsync(OperationVector vector, CancellationToken ct)
    {
        if (!TryParseLayoutPatchRequest(vector, out var req, out var err)) return (null, err);
        var bindingError = await VerifyLayoutPatchPackageBindingAsync(req!, ct);
        if (bindingError is not null) return (null, bindingError);
        var result = await _uiTopologyRepository.ValidateLayoutPatchAsync(
            Guid.Parse(req!.LayoutId), req.RouteKey, req.TensorPatchJson,
            LayoutPatchCssTokenRefsStripped(req.CssTokenRefs),
            LayoutPatchResponsiveTokenRefsStripped(req.ResponsiveTokenRefs), ct);
        if (!result.Ok || !result.Valid) return (null, new ValidationError("LAYOUT_PATCH_VALIDATION_FAILED", result.Message));
        return (JsonSerializer.SerializeToElement(result), null);
    }
    private async Task<(JsonElement? data, ValidationError? error)> DataLayoutPatchApplyAsync(OperationVector vector, CancellationToken ct)
    {
        if (!TryParseLayoutPatchRequest(vector, out var req, out var err)) return (null, err);
        var bindingError = await VerifyLayoutPatchPackageBindingAsync(req!, ct);
        if (bindingError is not null) return (null, bindingError);
        var gateError = await CheckCiAttentionPromotionGateAsync(ResolveAuthoringSurface(vector), ct);
        if (gateError is not null) return (null, gateError);
        var result = await _uiTopologyRepository.ApplyConfirmedLayoutPatchAsync(
            Guid.Parse(req!.PackageId),
            Guid.Parse(req.LayoutId),
            req.RouteKey,
            req.TensorPatchJson,
            LayoutPatchCssTokenRefsStripped(req.CssTokenRefs),
            LayoutPatchResponsiveTokenRefsStripped(req.ResponsiveTokenRefs),
            ct);
        if (!result.Ok || !result.Valid) return (null, new ValidationError("LAYOUT_PATCH_APPLY_FAILED", result.Message));
        return (JsonSerializer.SerializeToElement(result), null);
    }
    // ---------------------------------------------------------------------------
    // Seed Runtime operations — Issue #84
    // ---------------------------------------------------------------------------

    private ValidationError SeedRuntimeNotAvailable() =>
        new("SEED_RUNTIME_NOT_AVAILABLE",
            "SeedRuntime is not configured. Ensure SEED_STORAGE_PATH is set and /storage is mounted.");

    private async Task<(JsonElement? data, ValidationError? error)> DataSeedSaveAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_seedRuntime is null) return (null, SeedRuntimeNotAvailable());

        if (vector.Payload is null)
            return (null, new ValidationError("PAYLOAD_REQUIRED",
                "payload with content field is required for seed_runtime:save"));

        string? content;
        try
        {
            if (!vector.Payload.Value.TryGetProperty("content", out var contentEl) ||
                contentEl.ValueKind != JsonValueKind.String)
                return (null, new ValidationError("CONTENT_REQUIRED",
                    "payload.content (string) is required"));
            content = contentEl.GetString();
        }
        catch (Exception ex)
        {
            return (null, new ValidationError("MALFORMED_PAYLOAD", ex.Message));
        }

        if (string.IsNullOrWhiteSpace(content))
            return (null, new ValidationError("CONTENT_EMPTY", "payload.content must not be empty"));

        var result = await _seedRuntime.SaveAsync(content, ct);
        if (!result.Success)
            return (null, new ValidationError(result.ErrorCode!, result.ErrorMessage!));

        return (JsonSerializer.SerializeToElement(
            new SeedSaveResponseDto(true)), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataSeedLoadAsync(
        CancellationToken ct)
    {
        if (_seedRuntime is null) return (null, SeedRuntimeNotAvailable());

        var result = await _seedRuntime.LoadAsync(ct);
        if (!result.Success)
            return (null, new ValidationError(result.ErrorCode!, result.ErrorMessage!));

        return (JsonSerializer.SerializeToElement(
            new SeedLoadResponseDto(true, result.Json)), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataSeedValidateAsync(
        CancellationToken ct)
    {
        if (_seedRuntime is null) return (null, SeedRuntimeNotAvailable());

        var result = await _seedRuntime.ValidateAsync(ct);
        var errorDtos = result.Errors
            .Select(e => new SeedValidationErrorDto(e.Code, e.Message))
            .ToList();

        return (JsonSerializer.SerializeToElement(
            new SeedValidationResponseDto(true, result.IsValid, errorDtos)), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataSeedPreviewAsync(
        CancellationToken ct)
    {
        if (_seedRuntime is null) return (null, SeedRuntimeNotAvailable());

        var result = await _seedRuntime.PreviewAsync(ct);
        if (!result.Success)
        {
            var errDtos = result.Errors
                .Select(e => new SeedValidationErrorDto(e.Code, e.Message))
                .ToList();
            return (null, new ValidationError(errDtos[0].Code, errDtos[0].Message));
        }

        var runtimeDtos = result.Data!.Runtimes
            .Select(r => new SeedRuntimePreviewDto(r.Name, r.Target, r.Layer, r.Action))
            .ToList();

        return (JsonSerializer.SerializeToElement(
            new SeedPreviewResponseDto(true, result.Data.RuntimeCount, runtimeDtos, [])), null);
    }

    // ---------------------------------------------------------------------------
    // Admin CSV/JSON Import operations — M6 validate-preview-apply boundary
    // ---------------------------------------------------------------------------

    private ValidationError ImportRuntimeNotAvailable() =>
        new("ADMIN_IMPORT_RUNTIME_NOT_AVAILABLE",
            "AdminImportRuntime is not configured. Ensure the import repository is registered.");

    private async Task<(JsonElement? data, ValidationError? error)> DataImportUploadPreviewAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_adminImportRuntime is null) return (null, ImportRuntimeNotAvailable());
        if (vector.Payload is null)
            return (null, new ValidationError("PAYLOAD_REQUIRED", "payload is required for admin_csv_json_import:upload_preview"));

        AdminImportUploadPreviewRequestDto? req;
        try
        {
            req = JsonSerializer.Deserialize<AdminImportUploadPreviewRequestDto>(
                vector.Payload.Value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            return (null, new ValidationError("MALFORMED_PAYLOAD", ex.Message));
        }
        if (req is null)
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload could not be deserialized"));
        if (string.IsNullOrWhiteSpace(req.SourceType))
            return (null, new ValidationError("SOURCE_TYPE_REQUIRED", "sourceType is required"));
        if (string.IsNullOrWhiteSpace(req.FileName))
            return (null, new ValidationError("FILE_NAME_REQUIRED", "fileName is required"));
        if (string.IsNullOrWhiteSpace(req.ManifestId))
            return (null, new ValidationError("MANIFEST_ID_REQUIRED", "manifestId is required"));
        if (string.IsNullOrWhiteSpace(req.SchemaId))
            return (null, new ValidationError("SCHEMA_ID_REQUIRED", "schemaId is required"));
        if (string.IsNullOrWhiteSpace(req.Content))
            return (null, new ValidationError("CONTENT_EMPTY", "content must not be empty"));
        if (!Guid.TryParse(req.ManifestId, out var manifestGuid))
            return (null, new ValidationError("MALFORMED_MANIFEST_ID", "manifestId must be a valid UUID"));
        if (!Guid.TryParse(req.SchemaId, out var schemaGuid))
            return (null, new ValidationError("MALFORMED_SCHEMA_ID", "schemaId must be a valid UUID"));

        var result = await _adminImportRuntime.PreviewAsync(
            req.SourceType, req.FileName, manifestGuid, schemaGuid, req.Content, ct);

        if (!result.Success)
            return (null, new ValidationError(result.ErrorCode!, result.ErrorMessage!));

        var recordDtos = result.Records.Select(r => new AdminImportRecordPreviewDto(
            r.RowIndex, r.Records, r.Status, r.ValidationErrors)).ToList();

        var response = new AdminImportUploadPreviewResponseDto(
            Ok: true,
            SnapshotId: result.SnapshotId!,
            SourceType: req.SourceType,
            ManifestId: req.ManifestId,
            SchemaId: req.SchemaId,
            ValidCount: result.ValidCount,
            InvalidCount: result.InvalidCount,
            Records: recordDtos);

        return (JsonSerializer.SerializeToElement(response), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataImportApplyAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_adminImportRuntime is null) return (null, ImportRuntimeNotAvailable());
        if (vector.Payload is null)
            return (null, new ValidationError("PAYLOAD_REQUIRED", "payload is required for admin_csv_json_import:apply"));

        AdminImportApplyRequestDto? req;
        try
        {
            req = JsonSerializer.Deserialize<AdminImportApplyRequestDto>(
                vector.Payload.Value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            return (null, new ValidationError("MALFORMED_PAYLOAD", ex.Message));
        }
        if (req is null)
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload could not be deserialized"));
        if (string.IsNullOrWhiteSpace(req.SnapshotId))
            return (null, new ValidationError("SNAPSHOT_ID_REQUIRED", "snapshotId is required"));
        if (!Guid.TryParse(req.SnapshotId, out var snapshotGuid))
            return (null, new ValidationError("MALFORMED_SNAPSHOT_ID", "snapshotId must be a valid UUID"));

        var result = await _adminImportRuntime.ApplyAsync(snapshotGuid, ct);
        if (!result.Success)
            return (null, new ValidationError(result.ErrorCode!, result.ErrorMessage!));

        var response = new AdminImportApplyResponseDto(
            Ok: true,
            ApplyLogId: result.ApplyLogId!,
            SnapshotId: result.SnapshotId!,
            AppliedRecordCount: result.AppliedRecordCount,
            Status: result.Status,
            Note: result.Note ?? "");

        return (JsonSerializer.SerializeToElement(response), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataImportListManifestsAsync(
        CancellationToken ct)
    {
        if (_adminImportRuntime is null) return (null, ImportRuntimeNotAvailable());
        var manifests = await _adminImportRuntime.ListManifestsAsync(ct);
        var dtos = manifests.Select(m => new AdminImportManifestListItemDto(
            m.ManifestId.ToString(),
            m.Status,
            m.CreatedAt.ToString("o"),
            m.ManifestKey,
            m.HubId)).ToList();
        return (JsonSerializer.SerializeToElement(dtos), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataImportListSchemasAsync(
        CancellationToken ct)
    {
        if (_adminImportRuntime is null) return (null, ImportRuntimeNotAvailable());
        var schemas = await _adminImportRuntime.ListSchemasAsync(ct);
        var dtos = schemas.Select(s => new AdminImportSchemaListItemDto(
            s.SchemaId.ToString(), s.Name)).ToList();
        return (JsonSerializer.SerializeToElement(dtos), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataImportListSnapshotRecordsAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_adminImportRuntime is null) return (null, ImportRuntimeNotAvailable());
        if (vector.Payload is null)
            return (null, new ValidationError("PAYLOAD_REQUIRED",
                "payload is required for admin_csv_json_import:list_snapshot_records"));

        AdminImportListSnapshotRecordsRequestDto? req;
        try
        {
            req = JsonSerializer.Deserialize<AdminImportListSnapshotRecordsRequestDto>(
                vector.Payload.Value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            return (null, new ValidationError("MALFORMED_PAYLOAD", ex.Message));
        }
        if (req is null)
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload could not be deserialized"));
        if (string.IsNullOrWhiteSpace(req.SnapshotId))
            return (null, new ValidationError("SNAPSHOT_ID_REQUIRED", "snapshotId is required"));
        if (!Guid.TryParse(req.SnapshotId, out var snapshotGuid))
            return (null, new ValidationError("MALFORMED_SNAPSHOT_ID", "snapshotId must be a valid UUID"));

        var result = await _adminImportRuntime.ListSnapshotRecordsAsync(snapshotGuid, ct);
        if (!result.Success)
            return (null, new ValidationError(result.ErrorCode!, result.ErrorMessage!));

        var recordDtos = result.Records.Select(r => new AdminImportRecordPreviewDto(
            r.RowIndex, r.Records, r.Status, r.ValidationErrors)).ToList();

        var response = new AdminImportUploadPreviewResponseDto(
            Ok: true,
            SnapshotId: result.SnapshotId!,
            SourceType: "",
            ManifestId: "",
            SchemaId: "",
            ValidCount: recordDtos.Count(r => r.Status == "valid"),
            InvalidCount: recordDtos.Count(r => r.Status == "invalid"),
            Records: recordDtos);

        return (JsonSerializer.SerializeToElement(response), null);
    }

    private ValidationError ManifestRepositoryNotAvailable() =>
        new("MANIFEST_REPOSITORY_NOT_AVAILABLE", "Manifest repository is not registered.");

    private async Task<(JsonElement? data, ValidationError? error)> DataManifestListAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_manifestRepository is null) return (null, ManifestRepositoryNotAvailable());

        string? status = null;
        if (vector.Payload is { ValueKind: JsonValueKind.Object } payload &&
            payload.TryGetProperty("status", out var statusEl) &&
            statusEl.ValueKind == JsonValueKind.String)
        {
            status = statusEl.GetString();
        }

        var items = await _manifestRepository.ListManifestsAsync(status, ct);
        var dtos = items.Select(m => new AdminManifestListItemDto(
            m.ManifestId.ToString(),
            m.Status,
            m.RelationRegistryId?.ToString(),
            m.Role,
            m.Target,
            m.Layer,
            m.Action,
            m.RuntimeDestination,
            m.CreatedAt.ToString("o"),
            m.UpdatedAt.ToString("o"))).ToList();

        return (JsonSerializer.SerializeToElement(dtos), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataManifestGetAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_manifestRepository is null) return (null, ManifestRepositoryNotAvailable());
        if (!TryParseManifestId(vector, out var manifestId, out var parseError))
            return (null, parseError);

        var detail = await _manifestRepository.LoadDetailByIdAsync(manifestId, ct);
        if (detail is null)
            return (null, new ValidationError("MANIFEST_NOT_FOUND", $"Manifest {manifestId} was not found."));

        return (JsonSerializer.SerializeToElement(ToManifestDetailDto(detail)), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataManifestListScreenReadQueryWiringAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_manifestRepository is null) return (null, ManifestRepositoryNotAvailable());
        if (!TryParseManifestId(vector, out var manifestId, out var parseError))
            return (null, parseError);

        var detail = await _manifestRepository.LoadDetailByIdAsync(manifestId, ct);
        if (detail is null)
            return (null, new ValidationError("MANIFEST_NOT_FOUND", $"Manifest {manifestId} was not found."));

        var shapeEntry = ScreenDataShapeTopologyReader.FindScreenDataShapeEntry(detail.Topology);
        if (shapeEntry is null)
        {
            return (JsonSerializer.SerializeToElement(new { candidates = Array.Empty<object>() }), null);
        }

        if (!shapeEntry.Value.TryGetProperty("screenReadQueryWiring", out var wiring) ||
            wiring.ValueKind != JsonValueKind.Object)
        {
            return (JsonSerializer.SerializeToElement(new { candidates = Array.Empty<object>() }), null);
        }

        var candidates = ScreenReadQueryWiringCandidates.Flatten(wiring);
        return (JsonSerializer.SerializeToElement(new { candidates }), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataManifestValidateAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_manifestRepository is null) return (null, ManifestRepositoryNotAvailable());
        if (!TryParseManifestId(vector, out var manifestId, out var parseError))
            return (null, parseError);

        var detail = await _manifestRepository.LoadDetailByIdAsync(manifestId, ct);
        if (detail is null)
            return (null, new ValidationError("MANIFEST_NOT_FOUND", $"Manifest {manifestId} was not found."));

        var summary = ManifestTopologyValidator.ExtractSummary(detail.Topology);
        var conflictCount = 0;
        if (summary.DispatcherMapping is not null)
        {
            conflictCount = await _manifestRepository.CountActiveAxisConflictsAsync(
                summary.DispatcherMapping.Role,
                summary.DispatcherMapping.Target,
                summary.DispatcherMapping.Layer,
                summary.DispatcherMapping.Action,
                detail.Status.Equals("active", StringComparison.OrdinalIgnoreCase) ? detail.ManifestId : null,
                ct);
        }

        var checkConflict = !detail.Status.Equals("deprecated", StringComparison.OrdinalIgnoreCase);
        var validation = ManifestTopologyValidator.Validate(
            detail.Topology,
            KnownRuntimeDestinations,
            checkActiveAxisConflict: checkConflict,
            activeAxisConflictCount: conflictCount);

        var response = new AdminManifestValidateResponseDto(
            validation.Valid,
            validation.IsBlocking,
            validation.Errors.Select(e => new AdminManifestValidationIssueDto(e.Code, e.Message, true)).ToList(),
            ManifestTopologyValidator.ToDto(validation.Summary));

        return (JsonSerializer.SerializeToElement(response), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataManifestCreateDraftAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_manifestRepository is null) return (null, ManifestRepositoryNotAvailable());
        if (!TryParseDraftRequest(vector, out var request, out var parseError))
            return (null, parseError);

        var role = request!.Role;
        var target = request.Target;
        var layer = request.Layer;
        var action = request.Action;
        var runtimeDestination = request.RuntimeDestination;
        if (ManifestScreenOperationDeriver.TryDeriveAxes(
                request.ScreenOperationKind,
                manifestKey: null,
                manifestId: null,
                out var derivedRole,
                out var derivedTarget,
                out var derivedLayer,
                out var derivedAction,
                out var derivedRuntime))
        {
            role = derivedRole;
            target = derivedTarget;
            layer = derivedLayer;
            action = derivedAction;
            runtimeDestination = derivedRuntime;
        }

        var topology = ManifestTopologyValidator.BuildTopology(
            role,
            target,
            layer,
            action,
            runtimeDestination,
            request.ProjectionDefinition);

        var validation = ManifestTopologyValidator.Validate(topology, KnownRuntimeDestinations);
        if (validation.IsBlocking)
            return (null, validation.Errors[0]);

        Guid? relationRegistryId = null;
        if (!string.IsNullOrWhiteSpace(request.RelationRegistryId))
        {
            if (!Guid.TryParse(request.RelationRegistryId, out var relId))
            {
                return (null, new ValidationError(
                    "MALFORMED_RELATION_REGISTRY_ID",
                    "relationRegistryId must be a valid UUID when provided."));
            }
            relationRegistryId = relId;
        }

        var (manifest, error) = await _manifestRepository.CreateDraftAsync(relationRegistryId, topology, ct);
        if (error is not null) return (null, error);

        var (refreshed, refreshError) = await RefreshManifestDispatcherFromExtensionsAsync(manifest!, ct);
        if (refreshError is not null) return (null, refreshError);
        manifest = refreshed ?? manifest;

        return (JsonSerializer.SerializeToElement(ToManifestDetailDto(manifest!)), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataManifestUpdateDraftAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_manifestRepository is null) return (null, ManifestRepositoryNotAvailable());
        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object)
            return (null, new ValidationError("MANIFEST_PAYLOAD_REQUIRED", "payload is required."));

        AdminManifestUpdateDraftRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<AdminManifestUpdateDraftRequestDto>(vector.Payload.Value.GetRawText());
        }
        catch (JsonException)
        {
            return (null, new ValidationError("MANIFEST_PAYLOAD_MALFORMED", "payload could not be parsed."));
        }

        if (request is null || string.IsNullOrWhiteSpace(request.ManifestId))
            return (null, new ValidationError("MANIFEST_ID_REQUIRED", "manifestId is required."));
        if (!Guid.TryParse(request.ManifestId, out var manifestId))
            return (null, new ValidationError("MALFORMED_MANIFEST_ID", "manifestId must be a valid UUID."));
        if (string.IsNullOrWhiteSpace(request.Role) || string.IsNullOrWhiteSpace(request.Target) ||
            string.IsNullOrWhiteSpace(request.Layer) || string.IsNullOrWhiteSpace(request.Action))
        {
            return (null, new ValidationError("DISPATCHER_AXES_REQUIRED", "role, target, layer, and action are required."));
        }
        if (string.IsNullOrWhiteSpace(request.RuntimeDestination))
        {
            return (null, new ValidationError("RUNTIME_DESTINATION_REQUIRED", "runtimeDestination is required."));
        }

        var topology = ManifestTopologyValidator.BuildTopology(
            request.Role,
            request.Target,
            request.Layer,
            request.Action,
            request.RuntimeDestination,
            request.ProjectionDefinition);

        var existingDetail = await _manifestRepository.LoadDetailByIdAsync(manifestId, ct);
        var promotionEntry = existingDetail is not null
            ? PromotionManifestValidator.ExtractEntry(existingDetail.Topology)
            : null;
        if (promotionEntry is not null)
            topology = PromotionManifestValidator.MergeIntoTopology(topology, promotionEntry.Value);

        var validation = ManifestTopologyValidator.Validate(topology, KnownRuntimeDestinations);
        if (validation.IsBlocking)
            return (null, validation.Errors[0]);

        Guid? relationRegistryId = null;
        if (!string.IsNullOrWhiteSpace(request.RelationRegistryId))
        {
            if (!Guid.TryParse(request.RelationRegistryId, out var relId))
            {
                return (null, new ValidationError(
                    "MALFORMED_RELATION_REGISTRY_ID",
                    "relationRegistryId must be a valid UUID when provided."));
            }
            relationRegistryId = relId;
        }

        var (manifest, error) = await _manifestRepository.UpdateDraftAsync(
            manifestId, relationRegistryId, topology, ct);
        if (error is not null) return (null, error);
        return (JsonSerializer.SerializeToElement(ToManifestDetailDto(manifest!)), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataManifestPromoteAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_manifestRepository is null) return (null, ManifestRepositoryNotAvailable());
        if (!TryParseManifestId(vector, out var manifestId, out var parseError))
            return (null, parseError);

        var existingDetail = await _manifestRepository.LoadDetailByIdAsync(manifestId, ct);
        if (existingDetail is not null &&
            PromotionManifestValidator.ExtractEntry(existingDetail.Topology) is not null)
        {
            if (_topologyRepository is null)
            {
                return (JsonSerializer.SerializeToElement(new AdminManifestLifecycleResponseDto(
                    false, manifestId.ToString(), "draft", "Topology repository is not registered.",
                    "TOPOLOGY_REPOSITORY_NOT_AVAILABLE")), null);
            }

            var metadata = PromotionManifestValidator.ExtractMetadataDto(existingDetail.Topology);
            var conflictCount = metadata is not null
                ? await _manifestRepository.CountActivePromotionKeyConflictsAsync(
                    metadata.ManifestKey, metadata.VersionLabel, manifestId, ct)
                : 0;
            var promotionValidation = await PromotionManifestValidator.ValidateAsync(
                existingDetail.Topology, _topologyRepository, conflictCount, ct);
            if (promotionValidation.IsBlocking)
            {
                var first = promotionValidation.Errors[0];
                return (JsonSerializer.SerializeToElement(new AdminManifestLifecycleResponseDto(
                    false, manifestId.ToString(), "draft", first.Message, first.Code)), null);
            }
        }

        var (manifest, error) = await _manifestRepository.PromoteAsync(manifestId, KnownRuntimeDestinations, ct);
        if (error is not null)
        {
            return (JsonSerializer.SerializeToElement(new AdminManifestLifecycleResponseDto(
                false, manifestId.ToString(), "draft", error.Message, error.Code)), null);
        }

        return (JsonSerializer.SerializeToElement(new AdminManifestLifecycleResponseDto(
            true, manifest!.ManifestId.ToString(), manifest.Status, "Manifest promoted to active.")), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataManifestDeprecateAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_manifestRepository is null) return (null, ManifestRepositoryNotAvailable());
        if (!TryParseManifestId(vector, out var manifestId, out var parseError))
            return (null, parseError);

        var (manifest, error) = await _manifestRepository.DeprecateAsync(manifestId, ct);
        if (error is not null)
        {
            return (JsonSerializer.SerializeToElement(new AdminManifestLifecycleResponseDto(
                false, manifestId.ToString(), "active", error.Message, error.Code)), null);
        }

        return (JsonSerializer.SerializeToElement(new AdminManifestLifecycleResponseDto(
            true, manifest!.ManifestId.ToString(), manifest.Status, "Manifest deprecated.")), null);
    }

    private ValidationError TopologyRepositoryNotAvailable() =>
        new("TOPOLOGY_REPOSITORY_NOT_AVAILABLE", "Topology repository is not registered.");

    private async Task<(JsonElement? data, ValidationError? error)> DataPromotionManifestListAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_manifestRepository is null) return (null, ManifestRepositoryNotAvailable());

        string? status = null;
        if (vector.Payload is { ValueKind: JsonValueKind.Object } payload &&
            payload.TryGetProperty("status", out var statusEl) &&
            statusEl.ValueKind == JsonValueKind.String)
        {
            status = statusEl.GetString();
        }

        var items = await _manifestRepository.ListPromotionManifestsAsync(status, ct);
        var dtos = items.Select(i => new AdminPromotionManifestListItemDto(
            i.ManifestId.ToString(),
            i.Status,
            i.ManifestKey,
            i.VersionLabel,
            i.HasDisclosure,
            i.CreatedAt.ToString("o"),
            i.UpdatedAt.ToString("o"))).ToList();
        return (JsonSerializer.SerializeToElement(dtos), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataPromotionManifestGetAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_manifestRepository is null) return (null, ManifestRepositoryNotAvailable());
        if (!TryParseManifestId(vector, out var manifestId, out var parseError))
            return (null, parseError);

        var detail = await _manifestRepository.LoadDetailByIdAsync(manifestId, ct);
        if (detail is null)
            return (null, new ValidationError("MANIFEST_NOT_FOUND", $"Manifest {manifestId} was not found."));

        return (JsonSerializer.SerializeToElement(new AdminPromotionManifestDetailDto(
            detail.ManifestId.ToString(),
            detail.Status,
            PromotionManifestValidator.ExtractMetadataDto(detail.Topology),
            detail.CreatedAt.ToString("o"),
            detail.UpdatedAt.ToString("o"))), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataPromotionManifestValidateAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_manifestRepository is null) return (null, ManifestRepositoryNotAvailable());
        if (_topologyRepository is null) return (null, TopologyRepositoryNotAvailable());
        if (!TryParseManifestId(vector, out var manifestId, out var parseError))
            return (null, parseError);

        var detail = await _manifestRepository.LoadDetailByIdAsync(manifestId, ct);
        if (detail is null)
            return (null, new ValidationError("MANIFEST_NOT_FOUND", $"Manifest {manifestId} was not found."));

        var metadata = PromotionManifestValidator.ExtractMetadataDto(detail.Topology);
        var conflictCount = metadata is not null
            ? await _manifestRepository.CountActivePromotionKeyConflictsAsync(
                metadata.ManifestKey,
                metadata.VersionLabel,
                detail.Status.Equals("active", StringComparison.OrdinalIgnoreCase) ? detail.ManifestId : null,
                ct)
            : 0;

        var validation = await PromotionManifestValidator.ValidateAsync(
            detail.Topology, _topologyRepository, conflictCount, ct);

        var response = new AdminPromotionManifestValidateResponseDto(
            validation.Valid,
            validation.IsBlocking,
            validation.Errors.Select(e => new AdminManifestValidationIssueDto(e.Code, e.Message, true)).ToList(),
            validation.Metadata);

        return (JsonSerializer.SerializeToElement(response), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataPromotionManifestUpdateDraftAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_manifestRepository is null) return (null, ManifestRepositoryNotAvailable());
        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object)
            return (null, new ValidationError("PROMOTION_MANIFEST_PAYLOAD_REQUIRED", "payload is required."));

        AdminPromotionManifestUpdateDraftRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<AdminPromotionManifestUpdateDraftRequestDto>(
                vector.Payload.Value.GetRawText());
        }
        catch (JsonException)
        {
            return (null, new ValidationError("PROMOTION_MANIFEST_PAYLOAD_MALFORMED", "payload could not be parsed."));
        }

        if (request is null || string.IsNullOrWhiteSpace(request.ManifestId))
            return (null, new ValidationError("MANIFEST_ID_REQUIRED", "manifestId is required."));
        if (!Guid.TryParse(request.ManifestId, out var manifestId))
            return (null, new ValidationError("MALFORMED_MANIFEST_ID", "manifestId must be a valid UUID."));

        var metadata = new AdminPromotionManifestMetadataDto(
            request.ManifestKey,
            request.VersionLabel,
            request.DisclosureText,
            request.DisclosureCategoryLabel,
            request.PlacementKey,
            request.ProjectionSurfaceType,
            request.ActivationPolicyType,
            request.ActivationConditionExpression,
            request.TargetTopologyRefs);

        var entry = PromotionManifestValidator.BuildEntry(metadata);
        var (manifest, error) = await _manifestRepository.UpdatePromotionMetadataDraftAsync(manifestId, entry, ct);
        if (error is not null) return (null, error);

        return (JsonSerializer.SerializeToElement(new AdminPromotionManifestDetailDto(
            manifest!.ManifestId.ToString(),
            manifest.Status,
            PromotionManifestValidator.ExtractMetadataDto(manifest.Topology),
            manifest.CreatedAt.ToString("o"),
            manifest.UpdatedAt.ToString("o"))), null);
    }

    private ValidationError ContentBundleRepositoryNotAvailable() =>
        new("CONTENT_BUNDLE_REPOSITORY_NOT_AVAILABLE", "Content bundle repository is not registered.");

    private async Task<(JsonElement? data, ValidationError? error)> DataContentBundleListHubsAsync(CancellationToken ct)
    {
        if (_contentBundleRepository is null) return (null, ContentBundleRepositoryNotAvailable());
        var items = await _contentBundleRepository.ListContentHubsAsync(ct);
        return (JsonSerializer.SerializeToElement(items), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataContentBundleListEntitiesAsync(CancellationToken ct)
    {
        if (_contentBundleRepository is null) return (null, ContentBundleRepositoryNotAvailable());
        var items = await _contentBundleRepository.ListContentEntitiesAsync(ct);
        return (JsonSerializer.SerializeToElement(items), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataContentBundleListRelationsAsync(CancellationToken ct)
    {
        if (_contentBundleRepository is null) return (null, ContentBundleRepositoryNotAvailable());
        var items = await _contentBundleRepository.ListContentRelationsAsync(ct);
        return (JsonSerializer.SerializeToElement(items), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataContentBundleListStatesAsync(CancellationToken ct)
    {
        if (_contentBundleRepository is null) return (null, ContentBundleRepositoryNotAvailable());
        var items = await _contentBundleRepository.ListContentStatesAsync(ct);
        return (JsonSerializer.SerializeToElement(items), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataContentBundleGetEntityAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_contentBundleRepository is null) return (null, ContentBundleRepositoryNotAvailable());
        if (!TryParseEntityId(vector, out var entityId, out var parseError))
            return (null, parseError);

        var detail = await _contentBundleRepository.LoadContentEntityAsync(entityId, ct);
        if (detail is null)
            return (null, new ValidationError("ENTITY_NOT_FOUND", $"Entity {entityId} was not found."));

        return (JsonSerializer.SerializeToElement(detail), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataContentBundleSearchAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_contentBundleRepository is null) return (null, ContentBundleRepositoryNotAvailable());

        string? keyword = null;
        string? kind = null;
        string? state = null;
        if (vector.Payload is { ValueKind: JsonValueKind.Object } payload)
        {
            if (payload.TryGetProperty("keyword", out var kw) && kw.ValueKind == JsonValueKind.String)
                keyword = kw.GetString();
            if (payload.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String)
                kind = k.GetString();
            if (payload.TryGetProperty("state", out var s) && s.ValueKind == JsonValueKind.String)
                state = s.GetString();
        }

        var items = await _contentBundleRepository.SearchContentBundleAsync(keyword, kind, state, ct);
        return (JsonSerializer.SerializeToElement(items), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataContentBundleCreateDraftAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_contentBundleRepository is null) return (null, ContentBundleRepositoryNotAvailable());
        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object)
            return (null, new ValidationError("CONTENT_BUNDLE_PAYLOAD_REQUIRED", "payload is required."));

        ContentBundleDraftRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<ContentBundleDraftRequestDto>(vector.Payload.Value.GetRawText());
        }
        catch (JsonException)
        {
            return (null, new ValidationError("CONTENT_BUNDLE_PAYLOAD_MALFORMED", "payload could not be parsed."));
        }

        if (request is null || string.IsNullOrWhiteSpace(request.HubId))
            return (null, new ValidationError("HUB_ID_REQUIRED", "hubId is required."));
        if (!Guid.TryParse(request.HubId, out var hubId))
            return (null, new ValidationError("MALFORMED_HUB_ID", "hubId must be a valid UUID."));
        if (request.RelationIds is null || request.RelationIds.Count == 0)
            return (null, new ValidationError("RELATION_IDS_REQUIRED", "relationIds must contain at least one id."));
        if (string.IsNullOrWhiteSpace(request.StateName))
            return (null, new ValidationError("STATE_NAME_REQUIRED", "stateName is required."));

        var relationIds = new List<Guid>();
        foreach (var rid in request.RelationIds)
        {
            if (!Guid.TryParse(rid, out var relId))
                return (null, new ValidationError("MALFORMED_RELATION_ID", $"relationId '{rid}' is not a valid UUID."));
            relationIds.Add(relId);
        }

        var (draft, error) = await _contentBundleRepository.CreateEntityDraftAsync(
            hubId, request.EntityJsonb, relationIds, request.StateName, ct);
        if (error is not null) return (null, error);
        return (JsonSerializer.SerializeToElement(draft!), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataContentBundleValidateDraftAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_contentBundleRepository is null) return (null, ContentBundleRepositoryNotAvailable());
        if (!TryParseDraftId(vector, out var draftId, out var parseError))
            return (null, parseError);

        var draft = await _contentBundleRepository.LoadDraftAsync(draftId, ct);
        if (draft is null)
            return (null, new ValidationError("DRAFT_NOT_FOUND", $"Draft {draftId} was not found."));

        var stateName = await ResolveStateNameAsync(draft.StateId, ct);
        var refs = await _contentBundleRepository.LoadRefContextAsync(draft.HubId, draft.RelationIds, stateName, ct);
        var validation = ContentBundleValidator.ValidateDraft(draft, refs);
        return (JsonSerializer.SerializeToElement(ContentBundleValidator.ToResponse(validation)), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataContentBundlePreviewDraftAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_contentBundleRepository is null) return (null, ContentBundleRepositoryNotAvailable());
        if (!TryParseDraftId(vector, out var draftId, out var parseError))
            return (null, parseError);

        var draft = await _contentBundleRepository.LoadDraftAsync(draftId, ct);
        if (draft is null)
            return (null, new ValidationError("DRAFT_NOT_FOUND", $"Draft {draftId} was not found."));

        var stateName = await ResolveStateNameAsync(draft.StateId, ct);
        var refs = await _contentBundleRepository.LoadRefContextAsync(draft.HubId, draft.RelationIds, stateName, ct);
        var validation = ContentBundleValidator.ValidateDraft(draft, refs);

        string label = "Untitled";
        try
        {
            var json = JsonDocument.Parse(draft.EntityJsonb).RootElement;
            if (json.TryGetProperty("label", out var labelEl) && labelEl.ValueKind == JsonValueKind.String)
                label = labelEl.GetString() ?? label;
        }
        catch (JsonException) { /* validation will report malformed */ }

        var relationLabels = draft.RelationIds
            .Where(r => refs.RelationNames.ContainsKey(r))
            .Select(r => refs.RelationNames[r])
            .ToList();

        var preview = new ContentBundlePreviewResponseDto(
            draftId.ToString(),
            label,
            draft.HubId.ToString(),
            refs.HubRelationName,
            draft.RelationIds.Select(r => r.ToString()).ToList(),
            relationLabels,
            stateName,
            draft.EntityJsonb,
            ContentBundleValidator.ToResponse(validation),
            validation.Valid);

        return (JsonSerializer.SerializeToElement(preview), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataContentBundlePromoteDraftAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_contentBundleRepository is null) return (null, ContentBundleRepositoryNotAvailable());
        if (!TryParseDraftId(vector, out var draftId, out var parseError))
            return (null, parseError);

        var (response, error) = await _contentBundleRepository.PromoteDraftAsync(draftId, ct);
        if (error is not null) return (null, error);
        return (JsonSerializer.SerializeToElement(response), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataContentBundleGetHubAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_contentBundleRepository is null) return (null, ContentBundleRepositoryNotAvailable());
        if (!TryParseHubId(vector, out var hubId, out var parseError))
            return (null, parseError);

        var detail = await _contentBundleRepository.LoadContentHubAsync(hubId, ct);
        if (detail is null)
            return (null, new ValidationError("HUB_NOT_FOUND", $"Hub {hubId} was not found."));

        return (JsonSerializer.SerializeToElement(detail), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataContentBundleGetRelationAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_contentBundleRepository is null) return (null, ContentBundleRepositoryNotAvailable());
        if (!TryParseRelationRegistryId(vector, out var relationId, out var parseError))
            return (null, parseError);

        var detail = await _contentBundleRepository.LoadContentRelationAsync(relationId, ct);
        if (detail is null)
            return (null, new ValidationError("RELATION_NOT_FOUND", $"Relation {relationId} was not found."));

        return (JsonSerializer.SerializeToElement(detail), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataContentBundleUpdateDraftAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_contentBundleRepository is null) return (null, ContentBundleRepositoryNotAvailable());
        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object)
            return (null, new ValidationError("CONTENT_BUNDLE_PAYLOAD_REQUIRED", "payload is required."));

        ContentBundleUpdateDraftRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<ContentBundleUpdateDraftRequestDto>(vector.Payload.Value.GetRawText());
        }
        catch (JsonException)
        {
            return (null, new ValidationError("CONTENT_BUNDLE_PAYLOAD_MALFORMED", "payload could not be parsed."));
        }

        if (request is null || string.IsNullOrWhiteSpace(request.DraftId))
            return (null, new ValidationError("DRAFT_ID_REQUIRED", "draftId is required."));
        if (!Guid.TryParse(request.DraftId, out var draftId))
            return (null, new ValidationError("MALFORMED_DRAFT_ID", "draftId must be a valid UUID."));
        if (string.IsNullOrWhiteSpace(request.HubId))
            return (null, new ValidationError("HUB_ID_REQUIRED", "hubId is required."));
        if (!Guid.TryParse(request.HubId, out var hubId))
            return (null, new ValidationError("MALFORMED_HUB_ID", "hubId must be a valid UUID."));
        if (request.RelationIds is null || request.RelationIds.Count == 0)
            return (null, new ValidationError("RELATION_IDS_REQUIRED", "relationIds must contain at least one id."));
        if (string.IsNullOrWhiteSpace(request.StateName))
            return (null, new ValidationError("STATE_NAME_REQUIRED", "stateName is required."));

        var relationIds = new List<Guid>();
        foreach (var rid in request.RelationIds)
        {
            if (!Guid.TryParse(rid, out var relId))
                return (null, new ValidationError("MALFORMED_RELATION_ID", $"relationId '{rid}' is not a valid UUID."));
            relationIds.Add(relId);
        }

        var (draft, error) = await _contentBundleRepository.UpdateEntityDraftAsync(
            draftId, hubId, request.EntityJsonb, relationIds, request.StateName, ct);
        if (error is not null) return (null, error);
        return (JsonSerializer.SerializeToElement(draft!), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataContentBundleListHubRelationsAsync(
        CancellationToken ct)
    {
        if (_contentBundleRepository is null) return (null, ContentBundleRepositoryNotAvailable());
        var items = await _contentBundleRepository.ListContentHubRelationsAsync(ct);
        return (JsonSerializer.SerializeToElement(items), null);
    }

    private async Task<string?> ResolveStateNameAsync(Guid? stateId, CancellationToken ct)
    {
        if (stateId is null || _contentBundleRepository is null) return null;
        var states = await _contentBundleRepository.ListContentStatesAsync(ct);
        return states.FirstOrDefault(s => s.StateId == stateId.Value.ToString())?.Name;
    }

    private static bool TryParseDraftId(
        OperationVector vector,
        out Guid draftId,
        out ValidationError? error)
    {
        draftId = Guid.Empty;
        error = null;

        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object)
        {
            error = new ValidationError("CONTENT_BUNDLE_PAYLOAD_REQUIRED", "payload.draftId is required.");
            return false;
        }

        if (!vector.Payload.Value.TryGetProperty("draftId", out var idEl) ||
            idEl.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(idEl.GetString(), out draftId))
        {
            error = new ValidationError("MALFORMED_DRAFT_ID", "draftId must be a valid UUID.");
            return false;
        }

        return true;
    }

    private static bool TryParseEntityId(
        OperationVector vector,
        out Guid entityId,
        out ValidationError? error)
    {
        entityId = Guid.Empty;
        error = null;

        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object)
        {
            error = new ValidationError("CONTENT_BUNDLE_PAYLOAD_REQUIRED", "payload.entityId is required.");
            return false;
        }

        if (!vector.Payload.Value.TryGetProperty("entityId", out var idEl) ||
            idEl.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(idEl.GetString(), out entityId))
        {
            error = new ValidationError("MALFORMED_ENTITY_ID", "entityId must be a valid UUID.");
            return false;
        }

        return true;
    }

    private static bool TryParseHubId(
        OperationVector vector,
        out Guid hubId,
        out ValidationError? error)
    {
        hubId = Guid.Empty;
        error = null;

        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object)
        {
            error = new ValidationError("CONTENT_BUNDLE_PAYLOAD_REQUIRED", "payload.hubId is required.");
            return false;
        }

        if (!vector.Payload.Value.TryGetProperty("hubId", out var idEl) ||
            idEl.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(idEl.GetString(), out hubId))
        {
            error = new ValidationError("MALFORMED_HUB_ID", "hubId must be a valid UUID.");
            return false;
        }

        return true;
    }

    private static bool TryParseRelationRegistryId(
        OperationVector vector,
        out Guid relationRegistryId,
        out ValidationError? error)
    {
        relationRegistryId = Guid.Empty;
        error = null;

        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object)
        {
            error = new ValidationError("CONTENT_BUNDLE_PAYLOAD_REQUIRED", "payload.relationRegistryId is required.");
            return false;
        }

        if (!vector.Payload.Value.TryGetProperty("relationRegistryId", out var idEl) ||
            idEl.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(idEl.GetString(), out relationRegistryId))
        {
            error = new ValidationError("MALFORMED_RELATION_ID", "relationRegistryId must be a valid UUID.");
            return false;
        }

        return true;
    }

    private static bool TryParseManifestId(
        OperationVector vector,
        out Guid manifestId,
        out ValidationError? error)
    {
        manifestId = Guid.Empty;
        error = null;

        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object)
        {
            error = new ValidationError("MANIFEST_PAYLOAD_REQUIRED", "payload.manifestId is required.");
            return false;
        }

        if (!vector.Payload.Value.TryGetProperty("manifestId", out var idEl) ||
            idEl.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(idEl.GetString(), out manifestId))
        {
            error = new ValidationError("MALFORMED_MANIFEST_ID", "manifestId must be a valid UUID.");
            return false;
        }

        return true;
    }

    private static bool TryParseDraftRequest(
        OperationVector vector,
        out AdminManifestDraftRequestDto? request,
        out ValidationError? error)
    {
        request = null;
        error = null;

        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object)
        {
            error = new ValidationError("MANIFEST_PAYLOAD_REQUIRED", "payload is required.");
            return false;
        }

        try
        {
            request = JsonSerializer.Deserialize<AdminManifestDraftRequestDto>(vector.Payload.Value.GetRawText());
        }
        catch (JsonException)
        {
            error = new ValidationError("MANIFEST_PAYLOAD_MALFORMED", "payload could not be parsed.");
            return false;
        }

        if (request is null)
        {
            error = new ValidationError("MANIFEST_PAYLOAD_MALFORMED", "payload could not be parsed.");
            return false;
        }

        var hasScreenOp = ManifestScreenOperationDeriver.TryDeriveAxes(
            request.ScreenOperationKind,
            manifestKey: null,
            manifestId: null,
            out _, out _, out _, out _, out _);

        if (!hasScreenOp &&
            (string.IsNullOrWhiteSpace(request.Role) ||
             string.IsNullOrWhiteSpace(request.Target) ||
             string.IsNullOrWhiteSpace(request.Layer) ||
             string.IsNullOrWhiteSpace(request.Action)))
        {
            error = new ValidationError(
                "DISPATCHER_AXES_REQUIRED",
                "role, target, layer, and action are required (or provide screenOperationKind).");
            return false;
        }

        if (!hasScreenOp && string.IsNullOrWhiteSpace(request.RuntimeDestination))
        {
            error = new ValidationError("RUNTIME_DESTINATION_REQUIRED", "runtimeDestination is required.");
            return false;
        }

        return true;
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataManifestAssignHubGroupingAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_manifestRepository is null) return (null, ManifestRepositoryNotAvailable());
        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object)
            return (null, new ValidationError("MANIFEST_PAYLOAD_REQUIRED", "payload is required."));

        AdminManifestAssignHubGroupingRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<AdminManifestAssignHubGroupingRequestDto>(
                vector.Payload.Value.GetRawText());
        }
        catch (JsonException)
        {
            return (null, new ValidationError("MANIFEST_PAYLOAD_MALFORMED", "payload could not be parsed."));
        }

        if (request is null ||
            !Guid.TryParse(request.ManifestId, out var manifestId) ||
            !Guid.TryParse(request.HubId, out var hubId) ||
            string.IsNullOrWhiteSpace(request.ManifestKey))
        {
            return (null, new ValidationError(
                "HUB_GROUPING_PAYLOAD_INVALID",
                "manifestId, hubId, and manifestKey are required."));
        }

        var entry = JsonSerializer.SerializeToElement(new
        {
            type = ManifestCanonicalProjection.HubGroupingEntryType,
            hubId = hubId.ToString(),
            manifestKey = request.ManifestKey.Trim(),
        });

        var (manifest, error) = await _manifestRepository.MergeTopologyExtensionDraftAsync(
            manifestId,
            ManifestCanonicalProjection.HubGroupingEntryType,
            entry,
            ct);
        if (error is not null) return (null, error);

        var (refreshed, refreshError) = await RefreshManifestDispatcherFromExtensionsAsync(manifest!, ct);
        if (refreshError is not null) return (null, refreshError);
        manifest = refreshed ?? manifest;

        return (JsonSerializer.SerializeToElement(ToManifestDetailDto(manifest!)), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataManifestAssignScreenDataShapeAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_manifestRepository is null) return (null, ManifestRepositoryNotAvailable());
        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object)
            return (null, new ValidationError("MANIFEST_PAYLOAD_REQUIRED", "payload is required."));

        AdminManifestAssignScreenDataShapeRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<AdminManifestAssignScreenDataShapeRequestDto>(
                vector.Payload.Value.GetRawText());
        }
        catch (JsonException)
        {
            return (null, new ValidationError("MANIFEST_PAYLOAD_MALFORMED", "payload could not be parsed."));
        }

        if (request is null || !Guid.TryParse(request.ManifestId, out var manifestId))
        {
            return (null, new ValidationError("MALFORMED_MANIFEST_ID", "manifestId must be a valid UUID."));
        }

        var draftDetail = await _manifestRepository.LoadDetailByIdAsync(manifestId, ct);
        if (draftDetail is null)
            return (null, new ValidationError("MANIFEST_NOT_FOUND", $"Manifest {manifestId} was not found."));

        var draftTables = ManifestRelationIntentValidator.FromRequestLogicalTables(request.LogicalTables);
        if (draftTables.Count == 0)
            draftTables = ManifestRelationIntentValidator.ExtractLogicalTables(draftDetail.Topology);

        var enumError = await EnumDictionaryColumnValidator.ValidateEnumGroupReferencesAsync(
            _enumDictionaryRepository,
            request.LogicalTables,
            request.Columns,
            ct);
        if (enumError is not null) return (null, enumError);

        var relationIntents = request.RelationIntents ?? Array.Empty<AdminManifestRelationIntentDto>();
        if (relationIntents.Count > 0)
        {
            var relationErrors = await ManifestRelationIntentValidator.ValidateRelationIntentsAsync(
                relationIntents,
                draftTables,
                (id, token) => _manifestRepository.LoadDetailByIdAsync(id, token),
                ct);
            if (relationErrors.Count > 0)
                return (null, relationErrors[0]);
        }

        // Extract existing topologySystemName from persisted screen_data_shape topology entry.
        string? existingTopologySystemName = null;
        foreach (var topologyEntry in draftDetail.Topology)
        {
            if (topologyEntry.TryGetProperty("type", out var typeProp) &&
                typeProp.GetString() == ManifestCanonicalProjection.ScreenDataShapeEntryType &&
                topologyEntry.TryGetProperty("topologySystemName", out var tsnProp) &&
                tsnProp.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(tsnProp.GetString()))
            {
                existingTopologySystemName = tsnProp.GetString()!.Trim();
                break;
            }
        }

        var topologySystemName = request.TopologySystemName?.Trim();

        if (!string.IsNullOrWhiteSpace(topologySystemName))
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(topologySystemName, @"^[a-z0-9]+(?:-[a-z0-9]+)*$"))
                return (null, new ValidationError("INVALID_TOPOLOGY_SYSTEM_NAME",
                    "topologySystemName は英小文字・数字・ハイフンのみで、先頭・末尾・連続ハイフン禁止です。"));

            if (existingTopologySystemName is not null &&
                !string.Equals(existingTopologySystemName, topologySystemName, StringComparison.Ordinal))
                return (null, new ValidationError("TOPOLOGY_SYSTEM_NAME_IMMUTABLE",
                    $"topologySystemName は設定後に変更できません。既存値: {existingTopologySystemName}"));
        }
        else if (existingTopologySystemName is null)
        {
            return (null, new ValidationError("TOPOLOGY_SYSTEM_NAME_REQUIRED",
                "topologySystemName は必須です。英小文字・数字・ハイフンで指定してください。"));
        }

        // Use existing value if not provided in this request (re-assign without step 1 fields).
        var effectiveTopologySystemName = !string.IsNullOrWhiteSpace(topologySystemName)
            ? topologySystemName
            : existingTopologySystemName;

        var tableRef = !string.IsNullOrWhiteSpace(request.TableRef)
            ? request.TableRef.Trim()
            : request.DbTableName?.Trim();

        var primaryOp = !string.IsNullOrWhiteSpace(request.ScreenOperationKind)
            ? request.ScreenOperationKind.Trim()
            : request.ScreenOperationKinds is { Count: > 0 }
                ? request.ScreenOperationKinds[0].Trim()
                : null;
        var operationKinds = request.ScreenOperationKinds is { Count: > 0 }
            ? request.ScreenOperationKinds
            : !string.IsNullOrWhiteSpace(primaryOp)
                ? new[] { primaryOp! }
                : Array.Empty<string>();

        var entry = JsonSerializer.SerializeToElement(new
        {
            type = ManifestCanonicalProjection.ScreenDataShapeEntryType,
            tableRef,
            dbTableName = tableRef,
            importSchemaName = request.ImportSchemaName,
            searchTargets = request.SearchTargets ?? Array.Empty<string>(),
            searchKeyColumns = request.SearchKeyColumns ?? Array.Empty<string>(),
            aggregationSpec = request.AggregationSpec,
            aggregationKey = request.AggregationKey,
            aggregationFunction = request.AggregationFunction,
            aggregationColumns = request.AggregationColumns ?? Array.Empty<string>(),
            aggregationMeasures = request.AggregationMeasures ?? Array.Empty<AdminManifestAggregationMeasureDto>(),
            aggregationBlocks = request.AggregationBlocks ?? Array.Empty<AdminManifestAggregationBlockDto>(),
            displayColumns = request.DisplayColumns ?? Array.Empty<string>(),
            logicalTables = request.LogicalTables ?? Array.Empty<AdminManifestLogicalTableDto>(),
            columns = request.Columns ?? Array.Empty<AdminManifestScreenColumnDto>(),
            screenOperationKind = primaryOp,
            screenOperationKinds = operationKinds,
            topologySystemName = effectiveTopologySystemName,
            userFacingTopologyLabel = request.UserFacingTopologyLabel,
            relationIntents,
            operationEntityBindings = request.OperationEntityBindings ??
                Array.Empty<AdminManifestOperationEntityBindingDto>(),
            initialDataRows = request.InitialDataRows ?? Array.Empty<System.Text.Json.JsonElement>(),
            searchConditions = request.SearchConditions ?? Array.Empty<AdminManifestSearchConditionDto>(),
            havingConditions = request.HavingConditions ?? Array.Empty<AdminManifestHavingConditionDto>(),
            displayColumnMode = request.DisplayColumnMode,
            screenReadQueryWiring = ScreenReadQueryWiringBuilder.Build(
                request.SearchConditions,
                request.HavingConditions,
                request.AggregationMeasures,
                request.DisplayColumns,
                request.DisplayColumnMode),
        });

        var (manifest, error) = await _manifestRepository.MergeTopologyExtensionDraftAsync(
            manifestId,
            ManifestCanonicalProjection.ScreenDataShapeEntryType,
            entry,
            ct);
        if (error is not null) return (null, error);

        var (refreshed, refreshError) = await RefreshManifestDispatcherFromExtensionsAsync(manifest!, ct);
        if (refreshError is not null) return (null, refreshError);
        manifest = refreshed ?? manifest;

        return (JsonSerializer.SerializeToElement(ToManifestDetailDto(manifest!)), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataEnumDictionaryListGroupsAsync(
        CancellationToken ct)
    {
        if (_enumDictionaryRepository is null)
        {
            return (null, new ValidationError(
                "ENUM_DICTIONARY_NOT_AVAILABLE",
                "Enum dictionary repository is not configured."));
        }

        var groups = await _enumDictionaryRepository.ListGroupsAsync(ct);
        return (JsonSerializer.SerializeToElement(groups), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataEnumDictionaryGetGroupAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_enumDictionaryRepository is null)
        {
            return (null, new ValidationError(
                "ENUM_DICTIONARY_NOT_AVAILABLE",
                "Enum dictionary repository is not configured."));
        }

        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object)
            return (null, new ValidationError("ENUM_GROUP_PAYLOAD_REQUIRED", "payload with groupId is required."));

        EnumDictionaryGetGroupRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<EnumDictionaryGetGroupRequestDto>(
                vector.Payload.Value.GetRawText());
        }
        catch (JsonException)
        {
            return (null, new ValidationError("ENUM_GROUP_PAYLOAD_MALFORMED", "payload could not be parsed."));
        }

        if (request is null || !Guid.TryParse(request.GroupId, out var groupId))
        {
            return (null, new ValidationError("ENUM_GROUP_ID_MALFORMED", "groupId must be a valid UUID."));
        }

        var detail = await _enumDictionaryRepository.GetGroupDetailAsync(groupId, ct);
        if (detail is null)
        {
            return (null, new ValidationError(
                "ENUM_GROUP_NOT_FOUND",
                $"Enum group {groupId} was not found."));
        }

        if (detail.Items.Count == 0)
        {
            return (null, new ValidationError(
                "ENUM_GROUP_ITEMS_EMPTY",
                $"Enum group {groupId} has no items."));
        }

        return (JsonSerializer.SerializeToElement(detail), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataManifestListRelationshipRemoteTargetsAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_manifestRepository is null) return (null, ManifestRepositoryNotAvailable());

        Guid? excludeId = null;
        if (vector.Payload is { ValueKind: JsonValueKind.Object } payload &&
            payload.TryGetProperty("excludeManifestId", out var excludeEl) &&
            excludeEl.ValueKind == JsonValueKind.String &&
            Guid.TryParse(excludeEl.GetString(), out var parsedExclude))
        {
            excludeId = parsedExclude;
        }

        var items = await _manifestRepository.ListManifestsAsync("active", ct);
        var targets = new List<AdminManifestRelationshipRemoteTargetDto>();

        foreach (var item in items)
        {
            if (excludeId.HasValue && item.ManifestId == excludeId.Value)
                continue;

            var detail = await _manifestRepository.LoadDetailByIdAsync(item.ManifestId, ct);
            if (detail is null) continue;

            var logical = ManifestRelationIntentValidator.ExtractLogicalTables(detail.Topology);
            if (logical.Count == 0) continue;

            var (_, manifestKey) = ManifestCanonicalProjection.ExtractHubGrouping(detail.Topology);
            var tables = logical.Select(t => new AdminManifestRelationshipRemoteTargetTableDto(
                t.TableName,
                t.ColumnNames.Select(c => new AdminManifestScreenColumnDto(c, "text", true)).ToList()
            )).ToList();

            targets.Add(new AdminManifestRelationshipRemoteTargetDto(
                item.ManifestId.ToString(),
                detail.Status,
                manifestKey,
                tables));
        }

        return (JsonSerializer.SerializeToElement(targets), null);
    }

    private async Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> RefreshManifestDispatcherFromExtensionsAsync(
        ManifestDetailRecord detail,
        CancellationToken ct)
    {
        if (_manifestRepository is null)
            return (detail, ManifestRepositoryNotAvailable());

        if (!detail.Status.Equals("draft", StringComparison.OrdinalIgnoreCase))
            return (detail, null);

        var screenOp = ManifestCanonicalProjection.ExtractScreenOperationKind(detail.Topology);
        if (string.IsNullOrWhiteSpace(screenOp))
        {
            var kinds = ManifestCanonicalProjection.ExtractScreenOperationKinds(detail.Topology);
            screenOp = kinds.Count > 0 ? kinds[0] : null;
        }
        if (string.IsNullOrWhiteSpace(screenOp))
            return (detail, null);

        var (_, manifestKeyFromTopology) = ManifestCanonicalProjection.ExtractHubGrouping(detail.Topology);
        if (!ManifestScreenOperationDeriver.TryDeriveAxes(
                screenOp,
                manifestKeyFromTopology,
                detail.ManifestId,
                out var role,
                out var target,
                out var layer,
                out var action,
                out var runtimeDestination))
        {
            return (detail, null);
        }

        var topology = ManifestCanonicalProjection.WithDispatcherMapping(
            detail.Topology,
            role,
            target,
            layer,
            action,
            runtimeDestination);

        var promotionEntry = PromotionManifestValidator.ExtractEntry(detail.Topology);
        if (promotionEntry is not null)
            topology = PromotionManifestValidator.MergeIntoTopology(topology, promotionEntry.Value);

        var validation = ManifestTopologyValidator.Validate(topology, KnownRuntimeDestinations);
        if (validation.IsBlocking)
            return (null, validation.Errors[0]);

        return await _manifestRepository.UpdateDraftAsync(
            detail.ManifestId,
            detail.RelationRegistryId,
            topology,
            ct);
    }

    private static AdminManifestDetailDto ToManifestDetailDto(ManifestDetailRecord detail)
    {
        var summary = ManifestTopologyValidator.ExtractSummary(detail.Topology);
        return new AdminManifestDetailDto(
            detail.ManifestId.ToString(),
            detail.Status,
            detail.RelationRegistryId?.ToString(),
            ManifestTopologyValidator.ToDto(summary),
            JsonSerializer.Serialize(detail.Topology),
            detail.CreatedAt.ToString("o"),
            detail.UpdatedAt.ToString("o"));
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataSeedImportAsync(
        CancellationToken ct)
    {
        if (_seedRuntime is null) return (null, SeedRuntimeNotAvailable());

        var result = await _seedRuntime.ImportAsync(ct);
        if (!result.Success)
        {
            var errDtos = result.Errors
                .Select(e => new SeedValidationErrorDto(e.Code, e.Message))
                .ToList();
            var joined = string.Join(" | ", errDtos.Select(e => $"{e.Code}:{e.Message}"));
            return (null, new ValidationError("SEED_IMPORT_FAILED", joined));
        }

        return (JsonSerializer.SerializeToElement(
            new SeedImportResponseDto(
                true,
                result.ValidatedRuntimeCount,
                "Seed import completed via canonical runtime route.",
                [])), null);
    }

    // -----------------------------------------------------------------------
    // Hub Navigation layer
    // -----------------------------------------------------------------------

    private async Task<(JsonElement? data, ValidationError? error)> HubNavigationListManifestsAsync(
        CancellationToken ct)
    {
        if (_contentBundleRepository is null) return (null, ContentBundleRepositoryNotAvailable());
        var items = await _contentBundleRepository.ListTopologyManifestsAsync(ct);
        return (JsonSerializer.SerializeToElement(items), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> HubNavigationGetHubRelationsAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_contentBundleRepository is null) return (null, ContentBundleRepositoryNotAvailable());

        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object ||
            !vector.Payload.Value.TryGetProperty("topologyManifestId", out var midEl) ||
            midEl.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(midEl.GetString(), out var manifestId))
        {
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload.topologyManifestId is required."));
        }

        var items = await _contentBundleRepository.ListHubRelationsByManifestAsync(manifestId, ct);
        return (JsonSerializer.SerializeToElement(items), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> HubNavigationCreateAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_contentBundleRepository is null) return (null, ContentBundleRepositoryNotAvailable());

        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object)
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload is required."));

        var payload = vector.Payload.Value;
        if (!payload.TryGetProperty("topologyManifestId", out var midEl) || !Guid.TryParse(midEl.GetString(), out var manifestId))
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload.topologyManifestId is required."));
        if (!payload.TryGetProperty("relatedHubId", out var hidEl) || !Guid.TryParse(hidEl.GetString(), out var relatedHubId))
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload.relatedHubId is required."));
        if (!payload.TryGetProperty("sequencePosition", out var seqEl) || seqEl.ValueKind != JsonValueKind.Number)
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload.sequencePosition is required."));

        var (response, error) = await _contentBundleRepository.CreateHubRelationAsync(
            manifestId, relatedHubId, seqEl.GetInt32(), ct);
        if (error is not null) return (null, error);
        return (JsonSerializer.SerializeToElement(response), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> HubNavigationUpdateAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_contentBundleRepository is null) return (null, ContentBundleRepositoryNotAvailable());

        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object)
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload is required."));

        var payload = vector.Payload.Value;
        if (!payload.TryGetProperty("hubRelationId", out var hridEl) || !Guid.TryParse(hridEl.GetString(), out var hubRelationId))
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload.hubRelationId is required."));
        if (!payload.TryGetProperty("relatedHubId", out var hidEl) || !Guid.TryParse(hidEl.GetString(), out var relatedHubId))
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload.relatedHubId is required."));

        var (response, error) = await _contentBundleRepository.UpdateHubRelationAsync(
            hubRelationId, relatedHubId, ct);
        if (error is not null) return (null, error);
        return (JsonSerializer.SerializeToElement(response), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> HubNavigationDeprecateAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_contentBundleRepository is null) return (null, ContentBundleRepositoryNotAvailable());

        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object ||
            !vector.Payload.Value.TryGetProperty("hubRelationId", out var hridEl) ||
            !Guid.TryParse(hridEl.GetString(), out var hubRelationId))
        {
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload.hubRelationId is required."));
        }

        var (response, error) = await _contentBundleRepository.DeprecateHubRelationAsync(hubRelationId, ct);
        if (error is not null) return (null, error);
        return (JsonSerializer.SerializeToElement(response), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> HubNavigationReorderAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_contentBundleRepository is null) return (null, ContentBundleRepositoryNotAvailable());

        if (vector.Payload is null || vector.Payload.Value.ValueKind != JsonValueKind.Object)
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload is required."));

        var payload = vector.Payload.Value;
        if (!payload.TryGetProperty("topologyManifestId", out var midEl) || !Guid.TryParse(midEl.GetString(), out var manifestId))
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload.topologyManifestId is required."));
        if (!payload.TryGetProperty("items", out var itemsEl) || itemsEl.ValueKind != JsonValueKind.Array)
            return (null, new ValidationError("MALFORMED_PAYLOAD", "payload.items must be an array."));

        var items = new List<(Guid HubRelationId, int NewSequencePosition)>();
        foreach (var el in itemsEl.EnumerateArray())
        {
            if (!el.TryGetProperty("hubRelationId", out var hridEl) || !Guid.TryParse(hridEl.GetString(), out var hrid))
                return (null, new ValidationError("MALFORMED_PAYLOAD", "Each item must have a valid hubRelationId."));
            if (!el.TryGetProperty("newSequencePosition", out var seqEl) || seqEl.ValueKind != JsonValueKind.Number)
                return (null, new ValidationError("MALFORMED_PAYLOAD", "Each item must have a numeric newSequencePosition."));
            items.Add((hrid, seqEl.GetInt32()));
        }

        var (response, error) = await _contentBundleRepository.ReorderHubRelationsAsync(manifestId, items, ct);
        if (error is not null) return (null, error);
        return (JsonSerializer.SerializeToElement(response), null);
    }
}
