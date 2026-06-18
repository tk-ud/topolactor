using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;

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

    public AbstractFunctionExecutionContext(string authorityScope, JsonElement? requestPayload = null, ExternalPortExecutionContext? externalPortContext = null)
    {
        AuthorityScope = authorityScope;
        RequestPayload = requestPayload;
        ExternalPortContext = externalPortContext;
    }

    public string AuthorityScope { get; }
    public JsonElement? RequestPayload { get; }
    public ExternalPortExecutionContext? ExternalPortContext { get; }
    public IReadOnlyDictionary<string, object?> ResultContext => _resultContext;
    public IReadOnlyList<string> ExecutedPrimitiveKeys => _executedPrimitiveKeys;
    public IReadOnlyList<AbstractFunctionAuthorityBinding> AuthorityBindings { get; private set; } = Array.Empty<AbstractFunctionAuthorityBinding>();

    internal void SetAuthorityBindings(IReadOnlyList<AbstractFunctionAuthorityBinding> bindings) => AuthorityBindings = bindings;

    public object? ResolveBinding(AbstractFunctionInputBinding binding)
    {
        if (binding.BindingSource == "payload") return ResolveJsonPath(RequestPayload, binding.BindingPath);
        if (binding.BindingSource == "result_context") return _resultContext.TryGetValue(binding.BindingPath, out var value) ? value : null;
        if (binding.BindingSource == "constant") return binding.BindingPath;
        if (binding.BindingSource == "external_context") return ResolveExternalContext(binding.BindingPath);
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
        if (!string.Equals(manifest.RuntimeLane, "external_port_runtime", StringComparison.Ordinal)) throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "ABSTRACT_FUNCTION_RUNTIME_LANE_INVALID");
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
                var value = context.ResolveBinding(binding);
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
