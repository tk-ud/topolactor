namespace Topolactor.Schema;

/// <summary>
/// A single context token loaded from context_token_registry.
/// Value is a human-assigned ordering reference; it is NOT used in recommendation computation.
/// Recommendation uses multi-hot token ID presence (1.0 per token) as the topology observation.
/// Group is UI grouping only — not used in computation.
/// </summary>
public record ContextTokenRecord(
    Guid TokenId,
    string Label,
    string? Group,
    float Value,
    string Status
);

/// <summary>
/// A context event to be appended to context_event (append-only).
/// Records the operation and token snapshot at the moment of a user action.
/// </summary>
public record ContextEventRecord(
    Guid EventId,
    Guid SessionId,
    string? UserId,
    string? Role,
    string? TableName,
    string? RecordId,
    string Operation,
    IReadOnlyList<Guid> TokenIds,
    DateTimeOffset CreatedAt,
    string? NextOperationHint,
    IReadOnlyList<Guid>? NextTokenIdsHint
);

/// <summary>
/// Multi-hot event vector with precomputed l2 norm. Rebuildable projection cache — not SoT.
/// SparseVector maps token_id → 1.0 (multi-hot presence). Missing tokens are 0.
/// </summary>
public record ContextEventVector(
    Guid EventId,
    IReadOnlyDictionary<Guid, float> SparseVector,
    float L2Norm
);

/// <summary>
/// Cached prefix vector for (session_id, prefix_index). Rebuildable projection cache — not SoT.
/// SparseVector = SUM(multi-hot event_vectors[0..prefix_index]); each token contributes 1.0 per event.
/// NextOperation and NextTokenIdsHint carry the event that follows this prefix
/// in the original session, populated for nearest-prefix search without a second round-trip.
/// </summary>
public record ContextPrefixVectorRecord(
    Guid SessionId,
    int PrefixIndex,
    Guid LastEventId,
    IReadOnlyDictionary<Guid, float> SparseVector,
    float L2Norm,
    DateTimeOffset UpdatedAt,
    string? NextOperation = null,
    IReadOnlyList<Guid>? NextTokenIdsHint = null
);

/// <summary>
/// A neighbor prefix found by cosine similarity search.
/// NextOperation and NextTokenIdsHint are populated from the event that
/// follows this prefix in the original session.
/// </summary>
public record ContextNeighborResult(
    Guid SessionId,
    int PrefixIndex,
    float Similarity,
    string? NextOperation,
    IReadOnlyList<Guid>? NextTokenIdsHint
);

/// <summary>
/// A single transition probability row from context_transition_stats.
/// </summary>
public record ContextTransitionStat(
    string PrevOperation,
    string NextOperation,
    int CountEvents,
    float CountHits,
    float Prob01
);

/// <summary>
/// Enum item transition row from context_enum_transition_stats (state_pressure lane).
/// </summary>
public record ContextEnumTransitionStat(
    Guid EnumGroupId,
    int PrevEnumIndex,
    int NextEnumIndex,
    int CountEvents,
    float CountHits,
    float Prob01
);

/// <summary>
/// Append-only enum transition observation (logical enum_transition_logs surface).
/// </summary>
public record ContextEnumTransitionEventRecord(
    Guid EventId,
    Guid EnumGroupId,
    int PrevEnumIndex,
    int NextEnumIndex,
    string? Role,
    string? UserId,
    Guid? SessionId,
    DateTimeOffset CreatedAt
);

/// <summary>
/// A ranked recommendation candidate (operation or token).
/// Evidence carries human-readable signals for explainability.
/// </summary>
public record RecommendationCandidate(
    string Value,
    float Score,
    float? Probability,
    IReadOnlyList<string> Evidence,
    string Lane
);

/// <summary>
/// Explicit status of a context route recommendation result.
/// Ok: candidates available.
/// InsufficientHistory: not enough context data to recommend — not an error.
/// ExplicitError: something in the resolver pipeline failed explicitly.
/// There is no silent fallback status.
/// </summary>
public enum RecommendationStatus
{
    Ok,
    InsufficientHistory,
    ExplicitError
}

/// <summary>
/// Full recommendation result included in the Emission.
/// next_operations: ranked next operation candidates derived from neighbor voting + transition stats.
/// next_tokens: ranked token candidates derived from neighbor next_token_ids_hint.
/// status: always explicit — InsufficientHistory if not enough history, never silent null.
/// </summary>
public record ContextRouteRecommendationResult(
    IReadOnlyList<RecommendationCandidate> NextOperations,
    IReadOnlyList<RecommendationCandidate> NextTokens,
    IReadOnlyList<RecommendationCandidate> NextEnumItems,
    IReadOnlyList<Guid> NearestPrefixSessionIds,
    IReadOnlyList<string> ContributingTokens,
    RecommendationStatus Status,
    string? StatusDetail
);


/// <summary>
/// Runtime dispatch spec for a recommendation candidate. When null, the frontend must
/// render the candidate as non-executable and must not infer target/layer/action.
/// Mirrors the backend-resolved wiring pattern used by the runtime dispatch lane.
/// </summary>
public record RecommendRuntimeDispatchSpec(
    string OperationType,
    string Target,
    string Layer,
    string Action,
    string? WiringKey = null,
    string? WiringId = null,
    string? TargetRef = null
);

/// <summary>
/// Candidate in a renderable recommend projection section. Lane is backend-resolved;
/// frontend may display it but must not reclassify or mix candidates across lanes.
/// </summary>
public record RecommendProjectionCandidate(
    string Value,
    float Score,
    float? Probability,
    IReadOnlyList<string> Evidence,
    string Lane,
    RecommendRuntimeDispatchSpec? RuntimeDispatchSpec = null
);

/// <summary>
/// Explicit status for a renderable recommendation projection section.
/// ExplicitUnavailable is used for unresolved context/source surfaces; no silent fallback.
/// </summary>
public enum RecommendProjectionStatus
{
    Ok,
    InsufficientHistory,
    ExplicitUnavailable,
    ExplicitError
}

/// <summary>
/// Backend-resolved, render-only lane section for the recommend child island.
/// CandidateKind is the backend-authored display semantic (next_operation,
/// next_component, next_route_action, next_context_token, next_enum_item,
/// likely_status, state_shift_candidate, next_hub_projection_candidate).
/// </summary>
public record RecommendProjectionSection(
    string Lane,
    string CandidateKind,
    string Title,
    RecommendProjectionStatus Status,
    string? StatusDetail,
    IReadOnlyList<RecommendProjectionCandidate> Candidates
);

/// <summary>
/// SQL Attention child projection lane descriptor. When SourceSetId is absent the
/// frontend renders ExplicitUnavailable and must not fetch with a hard-coded fallback.
/// </summary>
public record SqlAttentionProjectionChildSpec(
    string Lane,
    string CandidateKind,
    RecommendProjectionStatus Status,
    string? StatusDetail,
    string? SourceSetId
);

/// <summary>
/// Renderable recommendation navigation child island spec carried by Emission.
/// The main projection island remains the primary DOM projection; this spec declares
/// the recommend navigation block as its child island and keeps hub-local and
/// SQL Attention projection lanes separated.
/// </summary>
public record RecommendNavigationProjectionSpec(
    string MainProjectionIslandId,
    string ChildIslandId,
    IReadOnlyList<RecommendProjectionSection> HubLocalSections,
    SqlAttentionProjectionChildSpec SqlAttentionProjection
)
{
    public static RecommendNavigationProjectionSpec FromRecommendation(
        ContextRouteRecommendationResult? recommendation,
        string? sqlAttentionSourceSetId)
    {
        var status = ToProjectionStatus(recommendation?.Status);
        var detail = recommendation?.StatusDetail;

        var operationCandidates = recommendation?.NextOperations.Select(ToProjectionCandidate).ToArray()
            ?? [];
        var contextTokenCandidates = recommendation?.NextTokens.Select(ToProjectionCandidate).ToArray()
            ?? [];
        var stateCandidates = recommendation?.NextEnumItems.Select(ToProjectionCandidate).ToArray()
            ?? [];

        var sqlStatus = string.IsNullOrWhiteSpace(sqlAttentionSourceSetId)
            ? RecommendProjectionStatus.ExplicitUnavailable
            : RecommendProjectionStatus.Ok;
        var sqlDetail = string.IsNullOrWhiteSpace(sqlAttentionSourceSetId)
            ? "sql_attention_source_set_id is not resolved for this emission."
            : "sourceSetId resolved by backend emission context; frontend may fetch the dedicated SQL Attention projection API.";

        return new RecommendNavigationProjectionSpec(
            MainProjectionIslandId: "main_projection_island",
            ChildIslandId: "recommend_navigation_child_island",
            HubLocalSections:
            [
                new RecommendProjectionSection(
                    Lane: RecommendationPressureLanes.UiPressure,
                    CandidateKind: "next_operation",
                    Title: "UI / operation pressure",
                    Status: status,
                    StatusDetail: detail,
                    Candidates: operationCandidates.Where(c => c.Lane == RecommendationPressureLanes.UiPressure).ToArray()),
                new RecommendProjectionSection(
                    Lane: RecommendationPressureLanes.UiPressure,
                    CandidateKind: "next_context_token",
                    Title: "Context token pressure",
                    Status: status,
                    StatusDetail: detail,
                    Candidates: contextTokenCandidates.Where(c => c.Lane == RecommendationPressureLanes.UiPressure).ToArray()),
                new RecommendProjectionSection(
                    Lane: RecommendationPressureLanes.StatePressure,
                    CandidateKind: "next_enum_item",
                    Title: "State / enum pressure",
                    Status: status,
                    StatusDetail: detail,
                    Candidates: stateCandidates.Where(c => c.Lane == RecommendationPressureLanes.StatePressure).ToArray())
            ],
            SqlAttentionProjection: new SqlAttentionProjectionChildSpec(
                Lane: RecommendationPressureLanes.SqlAttentionProjection,
                CandidateKind: "next_hub_projection_candidate",
                Status: sqlStatus,
                StatusDetail: sqlDetail,
                SourceSetId: string.IsNullOrWhiteSpace(sqlAttentionSourceSetId) ? null : sqlAttentionSourceSetId.Trim())
        );
    }

    private static RecommendProjectionCandidate ToProjectionCandidate(RecommendationCandidate candidate) =>
        new(candidate.Value, candidate.Score, candidate.Probability, candidate.Evidence, candidate.Lane);

    private static RecommendProjectionStatus ToProjectionStatus(RecommendationStatus? status) => status switch
    {
        RecommendationStatus.Ok => RecommendProjectionStatus.Ok,
        RecommendationStatus.InsufficientHistory => RecommendProjectionStatus.InsufficientHistory,
        RecommendationStatus.ExplicitError => RecommendProjectionStatus.ExplicitError,
        null => RecommendProjectionStatus.ExplicitUnavailable,
        _ => RecommendProjectionStatus.ExplicitError
    };
}

// =============================================================================
// Topology Vector Runtime contracts
// =============================================================================

/// <summary>
/// Validation class returned by registry vector validation.
/// duplicate_vector and near_duplicate_vector are separate from the existing
/// duplicate_key check (which compares string labels, not cosine similarity).
/// </summary>
public enum RegistryVectorValidationClass
{
    Pass,
    RelatedExistingRegistry,
    NearDuplicateVector,
    DuplicateVector,
    ZeroVector,
    /// <summary>DB unavailable or other explicit infrastructure error. Always blocking (fail-closed).</summary>
    ExplicitError
}

/// <summary>
/// A single neighbor found during registry vector cosine search.
/// Used in RegistryVectorValidationResult to explain which existing registries
/// are semantically close to the candidate being validated.
/// </summary>
public record RegistryVectorNeighbor(
    Guid RegistryId,
    string Name,
    float CosineScore,
    IReadOnlyList<Guid> MatchedIds,
    string Reason
);

/// <summary>
/// Structured result of registry vector validation.
/// Returned by ValidateRegistryVectorAsync.
///
/// IsBlocking: true for DuplicateVector and NearDuplicateVector.
/// Neighbors: existing registries with high cosine similarity to the candidate.
/// StatusDetail: error code when ValidationClass is not computable (DB unavailable, etc.).
///
/// No silent fallback — DB unavailability returns ExplicitError status in StatusDetail.
/// </summary>
public record RegistryVectorValidationResult(
    RegistryVectorValidationClass ValidationClass,
    bool IsBlocking,
    IReadOnlyList<RegistryVectorNeighbor> Neighbors,
    string? StatusDetail = null
);

/// <summary>
/// A row from context_hub_recommendation_current.
/// attention_score = static_relation_weight + cosine_similarity + statistical_weight
///                 + mlp_feature_score + feedback_adjustment.
/// evidence_json: transition key evidence (relation/cosine/stat/EMA/cross).
/// mlp_feature_json: feature crossing contribution breakdown.
/// </summary>
public record HubAttentionCurrentRecord(
    Guid HubId,
    string TargetTable,
    string CandidateKind,
    Guid CandidateId,
    int ScopeLimit,
    float? BaseProbability,
    float? CosineSimilarity,
    float? StaticRelationWeight,
    float? StatisticalWeight,
    float? MlpFeatureScore,
    float FeedbackAdjustment,
    float? EmaFast,
    float? EmaSlow,
    float? Trend,
    string? CrossState,
    float? AttentionScore,
    int? Rank,
    string EvidenceJson,
    string MlpFeatureJson,
    DateTimeOffset UpdatedAt
);

/// <summary>
/// A single transition key evidence entry explaining why a candidate is relevant.
/// Stored as an element in evidence_json of context_hub_recommendation_current.
/// </summary>
public record TransitionKeyEvidence(
    string KeyKind,
    string KeyId,
    string KeyLabel,
    float ContributionScore,
    string Reason
);

/// <summary>
/// Feature crossing entry for Topology MLP.
/// Stored as an element in mlp_feature_json of context_hub_recommendation_current.
/// </summary>
public record TopologyMlpFeature(
    IReadOnlyList<string> FeatureKeys,
    float CrossScore,
    string Reason
);

/// <summary>
/// A feedback event to be applied and appended.
/// TargetTable must match context_hub_recommendation_current.target_table so that
/// feedback_adjustment is applied to the exact (hub_id, target_table, candidate_kind,
/// candidate_id, scope_limit) row — never across target_tables.
/// </summary>
public record HubFeedbackEvent(
    Guid HubId,
    string TargetTable,
    Guid CandidateId,
    string CandidateKind,
    int ScopeLimit,
    string FeedbackKind,
    float DeltaApplied
);

/// <summary>
/// Result of a feedback weight update operation.
/// Status is always explicit — no silent fallback.
/// </summary>
public record FeedbackWeightUpdateResult(
    RecommendationStatus Status,
    int EventsApplied,
    string? StatusDetail = null
);


/// <summary>
/// Canonical append-only component operation event log record for frontend_component_event_log_lane.
/// Preserves required_identity end-to-end at repository boundary.
/// </summary>
public record ComponentOperationEventLogRecord(
    string ComponentId,
    string? PackageId,
    string? LayoutId,
    string? WiringId,
    string EventType,
    string PayloadJson,
    string ActorOrSource,
    DateTimeOffset OccurredAt,
    string IdempotencyKey
);
