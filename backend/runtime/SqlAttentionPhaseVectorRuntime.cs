using System.Text.Json;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Scheduler-runtime phase_vector generation (SSOT: sql-attention-logs-ssot generate_phase_vector).
///
/// Canonical phaseAT evidence is derived only from hubs.hub_relations exploration results
/// after SQLAT hit identity (x/y/z) and w-bounded ID-space expansion (i/j/k).
/// Manifest/policy cap values must not author phase movement; exploration_budget_tier
/// only bounds expansion limits from data-defined policy — not phase axis semantics.
/// </summary>
public static class SqlAttentionPhaseVectorRuntime
{
    /// <summary>
    /// Generates append-only phaseAT evidence JSON from canonical hub-relation exploration.
    /// </summary>
    public static string GeneratePhaseVector(PhaseVectorGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.HitHubRelationId == Guid.Empty)
            throw new ArgumentException("Hit hub_relation_id is required for canonical phase vector generation.", nameof(request));

        return JsonSerializer.Serialize(new
        {
            q_kind = "phaseAT",
            q_is_draft = false,
            generation_status = "generated",
            generated_from = "sql_attention_hit",
            canonical_exploration_field = "hubs.hub_relations",
            phase_movement_is_not_manifest_or_policy_cap_derived = true,
            w_l2_norm = request.WL2Norm,
            x_hit_hub_relation_id = request.HitHubRelationId,
            y_topology_manifest_id = request.TopologyManifestId,
            z_hub_id = request.HubId,
            i_expanded_hub_relation_ids = request.ExpandedHubRelationIds,
            j_expanded_topology_manifest_ids = request.ExpandedTopologyManifestIds,
            k_expanded_hub_ids = request.ExpandedHubIds,
            q_phaseAT_payload = new
            {
                evidence_only = true,
                is_draft = false,
                source_topology_manifest_ids = request.SourceTopologyManifestIds
            },
            exploration_budget_tier = request.ExplorationBudgetTierLabel,
            exploration_search_mode = request.ExplorationSearchMode,
            no_automatic_topology_mutation = true
        });
    }

    /// <summary>
    /// Deprecated logs.hub_current diagnostics shape — not used on canonical exploration route.
    /// Retained for SQL function parity tests only.
    /// </summary>
    public static string BuildPhaseVectorJson(
        WatchChangeCandidate candidate,
        HubCurrentCandidate hub,
        string vectorJson,
        ExplorationBudgetTier budgetTier,
        ExplorationBudgetTierLimits tierLimits)
    {
        var legacyAxisPopulation = FlattenVectorJson(hub.AxisPopulationJson);
        var legacyAxisMovement = FlattenVectorJson(hub.AxisZScoreJson);
        var vectorBasis = NormalizeJsonObjectOrEmpty(vectorJson);
        var vectorKeys = FlattenVectorJson(vectorJson).Keys.OrderBy(k => k).ToArray();
        var phaseBasis = NormalizeJsonObjectOrEmpty(hub.PhaseBasisJson);

        static double GetLegacyValue(Dictionary<string, double> values, string key)
            => values.TryGetValue(key, out var v) ? v : 0.0;

        return JsonSerializer.Serialize(new
        {
            q_kind = "phaseAT",
            q_is_draft = false,
            generation_status = "deprecated_support_cache_diagnostics_only",
            pending_reason = "legacy support-cache helper is deprecated; canonical phaseAT generation is emitted by manifest-scoped hubs.hub_relations exploration",
            canonical_exploration_field = "hubs.hub_relations",
            legacy_support_cache_source = "logs.hub_current",
            meaning_boundary = new
            {
                w = "l2_norm",
                x = "hit_hub_relation_id",
                y = "topology_manifest_id",
                z = "hub_id",
                ijk = "expanded ID arrays",
                q = "logs.attention.phaseAT append-only evidence row",
                q_is_draft = false,
                legacy_count_scalar_axes_deprecated = true,
                no_automatic_topology_mutation = true,
                exploration_budget_gate = "w_l2_norm",
                exploration_budget_tier = TierToLabel(budgetTier),
                exploration_search_mode = tierLimits.SearchMode
            },
            w_l2_norm = candidate.L2Norm,
            x_hit_hub_relation_id = (string?)null,
            y_topology_manifest_id = (string?)null,
            z_hub_id = (string?)null,
            i_expanded_hub_relation_ids = Array.Empty<string>(),
            j_expanded_topology_manifest_ids = Array.Empty<string>(),
            k_expanded_hub_ids = Array.Empty<string>(),
            q_phaseAT_payload = new
            {
                status = "deprecated_support_cache_diagnostics_only",
                evidence_only = true,
                is_draft = false
            },
            legacy_support_cache_statistics = new
            {
                hub_relations_count = GetLegacyValue(legacyAxisPopulation, "hub_relations_count"),
                hub_count = GetLegacyValue(legacyAxisPopulation, "hub_count"),
                topology_manifests_count = GetLegacyValue(legacyAxisPopulation, "topology_manifests_count")
            },
            legacy_axis_movement_observations = new
            {
                i = GetLegacyValue(legacyAxisMovement, "i"),
                j = GetLegacyValue(legacyAxisMovement, "j"),
                k = GetLegacyValue(legacyAxisMovement, "k")
            },
            generated_from = "legacy_logs_hub_current_support_cache_diagnostics",
            vector_keys = vectorKeys,
            vector_basis_json = vectorBasis,
            phase_basis_json = phaseBasis
        });
    }

    private static string TierToLabel(ExplorationBudgetTier tier) =>
        tier switch
        {
            ExplorationBudgetTier.Weak => "weak",
            ExplorationBudgetTier.Mid => "mid",
            ExplorationBudgetTier.High => "high",
            _ => "unknown"
        };

    private static Dictionary<string, double> FlattenVectorJson(string? json)
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            using var doc = JsonDocument.Parse(json);
            FlattenElement(doc.RootElement, "", result);
        }
        catch (JsonException) { }
        return result;
    }

    private static void FlattenElement(JsonElement element, string prefix, Dictionary<string, double> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var key = prefix.Length > 0 ? $"{prefix}.{prop.Name}" : prop.Name;
                    FlattenElement(prop.Value, key, result);
                }
                break;
            case JsonValueKind.Number when prefix.Length > 0 && element.TryGetDouble(out var value):
                result[prefix] = value;
                break;
        }
    }

    private static JsonElement NormalizeJsonObjectOrEmpty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return JsonSerializer.Deserialize<JsonElement>("{}");
        try
        {
            var parsed = JsonSerializer.Deserialize<JsonElement>(json);
            return parsed.ValueKind == JsonValueKind.Object
                ? parsed
                : JsonSerializer.Deserialize<JsonElement>("{}");
        }
        catch (JsonException)
        {
            return JsonSerializer.Deserialize<JsonElement>("{}");
        }
    }
}

/// <summary>Input for canonical phase_vector generation from hubs exploration only.</summary>
public record PhaseVectorGenerationRequest(
    double WL2Norm,
    Guid HitHubRelationId,
    Guid TopologyManifestId,
    Guid HubId,
    IReadOnlyList<Guid> SourceTopologyManifestIds,
    IReadOnlyList<Guid> ExpandedHubRelationIds,
    IReadOnlyList<Guid> ExpandedTopologyManifestIds,
    IReadOnlyList<Guid> ExpandedHubIds,
    string ExplorationBudgetTierLabel,
    string ExplorationSearchMode
);
