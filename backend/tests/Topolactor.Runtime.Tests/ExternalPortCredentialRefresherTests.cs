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

public class ExternalPortSeedDrivenPolicyTests
{
    [Fact]
    public async Task ExecutePolicyAsync_RunsPolicyStepsInOrder()
    {
        var executor = new ExternalPortPolicyStepExecutor();
        var policy = new ExternalPortPolicy(
            Guid.NewGuid(),
            "test_policy",
            "access_port",
            "external-port-substrate-seed-coding",
            new[]
            {
                NewStep(2, "append_runtime_event_log"),
                NewStep(1, "resolve_port_record"),
                NewStep(3, "capture_response")
            },
            Active: true);
        var context = new ExternalPortExecutionContext();

        await executor.ExecutePolicyAsync(policy, context);

        Assert.Equal(new[] { "resolve_port_record", "append_runtime_event_log", "capture_response" }, context.ExecutedOperationKeys);
    }

    [Fact]
    public async Task ResolveAsync_MissingPortRecord_FailCloses()
    {
        var resolver = new ExternalPortResolver(new MissingPortPolicyRepository());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resolver.ResolveAsync("missing_bundle", "access_port"));

        Assert.Equal("EXTERNAL_PORT_RECORD_MISSING", error.Message);
    }

    [Fact]
    public async Task ExecuteAsync_MissingCredentialReference_FailCloses()
    {
        var executor = new ExternalPortPolicyStepExecutor();
        var context = new ExternalPortExecutionContext
        {
            PortRecord = new ExternalPortRecord(
                Guid.NewGuid(),
                "access_port",
                "external-port-substrate-seed-coding",
                "generic-provider",
                "https://example.invalid",
                null,
                null,
                null,
                "external",
                null,
                Active: true)
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(NewStep(1, "resolve_credential_reference"), context));

        Assert.Equal("EXTERNAL_CREDENTIAL_REFERENCE_MISSING", error.Message);
    }

    private static ExternalPortPolicyStep NewStep(int order, string operationKey) =>
        new(Guid.NewGuid(), Guid.NewGuid(), order, operationKey, new Dictionary<string, string>(), Active: true);

    private sealed class MissingPortPolicyRepository : IExternalPortPolicyRepository
    {
        public Task<ExternalPortRecord?> LoadPortRecordAsync(string requiredByBundle, string portKind, string? routeKey, CancellationToken ct = default) =>
            Task.FromResult<ExternalPortRecord?>(null);

        public Task<ExternalPortPolicy?> LoadPolicyAsync(ExternalPortRecord portRecord, CancellationToken ct = default) =>
            Task.FromResult<ExternalPortPolicy?>(null);
    }
}
