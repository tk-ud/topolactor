using Topolactor.Repository;
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
        var executor = new ExternalPortPolicyStepExecutor(portResolver: new StaticPortResolver());
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
    public async Task ExecutePolicyAsync_ResolvePortRecord_PopulatesContextPortRecord()
    {
        var resolver = new StaticPortResolver();
        var executor = new ExternalPortPolicyStepExecutor(portResolver: resolver);
        var policy = new ExternalPortPolicy(
            Guid.NewGuid(),
            "resolve_policy",
            "access_port",
            "external-port-substrate-seed-coding",
            new[] { NewStep(1, "resolve_port_record") },
            Active: true);
        var context = new ExternalPortExecutionContext { RouteKey = "generic_route" };

        await executor.ExecutePolicyAsync(policy, context);

        Assert.NotNull(context.PortRecord);
        Assert.Equal("external-port-substrate-seed-coding", resolver.LastRequiredByBundle);
        Assert.Equal("access_port", resolver.LastPortKind);
        Assert.Equal("generic_route", resolver.LastRouteKey);
    }

    [Fact]
    public async Task ExecutePolicyAsync_HookSeedOperations_VerifiesSignatureAndReachesSchedulerBoundary()
    {
        var executor = new ExternalPortPolicyStepExecutor(portResolver: new StaticPortResolver(credentialKind: "none"));
        var policy = new ExternalPortPolicy(
            Guid.NewGuid(),
            "hook_seed_policy",
            "hook_port",
            "external-port-substrate-seed-coding",
            new[]
            {
                NewStep(1, "resolve_port_record"),
                NewStep(2, "resolve_credential_reference"),
                NewStep(3, "verify_signature_by_config", new Dictionary<string, string> { ["expected_signature"] = "sig-ok" }),
                NewStep(4, "enqueue_scheduler_event"),
                NewStep(5, "append_runtime_event_log")
            },
            Active: true);
        var context = new ExternalPortExecutionContext
        {
            SignatureInput = new Dictionary<string, string> { ["signature"] = "sig-ok" }
        };

        await executor.ExecutePolicyAsync(policy, context);

        Assert.True(context.SchedulerEventEnqueued);
        Assert.Equal(
            new[] { "resolve_port_record", "resolve_credential_reference", "verify_signature_by_config", "enqueue_scheduler_event", "append_runtime_event_log" },
            context.ExecutedOperationKeys);
    }

    [Fact]
    public async Task ExecuteAsync_VerifySignatureByConfig_FailClosesOnMismatch()
    {
        var executor = new ExternalPortPolicyStepExecutor();
        var context = new ExternalPortExecutionContext
        {
            SignatureInput = new Dictionary<string, string> { ["signature"] = "bad" }
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(
                NewStep(1, "verify_signature_by_config", new Dictionary<string, string> { ["expected_signature"] = "good" }),
                context));

        Assert.Equal("EXTERNAL_SIGNATURE_VERIFICATION_FAILED", error.Message);
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


    [Fact]
    public void NpgsqlCredentialVaultRepository_Source_ImplementsActiveLeaseAndAtomicVersionGuards()
    {
        var source = File.ReadAllText(FindRepositoryFile("backend/repository/NpgsqlExternalCredentialVaultRepository.cs"));

        Assert.Contains("class NpgsqlExternalCredentialVaultRepository", source);
        Assert.Contains("IExternalCredentialVaultRepository", source);
        Assert.Contains("topology.external_credential_vault", source);
        Assert.Contains("topology.external_credential_refresh_attempt", source);
        Assert.Contains("AND active = true", source);
        Assert.Contains("locked_until IS NULL OR locked_until <= @now", source);
        Assert.Contains("RETURNING version", source);
        Assert.Contains("attempt_status", source);
        Assert.Contains("encrypted_payload = @encryptedPayload", source);
        Assert.Contains("token_hash = @tokenHash", source);
        Assert.Contains("expires_at = @expiresAt", source);
        Assert.Contains("version = version + 1", source);
        Assert.Contains("AND version = @expectedVersion", source);
        Assert.Contains("EXTERNAL_CREDENTIAL_STALE_VERSION_OR_INACTIVE", source);
        Assert.DoesNotContain("switch (provider", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("if (provider", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("auth.credentials", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Program_RegistersProductionExternalCredentialVaultRepository()
    {
        var source = File.ReadAllText(FindRepositoryFile("backend/Program.cs"));

        Assert.Contains("AddSingleton<IExternalCredentialVaultRepository>", source);
        Assert.Contains("NpgsqlExternalCredentialVaultRepository", source);
    }

    [Fact]
    public void NpgsqlRepository_ParseStepConfig_ConvertsJsonbObjectToStringDictionary()
    {
        var config = NpgsqlExternalPortPolicyRepository.ParseStepConfig("""{ "expected_signature": "sig-ok", "retry": 3 }""");

        Assert.Equal("sig-ok", config["expected_signature"]);
        Assert.Equal("3", config["retry"]);
    }

    [Fact]
    public void NpgsqlRepository_Source_MapsPortTablesAndPolicyStepsWithoutPlaintextProjection()
    {
        var source = File.ReadAllText(FindRepositoryFile("backend/repository/NpgsqlExternalPortPolicyRepository.cs"));

        Assert.Contains("topology.external_access_ports", source);
        Assert.Contains("topology.external_response_ports", source);
        Assert.Contains("topology.external_hook_ports", source);
        Assert.Contains("topology.external_port_policies", source);
        Assert.Contains("topology.external_port_policy_steps", source);
        Assert.Contains("ORDER BY step_order ASC", source);
        Assert.Contains("EXTERNAL_PORT_RECORD_AMBIGUOUS", source);
        Assert.Contains("EXTERNAL_HOOK_PORT_ROUTE_KEY_REQUIRED", source);
        Assert.Contains("EXTERNAL_PORT_POLICY_AMBIGUOUS", source);
        Assert.DoesNotContain("encrypted_payload", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token_hash", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ProviderKind switch", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("switch (provider", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Program_RegistersProductionExternalPortPolicyRepository()
    {
        var source = File.ReadAllText(FindRepositoryFile("backend/Program.cs"));

        Assert.Contains("AddSingleton<IExternalPortPolicyRepository>", source);
        Assert.Contains("NpgsqlExternalPortPolicyRepository", source);
    }


    [Fact]
    public void ExternalPortAuthoringCandidates_ReadActivePortsWithoutConsumerAliasOrPlaintext()
    {
        var source = File.ReadAllText(FindRepositoryFile("backend/repository/NpgsqlUiTopologyRepository.cs"));

        Assert.Contains("FROM topology.external_access_ports", source);
        Assert.Contains("FROM topology.external_response_ports", source);
        Assert.Contains("FROM topology.external_hook_ports", source);
        Assert.Contains("WHERE active = true", source);
        Assert.DoesNotContain("required_by_bundle AS consumer_bundle_binding", source);
        Assert.Contains("NULL::text AS consumer_bundle_binding", source);
        Assert.Contains("external-port:{portKind}:{portId}", source);
        Assert.DoesNotContain("password", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Program_RegistersHttpClientAndCredentialResolverAndCryptoForExecutor()
    {
        var source = File.ReadAllText(FindRepositoryFile("backend/Program.cs"));
        Assert.Contains("IExternalPortHttpClient", source);
        Assert.Contains("HttpExternalPortHttpClient", source);
        Assert.Contains("IExternalPortCredentialReferenceResolver", source);
        Assert.Contains("ExternalPortCredentialReferenceResolver", source);
        Assert.Contains("IExternalCredentialCrypto", source);
        Assert.Contains("AesExternalCredentialCrypto", source);
        Assert.Contains("httpClient:", source);
        Assert.Contains("credentialReferenceResolver:", source);
        Assert.Contains("crypto:", source);
    }

    [Fact]
    public async Task LoadEncryptedCredentialPayload_MissingEncryptedPayload_FailsClose()
    {
        var executor = new ExternalPortPolicyStepExecutor();
        var context = new ExternalPortExecutionContext
        {
            CredentialVaultRecord = new ExternalCredentialVaultRecord(
                Guid.NewGuid(), "generic", "bundle", "oauth", null,
                null, null, null, 300, 1, null, true)
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(NewStep(1, "load_encrypted_credential_payload"), context));
        Assert.Contains("EXTERNAL_CREDENTIAL_PAYLOAD_MISSING", ex.Message);
    }

    [Fact]
    public async Task DecryptForRuntimeUse_WithoutCrypto_FailsClose()
    {
        var executor = new ExternalPortPolicyStepExecutor();
        var context = new ExternalPortExecutionContext
        {
            CredentialVaultRecord = new ExternalCredentialVaultRecord(
                Guid.NewGuid(), "generic", "bundle", "oauth", "sha256:h",
                new byte[] { 1, 2, 3 }, "env:KEY", null, 300, 1, null, true)
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(NewStep(1, "decrypt_for_runtime_use"), context));
        Assert.Contains("EXTERNAL_CREDENTIAL_CRYPTO_MISSING", ex.Message);
    }

    [Fact]
    public async Task DecryptForRuntimeUse_WithCrypto_SetsDecryptedPayloadOnContext()
    {
        var crypto = new FakeExternalCredentialCrypto("plaintext-credential");
        var executor = new ExternalPortPolicyStepExecutor(crypto: crypto);
        var context = new ExternalPortExecutionContext
        {
            CredentialVaultRecord = new ExternalCredentialVaultRecord(
                Guid.NewGuid(), "generic", "bundle", "oauth", "sha256:h",
                new byte[] { 1, 2, 3 }, "env:KEY", null, 300, 1, null, true)
        };

        await executor.ExecuteAsync(NewStep(1, "decrypt_for_runtime_use"), context);

        Assert.Equal("plaintext-credential", context.DecryptedCredentialPayload);
        Assert.Contains("decrypt_for_runtime_use", context.ExecutedOperationKeys);
    }

    [Fact]
    public async Task InjectAuthorizationHeader_SetsHeaderOnHttpRequest()
    {
        var crypto = new FakeExternalCredentialCrypto("Bearer token-abc");
        var executor = new ExternalPortPolicyStepExecutor(crypto: crypto);
        var context = new ExternalPortExecutionContext
        {
            CredentialVaultRecord = new ExternalCredentialVaultRecord(
                Guid.NewGuid(), "generic", "bundle", "oauth", "sha256:h",
                new byte[] { 1, 2, 3 }, "env:KEY", null, 300, 1, null, true),
            HttpRequest = new ExternalPortHttpRequest(
                new Uri("https://example.invalid"), HttpMethod.Get,
                new Dictionary<string, string>(), null),
            DecryptedCredentialPayload = "Bearer token-abc"
        };

        await executor.ExecuteAsync(NewStep(1, "inject_authorization_header"), context);

        Assert.NotNull(context.HttpRequest);
        Assert.True(context.HttpRequest.Headers.ContainsKey("Authorization"));
        Assert.Equal("Bearer token-abc", context.HttpRequest.Headers["Authorization"]);
        Assert.Contains("inject_authorization_header", context.ExecutedOperationKeys);
    }

    [Fact]
    public void CredentialReferenceResolver_Source_UsesProviderAndBundleNotReferenceKeyDirectly()
    {
        var source = File.ReadAllText(FindRepositoryFile("backend/runtime/ExternalPortCredentialReferenceResolver.cs"));
        Assert.Contains("LoadByProviderAndBundleAsync", source);
        Assert.Contains("portRecord.ProviderKind", source);
        Assert.Contains("portRecord.RequiredByBundle", source);
        Assert.DoesNotContain("ReferenceKey", source);
    }

    [Fact]
    public void NpgsqlCredentialVaultRepository_Source_ImplementsLoadByProviderAndBundle()
    {
        var source = File.ReadAllText(FindRepositoryFile("backend/repository/NpgsqlExternalCredentialVaultRepository.cs"));
        Assert.Contains("LoadByProviderAndBundleAsync", source);
        Assert.Contains("provider_kind = @providerKind", source);
        Assert.Contains("required_by_bundle = @requiredByBundle", source);
        Assert.Contains("AND active = true", source);
        Assert.DoesNotContain("switch (provider", source, StringComparison.OrdinalIgnoreCase);
    }

    private static ExternalPortPolicyStep NewStep(int order, string operationKey, IReadOnlyDictionary<string, string>? config = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), order, operationKey, config ?? new Dictionary<string, string>(), Active: true);

    private static string FindRepositoryFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not find {relativePath} from {AppContext.BaseDirectory}.");
    }

    private sealed class StaticPortResolver : IExternalPortResolver
    {
        private readonly string _credentialKind;

        public StaticPortResolver(string credentialKind = "none") => _credentialKind = credentialKind;

        public string? LastRequiredByBundle { get; private set; }

        public string? LastPortKind { get; private set; }

        public string? LastRouteKey { get; private set; }

        public Task<ExternalPortRecord> ResolveAsync(string requiredByBundle, string portKind, string? routeKey = null, CancellationToken ct = default)
        {
            LastRequiredByBundle = requiredByBundle;
            LastPortKind = portKind;
            LastRouteKey = routeKey;
            return Task.FromResult(new ExternalPortRecord(
                Guid.NewGuid(),
                portKind,
                requiredByBundle,
                "generic-provider",
                "https://example.invalid",
                "/hooks/generic",
                "x-signature",
                routeKey,
                _credentialKind,
                _credentialKind == "none" ? null : "vault-ref",
                Active: true));
        }
    }

    private sealed class MissingPortPolicyRepository : IExternalPortPolicyRepository
    {
        public Task<ExternalPortRecord?> LoadPortRecordAsync(string requiredByBundle, string portKind, string? routeKey, CancellationToken ct = default) =>
            Task.FromResult<ExternalPortRecord?>(null);

        public Task<ExternalPortRecord?> LoadPortRecordByIdAsync(string portKind, Guid portId, string? routeKey, CancellationToken ct = default) =>
            Task.FromResult<ExternalPortRecord?>(null);

        public Task<ExternalPortRecord?> LoadPortRecordByCanonicalBindingAsync(string manifestKey, string tableRef, string portKind, Guid portId, string? routeKey, CancellationToken ct = default) =>
            Task.FromResult<ExternalPortRecord?>(null);

        public Task<ExternalPortPolicy?> LoadPolicyAsync(ExternalPortRecord portRecord, CancellationToken ct = default) =>
            Task.FromResult<ExternalPortPolicy?>(null);
    }

    private sealed class FakeExternalCredentialCrypto : IExternalCredentialCrypto
    {
        private readonly string _plaintext;

        public FakeExternalCredentialCrypto(string plaintext) => _plaintext = plaintext;

        public string DecryptForRuntimeUse(byte[] encryptedPayload, string encryptionKeyReference) => _plaintext;

        public byte[] EncryptForVaultStorage(string plaintextPayload, string encryptionKeyReference) =>
            System.Text.Encoding.UTF8.GetBytes(plaintextPayload);

        public string ComputeTokenHash(string plaintextPayload) => $"sha256:{plaintextPayload.GetHashCode():x8}";
    }
}
