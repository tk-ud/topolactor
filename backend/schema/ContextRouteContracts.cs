namespace Topolactor.Schema;

/// <summary>
/// A single context token loaded from context_token_registry.
/// value is the sparse vector component used in cosine similarity.
/// group is UI grouping only — not used in computation.
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
/// Sparse event vector with precomputed l2 norm.
/// SparseVector maps token_id → value. Missing tokens are 0.
/// </summary>
public record ContextEventVector(
    Guid EventId,
    IReadOnlyDictionary<Guid, float> SparseVector,
    float L2Norm
);

/// <summary>
/// Cached prefix vector for (session_id, prefix_index).
/// SparseVector = SUM(event_vectors[0..prefix_index]).
/// </summary>
public record ContextPrefixVectorRecord(
    Guid SessionId,
    int PrefixIndex,
    Guid LastEventId,
    IReadOnlyDictionary<Guid, float> SparseVector,
    float L2Norm,
    DateTimeOffset UpdatedAt
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
/// A ranked recommendation candidate (operation or token).
/// Evidence carries human-readable signals for explainability.
/// </summary>
public record RecommendationCandidate(
    string Value,
    float Score,
    float? Probability,
    IReadOnlyList<string> Evidence
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
    IReadOnlyList<Guid> NearestPrefixSessionIds,
    IReadOnlyList<string> ContributingTokens,
    RecommendationStatus Status,
    string? StatusDetail
);
