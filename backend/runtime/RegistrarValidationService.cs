using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Backend validation service for registry vector validation.
/// Connects the Registrar draft/validate flow to TopologyVectorRuntime.
///
/// Loads registry validation policy from function_parameters (same JSON blob as
/// context route recommendation policy — topology_vector_runtime.registry_validation).
/// Policy missing → REGISTRY_VALIDATION_POLICY_NOT_FOUND (explicit error, not silent).
/// DB unavailable → REGISTRY_VECTOR_VALIDATION_DB_UNAVAILABLE (blocking explicit error).
///
/// UI does not compute cosine similarity — backend returns structured result only.
/// </summary>
public class RegistrarValidationService
{
    private const string PolicyFunctionName = "context_route_recommendation_resolve";
    private const string PolicyParameterKey = "default_policy";

    private readonly ILogger<RegistrarValidationService> _logger;
    private readonly TopologyRepository _topologyRepository;
    private readonly TopologyVectorRuntime _topologyVectorRuntime;

    public RegistrarValidationService(
        ILogger<RegistrarValidationService> logger,
        TopologyRepository topologyRepository,
        TopologyVectorRuntime topologyVectorRuntime)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
        _topologyVectorRuntime = topologyVectorRuntime ?? throw new ArgumentNullException(nameof(topologyVectorRuntime));
    }

    /// <summary>
    /// Validates a candidate registry ID array against existing registry rows using
    /// multi-hot cosine similarity. Policy is loaded from function_parameters.
    ///
    /// Returns RegistryVectorValidationResult with explicit status on every path:
    ///   Pass                    — no neighbor above related_threshold
    ///   RelatedExistingRegistry — cosine >= related_threshold (warning)
    ///   NearDuplicateVector     — cosine >= near_duplicate_threshold (blocking)
    ///   DuplicateVector         — cosine >= duplicate_threshold (blocking)
    ///   ZeroVector              — queryIds is empty
    ///   ExplicitError           — policy missing/invalid or DB unavailable (always blocking)
    ///
    /// No silent fallback. DB unavailability → ExplicitError with IsBlocking:true.
    /// </summary>
    public async Task<RegistryVectorValidationResult> ValidateAsync(
        string registryTable,
        IReadOnlyList<Guid> queryIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(registryTable);
        ArgumentNullException.ThrowIfNull(queryIds);

        var policyJson = await _topologyRepository.LoadFunctionParameterAsync(
            PolicyFunctionName, PolicyParameterKey, ct);

        if (policyJson is null)
        {
            _logger.LogWarning(
                "RegistrarValidationService: function_parameter '{FunctionName}/{Key}' not found.",
                PolicyFunctionName, PolicyParameterKey);
            return new RegistryVectorValidationResult(
                ValidationClass: RegistryVectorValidationClass.ExplicitError,
                IsBlocking: true,
                Neighbors: [],
                StatusDetail: "REGISTRY_VALIDATION_POLICY_NOT_FOUND"
            );
        }

        RegistryValidationPolicy? registryPolicy;
        try
        {
            registryPolicy = ParseRegistryValidationPolicy(policyJson);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "RegistrarValidationService: policy JSON is invalid — REGISTRY_VALIDATION_POLICY_INVALID.");
            return new RegistryVectorValidationResult(
                ValidationClass: RegistryVectorValidationClass.ExplicitError,
                IsBlocking: true,
                Neighbors: [],
                StatusDetail: $"REGISTRY_VALIDATION_POLICY_INVALID:{ex.Message}"
            );
        }

        if (registryPolicy is null)
        {
            _logger.LogWarning(
                "RegistrarValidationService: topology_vector_runtime.registry_validation not present in policy.");
            return new RegistryVectorValidationResult(
                ValidationClass: RegistryVectorValidationClass.ExplicitError,
                IsBlocking: true,
                Neighbors: [],
                StatusDetail: "REGISTRY_VALIDATION_POLICY_NOT_FOUND"
            );
        }

        return await _topologyVectorRuntime.ValidateRegistryVectorAsync(
            registryTable, queryIds, registryPolicy, ct);
    }

    // ---------------------------------------------------------------------------
    // Policy parsing
    // ---------------------------------------------------------------------------

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private static RegistryValidationPolicy? ParseRegistryValidationPolicy(string json)
    {
        var root = JsonSerializer.Deserialize<PolicyRootDto>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Policy JSON deserialized to null.");

        var rv = root.TopologyVectorRuntime?.RegistryValidation;
        if (rv is null)
            return null;

        return new RegistryValidationPolicy(
            Enabled:                rv.Enabled,
            DuplicateThreshold:     rv.DuplicateThreshold,
            NearDuplicateThreshold: rv.NearDuplicateThreshold,
            RelatedThreshold:       rv.RelatedThreshold,
            TopK:                   rv.TopK
        );
    }

    private record PolicyRootDto(
        [property: JsonPropertyName("topology_vector_runtime")] TopologyVectorRuntimeDto? TopologyVectorRuntime = null
    );

    private record TopologyVectorRuntimeDto(
        [property: JsonPropertyName("registry_validation")] RegistryValidationDto? RegistryValidation = null
    );

    private record RegistryValidationDto(
        [property: JsonPropertyName("enabled")]                    bool  Enabled,
        [property: JsonPropertyName("duplicate_threshold")]        float DuplicateThreshold,
        [property: JsonPropertyName("near_duplicate_threshold")]   float NearDuplicateThreshold,
        [property: JsonPropertyName("related_threshold")]          float RelatedThreshold,
        [property: JsonPropertyName("top_k")]                      int   TopK
    );
}
