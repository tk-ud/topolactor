namespace Topolactor.Runtime;

/// <summary>
/// Builds multi-hot event vectors and prefix vectors from token ID presence (1.0 per token).
/// Token IDs are treated as topology vocabulary axes — presence = 1.0, absence = 0.0.
/// Cosine computed on these vectors is intersection/sqrt(|a|*|b|), used as neighborhood filter.
/// token.value from context_token_registry is NOT used here; that field is for reference only.
/// Uses SUM aggregation for prefix vectors (low compute, statistical stability).
/// </summary>
public class ContextVectorBuilder
{
    /// <summary>
    /// Builds a multi-hot event vector: each token ID present maps to 1.0.
    /// Token IDs not in tokenIds are absent (0 — not added to the sparse representation).
    /// No value weighting; token presence is the topology observation signal.
    /// </summary>
    public IReadOnlyDictionary<Guid, float> BuildMultiHotVector(IEnumerable<Guid> tokenIds)
    {
        ArgumentNullException.ThrowIfNull(tokenIds);

        var vector = new Dictionary<Guid, float>();
        foreach (var id in tokenIds)
            vector[id] = 1.0f;

        return vector;
    }

    /// <summary>
    /// Builds a prefix vector by summing event vectors in session order.
    /// v_prefix = SUM(v_event[0..n]).
    /// </summary>
    public IReadOnlyDictionary<Guid, float> BuildPrefixVector(
        IEnumerable<IReadOnlyDictionary<Guid, float>> eventVectors)
    {
        ArgumentNullException.ThrowIfNull(eventVectors);

        var result = new Dictionary<Guid, float>();
        foreach (var ev in eventVectors)
            foreach (var (k, v) in ev)
                result[k] = result.GetValueOrDefault(k) + v;

        return result;
    }

    /// <summary>
    /// Computes the L2 norm of a sparse vector.
    /// For a multi-hot vector of N tokens, this equals sqrt(N).
    /// Returns 0 for an empty vector.
    /// </summary>
    public float ComputeL2Norm(IReadOnlyDictionary<Guid, float> vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        float sumSq = 0f;
        foreach (var v in vector.Values)
            sumSq += v * v;

        return (float)Math.Sqrt(sumSq);
    }
}
