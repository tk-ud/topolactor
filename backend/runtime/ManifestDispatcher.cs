using System.Linq;
using System.Text.Json;
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

        var requiredRole = explicitRequired ?? inferredRequired;
        if (requiredRole is null) return null;

        if (!string.Equals(requestRole, requiredRole, StringComparison.Ordinal))
            return new ValidationError(
                "AUTH_CAPABILITY_DENIED",
                $"This operation requires role='{requiredRole}'. Token role='{requestRole ?? "(missing)"}' is insufficient.");

        return null; // requirement satisfied
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
