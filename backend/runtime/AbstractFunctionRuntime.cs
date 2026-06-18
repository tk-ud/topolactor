using System.Text.Json;

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

public sealed record AbstractFunctionManifest(Guid AbstractFunctionId, string FunctionKey, string RuntimeLane, string AuthorityScope, IReadOnlyList<AbstractFunctionStep> Steps, IReadOnlyList<string> DeniedProjectionKeys, bool Active);

public sealed record AbstractFunctionStep(Guid AbstractFunctionStepId, int StepOrder, string PrimitiveKey, IReadOnlyDictionary<string, string> StepConfig, IReadOnlyList<AbstractFunctionInputBinding> InputBindings, string? ResultContextKey, bool Active);

public sealed record AbstractFunctionInputBinding(string InputKey, string BindingSource, string BindingPath, bool Required, bool Secret);

public sealed class AbstractFunctionExecutionContext
{
    private readonly Dictionary<string, object?> _resultContext = new(StringComparer.Ordinal);
    private readonly List<string> _executedPrimitiveKeys = new();

    public AbstractFunctionExecutionContext(string authorityScope, JsonElement? requestPayload = null)
    {
        AuthorityScope = authorityScope;
        RequestPayload = requestPayload;
    }

    public string AuthorityScope { get; }
    public JsonElement? RequestPayload { get; }
    public IReadOnlyDictionary<string, object?> ResultContext => _resultContext;
    public IReadOnlyList<string> ExecutedPrimitiveKeys => _executedPrimitiveKeys;

    public object? ResolveBinding(AbstractFunctionInputBinding binding)
    {
        if (binding.BindingSource == "payload") return ResolveJsonPath(RequestPayload, binding.BindingPath);
        if (binding.BindingSource == "result_context") return _resultContext.TryGetValue(binding.BindingPath, out var value) ? value : null;
        if (binding.BindingSource == "constant") return binding.BindingPath;
        throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidInputBinding, $"ABSTRACT_FUNCTION_INPUT_BINDING_SOURCE_UNSUPPORTED: {binding.BindingSource}");
    }

    public void StoreResult(string? key, object? value)
    {
        if (!string.IsNullOrWhiteSpace(key)) _resultContext[key] = value;
    }

    public void MarkExecuted(string primitiveKey) => _executedPrimitiveKeys.Add(primitiveKey);

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
