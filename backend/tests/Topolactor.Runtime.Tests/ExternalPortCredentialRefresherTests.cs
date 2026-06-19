using System.Text.Json;
using System.Text.RegularExpressions;
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
        var fakeLog = new FakeRuntimeEventLogRepository();
        var executor = new ExternalPortPolicyStepExecutor(portResolver: new StaticPortResolver(), runtimeEventLogRepository: fakeLog);
        var policy = new ExternalPortPolicy(
            Guid.NewGuid(),
            "test_policy",
            "access_port",
            "external-port-substrate-seed-coding",
            new[]
            {
                NewStep(2, "append_runtime_event_log", new Dictionary<string, string> { ["event_type"] = "test_event" }),
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
        var fakeLog = new FakeRuntimeEventLogRepository();
        var executor = new ExternalPortPolicyStepExecutor(portResolver: new StaticPortResolver(credentialKind: "none"), runtimeEventLogRepository: fakeLog);
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
                NewStep(5, "append_runtime_event_log", new Dictionary<string, string> { ["event_type"] = "scheduler_enqueued" })
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
    public void CredentialReferenceResolver_Source_UsesReferenceKeyViaLoadByReferenceKeyAsync()
    {
        var source = File.ReadAllText(FindRepositoryFile("backend/runtime/ExternalPortCredentialReferenceResolver.cs"));
        Assert.Contains("LoadByReferenceKeyAsync", source);
        Assert.Contains("portRecord.ReferenceKey", source);
        Assert.DoesNotContain("LoadByProviderAndBundleAsync", source);
        Assert.DoesNotContain("portRecord.ProviderKind", source);
        Assert.DoesNotContain("portRecord.RequiredByBundle", source);
    }

    [Fact]
    public void CredentialReferenceResolver_MissingReferenceKey_FailsClose()
    {
        var resolver = new ExternalPortCredentialReferenceResolver(new NullVaultRepository());
        var portRecord = new ExternalPortRecord(
            Guid.NewGuid(), "access_port", "file_storage_bundle",
            "object_storage", "env:ENDPOINT", null, null, null, "external", null, Active: true);

        var ex = Assert.Throws<InvalidOperationException>(
            () => resolver.ResolveCredentialReferenceAsync(portRecord).GetAwaiter().GetResult());
        Assert.Contains("EXTERNAL_PORT_REFERENCE_KEY_MISSING", ex.Message);
    }

    [Fact]
    public void NpgsqlCredentialVaultRepository_Source_ImplementsLoadByReferenceKey()
    {
        var source = File.ReadAllText(FindRepositoryFile("backend/repository/NpgsqlExternalCredentialVaultRepository.cs"));
        Assert.Contains("LoadByReferenceKeyAsync", source);
        Assert.Contains("reference_key = @referenceKey", source);
        Assert.Contains("AND active = true", source);
        Assert.DoesNotContain("switch (provider", source, StringComparison.OrdinalIgnoreCase);
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

    private sealed class FakeRuntimeEventLogRepository : IExternalPortRuntimeEventLogRepository
    {
        public string? LastEventType { get; private set; }

        public Task AppendAsync(string eventType, string? entityId, string? requiredByBundle, CancellationToken ct = default)
        {
            LastEventType = eventType;
            return Task.CompletedTask;
        }
    }

    private sealed class NullVaultRepository : IExternalCredentialVaultRepository
    {
        public Task<ExternalCredentialVaultRecord?> LoadAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<ExternalCredentialVaultRecord?>(null);

        public Task<ExternalCredentialVaultRecord?> LoadByReferenceKeyAsync(string referenceKey, CancellationToken ct = default) =>
            Task.FromResult<ExternalCredentialVaultRecord?>(null);

        public Task<ExternalCredentialVaultRecord?> LoadByProviderAndBundleAsync(string providerKind, string requiredByBundle, CancellationToken ct = default) =>
            Task.FromResult<ExternalCredentialVaultRecord?>(null);

        public Task<ExternalCredentialRefreshLease?> AcquireRefreshLeaseAsync(Guid credentialVaultId, string leaseOwner, TimeSpan leaseDuration, DateTimeOffset now, CancellationToken ct = default) =>
            Task.FromResult<ExternalCredentialRefreshLease?>(null);

        public Task WriteEncryptedCredentialPayloadAsync(Guid credentialVaultId, int expectedVersion, byte[] encryptedPayload, string tokenHash, DateTimeOffset expiresAt, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task ReleaseRefreshLeaseAsync(ExternalCredentialRefreshLease lease, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task FailRefreshLeaseAsync(ExternalCredentialRefreshLease lease, string failureCode, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}

public class CredentialPrimitiveHardeningTests
{
    // ── ParseTokenRefreshResult ───────────────────────────────────────────────

    [Fact]
    public void ParseTokenRefreshResult_WithoutCrypto_FailsClose()
    {
        var executor = new ExternalPortPolicyStepExecutor();
        var request = NewRequest();
        var response = new ExternalPortHttpResponse(200, "new-token-body");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            executor.ParseTokenRefreshResult(request, response));
        Assert.Equal("EXTERNAL_CREDENTIAL_CRYPTO_MISSING", ex.Message);
    }

    [Fact]
    public void ParseTokenRefreshResult_NonSuccessResponse_FailsClose()
    {
        var executor = new ExternalPortPolicyStepExecutor(crypto: new FakeCrypto("x"));
        var request = NewRequest();
        var response = new ExternalPortHttpResponse(401, "unauthorized");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            executor.ParseTokenRefreshResult(request, response));
        Assert.Equal("EXTERNAL_TOKEN_REFRESH_HTTP_FAILED", ex.Message);
    }

    [Fact]
    public void ParseTokenRefreshResult_MissingExpiry_FailsClose()
    {
        var executor = new ExternalPortPolicyStepExecutor(crypto: new FakeCrypto("x"));
        var request = NewRequest(config: new Dictionary<string, string>());
        var response = new ExternalPortHttpResponse(200, "{}");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            executor.ParseTokenRefreshResult(request, response));
        Assert.Equal("EXTERNAL_TOKEN_REFRESH_EXPIRY_MISSING", ex.Message);
    }

    [Fact]
    public void ParseTokenRefreshResult_WithDefaultExpiresInConfig_UsesConfigExpiry()
    {
        var executor = new ExternalPortPolicyStepExecutor(crypto: new FakeCrypto("x"));
        var before = DateTimeOffset.UtcNow;
        var request = NewRequest(config: new Dictionary<string, string> { ["default_expires_in_seconds"] = "7200" });
        var response = new ExternalPortHttpResponse(200, "new-token");

        var result = executor.ParseTokenRefreshResult(request, response);

        Assert.True(result.ExpiresAt >= before.AddSeconds(7200 - 2));
        Assert.True(result.ExpiresAt <= before.AddSeconds(7200 + 2));
        Assert.True(result.PayloadRotated);
    }

    [Fact]
    public void ParseTokenRefreshResult_WithJsonResponseExpiresIn_UsesResponseExpiry()
    {
        var executor = new ExternalPortPolicyStepExecutor(crypto: new FakeCrypto("x"));
        var before = DateTimeOffset.UtcNow;
        var request = NewRequest(config: new Dictionary<string, string>());
        var response = new ExternalPortHttpResponse(200, """{"access_token":"tok","expires_in":1800}""");

        var result = executor.ParseTokenRefreshResult(request, response);

        Assert.True(result.ExpiresAt >= before.AddSeconds(1800 - 2));
        Assert.True(result.ExpiresAt <= before.AddSeconds(1800 + 2));
    }

    [Fact]
    public void ParseTokenRefreshResult_ComputesHashViaCryptoAdapter_NotLengthPlaceholder()
    {
        var crypto = new FakeCrypto("x");
        var executor = new ExternalPortPolicyStepExecutor(crypto: crypto);
        var request = NewRequest(config: new Dictionary<string, string> { ["default_expires_in_seconds"] = "3600" });
        var response = new ExternalPortHttpResponse(200, "my-new-token");

        var result = executor.ParseTokenRefreshResult(request, response);

        var expectedHash = crypto.ComputeTokenHash("my-new-token");
        Assert.Equal(expectedHash, result.TokenHash);
        Assert.DoesNotContain("Length", result.TokenHash, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".Length", result.TokenHash, StringComparison.OrdinalIgnoreCase);
    }

    // ── acquire_refresh_lease ─────────────────────────────────────────────────

    [Fact]
    public async Task AcquireRefreshLease_WithoutRepository_FailsClose()
    {
        var executor = new ExternalPortPolicyStepExecutor();
        var context = new ExternalPortExecutionContext
        {
            CredentialVaultRecord = NewVaultRecord()
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(NewStep(1, "acquire_refresh_lease"), context));
        Assert.Equal("EXTERNAL_CREDENTIAL_VAULT_REPOSITORY_MISSING", ex.Message);
    }

    [Fact]
    public async Task AcquireRefreshLease_WithoutVaultRecord_FailsClose()
    {
        var repo = new FakeVaultRepository { LeaseToReturn = NewLease() };
        var executor = new ExternalPortPolicyStepExecutor(credentialVaultRepository: repo);
        var context = new ExternalPortExecutionContext();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(NewStep(1, "acquire_refresh_lease"), context));
        Assert.Equal("EXTERNAL_CREDENTIAL_VAULT_RECORD_MISSING", ex.Message);
    }

    [Fact]
    public async Task AcquireRefreshLease_LeaseUnavailable_FailsClose()
    {
        var repo = new FakeVaultRepository { LeaseToReturn = null };
        var executor = new ExternalPortPolicyStepExecutor(credentialVaultRepository: repo);
        var context = new ExternalPortExecutionContext { CredentialVaultRecord = NewVaultRecord() };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(NewStep(1, "acquire_refresh_lease"), context));
        Assert.Equal("EXTERNAL_CREDENTIAL_REFRESH_LEASE_UNAVAILABLE", ex.Message);
    }

    [Fact]
    public async Task AcquireRefreshLease_SetsRefreshLeaseOnContext()
    {
        var lease = NewLease();
        var repo = new FakeVaultRepository { LeaseToReturn = lease };
        var executor = new ExternalPortPolicyStepExecutor(credentialVaultRepository: repo);
        var context = new ExternalPortExecutionContext { CredentialVaultRecord = NewVaultRecord() };

        await executor.ExecuteAsync(NewStep(1, "acquire_refresh_lease"), context);

        Assert.Same(lease, context.RefreshLease);
        Assert.Contains("acquire_refresh_lease", context.ExecutedOperationKeys);
    }

    // ── request_token_by_config ───────────────────────────────────────────────

    [Fact]
    public async Task RequestTokenByConfig_WithoutHttpClient_FailsClose()
    {
        var executor = new ExternalPortPolicyStepExecutor();
        var context = new ExternalPortExecutionContext
        {
            DecryptedCredentialPayload = "old-token"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(NewStep(1, "request_token_by_config", new Dictionary<string, string> { ["endpoint"] = "https://auth.invalid/token" }), context));
        Assert.Equal("EXTERNAL_HTTP_REQUEST_MISSING", ex.Message);
    }

    [Fact]
    public async Task RequestTokenByConfig_WithoutDecryptedPayload_FailsClose()
    {
        var http = new FakeHttpClient(200, "{}");
        var executor = new ExternalPortPolicyStepExecutor(httpClient: http);
        var context = new ExternalPortExecutionContext();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(NewStep(1, "request_token_by_config", new Dictionary<string, string> { ["endpoint"] = "https://auth.invalid/token" }), context));
        Assert.Equal("EXTERNAL_CREDENTIAL_DECRYPTED_PAYLOAD_MISSING", ex.Message);
    }

    [Fact]
    public async Task RequestTokenByConfig_MissingEndpoint_FailsClose()
    {
        var http = new FakeHttpClient(200, "{}");
        var executor = new ExternalPortPolicyStepExecutor(httpClient: http);
        var context = new ExternalPortExecutionContext { DecryptedCredentialPayload = "old-token" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(NewStep(1, "request_token_by_config"), context));
        Assert.Equal("EXTERNAL_TOKEN_REFRESH_ENDPOINT_MISSING", ex.Message);
    }

    [Fact]
    public async Task RequestTokenByConfig_NonSuccessResponse_FailsClose()
    {
        var http = new FakeHttpClient(401, "unauthorized");
        var executor = new ExternalPortPolicyStepExecutor(httpClient: http);
        var context = new ExternalPortExecutionContext { DecryptedCredentialPayload = "old-token" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(NewStep(1, "request_token_by_config", new Dictionary<string, string> { ["endpoint"] = "https://auth.invalid/token" }), context));
        Assert.Equal("EXTERNAL_TOKEN_REFRESH_HTTP_FAILED", ex.Message);
    }

    [Fact]
    public async Task RequestTokenByConfig_SetsHttpResponseOnContext()
    {
        var http = new FakeHttpClient(200, "new-access-token");
        var executor = new ExternalPortPolicyStepExecutor(httpClient: http);
        var context = new ExternalPortExecutionContext { DecryptedCredentialPayload = "old-token" };

        await executor.ExecuteAsync(NewStep(1, "request_token_by_config", new Dictionary<string, string> { ["endpoint"] = "https://auth.invalid/token" }), context);

        Assert.NotNull(context.HttpResponse);
        Assert.Equal(200, context.HttpResponse.StatusCode);
        Assert.Equal("new-access-token", context.HttpResponse.Body);
        Assert.Contains("request_token_by_config", context.ExecutedOperationKeys);
    }

    [Fact]
    public async Task RequestTokenByConfig_WithEnvEndpoint_ResolvesFromEnvironment()
    {
        Environment.SetEnvironmentVariable("TOPOLACTOR_TEST_CRED_EP", "https://auth.test.invalid/token");
        try
        {
            var http = new FakeHttpClient(200, "new-token");
            var executor = new ExternalPortPolicyStepExecutor(httpClient: http);
            var context = new ExternalPortExecutionContext { DecryptedCredentialPayload = "old-token" };

            await executor.ExecuteAsync(
                NewStep(1, "request_token_by_config",
                    new Dictionary<string, string> { ["endpoint"] = "env:TOPOLACTOR_TEST_CRED_EP" }),
                context);

            Assert.NotNull(context.HttpResponse);
            Assert.Equal("new-token", context.HttpResponse.Body);
            Assert.Contains("request_token_by_config", context.ExecutedOperationKeys);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TOPOLACTOR_TEST_CRED_EP", null);
        }
    }

    [Fact]
    public async Task RequestTokenByConfig_WithMissingEnvEndpoint_FailsClose()
    {
        Environment.SetEnvironmentVariable("TOPOLACTOR_TEST_CRED_EP_MISSING", null);
        var http = new FakeHttpClient(200, "ignored");
        var executor = new ExternalPortPolicyStepExecutor(httpClient: http);
        var context = new ExternalPortExecutionContext { DecryptedCredentialPayload = "old-token" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(
                NewStep(1, "request_token_by_config",
                    new Dictionary<string, string> { ["endpoint"] = "env:TOPOLACTOR_TEST_CRED_EP_MISSING" }),
                context));
        Assert.Equal("EXTERNAL_HTTP_ENDPOINT_ENV_MISSING", ex.Message);
    }

    // ── update_token_hash ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateTokenHash_WithoutCrypto_FailsClose()
    {
        var executor = new ExternalPortPolicyStepExecutor();
        var context = new ExternalPortExecutionContext
        {
            HttpResponse = new ExternalPortHttpResponse(200, "new-token")
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(NewStep(1, "update_token_hash"), context));
        Assert.Equal("EXTERNAL_CREDENTIAL_CRYPTO_MISSING", ex.Message);
    }

    [Fact]
    public async Task UpdateTokenHash_WithoutHttpResponse_FailsClose()
    {
        var executor = new ExternalPortPolicyStepExecutor(crypto: new FakeCrypto("x"));
        var context = new ExternalPortExecutionContext();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(NewStep(1, "update_token_hash"), context));
        Assert.Equal("EXTERNAL_HTTP_RESPONSE_MISSING", ex.Message);
    }

    [Fact]
    public async Task UpdateTokenHash_ComputesHashViaCryptoAdapter()
    {
        var crypto = new FakeCrypto("x");
        var executor = new ExternalPortPolicyStepExecutor(crypto: crypto);
        var context = new ExternalPortExecutionContext
        {
            HttpResponse = new ExternalPortHttpResponse(200, "new-token-body")
        };

        await executor.ExecuteAsync(NewStep(1, "update_token_hash"), context);

        Assert.Equal(crypto.ComputeTokenHash("new-token-body"), context.PendingTokenHash);
        Assert.Contains("update_token_hash", context.ExecutedOperationKeys);
    }

    // ── update_expires_at_and_version ─────────────────────────────────────────

    [Fact]
    public async Task UpdateExpiresAtAndVersion_WithoutHttpResponse_FailsClose()
    {
        var executor = new ExternalPortPolicyStepExecutor();
        var context = new ExternalPortExecutionContext
        {
            CredentialVaultRecord = NewVaultRecord()
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(NewStep(1, "update_expires_at_and_version"), context));
        Assert.Equal("EXTERNAL_HTTP_RESPONSE_MISSING", ex.Message);
    }

    [Fact]
    public async Task UpdateExpiresAtAndVersion_MissingExpiry_FailsClose()
    {
        var executor = new ExternalPortPolicyStepExecutor();
        var context = new ExternalPortExecutionContext
        {
            HttpResponse = new ExternalPortHttpResponse(200, "{}"),
            CredentialVaultRecord = NewVaultRecord()
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(NewStep(1, "update_expires_at_and_version"), context));
        Assert.Equal("EXTERNAL_TOKEN_REFRESH_EXPIRY_MISSING", ex.Message);
    }

    [Fact]
    public async Task UpdateExpiresAtAndVersion_FromConfig_SetsExpiry()
    {
        var executor = new ExternalPortPolicyStepExecutor();
        var before = DateTimeOffset.UtcNow;
        var context = new ExternalPortExecutionContext
        {
            HttpResponse = new ExternalPortHttpResponse(200, "{}"),
            CredentialVaultRecord = NewVaultRecord()
        };

        await executor.ExecuteAsync(
            NewStep(1, "update_expires_at_and_version", new Dictionary<string, string> { ["default_expires_in_seconds"] = "3600" }),
            context);

        Assert.NotNull(context.PendingExpiresAt);
        Assert.True(context.PendingExpiresAt!.Value >= before.AddSeconds(3600 - 2));
        Assert.Contains("update_expires_at_and_version", context.ExecutedOperationKeys);
    }

    [Fact]
    public async Task UpdateExpiresAtAndVersion_FromResponseJson_SetsExpiry()
    {
        var executor = new ExternalPortPolicyStepExecutor();
        var before = DateTimeOffset.UtcNow;
        var context = new ExternalPortExecutionContext
        {
            HttpResponse = new ExternalPortHttpResponse(200, """{"access_token":"tok","expires_in":900}"""),
            CredentialVaultRecord = NewVaultRecord()
        };

        await executor.ExecuteAsync(NewStep(1, "update_expires_at_and_version"), context);

        Assert.NotNull(context.PendingExpiresAt);
        Assert.True(context.PendingExpiresAt!.Value >= before.AddSeconds(900 - 2));
    }

    // ── write_encrypted_credential_payload ────────────────────────────────────

    [Fact]
    public async Task WriteEncryptedCredentialPayload_WithoutCrypto_FailsClose()
    {
        var repo = new FakeVaultRepository();
        var executor = new ExternalPortPolicyStepExecutor(credentialVaultRepository: repo);
        var context = FullRefreshContext(lease: NewLease());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(NewStep(1, "write_encrypted_credential_payload"), context));
        Assert.Equal("EXTERNAL_CREDENTIAL_CRYPTO_MISSING", ex.Message);
    }

    [Fact]
    public async Task WriteEncryptedCredentialPayload_WithoutRepository_FailsClose()
    {
        var executor = new ExternalPortPolicyStepExecutor(crypto: new FakeCrypto("x"));
        var context = FullRefreshContext(lease: NewLease());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(NewStep(1, "write_encrypted_credential_payload"), context));
        Assert.Equal("EXTERNAL_CREDENTIAL_VAULT_REPOSITORY_MISSING", ex.Message);
    }

    [Fact]
    public async Task WriteEncryptedCredentialPayload_WithoutLease_FailsClose()
    {
        var crypto = new FakeCrypto("x");
        var repo = new FakeVaultRepository();
        var executor = new ExternalPortPolicyStepExecutor(crypto: crypto, credentialVaultRepository: repo);
        var context = FullRefreshContext(lease: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(NewStep(1, "write_encrypted_credential_payload"), context));
        Assert.Equal("EXTERNAL_CREDENTIAL_REFRESH_LEASE_MISSING", ex.Message);
    }

    [Fact]
    public async Task WriteEncryptedCredentialPayload_WithoutTokenHash_FailsClose()
    {
        var crypto = new FakeCrypto("x");
        var repo = new FakeVaultRepository();
        var executor = new ExternalPortPolicyStepExecutor(crypto: crypto, credentialVaultRepository: repo);
        var context = FullRefreshContext(lease: NewLease(), pendingTokenHash: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(NewStep(1, "write_encrypted_credential_payload"), context));
        Assert.Equal("EXTERNAL_CREDENTIAL_TOKEN_HASH_MISSING", ex.Message);
    }

    [Fact]
    public async Task WriteEncryptedCredentialPayload_WithoutExpiresAt_FailsClose()
    {
        var crypto = new FakeCrypto("x");
        var repo = new FakeVaultRepository();
        var executor = new ExternalPortPolicyStepExecutor(crypto: crypto, credentialVaultRepository: repo);
        var context = new ExternalPortExecutionContext
        {
            CredentialVaultRecord = NewVaultRecord(),
            HttpResponse = new ExternalPortHttpResponse(200, "new-token-body"),
            RefreshLease = NewLease(),
            PendingTokenHash = "sha256:abc",
            PendingExpiresAt = null
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(NewStep(1, "write_encrypted_credential_payload"), context));
        Assert.Equal("EXTERNAL_CREDENTIAL_EXPIRES_AT_MISSING", ex.Message);
    }

    [Fact]
    public async Task WriteEncryptedCredentialPayload_FullFlow_WritesAtomicallyAndClearsPlaintext()
    {
        var crypto = new FakeCrypto("x");
        var repo = new FakeVaultRepository();
        var vaultRecord = NewVaultRecord(version: 3);
        var lease = NewLease(version: 3);
        var executor = new ExternalPortPolicyStepExecutor(crypto: crypto, credentialVaultRepository: repo);
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var context = FullRefreshContext(lease: lease, vaultRecord: vaultRecord,
            pendingTokenHash: "sha256:abc123", pendingExpiresAt: expiresAt);
        context.DecryptedCredentialPayload = "old-decrypted-token";

        await executor.ExecuteAsync(NewStep(1, "write_encrypted_credential_payload"), context);

        Assert.Equal(vaultRecord.CredentialVaultId, repo.LastWriteCredentialVaultId);
        Assert.Equal(3, repo.LastWriteExpectedVersion);
        Assert.Equal("sha256:abc123", repo.LastWriteTokenHash);
        Assert.Equal(expiresAt, repo.LastWriteExpiresAt);
        Assert.Null(context.DecryptedCredentialPayload);
        Assert.Contains("write_encrypted_credential_payload", context.ExecutedOperationKeys);
    }

    // ── release_refresh_lease ─────────────────────────────────────────────────

    [Fact]
    public async Task ReleaseRefreshLease_WithoutRepository_FailsClose()
    {
        var executor = new ExternalPortPolicyStepExecutor();
        var context = new ExternalPortExecutionContext { RefreshLease = NewLease() };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(NewStep(1, "release_refresh_lease"), context));
        Assert.Equal("EXTERNAL_CREDENTIAL_VAULT_REPOSITORY_MISSING", ex.Message);
    }

    [Fact]
    public async Task ReleaseRefreshLease_WithoutLease_FailsClose()
    {
        var repo = new FakeVaultRepository();
        var executor = new ExternalPortPolicyStepExecutor(credentialVaultRepository: repo);
        var context = new ExternalPortExecutionContext();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(NewStep(1, "release_refresh_lease"), context));
        Assert.Equal("EXTERNAL_CREDENTIAL_REFRESH_LEASE_MISSING", ex.Message);
    }

    [Fact]
    public async Task ReleaseRefreshLease_ReleasesLease()
    {
        var repo = new FakeVaultRepository();
        var executor = new ExternalPortPolicyStepExecutor(credentialVaultRepository: repo);
        var context = new ExternalPortExecutionContext { RefreshLease = NewLease() };

        await executor.ExecuteAsync(NewStep(1, "release_refresh_lease"), context);

        Assert.True(repo.LeaseReleased);
        Assert.Contains("release_refresh_lease", context.ExecutedOperationKeys);
    }

    // ── full policy flow ──────────────────────────────────────────────────────

    [Fact]
    public async Task CredentialRefreshPolicy_ExecutesAllStepsInOrder()
    {
        var lease = NewLease(version: 1);
        var repo = new FakeVaultRepository { LeaseToReturn = lease };
        var crypto = new FakeCrypto("old-plaintext-token");
        var http = new FakeHttpClient(200, """{"access_token":"new-tok","expires_in":3600}""");
        var vaultRecord = NewVaultRecord(version: 1);

        var executor = new ExternalPortPolicyStepExecutor(
            crypto: crypto,
            httpClient: http,
            credentialVaultRepository: repo);
        var policy = new ExternalPortPolicy(
            Guid.NewGuid(), "credential_vault_generic_refresh", "access_port",
            "external-port-substrate-seed-coding",
            new[]
            {
                NewStep(1, "load_encrypted_credential_payload"),
                NewStep(2, "decrypt_for_runtime_use"),
                NewStep(3, "acquire_refresh_lease", new Dictionary<string, string> { ["lease_duration_seconds"] = "300" }),
                NewStep(4, "request_token_by_config", new Dictionary<string, string> { ["endpoint"] = "https://auth.invalid/token" }),
                NewStep(5, "update_token_hash"),
                NewStep(6, "update_expires_at_and_version"),
                NewStep(7, "write_encrypted_credential_payload"),
                NewStep(8, "release_refresh_lease"),
            },
            Active: true);
        var context = new ExternalPortExecutionContext
        {
            CredentialVaultRecord = vaultRecord
        };

        await executor.ExecutePolicyAsync(policy, context);

        Assert.Equal(
            new[]
            {
                "load_encrypted_credential_payload",
                "decrypt_for_runtime_use",
                "acquire_refresh_lease",
                "request_token_by_config",
                "update_token_hash",
                "update_expires_at_and_version",
                "write_encrypted_credential_payload",
                "release_refresh_lease",
            },
            context.ExecutedOperationKeys);
        Assert.Null(context.DecryptedCredentialPayload);
        Assert.True(repo.LeaseReleased);
        Assert.NotNull(repo.LastWriteTokenHash);
        Assert.NotNull(repo.LastWriteExpiresAt);
    }

    [Fact]
    public async Task ExecutePolicyAsync_FailsAfterLeaseAcquisition_FailsLease()
    {
        var lease = NewLease(version: 1);
        var repo = new FakeVaultRepository { LeaseToReturn = lease };
        var crypto = new FakeCrypto("old-token");
        var executor = new ExternalPortPolicyStepExecutor(
            crypto: crypto, credentialVaultRepository: repo);
        var policy = new ExternalPortPolicy(
            Guid.NewGuid(), "test_policy", "access_port", "test-bundle",
            new[]
            {
                NewStep(1, "load_encrypted_credential_payload"),
                NewStep(2, "decrypt_for_runtime_use"),
                NewStep(3, "acquire_refresh_lease", new Dictionary<string, string> { ["lease_duration_seconds"] = "300" }),
                NewStep(4, "fail_close"),
            },
            Active: true);
        var context = new ExternalPortExecutionContext { CredentialVaultRecord = NewVaultRecord() };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecutePolicyAsync(policy, context));

        Assert.True(repo.LeaseFailCalled);
        Assert.Equal("EXTERNAL_CREDENTIAL_REFRESH_FAILED", repo.LastLeaseFailCode);
        Assert.False(repo.LeaseReleased);
    }

    // ── plaintext projection prohibition ─────────────────────────────────────

    [Fact]
    public async Task WriteEncryptedCredentialPayload_ClearsDecryptedPayload_PreventingPlaintextProjection()
    {
        var crypto = new FakeCrypto("x");
        var repo = new FakeVaultRepository();
        var executor = new ExternalPortPolicyStepExecutor(crypto: crypto, credentialVaultRepository: repo);
        var context = FullRefreshContext(lease: NewLease());
        context.DecryptedCredentialPayload = "sensitive-plaintext-must-not-project";

        await executor.ExecuteAsync(NewStep(1, "write_encrypted_credential_payload"), context);

        Assert.Null(context.DecryptedCredentialPayload);
        Assert.Null(context.OutputProp);
    }

    // ── seed proof ────────────────────────────────────────────────────────────

    [Fact]
    public void CredentialVaultGenericRefreshPolicy_SeedContainsAllOperationKeysWithoutPlaintext()
    {
        var source = File.ReadAllText(FindRepositoryFile("db/seed_empty.sql"));

        Assert.Contains("credential_vault_generic_refresh", source);
        Assert.Contains("acquire_refresh_lease", source);
        Assert.Contains("request_token_by_config", source);
        Assert.Contains("update_token_hash", source);
        Assert.Contains("update_expires_at_and_version", source);
        Assert.Contains("write_encrypted_credential_payload", source);
        Assert.Contains("release_refresh_lease", source);
        Assert.Contains("default_expires_in_seconds", source);
        Assert.Contains("env:TOKEN_REFRESH_ENDPOINT_REF", source);
        Assert.DoesNotContain("api_key =", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("client_secret =", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw_token", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CredentialVaultGenericRefreshPolicy_SeedPolicyExecution_RunsAllStepsInOrder()
    {
        Environment.SetEnvironmentVariable("TOKEN_REFRESH_ENDPOINT_REF", "https://auth.seed-test.invalid/token");
        try
        {
            var seedSql = File.ReadAllText(FindRepositoryFile("db/seed_empty.sql"));
            const string policyId = "00000000-0000-0000-0000-0000000000f0";
            var steps = ParseSeedStepsForPolicy(seedSql, policyId);
            Assert.Equal(8, steps.Count);

            var policy = new ExternalPortPolicy(
                new Guid(policyId), "credential_vault_generic_refresh", "access_port",
                "external-port-substrate-seed-coding", steps, Active: true);

            var lease = NewLease(version: 1);
            var repo = new FakeVaultRepository { LeaseToReturn = lease };
            var crypto = new FakeCrypto("old-plaintext-token");
            var http = new FakeHttpClient(200, """{"expires_in":3600}""");
            var executor = new ExternalPortPolicyStepExecutor(
                crypto: crypto, httpClient: http, credentialVaultRepository: repo);
            var context = new ExternalPortExecutionContext
            {
                CredentialVaultRecord = NewVaultRecord(version: 1)
            };

            await executor.ExecutePolicyAsync(policy, context);

            Assert.Equal(
                new[]
                {
                    "load_encrypted_credential_payload", "decrypt_for_runtime_use",
                    "acquire_refresh_lease", "request_token_by_config",
                    "update_token_hash", "update_expires_at_and_version",
                    "write_encrypted_credential_payload", "release_refresh_lease",
                },
                context.ExecutedOperationKeys);
            Assert.Null(context.DecryptedCredentialPayload);
            Assert.True(repo.LeaseReleased);
            Assert.NotNull(repo.LastWriteTokenHash);
            Assert.NotNull(repo.LastWriteExpiresAt);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TOKEN_REFRESH_ENDPOINT_REF", null);
        }
    }

    private static IReadOnlyList<ExternalPortPolicyStep> ParseSeedStepsForPolicy(string seedSql, string policyId)
    {
        var pattern = new Regex(
            $@"'[0-9a-f-]{{36}}',\s*'{Regex.Escape(policyId)}',\s*(\d+),\s*'([a-z_]+)',\s*'(\{{[^']*\}})'",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return pattern.Matches(seedSql)
            .Select(m => new ExternalPortPolicyStep(
                Guid.NewGuid(),
                new Guid(policyId),
                int.Parse(m.Groups[1].Value),
                m.Groups[2].Value,
                JsonSerializer.Deserialize<Dictionary<string, string>>(m.Groups[3].Value)
                    ?? new Dictionary<string, string>(),
                Active: true))
            .OrderBy(s => s.StepOrder)
            .ToList();
    }

    [Fact]
    public void Program_RegistersCredentialVaultRepositoryOnPolicyStepExecutor()
    {
        var source = File.ReadAllText(FindRepositoryFile("backend/Program.cs"));
        Assert.Contains("credentialVaultRepository:", source);
        Assert.Contains("IExternalCredentialVaultRepository", source);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static ExternalPortPolicyStep NewStep(int order, string operationKey, IReadOnlyDictionary<string, string>? config = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), order, operationKey, config ?? new Dictionary<string, string>(), Active: true);

    private static ExternalTokenRefreshRequest NewRequest(IReadOnlyDictionary<string, string>? config = null) =>
        new(Guid.NewGuid(), "generic-oauth", "oauth_refresh_token",
            config ?? new Dictionary<string, string> { ["default_expires_in_seconds"] = "3600" },
            "old-secret", DateTimeOffset.UtcNow.AddMinutes(5), 1);

    private static ExternalCredentialVaultRecord NewVaultRecord(int version = 1) =>
        new(Guid.NewGuid(), "generic-oauth", "external-port-substrate-seed-coding",
            "oauth_refresh_token", "sha256:old-hash",
            new byte[] { 1, 2, 3 }, "env:KEY",
            DateTimeOffset.UtcNow.AddMinutes(10), 300, version, null, true);

    private static ExternalCredentialRefreshLease NewLease(int version = 1) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "credential_primitive",
            DateTimeOffset.UtcNow.AddMinutes(5), version);

    private static ExternalPortExecutionContext FullRefreshContext(
        ExternalCredentialRefreshLease? lease,
        ExternalCredentialVaultRecord? vaultRecord = null,
        string? pendingTokenHash = "sha256:abc",
        DateTimeOffset? pendingExpiresAt = null)
    {
        return new ExternalPortExecutionContext
        {
            CredentialVaultRecord = vaultRecord ?? NewVaultRecord(),
            HttpResponse = new ExternalPortHttpResponse(200, "new-token-body"),
            RefreshLease = lease,
            PendingTokenHash = pendingTokenHash,
            PendingExpiresAt = pendingExpiresAt ?? DateTimeOffset.UtcNow.AddHours(1),
        };
    }

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

    private sealed class FakeCrypto : IExternalCredentialCrypto
    {
        private readonly string _plaintext;
        public FakeCrypto(string plaintext) => _plaintext = plaintext;
        public string DecryptForRuntimeUse(byte[] encryptedPayload, string encryptionKeyReference) => _plaintext;
        public byte[] EncryptForVaultStorage(string plaintextPayload, string encryptionKeyReference) =>
            System.Text.Encoding.UTF8.GetBytes(plaintextPayload);
        public string ComputeTokenHash(string plaintextPayload) => $"sha256:{plaintextPayload.GetHashCode():x8}";
    }

    private sealed class FakeVaultRepository : IExternalCredentialVaultRepository
    {
        public ExternalCredentialRefreshLease? LeaseToReturn { get; set; }
        public Guid? LastWriteCredentialVaultId { get; private set; }
        public int? LastWriteExpectedVersion { get; private set; }
        public string? LastWriteTokenHash { get; private set; }
        public DateTimeOffset? LastWriteExpiresAt { get; private set; }
        public bool LeaseReleased { get; private set; }
        public bool LeaseFailCalled { get; private set; }
        public string? LastLeaseFailCode { get; private set; }

        public Task<ExternalCredentialVaultRecord?> LoadAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<ExternalCredentialVaultRecord?>(null);
        public Task<ExternalCredentialVaultRecord?> LoadByReferenceKeyAsync(string referenceKey, CancellationToken ct = default) =>
            Task.FromResult<ExternalCredentialVaultRecord?>(null);
        public Task<ExternalCredentialVaultRecord?> LoadByProviderAndBundleAsync(string providerKind, string requiredByBundle, CancellationToken ct = default) =>
            Task.FromResult<ExternalCredentialVaultRecord?>(null);

        public Task<ExternalCredentialRefreshLease?> AcquireRefreshLeaseAsync(Guid credentialVaultId, string leaseOwner, TimeSpan leaseDuration, DateTimeOffset now, CancellationToken ct = default) =>
            Task.FromResult(LeaseToReturn);

        public Task WriteEncryptedCredentialPayloadAsync(Guid credentialVaultId, int expectedVersion, byte[] encryptedPayload, string tokenHash, DateTimeOffset expiresAt, CancellationToken ct = default)
        {
            LastWriteCredentialVaultId = credentialVaultId;
            LastWriteExpectedVersion = expectedVersion;
            LastWriteTokenHash = tokenHash;
            LastWriteExpiresAt = expiresAt;
            return Task.CompletedTask;
        }

        public Task ReleaseRefreshLeaseAsync(ExternalCredentialRefreshLease lease, CancellationToken ct = default)
        {
            LeaseReleased = true;
            return Task.CompletedTask;
        }

        public Task FailRefreshLeaseAsync(ExternalCredentialRefreshLease lease, string failureCode, CancellationToken ct = default)
        {
            LeaseFailCalled = true;
            LastLeaseFailCode = failureCode;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHttpClient : IExternalPortHttpClient
    {
        private readonly ExternalPortHttpResponse _response;
        public FakeHttpClient(int statusCode, string body) => _response = new(statusCode, body);
        public Task<ExternalPortHttpResponse> SendAsync(ExternalPortHttpRequest request, CancellationToken ct = default) =>
            Task.FromResult(_response);
    }
}
