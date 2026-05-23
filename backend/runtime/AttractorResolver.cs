using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Resolves an attractor by loading the structure map entry keyed by the attractor key.
/// No silent fallback: if the attractor key produces no result, an exception is thrown.
/// </summary>
public class AttractorResolver
{
    private readonly TopologyRepository _topologyRepository;

    public AttractorResolver(TopologyRepository topologyRepository)
    {
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
    }

    /// <summary>
    /// Resolves the AttractorResult for the given vector.
    /// Throws InvalidOperationException if no structure map is found for the attractor key.
    /// There is no silent fallback.
    /// </summary>
    public async Task<AttractorResult> Resolve(OperationVector vector, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vector);

        if (string.IsNullOrWhiteSpace(vector.AttractorKey))
            throw new InvalidOperationException("Cannot resolve attractor: AttractorKey is null or empty.");

        var record = await _topologyRepository.LoadStructureMapAsync(vector.AttractorKey, ct);

        if (record is null)
            throw new InvalidOperationException(
                $"No structure map found for attractor key '{vector.AttractorKey}'. Broken reference — no fallback.");

        return new AttractorResult(
            AttractorKey: vector.AttractorKey,
            StructureMapId: record.StructureMapId,
            PackageId: record.PackageId,
            SchemaId: record.SchemaId
        );
    }

}
