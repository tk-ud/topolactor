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
