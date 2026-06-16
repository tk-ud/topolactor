using Topolactor.Runtime;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class ExternalPortCredentialRefresherTests
{
    [Fact]
    public void ShouldRefresh_UsesExpiresAtAndRefreshBeforeBoundary()
    {
        var now = DateTimeOffset.Parse("2026-06-16T00:00:00Z");
        var record = NewRecord(expiresAt: now.AddSeconds(120), refreshBeforeSeconds: 300);

        Assert.True(ExternalTokenRefresher.ShouldRefresh(record, now));
    }

    [Fact]
    public void FailCloseOnMissingOrInvalidCredential_RejectsMissingEncryptedPayload()
    {
        var record = NewRecord().WithEncryptedPayload(null);

        var error = Assert.Throws<InvalidOperationException>(() =>
            ExternalTokenRefresher.FailCloseOnMissingOrInvalidCredential(record));
        Assert.Equal("EXTERNAL_CREDENTIAL_INVALID", error.Message);
    }

    [Fact]
    public void GenericRefresherSurface_ContainsLeaseVersionAndExpiresAtBoundaries()
    {
        var lease = typeof(ExternalCredentialRefreshLease).GetProperties().Select(p => p.Name).ToHashSet();
        var record = typeof(ExternalCredentialVaultRecord).GetProperties().Select(p => p.Name).ToHashSet();

        Assert.Contains("Version", lease);
        Assert.Contains("LockedUntil", lease);
        Assert.Contains("Version", record);
        Assert.Contains("ExpiresAt", record);
        Assert.Contains("RefreshBeforeSeconds", record);
    }

    private static ExternalCredentialVaultRecord NewRecord(
        DateTimeOffset? expiresAt = null,
        int refreshBeforeSeconds = 300) =>
        new(
            CredentialVaultId: Guid.NewGuid(),
            ProviderKind: "generic-oauth",
            RequiredByBundle: "external-port-substrate-db-credential-vault-refresher",
            TokenKind: "oauth_refresh_token",
            TokenHash: "sha256:hash-only-test-value",
            EncryptedPayload: new byte[] { 1, 2, 3 },
            EncryptionKeyReference: "topolactor-db-guarded-key-ref",
            ExpiresAt: expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(10),
            RefreshBeforeSeconds: refreshBeforeSeconds,
            Version: 1,
            LockedUntil: null,
            Active: true);
}

internal static class ExternalCredentialVaultRecordTestExtensions
{
    public static ExternalCredentialVaultRecord WithEncryptedPayload(this ExternalCredentialVaultRecord record, byte[]? payload) =>
        record with { EncryptedPayload = payload };
}
