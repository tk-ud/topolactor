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

public sealed record AbstractFunctionStep(Guid AbstractFunctionStepId, int StepOrder, string PrimitiveKey, IReadOnlyDictionary<string, string> StepConfig, IReadOnlyList<AbstractFunctionInputBinding> InputBindings, string? ResultContextKey, bool Active);

public sealed record AbstractFunctionInputBinding(string InputKey, string BindingSource, string BindingPath, bool Required, bool Secret);

public sealed record AbstractFunctionAuthorityBinding(string AuthorityKind, string AuthorityRef, bool Active);

public sealed class AbstractFunctionExecutionContext
{
    private readonly Dictionary<string, object?> _resultContext = new(StringComparer.Ordinal);
    private readonly List<string> _executedPrimitiveKeys = new();

    public AbstractFunctionExecutionContext(string authorityScope, JsonElement? requestPayload = null, ExternalPortExecutionContext? externalPortContext = null, string? requiredRuntimeLane = null)
    {
        AuthorityScope = authorityScope;
        RequestPayload = requestPayload;
        ExternalPortContext = externalPortContext;
        _requiredRuntimeLane = requiredRuntimeLane;
    }

    private readonly string? _requiredRuntimeLane;

    public string AuthorityScope { get; }
    public JsonElement? RequestPayload { get; }
    public ExternalPortExecutionContext? ExternalPortContext { get; }
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
        _ => throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidInputBinding, $"ABSTRACT_FUNCTION_EXTERNAL_CONTEXT_BINDING_UNSUPPORTED: {key}")
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
        "credential", "credential_payload", "decrypted_payload", "plaintext_payload", "signed_url", "bucket", "endpoint", "storage_path", "storage_ref", "raw_storage_ref"
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

        var outputShape = manifest.OutputShape;
        if (outputShape is not null && outputShape.Count > 0)
        {
            var allowedOutputKeys = new HashSet<string>(outputShape.Values, StringComparer.Ordinal);
            foreach (var step in manifest.Steps.Where(static s => s.Active && s.ResultContextKey is not null))
            {
                if (!allowedOutputKeys.Contains(step.ResultContextKey!))
                    throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, $"ABSTRACT_FUNCTION_OUTPUT_KEY_UNAUTHORIZED: {step.ResultContextKey}");
            }
        }

        context.SetAuthorityBindings(activeAuthority);

        var deniedProjectionKeys = new HashSet<string>(manifest.DeniedProjectionKeys, StringComparer.OrdinalIgnoreCase);
        foreach (var denied in SecretProjectionDenyKeys) deniedProjectionKeys.Add(denied);

        foreach (var step in manifest.Steps.Where(static s => s.Active).OrderBy(static s => s.StepOrder))
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
