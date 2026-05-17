using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Resolves a full structure map from the repository using the attractor result.
/// Returns a RuntimeWorkingShape populated with structure map fields.
/// </summary>
public class StructureMapResolver
{
    private readonly TopologyRepository _topologyRepository;

    public StructureMapResolver(TopologyRepository topologyRepository)
    {
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
    }

    /// <summary>
    /// Loads the full structure map and constructs the initial RuntimeWorkingShape.
    /// Throws if the structure map record cannot be loaded by its ID.
    /// </summary>
    public async Task<RuntimeWorkingShape> Resolve(AttractorResult attractor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(attractor);

        var record = await _topologyRepository.LoadStructureMapAsync(attractor.StructureMapId, ct);

        if (record is null)
            throw new InvalidOperationException(
                $"Structure map '{attractor.StructureMapId}' not found. Broken reference — no fallback.");

        // Build the initial working shape from the structure map record.
        // Vector is set later by RuntimeExecutor after this step returns.
        // StatePolicyJson is forwarded to allow ContextRouteRecommendationResolver
        // to resolve a scoped policy_ref from structure_maps.state_policy.
        return new RuntimeWorkingShape(
            Vector: null,
            StructureMapId: record.StructureMapId,
            PackageId: record.PackageId,
            SchemaId: record.SchemaId,
            ComponentIds: record.ComponentIds,
            PackageDef: null,
            SchemaDef: null,
            ResolvedData: null,
            Errors: null,
            StructureMapStatePolicyJson: record.StatePolicyJson
        );
    }
}
