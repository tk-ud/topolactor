namespace Topolactor.Schema;

/// <summary>
/// Watch change candidate returned by logs.refresh_logs_current_watch.
/// Only candidates with ChangeDetected=true are eligible for hub-attractor exploration.
/// L2Norm and BasisVectorJson are included in the function return set (no separate JOIN needed).
/// </summary>
public record WatchChangeCandidate(
    Guid CurrentId,
    string PhysicalTableId,
    int NormRank,
    string? PreviousNormLevel,
    string? NormLevel,
    bool ChangeDetected,
    string? ChangeReason,
    double L2Norm,
    string BasisVectorJson,
    string PhysicalTableName = ""
);

/// <summary>
/// Deprecated diagnostics-only support-cache candidate loaded from logs.hub_current.
/// It is not a canonical SQL Attention hubs.hub_relations exploration target.
/// </summary>
public record HubCurrentCandidate(
    Guid HubCurrentId,
    string SourceSetId,
    Guid? HubId,
    string AttractorKey,
    Guid? HubRelationId,
    Guid? RelationRegistryId,
    string BasisWindow,
    string AttractorVectorJson,
    long PopulationCount,
    long PopulationRecordcount,
    string AxisPopulationJson,
    string AxisZScoreJson,
    string PhaseBasisJson
);

/// <summary>
/// w / l2_norm exploration budget tier per SSOT phase_attention_axis_mapping.exploration_budget_gate.
/// weak = near-neighbor + narrow topK; mid = normal topK; high = expanded distance band or permutation.
/// </summary>
public enum ExplorationBudgetTier
{
    Weak,
    Mid,
    High
}

/// <summary>
/// Per-tier exploration limits from policy exploration_budget_tiers.{weak,mid,high}.
/// All numeric values must be positive — no magic number defaults in runtime code.
/// </summary>
public record ExplorationBudgetTierLimits(
    int TopKPerHubKind,
    int MaxHubTablesPerKind,
    int PhaseExpansionLimit,
    string SearchMode
);

/// <summary>
/// Exploration policy resolved from topology.function_parameters.
/// w / l2_norm thresholds (norm_level_high/medium) classify budget tier; tier limits
/// bound topK, hub-table distance band, and permutation expansion — not full-space search.
/// neighbor_score_policy keys (neighbor_score_min, strong/normal/exploratory_hit_threshold)
/// resolve score band classification from data-defined source — no hidden literals.
/// Policy source: function_name="sql_attention_hub_attractor_exploration" parameter_key="default_policy".
/// </summary>
public record HubAttractorExplorationPolicy(
    double NormLevelHigh,
    double NormLevelMedium,
    ExplorationBudgetTierLimits WeakTier,
    ExplorationBudgetTierLimits MidTier,
    ExplorationBudgetTierLimits HighTier,
    int MaxHubKindsPerCurrent,
    int MaxAttentionRowsSaved,
    double NeighborScoreMin,
    double StrongHitThreshold,
    double NormalHitThreshold,
    double ExploratoryHitThreshold
);

/// <summary>
/// Explicit topology manifest binding resolved from one logs.current physical table candidate.
/// Empty resolution is an explicit no-hit boundary; no implicit or oldest-row fallback is allowed.
/// </summary>
public record RelatedTopologyManifestResolution(
    Guid CurrentId,
    IReadOnlyList<Guid> TopologyManifestIds,
    string ResolverEvidenceJson
);

/// <summary>
/// Canonical hubs.hub_relations exploration candidate loaded from active manifest scopes.
/// RelationScore is required data-defined evidence from relation_config.sql_attention_score.
/// </summary>
public record HubRelationExplorationCandidate(
    Guid HubRelationId,
    Guid TopologyManifestId,
    Guid HubId,
    Guid RelatedHubId,
    int SequencePosition,
    double RelationScore,
    string RelationConfigJson
);

/// <summary>
/// Dedicated append-only SQLAT -> phaseAT generation request.
/// </summary>
public record AttentionGenerationAppendRequest(
    Guid GenerationLineId,
    Guid CurrentId,
    string SourceSetId,
    Guid TopologyManifestId,
    Guid HubRelationId,
    Guid HubId,
    double NeighborScore,
    int HitRank,
    string ScoreBand,
    double L2Norm,
    string PhaseVectorJson,
    string EvidenceJson,
    IReadOnlyList<Guid> SourceTopologyManifestIds,
    IReadOnlyList<Guid> ExpandedHubRelationIds,
    IReadOnlyList<Guid> ExpandedTopologyManifestIds,
    IReadOnlyList<Guid> ExpandedHubIds,
    string ArchivePolicy = "required"
);

public record AttentionGenerationAppendResult(
    Guid GenerationLineId,
    Guid SqlAttentionHitAttentionId,
    Guid PhaseAtAttentionId
);

public enum AttentionLifecycleOperation
{
    CreateDraft,
    AdoptDraft,
    Reject
}

public record AttentionLifecycleCommand(
    Guid SourceAttentionId,
    AttentionLifecycleOperation Operation,
    string ActorOrSource,
    string CommandId
);

public record AttentionLifecycleSource(
    Guid AttentionId,
    Guid CurrentId,
    string SourceSetId,
    Guid GenerationLineId,
    string EvidenceKind,
    string PhaseVectorJson,
    IReadOnlyList<Guid> SourceTopologyManifestIds,
    IReadOnlyList<Guid> HitHubRelationIds,
    IReadOnlyList<Guid> ExpandedHubRelationIds,
    IReadOnlyList<Guid> ExpandedTopologyManifestIds,
    IReadOnlyList<Guid> ExpandedHubIds
);

public record AttentionLifecycleAppendRequest(
    AttentionLifecycleSource Source,
    string EvidenceKind,
    string PhaseStatus,
    string PromotionStatus,
    string ActorOrSource,
    string CommandId,
    string EvidenceJson
);

public record AttentionLifecycleResult(
    bool Succeeded,
    Guid? AttentionId,
    Guid? GenerationLineId,
    string Status,
    string Detail
);

/// <summary>
/// A single hub-attractor exploration hit produced by the exploration runtime.
/// Carries all fields needed for downstream write_logs_attention boundary.
/// L2Norm = logs.current.l2_norm at scoring time.
/// VectorJson = convergent neighbor hit vector (dot product component terms, per SSOT).
/// EvidenceJson = scoring provenance (cosine_score, overlap_score, current_l2_norm, key counts).
/// PhaseVectorJson carries append-only phaseAT evidence; q is not Draft.
/// Canonical hits come from manifest-scoped hubs.hub_relations exploration. Legacy cache diagnostics remain isolated.
/// </summary>
public record HubAttractorExplorationHit(
    Guid CurrentId,
    Guid HubCurrentId,
    string SourceSetId,
    Guid? HubId,
    string AttractorKey,
    Guid? HubRelationId,
    Guid? RelationRegistryId,
    double NeighborScore,
    int HitRank,
    string ScoreBand,
    string PermutationKey,
    double L2Norm,
    string VectorJson,
    string PhaseVectorJson,
    string EvidenceJson,
    Guid GenerationLineId = default,
    Guid? TopologyManifestId = null,
    Guid? RelatedHubId = null,
    IReadOnlyList<Guid>? SourceTopologyManifestIds = null,
    IReadOnlyList<Guid>? ExpandedHubRelationIds = null,
    IReadOnlyList<Guid>? ExpandedTopologyManifestIds = null,
    IReadOnlyList<Guid>? ExpandedHubIds = null
);

/// <summary>
/// Full exploration result for one (sourceSetId, basisWindow) run.
/// Passed to write_logs_attention boundary — not persisted here.
/// </summary>
public record HubAttractorExplorationResult(
    string SourceSetId,
    string BasisWindow,
    IReadOnlyList<HubAttractorExplorationHit> Hits
);

/// <summary>
/// Execution status of one hub-attractor exploration run.
/// NoChange: no change candidates detected — exploration skipped.
/// MissingPolicy: no active function_parameters row found — fail-close.
/// MalformedPolicy: policy JSON is invalid or required keys are missing/non-positive — fail-close.
/// NoRelatedTopologyManifest: explicit physical-table binding resolver returned no manifest — fail-close.
/// NoHubRelations: resolved manifests have no active scored hub relations — fail-close.
/// CanonicalRelationExplorationPending: retained compatibility status for pre-Step-4 callers.
/// Ok: canonical relation exploration or explicitly requested diagnostics completed.
/// </summary>
public enum HubAttractorExplorationStatus
{
    Ok,
    NoChange,
    MissingPolicy,
    MalformedPolicy,
    NoRelatedTopologyManifest,
    NoHubRelations,
    CanonicalRelationExplorationPending
}

/// <summary>
/// Result of one hub-attractor exploration run.
/// Result is null when Status is not Ok.
/// </summary>
public record HubAttractorExplorationRunResult(
    HubAttractorExplorationStatus Status,
    string Detail,
    HubAttractorExplorationResult? Result,
    DateTimeOffset ExecutedAt
);

/// <summary>
/// Input to the write_logs_attention boundary.
/// Carries all evidence fields for one logs.attention INSERT.
/// current_id and hub_current_id are required — absence is a hard error.
///
/// Evidence layer separation (SSOT):
///   statistics layer  : statistics_json, ema_score
///   attention layer   : l2_norm, vector_json, neighbor_score
///   phase-attention   : phase_vector_json (append-only phaseAT evidence; q is not Draft)
/// </summary>
public record LogsAttentionWriteRequest(
    Guid CurrentId,
    Guid HubCurrentId,
    string SourceSetId,
    Guid? HubId,
    string AttractorKey,
    Guid? HubRelationId,
    Guid? RelationRegistryId,
    double NeighborScore,
    int HitRank,
    string ScoreBand,
    string PermutationKey,
    double L2Norm,
    string VectorJson,
    string PhaseVectorJson,
    string StatisticsJson,
    double? EmaScore,
    string EvidenceJson,
    string ArchivePolicy
);


// =============================================================================
// SQL Attention Topology Projection contracts (SQLA-6)
// Child projection boundary: read-only evidence consumption → recommendation candidates.
// Evidence layer separation per SSOT: statistics / attention / phase_attention.
// Must never back-propagate into the parent evidence layer.
// =============================================================================

/// <summary>
/// A logs.attention evidence row loaded for child projection (read-only).
/// Evidence layers are preserved separately per SSOT:
///   StatisticsJson   = convergence confidence / stability / continuity
///   VectorJson       = current excitation / neighbor hit (attention layer)
///   PhaseVectorJson  = exploratory variance / shifted candidate direction
///   EvidenceJson     = scoring provenance
/// </summary>
public record AttentionEvidenceRecord(
    Guid AttentionId,
    Guid CurrentId,
    string SourceSetId,
    Guid? HubId,
    string AttractorKey,
    Guid? HubRelationId,
    Guid? RelationRegistryId,
    double NeighborScore,
    int HitRank,
    string ScoreBand,
    string PermutationKey,
    double L2Norm,
    string VectorJson,
    string PhaseVectorJson,
    string StatisticsJson,
    double? EmaScore,
    string EvidenceJson,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Status of a topology projection run.
/// NoEvidence: no recent evidence rows found — expected for cold start; not an error.
/// MissingPolicy: policy not found in function_parameters — explicit failure.
/// MalformedPolicy: policy JSON invalid or required keys missing — explicit failure.
/// DbUnavailable: DB query failed — explicit failure.
/// Ok: projection candidates are available.
/// </summary>
public enum TopologyProjectionStatus
{
    Ok,
    NoEvidence,
    MissingPolicy,
    MalformedPolicy,
    DbUnavailable
}

/// <summary>
/// A single child projection candidate derived from SQL Attention evidence.
/// Evidence layer separation is preserved per SSOT:
///   statistics layer  : StatisticsJson (convergence confidence / stability / continuity)
///   attention layer   : VectorJson (current excitation / neighbor hit)
///   phase_attention   : PhaseVectorJson (exploratory variance / shifted candidate direction)
/// AttentionScore is a display/ranking score for this candidate and must not
/// back-propagate into the parent evidence layer.
/// This is a read-only recommendation candidate; it does not mutate registry / topology / fixed route.
/// </summary>
public record TopologyProjectionCandidate(
    Guid? HubId,
    string AttractorKey,
    Guid? RelationRegistryId,
    double AttentionScore,
    string ScoreBand,
    string StatisticsJson,
    string VectorJson,
    string PhaseVectorJson,
    string EvidenceJson,
    int HitRank,
    string Lane = RecommendationPressureLanes.SqlAttentionProjection
);

/// <summary>
/// Result of SqlAttentionTopologyProjectionRuntime.ProjectAsync.
/// Status is always explicit — no silent fallback.
/// Candidates is empty when Status is not Ok.
/// </summary>
public record SqlAttentionTopologyProjectionResult(
    TopologyProjectionStatus Status,
    string? StatusDetail,
    IReadOnlyList<TopologyProjectionCandidate> Candidates,
    DateTimeOffset EvaluatedAt,
    string Lane = RecommendationPressureLanes.SqlAttentionProjection
);

/// <summary>
/// Policy for SQL Attention topology projection (child projection consumer).
/// Loaded from function_parameters:
///   function_name = 'sql_attention_topology_projection'
///   parameter_key = 'default_policy'
/// All values originate from the topology data store — no production defaults in runtime code.
/// Policy-missing → MissingPolicy explicit failure.
/// Policy malformed → MalformedPolicy explicit failure.
/// </summary>
public record SqlAttentionTopologyProjectionPolicy(
    int TopK,
    double MinNeighborScore,
    int RecentWindowDays
);

/// <summary>
/// Input to logs.diff append boundary for SQL Attention physical mutation pressure.
/// This boundary is append-only and must not mix topology_edit_log or UI operation events.
/// </summary>
public record LogsDiffAppendRequest(
    string SourceSetId,
    string BasisWindow,
    string PhysicalTableId,
    string PhysicalTableName,
    string RecordId,
    string OperationKind,
    string BeforeStateOrDiffJson,
    string AfterStateOrDiffJson,
    DateTimeOffset ObservedAt,
    string? ActorOrSource,
    string ArchivePolicy
);