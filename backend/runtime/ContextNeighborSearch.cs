using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Computes cosine similarity between sparse vectors and finds the nearest
/// prefix vectors from historical context sessions.
///
/// Cosine similarity: cos(a, b) = dot(a, b) / (||a|| * ||b||)
/// Zero-norm policy: if either norm is 0, similarity is 0.
/// Sparse dot product only — no dense embeddings.
/// </summary>
public class ContextNeighborSearch
{
    /// <summary>
    /// Computes cosine similarity between two sparse vectors.
    /// Returns 0 if either l2 norm is 0 (zero-norm policy).
    /// Uses the smaller vector for dot product iteration (efficiency).
    /// </summary>
    public float ComputeCosineSimilarity(
        IReadOnlyDictionary<Guid, float> a,
        float normA,
        IReadOnlyDictionary<Guid, float> b,
        float normB)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (normA == 0f || normB == 0f)
            return 0f;

        float dot = 0f;
        var (smaller, larger) = a.Count <= b.Count ? (a, b) : (b, a);
        foreach (var (k, v) in smaller)
            if (larger.TryGetValue(k, out var bv))
                dot += v * bv;

        return dot / (normA * normB);
    }

    /// <summary>
    /// Finds the nearest prefix vectors by cosine similarity.
    /// Returns up to topK results with similarity >= minSimilarity.
    /// Tie-breaking: recency (updatedAt descending).
    /// Returns empty list if currentNorm is 0 or no candidates exist.
    /// </summary>
    public IReadOnlyList<ContextNeighborResult> FindNearestPrefixes(
        IReadOnlyDictionary<Guid, float> currentVector,
        float currentNorm,
        IReadOnlyList<ContextPrefixVectorRecord> candidates,
        float minSimilarity,
        int topK)
    {
        ArgumentNullException.ThrowIfNull(currentVector);
        ArgumentNullException.ThrowIfNull(candidates);

        if (currentNorm == 0f || candidates.Count == 0)
            return [];

        var scored = new List<(ContextNeighborResult Result, float Sim, DateTimeOffset UpdatedAt)>(candidates.Count);

        foreach (var c in candidates)
        {
            var sim = ComputeCosineSimilarity(currentVector, currentNorm, c.SparseVector, c.L2Norm);
            if (sim >= minSimilarity)
                scored.Add((
                    new ContextNeighborResult(c.SessionId, c.PrefixIndex, sim, null, null),
                    sim,
                    c.UpdatedAt
                ));
        }

        return scored
            .OrderByDescending(x => x.Sim)
            .ThenByDescending(x => x.UpdatedAt)
            .Take(topK)
            .Select(x => x.Result)
            .ToList();
    }
}
