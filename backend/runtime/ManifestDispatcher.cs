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
///
/// Dev/demo bypass: when _manifestRepository is null (not injected), TargetDispatchOverride
/// handles demo/entity and admin targets; unhandled requests fall through to the
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

    public ManifestDispatcher(
        ILogger<ManifestDispatcher> logger,
        IReadOnlyDictionary<string, IDispatchableRuntime> runtimeHandlers,
        OperationVectorResolver operationVectorResolver,
        TargetDispatchOverride targetDispatchOverride,
        ManifestRepository? manifestRepository = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _runtimeHandlers = runtimeHandlers ?? throw new ArgumentNullException(nameof(runtimeHandlers));
        _operationVectorResolver = operationVectorResolver ?? throw new ArgumentNullException(nameof(operationVectorResolver));
        _targetDispatchOverride = targetDispatchOverride ?? throw new ArgumentNullException(nameof(targetDispatchOverride));
        _manifestRepository = manifestRepository;
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
    /// When _manifestRepository is null (dev/demo bypass): TargetDispatchOverride handles
    /// demo/entity and admin targets; unhandled requests fall through to the
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
            // Dev/demo bypass: no manifest repository configured.
            // TargetDispatchOverride handles demo/entity and admin targets.
            // This branch is isolated here and is not the production destination selection path.
            _logger.LogDebug("ManifestDispatcher: no manifest repository configured (dev/demo bypass).");

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
            else
            {
                manifest = await _manifestRepository.ResolveActiveManifestAsync(
                role: request.Role,
                target: request.Target,
                layer: request.Layer,
                action: request.Action,
                ct: ct);
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
                return new EndpointResponseDto(
                    Success: false,
                    Emission: null,
                    Errors: [new ValidationError(
                        errorCode,
                        errorMessage)]);
            }

            var projectionDefinition = ExtractProjectionConstructorMapping(manifest.Topology);
            var response = await DispatchToHandlerAsync(destination, request, manifest.ManifestId, ct);

            // Inject projection_constructor_mapping from manifest into emission so the frontend
            // projection runtime can call setProjectionDefinition without frontend topology judgment.
            if (response.Success && response.Emission is not null && projectionDefinition.HasValue)
            {
                var updatedEmission = response.Emission with { ProjectionDefinition = projectionDefinition };
                return response with { Emission = updatedEmission };
            }

            return response;
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("MANIFEST_AMBIGUOUS", StringComparison.Ordinal))
        {
            _logger.LogError(ex, "ManifestDispatcher: ambiguous manifest lookup rejected.");
            return new EndpointResponseDto(
                Success: false,
                Emission: null,
                Errors: [new ValidationError("MANIFEST_AMBIGUOUS", ex.Message)]);
        }
    }

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

    private Task<EndpointResponseDto> DispatchToHandlerAsync(
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
            return Task.FromResult(new EndpointResponseDto(
                Success: false,
                Emission: null,
                Errors: [new ValidationError(
                    "RUNTIME_DESTINATION_UNKNOWN",
                    $"runtime_destination '{destination}' is not a registered handler. Known: {string.Join(", ", _runtimeHandlers.Keys)}")]));
        }

        _logger.LogDebug(
            "ManifestDispatcher: dispatching to handler for runtime_destination={Destination}.",
            destination);

        return handler.ExecuteAsync(request, manifestId, ct);
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
