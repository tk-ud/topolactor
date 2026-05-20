using System.Text.Json;

namespace Topolactor.Schema;

/// <summary>
/// Represents the resolved argument surface for a topology function.
/// Produced by TopologyFunctionBinder from (function_name, where_condition, jsonb_array).
/// Consumed by topology_transform_runtime abstract function shapes.
///
/// Per SSOT function_binding_and_constructor_contract:
/// - role: function_parameter_surface (backend phase)
/// - prohibited: entity_constructor, csharp_factory, domain_invariant_constructor
/// </summary>
public record TopologyFunctionInterface(
    /// <summary>Name of the topology function to invoke.</summary>
    string FunctionName,
    /// <summary>WHERE condition string used to filter the jsonb_array scope.</summary>
    string? WhereCondition,
    /// <summary>JSONB array input to the function (source data).</summary>
    IReadOnlyList<JsonElement> JsonbArray,
    /// <summary>Resolved argument set produced by the binder.</summary>
    IReadOnlyDictionary<string, JsonElement> InterfaceArgs
);
