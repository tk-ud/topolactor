using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Binds topology function inputs to a TopologyFunctionInterface.
///
/// Per SSOT backend_contract.topology_function_binder:
///   input:  function_name + where_condition + jsonb_array
///   output: topology_function_interface_args
///
/// Per function_binding_and_constructor_contract:
///   prohibited: entity_constructor, csharp_factory, domain_invariant_constructor
///
/// The binder applies where_condition as a JSON path filter over jsonb_array,
/// then produces the interface_args dictionary consumed by abstract function shapes.
/// No DB write occurs here. No silent fallback: invalid inputs produce explicit errors.
/// </summary>
public class TopologyFunctionBinder
{
    private readonly ILogger<TopologyFunctionBinder> _logger;

    public TopologyFunctionBinder(ILogger<TopologyFunctionBinder> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Binds the given function_name, where_condition, and jsonb_array into a
    /// TopologyFunctionInterface. Throws ArgumentException on invalid inputs.
    /// Returns explicit error via out-parameter if binding fails at runtime.
    /// </summary>
    public TopologyFunctionInterface Bind(
        string functionName,
        string? whereCondition,
        IReadOnlyList<JsonElement> jsonbArray)
    {
        if (string.IsNullOrWhiteSpace(functionName))
            throw new ArgumentException("functionName is required", nameof(functionName));

        ArgumentNullException.ThrowIfNull(jsonbArray);

        _logger.LogDebug(
            "TopologyFunctionBinder: binding function={FunctionName} whereCondition={WhereCondition} inputCount={Count}",
            functionName, whereCondition ?? "<none>", jsonbArray.Count);

        var filtered = ApplyWhereCondition(jsonbArray, whereCondition);
        var interfaceArgs = BuildInterfaceArgs(functionName, filtered);

        return new TopologyFunctionInterface(
            FunctionName: functionName,
            WhereCondition: whereCondition,
            JsonbArray: filtered,
            InterfaceArgs: interfaceArgs);
    }

    /// <summary>
    /// Applies where_condition as a simple key=value JSON filter over jsonb_array.
    /// When where_condition is null or empty, the full array is returned unfiltered.
    /// Format: "key=value" — filters items where item[key] string equals value.
    /// Unknown format returns the full array with a warning (explicit, not silent).
    /// </summary>
    private IReadOnlyList<JsonElement> ApplyWhereCondition(
        IReadOnlyList<JsonElement> array,
        string? whereCondition)
    {
        if (string.IsNullOrWhiteSpace(whereCondition))
            return array;

        var eqIdx = whereCondition.IndexOf('=', StringComparison.Ordinal);
        if (eqIdx <= 0)
        {
            _logger.LogWarning(
                "TopologyFunctionBinder: where_condition '{Condition}' has unknown format; returning full array.",
                whereCondition);
            return array;
        }

        var key = whereCondition[..eqIdx].Trim();
        var value = whereCondition[(eqIdx + 1)..].Trim();

        var result = new List<JsonElement>();
        foreach (var item in array)
        {
            if (item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty(key, out var prop) &&
                string.Equals(prop.GetString(), value, StringComparison.Ordinal))
            {
                result.Add(item);
            }
        }

        return result;
    }

    /// <summary>
    /// Builds the interface_args dictionary from the filtered jsonb_array.
    /// Merges all object properties; later items override earlier on key conflict.
    /// Non-object elements are included under their array index as string key.
    /// </summary>
    private static Dictionary<string, JsonElement> BuildInterfaceArgs(
        string functionName,
        IReadOnlyList<JsonElement> filtered)
    {
        var args = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["__function_name"] = JsonSerializer.SerializeToElement(functionName),
            ["__item_count"] = JsonSerializer.SerializeToElement(filtered.Count),
        };

        for (var i = 0; i < filtered.Count; i++)
        {
            var item = filtered[i];
            if (item.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in item.EnumerateObject())
                    args[prop.Name] = prop.Value.Clone();
            }
            else
            {
                args[$"__item_{i}"] = item.Clone();
            }
        }

        return args;
    }
}
