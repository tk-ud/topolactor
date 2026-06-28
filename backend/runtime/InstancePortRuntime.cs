using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

public sealed record InstancePortRecord(Guid InstancePortId, string PortKind, string InstanceAuthorityKey, string ProviderKind, string RequiredByBundle, string ReferenceKey, bool Active);
public sealed record InstanceConnectionPolicy(Guid PolicyId, string InstanceAuthorityKey, string CredentialReferenceKey, int ConnectionTimeoutMs, int StatementTimeoutMs, int MaxResultBytes, IReadOnlySet<string> AllowedSchemas, IReadOnlySet<string> AllowedFunctionNames, string ResultSanitizePolicyKey, bool Active);
public sealed record InstanceOperationAuthorityBinding(Guid BindingId, string OperationBindingKey, string InstanceAuthorityKey, string FunctionKey, string FunctionSchema, string FunctionName, string AbstractFunctionKey, IReadOnlyDictionary<string, string> OutputShape, bool SecretDeny, bool Active);

public sealed class InstancePortExecutionContext
{
    public InstancePortRecord? PortRecord { get; set; }
    public InstanceConnectionPolicy? ConnectionPolicy { get; set; }
    public InstanceOperationAuthorityBinding? OperationBinding { get; set; }
    public ExternalCredentialVaultRecord? CredentialVaultRecord { get; set; }
    public string? RuntimeOnlyConnectionString { get; set; }
    public JsonElement? RequestPayload { get; init; }
}

public interface IInstancePortPolicyRepository
{
    Task<InstancePortRecord?> ResolveInstancePortRecordAsync(string portKind, Guid instancePortId, CancellationToken ct = default);
    Task<ExternalCredentialVaultRecord?> ResolveInstanceCredentialReferenceAsync(string referenceKey, CancellationToken ct = default);
    Task<InstanceConnectionPolicy?> LoadConnectionPolicyAsync(string instanceAuthorityKey, string credentialReferenceKey, CancellationToken ct = default);
    Task<InstanceOperationAuthorityBinding?> LoadOperationAuthorityBindingAsync(string instanceAuthorityKey, string operationBindingKey, CancellationToken ct = default);
}

public interface IInstanceCredentialMaterializer
{
    string MaterializeConnectionString(ExternalCredentialVaultRecord credentialVaultRecord);
}

public sealed class VaultReferenceInstanceCredentialMaterializer : IInstanceCredentialMaterializer
{
    public string MaterializeConnectionString(ExternalCredentialVaultRecord credentialVaultRecord)
    {
        if (credentialVaultRecord.EncryptedPayload is null || credentialVaultRecord.EncryptedPayload.Length == 0 || string.IsNullOrWhiteSpace(credentialVaultRecord.EncryptionKeyReference))
            throw new InvalidOperationException("INSTANCE_CREDENTIAL_REFERENCE_MISSING");
        // Runtime-only materialization boundary. Production deployments plug real decryption behind the guarded vault;
        // seed/projection/log surfaces only carry reference_key and encrypted payload metadata.
        return Convert.ToBase64String(credentialVaultRecord.EncryptedPayload);
    }
}

public sealed class InstancePortDispatchRuntime : IDispatchableRuntime
{
    private readonly ILogger<InstancePortDispatchRuntime> _logger;
    private readonly IInstancePortPolicyRepository _repository;
    private readonly IInstanceCredentialMaterializer _credentialMaterializer;
    private readonly AbstractFunctionExecutor _abstractFunctionExecutor;

    public InstancePortDispatchRuntime(ILogger<InstancePortDispatchRuntime> logger, IInstancePortPolicyRepository repository, IInstanceCredentialMaterializer credentialMaterializer, AbstractFunctionExecutor abstractFunctionExecutor)
    {
        _logger = logger;
        _repository = repository;
        _credentialMaterializer = credentialMaterializer;
        _abstractFunctionExecutor = abstractFunctionExecutor;
    }

    public async Task<EndpointResponseDto> ExecuteAsync(EndpointRequestDto request, Guid? manifestId, CancellationToken ct = default)
    {
        try
        {
            var rawRef = ReadStringProperty(request.Payload, "instanceTargetRef") ?? ReadStringProperty(request.Payload, "instance_target_ref") ?? ReadStringProperty(request.Payload, "target_ref");
            if (!TryParseInstanceTargetRef(rawRef, out var portKind, out var portId, out var operationBindingKey))
                return Fail("INSTANCE_PORT_TARGET_REF_INVALID", "instanceTargetRef must use instance-port:<db_instance_port|runtime_instance_port>:<uuid>:<operationBindingKey>.");

            var port = await _repository.ResolveInstancePortRecordAsync(portKind, portId, ct);
            if (port is null || !port.Active)
                return Fail("INSTANCE_PORT_RECORD_MISSING", "No active instance port record matched the dispatch target.");
            if (!string.Equals(port.PortKind, portKind, StringComparison.Ordinal) || port.InstancePortId != portId || string.IsNullOrWhiteSpace(port.ReferenceKey))
                return Fail("INSTANCE_PORT_RECORD_MISSING", "Resolved instance port record does not match target or credential reference.");
            if (ProviderSelectorAttempted(request.Payload))
                return Fail("INSTANCE_PROVIDER_SELECTOR_FORBIDDEN", "provider_kind and required_by_bundle are data labels and cannot select runtime handlers.");

            var credential = await _repository.ResolveInstanceCredentialReferenceAsync(port.ReferenceKey, ct);
            if (credential is null)
                return Fail("INSTANCE_CREDENTIAL_REFERENCE_MISSING", "Instance credential reference could not be resolved.");

            var policy = await _repository.LoadConnectionPolicyAsync(port.InstanceAuthorityKey, port.ReferenceKey, ct);
            if (policy is null || !policy.Active)
                return Fail("INSTANCE_CONNECTION_POLICY_MISSING", "No active instance connection policy matched the port authority.");

            var binding = await _repository.LoadOperationAuthorityBindingAsync(port.InstanceAuthorityKey, operationBindingKey, ct);
            if (binding is null || !binding.Active)
                return Fail("INSTANCE_OPERATION_AUTHORITY_MISSING", "No active instance operation authority binding matched the target.");

            var instanceContext = new InstancePortExecutionContext { PortRecord = port, CredentialVaultRecord = credential, ConnectionPolicy = policy, OperationBinding = binding, RuntimeOnlyConnectionString = _credentialMaterializer.MaterializeConnectionString(credential), RequestPayload = ReadObjectProperty(request.Payload, "dispatch_payload") };
            var result = await _abstractFunctionExecutor.ExecuteAsync(binding.AbstractFunctionKey, new AbstractFunctionExecutionContext(binding.FunctionKey, ReadObjectProperty(request.Payload, "dispatch_payload"), requiredRuntimeLane: "instance_port_runtime", instancePortContext: instanceContext), ct);
            var data = JsonSerializer.SerializeToElement(new { instancePortDispatch = new { status = "executed", instanceTargetRef = rawRef, portKind, instancePortId = portId, port.InstanceAuthorityKey, binding.OperationBindingKey, binding.FunctionKey, result = result.ResultContext } });
            return new EndpointResponseDto(true, new Emission(null, null, null, [], data, []), []);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or AbstractFunctionFailCloseException or TimeoutException)
        {
            _logger.LogWarning(ex, "Instance port dispatch failed closed.");
            return Fail(ex is AbstractFunctionFailCloseException af ? af.Message : ex.Message, "Instance port dispatch failed closed.");
        }
    }

    internal static bool TryParseInstanceTargetRef(string? rawRef, out string portKind, out Guid portId, out string operationBindingKey)
    {
        portKind = string.Empty; operationBindingKey = string.Empty; portId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(rawRef)) return false;
        var parts = rawRef.Split(':', StringSplitOptions.None);
        if (parts.Length != 4 || parts[0] != "instance-port") return false;
        if (parts[1] is not ("db_instance_port" or "runtime_instance_port")) return false;
        if (!Guid.TryParse(parts[2], out portId)) return false;
        if (string.IsNullOrWhiteSpace(parts[3])) return false;
        portKind = parts[1]; operationBindingKey = parts[3]; return true;
    }

    private static bool ProviderSelectorAttempted(JsonElement? payload) => ReadStringProperty(payload, "provider_kind_selector") is not null || ReadStringProperty(payload, "required_by_bundle_selector") is not null;
    private static EndpointResponseDto Fail(string code, string message) => new(false, null, [new ValidationError(code, message)]);
    private static string? ReadStringProperty(JsonElement? payload, string name) => payload is { ValueKind: JsonValueKind.Object } e && e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    private static JsonElement? ReadObjectProperty(JsonElement? payload, string name) => payload is { ValueKind: JsonValueKind.Object } e && e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Object ? p.Clone() : null;
}

public sealed class CallInstancePostgresFunctionPrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
{
    private static readonly Regex SafeIdentifier = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    public string PrimitiveKey => "call_instance_postgres_function";

    public async Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
    {
        var instance = context.InstancePortContext ?? throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingAuthority, "INSTANCE_PORT_RECORD_MISSING");
        var policy = instance.ConnectionPolicy ?? throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingAuthority, "INSTANCE_CONNECTION_POLICY_MISSING");
        var binding = instance.OperationBinding ?? throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingAuthority, "INSTANCE_OPERATION_AUTHORITY_MISSING");
        VerifyInstanceConnectionPolicyAsync(policy, binding);
        VerifyInstanceOperationAuthorityBindingAsync(context, binding);
        var functionSchema = RequireConfig(step, "function_schema", binding.FunctionSchema);
        var functionName = RequireConfig(step, "function_name", binding.FunctionName);
        if (!string.Equals(functionSchema, binding.FunctionSchema, StringComparison.Ordinal) || !string.Equals(functionName, binding.FunctionName, StringComparison.Ordinal)) throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "INSTANCE_FUNCTION_AUTHORITY_MISSING");
        if (!SafeIdentifier.IsMatch(functionSchema) || !SafeIdentifier.IsMatch(functionName)) throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "INSTANCE_FUNCTION_NOT_ALLOWLISTED");
        using var timeout = new CancellationTokenSource(policy.StatementTimeoutMs);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        try
        {
            var args = ReadArgumentKeys(step);
            await using var conn = new NpgsqlConnection(instance.RuntimeOnlyConnectionString);
            await conn.OpenAsync(linked.Token);
            await using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = Math.Max(1, policy.StatementTimeoutMs / 1000);
            cmd.CommandText = $"SELECT {functionSchema}.{functionName}({string.Join(", ", args.Select((_, i) => $"@p{i}"))})";
            for (var i = 0; i < args.Count; i++) { if (!inputs.TryGetValue(args[i], out var value)) throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, $"INSTANCE_FUNCTION_ARGUMENT_MISSING: {args[i]}"); cmd.Parameters.AddWithValue($"p{i}", value ?? DBNull.Value); }
            return SanitizeInstanceFunctionResultAsync(await cmd.ExecuteScalarAsync(linked.Token), policy, binding);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested) { throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "INSTANCE_TIMEOUT_EXCEEDED"); }
    }

    internal static void VerifyInstanceConnectionPolicyAsync(InstanceConnectionPolicy policy, InstanceOperationAuthorityBinding binding)
    {
        if (!policy.AllowedSchemas.Contains(binding.FunctionSchema)) throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "INSTANCE_FUNCTION_SCHEMA_NOT_ALLOWLISTED");
        if (!policy.AllowedFunctionNames.Contains(binding.FunctionName)) throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "INSTANCE_FUNCTION_NOT_ALLOWLISTED");
        if (policy.StatementTimeoutMs <= 0 || policy.ConnectionTimeoutMs <= 0 || policy.MaxResultBytes <= 0) throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "INSTANCE_CONNECTION_POLICY_MISSING");
    }

    internal static void VerifyInstanceOperationAuthorityBindingAsync(AbstractFunctionExecutionContext context, InstanceOperationAuthorityBinding binding)
    {
        if (binding.OutputShape.Count == 0) throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidProjection, "INSTANCE_OPERATION_AUTHORITY_MISSING");
        if (!context.AuthorityBindings.Any(b => b.Active && b.AuthorityKind == "instance" && b.AuthorityRef == binding.InstanceAuthorityKey) || !context.AuthorityBindings.Any(b => b.Active && b.AuthorityKind == "instance_function" && b.AuthorityRef == binding.FunctionKey)) throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.InvalidAuthority, "INSTANCE_FUNCTION_AUTHORITY_MISSING");
    }

    internal static object? SanitizeInstanceFunctionResultAsync(object? result, InstanceConnectionPolicy policy, InstanceOperationAuthorityBinding binding)
    {
        if (!binding.SecretDeny) throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.SecretProjectionDenied, "INSTANCE_RESULT_SECRET_DENIED");
        var text = result?.ToString() ?? string.Empty;
        if (text.Length > policy.MaxResultBytes || Regex.IsMatch(text, "(password|secret|token|connection_string|endpoint|private_key)", RegexOptions.IgnoreCase)) throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.SecretProjectionDenied, "INSTANCE_RESULT_SECRET_DENIED");
        return result is DBNull ? null : result;
    }

    private static string RequireConfig(AbstractFunctionStep step, string key, string fallback) => step.StepConfig.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;
    private static IReadOnlyList<string> ReadArgumentKeys(AbstractFunctionStep step) { if (!step.StepConfig.TryGetValue("arguments", out var json) || string.IsNullOrWhiteSpace(json)) return step.InputBindings.Select(b => b.InputKey).ToArray(); using var doc = JsonDocument.Parse(json); return doc.RootElement.EnumerateArray().Select(i => i.GetString() ?? string.Empty).Where(s => s.Length > 0).ToArray(); }
}

public sealed class CallBoundInstanceFunctionPrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
{
    public string PrimitiveKey => "call_bound_instance_function";
    public Task<object?> ExecuteAsync(AbstractFunctionStep step, IReadOnlyDictionary<string, object?> inputs, AbstractFunctionExecutionContext context, CancellationToken ct = default)
    {
        var binding = context.InstancePortContext?.OperationBinding ?? throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingAuthority, "INSTANCE_OPERATION_AUTHORITY_MISSING");
        CallInstancePostgresFunctionPrimitiveAdapter.VerifyInstanceOperationAuthorityBindingAsync(context, binding);
        if (!binding.SecretDeny) throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.SecretProjectionDenied, "INSTANCE_RESULT_SECRET_DENIED");
        return Task.FromResult<object?>(inputs.ToDictionary(kvp => kvp.Key, kvp => CallInstancePostgresFunctionPrimitiveAdapter.SanitizeInstanceFunctionResultAsync(kvp.Value, context.InstancePortContext!.ConnectionPolicy!, binding), StringComparer.Ordinal));
    }
}
