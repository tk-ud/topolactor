using Microsoft.Extensions.Logging;

namespace Topolactor.Repository;

/// <summary>
/// Repository for loading stored topology data: structure maps, packages, schemas,
/// and runtime function parameters.
///
/// In-memory test double: the default topology path (attractor key "default:entity:search")
/// returns seeded structure/package/schema records without a real DB connection.
/// Function parameters are not seeded in production repository code; missing policy
/// returns null so callers can surface explicit policy-missing errors.
/// </summary>
public class TopologyRepository
{
    // Deterministic IDs matching db/seed_empty.sql so in-memory tests
    // and the DB seed reference the same topology node IDs.
    public static readonly Guid DefaultPackageId  = new("00000000-0000-0000-0000-000000000001");
    public static readonly Guid DefaultSchemaId   = new("00000000-0000-0000-0000-000000000002");
    public const string DefaultComponentId        = "00000000-0000-0000-0000-000000000003";
    public const string DefaultStructureMapId     = "00000000-0000-0000-0000-000000000004";
    public const string DefaultAttractorKey       = "default:entity:search";

    private static readonly StructureMapRecord DefaultStructureMap = new(
        StructureMapId: DefaultStructureMapId,
        AttractorKey:   DefaultAttractorKey,
        PackageId:      DefaultPackageId,
        SchemaId:       DefaultSchemaId,
        ComponentIds:   [DefaultComponentId],
        StatePolicyJson: null
    );

    private static readonly PackageRecord DefaultPackage = new(
        PackageId:     DefaultPackageId,
        PackageName:   "default_package",
        Version:       null,
        RawDefinition: "{}"
    );

    private static readonly SchemaRecord DefaultSchema = new(
        SchemaId:      DefaultSchemaId,
        SchemaName:    "default_schema",
        Version:       null,
        RawDefinition: """{"fields":[{"key":"label","type":"text","label":"Label"}]}"""
    );

    protected readonly ILogger<TopologyRepository> _logger;
    protected readonly string _connectionString;

    public TopologyRepository(ILogger<TopologyRepository> logger, string connectionString)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Loads a structure map record by attractor key or structure map ID.
    /// Returns the default in-memory record for the "default:entity:search" path.
    /// Returns null for all other keys (broken reference — caller must error).
    /// Production: override in NpgsqlTopologyRepository.
    /// </summary>
    public virtual Task<StructureMapRecord?> LoadStructureMapAsync(string key, CancellationToken ct = default)
    {
        if (key == DefaultAttractorKey || key == DefaultStructureMapId)
        {
            _logger.LogDebug("TopologyRepository.LoadStructureMapAsync: returning default in-memory record for key='{Key}'.", key);
            return Task.FromResult<StructureMapRecord?>(DefaultStructureMap);
        }

        _logger.LogDebug("TopologyRepository.LoadStructureMapAsync: no record found for key='{Key}'.", key);
        return Task.FromResult<StructureMapRecord?>(null);
    }

    /// <summary>
    /// Loads a package record by its ID.
    /// Returns the default in-memory package for the default package ID.
    /// Returns null for all other IDs (broken reference — caller must error).
    /// Production: override in NpgsqlTopologyRepository.
    /// </summary>
    public virtual Task<PackageRecord?> LoadPackageAsync(Guid packageId, CancellationToken ct = default)
    {
        if (packageId == DefaultPackageId)
        {
            _logger.LogDebug("TopologyRepository.LoadPackageAsync: returning default in-memory record for packageId='{PackageId}'.", packageId);
            return Task.FromResult<PackageRecord?>(DefaultPackage);
        }

        _logger.LogDebug("TopologyRepository.LoadPackageAsync: no record found for packageId='{PackageId}'.", packageId);
        return Task.FromResult<PackageRecord?>(null);
    }

    /// <summary>
    /// Loads a schema record by its ID.
    /// Returns the default in-memory schema for the default schema ID.
    /// Returns null for all other IDs (broken reference — caller must error).
    /// Production: override in NpgsqlTopologyRepository.
    /// </summary>
    public virtual Task<SchemaRecord?> LoadSchemaAsync(Guid schemaId, CancellationToken ct = default)
    {
        if (schemaId == DefaultSchemaId)
        {
            _logger.LogDebug("TopologyRepository.LoadSchemaAsync: returning default in-memory record for schemaId='{SchemaId}'.", schemaId);
            return Task.FromResult<SchemaRecord?>(DefaultSchema);
        }

        _logger.LogDebug("TopologyRepository.LoadSchemaAsync: no record found for schemaId='{SchemaId}'.", schemaId);
        return Task.FromResult<SchemaRecord?>(null);
    }

    /// <summary>
    /// Loads a component record from component_registry by component_id.
    /// Returns null when not found — caller must treat as broken reference.
    /// </summary>
    public virtual Task<ComponentRecord?> LoadComponentAsync(Guid componentId, CancellationToken ct = default)
    {
        if (componentId.ToString() == DefaultComponentId)
        {
            return Task.FromResult<ComponentRecord?>(new ComponentRecord(
                componentId,
                "default_component",
                "default",
                "{}"));
        }

        _logger.LogDebug("TopologyRepository.LoadComponentAsync: no record found for componentId='{ComponentId}'.", componentId);
        return Task.FromResult<ComponentRecord?>(null);
    }

    /// <summary>
    /// Loads a function_parameters row by (function_name, parameter_key) and returns
    /// the parameter_value as a raw JSON string, or null if no active row is found.
    ///
    /// In-memory test double: returns null for all function parameters. Production
    /// policy values must come from stored topology data, not repository constants.
    ///
    /// Real DB implementation: SELECT parameter_value FROM function_parameters
    ///   WHERE function_name = @fn AND parameter_key = @key AND active = true
    ///   LIMIT 1
    /// Returns null when no active row exists — caller must treat null as policy-missing.
    /// </summary>
    public virtual Task<string?> LoadFunctionParameterAsync(
        string functionName,
        string parameterKey,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "TopologyRepository.LoadFunctionParameterAsync: no parameter found for '{FunctionName}/{ParameterKey}'.",
            functionName, parameterKey);
        return Task.FromResult<string?>(null);
    }

    /// <summary>
    /// Batch-loads component_kind from topology.ui_component_registry for the given component IDs.
    /// Returns a dictionary mapping componentId (string) → componentKind (string).
    /// Returns empty dict (base/test double). Missing IDs are simply absent from the result.
    /// Production: override in NpgsqlTopologyRepository.
    /// </summary>
    public virtual Task<IReadOnlyDictionary<string, string>> LoadComponentKindsByIdsAsync(
        IReadOnlyList<string> componentIds, CancellationToken ct = default)
    {
        _logger.LogDebug(
            "TopologyRepository.LoadComponentKindsByIdsAsync: returning empty dict (base/test double).");
        return Task.FromResult<IReadOnlyDictionary<string, string>>(
            new Dictionary<string, string>(StringComparer.Ordinal));
    }

    /// <summary>
    /// Batch-loads component_id from topology.ui_component_registry by component_key.
    /// Returns empty dict (base/test double). Missing keys are absent from the result.
    /// </summary>
    public virtual Task<IReadOnlyDictionary<string, string>> LoadComponentIdsByKeysAsync(
        IReadOnlyList<string> componentKeys, CancellationToken ct = default)
    {
        _logger.LogDebug(
            "TopologyRepository.LoadComponentIdsByKeysAsync: returning empty dict (base/test double).");
        return Task.FromResult<IReadOnlyDictionary<string, string>>(
            new Dictionary<string, string>(StringComparer.Ordinal));
    }

    /// <summary>
    /// Loads layout nodes by parsing layout_patch_json.nodes[] from topology.ui_topology_tensor
    /// for the given layout_id. Returns empty list (base/test double).
    /// LAYOUT_NODES_NOT_FOUND is signaled by an empty list — callers must treat empty as a
    /// broken layout configuration when layout_id is set.
    /// </summary>
    public virtual Task<IReadOnlyList<LayoutNodeRecord>> LoadLayoutNodesAsync(
        Guid layoutId, CancellationToken ct = default)
    {
        _logger.LogDebug(
            "TopologyRepository.LoadLayoutNodesAsync: returning empty list for layoutId='{LayoutId}' (base/test double).",
            layoutId);
        return Task.FromResult<IReadOnlyList<LayoutNodeRecord>>(Array.Empty<LayoutNodeRecord>());
    }

    /// <summary>
    /// Loads the raw calculationBindings JSON array from layout_patch_json root for the given layout_id.
    /// Returns null when absent or empty. Base/test double returns null.
    /// </summary>
    public virtual Task<string?> LoadLayoutCalcBindingsJsonAsync(
        Guid layoutId, CancellationToken ct = default)
    {
        return Task.FromResult<string?>(null);
    }

}

/// <summary>
/// Stored structure map data loaded from topology storage.
/// Maps an attractor key to its associated package, schema, and component definitions.
/// StatePolicyJson holds the raw JSONB from structure_maps.state_policy, used by
/// ContextRouteRecommendationResolver to resolve a scoped policy_ref.
/// LayoutId is the optional admin-authored layout reference (topology.components_layout_design).
/// </summary>
public record StructureMapRecord(
    string StructureMapId,
    string AttractorKey,
    Guid PackageId,
    Guid SchemaId,
    IReadOnlyList<string> ComponentIds,
    string? StatePolicyJson = null,
    Guid? LayoutId = null
);

/// <summary>
/// Stored package definition loaded from topology storage.
/// </summary>
public record PackageRecord(
    Guid PackageId,
    string PackageName,
    string? Version,
    string? RawDefinition
);

/// <summary>
/// Stored schema definition loaded from topology storage.
/// </summary>
public record SchemaRecord(
    Guid SchemaId,
    string SchemaName,
    string? Version,
    string? RawDefinition
);

public record ComponentRecord(
    Guid ComponentId,
    string ComponentName,
    string ComponentType,
    string? RawDefinition
);

/// <summary>
/// A single layout node parsed from layout_patch_json.nodes[].
/// Loaded from topology.ui_topology_tensor.layout_patch_json for a given layout_id.
/// ComponentId comes from nodes[].componentId — not positionally from structure_maps.component_ids.
/// WiringId/WiringKey/WiringKind/TargetSurface/TargetRef carry the full admin-configured
/// wiring spec from ui_wiring_registry for frontend dispatch spec construction.
/// </summary>
public record LayoutNodeRecord(
    string NodeId,
    string? NodeKind,
    string? HtmlTag,
    string? ComponentKey,
    string? ComponentId,
    string? ParentNodeId,
    string? SlotKey,
    int OrderIndex,
    double X,
    double Y,
    object? Width,
    object? Height,
    IReadOnlyList<string>? LayoutClassRefs,
    string? ComponentKind = null,
    string? RuntimeDispatchAction = null,
    string? WiringId = null,
    string? WiringKey = null,
    string? WiringKind = null,
    string? TargetSurface = null,
    string? TargetRef = null,
    /// <summary>Node-local props override JSON string from layout_patch_json. Null when not authored.</summary>
    string? PropsJson = null,
    /// <summary>Node-local state JSON string from layout_patch_json (e.g. open:bool for modal/drawer). Null when not authored.</summary>
    string? StateJson = null,
    /// <summary>Array prop bindings JSON string from layout_patch_json. Null when not authored.</summary>
    string? PropBindingsJson = null,
    /// <summary>Canonical runtime UI interactions JSON array from layout_patch_json.nodes[].runtimeInteractions. Null when not authored.</summary>
    string? RuntimeInteractionsJson = null,
    /// <summary>Sizing mode from layout_patch_json: auto | preset | custom.</summary>
    string? WidthMode = null,
    string? HeightMode = null,
    /// <summary>
    /// topology_ui_category/topology_ui_section/topology_ui_form/topology_ui_workflow/topology_ui_validation
    /// when NodeKind is "structural_node", or topology_ui_unresolved when NodeKind is
    /// "unresolved_gap" (both sourced from components_layout_design.layout_schema_json.records[]).
    /// Null for tensor-only "catalog_component"/"structural_html" nodes.
    /// </summary>
    string? RecordType = null,
    /// <summary>
    /// Authored display label from layout_schema_json.records[].record.label — present for every
    /// schema-composed node (structural_node, catalog_component, and unresolved_gap alike). Null
    /// for tensor-only nodes composed outside the layout-schema structural authority path.
    /// </summary>
    string? Label = null,
    /// <summary>Authored knownGapRefs for an unresolved_gap node (topology_ui_unresolved), from layout_schema_json.records[].record.knownGapRefs. Null for every other NodeKind.</summary>
    IReadOnlyList<string>? KnownGapRefs = null
);
