using System.Text.Json;
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
    // --- C# violation fixes ---

    [Fact]
    public async Task CredentialPrimitive_BuildHttpRequest_RequiresExplicitMethod()
    {
        var executor = new ExternalPortPolicyStepExecutor();
        var context = new ExternalPortExecutionContext
        {
            PortRecord = new ExternalPortRecord(Guid.NewGuid(), "access_port", "bundle", "oauth", "https://token.invalid", null, null, null, "external", null, true)
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(
                NewStep(1, "build_http_request", new Dictionary<string, string> { ["endpoint"] = "https://token.invalid" }),
                context));
        Assert.Equal("EXTERNAL_HTTP_METHOD_MISSING", ex.Message);
    }

    [Fact]
    public void CredentialPrimitive_BuildTokenRefreshRequest_RequiresExplicitMethod()
    {
        var executor = new ExternalPortPolicyStepExecutor();
        var request = new ExternalTokenRefreshRequest(
            Guid.NewGuid(), "oauth", "refresh",
            new Dictionary<string, string> { ["endpoint"] = "https://token.invalid" },
            "secret", null, 1);

        var ex = Assert.Throws<InvalidOperationException>(() => executor.BuildTokenRefreshRequest(request));
        Assert.Equal("EXTERNAL_HTTP_METHOD_MISSING", ex.Message);
    }

    [Fact]
    public void CredentialPrimitive_ParseTokenRefreshResult_RequiresCryptoAdapter()
    {
        var executor = new ExternalPortPolicyStepExecutor();
        var request = new ExternalTokenRefreshRequest(
            Guid.NewGuid(), "oauth", "refresh",
            new Dictionary<string, string> { ["expires_at_response_key"] = "expires_at" },
            "secret", null, 1);
        var response = new ExternalPortHttpResponse(200, """{"access_token":"tok","expires_at":"2026-06-20T00:00:00Z"}""");

        var ex = Assert.Throws<InvalidOperationException>(() => executor.ParseTokenRefreshResult(request, response));
        Assert.Equal("EXTERNAL_TOKEN_REFRESH_HASH_ADAPTER_REQUIRED", ex.Message);
    }

    [Fact]
    public void CredentialPrimitive_ParseTokenRefreshResult_RequiresExpiresAtKey()
    {
        var crypto = new HardeningFakeCredentialCrypto();
        var executor = new ExternalPortPolicyStepExecutor(crypto: crypto);
        var request = new ExternalTokenRefreshRequest(
            Guid.NewGuid(), "oauth", "refresh",
            new Dictionary<string, string>(),
            "secret", null, 1);
        var response = new ExternalPortHttpResponse(200, """{"access_token":"tok"}""");

        var ex = Assert.Throws<InvalidOperationException>(() => executor.ParseTokenRefreshResult(request, response));
        Assert.Equal("EXTERNAL_TOKEN_REFRESH_EXPIRY_MAPPING_MISSING", ex.Message);
    }

    [Fact]
    public void CredentialPrimitive_ParseTokenRefreshResult_ComputesHashViaCrypto()
    {
        var crypto = new HardeningFakeCredentialCrypto();
        var executor = new ExternalPortPolicyStepExecutor(crypto: crypto);
        var request = new ExternalTokenRefreshRequest(
            Guid.NewGuid(), "oauth", "refresh",
            new Dictionary<string, string> { ["expires_at_response_key"] = "expires_at" },
            "secret", null, 1);
        var body = """{"access_token":"tok","expires_at":"2026-06-20T00:00:00Z"}""";
        var response = new ExternalPortHttpResponse(200, body);

        var result = executor.ParseTokenRefreshResult(request, response);

        Assert.Equal(crypto.ComputeTokenHash(body), result.TokenHash);
        Assert.NotEqual($"sha256:{body.Length}", result.TokenHash);
    }

    // --- Seed evidence ---

    [Fact]
    public void SeedSql_CredentialRefreshPolicy_UsesExecuteAbstractFunction()
    {
        var source = File.ReadAllText(FindRepositoryFile("db/seed_empty.sql"));
        Assert.Contains("execute_abstract_function", source);
        Assert.Contains("credential.refresh_token", source);
        Assert.Contains("external_credential_vault_refresh", source);
        Assert.DoesNotContain("acquire_refresh_lease", source);
        Assert.DoesNotContain("request_token_by_config", source);
    }

    [Fact]
    public void SeedSql_BuildHttpRequest_AllStepsHaveExplicitMethod()
    {
        var source = File.ReadAllText(FindRepositoryFile("db/seed_empty.sql"));
        var lines = source.Split('\n');
        var buildHttpLines = lines.Where(static l => l.Contains("'build_http_request'")).ToList();
        Assert.NotEmpty(buildHttpLines);
        foreach (var line in buildHttpLines)
            Assert.Contains("\"method\"", line);
    }

    // --- DI registration ---

    [Fact]
    public void Program_RegistersAllSixCredentialPrimitiveAdapters()
    {
        var source = File.ReadAllText(FindRepositoryFile("backend/Program.cs"));
        Assert.Contains("CredentialAcquireLeaseAdapter", source);
        Assert.Contains("CredentialHttpRequestAdapter", source);
        Assert.Contains("CredentialComputeTokenHashAdapter", source);
        Assert.Contains("CredentialParseExpiresAtAdapter", source);
        Assert.Contains("CredentialWriteVaultAdapter", source);
        Assert.Contains("CredentialReleaseLeaseAdapter", source);
    }

    // --- No provider/bundle branching ---

    [Fact]
    public void CredentialPrimitives_DoNotBranchOnProviderKindOrRequiredByBundle()
    {
        var source = File.ReadAllText(FindRepositoryFile("backend/runtime/AbstractFunctionRuntime.cs"));
        var idx = source.IndexOf("class CredentialAcquireLeaseAdapter", StringComparison.Ordinal);
        Assert.True(idx >= 0, "Credential adapter classes not found in AbstractFunctionRuntime.cs");
        var credentialSection = source[idx..];
        Assert.DoesNotContain("provider_kind", credentialSection, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("required_by_bundle", credentialSection, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("switch (provider", credentialSection, StringComparison.OrdinalIgnoreCase);
    }

    // --- Non-2xx fail-close ---

    [Fact]
    public async Task CredentialHttpRequestAdapter_Non2xxResponse_FailsClose()
    {
        var adapter = new CredentialHttpRequestAdapter(new HardeningNon2xxHttpClient(401));
        var step = new AbstractFunctionStep(
            Guid.NewGuid(), 1, "credential_http_request",
            new Dictionary<string, string> { ["method"] = "POST", ["endpoint"] = "https://token.example.com" },
            Array.Empty<AbstractFunctionInputBinding>(), "token_response", true);
        var inputs = new Dictionary<string, object?> { ["decrypted_credential_payload"] = "secret" };

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => adapter.ExecuteAsync(step, inputs, new AbstractFunctionExecutionContext("test")));
        Assert.Contains("CREDENTIAL_HTTP_REQUEST_NON_2XX", ex.Message);
        Assert.Contains("401", ex.Message);
    }

    // --- Seed-route test: full chain through AbstractFunctionExecutor ---

    [Fact]
    public async Task SeedSql_CredentialRefreshManifest_ExecutesFullChainThroughAbstractFunctionExecutor()
    {
        var vaultId = Guid.NewGuid();
        var vaultRecord = new ExternalCredentialVaultRecord(
            vaultId, "oauth", "bundle", "refresh", null, new byte[] { 1 }, "key-ref", null, 300, 1, null, true);

        var spyVault = new HardeningSpyVaultRepository(vaultId);
        var fakeHttp = new HardeningSuccessHttpClient(@"{""access_token"":""tok"",""expires_at"":""2027-06-20T00:00:00Z""}");
        var crypto = new HardeningFakeCredentialCrypto();

        var executor = new AbstractFunctionExecutor(
            new HardeningStaticManifestRepository(BuildCredentialRefreshManifest()),
            new IAbstractFunctionPrimitiveAdapter[]
            {
                new CredentialAcquireLeaseAdapter(spyVault),
                new CredentialHttpRequestAdapter(fakeHttp),
                new CredentialComputeTokenHashAdapter(crypto),
                new CredentialParseExpiresAtAdapter(),
                new CredentialWriteVaultAdapter(spyVault, crypto),
                new CredentialReleaseLeaseAdapter(spyVault),
                new CredentialFailLeaseAdapter(spyVault)
            });

        var portRecord = new ExternalPortRecord(Guid.NewGuid(), "oauth_refresh", "external_credential_vault_refresh", "oauth",
            "https://token.example.com", null, null, null, "external", null, true);
        var policy = new ExternalPortPolicy(Guid.NewGuid(), "external_credential_vault_refresh", "access_port",
            "external_credential_vault_refresh", Array.Empty<ExternalPortPolicyStep>(), true);
        var portContext = new ExternalPortExecutionContext
        {
            PortRecord = portRecord,
            Policy = policy,
            CredentialVaultRecord = vaultRecord,
            DecryptedCredentialPayload = @"{""refresh_token"":""rt123""}"
        };
        var context = new AbstractFunctionExecutionContext("external_credential_vault_refresh", externalPortContext: portContext);

        await executor.ExecuteAsync("credential.refresh_token", context);

        Assert.Equal(new[] { "credential_acquire_lease", "credential_http_request", "credential_compute_token_hash", "credential_parse_expires_at", "credential_write_vault", "credential_release_lease" },
            context.ExecutedPrimitiveKeys);
        Assert.True(spyVault.WriteWasCalled, "vault write must be called");
        Assert.True(spyVault.ReleaseWasCalled, "lease release must be called on success path");
        Assert.False(spyVault.FailWasCalled, "FailRefreshLease must NOT be called on success path");
    }

    // --- Compensation: lease fail on step failure ---

    [Fact]
    public async Task CredentialRefreshManifest_WhenHttpStepFails_CompensationCallsFailLease()
    {
        var vaultId = Guid.NewGuid();
        var vaultRecord = new ExternalCredentialVaultRecord(
            vaultId, "oauth", "bundle", "refresh", null, new byte[] { 1 }, "key-ref", null, 300, 1, null, true);

        var spyVault = new HardeningSpyVaultRepository(vaultId);
        var non2xxHttp = new HardeningNon2xxHttpClient(401);
        var crypto = new HardeningFakeCredentialCrypto();

        var executor = new AbstractFunctionExecutor(
            new HardeningStaticManifestRepository(BuildCredentialRefreshManifest()),
            new IAbstractFunctionPrimitiveAdapter[]
            {
                new CredentialAcquireLeaseAdapter(spyVault),
                new CredentialHttpRequestAdapter(non2xxHttp),
                new CredentialComputeTokenHashAdapter(crypto),
                new CredentialParseExpiresAtAdapter(),
                new CredentialWriteVaultAdapter(spyVault, crypto),
                new CredentialReleaseLeaseAdapter(spyVault),
                new CredentialFailLeaseAdapter(spyVault)
            });

        var portRecord = new ExternalPortRecord(Guid.NewGuid(), "oauth_refresh", "external_credential_vault_refresh", "oauth",
            "https://token.example.com", null, null, null, "external", null, true);
        var policy = new ExternalPortPolicy(Guid.NewGuid(), "external_credential_vault_refresh", "access_port",
            "external_credential_vault_refresh", Array.Empty<ExternalPortPolicyStep>(), true);
        var portContext = new ExternalPortExecutionContext
        {
            PortRecord = portRecord,
            Policy = policy,
            CredentialVaultRecord = vaultRecord,
            DecryptedCredentialPayload = @"{""refresh_token"":""rt123""}"
        };
        var context = new AbstractFunctionExecutionContext("external_credential_vault_refresh", externalPortContext: portContext);

        await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("credential.refresh_token", context));

        Assert.True(spyVault.AcquireWasCalled, "lease must have been acquired before failure");
        Assert.True(spyVault.FailWasCalled, "compensation must call FailRefreshLease after step failure");
        Assert.False(spyVault.WriteWasCalled, "vault write must NOT be called after HTTP failure");
        Assert.False(spyVault.ReleaseWasCalled, "ReleaseRefreshLease must NOT be called when step failed");
    }

    // --- Primitive adapter fail-close ---

    [Fact]
    public async Task CredentialAcquireLeaseAdapter_MissingLeaseDuration_FailsClose()
    {
        var adapter = new CredentialAcquireLeaseAdapter(new HardeningNullVaultRepository());
        var step = new AbstractFunctionStep(
            Guid.NewGuid(), 1, "credential_acquire_lease",
            new Dictionary<string, string> { ["lease_owner"] = "test" },
            Array.Empty<AbstractFunctionInputBinding>(), "credential_lease", true);
        var inputs = new Dictionary<string, object?> { ["credential_vault_id"] = Guid.NewGuid() };

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => adapter.ExecuteAsync(step, inputs, new AbstractFunctionExecutionContext("test")));
        Assert.Equal("CREDENTIAL_ACQUIRE_LEASE_DURATION_MISSING", ex.Message);
    }

    [Fact]
    public async Task CredentialHttpRequestAdapter_MissingMethod_FailsClose()
    {
        var adapter = new CredentialHttpRequestAdapter(new HardeningThrowingHttpClient());
        var step = new AbstractFunctionStep(
            Guid.NewGuid(), 1, "credential_http_request",
            new Dictionary<string, string>(),
            Array.Empty<AbstractFunctionInputBinding>(), "token_response", true);
        var inputs = new Dictionary<string, object?> { ["decrypted_credential_payload"] = "secret" };

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => adapter.ExecuteAsync(step, inputs, new AbstractFunctionExecutionContext("test")));
        Assert.Equal("CREDENTIAL_HTTP_REQUEST_METHOD_MISSING", ex.Message);
    }

    [Fact]
    public async Task CredentialParseExpiresAtAdapter_MissingExpiresAtKey_FailsClose()
    {
        var adapter = new CredentialParseExpiresAtAdapter();
        var step = new AbstractFunctionStep(
            Guid.NewGuid(), 1, "credential_parse_expires_at",
            new Dictionary<string, string>(),
            Array.Empty<AbstractFunctionInputBinding>(), "token_expires_at", true);
        var response = new ExternalPortHttpResponse(200, """{"token":"abc","expires_at":"2026-06-20T00:00:00Z"}""");
        var inputs = new Dictionary<string, object?> { ["token_response"] = response };

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => adapter.ExecuteAsync(step, inputs, new AbstractFunctionExecutionContext("test")));
        Assert.Equal("CREDENTIAL_PARSE_EXPIRES_AT_KEY_MISSING", ex.Message);
    }

    [Fact]
    public async Task CredentialParseExpiresAtAdapter_ParsesIsoDtoString()
    {
        var adapter = new CredentialParseExpiresAtAdapter();
        var step = new AbstractFunctionStep(
            Guid.NewGuid(), 1, "credential_parse_expires_at",
            new Dictionary<string, string> { ["expires_at_response_key"] = "expires_at" },
            Array.Empty<AbstractFunctionInputBinding>(), "token_expires_at", true);
        var response = new ExternalPortHttpResponse(200, """{"token":"abc","expires_at":"2026-06-20T12:00:00Z"}""");
        var inputs = new Dictionary<string, object?> { ["token_response"] = response };

        var result = await adapter.ExecuteAsync(step, inputs, new AbstractFunctionExecutionContext("test"));

        Assert.IsType<DateTimeOffset>(result);
        Assert.Equal(new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero), (DateTimeOffset)result!);
    }

    [Fact]
    public async Task CredentialParseExpiresAtAdapter_ParsesUnixTimestampNumber()
    {
        var adapter = new CredentialParseExpiresAtAdapter();
        var step = new AbstractFunctionStep(
            Guid.NewGuid(), 1, "credential_parse_expires_at",
            new Dictionary<string, string> { ["expires_at_response_key"] = "exp" },
            Array.Empty<AbstractFunctionInputBinding>(), "token_expires_at", true);
        var unixTs = new DateTimeOffset(2026, 6, 20, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        var response = new ExternalPortHttpResponse(200, $$"""{"token":"abc","exp":{{unixTs}}}""");
        var inputs = new Dictionary<string, object?> { ["token_response"] = response };

        var result = await adapter.ExecuteAsync(step, inputs, new AbstractFunctionExecutionContext("test"));

        Assert.IsType<DateTimeOffset>(result);
        Assert.Equal(unixTs, ((DateTimeOffset)result!).ToUnixTimeSeconds());
    }

    [Fact]
    public async Task CredentialWriteVaultAdapter_MissingTokenHash_FailsClose()
    {
        var adapter = new CredentialWriteVaultAdapter(new HardeningNullVaultRepository(), new HardeningFakeCredentialCrypto());
        var step = new AbstractFunctionStep(
            Guid.NewGuid(), 1, "credential_write_vault",
            new Dictionary<string, string>(),
            Array.Empty<AbstractFunctionInputBinding>(), null, true);
        var vaultRecord = new ExternalCredentialVaultRecord(
            Guid.NewGuid(), "oauth", "bundle", "refresh", null, new byte[] { 1 }, "key-ref", null, 300, 1, null, true);
        var portContext = new ExternalPortExecutionContext { CredentialVaultRecord = vaultRecord };
        var lease = new ExternalCredentialRefreshLease(Guid.NewGuid(), vaultRecord.CredentialVaultId, "owner", DateTimeOffset.UtcNow.AddMinutes(5), 1);
        var inputs = new Dictionary<string, object?>
        {
            ["credential_lease"] = lease,
            // token_hash missing intentionally
            ["token_expires_at"] = DateTimeOffset.UtcNow.AddHours(1),
            ["token_response"] = new ExternalPortHttpResponse(200, "body")
        };
        var context = new AbstractFunctionExecutionContext("test", externalPortContext: portContext);

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => adapter.ExecuteAsync(step, inputs, context));
        Assert.Equal("CREDENTIAL_WRITE_VAULT_TOKEN_HASH_MISSING", ex.Message);
    }

    [Fact]
    public async Task CredentialReleaseLeaseAdapter_MissingLease_FailsClose()
    {
        var adapter = new CredentialReleaseLeaseAdapter(new HardeningNullVaultRepository());
        var step = new AbstractFunctionStep(
            Guid.NewGuid(), 1, "credential_release_lease",
            new Dictionary<string, string>(),
            Array.Empty<AbstractFunctionInputBinding>(), null, true);
        var inputs = new Dictionary<string, object?>();

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => adapter.ExecuteAsync(step, inputs, new AbstractFunctionExecutionContext("test")));
        Assert.Equal("CREDENTIAL_RELEASE_LEASE_MISSING", ex.Message);
    }

    // --- Helpers ---

    private static ExternalPortPolicyStep NewStep(int order, string operationKey, IReadOnlyDictionary<string, string>? config = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), order, operationKey, config ?? new Dictionary<string, string>(), Active: true);

    private static string FindRepositoryFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"Could not find {relativePath} from {AppContext.BaseDirectory}.");
    }

    private static AbstractFunctionManifest BuildCredentialRefreshManifest()
    {
        var manifestId = Guid.Parse("00000000-0000-0000-0000-00000000af10");
        var bf15 = new AbstractFunctionStep(
            Guid.Parse("00000000-0000-0000-0000-00000000bf15"), 1, "credential_acquire_lease",
            new Dictionary<string, string> { ["lease_owner"] = "external_credential_vault_refresh", ["lease_duration_minutes"] = "5" },
            new[] { new AbstractFunctionInputBinding("credential_vault_id", "external_context", "credential_vault_id", true, false) },
            "credential_lease", true, false);
        var bf16 = new AbstractFunctionStep(
            Guid.Parse("00000000-0000-0000-0000-00000000bf16"), 2, "credential_http_request",
            new Dictionary<string, string> { ["method"] = "POST" },
            new[] { new AbstractFunctionInputBinding("decrypted_credential_payload", "external_context", "decrypted_credential_payload", true, true) },
            "token_response", true, false);
        var bf17 = new AbstractFunctionStep(
            Guid.Parse("00000000-0000-0000-0000-00000000bf17"), 3, "credential_compute_token_hash",
            new Dictionary<string, string>(),
            new[] { new AbstractFunctionInputBinding("token_response", "result_context", "token_response", true, true) },
            "token_hash", true, false);
        var bf18 = new AbstractFunctionStep(
            Guid.Parse("00000000-0000-0000-0000-00000000bf18"), 4, "credential_parse_expires_at",
            new Dictionary<string, string> { ["expires_at_response_key"] = "expires_at" },
            new[] { new AbstractFunctionInputBinding("token_response", "result_context", "token_response", true, true) },
            "token_expires_at", true, false);
        var bf19 = new AbstractFunctionStep(
            Guid.Parse("00000000-0000-0000-0000-00000000bf19"), 5, "credential_write_vault",
            new Dictionary<string, string>(),
            new AbstractFunctionInputBinding[]
            {
                new("credential_lease",  "result_context", "credential_lease",  true,  false),
                new("token_hash",        "result_context", "token_hash",        true,  false),
                new("token_expires_at",  "result_context", "token_expires_at",  true,  false),
                new("token_response",    "result_context", "token_response",    true,  true)
            },
            null, true, false);
        var bf1a = new AbstractFunctionStep(
            Guid.Parse("00000000-0000-0000-0000-00000000bf1a"), 6, "credential_release_lease",
            new Dictionary<string, string>(),
            new[] { new AbstractFunctionInputBinding("credential_lease", "result_context", "credential_lease", true, false) },
            null, true, false);
        var bf1b = new AbstractFunctionStep(
            Guid.Parse("00000000-0000-0000-0000-00000000bf1b"), 7, "credential_fail_lease",
            new Dictionary<string, string> { ["failure_code"] = "step_failure" },
            new[] { new AbstractFunctionInputBinding("credential_lease", "result_context", "credential_lease", false, false) },
            null, true, true);
        var authorityBindings = new[]
        {
            new AbstractFunctionAuthorityBinding("policy", "external_credential_vault_refresh", true),
            new AbstractFunctionAuthorityBinding("table",  "topology.external_credential_vaults", true)
        };
        return new AbstractFunctionManifest(
            manifestId, "credential.refresh_token", "external_port_runtime", "external_credential_vault_refresh",
            new[] { bf15, bf16, bf17, bf18, bf19, bf1a, bf1b },
            new[] { "credential", "credential_payload", "decrypted_payload", "plaintext_payload", "decrypted_credential_payload", "token_response", "token_body" },
            true, authorityBindings);
    }

    private sealed class HardeningStaticManifestRepository : IAbstractFunctionManifestRepository
    {
        private readonly AbstractFunctionManifest _manifest;
        public HardeningStaticManifestRepository(AbstractFunctionManifest manifest) => _manifest = manifest;
        public Task<AbstractFunctionManifest?> LoadAsync(string functionKey, CancellationToken ct = default) =>
            Task.FromResult<AbstractFunctionManifest?>(_manifest);
    }

    private sealed class HardeningSpyVaultRepository : IExternalCredentialVaultRepository
    {
        private readonly Guid _vaultId;
        public bool AcquireWasCalled { get; private set; }
        public bool WriteWasCalled { get; private set; }
        public bool ReleaseWasCalled { get; private set; }
        public bool FailWasCalled { get; private set; }

        public HardeningSpyVaultRepository(Guid vaultId) => _vaultId = vaultId;

        public Task<ExternalCredentialVaultRecord?> LoadAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<ExternalCredentialVaultRecord?>(null);
        public Task<ExternalCredentialVaultRecord?> LoadByReferenceKeyAsync(string referenceKey, CancellationToken ct = default) =>
            Task.FromResult<ExternalCredentialVaultRecord?>(null);
        public Task<ExternalCredentialVaultRecord?> LoadByProviderAndBundleAsync(string providerKind, string requiredByBundle, CancellationToken ct = default) =>
            Task.FromResult<ExternalCredentialVaultRecord?>(null);

        public Task<ExternalCredentialRefreshLease?> AcquireRefreshLeaseAsync(Guid credentialVaultId, string leaseOwner, TimeSpan leaseDuration, DateTimeOffset now, CancellationToken ct = default)
        {
            AcquireWasCalled = true;
            return Task.FromResult<ExternalCredentialRefreshLease?>(
                new ExternalCredentialRefreshLease(Guid.NewGuid(), credentialVaultId, leaseOwner, now.Add(leaseDuration), 1));
        }

        public Task WriteEncryptedCredentialPayloadAsync(Guid credentialVaultId, int expectedVersion, byte[] encryptedPayload, string tokenHash, DateTimeOffset expiresAt, CancellationToken ct = default)
        {
            WriteWasCalled = true;
            return Task.CompletedTask;
        }

        public Task ReleaseRefreshLeaseAsync(ExternalCredentialRefreshLease lease, CancellationToken ct = default)
        {
            ReleaseWasCalled = true;
            return Task.CompletedTask;
        }

        public Task FailRefreshLeaseAsync(ExternalCredentialRefreshLease lease, string failureCode, CancellationToken ct = default)
        {
            FailWasCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class HardeningSuccessHttpClient : IExternalPortHttpClient
    {
        private readonly string _responseBody;
        public HardeningSuccessHttpClient(string responseBody) => _responseBody = responseBody;
        public Task<ExternalPortHttpResponse> SendAsync(ExternalPortHttpRequest request, CancellationToken ct = default) =>
            Task.FromResult(new ExternalPortHttpResponse(200, _responseBody));
    }

    private sealed class HardeningNon2xxHttpClient : IExternalPortHttpClient
    {
        private readonly int _statusCode;
        public HardeningNon2xxHttpClient(int statusCode) => _statusCode = statusCode;
        public Task<ExternalPortHttpResponse> SendAsync(ExternalPortHttpRequest request, CancellationToken ct = default) =>
            Task.FromResult(new ExternalPortHttpResponse(_statusCode, "error"));
    }

    private sealed class HardeningFakeCredentialCrypto : IExternalCredentialCrypto
    {
        public string DecryptForRuntimeUse(byte[] encryptedPayload, string encryptionKeyReference) => "plaintext";
        public byte[] EncryptForVaultStorage(string plaintextPayload, string encryptionKeyReference) =>
            System.Text.Encoding.UTF8.GetBytes(plaintextPayload);
        public string ComputeTokenHash(string plaintextPayload) => $"sha256:{plaintextPayload.GetHashCode():x8}";
    }

    private sealed class HardeningNullVaultRepository : IExternalCredentialVaultRepository
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

    private sealed class HardeningThrowingHttpClient : IExternalPortHttpClient
    {
        public Task<ExternalPortHttpResponse> SendAsync(ExternalPortHttpRequest request, CancellationToken ct = default) =>
            throw new InvalidOperationException("should not reach HTTP client");
    }
}
