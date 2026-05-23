namespace Topolactor.Schema;

/// <summary>
/// Watch change candidate returned by logs.refresh_logs_current_watch JOIN logs.current.
/// Only candidates with ChangeDetected=true are eligible for hub-attractor exploration.
/// L2Norm and BasisVectorJson are loaded from logs.current at watch time for scoring.
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
    string BasisVectorJson
);

/// <summary>
/// Hub current candidate loaded from logs.hub_current.
/// Used as the attractor target for hub-attractor neighbor exploration.
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
/// Exploration policy resolved from topologys.function_parameters.
/// All values must be positive integers — no magic number defaults in runtime code.
/// Policy source: function_name="sql_attention_hub_attractor_exploration" parameter_key="default_policy".
/// </summary>
public record HubAttractorExplorationPolicy(
    int TopKPerHubKind,
    int MaxHubKindsPerCurrent,
    int MaxHubTablesPerKind,
    int PhaseExpansionLimit,
    int MaxAttentionRowsSaved
);

/// <summary>
/// A single hub-attractor exploration hit produced by the exploration runtime.
/// Carries all fields needed for downstream write_logs_attention boundary.
/// L2Norm = logs.current.l2_norm at scoring time.
/// VectorJson = convergent neighbor hit vector (dot product component terms, per SSOT).
/// EvidenceJson = scoring provenance (cosine_score, overlap_score, current_l2_norm, key counts).
/// Phase vector is excluded — phase_vector generation is a separate post-main step.
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
    string EvidenceJson
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
/// Ok: exploration ran successfully (Hits may be empty if no hub current records exist).
/// </summary>
public enum HubAttractorExplorationStatus
{
    Ok,
    NoChange,
    MissingPolicy,
    MalformedPolicy
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
///   phase-attention   : phase_vector_json (stored; generation is a separate TODO)
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
