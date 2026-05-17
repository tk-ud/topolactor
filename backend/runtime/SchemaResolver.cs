using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Resolves the schema definition from the repository using the SchemaId on the working shape.
/// Returns an updated RuntimeWorkingShape with SchemaDef populated.
/// An error is returned (via exception) if SchemaId is missing or the schema is not found.
/// </summary>
public class SchemaResolver
{
    private readonly TopologyRepository _topologyRepository;

    public SchemaResolver(TopologyRepository topologyRepository)
    {
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
    }

    /// <summary>
    /// Loads the schema for the given shape. Throws if SchemaId is missing or not found.
    /// No silent fallback.
    /// </summary>
    public async Task<RuntimeWorkingShape> Resolve(RuntimeWorkingShape shape, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(shape);

        if (shape.SchemaId is null)
            throw new InvalidOperationException(
                "Cannot resolve schema: SchemaId is null on RuntimeWorkingShape.");

        var record = await _topologyRepository.LoadSchemaAsync(shape.SchemaId.Value, ct);

        if (record is null)
            throw new InvalidOperationException(
                $"Schema '{shape.SchemaId}' not found. Broken reference — no fallback.");

        return shape with { SchemaDef = record };
    }
}
