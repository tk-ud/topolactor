using System.Text.Json;
using Topolactor.Schema;

namespace Topolactor.Repository;

public record PromotionManifestValidationResult(
    bool Valid,
    bool IsBlocking,
    IReadOnlyList<ValidationError> Errors,
    AdminPromotionManifestMetadataDto? Metadata
);

public static class PromotionManifestValidator
{
    public const string EntryType = "promotion_metadata_mapping";

    private static readonly HashSet<string> AllowedActivationPolicyTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "manual",
        "scheduled",
        "conditional",
    };

    public static JsonElement? ExtractEntry(IReadOnlyList<JsonElement> topology)
    {
        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String) continue;
            if (string.Equals(typeEl.GetString(), EntryType, StringComparison.OrdinalIgnoreCase))
                return entry;
        }
        return null;
    }

    public static AdminPromotionManifestMetadataDto? ExtractMetadataDto(IReadOnlyList<JsonElement> topology)
    {
        var entry = ExtractEntry(topology);
        if (entry is null) return null;
        return ParseMetadataFromEntry(entry.Value);
    }

    public static AdminPromotionManifestMetadataDto? ParseMetadataFromEntry(JsonElement entry)
    {
        var manifestKey = ReadString(entry, "manifestKey");
        var versionLabel = ReadString(entry, "versionLabel");
        if (manifestKey is null || versionLabel is null) return null;

        var disclosure = entry.TryGetProperty("disclosure", out var discEl) && discEl.ValueKind == JsonValueKind.Object
            ? discEl
            : default;
        var disclosureText = disclosure.ValueKind == JsonValueKind.Object
            ? ReadString(disclosure, "text") ?? string.Empty
            : string.Empty;
        var disclosureCategory = disclosure.ValueKind == JsonValueKind.Object
            ? ReadString(disclosure, "categoryLabel")
            : null;

        var placement = entry.TryGetProperty("displayPlacement", out var placeEl) && placeEl.ValueKind == JsonValueKind.Object
            ? placeEl
            : default;
        var placementKey = placement.ValueKind == JsonValueKind.Object ? ReadString(placement, "placementKey") ?? "" : "";
        var surfaceType = placement.ValueKind == JsonValueKind.Object
            ? ReadString(placement, "projectionSurfaceType") ?? ""
            : "";

        var policy = entry.TryGetProperty("activationPolicy", out var polEl) && polEl.ValueKind == JsonValueKind.Object
            ? polEl
            : default;
        var policyType = policy.ValueKind == JsonValueKind.Object ? ReadString(policy, "policyType") ?? "" : "";
        var conditionExpr = policy.ValueKind == JsonValueKind.Object
            ? ReadString(policy, "conditionExpression")
            : null;

        var refs = new List<AdminPromotionManifestTargetRefDto>();
        if (entry.TryGetProperty("targetTopologyRefs", out var refsEl) && refsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var refEl in refsEl.EnumerateArray())
            {
                if (refEl.ValueKind != JsonValueKind.Object) continue;
                var packageId = ReadString(refEl, "packageId");
                var schemaId = ReadString(refEl, "schemaId");
                var componentId = ReadString(refEl, "componentId");
                if (packageId is null || schemaId is null || componentId is null) continue;
                refs.Add(new AdminPromotionManifestTargetRefDto(packageId, schemaId, componentId));
            }
        }

        return new AdminPromotionManifestMetadataDto(
            manifestKey,
            versionLabel,
            disclosureText,
            disclosureCategory,
            placementKey,
            surfaceType,
            policyType,
            conditionExpr,
            refs);
    }

    public static JsonElement BuildEntry(AdminPromotionManifestMetadataDto metadata)
    {
        return JsonSerializer.SerializeToElement(new
        {
            type = EntryType,
            manifestKey = metadata.ManifestKey,
            versionLabel = metadata.VersionLabel,
            disclosure = new
            {
                text = metadata.DisclosureText,
                categoryLabel = metadata.DisclosureCategoryLabel,
            },
            displayPlacement = new
            {
                placementKey = metadata.PlacementKey,
                projectionSurfaceType = metadata.ProjectionSurfaceType,
            },
            activationPolicy = new
            {
                policyType = metadata.ActivationPolicyType,
                conditionExpression = metadata.ActivationConditionExpression,
            },
            targetTopologyRefs = metadata.TargetTopologyRefs.Select(r => new
            {
                packageId = r.PackageId,
                schemaId = r.SchemaId,
                componentId = r.ComponentId,
            }).ToArray(),
        });
    }

    public static IReadOnlyList<JsonElement> MergeIntoTopology(
        IReadOnlyList<JsonElement> topology,
        JsonElement promotionEntry)
    {
        var merged = topology
            .Where(e =>
            {
                if (e.ValueKind != JsonValueKind.Object) return true;
                if (!e.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String) return true;
                return !string.Equals(typeEl.GetString(), EntryType, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();
        merged.Add(promotionEntry);
        return merged;
    }

    public static PromotionManifestValidationResult ValidateStructure(
        IReadOnlyList<JsonElement> topology,
        int activeKeyConflictCount = 0)
    {
        var errors = new List<ValidationError>();
        var entry = ExtractEntry(topology);
        if (entry is null)
        {
            errors.Add(new ValidationError(
                "PROMOTION_METADATA_MISSING",
                "promotion_metadata_mapping entry is required for promotion manifest validation."));
            return new PromotionManifestValidationResult(false, true, errors, null);
        }

        var metadata = ParseMetadataFromEntry(entry.Value);
        if (metadata is null)
        {
            errors.Add(new ValidationError(
                "PROMOTION_METADATA_MALFORMED",
                "promotion_metadata_mapping is malformed."));
            return new PromotionManifestValidationResult(false, true, errors, null);
        }

        if (string.IsNullOrWhiteSpace(metadata.ManifestKey))
            errors.Add(new ValidationError("PROMOTION_MANIFEST_KEY_REQUIRED", "manifestKey is required."));
        if (string.IsNullOrWhiteSpace(metadata.VersionLabel))
            errors.Add(new ValidationError("PROMOTION_VERSION_LABEL_REQUIRED", "versionLabel is required."));
        if (string.IsNullOrWhiteSpace(metadata.DisclosureText))
            errors.Add(new ValidationError("PROMOTION_DISCLOSURE_TEXT_REQUIRED", "disclosure text is required."));
        if (string.IsNullOrWhiteSpace(metadata.PlacementKey))
            errors.Add(new ValidationError("PROMOTION_PLACEMENT_KEY_REQUIRED", "placementKey is required."));
        if (string.IsNullOrWhiteSpace(metadata.ProjectionSurfaceType))
        {
            errors.Add(new ValidationError(
                "PROMOTION_PROJECTION_SURFACE_REQUIRED",
                "projectionSurfaceType is required."));
        }
        if (string.IsNullOrWhiteSpace(metadata.ActivationPolicyType))
        {
            errors.Add(new ValidationError(
                "PROMOTION_ACTIVATION_POLICY_REQUIRED",
                "activationPolicy.policyType is required."));
        }
        else if (!AllowedActivationPolicyTypes.Contains(metadata.ActivationPolicyType))
        {
            errors.Add(new ValidationError(
                "PROMOTION_ACTIVATION_POLICY_INVALID",
                $"activationPolicy.policyType must be one of: {string.Join(", ", AllowedActivationPolicyTypes)}."));
        }

        if (metadata.TargetTopologyRefs.Count == 0)
        {
            errors.Add(new ValidationError(
                "PROMOTION_TARGET_REFS_REQUIRED",
                "at least one targetTopologyRef is required."));
        }

        foreach (var r in metadata.TargetTopologyRefs)
        {
            if (!Guid.TryParse(r.PackageId, out _))
                errors.Add(new ValidationError("PROMOTION_PACKAGE_REF_INVALID", $"Invalid packageId ref: {r.PackageId}."));
            if (!Guid.TryParse(r.SchemaId, out _))
                errors.Add(new ValidationError("PROMOTION_SCHEMA_REF_INVALID", $"Invalid schemaId ref: {r.SchemaId}."));
            if (!Guid.TryParse(r.ComponentId, out _))
                errors.Add(new ValidationError("PROMOTION_COMPONENT_REF_INVALID", $"Invalid componentId ref: {r.ComponentId}."));
        }

        if (activeKeyConflictCount > 0)
        {
            errors.Add(new ValidationError(
                "PROMOTION_MANIFEST_KEY_CONFLICT",
                $"An active promotion manifest already exists for manifestKey={metadata.ManifestKey} versionLabel={metadata.VersionLabel}."));
        }

        var blocking = errors.Count > 0;
        return new PromotionManifestValidationResult(!blocking, blocking, errors, metadata);
    }

    public static async Task<PromotionManifestValidationResult> ValidateAsync(
        IReadOnlyList<JsonElement> topology,
        TopologyRepository topologyRepository,
        int activeKeyConflictCount,
        CancellationToken ct = default)
    {
        var structural = ValidateStructure(topology, activeKeyConflictCount);
        if (structural.IsBlocking || structural.Metadata is null)
            return structural;

        var errors = structural.Errors.ToList();
        var metadata = structural.Metadata;

        foreach (var r in metadata.TargetTopologyRefs)
        {
            var package = await topologyRepository.LoadPackageAsync(Guid.Parse(r.PackageId), ct);
            if (package is null)
            {
                errors.Add(new ValidationError(
                    "PROMOTION_PACKAGE_REF_NOT_FOUND",
                    $"packageId {r.PackageId} does not resolve in topology."));
            }

            var schema = await topologyRepository.LoadSchemaAsync(Guid.Parse(r.SchemaId), ct);
            if (schema is null)
            {
                errors.Add(new ValidationError(
                    "PROMOTION_SCHEMA_REF_NOT_FOUND",
                    $"schemaId {r.SchemaId} does not resolve in topology."));
            }

            var component = await topologyRepository.LoadComponentAsync(Guid.Parse(r.ComponentId), ct);
            if (component is null)
            {
                errors.Add(new ValidationError(
                    "PROMOTION_COMPONENT_REF_NOT_FOUND",
                    $"componentId {r.ComponentId} does not resolve in topology."));
            }
        }

        var structureMap = await topologyRepository.LoadStructureMapAsync(metadata.PlacementKey, ct);
        if (structureMap is null)
        {
            errors.Add(new ValidationError(
                "PROMOTION_PLACEMENT_KEY_NOT_FOUND",
                $"placementKey '{metadata.PlacementKey}' does not resolve to a topology target."));
        }

        var blocking = errors.Count > 0;
        return new PromotionManifestValidationResult(!blocking, blocking, errors, metadata);
    }

    private static string? ReadString(JsonElement entry, string propName)
    {
        if (!entry.TryGetProperty(propName, out var el) || el.ValueKind != JsonValueKind.String) return null;
        var value = el.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
