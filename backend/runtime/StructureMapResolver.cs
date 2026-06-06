using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Resolves a full structure map from the repository using the attractor result.
/// Returns a RuntimeWorkingShape populated with structure map fields.
/// When layout_id is set, loads tensor-derived layout nodes from ui_topology_tensor.
/// Missing tensor rows when layout_id is set yields LAYOUT_NODES_NOT_FOUND error —
/// no silent fallback to flat componentIds rendering.
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
    /// When layout_id is set on the record, loads tensor rows and builds LayoutNodes.
    /// Returns LAYOUT_NODES_NOT_FOUND validation error if layout_id is set but no rows found.
    /// </summary>
    public async Task<RuntimeWorkingShape> Resolve(AttractorResult attractor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(attractor);

        var record = await _topologyRepository.LoadStructureMapAsync(attractor.StructureMapId, ct);

        if (record is null)
            throw new InvalidOperationException(
                $"Structure map '{attractor.StructureMapId}' not found. Broken reference — no fallback.");

        // When layout_id is present, load tensor rows and assign component_ids positionally.
        IReadOnlyList<LayoutNode>? layoutNodes = null;
        IReadOnlyList<ValidationError>? layoutErrors = null;

        if (record.LayoutId.HasValue)
        {
            var tensorRows = await _topologyRepository.LoadLayoutNodesAsync(record.LayoutId.Value, ct);

            if (tensorRows.Count == 0)
            {
                layoutErrors =
                [
                    new ValidationError(
                        "LAYOUT_NODES_NOT_FOUND",
                        $"layout_id '{record.LayoutId.Value}' has no tensor rows in ui_topology_tensor. " +
                        "Broken layout configuration — no fallback to flat component rendering.")
                ];
            }
            else
            {
                // Positional assignment: tensor[i].slot → componentIds[i]
                var componentIds = record.ComponentIds;
                layoutNodes = tensorRows
                    .Select((row, i) => new LayoutNode(
                        SlotKey: row.SlotKey,
                        OrderIndex: row.OrderIndex,
                        ComponentId: i < componentIds.Count ? componentIds[i] : null,
                        LayoutPatchJson: row.LayoutPatchJson))
                    .ToList();
            }
        }

        return new RuntimeWorkingShape(
            Vector: null,
            StructureMapId: record.StructureMapId,
            PackageId: record.PackageId,
            SchemaId: record.SchemaId,
            ComponentIds: record.ComponentIds,
            PackageDef: null,
            SchemaDef: null,
            ResolvedData: null,
            Errors: layoutErrors,
            StructureMapStatePolicyJson: record.StatePolicyJson,
            LayoutId: record.LayoutId?.ToString(),
            LayoutNodes: layoutNodes
        );
    }
}
