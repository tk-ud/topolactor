using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Manifest-driven dispatcher. Resolves runtime_destination from manifest axes
/// (role, target, layer, action) and forwards to the appropriate runtime.
///
/// Per SSOT dispatcher_contract:
///   - Reads active manifest from DB by [role, target, layer, action] axes.
///   - No silent fallback: missing manifest -> MANIFEST_NOT_FOUND error.
///   - Ambiguous manifests (multiple active for same axes) -> MANIFEST_AMBIGUOUS error.
///   - Does not own topology transform logic.
///
/// Dev/demo bypass: when _manifestRepository is null (not injected), the dispatcher
/// delegates directly to RuntimeExecutor without manifest resolution. This is an
/// explicit bypass for dev/demo environments without a manifest DB. It is not a
/// silent fallback — it is documented and isolated here.
/// </summary>
public class ManifestDispatcher
{
    private readonly ILogger<ManifestDispatcher> _logger;
    private readonly RuntimeExecutor _runtimeExecutor;
    private readonly ManifestRepository? _manifestRepository;

    public ManifestDispatcher(
        ILogger<ManifestDispatcher> logger,
        RuntimeExecutor runtimeExecutor,
        ManifestRepository? manifestRepository = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _runtimeExecutor = runtimeExecutor ?? throw new ArgumentNullException(nameof(runtimeExecutor));
        _manifestRepository = manifestRepository;
    }

    /// <summary>
    /// Resolves the runtime destination from manifest axes and dispatches the request.
    /// When _manifestRepository is null, delegates directly (dev/demo bypass).
    /// When a manifest repository is configured and no active manifest matches the axes,
    /// returns MANIFEST_NOT_FOUND — no implicit fallthrough to RuntimeExecutor.
    /// </summary>
    public async Task<EndpointResponseDto> DispatchAsync(
        EndpointRequestDto request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug(
            "ManifestDispatcher: resolving destination for Role={Role} Target={Target} Layer={Layer} Action={Action} TriggerKind={TriggerKind}",
            request.Role, request.Target, request.Layer, request.Action, request.TriggerKind);

        if (_manifestRepository is null)
        {
            // Dev/demo bypass: no manifest repository configured.
            _logger.LogDebug("ManifestDispatcher: no manifest repository configured; delegating to RuntimeExecutor (dev/demo bypass).");
            return await _runtimeExecutor.ExecuteAsync(request, manifestId: null, ct);
        }

        try
        {
            var manifest = await _manifestRepository.ResolveActiveManifestAsync(
                role: request.Role,
                target: request.Target,
                layer: request.Layer,
                action: request.Action,
                ct: ct);

            if (manifest is not null)
            {
                _logger.LogDebug(
                    "ManifestDispatcher: resolved manifest {ManifestId} for axes.",
                    manifest.ManifestId);

                var runtimeDestinationError = ValidateRuntimeDestination(manifest.Topology);
                if (runtimeDestinationError is not null)
                    return new EndpointResponseDto(Success: false, Emission: null, Errors: [runtimeDestinationError]);

                return await _runtimeExecutor.ExecuteAsync(request, manifest.ManifestId, ct);
            }

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
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("MANIFEST_AMBIGUOUS", StringComparison.Ordinal))
        {
            _logger.LogError(ex, "ManifestDispatcher: ambiguous manifest lookup rejected.");
            return new EndpointResponseDto(
                Success: false,
                Emission: null,
                Errors: [new ValidationError("MANIFEST_AMBIGUOUS", ex.Message)]);
        }
    }

    
    /// <summary>
    /// Maps legacy change intake shape to canonical dispatch request.
    /// table_name is preserved as Target for registry resolution.
    /// Returns explicit validation errors when minimum identity is missing.
    /// </summary>
    public (EndpointRequestDto? Request, IReadOnlyList<ValidationError> Errors) BuildLegacyHookRequest(LegacyChangeIntakeRequestDto intake)
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
            OperationType: "LegacyChangeIntake",
            Target: intake.TableName,
            Layer: "legacy_mirror",
            Action: intake.Operation,
            IdOrHubId: null,
            Payload: payload,
            Context: null,
            TriggerKind: "hook",
            Role: intake.Role ?? "legacy_system"
        ), []);
    }

private static readonly HashSet<string> AllowedLegacyOperations =
        new(StringComparer.OrdinalIgnoreCase) { "create", "update", "delete", "transition" };

    private static readonly HashSet<string> KnownRuntimeDestinations =
        new(StringComparer.OrdinalIgnoreCase) { "topology_transform_runtime", "registry_attractor_runtime" };

    /// <summary>
    /// Extracts the runtime_destination from a runtime_mapping topology entry.
    /// Returns null when no runtime_mapping entry is present (not an error — manifest may omit it).
    /// Returns a ValidationError when runtime_destination is present but not a known kind.
    /// </summary>
    private ValidationError? ValidateRuntimeDestination(IReadOnlyList<JsonElement> topology)
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
            if (string.IsNullOrWhiteSpace(destination))
                continue;

            if (!KnownRuntimeDestinations.Contains(destination))
            {
                _logger.LogError(
                    "ManifestDispatcher: unknown runtime_destination '{Destination}' in runtime_mapping entry.",
                    destination);
                return new ValidationError(
                    "RUNTIME_DESTINATION_UNKNOWN",
                    $"runtime_destination '{destination}' is not a known kind. Known: topology_transform_runtime, registry_attractor_runtime.");
            }

            _logger.LogDebug(
                "ManifestDispatcher: runtime_mapping resolved runtime_destination={Destination}.",
                destination);
            return null;
        }

        // No runtime_mapping entry — log and continue (not an error).
        _logger.LogDebug("ManifestDispatcher: no runtime_mapping entry in manifest topology; using default path.");
        return null;
    }
}
