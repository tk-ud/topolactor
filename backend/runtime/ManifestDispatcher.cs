using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Manifest-driven dispatcher. Resolves runtime_destination from manifest axes
/// (role, target, layer, action) and forwards to the registered IDispatchableRuntime handler.
///
/// Per SSOT dispatcher_contract:
///   - Reads active manifest from DB by [role, target, layer, action] axes.
///   - No silent fallback: missing manifest -> MANIFEST_NOT_FOUND error.
///   - Ambiguous manifests (multiple active for same axes) -> MANIFEST_AMBIGUOUS error.
///   - runtime_destination not in handler registry -> RUNTIME_DESTINATION_UNKNOWN error.
///   - Does not own topology transform logic.
///
/// Production handler registry (injected):
///   topology_transform_runtime -> RuntimeExecutor (canonical topology pipeline)
///   admin_runtime              -> AdminRuntimeDispatchAdapter -> AdminRuntime
///   sse_projection_runtime     -> SseProjectionRuntime
///
/// Boundary note:
///   registry_attractor_runtime is not registered in the production handler registry;
///   manifest dispatch to that destination returns RUNTIME_DESTINATION_UNKNOWN until a handler is registered.
///
/// Dev bypass: when _manifestRepository is null (not injected), TargetDispatchOverride
/// handles admin targets; unhandled requests fall through to the
/// topology_transform_runtime handler from the registry.
/// This explicit bypass is isolated inside the null-repository branch only and is not
/// the production destination selection path.
///
/// Production path: when _manifestRepository is configured, manifest resolution is the
/// sole authority for destination selection. TargetDispatchOverride is not consulted.
/// </summary>
public class ManifestDispatcher
{
    private readonly ILogger<ManifestDispatcher> _logger;
    private readonly IReadOnlyDictionary<string, IDispatchableRuntime> _runtimeHandlers;
    private readonly ManifestRepository? _manifestRepository;
    private readonly OperationVectorResolver _operationVectorResolver;
    private readonly TargetDispatchOverride _targetDispatchOverride;
    private readonly IBackendErrorEvidenceAppender? _errorAppender;
    private readonly TopologyRepository? _topologyRepository;
    private readonly HubNavigationResolver? _hubNavigationResolver;

    public ManifestDispatcher(
        ILogger<ManifestDispatcher> logger,
        IReadOnlyDictionary<string, IDispatchableRuntime> runtimeHandlers,
        OperationVectorResolver operationVectorResolver,
        TargetDispatchOverride targetDispatchOverride,
        ManifestRepository? manifestRepository = null,
        IBackendErrorEvidenceAppender? errorAppender = null,
        TopologyRepository? topologyRepository = null,
        HubNavigationResolver? hubNavigationResolver = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _runtimeHandlers = runtimeHandlers ?? throw new ArgumentNullException(nameof(runtimeHandlers));
        _operationVectorResolver = operationVectorResolver ?? throw new ArgumentNullException(nameof(operationVectorResolver));
        _targetDispatchOverride = targetDispatchOverride ?? throw new ArgumentNullException(nameof(targetDispatchOverride));
        _manifestRepository = manifestRepository;
        _errorAppender = errorAppender;
        _topologyRepository = topologyRepository;
        _hubNavigationResolver = hubNavigationResolver;
    }

    /// <summary>
    /// Resolves the runtime destination from manifest axes and dispatches the request
    /// to the registered handler.
    ///
    /// Special routing: target="db_notify" is allowed only for hook triggers and requires
    /// manifest_id from payload. The manifest_id is loaded and validated, then dispatches to
    /// the runtime_destination resolved from db_notify projection mapping in that manifest topology.
    /// Per SSOT notify_listen_contract.db_listen: listen_event_enters_scheduler_before_projection_runtime.
    ///
    /// When _manifestRepository is null (dev bypass): TargetDispatchOverride handles
    /// admin targets; unhandled requests fall through to the
    /// topology_transform_runtime handler in the registry.
    ///
    /// When _manifestRepository is configured (production): manifest resolution is the sole
    /// authority. TargetDispatchOverride is not consulted. Missing manifest returns
    /// MANIFEST_NOT_FOUND. No runtime_mapping or unregistered destination returns
    /// RUNTIME_DESTINATION_UNKNOWN — no implicit fallthrough.
    /// </summary>
    public async Task<EndpointResponseDto> DispatchAsync(
        EndpointRequestDto request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug(
            "ManifestDispatcher: resolving destination for Role={Role} Target={Target} Layer={Layer} Action={Action} TriggerKind={TriggerKind}",
            request.Role, request.Target, request.Layer, request.Action, request.TriggerKind);

        var vector = _operationVectorResolver.Resolve(request);


        if (_manifestRepository is null)
        {
            // Dev bypass: no manifest repository configured.
            // TargetDispatchOverride handles admin targets.
            // This branch is isolated here and is not the production destination selection path.
            _logger.LogDebug("ManifestDispatcher: no manifest repository configured (dev bypass).");

            var overrideValidationError = _targetDispatchOverride.ValidateRequest(vector);
            if (overrideValidationError is not null)
            {
                return new EndpointResponseDto(
                    Success: false,
                    Emission: null,
                    Errors: [overrideValidationError]);
            }

            var (handled, overrideData, overrideError) = await _targetDispatchOverride.TryHandleAsync(vector, ct);
            if (handled)
            {
                if (overrideError is not null)
                {
                    return new EndpointResponseDto(
                        Success: false,
                        Emission: null,
                        Errors: [overrideError]);
                }

                var emission = new Emission(
                    StructureMapId: null,
                    PackageId: null,
                    SchemaId: null,
                    ComponentIds: [],
                    Data: overrideData,
                    Errors: [],
                    JumpEvents: null,
                    ContextRouteRecommendation: null);
                return new EndpointResponseDto(Success: true, Emission: emission, Errors: []);
            }

            // Bypass fallthrough: unhandled target goes to topology_transform_runtime handler.
            return await DispatchToHandlerAsync("topology_transform_runtime", request, manifestId: null, ct);
        }

        // Production: manifest-driven destination selection.
        // TargetDispatchOverride is not consulted here.
        try
        {
            ManifestRecord? manifest;
            if (string.Equals(request.Target, "db_notify", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(request.TriggerKind, "hook", StringComparison.OrdinalIgnoreCase))
                {
                    return new EndpointResponseDto(
                        Success: false,
                        Emission: null,
                        Errors: [new ValidationError(
                            "DB_NOTIFY_TRIGGER_KIND_INVALID",
                            "db_notify target is only allowed for hook triggers.")]);
                }

                if (!TryExtractManifestId(request, out var dbNotifyManifestId))
                {
                    return new EndpointResponseDto(
                        Success: false,
                        Emission: null,
                        Errors: [new ValidationError(
                            "DB_NOTIFY_MANIFEST_ID_MISSING",
                            "db_notify payload must include a valid manifest_id.")]);
                }

                manifest = await _manifestRepository.LoadByIdAsync(dbNotifyManifestId, ct);
                if (manifest is null)
                {
                    return new EndpointResponseDto(
                        Success: false,
                        Emission: null,
                        Errors: [new ValidationError(
                            "MANIFEST_NOT_FOUND",
                            $"No manifest found for db_notify manifest_id={dbNotifyManifestId}.")]);
                }
            }
            else if (TryGetRawTargetRef(request, out var rawTargetRef))
            {
                // target_ref present in payload — must parse as "manifest:{uuid}:{wiring_key}".
                // Malformed target_ref returns TARGET_REF_INVALID: silent axes-fallback risks
                // routing to an unintended manifest when the admin-configured ref is broken.
                if (!TryParseManifestTargetRef(rawTargetRef!, out var targetRefManifestId))
                {
                    _logger.LogWarning(
                        "ManifestDispatcher: malformed target_ref '{TargetRef}' — expected manifest:{{uuid}}:{{key}} format.",
                        rawTargetRef);
                    return new EndpointResponseDto(
                        Success: false,
                        Emission: null,
                        Errors: [new ValidationError(
                            "TARGET_REF_INVALID",
                            $"target_ref '{rawTargetRef}' is not a valid manifest reference. Expected format: manifest:<uuid>:<wiring_key>.")]);
                }

                manifest = await _manifestRepository.LoadByIdAsync(targetRefManifestId, ct);
                if (manifest is null)
                {
                    _logger.LogWarning(
                        "ManifestDispatcher: manifest not found for target_ref manifest_id={ManifestId}.",
                        targetRefManifestId);
                    return new EndpointResponseDto(
                        Success: false,
                        Emission: null,
                        Errors: [new ValidationError(
                            "MANIFEST_NOT_FOUND",
                            $"No manifest found for target_ref manifest_id={targetRefManifestId}.")]);
                }

                // TOCTOU close (round 17 hardening): LoadByIdAsync (unlike ResolveActiveManifestAsync)
                // never filters by status, so a target_ref saved into an authored override while its
                // manifest was active would otherwise still dispatch after that manifest is later
                // deprecated or reverted to draft. Save-time authorization
                // (NpgsqlUiTopologyRepository.LoadAdminRuntimeManifestAuthorizationAsync) checks active
                // status only once, at save time — this dispatch-time check re-verifies it every time.
                if (!string.Equals(manifest.Status, "active", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "ManifestDispatcher: target_ref manifest {ManifestId} is not active (status={Status}).",
                        targetRefManifestId, manifest.Status);
                    return new EndpointResponseDto(
                        Success: false,
                        Emission: null,
                        Errors: [new ValidationError(
                            "TARGET_REF_MANIFEST_NOT_ACTIVE",
                            $"target_ref manifest {targetRefManifestId} has status '{manifest.Status}', not 'active'.")]);
                }

                // Layer/action authorization (round 17, hardened round 18): AdminRuntime.
                // ExecuteDataAsync dispatches purely on Request.Layer/Request.Action, independent of
                // which manifest_id resolved the target_ref (see class remarks). Without this check,
                // an authored dispatchTargetRefByTrigger override (or, identically, the uniform
                // per-layout target_ref applied by NpgsqlTopologyRepository.LoadLayoutNodesAsync —
                // both reach this exact branch, so this check covers both paths identically) could
                // combine one manifest's identity (e.g. a create_group-only manifest) with an
                // unrelated Layer/Action pair (e.g. delete_group) that manifest never declared
                // authority over.
                //
                // Round 17's first cut gated this on "target_ref already matches the strict
                // manifest:<uuid>:<layer>:<action> shape" -- a live-DB run caught that this
                // legitimately exempted the generic wiringKey navigation convention (e.g.
                // "manifest:<uuid>:projection_entry", "manifest:<uuid>:hub_relations_read" against
                // Layer=screen_list/Action=Search), but ALSO, incorrectly, exempted a CONCRETE
                // admin_runtime operation (e.g. Layer=enum_dictionary/Action=create_group) sent
                // alongside a non-canonical or generic-shaped target_ref -- shape alone said nothing
                // about which operation was actually being requested. Round 18 replaces the shape
                // gate with a semantic one: IsGenericStructuralReadTargetRef below is the exact
                // pre-condition of the "structural read fallback" further down in this same method
                // (destination=admin_runtime, manifest has a ui_projection entry, and the request
                // axes are a registered screen-read shape) -- the only combination under which
                // AdminRuntime.ExecuteDataAsync is known, by exhaustive review of its layerAction
                // switch, to never define a real case (it always yields ADMIN_OPERATION_NOT_FOUND,
                // downgraded to an empty success purely to resolve ui_projection/LayoutNodes for
                // rendering) -- so it is safe to skip strict authorization only for that exact
                // shape. Any OTHER admin_runtime dispatch (a concrete operation) now REQUIRES the
                // strict "manifest:<uuid>:<layer>:<action>" shape -- a non-canonical UUID (e.g. the
                // 32-hex "N" form) or a bare/generic wiringKey used for a concrete Layer/Action fails
                // closed here (TARGET_REF_ADMIN_RUNTIME_LAYER_ACTION_MISSING) rather than silently
                // skipping authorization. The generic screenReadQueryWiring target_ref shape on
                // NON-admin_runtime destinations is untouched either way.
                //
                // Second live-DB catch (round 18): a pre-existing, orthogonal convention
                // (CredentialManagementHubRelationUiProjectionLiveDbTests) dispatches
                // hub_navigation:get_hub_relations via target_ref against a manifest that
                // deliberately declares NEITHER dispatcher_mapping NOR ui_projection — a bare
                // "runtime_mapping only" manifest used purely to select which manifest's own
                // navigation sequence to enrich (EnrichWithHubNavigationAsync runs for ANY
                // successful admin_runtime response). See IsBareManifestNavigationReadTargetRefAsync
                // for the full rationale and its deliberately narrow, closed action allowlist.
                //
                // Round 19 restructuring (audit catch): the shape match against
                // AdminRuntimeTargetRefRe must be evaluated UNCONDITIONALLY, before consulting
                // either generic exemption below — round 18's structure computed it only INSIDE
                // the "not a generic exemption" branch, so a target_ref string that itself parses
                // as a DIFFERENT concrete operation (e.g. "manifest:<uuid>:enum_dictionary:
                // create_group") sent alongside screen-read request axes (Layer=screen_list,
                // Action=Search) would take the generic-structural-read exemption purely by
                // request-axes shape, never even inspecting what the target_ref string itself
                // claimed. A target_ref that DOES parse as the strict operation shape always means
                // something concrete regardless of the request's OWN axes — its embedded
                // layer:action must always be validated against the request and the resolved
                // manifest's own dispatcher_mapping; only a target_ref that does NOT parse as that
                // shape (a genuine generic/bare wiringKey) is eligible for either narrow exemption.
                if (string.Equals(ExtractRuntimeDestination(manifest.Topology), "admin_runtime", StringComparison.Ordinal))
                {
                    var adminRuntimeShapeMatch = AdminRuntimeTargetRefRe.Match(rawTargetRef!);
                    if (adminRuntimeShapeMatch.Success)
                    {
                        var layerActionError = ValidateAdminRuntimeTargetRefLayerAction(
                            adminRuntimeShapeMatch, targetRefManifestId, manifest.Topology, request.Layer, request.Action, request.Role);
                        if (layerActionError is not null)
                        {
                            _logger.LogWarning(
                                "ManifestDispatcher: target_ref layer/action authorization failed for manifest {ManifestId}: {Code}",
                                targetRefManifestId, layerActionError.Code);
                            return new EndpointResponseDto(
                                Success: false,
                                Emission: null,
                                Errors: [layerActionError]);
                        }
                    }
                    else if (!IsGenericStructuralReadTargetRef(manifest.Topology, request.Layer, request.Action) &&
                        !await IsBareManifestNavigationReadTargetRefAsync(
                            manifest.Topology, request.Role, request.Layer, request.Action, ct))
                    {
                        _logger.LogWarning(
                            "ManifestDispatcher: target_ref '{TargetRef}' resolves to an admin_runtime manifest for a concrete operation (Layer={Layer} Action={Action}) but is not in manifest:<uuid>:<layer>:<action> format.",
                            rawTargetRef, request.Layer, request.Action);
                        return new EndpointResponseDto(
                            Success: false,
                            Emission: null,
                            Errors: [new ValidationError(
                                "TARGET_REF_ADMIN_RUNTIME_LAYER_ACTION_MISSING",
                                $"target_ref '{rawTargetRef}' resolves to an admin_runtime manifest for a concrete operation " +
                                $"(Layer='{request.Layer}' Action='{request.Action}') but is not in \"manifest:<uuid>:<layer>:<action>\" format.")]);
                    }
                }
            }
            else
            {
                manifest = await _manifestRepository.ResolveActiveManifestAsync(
                role: request.Role,
                target: request.Target,
                layer: request.Layer,
                action: request.Action,
                ct: ct);

                // Canonical default entry fallback: a bare/no-selection projection entry (no
                // route target override, no explicit ?manifest=, no pre-injected
                // payload.target_ref — frontend/runtime/projectionEntry.ts
                // resolveProjectionEntryAxes({}) sends exactly this axes combination) has no
                // seeded dispatcher_mapping of its own. hubs.hub_relations' explicitly marked
                // canonical_default_entry row (see
                // ContentBundleRepository.ResolveCanonicalDefaultEntryManifestIdAsync) is the
                // means for resolving it — distinct from NavigationSequence's outbound links
                // from an already-resolved manifest. Narrowly scoped to this exact axes
                // combination only — this is not a general MANIFEST_NOT_FOUND fallback.
                if (manifest is null &&
                    _hubNavigationResolver is not null &&
                    string.Equals(request.Target, "default", StringComparison.Ordinal) &&
                    string.Equals(request.Layer, "screen_list", StringComparison.Ordinal) &&
                    string.Equals(request.Action, "Search", StringComparison.Ordinal))
                {
                    var canonicalDefaultEntryManifestId =
                        await _hubNavigationResolver.ResolveCanonicalDefaultEntryManifestIdAsync(ct);
                    if (canonicalDefaultEntryManifestId.HasValue)
                    {
                        var candidate = await _manifestRepository.LoadByIdAsync(canonicalDefaultEntryManifestId.Value, ct);
                        // active_status_requirement: a marked relation naming a draft/deprecated
                        // manifest resolves to no manifest (fail-close) — never a stale/inactive
                        // projection, even though the relation row itself is active.
                        if (candidate is not null &&
                            string.Equals(candidate.Status, "active", StringComparison.OrdinalIgnoreCase))
                        {
                            manifest = candidate;
                        }
                    }
                }
            }

            // Capability gate: after manifest resolution, enforce any explicit required_role
            // declared in topology capability_requirement entry. This is especially important
            // for target_ref paths that bypass role-axis filtering.
            if (manifest is not null)
            {
                var capabilityError = ValidateCapabilityRequirement(manifest.Topology, request.Role);
                if (capabilityError is not null)
                {
                    _logger.LogWarning(
                        "ManifestDispatcher: capability requirement not met for manifest {ManifestId}. RequiredRole={RequiredRole} ActualRole={ActualRole}",
                        manifest.ManifestId, capabilityError.Message, request.Role);
                    return new EndpointResponseDto(
                        Success: false,
                        Emission: null,
                        Errors: [capabilityError]);
                }
            }

            if (manifest is null)
            {
                _logger.LogWarning(
                    "ManifestDispatcher: no active manifest for Role={Role} Target={Target} Layer={Layer} Action={Action}.",
                    request.Role, request.Target, request.Layer, request.Action);

                return new EndpointResponseDto(
                    Success: false,
                    Emission: null,
                    Errors: [new ValidationError(
                        "MANIFEST_NOT_FOUND",
                        $"No active manifest for Role={request.Role} Target={request.Target} Layer={request.Layer} Action={request.Action}.")]);
            }

            _logger.LogDebug(
                "ManifestDispatcher: resolved manifest {ManifestId} for axes.",
                manifest.ManifestId);

            var destination = string.Equals(request.Target, "db_notify", StringComparison.OrdinalIgnoreCase)
                ? ExtractDbNotifyProjectionDestination(manifest.Topology)
                : ExtractRuntimeDestination(manifest.Topology);
            if (destination is null)
            {
                var errorCode = string.Equals(request.Target, "db_notify", StringComparison.OrdinalIgnoreCase)
                    ? "DB_NOTIFY_PROJECTION_MAPPING_MISSING"
                    : "RUNTIME_DESTINATION_UNKNOWN";
                var errorMessage = string.Equals(request.Target, "db_notify", StringComparison.OrdinalIgnoreCase)
                    ? $"Manifest {manifest.ManifestId} has no db_notify projection mapping entry."
                    : $"Manifest {manifest.ManifestId} has no runtime_mapping entry.";
                _logger.LogError("ManifestDispatcher: destination mapping missing in manifest {ManifestId}. code={Code}", manifest.ManifestId, errorCode);
                await RecordSubstrateErrorAsync(errorCode, errorMessage,
                    $"manifest_dispatcher:{manifest.ManifestId}", ct);
                return new EndpointResponseDto(
                    Success: false,
                    Emission: null,
                    Errors: [new ValidationError(
                        errorCode,
                        errorMessage)]);
            }

            var projectionDefinition = ExtractProjectionConstructorMapping(manifest.Topology);
            var response = await DispatchToHandlerAsync(destination, request, manifest.ManifestId, ct);

            // Structural read fallback: an admin_runtime-routed manifest with a declared
            // ui_projection entry (i.e. a render-only fixed_form_projection admin surface, such
            // as auth.external.credential_management.projection / manifest 092) has no explicit
            // admin action registered in AdminRuntime.ExecuteDataAsync for a plain screen-read
            // entry (ADMIN_OPERATION_NOT_FOUND). That is a routing gap, not a real failure: the
            // manifest is renderable via ui_projection, it just has no admin write/read verb for
            // this axes combination. Synthesize an empty success Emission so the ui_projection
            // enrichment below can still reach LayoutNodes. This does not fabricate business
            // data — actual row population for this class of manifest remains a separately
            // tracked authoring surface (see
            // docs/projection_design/credential-management-projection-design.md).
            // Narrow guard (all required, no partial match): destination must literally be
            // admin_runtime (not inferred from the error alone — a differently-routed manifest
            // must never take this path); the manifest must declare ui_projection; the axes must
            // be a recognized screen-read combination; and ADMIN_OPERATION_NOT_FOUND must be the
            // ONLY error — a composite/multi-error failure is a real failure, not a routing gap,
            // and must not be silently downgraded to success.
            if (!response.Success &&
                string.Equals(destination, "admin_runtime", StringComparison.Ordinal) &&
                HasUiProjectionEntry(manifest.Topology) &&
                ScreenDataShapeTopologyReader.IsScreenReadAction(vector.Layer, vector.Action) &&
                response.Errors.Count == 1 &&
                response.Errors[0].Code == "ADMIN_OPERATION_NOT_FOUND")
            {
                response = new EndpointResponseDto(
                    Success: true,
                    Emission: new Emission(
                        StructureMapId: null,
                        PackageId: null,
                        SchemaId: null,
                        ComponentIds: [],
                        Data: null,
                        Errors: []),
                    Errors: []);
            }

            if (!response.Success || response.Emission is null)
                return response;

            var emission = response.Emission;

            // Inject projection_constructor_mapping from manifest into emission so the frontend
            // projection runtime can call setProjectionDefinition without frontend topology judgment.
            if (projectionDefinition.HasValue)
                emission = emission with { ProjectionDefinition = projectionDefinition };

            // Resolve manifest.topology[ui_projection] refs into LayoutId/LayoutNodes/PackageId
            // regardless of runtime_destination — the structure-map-driven pipeline
            // (topology_transform_runtime) already resolves LayoutId/LayoutNodes through a
            // separate attractor/structure-map path, so this only fills the gap for
            // destinations (e.g. admin_runtime) that never resolve ui_projection today.
            emission = await EnrichWithUiProjectionAsync(emission, manifest.Topology, ct);

            // "current hub relation" / "current topology phase": attach the manifest-scoped
            // hub_relations navigation sequence and the resolved manifest identity for any
            // destination, not just topology_transform_runtime (RuntimeExecutor already does
            // this for its own path; this covers the remaining destinations).
            emission = await EnrichWithHubNavigationAsync(emission, manifest.ManifestId, ct);
            if (emission.ManifestId is null)
                emission = emission with { ManifestId = manifest.ManifestId.ToString() };

            var success = emission.Errors.Count == 0;
            return response with { Success = success, Emission = emission, Errors = emission.Errors };
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("MANIFEST_AMBIGUOUS", StringComparison.Ordinal))
        {
            _logger.LogError(ex, "ManifestDispatcher: ambiguous manifest lookup rejected.");
            await BackendErrorBoundary.RecordSystemErrorAsync(
                _errorAppender, _logger, ex,
                BackendErrorOriginLayer.ManifestDispatcher,
                "manifest_dispatcher:resolve_active_manifest",
                new BackendErrorEvidenceHint(ErrorCode: "MANIFEST_AMBIGUOUS"),
                ct);
            return new EndpointResponseDto(
                Success: false,
                Emission: null,
                Errors: [new ValidationError("MANIFEST_AMBIGUOUS", ex.Message)]);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("CANONICAL_DEFAULT_ENTRY_AMBIGUOUS", StringComparison.Ordinal))
        {
            _logger.LogError(ex, "ManifestDispatcher: ambiguous canonical default entry rejected.");
            await BackendErrorBoundary.RecordSystemErrorAsync(
                _errorAppender, _logger, ex,
                BackendErrorOriginLayer.ManifestDispatcher,
                "manifest_dispatcher:resolve_canonical_default_entry",
                new BackendErrorEvidenceHint(ErrorCode: "CANONICAL_DEFAULT_ENTRY_AMBIGUOUS"),
                ct);
            return new EndpointResponseDto(
                Success: false,
                Emission: null,
                Errors: [new ValidationError("CANONICAL_DEFAULT_ENTRY_AMBIGUOUS", ex.Message)]);
        }
    }

    // Records a manifest-dispatch substrate failure that surfaced as a configuration gap (no
    // exception object): missing runtime destination mapping or unregistered handler. These are
    // substrate breakage, distinct from MANIFEST_NOT_FOUND / TARGET_REF_INVALID / capability rejects.
    private Task RecordSubstrateErrorAsync(string errorCode, string message, string boundaryKey, CancellationToken ct) =>
        BackendErrorBoundary.RecordSystemErrorAsync(
            _errorAppender, _logger,
            new BackendErrorEvidence(
                OriginLayer: BackendErrorOriginLayer.ManifestDispatcher,
                BoundaryKey: boundaryKey,
                ErrorKind: BackendErrorKind.SystemError,
                ErrorCode: errorCode,
                MessagePublic: message,
                StackHash: BackendErrorBoundary.ComputeHash($"manifest_dispatcher|{errorCode}|{boundaryKey}"),
                Severity: "error",
                Retryable: false),
            ct);

    private static bool TryExtractManifestId(EndpointRequestDto request, out Guid manifestId)
    {
        manifestId = Guid.Empty;
        if (!request.Payload.HasValue || request.Payload.Value.ValueKind != JsonValueKind.Object)
            return false;

        var payload = request.Payload.Value;
        if (!payload.TryGetProperty("manifest_id", out var manifestIdEl) ||
            manifestIdEl.ValueKind != JsonValueKind.String)
            return false;

        return Guid.TryParse(manifestIdEl.GetString(), out manifestId);
    }

    /// <summary>
    /// Extracts the raw target_ref string from payload when the field is present and non-empty.
    /// Returns false when target_ref is absent, null, or empty — falling through to axes resolution.
    /// Returns true with the raw string when present, regardless of format validity.
    /// The caller must then call TryParseManifestTargetRef to validate the format.
    /// </summary>
    private static bool TryGetRawTargetRef(EndpointRequestDto request, out string? rawTargetRef)
    {
        rawTargetRef = null;
        if (!request.Payload.HasValue || request.Payload.Value.ValueKind != JsonValueKind.Object)
            return false;

        var payload = request.Payload.Value;
        if (!payload.TryGetProperty("target_ref", out var targetRefEl) ||
            targetRefEl.ValueKind != JsonValueKind.String)
            return false;

        var value = targetRefEl.GetString();
        if (string.IsNullOrWhiteSpace(value)) return false;

        rawTargetRef = value;
        return true;
    }

    /// <summary>
    /// Parses a raw target_ref string in "manifest:{uuid}:{wiring_key}" format.
    /// Returns true and sets manifestId when the format is valid.
    /// Returns false for any other format — caller must treat this as TARGET_REF_INVALID.
    /// Silent axes-fallback for malformed refs risks routing to an unintended manifest.
    /// </summary>
    private static bool TryParseManifestTargetRef(string rawTargetRef, out Guid manifestId)
    {
        manifestId = Guid.Empty;
        if (!rawTargetRef.StartsWith("manifest:", StringComparison.OrdinalIgnoreCase))
            return false;

        var parts = rawTargetRef.Split(':', 3);
        if (parts.Length < 2) return false;

        return Guid.TryParse(parts[1], out manifestId);
    }

    /// <summary>
    /// Mirrors frontend/runtime/renderEmission.ts's ADMIN_RUNTIME_TARGET_REF_RE exactly:
    /// "manifest:&lt;36-char-uuid&gt;:&lt;layer&gt;:&lt;action&gt;" — a proper subset of
    /// TryParseManifestTargetRef's lenient Guid.TryParse-based manifest-id extraction above.
    /// </summary>
    private static readonly Regex AdminRuntimeTargetRefRe =
        new(@"^manifest:([0-9a-fA-F-]{36}):([^:]+):([^:]+)$", RegexOptions.Compiled);

    /// <summary>
    /// Round 17/18 hardening: validates that an admin_runtime-destined target_ref's embedded
    /// layer:action suffix (1) matches what the request itself declares as Layer/Action, (2) is an
    /// operation the resolved manifest's own dispatcher_mapping actually authorizes for that
    /// layer/action, AND (3, round 18) that same dispatcher_mapping entry's role (when declared)
    /// matches the request's role — a role="admin" manifest can no longer be invoked by a
    /// non-admin/absent-role request via target_ref. All three are required — (1) alone would not
    /// stop a manifest whose dispatcher_mapping never declared this action at all; (2) alone would
    /// not stop a client sending mismatched target_ref/Layer/Action pairs; (3) alone would not stop
    /// a request claiming an unrelated Layer/Action the manifest never declared. This is
    /// complementary to, not a replacement for, ManifestDispatcher's own uniform
    /// ValidateCapabilityRequirement gate (which runs after every manifest resolution path
    /// regardless of target_ref) — dispatcher_mapping.role is a per-axis-combination authority
    /// declared by the manifest author, capability_requirement is a manifest-wide declared-or-
    /// inferred requirement; both apply. Caller has already confirmed the target_ref matches
    /// AdminRuntimeTargetRefRe.
    /// </summary>
    private static ValidationError? ValidateAdminRuntimeTargetRefLayerAction(
        Match adminRuntimeShapeMatch,
        Guid manifestId,
        IReadOnlyList<JsonElement> topology,
        string? requestLayer,
        string? requestAction,
        string? requestRole)
    {
        var rawTargetRef = adminRuntimeShapeMatch.Value;
        var targetRefLayer = adminRuntimeShapeMatch.Groups[2].Value;
        var targetRefAction = adminRuntimeShapeMatch.Groups[3].Value;

        if (!string.Equals(targetRefLayer, requestLayer, StringComparison.Ordinal) ||
            !string.Equals(targetRefAction, requestAction, StringComparison.Ordinal))
        {
            return new ValidationError(
                "TARGET_REF_LAYER_ACTION_MISMATCH",
                $"target_ref '{rawTargetRef}' encodes layer:action '{targetRefLayer}:{targetRefAction}' but " +
                $"the request declares Layer='{requestLayer}' Action='{requestAction}'.");
        }

        // target is intentionally wildcarded via null: request.Target for these dispatches is the
        // node's targetSurface ("manifest"), not the dispatcher_mapping "target" axis value used by
        // the parallel axes-registered manifests for the same admin action (e.g. auth via
        // /admin/enums' hardcoded route) — checking target here would either force a collision
        // between the two registration styles (MANIFEST_AMBIGUOUS on axes resolution) or reject
        // every legitimate target_ref dispatch.
        if (!DispatcherMappingAxisAuthority.MatchesAxes(topology, role: null, target: null, targetRefLayer, targetRefAction))
        {
            return new ValidationError(
                "TARGET_REF_LAYER_ACTION_UNAUTHORIZED",
                $"manifest {manifestId} has no dispatcher_mapping entry authorizing layer='{targetRefLayer}' action='{targetRefAction}'.");
        }

        // Round 19 defense-in-depth: save-time validation (ManifestTopologyValidator) rejects a
        // manifest topology containing 2+ dispatcher_mapping entries outright, so this should be
        // structurally unreachable for anything authored through the standard create/update/promote
        // flow -- but topology is untyped JSONB, reachable by hand-authored seed SQL or a future
        // write path that bypasses that validator. Rather than let FindDeclaredRole/
        // IsDeclaredIdentitySelectorRead silently pick whichever entry JSONB array order happens to
        // put first, fail closed the moment two entries for the SAME (layer, action) disagree on
        // role or identity_selector_read -- an authorization decision must never depend on
        // unaudited array order.
        if (DispatcherMappingAxisAuthority.HasConflictingDispatcherMappingEntries(topology, targetRefLayer, targetRefAction))
        {
            return new ValidationError(
                "TARGET_REF_DUPLICATE_AXIS_DECLARATION",
                $"manifest {manifestId} declares multiple dispatcher_mapping entries for layer='{targetRefLayer}' " +
                $"action='{targetRefAction}' with disagreeing role/identity_selector_read values.");
        }

        // Role is NOT wildcarded (round 18) -- a manifest's dispatcher_mapping.role for THIS
        // layer/action is real declared authority and request.Role carries the same JWT-claim
        // semantics axes resolution already trusts; there is no registration-collision reason to
        // skip it the way there is for target. Deliberately NOT implemented as
        // MatchesAxes(topology, requestRole, ...): that method's AxisMatches treats a null QUERY
        // value as "match anything", which would let a request with NO role satisfy a manifest that
        // never declares one there — the opposite of authorization semantics wanted here. Instead,
        // FindDeclaredRole finds the entry's OWN role value once (null = that entry is role-
        // wildcard) and compares it against the request's actual role directly.
        //
        // Round 19: role comparison is now OrdinalIgnoreCase, matching AxisMatches's own existing
        // case-insensitive role-axis convention used throughout axes resolution
        // (ResolveActiveManifestAsync, CountActiveAxisConflictsAsync) -- round 18 had introduced a
        // NEW, narrower Ordinal (case-sensitive) comparison here, inconsistent with that
        // pre-existing axis authority for no principled reason. capability_requirement's OWN role
        // check (ValidateCapabilityRequirement) remains independently Ordinal, as it always has been
        // -- a separate, older, complementary gate this alignment does not touch.
        var declaredRole = DispatcherMappingAxisAuthority.FindDeclaredRole(topology, targetRefLayer, targetRefAction);
        if (declaredRole is not null && !string.Equals(declaredRole, requestRole, StringComparison.OrdinalIgnoreCase))
        {
            return new ValidationError(
                "TARGET_REF_ROLE_UNAUTHORIZED",
                $"manifest {manifestId}'s dispatcher_mapping entry for layer='{targetRefLayer}' action='{targetRefAction}' " +
                $"requires role='{declaredRole}'. Request role='{requestRole ?? "(missing)"}' is insufficient.");
        }

        return null;
    }

    private async Task<EndpointResponseDto> DispatchToHandlerAsync(
        string destination,
        EndpointRequestDto request,
        Guid? manifestId,
        CancellationToken ct)
    {
        if (!_runtimeHandlers.TryGetValue(destination, out var handler))
        {
            _logger.LogError(
                "ManifestDispatcher: no handler registered for runtime_destination '{Destination}'.",
                destination);
            await RecordSubstrateErrorAsync(
                "RUNTIME_DESTINATION_UNKNOWN",
                $"runtime_destination '{destination}' is not a registered handler.",
                $"manifest_dispatcher:handler:{destination}", ct);
            return new EndpointResponseDto(
                Success: false,
                Emission: null,
                Errors: [new ValidationError(
                    "RUNTIME_DESTINATION_UNKNOWN",
                    $"runtime_destination '{destination}' is not a registered handler. Known: {string.Join(", ", _runtimeHandlers.Keys)}")]);
        }

        _logger.LogDebug(
            "ManifestDispatcher: dispatching to handler for runtime_destination={Destination}.",
            destination);

        return await handler.ExecuteAsync(request, manifestId, ct);
    }

    private static string? ExtractDbNotifyProjectionDestination(IReadOnlyList<JsonElement> topology)
    {
        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String) continue;
            if (!string.Equals(typeEl.GetString(), "db_notify_projection_mapping", StringComparison.OrdinalIgnoreCase)) continue;
            if (!entry.TryGetProperty("runtime_destination", out var destinationEl) || destinationEl.ValueKind != JsonValueKind.String) continue;

            var destination = destinationEl.GetString();
            if (!string.IsNullOrWhiteSpace(destination))
                return destination;
        }
        return null;
    }

    /// <summary>
    /// Maps legacy change intake shape to canonical dispatch request.
    /// table_name is preserved as Target for registry resolution.
    /// Returns explicit validation errors when minimum identity is missing.
    /// </summary>
    public (EndpointRequestDto? Request, IReadOnlyList<ValidationError> Errors) BuildExistingSystemHookRequest(
        ExistingSystemChangeIntakeRequestDto intake,
        string roleFromJwtClaim)
    {
        ArgumentNullException.ThrowIfNull(intake);

        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(intake.TableName))
            errors.Add(new ValidationError("LEGACY_TABLE_NAME_REQUIRED", "table_name is required."));
        if (string.IsNullOrWhiteSpace(intake.RowId))
            errors.Add(new ValidationError("LEGACY_ROW_ID_REQUIRED", "row_id is required."));
        if (string.IsNullOrWhiteSpace(intake.Operation))
            errors.Add(new ValidationError("LEGACY_OPERATION_REQUIRED", "operation is required."));
        else if (!AllowedLegacyOperations.Contains(intake.Operation))
            errors.Add(new ValidationError("LEGACY_OPERATION_UNSUPPORTED", "operation must be one of: create, update, delete, transition."));
        if (intake.ChangedDataJsonb is null && intake.DiffJsonb is null)
            errors.Add(new ValidationError("LEGACY_CHANGE_PAYLOAD_REQUIRED", "changed_data_jsonb or diff_jsonb is required."));

        if (errors.Count > 0)
            return (null, errors);

        var payload = JsonSerializer.SerializeToElement(new
        {
            table_name = intake.TableName,
            row_id = intake.RowId,
            operation = intake.Operation,
            changed_data_jsonb = intake.ChangedDataJsonb,
            diff_jsonb = intake.DiffJsonb,
            actor = intake.Actor,
            source = intake.Source,
            occurred_at = intake.OccurredAt
        });

        return (new EndpointRequestDto(
            OperationType: "ExistingSystemChangeIntake",
            Target: intake.TableName,
            Layer: "existing_system_intake",
            Action: intake.Operation,
            IdOrHubId: null,
            Payload: payload,
            Context: null,
            TriggerKind: "hook",
            Role: roleFromJwtClaim
        ), []);
    }

    private static readonly HashSet<string> AllowedLegacyOperations =
        new(StringComparer.OrdinalIgnoreCase) { "create", "update", "delete", "transition" };

    /// <summary>
    /// Extracts the projection_definition from the projection_constructor_mapping topology entry.
    /// Returns null when no projection_constructor_mapping entry is present.
    /// The returned JsonElement carries the raw projection_definition JSON for the frontend to
    /// deserialize as ProjectionDefinition — backend does not interpret the structure.
    /// </summary>
    private static JsonElement? ExtractProjectionConstructorMapping(IReadOnlyList<JsonElement> topology)
    {
        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String) continue;
            if (!string.Equals(typeEl.GetString(), "projection_constructor_mapping", StringComparison.OrdinalIgnoreCase)) continue;
            if (!entry.TryGetProperty("projection_definition", out var definitionEl)) continue;
            if (definitionEl.ValueKind == JsonValueKind.Object) return definitionEl;
        }
        return null;
    }

    private static bool HasUiProjectionEntry(IReadOnlyList<JsonElement> topology) =>
        ExtractUiProjectionRefs(topology).LayoutId is not null;

    /// <summary>
    /// Round 18: the closed predicate under which a target_ref reaching an admin_runtime manifest
    /// may skip strict layer:action authorization — deliberately the EXACT pre-condition of the
    /// "structural read fallback" further below in DispatchAsync (manifest has a ui_projection
    /// entry AND the request axes are a registered screen-read shape), not a shape-based guess
    /// about the target_ref string itself. Caller must independently confirm
    /// runtime_destination=="admin_runtime" — kept separate so callers that already resolved the
    /// destination don't redundantly re-extract it here.
    /// </summary>
    private static bool IsGenericStructuralReadTargetRef(
        IReadOnlyList<JsonElement> topology, string? requestLayer, string? requestAction) =>
        HasUiProjectionEntry(topology) &&
        ScreenDataShapeTopologyReader.IsScreenReadAction(requestLayer, requestAction);

    /// <summary>
    /// Round 18 (second live-DB catch): closed allowlist of read-only, manifest-identity-agnostic
    /// navigation actions that a deliberately BARE admin_runtime manifest (no dispatcher_mapping, no
    /// ui_projection — a pure identity/context selector, never an authored operation-scoped
    /// manifest) may reach via target_ref, PROVIDED the same (role, layer, action) is independently,
    /// actively registered under the pre-existing axes-based target="admin" system — the exact
    /// registration ResolveActiveManifestAsync would have found had target_ref not short-circuited
    /// resolution. Deliberately excludes hub_navigation:create/update/deprecate/reorder (real
    /// mutations in the same layer) even though they too happen to be axes-registered under
    /// target="admin" — this axes-registered manifest's OWN declared classification (round 19:
    /// DispatcherMappingAxisAuthority.IsDeclaredIdentitySelectorRead, reading its
    /// "identity_selector_read": true dispatcher_mapping field — db/seed_empty.sql's
    /// hub_navigation:list_manifests/get_hub_relations entries), not a closed action-name allowlist
    /// hardcoded in this runtime code, is what keeps this exemption from becoming a blanket
    /// target="admin"-registered-operation bypass: hub_navigation:create/update/deprecate/reorder
    /// are axes-registered under target="admin" too but declare no such field, so they never
    /// qualify. A manifest that declares ANY dispatcher_mapping or ui_projection entry of its own
    /// never qualifies either — this is not a general escape hatch, only for manifests making no
    /// operation-scope claim whatsoever. Adding a new identity-selector-compatible read action means
    /// authoring "identity_selector_read": true on that action's OWN axes-registered
    /// dispatcher_mapping entry (SSOT-owned seed data) — never editing this method.
    /// </summary>
    private async Task<bool> IsBareManifestNavigationReadTargetRefAsync(
        IReadOnlyList<JsonElement> topology, string? requestRole, string? requestLayer, string? requestAction,
        CancellationToken ct)
    {
        if (requestLayer is null || requestAction is null) return false;
        if (DispatcherMappingAxisAuthority.HasAnyDispatcherMapping(topology)) return false;
        if (HasUiProjectionEntry(topology)) return false;
        if (_manifestRepository is null) return false;

        var axesManifest = await _manifestRepository.ResolveActiveManifestAsync(
            role: requestRole, target: "admin", layer: requestLayer, action: requestAction, ct: ct);
        if (axesManifest is null) return false;

        return DispatcherMappingAxisAuthority.IsDeclaredIdentitySelectorRead(
            axesManifest.Topology, requestLayer, requestAction);
    }

    /// <summary>
    /// Extracts packageId/layoutId from the manifest.topology[ui_projection] refs-only entry
    /// (db-schema.yaml packages/components_package_design.manifest_reference:
    /// manifest.topology[ui_projection].packageIds). Only the first packageIds entry is used —
    /// this bundle resolves a single render surface, not multi-package composition.
    /// </summary>
    private static (Guid? PackageId, Guid? LayoutId) ExtractUiProjectionRefs(IReadOnlyList<JsonElement> topology)
    {
        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String) continue;
            if (!string.Equals(typeEl.GetString(), "ui_projection", StringComparison.OrdinalIgnoreCase)) continue;

            Guid? packageId = null;
            if (entry.TryGetProperty("packageIds", out var packageIdsEl) && packageIdsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var idEl in packageIdsEl.EnumerateArray())
                {
                    if (idEl.ValueKind == JsonValueKind.String && Guid.TryParse(idEl.GetString(), out var parsedPackageId))
                    {
                        packageId = parsedPackageId;
                        break;
                    }
                }
            }

            Guid? layoutId = null;
            if (entry.TryGetProperty("layoutId", out var layoutIdEl) &&
                layoutIdEl.ValueKind == JsonValueKind.String &&
                Guid.TryParse(layoutIdEl.GetString(), out var parsedLayoutId))
            {
                layoutId = parsedLayoutId;
            }

            return (packageId, layoutId);
        }
        return (null, null);
    }

    /// <summary>
    /// Resolves manifest.topology[ui_projection].layoutId into LayoutId/LayoutNodes/PackageId on
    /// the emission, reusing the same topology.ui_topology_tensor / topology.ui_wiring_registry
    /// load path and node validation as StructureMapResolver (TopologyRepository.LoadLayoutNodesAsync,
    /// StructureMapResolver.ValidateLayoutNodes/ToLayoutNode) — no duplicate implementation.
    /// No-op when the manifest has no ui_projection entry, when _topologyRepository is not
    /// injected, or when the emission already carries LayoutId/LayoutNodes from the
    /// structure-map-driven pipeline (topology_transform_runtime never needs this fallback).
    /// A resolved-but-empty/invalid layout is an explicit failure (LAYOUT_NODES_NOT_FOUND or the
    /// structural validation error) appended to Emission.Errors — no silent fallback to flat
    /// component rendering. Unexpected repository failures (e.g. DB unavailable) are logged and
    /// treated as non-fatal for this enrichment step, matching the existing hub-navigation
    /// resolver's non-fatal failure policy.
    /// </summary>
    private async Task<Emission> EnrichWithUiProjectionAsync(
        Emission emission, IReadOnlyList<JsonElement> topology, CancellationToken ct)
    {
        if (_topologyRepository is null) return emission;
        if (emission.LayoutId is not null || emission.LayoutNodes is not null) return emission;

        var (packageId, layoutId) = ExtractUiProjectionRefs(topology);
        if (layoutId is null) return emission;

        IReadOnlyList<LayoutNodeRecord> records;
        try
        {
            records = await _topologyRepository.LoadLayoutNodesAsync(layoutId.Value, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("LAYOUT_SCHEMA_RECORDS_INVALID", StringComparison.Ordinal))
        {
            // A present-but-malformed layout_schema_json.records[] is a real authoring defect,
            // never equivalent to "no records[]" — it must surface as an explicit Emission.Errors
            // entry, never a silent swallow-and-continue.
            _logger.LogError(ex,
                "ManifestDispatcher: layout_schema_json.records[] is malformed for layoutId='{LayoutId}'.",
                layoutId);
            var invalidError = new ValidationError("LAYOUT_SCHEMA_RECORDS_INVALID", ex.Message);
            return emission with { Errors = [.. emission.Errors, invalidError], LayoutId = layoutId.Value.ToString() };
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("LAYOUT_SCHEMA_RUNTIME_INTERACTIONS_INVALID", StringComparison.Ordinal))
        {
            // A malformed/unattributable tensor runtimeInteractions shape is a real authoring
            // defect — never silently skipped during the schema-authority merge.
            _logger.LogError(ex,
                "ManifestDispatcher: tensor runtimeInteractions is malformed for layoutId='{LayoutId}'.",
                layoutId);
            var invalidInteractionsError = new ValidationError("LAYOUT_SCHEMA_RUNTIME_INTERACTIONS_INVALID", ex.Message);
            return emission with { Errors = [.. emission.Errors, invalidInteractionsError], LayoutId = layoutId.Value.ToString() };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ManifestDispatcher: ui_projection layout node resolution failed for layoutId='{LayoutId}'.",
                layoutId);
            return emission;
        }

        if (records.Count == 0)
        {
            var notFoundError = new ValidationError(
                "LAYOUT_NODES_NOT_FOUND",
                $"ui_projection layoutId '{layoutId}' has no nodes in layout_patch_json. " +
                "Broken layout configuration — no fallback to flat component rendering.");
            return emission with { Errors = [.. emission.Errors, notFoundError], LayoutId = layoutId.Value.ToString() };
        }

        var validationErrors = StructureMapResolver.ValidateLayoutNodes(records);
        if (validationErrors is not null)
        {
            return emission with { Errors = [.. emission.Errors, .. validationErrors], LayoutId = layoutId.Value.ToString() };
        }

        var layoutNodes = records.Select(StructureMapResolver.ToLayoutNode).ToList();

        JsonElement? calculationBindings = emission.CalculationBindings;
        var calcJson = await _topologyRepository.LoadLayoutCalcBindingsJsonAsync(layoutId.Value, ct);
        if (!string.IsNullOrWhiteSpace(calcJson))
        {
            try
            {
                calculationBindings = JsonSerializer.Deserialize<JsonElement>(calcJson);
            }
            catch (JsonException)
            {
                // Malformed calculationBindings is non-blocking for this enrichment path — the
                // primary layout render is unaffected; StructureMapResolver's own path already
                // surfaces CALC_BINDINGS_JSON_INVALID for the structure-map-driven pipeline.
            }
        }

        return emission with
        {
            LayoutId = layoutId.Value.ToString(),
            LayoutNodes = layoutNodes,
            PackageId = emission.PackageId ?? packageId,
            CalculationBindings = calculationBindings,
        };
    }

    /// <summary>
    /// Attaches the manifest-scoped hub_relations navigation sequence ("current hub relation"
    /// candidates) to the emission for any runtime_destination, not only
    /// topology_transform_runtime — RuntimeExecutor already performs this for its own path
    /// (non-fatal on failure); this closes the gap for other destinations (e.g. admin_runtime).
    /// No-op when _hubNavigationResolver is not injected or NavigationSequence is already set.
    /// </summary>
    private async Task<Emission> EnrichWithHubNavigationAsync(Emission emission, Guid manifestId, CancellationToken ct)
    {
        if (_hubNavigationResolver is null || emission.NavigationSequence is not null) return emission;

        try
        {
            var navigationSequence = await _hubNavigationResolver.ResolveAsync(manifestId, ct);
            return emission with { NavigationSequence = navigationSequence };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ManifestDispatcher: HubNavigationResolver.ResolveAsync failed for manifest '{ManifestId}'.",
                manifestId);
            return emission;
        }
    }

    /// <summary>
    /// Determines the required role for a manifest by combining:
    /// 1. Explicit capability_requirement topology entry (highest priority).
    /// 2. Inferred requirement from runtime_mapping.runtime_destination:
    ///    admin_runtime → required_role=admin (protects admin manifests that lack an
    ///    explicit capability_requirement, especially on target_ref paths that bypass
    ///    role-axis filtering in ResolveActiveManifestAsync).
    ///
    /// Note: dispatcher_mapping.role is NOT used for inference. That field is a routing
    /// constraint (axes filtering in ResolveActiveManifestAsync) and is already enforced
    /// by the manifest resolution step. Using it as a capability gate would create a
    /// tautological check on axes paths and incorrect constraints on non-admin-runtime
    /// manifests accessed via target_ref.
    ///
    /// Returns a validation error when the required role does not match the requesting role.
    /// Returns null when no requirement can be determined or when the role satisfies the requirement.
    ///
    /// This gate runs after all manifest resolution paths (axes, target_ref, db_notify)
    /// so target_ref routes that bypass role-axis filtering are also protected.
    /// </summary>
    private static ValidationError? ValidateCapabilityRequirement(
        IReadOnlyList<JsonElement> topology,
        string? requestRole)
    {
        var requiredRole = ResolveRequiredRole(topology);
        if (requiredRole is null) return null;

        if (!string.Equals(requestRole, requiredRole, StringComparison.Ordinal))
            return new ValidationError(
                "AUTH_CAPABILITY_DENIED",
                $"This operation requires role='{requiredRole}'. Token role='{requestRole ?? "(missing)"}' is insufficient.");

        return null; // requirement satisfied
    }

    /// <summary>
    /// Resolves the required_role a manifest's topology demands, from an explicit
    /// capability_requirement entry (highest priority) or inferred from
    /// runtime_mapping.runtime_destination=admin_runtime. Returns null when no requirement can be
    /// determined. Shared by ValidateCapabilityRequirement (mutation/dispatch authority) and
    /// HubNavigationResolver (read-only relation fallback role filtering) so both reuse the exact
    /// same manifest capability authority instead of maintaining parallel role-resolution logic.
    /// </summary>
    internal static string? ResolveRequiredRole(IReadOnlyList<JsonElement> topology)
    {
        string? explicitRequired = null;
        string? inferredRequired = null;

        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String) continue;
            var type = typeEl.GetString();

            // Explicit capability_requirement overrides any inference.
            if (string.Equals(type, "capability_requirement", StringComparison.OrdinalIgnoreCase))
            {
                if (entry.TryGetProperty("required_role", out var reqEl) && reqEl.ValueKind == JsonValueKind.String)
                {
                    var r = reqEl.GetString();
                    if (!string.IsNullOrWhiteSpace(r))
                        explicitRequired = r;
                }
            }

            // Infer from runtime_destination: admin_runtime manifests require admin role.
            if (string.Equals(type, "runtime_mapping", StringComparison.OrdinalIgnoreCase))
            {
                if (entry.TryGetProperty("runtime_destination", out var destEl) && destEl.ValueKind == JsonValueKind.String)
                {
                    if (string.Equals(destEl.GetString(), "admin_runtime", StringComparison.OrdinalIgnoreCase))
                        inferredRequired = AuthRealm.AdminRole;
                }
            }
        }

        return explicitRequired ?? inferredRequired;
    }

    /// <summary>
    /// Extracts runtime_destination from the runtime_mapping topology entry.
    /// Returns null when no runtime_mapping entry is present.
    /// </summary>
    private static string? ExtractRuntimeDestination(IReadOnlyList<JsonElement> topology)
    {
        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object)
                continue;

            if (!entry.TryGetProperty("type", out var typeEl) ||
                !string.Equals(typeEl.GetString(), "runtime_mapping", StringComparison.Ordinal))
                continue;

            if (!entry.TryGetProperty("runtime_destination", out var destEl))
                continue;

            var destination = destEl.GetString();
            if (!string.IsNullOrWhiteSpace(destination))
                return destination;
        }

        return null;
    }
}
