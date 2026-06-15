using Microsoft.Extensions.Logging;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Environment variable implementation of ICredentialStore.
/// Treats the referenceKey as the name of the environment variable to check.
///
/// SSOT: docs/design/runtime-bundle-secret-credential-ssot.yaml
///   runtime_injection.description: runtime起動時のsecret store経由でのcredential注入。環境変数またはsecret manager API経由。
///
/// Security boundary:
///   GetAsync: checks whether the env var is set and non-empty. Returns presence status only.
///   The actual value is never returned, never logged, and never included in any result.
///
/// Failure policy:
///   This store is always "available" (env vars are always accessible).
///   When the referenced env var is not set, CredentialPresent=false is returned.
///   The caller must treat CredentialPresent=false as an explicit validation failure,
///   not silently fall back to a default credential.
/// </summary>
public sealed class EnvironmentVariableCredentialStore : ICredentialStore
{
    private readonly ILogger<EnvironmentVariableCredentialStore> _logger;

    public EnvironmentVariableCredentialStore(ILogger<EnvironmentVariableCredentialStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<CredentialStoreResult> GetAsync(string referenceKey, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceKey);

        var value = Environment.GetEnvironmentVariable(referenceKey);
        var present = !string.IsNullOrEmpty(value);

        _logger.LogDebug(
            "EnvironmentVariableCredentialStore.GetAsync: referenceKey={ReferenceKey} present={Present}",
            referenceKey, present);

        return Task.FromResult(new CredentialStoreResult(
            StoreAvailable: true,
            CredentialPresent: present,
            FailureReason: present ? null : $"Environment variable '{referenceKey}' is not set or empty."));
    }

    public Task<CredentialStoreSetResult> SetAsync(string referenceKey, string providerKind, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceKey);

        _logger.LogDebug(
            "EnvironmentVariableCredentialStore.SetAsync: referenceKey={ReferenceKey} providerKind={ProviderKind}",
            referenceKey, providerKind);

        return Task.FromResult(new CredentialStoreSetResult(
            Success: true,
            StoreAvailable: true,
            FailureReason: null));
    }
}
