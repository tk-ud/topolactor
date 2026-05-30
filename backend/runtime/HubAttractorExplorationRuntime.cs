using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Hub-attractor exploration runtime for SQL Attention.
///
/// Consumes watch change candidates from logs.refresh_logs_current_watch and performs
/// bounded hub-attractor topK neighbor exploration. Returns an exploration result that
/// downstream write_logs_attention can consume — does NOT write to logs.attention here.
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
///   - Writing to logs.attention directly (persistence boundary is SqlAttentionScheduler -> WriteLogsAttentionAsync)
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
    /// Executes one hub-attractor exploration run.
    ///
    /// Returns:
    ///   NoChange   — no change candidates detected; exploration skipped.
    ///   MissingPolicy  — no active policy row; fail-close.
    ///   MalformedPolicy — policy JSON invalid or required key invalid; fail-close.
    ///   Ok         — exploration ran; Result contains hits (may be empty if no hub current records).
    ///
    /// Never writes to logs.attention. Runtime generates phase_vector_json evidence for logs.attention
    /// without mutation / migration / promotion and without deriving phase movement from manifest / policy cap.
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
        {
            _logger.LogDebug(
                "HubAttractorExplorationRuntime: no change candidates — skipping exploration for sourceSetId={SourceSetId} basisWindow={BasisWindow}.",
                sourceSetId, basisWindow);
            return new HubAttractorExplorationRunResult(
                HubAttractorExplorationStatus.NoChange,
                "No change candidates detected — exploration skipped.",
                null,
                executedAt);
        }

        var policyJson = await _topologyRepository.LoadFunctionParameterAsync(
            ExplorationFunctionName, ExplorationPolicyKey, ct);

        if (policyJson is null)
        {
            _logger.LogError(
                "HubAttractorExplorationRuntime: MissingPolicy — no active function_parameters row for '{Fn}/{Key}'.",
                ExplorationFunctionName, ExplorationPolicyKey);
            return new HubAttractorExplorationRunResult(
                HubAttractorExplorationStatus.MissingPolicy,
                $"No active function_parameters row for '{ExplorationFunctionName}/{ExplorationPolicyKey}'.",
                null,
                executedAt);
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
            return new HubAttractorExplorationRunResult(
                HubAttractorExplorationStatus.MalformedPolicy,
                $"Policy JSON for '{ExplorationFunctionName}/{ExplorationPolicyKey}' is malformed: {ex.Message}",
                null,
                executedAt);
        }

        var validationError = ValidatePolicy(policy);
        if (validationError is not null)
        {
            _logger.LogError(
                "HubAttractorExplorationRuntime: MalformedPolicy — invalid policy value: {Detail}",
                validationError);
            return new HubAttractorExplorationRunResult(
                HubAttractorExplorationStatus.MalformedPolicy,
                $"Policy for '{ExplorationFunctionName}/{ExplorationPolicyKey}' has invalid value: {validationError}",
                null,
                executedAt);
        }

        var hubCurrentCandidates = await _sqlAttentionLogsRepository.LoadHubCurrentCandidatesAsync(
            sourceSetId, basisWindow, ct);

        var hits = RunExploration(changeCandidates, hubCurrentCandidates, policy, sourceSetId);

        _logger.LogInformation(
            "HubAttractorExplorationRuntime: exploration complete — sourceSetId={SourceSetId} basisWindow={BasisWindow} changeCandidates={ChangeCandidateCount} hubCurrentCandidates={HubCurrentCount} hits={HitCount}.",
            sourceSetId, basisWindow, changeCandidates.Count, hubCurrentCandidates.Count, hits.Count);

        return new HubAttractorExplorationRunResult(
            HubAttractorExplorationStatus.Ok,
            $"Exploration complete: {hits.Count} hit(s) from {changeCandidates.Count} change candidate(s).",
            new HubAttractorExplorationResult(sourceSetId, basisWindow, hits),
            executedAt);
    }

    /// <summary>
    /// Runs bounded hub-attractor topK exploration gated by w / l2_norm budget tier.
    /// Applies tier-specific topK, hub-table distance band, permutation expansion,
    /// max_hub_kinds_per_current, and max_attention_rows_saved caps.
    /// Does NOT write to logs.attention. Does NOT perform full-space repeated search.
    /// </summary>
    private static IReadOnlyList<HubAttractorExplorationHit> RunExploration(
        IReadOnlyList<WatchChangeCandidate> changeCandidates,
        IReadOnlyList<HubCurrentCandidate> hubCurrentCandidates,
        HubAttractorExplorationPolicy policy,
        string sourceSetId)
    {
        var hits = new List<HubAttractorExplorationHit>();

        var hubsByKind = hubCurrentCandidates
            .GroupBy(h => h.AttractorKey)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var candidate in changeCandidates)
        {
            if (hits.Count >= policy.MaxAttentionRowsSaved)
                break;

            var budgetTier = ClassifyExplorationBudgetTier(candidate, policy);
            var tierLimits = ResolveTierLimits(policy, budgetTier);
            var kindsProcessed = 0;

            foreach (var (_, hubsInKind) in hubsByKind)
            {
                if (kindsProcessed >= policy.MaxHubKindsPerCurrent)
                    break;
                if (hits.Count >= policy.MaxAttentionRowsSaved)
                    break;

                var permutations = tierLimits.PhaseExpansionLimit;
                for (var permIdx = 0; permIdx < permutations; permIdx++)
                {
                    if (hits.Count >= policy.MaxAttentionRowsSaved)
                        break;

                    var permutationKey = permIdx == 0 ? "default" : $"permutation_{permIdx}";

                    var scoredHubs = SelectBoundedHubCandidates(
                        candidate,
                        hubsInKind,
                        tierLimits,
                        permIdx);

                    for (var rank = 0; rank < scoredHubs.Count; rank++)
                    {
                        if (hits.Count >= policy.MaxAttentionRowsSaved)
                            break;

                        var (hub, scoring) = scoredHubs[rank];
                        var phaseVectorJson = BuildPhaseVectorJson(
                            candidate, hub, scoring.VectorJson, budgetTier, tierLimits);
                        hits.Add(new HubAttractorExplorationHit(
                            CurrentId: candidate.CurrentId,
                            HubCurrentId: hub.HubCurrentId,
                            SourceSetId: sourceSetId,
                            HubId: hub.HubId,
                            AttractorKey: hub.AttractorKey,
                            HubRelationId: hub.HubRelationId,
                            RelationRegistryId: hub.RelationRegistryId,
                            NeighborScore: scoring.Score,
                            HitRank: rank + 1,
                            ScoreBand: ClassifyScoreBand(scoring.Score),
                            PermutationKey: permutationKey,
                            L2Norm: candidate.L2Norm,
                            VectorJson: scoring.VectorJson,
                            PhaseVectorJson: phaseVectorJson,
                            EvidenceJson: AugmentEvidenceWithBudgetGate(
                                scoring.EvidenceJson, candidate, budgetTier, tierLimits, permIdx)
                        ));
                    }
                }

                kindsProcessed++;
            }
        }

        return hits;
    }

    /// <summary>
    /// Selects bounded hub candidates within policy-defined distance band (max_hub_tables_per_kind)
    /// and topK. Scores all hubs in kind first so weak tier prefers near-neighbor (highest cosine).
    /// High-tier permutation rounds rotate the distance-band window without full-space search.
    /// </summary>
    private static List<(HubCurrentCandidate Hub, (double Score, string VectorJson, string EvidenceJson) Scoring)>
        SelectBoundedHubCandidates(
            WatchChangeCandidate candidate,
            IReadOnlyList<HubCurrentCandidate> hubsInKind,
            ExplorationBudgetTierLimits tierLimits,
            int permutationIndex)
    {
        var scoredAll = hubsInKind
            .Select(h => (Hub: h, Scoring: ComputeNeighborScore(candidate, h)))
            .OrderByDescending(x => x.Scoring.Score)
            .ThenBy(x => x.Hub.HubCurrentId)
            .ToList();

        IEnumerable<(HubCurrentCandidate Hub, (double Score, string VectorJson, string EvidenceJson) Scoring)> window =
            scoredAll;

        if (tierLimits.MaxHubTablesPerKind < scoredAll.Count)
        {
            var offset = permutationIndex * tierLimits.MaxHubTablesPerKind;
            if (offset >= scoredAll.Count)
                return [];

            window = scoredAll
                .Skip(offset)
                .Take(tierLimits.MaxHubTablesPerKind);
        }

        return window
            .Take(tierLimits.TopKPerHubKind)
            .ToList();
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

    private static string AugmentEvidenceWithBudgetGate(
        string baseEvidenceJson,
        WatchChangeCandidate candidate,
        ExplorationBudgetTier budgetTier,
        ExplorationBudgetTierLimits tierLimits,
        int permutationIndex)
    {
        using var baseDoc = JsonDocument.Parse(baseEvidenceJson);
        var augmented = new Dictionary<string, object?>();
        foreach (var prop in baseDoc.RootElement.EnumerateObject())
            augmented[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.Number when prop.Value.TryGetDouble(out var d) => d,
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => prop.Value.GetRawText()
            };

        augmented["exploration_budget_gate"] = "w_l2_norm";
        augmented["exploration_budget_tier"] = TierToLabel(budgetTier);
        augmented["exploration_search_mode"] = tierLimits.SearchMode;
        augmented["w_l2_norm"] = candidate.L2Norm;
        augmented["norm_level"] = candidate.NormLevel;
        augmented["tier_topK_per_hub_kind"] = tierLimits.TopKPerHubKind;
        augmented["tier_max_hub_tables_per_kind"] = tierLimits.MaxHubTablesPerKind;
        augmented["tier_phase_expansion_limit"] = tierLimits.PhaseExpansionLimit;
        augmented["permutation_index"] = permutationIndex;

        return JsonSerializer.Serialize(augmented);
    }

    /// <summary>
    /// Computes neighbor score using vector cosine similarity between
    /// candidate.BasisVectorJson (logs.current) and hub.AttractorVectorJson (logs.hub_current).
    ///
    /// Returns:
    ///   Score       — cosine similarity clamped [0, 1]. 0 when either vector is empty/unpopulated.
    ///   VectorJson  — convergent neighbor hit vector: dot product component terms (per SSOT).
    ///   EvidenceJson — scoring provenance with cosine_score, overlap_score, key counts, l2_norm.
    ///
    /// When attractor_vector_json is {} (hub refresh not yet implemented), score=0 and
    /// evidence_json documents this fact — no silent fallback, no magic number override.
    /// </summary>
    private static (double Score, string VectorJson, string EvidenceJson) ComputeNeighborScore(
        WatchChangeCandidate candidate,
        HubCurrentCandidate hub)
    {
        var basisVec = FlattenVectorJson(candidate.BasisVectorJson);
        var attractorVec = FlattenVectorJson(hub.AttractorVectorJson);

        var cosineScore = CosineSimilarity(basisVec, attractorVec);
        var overlapScore = OverlapScore(basisVec, attractorVec);
        var neighborScore = Math.Clamp(cosineScore, 0.0, 1.0);

        var sharedKeys = basisVec.Keys.Intersect(attractorVec.Keys).ToList();

        var vectorComponents = sharedKeys
            .ToDictionary(k => k, k => basisVec[k] * attractorVec[k]);

        var vectorJson = JsonSerializer.Serialize(vectorComponents);

        var evidenceJson = JsonSerializer.Serialize(new
        {
            cosine_score = cosineScore,
            overlap_score = overlapScore,
            neighbor_score = neighborScore,
            current_l2_norm = candidate.L2Norm,
            basis_key_count = basisVec.Count,
            attractor_key_count = attractorVec.Count,
            shared_key_count = sharedKeys.Count
        });

        return (neighborScore, vectorJson, evidenceJson);
    }

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
        var axisPopulation = FlattenVectorJson(hub.AxisPopulationJson);
        var axisMovement = FlattenVectorJson(hub.AxisZScoreJson);
        var vectorBasis = NormalizeJsonObjectOrEmpty(vectorJson);
        var vectorKeys = FlattenVectorJson(vectorJson).Keys.OrderBy(k => k).ToArray();
        var phaseBasis = NormalizeJsonObjectOrEmpty(hub.PhaseBasisJson);

        static double GetAxisValue(Dictionary<string, double> values, string key)
            => values.TryGetValue(key, out var v) ? v : 0.0;

        var x = GetAxisValue(axisPopulation, "hub_relations_count");
        var y = GetAxisValue(axisPopulation, "hub_count");
        var z = GetAxisValue(axisPopulation, "topology_manifests_count");

        var i = GetAxisValue(axisMovement, "i");
        var j = GetAxisValue(axisMovement, "j");
        var k = GetAxisValue(axisMovement, "k");

        return JsonSerializer.Serialize(new
        {
            basis_source = "logs.hub_current",
            meaning_boundary = new
            {
                w = "l2_norm",
                x = "hubs_hub_relations_count",
                y = "hubs_hub_count",
                z = "hubs_topology_manifests_count",
                ijk = "axis movement amounts",
                phase_movement_source = "not_manifest_or_policy_cap",
                no_automatic_topology_mutation = true,
                exploration_budget_gate = "w_l2_norm",
                exploration_budget_tier = TierToLabel(budgetTier),
                exploration_search_mode = tierLimits.SearchMode
            },
            w = candidate.L2Norm,
            x,
            y,
            z,
            i,
            j,
            k,
            generated_from = "logs.attention.vector_json",
            vector_keys = vectorKeys,
            vector_basis_json = vectorBasis,
            phase_basis_json = phaseBasis
        });
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
    /// Classifies a neighbor score into a score band per SSOT neighbor_score_policy_range.
    /// strong: 0.95-1.00, normal: 0.90-0.95, exploratory: 0.85-0.90, evidence_only: below 0.85.
    /// </summary>
    internal static string ClassifyScoreBand(double score) =>
        score >= 0.95 ? "strong"
        : score >= 0.90 ? "normal"
        : score >= 0.85 ? "exploratory"
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
            MaxAttentionRowsSaved: RequireInt(root, "max_attention_rows_saved")
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
        return null;
    }
}
