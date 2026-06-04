using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Hub-attractor exploration runtime for SQL Attention.
///
/// Consumes watch change candidates from logs.refresh_logs_current_watch. Canonical SQL
/// Attention exploration is related topology_manifest_id[] resolution followed by
/// hubs.hub_relations exploration. The production entry point never falls back to logs.hub_current.
///
/// Policy source: topology.function_parameters
///   function_name = "sql_attention_hub_attractor_exploration"
///   parameter_key = "default_policy"
///   Required keys: norm_level_high, norm_level_medium, exploration_budget_tiers.{weak,mid,high},
///                  max_hub_kinds_per_current, max_attention_rows_saved
///   w / l2_norm gate: weak=near+narrow topK, mid=normal topK,
///                     high=expanded distance band or permutation expansion
///
/// Fail-close invariants:
///   - Policy missing (no active function_parameters row) → MissingPolicy
///   - Policy JSON malformed or required keys missing/non-positive → MalformedPolicy
///   - No change candidates → NoChange (exploration skipped, not an error)
///
/// Prohibited:
///   - Writing to logs.attention directly (persistence boundary is SqlAttentionScheduler -> AppendAttentionGenerationAsync)
///   - registry mutation / migration / column promotion
///   - Magic number policy defaults in runtime code
/// </summary>
public class HubAttractorExplorationRuntime
{
    internal const string ExplorationFunctionName = "sql_attention_hub_attractor_exploration";
    internal const string ExplorationPolicyKey = "default_policy";

    private readonly ILogger<HubAttractorExplorationRuntime> _logger;
    private readonly TopologyRepository _topologyRepository;
    private readonly SqlAttentionLogsRepository _sqlAttentionLogsRepository;

    public HubAttractorExplorationRuntime(
        ILogger<HubAttractorExplorationRuntime> logger,
        TopologyRepository topologyRepository,
        SqlAttentionLogsRepository sqlAttentionLogsRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
        _sqlAttentionLogsRepository = sqlAttentionLogsRepository ?? throw new ArgumentNullException(nameof(sqlAttentionLogsRepository));
    }

    /// <summary>
    /// Executes the canonical SQL Attention exploration entry point.
    ///
    /// Resolves explicit related topology_manifest_id[] bindings and explores manifest-scoped
    /// hubs.hub_relations. This method never falls back to logs.hub_current cosine search.
    /// </summary>
    public async Task<HubAttractorExplorationRunResult> ExploreAsync(
        IReadOnlyList<WatchChangeCandidate> candidates,
        string sourceSetId,
        string basisWindow,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(basisWindow);
        var executedAt = DateTimeOffset.UtcNow;
        var changeCandidates = candidates.Where(c => c.ChangeDetected).ToList();
        if (changeCandidates.Count == 0)
            return new HubAttractorExplorationRunResult(HubAttractorExplorationStatus.NoChange, "No change candidates detected — exploration skipped.", null, executedAt);

        var policyResult = await LoadValidatedPolicyAsync(executedAt, ct);
        if (policyResult.EarlyResult is not null) return policyResult.EarlyResult;
        var policy = policyResult.Policy!;
        var hits = new List<HubAttractorExplorationHit>();
        var resolvedAnyManifest = false;

        foreach (var candidate in changeCandidates)
        {
            if (hits.Count >= policy.MaxAttentionRowsSaved) break;
            var resolution = await _sqlAttentionLogsRepository.ResolveRelatedTopologyManifestsAsync(candidate, ct);
            if (resolution.TopologyManifestIds.Count == 0) continue;
            resolvedAnyManifest = true;
            var relations = await _sqlAttentionLogsRepository.LoadHubRelationExplorationCandidatesAsync(resolution.TopologyManifestIds, ct);
            hits.AddRange(RunCanonicalHubRelationsExploration(candidate, relations, resolution, policy, sourceSetId)
                .Take(policy.MaxAttentionRowsSaved - hits.Count));
        }

        if (!resolvedAnyManifest)
            return new HubAttractorExplorationRunResult(HubAttractorExplorationStatus.NoRelatedTopologyManifest, "No explicit physical table -> topology manifest binding resolved. No logs.hub_current fallback executed.", null, executedAt);
        if (hits.Count == 0)
            return new HubAttractorExplorationRunResult(HubAttractorExplorationStatus.NoHubRelations, "No active hubs.hub_relations candidates with data-defined sql_attention_score were found. No fallback executed.", null, executedAt);

        return new HubAttractorExplorationRunResult(HubAttractorExplorationStatus.Ok, $"Canonical hubs.hub_relations exploration complete: {hits.Count} hit(s).", new HubAttractorExplorationResult(sourceSetId, basisWindow, hits), executedAt);
    }

    private static IReadOnlyList<HubAttractorExplorationHit> RunCanonicalHubRelationsExploration(
        WatchChangeCandidate candidate,
        IReadOnlyList<HubRelationExplorationCandidate> relations,
        RelatedTopologyManifestResolution resolution,
        HubAttractorExplorationPolicy policy,
        string sourceSetId)
    {
        var tier = ClassifyExplorationBudgetTier(candidate, policy);
        var limits = ResolveTierLimits(policy, tier);
        foreach (var relation in relations)
            if (relation.RelationScore is < 0 or > 1)
                throw new InvalidOperationException($"relation_config.sql_attention_score for hub_relation_id={relation.HubRelationId} must be within [0,1].");

        var eligible = relations.Where(r => r.RelationScore >= policy.NeighborScoreMin).ToList();
        var bounded = eligible
            .GroupBy(r => r.TopologyManifestId)
            .OrderBy(g => g.Key)
            .Take(policy.MaxHubKindsPerCurrent)
            .SelectMany(g => g.OrderBy(r => r.SequencePosition).Take(limits.MaxHubTablesPerKind))
            .OrderByDescending(r => r.RelationScore)
            .ThenBy(r => r.SequencePosition)
            .Take(limits.TopKPerHubKind)
            .ToList();
        var hits = new List<HubAttractorExplorationHit>();
        for (var rank = 0; rank < bounded.Count; rank++)
        {
            var relation = bounded[rank];
            var expandedRelations = relations
                .Where(r => r.HubRelationId != relation.HubRelationId)
                .OrderByDescending(r => Math.Abs(r.SequencePosition - relation.SequencePosition))
                .ThenBy(r => r.SequencePosition)
                .Take(limits.PhaseExpansionLimit)
                .ToList();
            var expandedRelationIds = expandedRelations.Select(r => r.HubRelationId).Distinct().ToArray();
            var expandedManifestIds = expandedRelations.Select(r => r.TopologyManifestId).Append(relation.TopologyManifestId).Distinct().ToArray();
            var expandedHubIds = expandedRelations.SelectMany(r => new[] { r.HubId, r.RelatedHubId }).Append(relation.HubId).Append(relation.RelatedHubId).Distinct().ToArray();
            var generationLineId = Guid.NewGuid();
            var phaseJson = BuildCanonicalPhaseVectorJson(candidate, relation, resolution.TopologyManifestIds, expandedRelationIds, expandedManifestIds, expandedHubIds, tier, limits);
            var evidenceJson = JsonSerializer.Serialize(new
            {
                scoring_role = "canonical_hubs_hub_relations_manifest_scoped",
                canonical_exploration_field = "hubs.hub_relations",
                relation_score_source = "relation_config.sql_attention_score",
                relation_score = relation.RelationScore,
                relation_config_json = NormalizeJsonObjectOrEmpty(relation.RelationConfigJson),
                resolver_evidence_json = NormalizeJsonArrayOrEmpty(resolution.ResolverEvidenceJson),
                no_logs_hub_current_fallback = true
            });
            hits.Add(new HubAttractorExplorationHit(candidate.CurrentId, Guid.Empty, sourceSetId, relation.HubId, "hubs.hub_relations", relation.HubRelationId, null, relation.RelationScore, rank + 1, ClassifyScoreBand(relation.RelationScore, policy), "canonical", candidate.L2Norm, "{}", phaseJson, evidenceJson, generationLineId, relation.TopologyManifestId, relation.RelatedHubId, resolution.TopologyManifestIds, expandedRelationIds, expandedManifestIds, expandedHubIds));
        }
        return hits;
    }

    private static string BuildCanonicalPhaseVectorJson(
        WatchChangeCandidate candidate,
        HubRelationExplorationCandidate hit,
        IReadOnlyList<Guid> sourceManifestIds,
        IReadOnlyList<Guid> expandedRelationIds,
        IReadOnlyList<Guid> expandedManifestIds,
        IReadOnlyList<Guid> expandedHubIds,
        ExplorationBudgetTier tier,
        ExplorationBudgetTierLimits limits) =>
        JsonSerializer.Serialize(new
        {
            q_kind = "phaseAT",
            q_is_draft = false,
            generation_status = "generated",
            generated_from = "sql_attention_hit",
            canonical_exploration_field = "hubs.hub_relations",
            w_l2_norm = candidate.L2Norm,
            x_hit_hub_relation_id = hit.HubRelationId,
            y_topology_manifest_id = hit.TopologyManifestId,
            z_hub_id = hit.HubId,
            i_expanded_hub_relation_ids = expandedRelationIds,
            j_expanded_topology_manifest_ids = expandedManifestIds,
            k_expanded_hub_ids = expandedHubIds,
            q_phaseAT_payload = new { evidence_only = true, is_draft = false, source_topology_manifest_ids = sourceManifestIds },
            exploration_budget_tier = TierToLabel(tier),
            exploration_search_mode = limits.SearchMode,
            no_automatic_topology_mutation = true
        });

    private async Task<(HubAttractorExplorationPolicy? Policy, HubAttractorExplorationRunResult? EarlyResult)> LoadValidatedPolicyAsync(
        DateTimeOffset executedAt,
        CancellationToken ct)
    {
        var policyJson = await _topologyRepository.LoadFunctionParameterAsync(
            ExplorationFunctionName, ExplorationPolicyKey, ct);

        if (policyJson is null)
        {
            _logger.LogError(
                "HubAttractorExplorationRuntime: MissingPolicy — no active function_parameters row for '{Fn}/{Key}'.",
                ExplorationFunctionName, ExplorationPolicyKey);
            return (null, new HubAttractorExplorationRunResult(
                HubAttractorExplorationStatus.MissingPolicy,
                $"No active function_parameters row for '{ExplorationFunctionName}/{ExplorationPolicyKey}'.",
                null,
                executedAt));
        }

        HubAttractorExplorationPolicy policy;
        try
        {
            var raw = JsonSerializer.Deserialize<JsonElement>(policyJson);
            policy = ParsePolicy(raw);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            _logger.LogError(ex,
                "HubAttractorExplorationRuntime: MalformedPolicy — '{Fn}/{Key}' could not be parsed.",
                ExplorationFunctionName, ExplorationPolicyKey);
            return (null, new HubAttractorExplorationRunResult(
                HubAttractorExplorationStatus.MalformedPolicy,
                $"Policy JSON for '{ExplorationFunctionName}/{ExplorationPolicyKey}' is malformed: {ex.Message}",
                null,
                executedAt));
        }

        var validationError = ValidatePolicy(policy);
        if (validationError is not null)
        {
            _logger.LogError(
                "HubAttractorExplorationRuntime: MalformedPolicy — invalid policy value: {Detail}",
                validationError);
            return (null, new HubAttractorExplorationRunResult(
                HubAttractorExplorationStatus.MalformedPolicy,
                $"Policy for '{ExplorationFunctionName}/{ExplorationPolicyKey}' has invalid value: {validationError}",
                null,
                executedAt));
        }

        return (policy, null);
    }

    /// <summary>
    /// Classifies w / l2_norm into exploration budget tier.
    /// Prefers candidate.NormLevel from logs.refresh_logs_current_watch; falls back to policy thresholds.
    /// </summary>
    internal static ExplorationBudgetTier ClassifyExplorationBudgetTier(
        WatchChangeCandidate candidate,
        HubAttractorExplorationPolicy policy)
    {
        var normLevel = candidate.NormLevel?.Trim().ToLowerInvariant();
        return normLevel switch
        {
            "high" => ExplorationBudgetTier.High,
            "medium" => ExplorationBudgetTier.Mid,
            "low" => ExplorationBudgetTier.Weak,
            _ => candidate.L2Norm >= policy.NormLevelHigh
                ? ExplorationBudgetTier.High
                : candidate.L2Norm >= policy.NormLevelMedium
                    ? ExplorationBudgetTier.Mid
                    : ExplorationBudgetTier.Weak
        };
    }

    internal static ExplorationBudgetTierLimits ResolveTierLimits(
        HubAttractorExplorationPolicy policy,
        ExplorationBudgetTier tier) =>
        tier switch
        {
            ExplorationBudgetTier.Weak => policy.WeakTier,
            ExplorationBudgetTier.Mid => policy.MidTier,
            ExplorationBudgetTier.High => policy.HighTier,
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown exploration budget tier.")
        };

    internal static string TierToLabel(ExplorationBudgetTier tier) =>
        tier switch
        {
            ExplorationBudgetTier.Weak => "weak",
            ExplorationBudgetTier.Mid => "mid",
            ExplorationBudgetTier.High => "high",
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown exploration budget tier.")
        };

    /// <summary>
    /// Flattens a JSONB vector string to a key→double map.
    /// Nested objects are flattened with dot-notation keys.
    /// Non-numeric values are ignored. Malformed JSON returns empty map.
    /// </summary>
    internal static Dictionary<string, double> FlattenVectorJson(string json)
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}") return result;

        try
        {
            var element = JsonSerializer.Deserialize<JsonElement>(json);
            FlattenElement(element, string.Empty, result);
        }
        catch (JsonException)
        {
            // Malformed vector JSON: treat as empty (score = 0, documented in evidence_json)
        }
        return result;
    }

    private static void FlattenElement(JsonElement element, string prefix, Dictionary<string, double> result)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                var key = prefix.Length > 0 ? $"{prefix}.{prop.Name}" : prop.Name;
                FlattenElement(prop.Value, key, result);
            }
        }
        else if (element.ValueKind == JsonValueKind.Number
                 && prefix.Length > 0
                 && element.TryGetDouble(out var value))
        {
            result[prefix] = value;
        }
    }

    /// <summary>
    /// Cosine similarity between two flat numeric vectors. Returns 0 when either is empty.
    /// Result clamped [0, 1] (negative cosine from anti-correlated vectors treated as 0).
    /// </summary>
    internal static double CosineSimilarity(Dictionary<string, double> a, Dictionary<string, double> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0.0;

        var dot = 0.0;
        foreach (var (key, aVal) in a)
            if (b.TryGetValue(key, out var bVal))
                dot += aVal * bVal;

        var normA = Math.Sqrt(a.Values.Sum(v => v * v));
        var normB = Math.Sqrt(b.Values.Sum(v => v * v));

        if (normA <= 0.0 || normB <= 0.0) return 0.0;

        return Math.Clamp(dot / (normA * normB), 0.0, 1.0);
    }

    /// <summary>
    /// Overlap score: Jaccard-like ratio of shared keys to union keys across both vectors.
    /// Returns 0 when either vector is empty.
    /// </summary>
    internal static double OverlapScore(Dictionary<string, double> a, Dictionary<string, double> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0.0;

        var sharedCount = a.Keys.Count(b.ContainsKey);
        var unionCount = a.Keys.Union(b.Keys).Count();

        return unionCount == 0 ? 0.0 : (double)sharedCount / unionCount;
    }

    private static string BuildPhaseVectorJson(
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

    private static JsonElement NormalizeJsonArrayOrEmpty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return JsonSerializer.Deserialize<JsonElement>("[]");
        try
        {
            var parsed = JsonSerializer.Deserialize<JsonElement>(json);
            return parsed.ValueKind == JsonValueKind.Array ? parsed : JsonSerializer.Deserialize<JsonElement>("[]");
        }
        catch (JsonException) { return JsonSerializer.Deserialize<JsonElement>("[]"); }
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

    /// <summary>
    /// Classifies a neighbor score into a score band using data-defined policy thresholds.
    /// Thresholds are resolved from policy; no hidden literals in runtime code.
    /// </summary>
    internal static string ClassifyScoreBand(double score, HubAttractorExplorationPolicy policy) =>
        score >= policy.StrongHitThreshold ? "strong"
        : score >= policy.NormalHitThreshold ? "normal"
        : score >= policy.ExploratoryHitThreshold ? "exploratory"
        : "evidence_only";

    /// <summary>
    /// Parses the policy JSON element into a HubAttractorExplorationPolicy.
    /// Throws InvalidOperationException if required keys are missing.
    /// </summary>
    private static HubAttractorExplorationPolicy ParsePolicy(JsonElement root)
    {
        static int RequireInt(JsonElement root, string key)
        {
            if (!root.TryGetProperty(key, out var el))
                throw new InvalidOperationException(
                    $"Required policy key '{key}' is missing.");

            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var v))
                return v;

            if (el.ValueKind == JsonValueKind.String &&
                int.TryParse(el.GetString(), out var sv))
                return sv;

            throw new InvalidOperationException(
                $"Required policy key '{key}' is not a valid integer.");
        }

        static double RequireDouble(JsonElement root, string key)
        {
            if (!root.TryGetProperty(key, out var el))
                throw new InvalidOperationException(
                    $"Required policy key '{key}' is missing.");

            if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var v))
                return v;

            if (el.ValueKind == JsonValueKind.String &&
                double.TryParse(el.GetString(), out var sv))
                return sv;

            throw new InvalidOperationException(
                $"Required policy key '{key}' is not a valid number.");
        }

        static ExplorationBudgetTierLimits ParseTierLimits(JsonElement tiersRoot, string tierKey)
        {
            if (!tiersRoot.TryGetProperty(tierKey, out var tierEl)
                || tierEl.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    $"Required policy key 'exploration_budget_tiers.{tierKey}' is missing.");
            }

            if (!tierEl.TryGetProperty("search_mode", out var searchModeEl)
                || searchModeEl.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(searchModeEl.GetString()))
            {
                throw new InvalidOperationException(
                    $"Required policy key 'exploration_budget_tiers.{tierKey}.search_mode' is missing.");
            }

            return new ExplorationBudgetTierLimits(
                TopKPerHubKind: RequireInt(tierEl, "topK_per_hub_kind"),
                MaxHubTablesPerKind: RequireInt(tierEl, "max_hub_tables_per_kind"),
                PhaseExpansionLimit: RequireInt(tierEl, "phase_expansion_limit"),
                SearchMode: searchModeEl.GetString()!
            );
        }

        if (!root.TryGetProperty("exploration_budget_tiers", out var tiersRoot)
            || tiersRoot.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                "Required policy key 'exploration_budget_tiers' is missing.");
        }

        return new HubAttractorExplorationPolicy(
            NormLevelHigh: RequireDouble(root, "norm_level_high"),
            NormLevelMedium: RequireDouble(root, "norm_level_medium"),
            WeakTier: ParseTierLimits(tiersRoot, "weak"),
            MidTier: ParseTierLimits(tiersRoot, "mid"),
            HighTier: ParseTierLimits(tiersRoot, "high"),
            MaxHubKindsPerCurrent: RequireInt(root, "max_hub_kinds_per_current"),
            MaxAttentionRowsSaved: RequireInt(root, "max_attention_rows_saved"),
            NeighborScoreMin: RequireDouble(root, "neighbor_score_min"),
            StrongHitThreshold: RequireDouble(root, "strong_hit_threshold"),
            NormalHitThreshold: RequireDouble(root, "normal_hit_threshold"),
            ExploratoryHitThreshold: RequireDouble(root, "exploratory_hit_threshold")
        );
    }

    /// <summary>
    /// Validates policy values. Thresholds and tier limits must be positive; search_mode non-empty.
    /// Returns a human-readable error message, or null when valid.
    /// </summary>
    private static string? ValidatePolicy(HubAttractorExplorationPolicy policy)
    {
        if (policy.NormLevelMedium <= 0)
            return $"norm_level_medium={policy.NormLevelMedium} must be > 0.";
        if (policy.NormLevelHigh <= policy.NormLevelMedium)
            return $"norm_level_high={policy.NormLevelHigh} must be > norm_level_medium={policy.NormLevelMedium}.";

        foreach (var (label, tier) in new[]
                 {
                     ("weak", policy.WeakTier),
                     ("mid", policy.MidTier),
                     ("high", policy.HighTier)
                 })
        {
            if (tier.TopKPerHubKind <= 0)
                return $"exploration_budget_tiers.{label}.topK_per_hub_kind={tier.TopKPerHubKind} must be > 0.";
            if (tier.MaxHubTablesPerKind <= 0)
                return $"exploration_budget_tiers.{label}.max_hub_tables_per_kind={tier.MaxHubTablesPerKind} must be > 0.";
            if (tier.PhaseExpansionLimit <= 0)
                return $"exploration_budget_tiers.{label}.phase_expansion_limit={tier.PhaseExpansionLimit} must be > 0.";
            if (string.IsNullOrWhiteSpace(tier.SearchMode))
                return $"exploration_budget_tiers.{label}.search_mode must be non-empty.";
        }

        if (policy.MaxHubKindsPerCurrent <= 0)
            return $"max_hub_kinds_per_current={policy.MaxHubKindsPerCurrent} must be > 0.";
        if (policy.MaxAttentionRowsSaved <= 0)
            return $"max_attention_rows_saved={policy.MaxAttentionRowsSaved} must be > 0.";

        if (policy.NeighborScoreMin < 0)
            return $"neighbor_score_min={policy.NeighborScoreMin} must be >= 0.";
        if (policy.ExploratoryHitThreshold < policy.NeighborScoreMin)
            return $"exploratory_hit_threshold={policy.ExploratoryHitThreshold} must be >= neighbor_score_min={policy.NeighborScoreMin}.";
        if (policy.NormalHitThreshold < policy.ExploratoryHitThreshold)
            return $"normal_hit_threshold={policy.NormalHitThreshold} must be >= exploratory_hit_threshold={policy.ExploratoryHitThreshold}.";
        if (policy.StrongHitThreshold < policy.NormalHitThreshold)
            return $"strong_hit_threshold={policy.StrongHitThreshold} must be >= normal_hit_threshold={policy.NormalHitThreshold}.";
        if (policy.StrongHitThreshold > 1.0)
            return $"strong_hit_threshold={policy.StrongHitThreshold} must be <= 1.";

        return null;
    }
}