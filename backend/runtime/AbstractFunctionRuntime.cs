using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

public static class AbstractFunctionFailCloseStatus
{
    public const string MissingAuthority = "missing_authority";
    public const string InvalidAuthority = "invalid_authority";
    public const string MissingInput = "missing_input";
    public const string InvalidInputBinding = "invalid_input_binding";
    public const string InvalidProjection = "invalid_projection";
    public const string SecretProjectionDenied = "secret_projection_denied";
    public const string UnsupportedPrimitive = "unsupported_primitive";
}

public sealed class AbstractFunctionFailCloseException : InvalidOperationException
{
    public AbstractFunctionFailCloseException(string status, string message) : base(message) => Status = status;
    public string Status { get; }
}

public sealed record AbstractFunctionManifest(Guid AbstractFunctionId, string FunctionKey, string RuntimeLane, string AuthorityScope, IReadOnlyList<AbstractFunctionStep> Steps, IReadOnlyList<string> DeniedProjectionKeys, bool Active, IReadOnlyList<AbstractFunctionAuthorityBinding>? AuthorityBindings = null, IReadOnlyDictionary<string, string>? OutputShape = null);

public sealed record AbstractFunctionStep(Guid AbstractFunctionStepId, int StepOrder, string PrimitiveKey, IReadOnlyDictionary<string, string> StepConfig, IReadOnlyList<AbstractFunctionInputBinding> InputBindings, string? ResultContextKey, bool Active, bool IsCompensationStep = false);

public sealed record AbstractFunctionInputBinding(string InputKey, string BindingSource, string BindingPath, bool Required, bool Secret);

public sealed record AbstractFunctionAuthorityBinding(string AuthorityKind, string AuthorityRef, bool Active);

public sealed class AbstractFunctionExecutionContext
{
    private readonly Dictionary<string, object?> _resultContext = new(StringComparer.Ordinal);
    private readonly List<string> _executedPrimitiveKeys = new();

    public AbstractFunctionExecutionContext(string authorityScope, JsonElement? requestPayload = null, ExternalPortExecutionContext? externalPortContext = null, string? requiredRuntimeLane = null, RuntimeWorkingShape? runtimeContext = null)
    {
        AuthorityScope = authorityScope;
        RequestPayload = requestPayload;
        ExternalPortContext = externalPortContext;
        _requiredRuntimeLane = requiredRuntimeLane;
        RuntimeContext = runtimeContext;
    }

    private readonly string? _requiredRuntimeLane;

    public string AuthorityScope { get; }
    public JsonElement? RequestPayload { get; }
    public ExternalPortExecutionContext? ExternalPortContext { get; }
    public RuntimeWorkingShape? RuntimeContext { get; }
    public string RequiredRuntimeLane => _requiredRuntimeLane ?? "external_port_runtime";
    public IReadOnlyDictionary<string, object?> ResultContext => _resultContext;
    public IReadOnlyList<string> ExecutedPrimitiveKeys => _executedPrimitiveKeys;
    public IReadOnlyList<AbstractFunctionAuthorityBinding> AuthorityBindings { get; private set; } = Array.Empty<AbstractFunctionAuthorityBinding>();

    internal void SetAuthorityBindings(IReadOnlyList<AbstractFunctionAuthorityBinding> bindings) => AuthorityBindings = bindings;

    public object? ResolveBinding(AbstractFunctionInputBinding binding, IReadOnlyDictionary<string, string>? stepConfig = null)
    {
        if (binding.BindingSource == "payload") return ResolveJsonPath(RequestPayload, binding.BindingPath);
        if (binding.BindingSource == "result_context") return _resultContext.TryGetValue(binding.BindingPath, out var value) ? value : null;
        if (binding.BindingSource == "constant") return binding.BindingPath;
        if (binding.BindingSource == "external_context") return ResolveExternalContext(binding.BindingPath);
        if (binding.BindingSource == "runtime_context") return ResolveRuntimeContext(binding.BindingPath);
        if (binding.BindingSource == "step_config")
        {
            if (stepConfig is null)
                throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidInputBinding, "ABSTRACT_FUNCTION_STEP_CONFIG_NOT_AVAILABLE");
            return stepConfig.TryGetValue(binding.BindingPath, out var configValue) ? configValue : null;
        }
        throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidInputBinding, $"ABSTRACT_FUNCTION_INPUT_BINDING_SOURCE_UNSUPPORTED: {binding.BindingSource}");
    }

    public void StoreResult(string? key, object? value)
    {
        if (!string.IsNullOrWhiteSpace(key)) _resultContext[key] = value;
    }

    public void MarkExecuted(string primitiveKey) => _executedPrimitiveKeys.Add(primitiveKey);

    public void ApplyResultToExternalContext(string? key, object? value)
    {
        if (ExternalPortContext is null || string.IsNullOrWhiteSpace(key) || value is null) return;
        switch (key)
        {
            case "ExportJobId" when value is Guid exportJobId:
                ExternalPortContext.ExportJobId = exportJobId;
                break;
            case "FileArtifactId" when value is Guid fileArtifactId:
                ExternalPortContext.FileArtifactId = fileArtifactId;
                break;
            case "ChecksumValue" when value is string checksum:
                ExternalPortContext.ChecksumValue = checksum;
                break;
            case "AuthorizationKey" when value is string authorizationKey:
                ExternalPortContext.AuthorizationKey = authorizationKey;
                break;
            case "OutputProp":
                ExternalPortContext.OutputProp = value is string s ? s : JsonSerializer.Serialize(value);
                break;
        }
    }

    private object? ResolveExternalContext(string key) => key switch
    {
        "port_id" => ExternalPortContext?.PortRecord?.PortId,
        "port_kind" => ExternalPortContext?.PortRecord?.PortKind ?? ExternalPortContext?.PortKind,
        "required_by_bundle" => ExternalPortContext?.RequiredByBundle ?? ExternalPortContext?.Policy?.RequiredByBundle,
        "storage_ref" => ExternalPortContext?.PortRecord?.ReferenceKey,
        "export_job_id" => ExternalPortContext?.ExportJobId,
        "file_artifact_id" => ExternalPortContext?.FileArtifactId,
        "checksum_value" => ExternalPortContext?.ChecksumValue,
        "credential_vault_id" => ExternalPortContext?.CredentialVaultRecord?.CredentialVaultId,
        "decrypted_credential_payload" => ExternalPortContext?.DecryptedCredentialPayload,
        _ => throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidInputBinding, $"ABSTRACT_FUNCTION_EXTERNAL_CONTEXT_BINDING_UNSUPPORTED: {key}")
    };

    private object? ResolveRuntimeContext(string key) => key switch
    {
        "working_shape" => RuntimeContext,
        _ => throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidInputBinding, $"ABSTRACT_FUNCTION_RUNTIME_CONTEXT_BINDING_UNSUPPORTED: {key}")
    };

    private static object? ResolveJsonPath(JsonElement? payload, string key)
    {
        if (payload is null || string.IsNullOrWhiteSpace(key)) return null;
        var current = payload.Value;
        foreach (var part in key.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out current)) return null;
        }
        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number when current.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => current.GetRawText()
        };
    }
}

public interface IAbstractFunctionManifestRepository
{
    Task<AbstractFunctionManifest?> LoadAsync(string functionKey, CancellationToken ct = default);
}

public interface IAbstractFunctionPrimitiveAdapter
{
    string PrimitiveKey { get; }
    Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default);
}

public sealed class AbstractFunctionExecutor
{
    private static readonly ISet<string> SecretProjectionDenyKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "credential", "credential_payload", "decrypted_payload", "plaintext_payload", "signed_url", "bucket", "endpoint", "storage_path", "storage_ref", "raw_storage_ref",
        "decrypted_credential_payload", "token_response", "token_body"
    };

    private readonly IAbstractFunctionManifestRepository _manifestRepository;
    private readonly IReadOnlyDictionary<string, IAbstractFunctionPrimitiveAdapter> _primitiveRegistry;

    public AbstractFunctionExecutor(IAbstractFunctionManifestRepository manifestRepository, IEnumerable<IAbstractFunctionPrimitiveAdapter> primitiveAdapters)
    {
        _manifestRepository = manifestRepository;
        _primitiveRegistry = primitiveAdapters.ToDictionary(static adapter => adapter.PrimitiveKey, StringComparer.Ordinal);
    }

    public async Task<AbstractFunctionExecutionContext> ExecuteAsync(string functionKey, AbstractFunctionExecutionContext context, CancellationToken ct = default)
    {
        var manifest = await _manifestRepository.LoadAsync(functionKey, ct) ?? throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingAuthority, "ABSTRACT_FUNCTION_MANIFEST_MISSING");
        if (!manifest.Active) throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "ABSTRACT_FUNCTION_MANIFEST_INACTIVE");
        if (!string.Equals(manifest.RuntimeLane, context.RequiredRuntimeLane, StringComparison.Ordinal)) throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "ABSTRACT_FUNCTION_RUNTIME_LANE_INVALID");
        if (!string.Equals(manifest.AuthorityScope, context.AuthorityScope, StringComparison.Ordinal)) throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "ABSTRACT_FUNCTION_AUTHORITY_SCOPE_INVALID");

        var activeAuthority = (manifest.AuthorityBindings ?? Array.Empty<AbstractFunctionAuthorityBinding>())
            .Where(static b => b.Active).ToList();
        if (activeAuthority.Count == 0)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingAuthority, "ABSTRACT_FUNCTION_AUTHORITY_BINDINGS_MISSING");

        var policyKey = context.ExternalPortContext?.Policy?.PolicyKey;
        if (policyKey is not null)
        {
            if (!activeAuthority.Any(b => string.Equals(b.AuthorityKind, "policy", StringComparison.Ordinal) && string.Equals(b.AuthorityRef, policyKey, StringComparison.Ordinal)))
                throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, $"ABSTRACT_FUNCTION_POLICY_AUTHORITY_INVALID: {policyKey}");
        }
        else
        {
            if (!activeAuthority.Any(static b => string.Equals(b.AuthorityKind, "policy", StringComparison.Ordinal)))
                throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingAuthority, "ABSTRACT_FUNCTION_POLICY_AUTHORITY_MISSING");
        }

        var allActiveSteps = manifest.Steps.Where(static s => s.Active).ToList();
        var normalSteps = allActiveSteps.Where(static s => !s.IsCompensationStep).OrderBy(static s => s.StepOrder).ToList();
        var compensationSteps = allActiveSteps.Where(static s => s.IsCompensationStep).OrderBy(static s => s.StepOrder).ToList();

        var outputShape = manifest.OutputShape;
        if (outputShape is not null && outputShape.Count > 0)
        {
            var allowedOutputKeys = new HashSet<string>(outputShape.Values, StringComparer.Ordinal);
            foreach (var step in normalSteps.Where(static s => s.ResultContextKey is not null))
            {
                if (!allowedOutputKeys.Contains(step.ResultContextKey!))
                    throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, $"ABSTRACT_FUNCTION_OUTPUT_KEY_UNAUTHORIZED: {step.ResultContextKey}");
            }
        }

        context.SetAuthorityBindings(activeAuthority);

        var deniedProjectionKeys = new HashSet<string>(manifest.DeniedProjectionKeys, StringComparer.OrdinalIgnoreCase);
        foreach (var denied in SecretProjectionDenyKeys) deniedProjectionKeys.Add(denied);

        try
        {
            foreach (var step in normalSteps)
            {
                if (!_primitiveRegistry.TryGetValue(step.PrimitiveKey, out var primitive)) throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.UnsupportedPrimitive, $"ABSTRACT_FUNCTION_PRIMITIVE_UNSUPPORTED: {step.PrimitiveKey}");
                var inputs = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var binding in step.InputBindings)
                {
                    if (string.Equals(step.PrimitiveKey, "projection", StringComparison.Ordinal) && (binding.Secret || deniedProjectionKeys.Contains(binding.InputKey))) throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.SecretProjectionDenied, "ABSTRACT_FUNCTION_SECRET_PROJECTION_DENIED");
                    var value = context.ResolveBinding(binding, step.StepConfig);
                    if (binding.Required && value is null) throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, $"ABSTRACT_FUNCTION_INPUT_MISSING: {binding.InputKey}");
                    inputs[binding.InputKey] = value;
                }
                var result = await primitive.ExecuteAsync(step, inputs, context, ct);
                context.StoreResult(step.ResultContextKey, result);
                context.ApplyResultToExternalContext(step.ResultContextKey, result);
                context.MarkExecuted(step.PrimitiveKey);
            }
        }
        catch
        {
            foreach (var cStep in compensationSteps)
            {
                if (!_primitiveRegistry.TryGetValue(cStep.PrimitiveKey, out var cPrimitive)) continue;
                var cInputs = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var binding in cStep.InputBindings)
                {
                    try
                    {
                        var value = context.ResolveBinding(binding, cStep.StepConfig);
                        cInputs[binding.InputKey] = value;
                    }
                    catch { /* best-effort: skip binding errors during compensation */ }
                }
                try { await cPrimitive.ExecuteAsync(cStep, cInputs, context, ct); }
                catch { /* best-effort: ignore compensation step failures */ }
            }
            throw;
        }
        return context;
    }
}

public sealed class ProjectionPrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
{
    public string PrimitiveKey => "projection";
    public Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default) => Task.FromResult<object?>(inputs.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value, StringComparer.Ordinal));
}


public sealed class CallPostgresFunctionPrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
{
    private static readonly Regex AllowedFunctionName = new(@"^topology\.[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string _connectionString;

    public CallPostgresFunctionPrimitiveAdapter(string connectionString) =>
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public string PrimitiveKey => "call_postgres_function";

    public async Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
    {
        if (!step.StepConfig.TryGetValue("function", out var functionName) || string.IsNullOrWhiteSpace(functionName) || !AllowedFunctionName.IsMatch(functionName))
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "ABSTRACT_FUNCTION_POSTGRES_FUNCTION_INVALID");

        if (!step.StepConfig.TryGetValue("required_table_authority", out var requiredTable) || string.IsNullOrWhiteSpace(requiredTable))
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "ABSTRACT_FUNCTION_TABLE_AUTHORITY_MISSING_FROM_STEP_CONFIG");

        if (!context.AuthorityBindings.Any(b => string.Equals(b.AuthorityKind, "table", StringComparison.Ordinal) && string.Equals(b.AuthorityRef, requiredTable, StringComparison.Ordinal)))
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, $"ABSTRACT_FUNCTION_TABLE_AUTHORITY_INVALID: required={requiredTable}");

        var argumentKeys = ReadArgumentKeys(step);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {functionName}({string.Join(", ", argumentKeys.Select((_, index) => $"@p{index}"))})";

        for (var index = 0; index < argumentKeys.Count; index++)
        {
            if (!inputs.TryGetValue(argumentKeys[index], out var value))
                throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, $"ABSTRACT_FUNCTION_POSTGRES_ARGUMENT_MISSING: {argumentKeys[index]}");
            cmd.Parameters.AddWithValue($"p{index}", value ?? DBNull.Value);
        }

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is DBNull ? null : result;
    }

    private static IReadOnlyList<string> ReadArgumentKeys(AbstractFunctionStep step)
    {
        if (!step.StepConfig.TryGetValue("arguments", out var json) || string.IsNullOrWhiteSpace(json))
            return step.InputBindings.Select(static binding => binding.InputKey).ToArray();

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidInputBinding, "ABSTRACT_FUNCTION_POSTGRES_ARGUMENTS_INVALID");
        return doc.RootElement.EnumerateArray().Select(static item => item.GetString() ?? string.Empty).Where(static item => item.Length > 0).ToArray();
    }
}

public sealed class FailClosePrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
{
    public string PrimitiveKey => "fail_close";

    public Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
    {
        var status = step.StepConfig.TryGetValue("status", out var configuredStatus) && !string.IsNullOrWhiteSpace(configuredStatus)
            ? configuredStatus
            : AbstractFunctionFailCloseStatus.InvalidAuthority;
        throw new AbstractFunctionFailCloseException(status, "ABSTRACT_FUNCTION_FAIL_CLOSE_PRIMITIVE");
    }
}

/// <summary>
/// Primitive adapter for the sql_attention primitive key.
///
/// Implements read-only SQL Attention projection through the abstract function substrate:
///   - table authority for logs.attention is verified from manifest authority bindings.
///   - function_name and parameter_key are resolved from step_config (manifest-authority).
///   - source_set_id is resolved from inputs (payload-bound by manifest input binding).
///   - Policy is loaded from topology.function_parameters (data-defined; no literal fallback).
///   - Evidence is loaded from logs.attention (read-only; no write to evidence layer).
///   - Candidates are projected via SqlAttentionTopologyProjectionRuntime.ProjectCandidates.
///
/// Fail-close statuses:
///   - missing_input       : source_set_id is null or whitespace
///   - invalid_authority   : logs.attention table authority not present, or step_config missing function_name/parameter_key
///   - MissingPolicy (result status) : policy row not found; not a fail-close exception — returned as explicit result status
///   - DbUnavailable (result status) : logs.attention query failed; not a fail-close exception — returned as explicit result status
///
/// Prohibited:
///   - Writing to logs.attention or any evidence layer.
///   - Auto-mutating registry / topology / route from projection result.
///   - Phase Attention internals inside this primitive (call adapter path only).
/// </summary>
public sealed class SqlAttentionProjectionPrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
{
    private readonly ILogger<SqlAttentionProjectionPrimitiveAdapter> _logger;
    private readonly SqlAttentionLogsRepository _logsRepository;
    private readonly TopologyRepository _topologyRepository;

    public SqlAttentionProjectionPrimitiveAdapter(
        ILogger<SqlAttentionProjectionPrimitiveAdapter> logger,
        SqlAttentionLogsRepository logsRepository,
        TopologyRepository topologyRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logsRepository = logsRepository ?? throw new ArgumentNullException(nameof(logsRepository));
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
    }

    public string PrimitiveKey => "sql_attention";

    public async Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
    {
        if (!context.AuthorityBindings.Any(b => string.Equals(b.AuthorityKind, "table", StringComparison.Ordinal) && string.Equals(b.AuthorityRef, "logs.attention", StringComparison.Ordinal)))
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "ABSTRACT_FUNCTION_SQL_ATTENTION_TABLE_AUTHORITY_INVALID: required=logs.attention");

        if (!step.StepConfig.TryGetValue("function_name", out var functionName) || string.IsNullOrWhiteSpace(functionName))
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "ABSTRACT_FUNCTION_SQL_ATTENTION_FUNCTION_NAME_MISSING_FROM_STEP_CONFIG");

        if (!step.StepConfig.TryGetValue("parameter_key", out var parameterKey) || string.IsNullOrWhiteSpace(parameterKey))
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "ABSTRACT_FUNCTION_SQL_ATTENTION_PARAMETER_KEY_MISSING_FROM_STEP_CONFIG");

        if (!inputs.TryGetValue("source_set_id", out var sourceSetIdRaw) || sourceSetIdRaw is not string sourceSetId || string.IsNullOrWhiteSpace(sourceSetId))
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "ABSTRACT_FUNCTION_SQL_ATTENTION_SOURCE_SET_ID_MISSING");

        var evaluatedAt = DateTimeOffset.UtcNow;

        var policyJson = await _topologyRepository.LoadFunctionParameterAsync(functionName, parameterKey, ct);
        if (policyJson is null)
        {
            _logger.LogError("SqlAttentionProjectionPrimitiveAdapter: MissingPolicy — no active function_parameters row for '{Fn}/{Key}'.", functionName, parameterKey);
            return new SqlAttentionTopologyProjectionResult(
                Status: TopologyProjectionStatus.MissingPolicy,
                StatusDetail: $"No active function_parameters row for '{functionName}/{parameterKey}'.",
                Candidates: [],
                EvaluatedAt: evaluatedAt);
        }

        SqlAttentionTopologyProjectionPolicy policy;
        try
        {
            policy = SqlAttentionTopologyProjectionRuntime.ParsePolicy(policyJson);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            _logger.LogError(ex, "SqlAttentionProjectionPrimitiveAdapter: MalformedPolicy — '{Fn}/{Key}' could not be parsed.", functionName, parameterKey);
            return new SqlAttentionTopologyProjectionResult(
                Status: TopologyProjectionStatus.MalformedPolicy,
                StatusDetail: $"Policy JSON for '{functionName}/{parameterKey}' is malformed: {ex.Message}",
                Candidates: [],
                EvaluatedAt: evaluatedAt);
        }

        IReadOnlyList<AttentionEvidenceRecord> evidence;
        try
        {
            evidence = await _logsRepository.LoadAttentionEvidenceForProjectionAsync(
                sourceSetId, policy.TopK, policy.MinNeighborScore, policy.RecentWindowDays, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SqlAttentionProjectionPrimitiveAdapter: DbUnavailable — logs.attention query failed for sourceSetId={SourceSetId}.", sourceSetId);
            return new SqlAttentionTopologyProjectionResult(
                Status: TopologyProjectionStatus.DbUnavailable,
                StatusDetail: $"logs.attention query failed for sourceSetId='{sourceSetId}': {ex.Message}",
                Candidates: [],
                EvaluatedAt: evaluatedAt);
        }

        if (evidence.Count == 0)
        {
            _logger.LogDebug("SqlAttentionProjectionPrimitiveAdapter: NoEvidence — no evidence rows for sourceSetId={SourceSetId}.", sourceSetId);
            return new SqlAttentionTopologyProjectionResult(
                Status: TopologyProjectionStatus.NoEvidence,
                StatusDetail: "No recent attention evidence rows found.",
                Candidates: [],
                EvaluatedAt: evaluatedAt);
        }

        var candidates = SqlAttentionTopologyProjectionRuntime.ProjectCandidates(evidence);
        _logger.LogInformation(
            "SqlAttentionProjectionPrimitiveAdapter: projected {Count} candidate(s) from {EvidenceCount} evidence row(s) for sourceSetId={SourceSetId}.",
            candidates.Count, evidence.Count, sourceSetId);

        return new SqlAttentionTopologyProjectionResult(
            Status: TopologyProjectionStatus.Ok,
            StatusDetail: null,
            Candidates: candidates,
            EvaluatedAt: evaluatedAt);
    }
}

/// <summary>
/// COMPATIBILITY FALLBACK — calls ContextRouteRecommendationResolver.ResolveAsync (monolithic).
/// Canonical path uses the 4-step decomposed primitives (recommendation_candidate_source →
/// recommendation_eligibility → recommendation_score_rank → recommendation_projection).
/// Kept for backward compatibility with legacy manifests that use a single recommendation_attention step.
/// </summary>
public sealed class RecommendationAttentionPrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
{
    private readonly ILogger<RecommendationAttentionPrimitiveAdapter> _logger;
    private readonly ContextRouteRecommendationResolver _resolver;

    public RecommendationAttentionPrimitiveAdapter(
        ILogger<RecommendationAttentionPrimitiveAdapter> logger,
        ContextRouteRecommendationResolver resolver)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public string PrimitiveKey => "recommendation_attention";

    public async Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
    {
        if (!context.AuthorityBindings.Any(b => string.Equals(b.AuthorityKind, "table", StringComparison.Ordinal) && string.Equals(b.AuthorityRef, "context_route.context_hub_recommendation_current", StringComparison.Ordinal)))
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "ABSTRACT_FUNCTION_RECOMMENDATION_ATTENTION_TABLE_AUTHORITY_INVALID: required=context_route.context_hub_recommendation_current");

        if (!step.StepConfig.TryGetValue("function_name", out var functionName) || string.IsNullOrWhiteSpace(functionName))
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "ABSTRACT_FUNCTION_RECOMMENDATION_ATTENTION_FUNCTION_NAME_MISSING: FUNCTION_NAME_MISSING");

        if (!step.StepConfig.TryGetValue("parameter_key", out var parameterKey) || string.IsNullOrWhiteSpace(parameterKey))
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "ABSTRACT_FUNCTION_RECOMMENDATION_ATTENTION_PARAMETER_KEY_MISSING");

        if (!inputs.TryGetValue("working_shape", out var shapeRaw) || shapeRaw is not RuntimeWorkingShape shape)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "ABSTRACT_FUNCTION_RECOMMENDATION_ATTENTION_WORKING_SHAPE_MISSING");

        _logger.LogDebug("RecommendationAttentionPrimitiveAdapter: resolving via adapter for function_name={FunctionName}.", functionName);
        return await _resolver.ResolveAsync(shape, functionName!, parameterKey!, ct);
    }
}

/// <summary>
/// Primitive adapter for recommendation_candidate_source (phase 1 of decomposed af09 path).
/// Loads policy from function_parameters, checks vector/session, loads prefix candidates.
/// function_name and parameter_key MUST come from step_config (manifest-authority, not payload).
/// working_shape is bound from runtime_context input.
/// Table authority for context_route.context_hub_recommendation_current is required.
/// Returns RecommendationCandidateSourceResult (includes InsufficientHistory for NO_SESSION_ID).
/// </summary>
public sealed class RecommendationCandidateSourcePrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
{
    private readonly ILogger<RecommendationCandidateSourcePrimitiveAdapter> _logger;
    private readonly ContextRouteRecommendationResolver _resolver;

    public RecommendationCandidateSourcePrimitiveAdapter(
        ILogger<RecommendationCandidateSourcePrimitiveAdapter> logger,
        ContextRouteRecommendationResolver resolver)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public string PrimitiveKey => "recommendation_candidate_source";

    public async Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
    {
        if (!context.AuthorityBindings.Any(b => string.Equals(b.AuthorityKind, "table", StringComparison.Ordinal) && string.Equals(b.AuthorityRef, "context_route.context_hub_recommendation_current", StringComparison.Ordinal)))
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "ABSTRACT_FUNCTION_RECOMMENDATION_CANDIDATE_SOURCE_TABLE_AUTHORITY_INVALID: required=context_route.context_hub_recommendation_current");

        if (!step.StepConfig.TryGetValue("function_name", out var functionName) || string.IsNullOrWhiteSpace(functionName))
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "ABSTRACT_FUNCTION_RECOMMENDATION_CANDIDATE_SOURCE_FUNCTION_NAME_MISSING: FUNCTION_NAME_MISSING");

        if (!step.StepConfig.TryGetValue("parameter_key", out var parameterKey) || string.IsNullOrWhiteSpace(parameterKey))
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "ABSTRACT_FUNCTION_RECOMMENDATION_CANDIDATE_SOURCE_PARAMETER_KEY_MISSING");

        if (!inputs.TryGetValue("working_shape", out var shapeRaw) || shapeRaw is not RuntimeWorkingShape shape)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "ABSTRACT_FUNCTION_RECOMMENDATION_CANDIDATE_SOURCE_WORKING_SHAPE_MISSING");

        _logger.LogDebug("RecommendationCandidateSourcePrimitiveAdapter: loading candidates for function_name={FunctionName}.", functionName);
        return await _resolver.BuildCandidateSourceAsync(shape, functionName!, parameterKey!, ct);
    }
}

/// <summary>
/// Primitive adapter for recommendation_eligibility (phase 2 of decomposed af09 path).
/// Runs neighbor search, loads transition stats, appends context event, runs TVR extension.
/// Reads working_shape (runtime_context) and recommendation_source (result_context) from inputs.
/// Event append always runs when source status is Ok (cold-start history growth).
/// Returns RecommendationEligibilityResult (includes InsufficientHistory for thin history).
/// </summary>
public sealed class RecommendationEligibilityPrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
{
    private readonly ILogger<RecommendationEligibilityPrimitiveAdapter> _logger;
    private readonly ContextRouteRecommendationResolver _resolver;

    public RecommendationEligibilityPrimitiveAdapter(
        ILogger<RecommendationEligibilityPrimitiveAdapter> logger,
        ContextRouteRecommendationResolver resolver)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public string PrimitiveKey => "recommendation_eligibility";

    public async Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
    {
        if (!inputs.TryGetValue("working_shape", out var shapeRaw) || shapeRaw is not RuntimeWorkingShape shape)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "ABSTRACT_FUNCTION_RECOMMENDATION_ELIGIBILITY_WORKING_SHAPE_MISSING");

        if (!inputs.TryGetValue("recommendation_source", out var sourceRaw) || sourceRaw is not RecommendationCandidateSourceResult source)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "ABSTRACT_FUNCTION_RECOMMENDATION_ELIGIBILITY_SOURCE_MISSING");

        return await _resolver.BuildEligibilityAsync(shape, source, ct);
    }
}

/// <summary>
/// Primitive adapter for recommendation_score_rank (phase 3 of decomposed af09 path).
/// Loads attention blend map, ranks operations/tokens/enum items.
/// Reads working_shape, recommendation_source, recommendation_eligibility from inputs.
/// Skips computation when eligibility status is not Ok (propagates status).
/// Returns RecommendationScoreRankResult per lane (ui_pressure, state_pressure).
/// </summary>
public sealed class RecommendationScoreRankPrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
{
    private readonly ILogger<RecommendationScoreRankPrimitiveAdapter> _logger;
    private readonly ContextRouteRecommendationResolver _resolver;

    public RecommendationScoreRankPrimitiveAdapter(
        ILogger<RecommendationScoreRankPrimitiveAdapter> logger,
        ContextRouteRecommendationResolver resolver)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public string PrimitiveKey => "recommendation_score_rank";

    public async Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
    {
        if (!inputs.TryGetValue("working_shape", out var shapeRaw) || shapeRaw is not RuntimeWorkingShape shape)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "ABSTRACT_FUNCTION_RECOMMENDATION_SCORE_RANK_WORKING_SHAPE_MISSING");

        if (!inputs.TryGetValue("recommendation_source", out var sourceRaw) || sourceRaw is not RecommendationCandidateSourceResult source)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "ABSTRACT_FUNCTION_RECOMMENDATION_SCORE_RANK_SOURCE_MISSING");

        if (!inputs.TryGetValue("recommendation_eligibility", out var eligibilityRaw) || eligibilityRaw is not RecommendationEligibilityResult eligibility)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "ABSTRACT_FUNCTION_RECOMMENDATION_SCORE_RANK_ELIGIBILITY_MISSING");

        return await _resolver.BuildScoreRankAsync(shape, source, eligibility, ct);
    }
}

/// <summary>
/// Primitive adapter for recommendation_projection (phase 4 of decomposed af09 path).
/// Assembles final ContextRouteRecommendationResult from all intermediate phase results.
/// Enforces lane separation: ui_pressure and state_pressure candidates must not carry
/// sql_attention_projection lane — mixing is fail-closed with InvalidProjection.
/// Reads recommendation_source, recommendation_eligibility, recommendation_score_rank from inputs.
/// Returns ContextRouteRecommendationResult (the terminal output stored as recommendation_result).
/// </summary>
public sealed class RecommendationProjectionPrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
{
    public string PrimitiveKey => "recommendation_projection";

    public Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
    {
        if (!inputs.TryGetValue("recommendation_source", out var sourceRaw) || sourceRaw is not RecommendationCandidateSourceResult source)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "ABSTRACT_FUNCTION_RECOMMENDATION_PROJECTION_SOURCE_MISSING");

        if (!inputs.TryGetValue("recommendation_eligibility", out var eligibilityRaw) || eligibilityRaw is not RecommendationEligibilityResult eligibility)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "ABSTRACT_FUNCTION_RECOMMENDATION_PROJECTION_ELIGIBILITY_MISSING");

        if (!inputs.TryGetValue("recommendation_score_rank", out var scoreRankRaw) || scoreRankRaw is not RecommendationScoreRankResult scoreRank)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "ABSTRACT_FUNCTION_RECOMMENDATION_PROJECTION_SCORE_RANK_MISSING");

        var result = ContextRouteRecommendationResolver.BuildProjectionResult(source, eligibility, scoreRank);

        // Lane enforcement: hub-local recommendation result (ui_pressure, state_pressure) must not
        // contain sql_attention_projection candidates. Mixing lanes is fail-closed.
        if (result.Status == RecommendationStatus.Ok)
        {
            var allCandidates = result.NextOperations.Concat(result.NextTokens).Concat(result.NextEnumItems);
            foreach (var candidate in allCandidates)
            {
                if (string.Equals(candidate.Lane, RecommendationPressureLanes.SqlAttentionProjection, StringComparison.Ordinal))
                    throw new AbstractFunctionFailCloseException(
                        AbstractFunctionFailCloseStatus.InvalidProjection,
                        $"RECOMMENDATION_HUB_LOCAL_LANE_MIXING_PREVENTED: sql_attention_projection candidate in hub-local result (value={candidate.Value})");
            }
        }

        return Task.FromResult<object?>(result);
    }
}

/// <summary>
/// Primitive adapter for credential_acquire_lease (step 1 of credential.refresh_token manifest).
/// Reads credential_vault_id from external_context input binding, lease_owner and
/// lease_duration_minutes from step_config (manifest-authority; fail-close if missing).
/// Returns ExternalCredentialRefreshLease stored as credential_lease.
/// Prohibited: provider-specific branching, bundle-specific branching, lease default supplementation.
/// </summary>
public sealed class CredentialAcquireLeaseAdapter : IAbstractFunctionPrimitiveAdapter
{
    private readonly IExternalCredentialVaultRepository _vaultRepository;

    public CredentialAcquireLeaseAdapter(IExternalCredentialVaultRepository vaultRepository)
        => _vaultRepository = vaultRepository ?? throw new ArgumentNullException(nameof(vaultRepository));

    public string PrimitiveKey => "credential_acquire_lease";

    public async Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
    {
        if (!step.StepConfig.TryGetValue("lease_owner", out var leaseOwner) || string.IsNullOrWhiteSpace(leaseOwner))
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "CREDENTIAL_ACQUIRE_LEASE_OWNER_MISSING");

        if (!step.StepConfig.TryGetValue("lease_duration_minutes", out var durationStr) || !int.TryParse(durationStr, out var durationMinutes) || durationMinutes <= 0)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "CREDENTIAL_ACQUIRE_LEASE_DURATION_MISSING");

        if (!inputs.TryGetValue("credential_vault_id", out var vaultIdRaw) || vaultIdRaw is not Guid credentialVaultId)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "CREDENTIAL_ACQUIRE_LEASE_VAULT_ID_MISSING");

        var lease = await _vaultRepository.AcquireRefreshLeaseAsync(credentialVaultId, leaseOwner, TimeSpan.FromMinutes(durationMinutes), DateTimeOffset.UtcNow, ct)
            ?? throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "CREDENTIAL_ACQUIRE_LEASE_UNAVAILABLE");

        return lease;
    }
}

/// <summary>
/// Primitive adapter for credential_http_request (step 2 of credential.refresh_token manifest).
/// Reads method from step_config (fail-close if missing; no GET/POST default).
/// Endpoint resolved from step_config["endpoint"] or ExternalPortContext.PortRecord.UrlOrEnvReference.
/// decrypted_credential_payload from external_context input binding (secret=true).
/// Sends HTTP request and returns ExternalPortHttpResponse stored as token_response.
/// Prohibited: provider-specific branching, env:-prefix special-casing, HTTP method defaulting.
/// </summary>
public sealed class CredentialHttpRequestAdapter : IAbstractFunctionPrimitiveAdapter
{
    private readonly IExternalPortHttpClient _httpClient;

    public CredentialHttpRequestAdapter(IExternalPortHttpClient httpClient)
        => _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public string PrimitiveKey => "credential_http_request";

    public async Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
    {
        if (!step.StepConfig.TryGetValue("method", out var methodStr) || string.IsNullOrWhiteSpace(methodStr))
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "CREDENTIAL_HTTP_REQUEST_METHOD_MISSING");

        var endpoint = step.StepConfig.TryGetValue("endpoint", out var configEndpoint) && !string.IsNullOrWhiteSpace(configEndpoint)
            ? configEndpoint
            : context.ExternalPortContext?.PortRecord?.UrlOrEnvReference;

        if (string.IsNullOrWhiteSpace(endpoint))
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "CREDENTIAL_HTTP_REQUEST_ENDPOINT_MISSING");

        if (!inputs.TryGetValue("decrypted_credential_payload", out var payloadRaw) || payloadRaw is not string decryptedPayload)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "CREDENTIAL_HTTP_REQUEST_DECRYPTED_PAYLOAD_MISSING");

        var httpRequest = new ExternalPortHttpRequest(
            new Uri(endpoint, UriKind.RelativeOrAbsolute),
            new HttpMethod(methodStr),
            new Dictionary<string, string>(),
            decryptedPayload);

        var httpResponse = await _httpClient.SendAsync(httpRequest, ct);
        if (httpResponse.StatusCode < 200 || httpResponse.StatusCode >= 300)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidInputBinding, $"CREDENTIAL_HTTP_REQUEST_NON_2XX: {httpResponse.StatusCode}");
        return httpResponse;
    }
}

/// <summary>
/// Primitive adapter for credential_compute_token_hash (step 3 of credential.refresh_token manifest).
/// Reads token_response from result_context input binding (secret=true).
/// Computes hash via IExternalCredentialCrypto.ComputeTokenHash — no response-length placeholder.
/// Returns hash string stored as token_hash.
/// Prohibited: provider-specific branching, response-length hash, hardcoded hash key from request config.
/// </summary>
public sealed class CredentialComputeTokenHashAdapter : IAbstractFunctionPrimitiveAdapter
{
    private readonly IExternalCredentialCrypto _crypto;

    public CredentialComputeTokenHashAdapter(IExternalCredentialCrypto crypto)
        => _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));

    public string PrimitiveKey => "credential_compute_token_hash";

    public Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
    {
        if (!inputs.TryGetValue("token_response", out var responseRaw) || responseRaw is not ExternalPortHttpResponse response)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "CREDENTIAL_COMPUTE_TOKEN_HASH_RESPONSE_MISSING");

        return Task.FromResult<object?>(_crypto.ComputeTokenHash(response.Body));
    }
}

/// <summary>
/// Primitive adapter for credential_parse_expires_at (step 4 of credential.refresh_token manifest).
/// Reads token_response from result_context input binding (secret=true).
/// Reads expires_at_response_key from step_config (fail-close if missing; no AddHours fallback).
/// Parses DateTimeOffset from response JSON — supports ISO string or Unix timestamp number.
/// Returns DateTimeOffset stored as token_expires_at.
/// Prohibited: AddHours(1) expiry fallback, hardcoded response key, provider-specific branching.
/// </summary>
public sealed class CredentialParseExpiresAtAdapter : IAbstractFunctionPrimitiveAdapter
{
    public string PrimitiveKey => "credential_parse_expires_at";

    public Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
    {
        if (!inputs.TryGetValue("token_response", out var responseRaw) || responseRaw is not ExternalPortHttpResponse response)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "CREDENTIAL_PARSE_EXPIRES_AT_RESPONSE_MISSING");

        if (!step.StepConfig.TryGetValue("expires_at_response_key", out var expiresAtKey) || string.IsNullOrWhiteSpace(expiresAtKey))
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "CREDENTIAL_PARSE_EXPIRES_AT_KEY_MISSING");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(response.Body); }
        catch (JsonException) { throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidInputBinding, "CREDENTIAL_PARSE_EXPIRES_AT_RESPONSE_INVALID_JSON"); }
        using (doc)
        {
            if (!doc.RootElement.TryGetProperty(expiresAtKey, out var expiresAtProp))
                throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, $"CREDENTIAL_PARSE_EXPIRES_AT_KEY_NOT_IN_RESPONSE: {expiresAtKey}");

            var expiresAt = expiresAtProp.ValueKind switch
            {
                JsonValueKind.String => DateTimeOffset.Parse(expiresAtProp.GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind),
                JsonValueKind.Number => DateTimeOffset.FromUnixTimeSeconds(expiresAtProp.GetInt64()),
                _ => throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidInputBinding, "CREDENTIAL_PARSE_EXPIRES_AT_FORMAT_UNSUPPORTED")
            };
            return Task.FromResult<object?>(expiresAt);
        }
    }
}

/// <summary>
/// Primitive adapter for credential_write_vault (step 5 of credential.refresh_token manifest).
/// Reads vault record from ExternalPortContext (encryption_key_reference required; fail-close if missing).
/// Reads credential_lease, token_hash, token_expires_at, token_response from inputs.
/// Encrypts response body and writes atomically via IExternalCredentialVaultRepository.
/// Atomic write uses lease.Version as expected_version (optimistic concurrency guard).
/// Returns null (no output to result_context).
/// Prohibited: plaintext credential in result_context, provider-specific branching, expiry fallback.
/// </summary>
public sealed class CredentialWriteVaultAdapter : IAbstractFunctionPrimitiveAdapter
{
    private readonly IExternalCredentialVaultRepository _vaultRepository;
    private readonly IExternalCredentialCrypto _crypto;

    public CredentialWriteVaultAdapter(IExternalCredentialVaultRepository vaultRepository, IExternalCredentialCrypto crypto)
    {
        _vaultRepository = vaultRepository ?? throw new ArgumentNullException(nameof(vaultRepository));
        _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
    }

    public string PrimitiveKey => "credential_write_vault";

    public async Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
    {
        var vaultRecord = context.ExternalPortContext?.CredentialVaultRecord
            ?? throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "CREDENTIAL_WRITE_VAULT_RECORD_MISSING");

        if (string.IsNullOrWhiteSpace(vaultRecord.EncryptionKeyReference))
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "CREDENTIAL_WRITE_VAULT_ENCRYPTION_KEY_MISSING");

        if (!inputs.TryGetValue("credential_lease", out var leaseRaw) || leaseRaw is not ExternalCredentialRefreshLease lease)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "CREDENTIAL_WRITE_VAULT_LEASE_MISSING");

        if (!inputs.TryGetValue("token_hash", out var hashRaw) || hashRaw is not string tokenHash)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "CREDENTIAL_WRITE_VAULT_TOKEN_HASH_MISSING");

        if (!inputs.TryGetValue("token_expires_at", out var expiresAtRaw) || expiresAtRaw is not DateTimeOffset tokenExpiresAt)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "CREDENTIAL_WRITE_VAULT_EXPIRES_AT_MISSING");

        if (!inputs.TryGetValue("token_response", out var responseRaw) || responseRaw is not ExternalPortHttpResponse tokenResponse)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "CREDENTIAL_WRITE_VAULT_TOKEN_RESPONSE_MISSING");

        var encrypted = _crypto.EncryptForVaultStorage(tokenResponse.Body, vaultRecord.EncryptionKeyReference);

        await _vaultRepository.WriteEncryptedCredentialPayloadAsync(
            vaultRecord.CredentialVaultId,
            lease.Version,
            encrypted,
            tokenHash,
            tokenExpiresAt,
            ct);

        return null;
    }
}

/// <summary>
/// Primitive adapter for credential_release_lease (step 6 of credential.refresh_token manifest).
/// Reads credential_lease from result_context input binding.
/// Calls IExternalCredentialVaultRepository.ReleaseRefreshLeaseAsync.
/// Returns null (terminal step; no output).
/// Prohibited: provider-specific branching, lease default supplementation.
/// </summary>
public sealed class CredentialReleaseLeaseAdapter : IAbstractFunctionPrimitiveAdapter
{
    private readonly IExternalCredentialVaultRepository _vaultRepository;

    public CredentialReleaseLeaseAdapter(IExternalCredentialVaultRepository vaultRepository)
        => _vaultRepository = vaultRepository ?? throw new ArgumentNullException(nameof(vaultRepository));

    public string PrimitiveKey => "credential_release_lease";

    public async Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
    {
        if (!inputs.TryGetValue("credential_lease", out var leaseRaw) || leaseRaw is not ExternalCredentialRefreshLease lease)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "CREDENTIAL_RELEASE_LEASE_MISSING");

        await _vaultRepository.ReleaseRefreshLeaseAsync(lease, ct);
        return null;
    }
}

/// <summary>
/// Compensation-step adapter for credential_fail_lease (step 7 of credential.refresh_token manifest).
/// Runs only when a preceding normal step fails (is_compensation_step = true in seed).
/// Reads credential_lease from result_context (not required: if lease was never acquired, returns null).
/// Calls IExternalCredentialVaultRepository.FailRefreshLeaseAsync with failure_code from step_config.
/// Returns null; failures are silently swallowed by the compensation loop.
/// </summary>
public sealed class CredentialFailLeaseAdapter : IAbstractFunctionPrimitiveAdapter
{
    private readonly IExternalCredentialVaultRepository _vaultRepository;

    public CredentialFailLeaseAdapter(IExternalCredentialVaultRepository vaultRepository)
        => _vaultRepository = vaultRepository ?? throw new ArgumentNullException(nameof(vaultRepository));

    public string PrimitiveKey => "credential_fail_lease";

    public async Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
    {
        if (!inputs.TryGetValue("credential_lease", out var leaseRaw) || leaseRaw is not ExternalCredentialRefreshLease lease)
            return null;

        var failureCode = step.StepConfig.TryGetValue("failure_code", out var code) && !string.IsNullOrWhiteSpace(code)
            ? code
            : "step_failure";

        await _vaultRepository.FailRefreshLeaseAsync(lease, failureCode, ct);
        return null;
    }
}

/// <summary>
/// Primitive adapter for event_log (external_and_event category).
/// Appends an append-only runtime event log entry for the current bundle.
/// event_type is required from step_config. entity_ref_key is optional; when present,
/// it is resolved from ExternalPortContext fields (ExportJobId, FileArtifactId, ChecksumValue, AuthorizationKey).
/// Missing entity_ref_key → null entityId (not fail-close). Unresolvable entity_ref_key → fail-close.
/// Prohibited: provider_kind_branching, required_by_bundle_branching, mutable event overwrite.
/// </summary>
public sealed class EventLogPrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
{
    private readonly IExternalPortRuntimeEventLogRepository _eventLogRepository;

    public EventLogPrimitiveAdapter(IExternalPortRuntimeEventLogRepository eventLogRepository)
        => _eventLogRepository = eventLogRepository ?? throw new ArgumentNullException(nameof(eventLogRepository));

    public string PrimitiveKey => "event_log";

    public async Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
    {
        if (!step.StepConfig.TryGetValue("event_type", out var eventType) || string.IsNullOrWhiteSpace(eventType))
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "EVENT_LOG_EVENT_TYPE_MISSING");

        string? entityId = null;
        if (step.StepConfig.TryGetValue("entity_ref_key", out var refKey) && !string.IsNullOrWhiteSpace(refKey))
        {
            entityId = ResolveEntityRef(refKey, context);
            if (entityId is null)
                throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, $"EVENT_LOG_ENTITY_REF_KEY_UNRESOLVABLE: {refKey}");
        }

        var bundle = context.ExternalPortContext?.RequiredByBundle
            ?? context.ExternalPortContext?.Policy?.RequiredByBundle;
        await _eventLogRepository.AppendAsync(eventType, entityId, bundle, ct);
        return null;
    }

    private static string? ResolveEntityRef(string refKey, AbstractFunctionExecutionContext context)
    {
        var ext = context.ExternalPortContext;
        if (ext is null) return null;
        return refKey switch
        {
            "ExportJobId" => ext.ExportJobId?.ToString(),
            "FileArtifactId" => ext.FileArtifactId?.ToString(),
            "ChecksumValue" => ext.ChecksumValue,
            "AuthorizationKey" => ext.AuthorizationKey,
            _ => null
        };
    }
}

/// <summary>
/// Primitive adapter for http_request (external_and_event category).
/// Sends a generic outbound HTTP request using manifest-configured url and method.
/// url and method are required from step_config (no provider-specific defaulting).
/// Optional request_body input from input binding (result_context or payload).
/// Returns ExternalPortHttpResponse stored as http_response.
/// Prohibited: provider_kind_branching, hidden_retry, default_http_method, silent_fallback_on_error.
/// </summary>
public sealed class HttpRequestPrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
{
    private readonly IExternalPortHttpClient _httpClient;

    public HttpRequestPrimitiveAdapter(IExternalPortHttpClient httpClient)
        => _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public string PrimitiveKey => "http_request";

    public async Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
    {
        if (!step.StepConfig.TryGetValue("url", out var url) || string.IsNullOrWhiteSpace(url))
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "HTTP_REQUEST_URL_MISSING");

        if (!step.StepConfig.TryGetValue("method", out var method) || string.IsNullOrWhiteSpace(method))
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "HTTP_REQUEST_METHOD_MISSING");

        inputs.TryGetValue("request_body", out var bodyRaw);
        var body = bodyRaw as string;

        var httpRequest = new ExternalPortHttpRequest(
            new Uri(url, UriKind.RelativeOrAbsolute),
            new HttpMethod(method),
            new Dictionary<string, string>(),
            body);

        var response = await _httpClient.SendAsync(httpRequest, ct);
        if (response.StatusCode < 200 || response.StatusCode >= 300)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidInputBinding, $"HTTP_REQUEST_NON_2XX: {response.StatusCode}");

        return response;
    }
}

/// <summary>
/// Primitive adapter for scheduler_enqueue (external_and_event category).
/// Marks SchedulerEventEnqueued on ExternalPortContext — signals the caller that a scheduler event
/// should be enqueued for the current external port execution.
/// No step_config required; ExternalPortContext must be present (fail-close if missing).
/// Returns null (no result_context output).
/// Prohibited: provider_kind_branching, required_by_bundle_branching, direct scheduler client calls.
/// </summary>
public sealed class SchedulerEnqueuePrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
{
    public string PrimitiveKey => "scheduler_enqueue";

    public Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
    {
        if (context.ExternalPortContext is null)
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "SCHEDULER_ENQUEUE_EXTERNAL_PORT_CONTEXT_MISSING");

        context.ExternalPortContext.SchedulerEventEnqueued = true;
        return Task.FromResult<object?>(null);
    }
}
