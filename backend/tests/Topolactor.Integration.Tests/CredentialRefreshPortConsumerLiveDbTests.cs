using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live DB proof for the credential refresh dispatch chain.
/// Reads the seeded external_credential_vault_refresh port record and policy from DB
/// via NpgsqlExternalPortPolicyRepository, verifies execute_abstract_function routes
/// to credential.refresh_token manifest (loaded by NpgsqlAbstractFunctionManifestRepository),
/// and runs ExternalPortPolicyStepExecutor.ExecutePolicyAsync as the entry point.
/// Uses spy/fake adapters for vault operations and HTTP; does not issue real network calls.
/// TOPOLACTOR_TEST_DB_CONNECTION unset means explicit local skip.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class CredentialRefreshPortConsumerLiveDbTests
{
    private const string PortId = "00000000-0000-0000-0000-000000000f10";

    [Fact]
    public async Task SeededCredentialRefreshPolicy_ProjectsPortRecordPolicyAndManifestFromDb()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var repo = new NpgsqlExternalPortPolicyRepository(NullLogger<NpgsqlExternalPortPolicyRepository>.Instance, cs);
        var manifestRepo = new NpgsqlAbstractFunctionManifestRepository(cs);

        var port = await repo.LoadPortRecordAsync("external_credential_vault_refresh", "access_port", null);
        Assert.NotNull(port);
        Assert.Equal(Guid.Parse(PortId), port.PortId);
        Assert.Equal("access_port", port.PortKind);
        Assert.Equal("external_credential_vault_refresh", port.RequiredByBundle);
        Assert.Equal("oauth_refresh", port.ProviderKind);
        Assert.Equal("external", port.CredentialKind);

        var policy = await repo.LoadPolicyAsync(port);
        Assert.NotNull(policy);
        Assert.Equal("external_credential_vault_refresh", policy.RequiredByBundle);
        Assert.Equal(6, policy.PolicySteps.Count);

        var steps = policy.PolicySteps.OrderBy(s => s.StepOrder).ToList();
        var step5 = steps[4];
        Assert.Equal("execute_abstract_function", step5.OperationKey);
        Assert.Equal("credential.refresh_token", step5.StepConfig["abstract_function_key"]);

        var manifest = await manifestRepo.LoadAsync("credential.refresh_token");
        Assert.NotNull(manifest);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-00000000af10"), manifest.AbstractFunctionId);
        Assert.Equal("external_port_runtime", manifest.RuntimeLane);
        Assert.Equal("external_credential_vault_refresh", manifest.AuthorityScope);
        Assert.NotNull(manifest.AuthorityBindings);
        Assert.Contains(manifest.AuthorityBindings, b => b.AuthorityKind == "policy" && b.AuthorityRef == "external_credential_vault_refresh" && b.Active);
        Assert.Contains(manifest.AuthorityBindings, b => b.AuthorityKind == "table" && b.AuthorityRef == "topology.external_credential_vaults" && b.Active);

        var allSteps = manifest.Steps.OrderBy(s => s.StepOrder).ToList();
        Assert.Equal(7, allSteps.Count);

        var normalSteps = allSteps.Where(static s => !s.IsCompensationStep).OrderBy(static s => s.StepOrder).ToList();
        Assert.Equal(6, normalSteps.Count);
        Assert.Equal("credential_acquire_lease",      normalSteps[0].PrimitiveKey);
        Assert.Equal("credential_http_request",       normalSteps[1].PrimitiveKey);
        Assert.Equal("credential_compute_token_hash", normalSteps[2].PrimitiveKey);
        Assert.Equal("credential_parse_expires_at",   normalSteps[3].PrimitiveKey);
        Assert.Equal("credential_write_vault",        normalSteps[4].PrimitiveKey);
        Assert.Equal("credential_release_lease",      normalSteps[5].PrimitiveKey);

        var compensationStep = allSteps.Single(static s => s.IsCompensationStep);
        Assert.Equal("credential_fail_lease", compensationStep.PrimitiveKey);
        Assert.Equal("step_failure", compensationStep.StepConfig["failure_code"]);
        Assert.True(compensationStep.IsCompensationStep);
    }

    [Fact]
    public async Task SeededCredentialRefreshPolicy_DispatchesViaExternalPortPolicyStepExecutor_SuccessPath()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var repo = new NpgsqlExternalPortPolicyRepository(NullLogger<NpgsqlExternalPortPolicyRepository>.Instance, cs);
        var manifestRepo = new NpgsqlAbstractFunctionManifestRepository(cs);

        var port = await repo.LoadPortRecordAsync("external_credential_vault_refresh", "access_port", null);
        Assert.NotNull(port);
        var policy = await repo.LoadPolicyAsync(port);
        Assert.NotNull(policy);

        var vaultId = Guid.NewGuid();
        var vaultRecord = new ExternalCredentialVaultRecord(
            vaultId, "oauth_refresh", "external_credential_vault_refresh", "refresh",
            null, new byte[] { 1, 2, 3 }, "key-ref", null, 300, 1, null, true);

        var spyVault = new SpyVaultRepository(vaultId);
        var successHttp = new SuccessHttpClient(@"{""access_token"":""tok"",""expires_at"":""2027-06-20T00:00:00Z""}");
        var crypto = new FakeCredentialCrypto();
        var fakeLog = new FakeEventLogRepository();
        var credResolver = new StaticCredentialReferenceResolver(vaultRecord);

        var abstractExecutor = new AbstractFunctionExecutor(manifestRepo, new IAbstractFunctionPrimitiveAdapter[]
        {
            new CredentialAcquireLeaseAdapter(spyVault),
            new CredentialHttpRequestAdapter(successHttp),
            new CredentialComputeTokenHashAdapter(crypto),
            new CredentialParseExpiresAtAdapter(),
            new CredentialWriteVaultAdapter(spyVault, crypto),
            new CredentialReleaseLeaseAdapter(spyVault),
            new CredentialFailLeaseAdapter(spyVault)
        });

        var policyExecutor = new ExternalPortPolicyStepExecutor(
            credentialReferenceResolver: credResolver,
            crypto: crypto,
            abstractFunctionExecutor: abstractExecutor,
            runtimeEventLogRepository: fakeLog);

        var context = new ExternalPortExecutionContext
        {
            PortRecord = port,
            RequiredByBundle = "external_credential_vault_refresh",
            PortKind = "access_port"
        };

        await policyExecutor.ExecutePolicyAsync(policy, context);

        Assert.True(spyVault.AcquireWasCalled, "lease must be acquired via credential_acquire_lease");
        Assert.True(spyVault.WriteWasCalled, "vault write must be called on success path");
        Assert.True(spyVault.ReleaseWasCalled, "lease release must be called on success path");
        Assert.False(spyVault.FailWasCalled, "FailRefreshLease must NOT be called on success path");

        Assert.Contains("execute_abstract_function", context.ExecutedOperationKeys);
        Assert.Contains("append_runtime_event_log", context.ExecutedOperationKeys);
    }

    [Fact]
    public async Task SeededCredentialRefreshPolicy_DispatchesViaExternalPortPolicyStepExecutor_Non2xxTriggersCompensation()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var repo = new NpgsqlExternalPortPolicyRepository(NullLogger<NpgsqlExternalPortPolicyRepository>.Instance, cs);
        var manifestRepo = new NpgsqlAbstractFunctionManifestRepository(cs);

        var port = await repo.LoadPortRecordAsync("external_credential_vault_refresh", "access_port", null);
        Assert.NotNull(port);
        var policy = await repo.LoadPolicyAsync(port);
        Assert.NotNull(policy);

        var vaultId = Guid.NewGuid();
        var vaultRecord = new ExternalCredentialVaultRecord(
            vaultId, "oauth_refresh", "external_credential_vault_refresh", "refresh",
            null, new byte[] { 1, 2, 3 }, "key-ref", null, 300, 1, null, true);

        var spyVault = new SpyVaultRepository(vaultId);
        var non2xxHttp = new Non2xxHttpClient(401);
        var crypto = new FakeCredentialCrypto();
        var fakeLog = new FakeEventLogRepository();
        var credResolver = new StaticCredentialReferenceResolver(vaultRecord);

        var abstractExecutor = new AbstractFunctionExecutor(manifestRepo, new IAbstractFunctionPrimitiveAdapter[]
        {
            new CredentialAcquireLeaseAdapter(spyVault),
            new CredentialHttpRequestAdapter(non2xxHttp),
            new CredentialComputeTokenHashAdapter(crypto),
            new CredentialParseExpiresAtAdapter(),
            new CredentialWriteVaultAdapter(spyVault, crypto),
            new CredentialReleaseLeaseAdapter(spyVault),
            new CredentialFailLeaseAdapter(spyVault)
        });

        var policyExecutor = new ExternalPortPolicyStepExecutor(
            credentialReferenceResolver: credResolver,
            crypto: crypto,
            abstractFunctionExecutor: abstractExecutor,
            runtimeEventLogRepository: fakeLog);

        var context = new ExternalPortExecutionContext
        {
            PortRecord = port,
            RequiredByBundle = "external_credential_vault_refresh",
            PortKind = "access_port"
        };

        await Assert.ThrowsAnyAsync<Exception>(
            () => policyExecutor.ExecutePolicyAsync(policy, context));

        Assert.True(spyVault.AcquireWasCalled, "lease must have been acquired before HTTP failure");
        Assert.True(spyVault.FailWasCalled, "compensation must call FailRefreshLease after non-2xx HTTP step");
        Assert.False(spyVault.WriteWasCalled, "vault write must NOT be called after HTTP failure");
        Assert.False(spyVault.ReleaseWasCalled, "ReleaseRefreshLease must NOT be called when step failed");
    }

    private static string? GetConnectionString()
    {
        var cs = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
                throw new InvalidOperationException(
                    "TOPOLACTOR_TEST_DB_CONNECTION is required when TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1.");
            return null;
        }
        return cs;
    }

    private sealed class SpyVaultRepository : IExternalCredentialVaultRepository
    {
        private readonly Guid _vaultId;
        public bool AcquireWasCalled { get; private set; }
        public bool WriteWasCalled { get; private set; }
        public bool ReleaseWasCalled { get; private set; }
        public bool FailWasCalled { get; private set; }

        public SpyVaultRepository(Guid vaultId) => _vaultId = vaultId;

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

    private sealed class StaticCredentialReferenceResolver : IExternalPortCredentialReferenceResolver
    {
        private readonly ExternalCredentialVaultRecord _vaultRecord;
        public StaticCredentialReferenceResolver(ExternalCredentialVaultRecord vaultRecord) => _vaultRecord = vaultRecord;
        public Task<ExternalCredentialVaultRecord?> ResolveCredentialReferenceAsync(ExternalPortRecord portRecord, CancellationToken ct = default) =>
            Task.FromResult<ExternalCredentialVaultRecord?>(_vaultRecord);
    }

    private sealed class SuccessHttpClient : IExternalPortHttpClient
    {
        private readonly string _responseBody;
        public SuccessHttpClient(string responseBody) => _responseBody = responseBody;
        public Task<ExternalPortHttpResponse> SendAsync(ExternalPortHttpRequest request, CancellationToken ct = default) =>
            Task.FromResult(new ExternalPortHttpResponse(200, _responseBody));
    }

    private sealed class Non2xxHttpClient : IExternalPortHttpClient
    {
        private readonly int _statusCode;
        public Non2xxHttpClient(int statusCode) => _statusCode = statusCode;
        public Task<ExternalPortHttpResponse> SendAsync(ExternalPortHttpRequest request, CancellationToken ct = default) =>
            Task.FromResult(new ExternalPortHttpResponse(_statusCode, "error"));
    }

    private sealed class FakeCredentialCrypto : IExternalCredentialCrypto
    {
        public string DecryptForRuntimeUse(byte[] encryptedPayload, string encryptionKeyReference) =>
            @"{""refresh_token"":""rt-test-token""}";
        public byte[] EncryptForVaultStorage(string plaintextPayload, string encryptionKeyReference) =>
            System.Text.Encoding.UTF8.GetBytes(plaintextPayload);
        public string ComputeTokenHash(string plaintextPayload) =>
            $"sha256:{plaintextPayload.GetHashCode():x8}";
    }

    private sealed class FakeEventLogRepository : IExternalPortRuntimeEventLogRepository
    {
        public Task AppendAsync(string eventType, string? entityId, string? requiredByBundle, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
