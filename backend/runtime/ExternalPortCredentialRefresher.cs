using System.Text.Json;

using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// External port credential vault metadata. This is a DB guarded attachment for
/// external_port_substrate port records, not an auth credential and not a
/// standalone credential-management plane.
/// </summary>
public sealed record ExternalCredentialVaultRecord(
    Guid CredentialVaultId,
    string ProviderKind,
    string RequiredByBundle,
    string TokenKind,
    string? TokenHash,
    byte[]? EncryptedPayload,
    string? EncryptionKeyReference,
    DateTimeOffset? ExpiresAt,
    int RefreshBeforeSeconds,
    int Version,
    DateTimeOffset? LockedUntil,
    bool Active);

public sealed record ExternalCredentialRefreshLease(
    Guid CredentialRefreshAttemptId,
    Guid CredentialVaultId,
    string LeaseOwner,
    DateTimeOffset LockedUntil,
    int Version);

public sealed record ExternalTokenRefreshRequest(
    Guid CredentialVaultId,
    string ProviderKind,
    string TokenKind,
    IReadOnlyDictionary<string, string> RequestConfig,
    string RuntimeSecretPayload,
    DateTimeOffset? CurrentExpiresAt,
    int Version);

public sealed record ExternalTokenRefreshResult(
    string EncryptedPayloadSource,
    string TokenHash,
    DateTimeOffset ExpiresAt,
    bool PayloadRotated);

public sealed record ExternalPortHttpRequest(
    Uri Endpoint,
    HttpMethod Method,
    IReadOnlyDictionary<string, string> Headers,
    string? Body);

public sealed record ExternalPortHttpResponse(
    int StatusCode,
    string Body);

public interface IExternalCredentialVaultRepository
{
    Task<ExternalCredentialVaultRecord?> LoadAsync(Guid credentialVaultId, CancellationToken ct = default);

    Task<ExternalCredentialVaultRecord?> LoadByProviderAndBundleAsync(string providerKind, string requiredByBundle, CancellationToken ct = default);

    Task<ExternalCredentialVaultRecord?> LoadByReferenceKeyAsync(string referenceKey, CancellationToken ct = default);

    Task<ExternalCredentialRefreshLease?> AcquireRefreshLeaseAsync(
        Guid credentialVaultId,
        string leaseOwner,
        TimeSpan leaseDuration,
        DateTimeOffset now,
        CancellationToken ct = default);

    Task WriteEncryptedCredentialPayloadAsync(
        Guid credentialVaultId,
        int expectedVersion,
        byte[] encryptedPayload,
        string tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken ct = default);

    Task ReleaseRefreshLeaseAsync(ExternalCredentialRefreshLease lease, CancellationToken ct = default);

    Task FailRefreshLeaseAsync(ExternalCredentialRefreshLease lease, string failureCode, CancellationToken ct = default);
}

public interface IExternalCredentialCrypto
{
    string DecryptForRuntimeUse(byte[] encryptedPayload, string encryptionKeyReference);

    byte[] EncryptForVaultStorage(string plaintextPayload, string encryptionKeyReference);

    string ComputeTokenHash(string plaintextPayload);
}

public interface IExternalPortHttpClient
{
    Task<ExternalPortHttpResponse> SendAsync(ExternalPortHttpRequest request, CancellationToken ct = default);
}

public interface IExternalPortPolicyStepExecutor
{
    Task ExecuteAsync(ExternalPortPolicyStep step, ExternalPortExecutionContext context, CancellationToken ct = default);

    Task ExecutePolicyAsync(ExternalPortPolicy policy, ExternalPortExecutionContext context, CancellationToken ct = default);

    ExternalPortHttpRequest BuildTokenRefreshRequest(ExternalTokenRefreshRequest request);

    ExternalTokenRefreshResult ParseTokenRefreshResult(ExternalTokenRefreshRequest request, ExternalPortHttpResponse response);
}

public interface IExternalTokenRefresher
{
    Task<ExternalCredentialVaultRecord> RefreshIfNeededAsync(
        Guid credentialVaultId,
        string leaseOwner,
        IReadOnlyDictionary<string, string> requestConfig,
        DateTimeOffset now,
        CancellationToken ct = default);
}

/// <summary>
/// Generic token refresher primitive for external port credentials. It uses
/// provider_kind as data passed through records/config; it must not branch into
/// provider-specific runtime handlers.
/// </summary>
public sealed class ExternalTokenRefresher : IExternalTokenRefresher
{
    private readonly IExternalCredentialVaultRepository _repository;
    private readonly IExternalCredentialCrypto _crypto;
    private readonly IExternalPortHttpClient _httpClient;
    private readonly IExternalPortPolicyStepExecutor _policyStepExecutor;

    public ExternalTokenRefresher(
        IExternalCredentialVaultRepository repository,
        IExternalCredentialCrypto crypto,
        IExternalPortHttpClient httpClient,
        IExternalPortPolicyStepExecutor policyStepExecutor)
    {
        _repository = repository;
        _crypto = crypto;
        _httpClient = httpClient;
        _policyStepExecutor = policyStepExecutor;
    }

    public async Task<ExternalCredentialVaultRecord> RefreshIfNeededAsync(
        Guid credentialVaultId,
        string leaseOwner,
        IReadOnlyDictionary<string, string> requestConfig,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var record = await _repository.LoadAsync(credentialVaultId, ct)
            ?? throw new InvalidOperationException("EXTERNAL_CREDENTIAL_MISSING");

        FailCloseOnMissingOrInvalidCredential(record);
        if (!ShouldRefresh(record, now))
        {
            return record;
        }

        var lease = await _repository.AcquireRefreshLeaseAsync(credentialVaultId, leaseOwner, TimeSpan.FromMinutes(5), now, ct)
            ?? throw new InvalidOperationException("EXTERNAL_CREDENTIAL_REFRESH_LEASE_UNAVAILABLE");

        try
        {
            var plaintext = _crypto.DecryptForRuntimeUse(record.EncryptedPayload!, record.EncryptionKeyReference!);
            var request = new ExternalTokenRefreshRequest(
                record.CredentialVaultId,
                record.ProviderKind,
                record.TokenKind,
                requestConfig,
                plaintext,
                record.ExpiresAt,
                record.Version);
            var httpRequest = _policyStepExecutor.BuildTokenRefreshRequest(request);
            var response = await _httpClient.SendAsync(httpRequest, ct);
            var result = _policyStepExecutor.ParseTokenRefreshResult(request, response);
            var encrypted = _crypto.EncryptForVaultStorage(result.EncryptedPayloadSource, record.EncryptionKeyReference!);

            await _repository.WriteEncryptedCredentialPayloadAsync(
                credentialVaultId,
                lease.Version,
                encrypted,
                result.TokenHash,
                result.ExpiresAt,
                ct);
            await _repository.ReleaseRefreshLeaseAsync(lease, ct);

            return await _repository.LoadAsync(credentialVaultId, ct)
                ?? throw new InvalidOperationException("EXTERNAL_CREDENTIAL_MISSING_AFTER_REFRESH");
        }
        catch
        {
            await _repository.FailRefreshLeaseAsync(lease, "EXTERNAL_CREDENTIAL_REFRESH_FAILED", ct);
            throw;
        }
    }

    public static bool ShouldRefresh(ExternalCredentialVaultRecord record, DateTimeOffset now) =>
        record.ExpiresAt.HasValue &&
        record.ExpiresAt.Value <= now.AddSeconds(record.RefreshBeforeSeconds);

    public static void FailCloseOnMissingOrInvalidCredential(ExternalCredentialVaultRecord record)
    {
        if (!record.Active || record.EncryptedPayload is null || string.IsNullOrWhiteSpace(record.EncryptionKeyReference))
        {
            throw new InvalidOperationException("EXTERNAL_CREDENTIAL_INVALID");
        }
    }
}

public enum ExternalPortKind
{
    AccessPort,
    ResponsePort,
    HookPort
}

public sealed record ExternalPortRecord(
    Guid PortId,
    string PortKind,
    string RequiredByBundle,
    string ProviderKind,
    string? UrlOrEnvReference,
    string? HookPath,
    string? HeaderKey,
    string? RouteKey,
    string CredentialKind,
    string? ReferenceKey,
    bool Active);

public sealed record ExternalPortPolicy(
    Guid PolicyId,
    string PolicyKey,
    string PortKind,
    string RequiredByBundle,
    IReadOnlyList<ExternalPortPolicyStep> PolicySteps,
    bool Active);

public sealed record ExternalPortPolicyStep(
    Guid PolicyStepId,
    Guid PolicyId,
    int StepOrder,
    string OperationKey,
    IReadOnlyDictionary<string, string> StepConfig,
    bool Active);

public sealed class ExternalPortExecutionContext
{
    private readonly List<string> _executedOperationKeys = new();

    public string? RequiredByBundle { get; set; }

    public string? PortKind { get; set; }

    public string? RouteKey { get; set; }

    public ExternalPortRecord? PortRecord { get; set; }

    public ExternalPortPolicy? Policy { get; set; }

    public ExternalCredentialVaultRecord? CredentialVaultRecord { get; set; }

    public ExternalPortHttpRequest? HttpRequest { get; set; }

    public ExternalPortHttpResponse? HttpResponse { get; set; }

    public JsonElement? RequestPayload { get; set; }

    public string? OutputProp { get; set; }

    public IReadOnlyDictionary<string, string> SignatureConfig { get; set; } = new Dictionary<string, string>();

    public IReadOnlyDictionary<string, string> SignatureInput { get; set; } = new Dictionary<string, string>();

    public bool SchedulerEventEnqueued { get; set; }

    public string? DecryptedCredentialPayload { get; set; }

    public Guid? ExportJobId { get; set; }

    public string? ChecksumValue { get; set; }

    public Guid? FileArtifactId { get; set; }

    public string? AuthorizationKey { get; set; }

    public IReadOnlyList<string> ExecutedOperationKeys => _executedOperationKeys;

    public void MarkExecuted(string operationKey) => _executedOperationKeys.Add(operationKey);
}

public interface IExternalPortResolver
{
    Task<ExternalPortRecord> ResolveAsync(
        string requiredByBundle,
        string portKind,
        string? routeKey = null,
        CancellationToken ct = default);
}

public interface IExternalPortPolicyRepository
{
    Task<ExternalPortRecord?> LoadPortRecordAsync(
        string requiredByBundle,
        string portKind,
        string? routeKey,
        CancellationToken ct = default);

    Task<ExternalPortRecord?> LoadPortRecordByIdAsync(
        string portKind,
        Guid portId,
        string? routeKey,
        CancellationToken ct = default);

    Task<ExternalPortRecord?> LoadPortRecordByCanonicalBindingAsync(
        string manifestKey,
        string tableRef,
        string portKind,
        Guid portId,
        string? routeKey,
        CancellationToken ct = default);

    Task<ExternalPortPolicy?> LoadPolicyAsync(ExternalPortRecord portRecord, CancellationToken ct = default);
}

public interface IExternalPortCredentialReferenceResolver
{
    Task<ExternalCredentialVaultRecord?> ResolveCredentialReferenceAsync(ExternalPortRecord portRecord, CancellationToken ct = default);
}

public interface IFileStorageRepository
{
    Task<Guid> RecordExportJobAsync(RecordExportJobCommand command, CancellationToken ct = default);
    Task UpdateExportJobStatusAsync(Guid exportJobId, string status, string? failureCode = null, CancellationToken ct = default);
    Task<FileChecksumRecord> RecordChecksumAsync(RecordChecksumCommand command, CancellationToken ct = default);
    Task<FileArtifactRecord> RecordFileArtifactAsync(RecordFileArtifactCommand command, CancellationToken ct = default);
    Task<ExportManifestRecord> WriteManifestRecordAsync(WriteManifestCommand command, CancellationToken ct = default);
    Task<SignedDownloadAuthorizationRecord> AuthorizeSignedDownloadAsync(AuthorizeSignedDownloadCommand command, CancellationToken ct = default);
}

/// <summary>
/// Generic abstract function boundary for DB-driven domain mutations via execute_db_function
/// operation_key. Implementations call named PostgreSQL functions (e.g. topology.fs_*) using
/// context-derived parameters. This is the data-driven path for all consumer bundle domain
/// mutations that are not hard-runtime compute operations.
/// </summary>
public interface IExternalPortDbFunctionRepository
{
    Task ExecuteAsync(
        string functionName,
        IReadOnlyDictionary<string, string> stepConfig,
        ExternalPortExecutionContext context,
        CancellationToken ct = default);
}

/// <summary>
/// Reusable extension point for consumer bundle-specific operation_key handlers.
/// Each consumer bundle (file_storage, email, audit_approval, export_sftp, etc.) registers
/// its own implementation. The generic ExternalPortPolicyStepExecutor stays free of
/// bundle-specific dependencies.
/// </summary>
public interface IExternalPortBundleStepHandler
{
    IReadOnlySet<string> SupportedOperationKeys { get; }

    Task ExecuteAsync(ExternalPortPolicyStep step, ExternalPortExecutionContext context, CancellationToken ct = default);
}

public sealed class ExternalPortResolver : IExternalPortResolver
{
    private readonly IExternalPortPolicyRepository _repository;

    public ExternalPortResolver(IExternalPortPolicyRepository repository) => _repository = repository;

    public async Task<ExternalPortRecord> ResolveAsync(
        string requiredByBundle,
        string portKind,
        string? routeKey = null,
        CancellationToken ct = default)
    {
        var record = await _repository.LoadPortRecordAsync(requiredByBundle, portKind, routeKey, ct)
            ?? throw new InvalidOperationException("EXTERNAL_PORT_RECORD_MISSING");
        FailCloseOnInvalidPortRecord(record);
        return record;
    }

    public static void FailCloseOnInvalidPortRecord(ExternalPortRecord record)
    {
        if (!record.Active || string.IsNullOrWhiteSpace(record.RequiredByBundle) ||
            string.IsNullOrWhiteSpace(record.ProviderKind) || string.IsNullOrWhiteSpace(record.CredentialKind))
        {
            throw new InvalidOperationException("EXTERNAL_PORT_RECORD_INVALID");
        }
    }
}

public sealed class ExternalPortPolicyStepExecutor : IExternalPortPolicyStepExecutor
{
    public static readonly IReadOnlySet<string> AllowedOperationKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "resolve_port_record",
        "resolve_credential_reference",
        "load_encrypted_credential_payload",
        "decrypt_for_runtime_use",
        "build_http_request",
        "inject_authorization_header",
        "send_http",
        "capture_response",
        "execute_db_function",
        "verify_signature_by_config",
        "enqueue_scheduler_event",
        "append_runtime_event_log",
        "fail_close",
        "acquire_refresh_lease",
        "request_token_by_config",
        "write_encrypted_credential_payload",
        "update_token_hash",
        "update_expires_at_and_version",
        "release_refresh_lease"
    };

    private readonly IReadOnlyDictionary<string, Func<ExternalPortPolicyStep, ExternalPortExecutionContext, CancellationToken, Task>> _registry;
    private readonly IReadOnlyList<IExternalPortBundleStepHandler> _bundleHandlers;

    public ExternalPortPolicyStepExecutor(
        IExternalPortHttpClient? httpClient = null,
        IExternalPortCredentialReferenceResolver? credentialReferenceResolver = null,
        IExternalPortResolver? portResolver = null,
        IExternalCredentialCrypto? crypto = null,
        IExternalPortDbFunctionRepository? dbFunctionRepository = null,
        IEnumerable<IExternalPortBundleStepHandler>? bundleHandlers = null)
    {
        _bundleHandlers = bundleHandlers?.ToList() ?? new List<IExternalPortBundleStepHandler>();
        _registry = new Dictionary<string, Func<ExternalPortPolicyStep, ExternalPortExecutionContext, CancellationToken, Task>>(StringComparer.Ordinal)
        {
            ["resolve_port_record"] = async (step, context, ct) =>
            {
                if (context.PortRecord is null)
                {
                    if (portResolver is null)
                    {
                        throw new InvalidOperationException("EXTERNAL_PORT_RESOLVER_MISSING");
                    }

                    var requiredByBundle = FirstNonBlank(context.RequiredByBundle, context.Policy?.RequiredByBundle);
                    var portKind = FirstNonBlank(context.PortKind, context.Policy?.PortKind);
                    if (requiredByBundle is null || portKind is null)
                    {
                        throw new InvalidOperationException("EXTERNAL_PORT_RESOLUTION_INPUT_MISSING");
                    }

                    context.PortRecord = await portResolver.ResolveAsync(requiredByBundle, portKind, context.RouteKey, ct);
                }

                context.MarkExecuted(step.OperationKey);
            },
            ["resolve_credential_reference"] = async (step, context, ct) =>
            {
                if (context.PortRecord is null)
                {
                    throw new InvalidOperationException("EXTERNAL_PORT_RECORD_MISSING");
                }

                if (context.PortRecord.CredentialKind == "none")
                {
                    context.MarkExecuted(step.OperationKey);
                    await Task.CompletedTask;
                    return;
                }

                if (credentialReferenceResolver is null || string.IsNullOrWhiteSpace(context.PortRecord.ReferenceKey))
                {
                    throw new InvalidOperationException("EXTERNAL_CREDENTIAL_REFERENCE_MISSING");
                }

                context.CredentialVaultRecord = await credentialReferenceResolver.ResolveCredentialReferenceAsync(context.PortRecord, ct)
                    ?? throw new InvalidOperationException("EXTERNAL_CREDENTIAL_REFERENCE_MISSING");
                context.MarkExecuted(step.OperationKey);
            },
            ["build_http_request"] = (step, context, ct) =>
            {
                if (!step.StepConfig.TryGetValue("endpoint", out var endpoint) || string.IsNullOrWhiteSpace(endpoint))
                {
                    endpoint = context.PortRecord?.UrlOrEnvReference;
                }

                if (string.IsNullOrWhiteSpace(endpoint))
                {
                    throw new InvalidOperationException("EXTERNAL_HTTP_ENDPOINT_MISSING");
                }

                var method = step.StepConfig.TryGetValue("method", out var methodValue) ? new HttpMethod(methodValue) : HttpMethod.Get;
                context.HttpRequest = new ExternalPortHttpRequest(new Uri(endpoint, UriKind.RelativeOrAbsolute), method, new Dictionary<string, string>(), null);
                context.MarkExecuted(step.OperationKey);
                return Task.CompletedTask;
            },
            ["send_http"] = async (step, context, ct) =>
            {
                if (httpClient is null || context.HttpRequest is null)
                {
                    throw new InvalidOperationException("EXTERNAL_HTTP_REQUEST_MISSING");
                }

                context.HttpResponse = await httpClient.SendAsync(context.HttpRequest, ct);
                context.MarkExecuted(step.OperationKey);
            },
            ["verify_signature_by_config"] = (step, context, ct) =>
            {
                var expected = FirstNonBlank(
                    ReadConfig(step.StepConfig, "expected_signature"),
                    ReadConfig(context.SignatureConfig, "expected_signature"));
                var actual = FirstNonBlank(
                    ReadConfig(context.SignatureInput, "signature"),
                    ReadConfig(step.StepConfig, "signature"));

                if (expected is null || actual is null)
                {
                    throw new InvalidOperationException("EXTERNAL_SIGNATURE_CONFIG_MISSING");
                }

                if (!string.Equals(expected, actual, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("EXTERNAL_SIGNATURE_VERIFICATION_FAILED");
                }

                context.MarkExecuted(step.OperationKey);
                return Task.CompletedTask;
            },
            ["enqueue_scheduler_event"] = (step, context, ct) =>
            {
                context.SchedulerEventEnqueued = true;
                context.MarkExecuted(step.OperationKey);
                return Task.CompletedTask;
            },
            ["append_runtime_event_log"] = MarkOnly,
            ["capture_response"] = (step, context, ct) =>
            {
                if (context.HttpResponse is not null)
                    context.OutputProp = context.HttpResponse.Body;
                context.MarkExecuted(step.OperationKey);
                return Task.CompletedTask;
            },
            ["execute_db_function"] = async (step, context, ct) =>
            {
                if (dbFunctionRepository is null)
                    throw new InvalidOperationException("EXTERNAL_PORT_DB_FUNCTION_REPOSITORY_MISSING");
                if (!step.StepConfig.TryGetValue("function", out var functionName) || string.IsNullOrWhiteSpace(functionName))
                    throw new InvalidOperationException("EXTERNAL_PORT_DB_FUNCTION_NAME_MISSING");
                await dbFunctionRepository.ExecuteAsync(functionName, step.StepConfig, context, ct);
                context.MarkExecuted(step.OperationKey);
            },
            ["fail_close"] = (step, context, ct) => throw new InvalidOperationException("EXTERNAL_PORT_POLICY_FAIL_CLOSE"),
            ["load_encrypted_credential_payload"] = (step, context, ct) =>
            {
                if (context.CredentialVaultRecord?.EncryptedPayload is null)
                    throw new InvalidOperationException("EXTERNAL_CREDENTIAL_PAYLOAD_MISSING");
                context.MarkExecuted(step.OperationKey);
                return Task.CompletedTask;
            },
            ["decrypt_for_runtime_use"] = (step, context, ct) =>
            {
                if (crypto is null)
                    throw new InvalidOperationException("EXTERNAL_CREDENTIAL_CRYPTO_MISSING");
                if (context.CredentialVaultRecord is null)
                    throw new InvalidOperationException("EXTERNAL_CREDENTIAL_VAULT_RECORD_MISSING");
                ExternalTokenRefresher.FailCloseOnMissingOrInvalidCredential(context.CredentialVaultRecord);
                context.DecryptedCredentialPayload = crypto.DecryptForRuntimeUse(
                    context.CredentialVaultRecord.EncryptedPayload!,
                    context.CredentialVaultRecord.EncryptionKeyReference!);
                context.MarkExecuted(step.OperationKey);
                return Task.CompletedTask;
            },
            ["inject_authorization_header"] = (step, context, ct) =>
            {
                if (context.DecryptedCredentialPayload is null)
                    throw new InvalidOperationException("EXTERNAL_CREDENTIAL_DECRYPTED_PAYLOAD_MISSING");
                if (context.HttpRequest is null)
                    throw new InvalidOperationException("EXTERNAL_HTTP_REQUEST_MISSING");
                var headerKey = FirstNonBlank(
                    ReadConfig(step.StepConfig, "header_key"),
                    context.PortRecord?.HeaderKey,
                    "Authorization");
                var headers = new Dictionary<string, string>(context.HttpRequest.Headers, StringComparer.OrdinalIgnoreCase)
                {
                    [headerKey!] = context.DecryptedCredentialPayload
                };
                context.HttpRequest = context.HttpRequest with { Headers = headers };
                context.MarkExecuted(step.OperationKey);
                return Task.CompletedTask;
            },
        };
    }

    public async Task ExecutePolicyAsync(ExternalPortPolicy policy, ExternalPortExecutionContext context, CancellationToken ct = default)
    {
        context.Policy = policy;
        context.RequiredByBundle ??= policy.RequiredByBundle;
        context.PortKind ??= policy.PortKind;

        foreach (var step in policy.PolicySteps.Where(static s => s.Active).OrderBy(static s => s.StepOrder))
        {
            await ExecuteAsync(step, context, ct);
        }
    }

    public Task ExecuteAsync(ExternalPortPolicyStep step, ExternalPortExecutionContext context, CancellationToken ct = default)
    {
        if (_registry.TryGetValue(step.OperationKey, out var primitive))
            return primitive(step, context, ct);

        foreach (var handler in _bundleHandlers)
        {
            if (handler.SupportedOperationKeys.Contains(step.OperationKey))
                return handler.ExecuteAsync(step, context, ct);
        }

        throw new InvalidOperationException("EXTERNAL_PORT_POLICY_OPERATION_UNSUPPORTED");
    }

    public ExternalPortHttpRequest BuildTokenRefreshRequest(ExternalTokenRefreshRequest request)
    {
        if (!request.RequestConfig.TryGetValue("endpoint", out var endpoint) || string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("EXTERNAL_TOKEN_REFRESH_ENDPOINT_MISSING");
        }

        var method = request.RequestConfig.TryGetValue("method", out var methodValue) ? new HttpMethod(methodValue) : HttpMethod.Post;
        return new ExternalPortHttpRequest(new Uri(endpoint, UriKind.RelativeOrAbsolute), method, new Dictionary<string, string>(), request.RuntimeSecretPayload);
    }

    public ExternalTokenRefreshResult ParseTokenRefreshResult(ExternalTokenRefreshRequest request, ExternalPortHttpResponse response)
    {
        if (response.StatusCode < 200 || response.StatusCode >= 300)
        {
            throw new InvalidOperationException("EXTERNAL_TOKEN_REFRESH_HTTP_FAILED");
        }

        var hash = request.RequestConfig.TryGetValue("token_hash", out var tokenHash) ? tokenHash : $"sha256:{response.Body.Length}";
        var expiresAt = request.CurrentExpiresAt?.AddHours(1) ?? DateTimeOffset.UtcNow.AddHours(1);
        return new ExternalTokenRefreshResult(response.Body, hash, expiresAt, PayloadRotated: true);
    }

    private static Task MarkOnly(ExternalPortPolicyStep step, ExternalPortExecutionContext context, CancellationToken ct)
    {
        context.MarkExecuted(step.OperationKey);
        return Task.CompletedTask;
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));

    private static string? ReadConfig(IReadOnlyDictionary<string, string> config, string key) =>
        config.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

}
