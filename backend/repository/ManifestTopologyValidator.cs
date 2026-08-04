using System.Text.Json;
using Topolactor.Schema;

namespace Topolactor.Repository;

public record ManifestTopologySummary(
    AdminManifestDispatcherMappingDto? DispatcherMapping,
    AdminManifestRuntimeMappingDto? RuntimeMapping,
    AdminManifestProjectionConstructorMappingDto? ProjectionConstructorMapping,
    IReadOnlyList<string> EntryTypes
);

public record ManifestTopologyValidationResult(
    bool Valid,
    bool IsBlocking,
    IReadOnlyList<ValidationError> Errors,
    ManifestTopologySummary Summary
);

/// <summary>
/// Backend manifest topology validation — frontend must not substitute this boundary.
/// </summary>
public static class ManifestTopologyValidator
{
    public static ManifestTopologyValidationResult Validate(
        IReadOnlyList<JsonElement> topology,
        IReadOnlySet<string> allowedRuntimeDestinations,
        bool checkActiveAxisConflict = false,
        int activeAxisConflictCount = 0)
    {
        var errors = new List<ValidationError>();
        var entryTypes = new List<string>();

        if (topology.Count == 0)
        {
            errors.Add(new ValidationError("MANIFEST_TOPOLOGY_EMPTY", "topology must contain at least one wiring entry."));
            return Blocked(errors, EmptySummary(entryTypes));
        }

        JsonElement? dispatcherEntry = null;
        JsonElement? runtimeEntry = null;
        JsonElement? projectionEntry = null;
        var dispatcherMappingCount = 0;

        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                errors.Add(new ValidationError("MANIFEST_TOPOLOGY_MALFORMED", "topology entries must be JSON objects."));
                continue;
            }

            if (!entry.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
            {
                errors.Add(new ValidationError("MANIFEST_TOPOLOGY_TYPE_MISSING", "topology entry is missing type."));
                continue;
            }

            var type = typeEl.GetString() ?? string.Empty;
            entryTypes.Add(type);

            switch (type)
            {
                case "dispatcher_mapping":
                    dispatcherEntry = entry;
                    dispatcherMappingCount++;
                    break;
                case "runtime_mapping":
                    runtimeEntry = entry;
                    break;
                case "projection_constructor_mapping":
                    projectionEntry = entry;
                    break;
            }
        }

        // Round 19: a manifest topology must declare at most one dispatcher_mapping entry.
        // Without this, save-time validation silently reports the LAST entry in array order (the
        // "dispatcherEntry = entry" assignment above has no duplicate guard) as if it were the only
        // one, discarding the first entry's role/axes with no signal to the author, and leaving
        // dispatch-time authorization (DispatcherMappingAxisAuthority.FindDeclaredRole/
        // IsDeclaredIdentitySelectorRead/MatchesAxes) dependent on JSONB array order for any
        // (layer, action) the duplicates happen to share. Rejected outright, agreeing or not --
        // authoring two entries for the intent of applying to different (layer, action) values is
        // not a case this validator's single-entry summary model (ManifestTopologySummary.
        // DispatcherMapping) represents anyway.
        if (dispatcherMappingCount > 1)
        {
            errors.Add(new ValidationError(
                "MANIFEST_TOPOLOGY_DUPLICATE_DISPATCHER_MAPPING",
                $"topology must declare at most one dispatcher_mapping entry, found {dispatcherMappingCount}."));
        }

        AdminManifestDispatcherMappingDto? dispatcherDto = null;
        if (dispatcherEntry is null)
        {
            errors.Add(new ValidationError("DISPATCHER_MAPPING_MISSING", "dispatcher_mapping entry is required."));
        }
        else
        {
            var role = RequireAxis(dispatcherEntry.Value, "role", errors);
            var target = RequireAxis(dispatcherEntry.Value, "target", errors);
            var layer = RequireAxis(dispatcherEntry.Value, "layer", errors);
            var action = RequireAxis(dispatcherEntry.Value, "action", errors);
            if (role is not null && target is not null && layer is not null && action is not null)
            {
                dispatcherDto = new AdminManifestDispatcherMappingDto(role, target, layer, action);
            }
        }

        AdminManifestRuntimeMappingDto? runtimeDto = null;
        if (runtimeEntry is null)
        {
            errors.Add(new ValidationError("RUNTIME_MAPPING_MISSING", "runtime_mapping entry is required."));
        }
        else
        {
            if (!runtimeEntry.Value.TryGetProperty("runtime_destination", out var destEl) ||
                destEl.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(destEl.GetString()))
            {
                errors.Add(new ValidationError(
                    "RUNTIME_DESTINATION_MISSING",
                    "runtime_mapping.runtime_destination is required."));
            }
            else
            {
                var destination = destEl.GetString()!;
                runtimeDto = new AdminManifestRuntimeMappingDto(destination);
                if (!allowedRuntimeDestinations.Contains(destination))
                {
                    errors.Add(new ValidationError(
                        "RUNTIME_DESTINATION_UNKNOWN",
                        $"runtime_destination '{destination}' is not registered. Known: {string.Join(", ", allowedRuntimeDestinations)}"));
                }
            }
        }

        AdminManifestProjectionConstructorMappingDto? projectionDto = null;
        if (projectionEntry is not null)
        {
            if (!projectionEntry.Value.TryGetProperty("projection_definition", out var defEl) ||
                defEl.ValueKind != JsonValueKind.Object)
            {
                errors.Add(new ValidationError(
                    "PROJECTION_DEFINITION_INVALID",
                    "projection_constructor_mapping.projection_definition must be a JSON object when present."));
            }
            else
            {
                if (!defEl.TryGetProperty("constructorKey", out var ckEl) ||
                    ckEl.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(ckEl.GetString()))
                {
                    errors.Add(new ValidationError(
                        "PROJECTION_CONSTRUCTOR_KEY_MISSING",
                        "projection_definition.constructorKey is required."));
                }

                if (!defEl.TryGetProperty("outputKind", out var okEl) ||
                    okEl.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(okEl.GetString()))
                {
                    errors.Add(new ValidationError(
                        "PROJECTION_OUTPUT_KIND_MISSING",
                        "projection_definition.outputKind is required."));
                }

                projectionDto = new AdminManifestProjectionConstructorMappingDto(true);
            }
        }

        if (checkActiveAxisConflict && activeAxisConflictCount > 0)
        {
            errors.Add(new ValidationError(
                "MANIFEST_ACTIVE_AXES_CONFLICT",
                $"An active manifest already exists for the same [role,target,layer,action] axes ({activeAxisConflictCount} conflict(s))."));
        }

        var summary = new ManifestTopologySummary(
            dispatcherDto,
            runtimeDto,
            projectionDto,
            entryTypes);

        var isBlocking = errors.Count > 0;
        return new ManifestTopologyValidationResult(!isBlocking, isBlocking, errors, summary);
    }

    public static AdminManifestTopologySummaryDto ToDto(ManifestTopologySummary summary) =>
        new(
            summary.DispatcherMapping,
            summary.RuntimeMapping,
            summary.ProjectionConstructorMapping,
            summary.EntryTypes);

    public static ManifestTopologySummary ExtractSummary(IReadOnlyList<JsonElement> topology)
    {
        var entryTypes = new List<string>();
        JsonElement? dispatcherEntry = null;
        JsonElement? runtimeEntry = null;
        JsonElement? projectionEntry = null;

        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String) continue;
            var type = typeEl.GetString() ?? string.Empty;
            entryTypes.Add(type);
            switch (type)
            {
                case "dispatcher_mapping": dispatcherEntry = entry; break;
                case "runtime_mapping": runtimeEntry = entry; break;
                case "projection_constructor_mapping": projectionEntry = entry; break;
            }
        }

        AdminManifestDispatcherMappingDto? dispatcherDto = null;
        if (dispatcherEntry is not null &&
            TryReadAxis(dispatcherEntry.Value, "role", out var role) &&
            TryReadAxis(dispatcherEntry.Value, "target", out var target) &&
            TryReadAxis(dispatcherEntry.Value, "layer", out var layer) &&
            TryReadAxis(dispatcherEntry.Value, "action", out var action))
        {
            dispatcherDto = new AdminManifestDispatcherMappingDto(role, target, layer, action);
        }

        AdminManifestRuntimeMappingDto? runtimeDto = null;
        if (runtimeEntry is not null &&
            runtimeEntry.Value.TryGetProperty("runtime_destination", out var destEl) &&
            destEl.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(destEl.GetString()))
        {
            runtimeDto = new AdminManifestRuntimeMappingDto(destEl.GetString()!);
        }

        AdminManifestProjectionConstructorMappingDto? projectionDto = null;
        if (projectionEntry is not null &&
            projectionEntry.Value.TryGetProperty("projection_definition", out var defEl) &&
            defEl.ValueKind == JsonValueKind.Object)
        {
            projectionDto = new AdminManifestProjectionConstructorMappingDto(true);
        }

        return new ManifestTopologySummary(dispatcherDto, runtimeDto, projectionDto, entryTypes);
    }

    public static IReadOnlyList<JsonElement> BuildTopology(
        string role,
        string target,
        string layer,
        string action,
        string runtimeDestination,
        JsonElement? projectionDefinition)
    {
        var entries = new List<JsonElement>
        {
            JsonSerializer.SerializeToElement(new
            {
                type = "dispatcher_mapping",
                role,
                target,
                layer,
                action,
            }),
            JsonSerializer.SerializeToElement(new
            {
                type = "db_notify_projection_mapping",
                runtime_destination = "sse_projection_runtime",
            }),
            JsonSerializer.SerializeToElement(new
            {
                type = "runtime_mapping",
                runtime_destination = runtimeDestination,
            }),
        };

        if (projectionDefinition is { ValueKind: JsonValueKind.Object } def)
        {
            entries.Add(JsonSerializer.SerializeToElement(new
            {
                type = "projection_constructor_mapping",
                projection_definition = def,
            }));
        }

        return entries;
    }

    private static string? RequireAxis(JsonElement entry, string name, List<ValidationError> errors)
    {
        if (!entry.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(el.GetString()))
        {
            errors.Add(new ValidationError(
                "DISPATCHER_AXIS_MISSING",
                $"dispatcher_mapping.{name} is required."));
            return null;
        }

        return el.GetString()!;
    }

    private static bool TryReadAxis(JsonElement entry, string name, out string value)
    {
        value = string.Empty;
        if (!entry.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String) return false;
        var raw = el.GetString();
        if (string.IsNullOrWhiteSpace(raw)) return false;
        value = raw;
        return true;
    }

    private static ManifestTopologyValidationResult Blocked(
        IReadOnlyList<ValidationError> errors,
        ManifestTopologySummary summary) =>
        new(false, true, errors, summary);

    private static ManifestTopologySummary EmptySummary(IReadOnlyList<string> entryTypes) =>
        new(null, null, null, entryTypes);
}
