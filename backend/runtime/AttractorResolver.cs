using Topolactor.Repository;
using Topolactor.Schema;
using System.Text.Json;

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

    /// <summary>
    /// Builds the phase_vector_json payload for logs.attention.
    /// Boundary:
    ///   - w = l2_norm
    ///   - x/y/z = hub-side record-count bases
    ///   - i/j/k = axis movement amounts
    /// No automatic mutation/migration/promotion is derived from this vector.
    /// </summary>
    public static string BuildPhaseVectorJson(
        double l2Norm,
        long populationCount,
        long populationRecordcount,
        long axisPopulationRecordcount,
        double axisMoveI,
        double axisMoveJ,
        double axisMoveK,
        string? phaseBasisJson = null)
    {
        var payload = new
        {
            basis_source = "logs.hub_current",
            meaning_boundary = new
            {
                w = "l2_norm",
                xyz = "hub-side record-count bases",
                ijk = "axis movement amounts",
                phase_movement_source = "not_manifest_or_policy_cap",
                no_automatic_topology_mutation = true
            },
            w = l2Norm,
            x = populationCount,
            y = populationRecordcount,
            z = axisPopulationRecordcount,
            i = axisMoveI,
            j = axisMoveJ,
            k = axisMoveK,
            phase_basis_json = string.IsNullOrWhiteSpace(phaseBasisJson) ? "{}" : phaseBasisJson
        };

        return JsonSerializer.Serialize(payload);
    }
}
