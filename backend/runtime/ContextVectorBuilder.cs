using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Builds sparse event vectors and prefix vectors from token registry values.
/// Uses SUM aggregation for prefix vectors (low compute, statistical stability).
/// Missing tokens are treated as 0 — not added to the sparse representation.
/// Dense embeddings are not used.
/// </summary>
public class ContextVectorBuilder
{
    /// <summary>
    /// Builds a sparse event vector from a token ID list and a token-value map.
    /// Token IDs not present in tokenValueMap are omitted (treated as 0).
    /// </summary>
    public IReadOnlyDictionary<Guid, float> BuildEventVector(
        IEnumerable<Guid> tokenIds,
        IReadOnlyDictionary<Guid, float> tokenValueMap)
    {
        ArgumentNullException.ThrowIfNull(tokenIds);
        ArgumentNullException.ThrowIfNull(tokenValueMap);

        var vector = new Dictionary<Guid, float>();
        foreach (var id in tokenIds)
            if (tokenValueMap.TryGetValue(id, out var v))
                vector[id] = v;

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
    /// Returns 0 for an empty or all-zero vector.
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
