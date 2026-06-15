using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Tests for SecretCredentialBundleRuntime (secret_credential_runtime manifest dispatch handler).
///
/// SSOT completion conditions (docs/design/runtime-bundle-secret-credential-ssot.yaml):
///   - registration persists credential reference only (no real values)
///   - secret values absent from response / event log / audit log / DB reference table
///   - validation failure is explicit structured error
///   - secret store unavailable is fail-close (no silent fallback)
///   - unknown / missing credential reference is explicit error
///   - IDispatchableRuntime boundary operates correctly
///   - handler registration is reachable from runtime destination
///   - SSOT vocabulary / runtime boundary not violated
/// </summary>
public class SecretCredentialBundleRuntimeTests
{
    // ─── registration: reference-only persistence ─────────────────────────────

    [Fact]
    public async Task Register_ValidPayload_PersistsReferenceKeyAndProviderKindOnly()
    {
        var repo = new InMemoryCredentialReferenceRepository();
        var runtime = BuildRuntime(repo, new AlwaysAvailableCredentialStore());
        var payload = BuildPayload(new { reference_key = "smtp_api_key", provider_kind = "env_var" });
        var request = new EndpointRequestDto("Create", "credential", "config", "register", null, payload, null);

        var response = await runtime.ExecuteAsync(request, manifestId: null);

        Assert.True(response.Success);
        Assert.NotNull(response.Emission);

        var registered = repo.AllRegistered.Single();
        Assert.Equal("smtp_api_key", registered.ReferenceKey);
        Assert.Equal("env_var", registered.ProviderKind);
        Assert.Equal("registered", registered.Status);
    }

    [Fact]
    public async Task Register_ResponseContainsNoCredentialValues()
    {
        var repo = new InMemoryCredentialReferenceRepository();
        var runtime = BuildRuntime(repo, new AlwaysAvailableCredentialStore());
        var payload = BuildPayload(new { reference_key = "stripe_secret_key_ref", provider_kind = "env_var" });
        var request = new EndpointRequestDto("Create", "credential", "config", "register", null, payload, null);

        var response = await runtime.ExecuteAsync(request, manifestId: null);

        Assert.True(response.Success);
        var rawJson = response.Emission?.Data?.GetRawText() ?? "";

        // response must not contain any credential-value-like fields
        Assert.DoesNotContain("secret_value", rawJson);
        Assert.DoesNotContain("api_key_value", rawJson);
        Assert.DoesNotContain("connection_string", rawJson);
        Assert.DoesNotContain("private_key", rawJson);
        Assert.DoesNotContain("password", rawJson);

        // must contain reference-safe fields only
        Assert.Contains("reference_key", rawJson);
        Assert.Contains("provider_kind", rawJson);
        Assert.Contains("status", rawJson);
    }

    [Fact]
    public async Task Register_AuditLogContainsNoCredentialValues()
    {
        var repo = new InMemoryCredentialReferenceRepository();
        var runtime = BuildRuntime(repo, new AlwaysAvailableCredentialStore());
        var payload = BuildPayload(new { reference_key = "webhook_signing_key_ref", provider_kind = "env_var" });
        var request = new EndpointRequestDto("Create", "credential", "config", "register", null, payload, null);

        await runtime.ExecuteAsync(request, manifestId: null);

        // After register we have a credential reference; now validate to produce an audit event
        var validatePayload = BuildPayload(new { reference_key = "webhook_signing_key_ref" });
        var validateRequest = new EndpointRequestDto("Search", "credential", "config", "validate", null, validatePayload, null);
        await runtime.ExecuteAsync(validateRequest, manifestId: null);

        var auditEvents = repo.AuditEvents;
        Assert.NotEmpty(auditEvents);

        foreach (var ev in auditEvents)
        {
            // audit events must contain reference identifier and logical key — not credential values
            Assert.NotEqual(Guid.Empty, ev.CredentialReferenceId);
            Assert.Equal("webhook_signing_key_ref", ev.ReferenceKey);

            // serialize the whole record to detect if any secret-looking content slipped in
            var serialized = JsonSerializer.Serialize(ev);
            Assert.DoesNotContain("secret_value", serialized);
            Assert.DoesNotContain("password", serialized);
            Assert.DoesNotContain("private_key", serialized);
        }
    }

    [Fact]
    public async Task Register_MissingReferenceKey_ReturnsExplicitError()
    {
        var runtime = BuildRuntime(new InMemoryCredentialReferenceRepository(), new AlwaysAvailableCredentialStore());
        var payload = BuildPayload(new { provider_kind = "env_var" });
        var request = new EndpointRequestDto("Create", "credential", "config", "register", null, payload, null);

        var response = await runtime.ExecuteAsync(request, manifestId: null);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "CREDENTIAL_REFERENCE_KEY_REQUIRED");
    }

    [Fact]
    public async Task Register_MissingProviderKind_ReturnsExplicitError()
    {
        var runtime = BuildRuntime(new InMemoryCredentialReferenceRepository(), new AlwaysAvailableCredentialStore());
        var payload = BuildPayload(new { reference_key = "some_key" });
        var request = new EndpointRequestDto("Create", "credential", "config", "register", null, payload, null);

        var response = await runtime.ExecuteAsync(request, manifestId: null);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "CREDENTIAL_PROVIDER_KIND_REQUIRED");
    }

    [Fact]
    public async Task Register_DuplicateReferenceKey_ReturnsExplicitConflictError()
    {
        var repo = new InMemoryCredentialReferenceRepository();
        var runtime = BuildRuntime(repo, new AlwaysAvailableCredentialStore());
        var payload = BuildPayload(new { reference_key = "dup_key", provider_kind = "env_var" });
        var request = new EndpointRequestDto("Create", "credential", "config", "register", null, payload, null);

        await runtime.ExecuteAsync(request, manifestId: null);
        var response = await runtime.ExecuteAsync(request, manifestId: null);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "CREDENTIAL_REFERENCE_KEY_CONFLICT");
    }

    [Fact]
    public async Task Register_StoreUnavailable_FailsClose_NoDB()
    {
        var repo = new InMemoryCredentialReferenceRepository();
        var runtime = BuildRuntime(repo, new StoreUnavailableCredentialStore());
        var payload = BuildPayload(new { reference_key = "reg_unavail_key", provider_kind = "env_var" });
        var request = new EndpointRequestDto("Create", "credential", "config", "register", null, payload, null);

        var response = await runtime.ExecuteAsync(request, manifestId: null);

        // store unavailable at registration → fail-close, no DB entry written
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "SECRET_STORE_UNAVAILABLE");
        Assert.Empty(repo.AllRegistered);
    }

    [Fact]
    public async Task Register_StoreBindingFailed_ExplicitError_NoDB()
    {
        var repo = new InMemoryCredentialReferenceRepository();
        var runtime = BuildRuntime(repo, new StoreBindingFailedCredentialStore());
        var payload = BuildPayload(new { reference_key = "reg_binding_fail_key", provider_kind = "unsupported_provider" });
        var request = new EndpointRequestDto("Create", "credential", "config", "register", null, payload, null);

        var response = await runtime.ExecuteAsync(request, manifestId: null);

        // store rejects binding → explicit error, no DB entry written
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "CREDENTIAL_STORE_BINDING_FAILED");
        Assert.Empty(repo.AllRegistered);
    }

    [Fact]
    public async Task Register_AppendsRegistrationAuditEvent()
    {
        var repo = new InMemoryCredentialReferenceRepository();
        var runtime = BuildRuntime(repo, new AlwaysAvailableCredentialStore());
        var payload = BuildPayload(new { reference_key = "audit_reg_key", provider_kind = "env_var" });
        var request = new EndpointRequestDto("Create", "credential", "config", "register", null, payload, null);

        var response = await runtime.ExecuteAsync(request, manifestId: null);

        Assert.True(response.Success);
        var regEvent = repo.AuditEvents.FirstOrDefault(e => e.EventType == "credential_registered");
        Assert.NotNull(regEvent);
        Assert.Equal("audit_reg_key", regEvent!.ReferenceKey);
        Assert.Equal("success", regEvent.Outcome);
        Assert.NotEqual(Guid.Empty, regEvent.CredentialReferenceId);
    }

    // ─── validation failure: explicit structured error ────────────────────────

    [Fact]
    public async Task Validate_MissingCredentialInStore_ReturnsExplicitValidationError()
    {
        var repo = new InMemoryCredentialReferenceRepository();
        var runtime = BuildRuntime(repo, new CredentialAbsentStore());

        // register the reference first
        await RegisterCredential(runtime, "sftp_host_key", "env_var");

        var payload = BuildPayload(new { reference_key = "sftp_host_key" });
        var request = new EndpointRequestDto("Search", "credential", "config", "validate", null, payload, null);

        var response = await runtime.ExecuteAsync(request, manifestId: null);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "CREDENTIAL_NOT_PRESENT_IN_STORE");
    }

    [Fact]
    public async Task Validate_ValidationFailure_IsExplicitStructuredError_NotSilentSuccess()
    {
        var repo = new InMemoryCredentialReferenceRepository();
        var runtime = BuildRuntime(repo, new CredentialAbsentStore());
        await RegisterCredential(runtime, "email_smtp_pass", "env_var");

        var payload = BuildPayload(new { reference_key = "email_smtp_pass" });
        var request = new EndpointRequestDto("Search", "credential", "config", "validate", null, payload, null);

        var response = await runtime.ExecuteAsync(request, manifestId: null);

        Assert.False(response.Success,
            "validation failure must produce Success=false, not silent Success=true");
        Assert.NotEmpty(response.Errors);
        Assert.All(response.Errors, e => Assert.False(string.IsNullOrWhiteSpace(e.Code)));
    }

    // ─── secret store unavailable: fail-close, no silent fallback ────────────

    [Fact]
    public async Task Validate_SecretStoreUnavailable_FailsClose_NoSilentFallback()
    {
        // Register with a working store, then validate when store becomes unavailable.
        var repo = new InMemoryCredentialReferenceRepository();
        var setupRuntime = BuildRuntime(repo, new AlwaysAvailableCredentialStore());
        await RegisterCredential(setupRuntime, "stripe_wh_secret", "env_var");

        var validateRuntime = BuildRuntime(repo, new StoreUnavailableCredentialStore());
        var payload = BuildPayload(new { reference_key = "stripe_wh_secret" });
        var request = new EndpointRequestDto("Search", "credential", "config", "validate", null, payload, null);

        var response = await validateRuntime.ExecuteAsync(request, manifestId: null);

        // store unavailable → must fail-close with explicit error, no silent fallback
        Assert.False(response.Success,
            "secret store unavailable must fail-close with Success=false");
        Assert.Contains(response.Errors, e => e.Code == "SECRET_STORE_UNAVAILABLE");
    }

    [Fact]
    public async Task Validate_SecretStoreUnavailable_DoesNotReturnCredentialValue()
    {
        // Register with a working store, then validate when store becomes unavailable.
        var repo = new InMemoryCredentialReferenceRepository();
        var setupRuntime = BuildRuntime(repo, new AlwaysAvailableCredentialStore());
        await RegisterCredential(setupRuntime, "cached_fallback_guard", "env_var");

        var validateRuntime = BuildRuntime(repo, new StoreUnavailableCredentialStore());
        var payload = BuildPayload(new { reference_key = "cached_fallback_guard" });
        var request = new EndpointRequestDto("Search", "credential", "config", "validate", null, payload, null);

        var response = await validateRuntime.ExecuteAsync(request, manifestId: null);

        // even in failure, no credential values in response
        var responseJson = JsonSerializer.Serialize(response);
        Assert.DoesNotContain("cached_value", responseJson);
        Assert.DoesNotContain("fallback_credential", responseJson);
        Assert.DoesNotContain("secret_value", responseJson);
    }

    // ─── unknown / missing credential reference: explicit error ──────────────

    [Fact]
    public async Task Validate_MissingCredentialReference_ReturnsExplicitError()
    {
        var runtime = BuildRuntime(new InMemoryCredentialReferenceRepository(), new AlwaysAvailableCredentialStore());

        var payload = BuildPayload(new { reference_key = "nonexistent_ref" });
        var request = new EndpointRequestDto("Search", "credential", "config", "validate", null, payload, null);

        var response = await runtime.ExecuteAsync(request, manifestId: null);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "CREDENTIAL_REFERENCE_NOT_FOUND");
    }

    [Fact]
    public async Task Validate_UnknownAction_ReturnsExplicitError()
    {
        var runtime = BuildRuntime(new InMemoryCredentialReferenceRepository(), new AlwaysAvailableCredentialStore());
        var request = new EndpointRequestDto("Search", "credential", "config", "unknown_action", null, null, null);

        var response = await runtime.ExecuteAsync(request, manifestId: null);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "CREDENTIAL_ACTION_UNKNOWN");
    }

    // ─── IDispatchableRuntime boundary ───────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ImplementsIDispatchableRuntime()
    {
        IDispatchableRuntime runtime = BuildRuntime(
            new InMemoryCredentialReferenceRepository(),
            new AlwaysAvailableCredentialStore());

        // IDispatchableRuntime.ExecuteAsync must be callable with null manifestId
        var payload = BuildPayload(new { reference_key = "boundary_test_ref", provider_kind = "env_var" });
        var request = new EndpointRequestDto("Create", "credential", "config", "register", null, payload, null);

        var response = await runtime.ExecuteAsync(request, manifestId: null);

        Assert.True(response.Success);
    }

    [Fact]
    public async Task ExecuteAsync_WithManifestId_StillRoutesCorrectly()
    {
        IDispatchableRuntime runtime = BuildRuntime(
            new InMemoryCredentialReferenceRepository(),
            new AlwaysAvailableCredentialStore());

        var manifestId = Guid.NewGuid();
        var payload = BuildPayload(new { reference_key = "manifest_wired_ref", provider_kind = "env_var" });
        var request = new EndpointRequestDto("Create", "credential", "config", "register", null, payload, null);

        var response = await runtime.ExecuteAsync(request, manifestId);

        Assert.True(response.Success);
    }

    // ─── handler registration reachable from runtime destination ─────────────

    [Fact]
    public async Task ManifestDispatcher_SecretCredentialRuntime_RoutesToRegisteredHandler()
    {
        // Prove that ManifestDispatcher selects the secret_credential_runtime handler
        // when manifest runtime_mapping.runtime_destination = "secret_credential_runtime".
        var sentinel = JsonSerializer.SerializeToElement(new { dispatched = "secret_credential_dispatch_test" });
        var fakeHandler = new FakeDispatchableRuntime(sentinel);
        var manifestRepo = new StubManifestRepository(
            new ManifestRecord(
                Guid.NewGuid(),
                RelationRegistryId: null,
                Topology: BuildRuntimeMappingTopology("secret_credential_runtime"),
                Status: "active"));
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo,
            extraHandlers: new Dictionary<string, IDispatchableRuntime>
            {
                ["secret_credential_runtime"] = fakeHandler
            });
        var request = new EndpointRequestDto("Create", "credential", "config", "register", null, null, null);

        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success);
        Assert.True(fakeHandler.WasCalled,
            "secret_credential_runtime handler must be called when manifest runtime_destination matches.");
    }

    [Fact]
    public async Task ManifestDispatcher_UnknownDestination_StillReturnsRuntimeDestinationUnknown()
    {
        // Sanity: adding secret_credential_runtime handler must not break unknown-destination check.
        var repo = new InMemoryCredentialReferenceRepository();
        var credentialRuntime = BuildRuntime(repo, new AlwaysAvailableCredentialStore());

        var manifestRepo = new StubManifestRepository(
            new ManifestRecord(
                Guid.NewGuid(),
                RelationRegistryId: null,
                Topology: BuildRuntimeMappingTopology("nonexistent_destination_xyz"),
                Status: "active"));
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo,
            extraHandlers: new Dictionary<string, IDispatchableRuntime>
            {
                ["secret_credential_runtime"] = credentialRuntime
            });
        var request = new EndpointRequestDto("Create", "credential", "config", "register", null, null, null);

        var response = await dispatcher.DispatchAsync(request);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "RUNTIME_DESTINATION_UNKNOWN");
    }

    // ─── rotation: explicit admin action required ─────────────────────────────

    [Fact]
    public async Task Rotate_MissingActorId_ReturnsExplicitError()
    {
        var repo = new InMemoryCredentialReferenceRepository();
        var runtime = BuildRuntime(repo, new AlwaysAvailableCredentialStore());
        await RegisterCredential(runtime, "rotate_test_key", "env_var");

        // rotation without explicit actor_id must be rejected
        var payload = BuildPayload(new { reference_key = "rotate_test_key" });
        var request = new EndpointRequestDto("Update", "credential", "config", "rotate", null, payload, null);

        var response = await runtime.ExecuteAsync(request, manifestId: null);

        Assert.False(response.Success,
            "rotation without explicit rotation_actor_id must be rejected");
        Assert.Contains(response.Errors, e => e.Code == "CREDENTIAL_ROTATION_ACTOR_REQUIRED");
    }

    [Fact]
    public async Task Rotate_MissingReference_ReturnsExplicitError()
    {
        var runtime = BuildRuntime(new InMemoryCredentialReferenceRepository(), new AlwaysAvailableCredentialStore());

        // rotation_actor_id is the authenticated admin username (from JWT sub claim)
        var payload = BuildPayload(new { reference_key = "nonexistent_for_rotate", rotation_actor_id = "admin_user" });
        var request = new EndpointRequestDto("Update", "credential", "config", "rotate", null, payload, null);

        var response = await runtime.ExecuteAsync(request, manifestId: null);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "CREDENTIAL_REFERENCE_NOT_FOUND");
    }

    [Fact]
    public async Task Rotate_WithExplicitActor_RecordsRotationAndValidates()
    {
        var repo = new InMemoryCredentialReferenceRepository();
        var runtime = BuildRuntime(repo, new AlwaysAvailableCredentialStore());
        await RegisterCredential(runtime, "smtp_pass_to_rotate", "env_var");

        // rotation_actor_id is the authenticated admin username (from JWT sub claim)
        var payload = BuildPayload(new { reference_key = "smtp_pass_to_rotate", rotation_actor_id = "admin_user" });
        var request = new EndpointRequestDto("Update", "credential", "config", "rotate", null, payload, null);

        var response = await runtime.ExecuteAsync(request, manifestId: null);

        Assert.True(response.Success);

        // rotation audit event recorded
        var rotationEvent = repo.AuditEvents.FirstOrDefault(e => e.EventType == "credential_rotated");
        Assert.NotNull(rotationEvent);
        Assert.Equal("smtp_pass_to_rotate", rotationEvent!.ReferenceKey);
        Assert.Equal("success", rotationEvent.Outcome);

        // post-rotation validation audit event recorded
        var validationEvent = repo.AuditEvents.FirstOrDefault(e => e.EventType == "credential_validated");
        Assert.NotNull(validationEvent);

        // rotation response must not contain credential values
        var responseJson = JsonSerializer.Serialize(response);
        Assert.DoesNotContain("secret_value", responseJson);
        Assert.DoesNotContain("password", responseJson);
        Assert.DoesNotContain("private_key", responseJson);
    }

    [Fact]
    public async Task Rotate_NewCredentialNotInStore_ExplicitValidationError()
    {
        // Setup: register with an always-available store
        var repo = new InMemoryCredentialReferenceRepository();
        var setupRuntime = BuildRuntime(repo, new AlwaysAvailableCredentialStore());
        await RegisterCredential(setupRuntime, "post_rotate_absent_key", "env_var");

        // Rotation attempt with a store where the new credential is absent
        var rotateRuntime = BuildRuntime(repo, new CredentialAbsentStore());
        var payload = BuildPayload(new { reference_key = "post_rotate_absent_key", rotation_actor_id = "admin_user" });
        var request = new EndpointRequestDto("Update", "credential", "config", "rotate", null, payload, null);

        var response = await rotateRuntime.ExecuteAsync(request, manifestId: null);

        // Rotation was recorded, but post-rotation validation must fail explicitly
        Assert.False(response.Success,
            "rotation where new credential is not in store must fail with explicit validation error");
        Assert.Contains(response.Errors, e => e.Code == "CREDENTIAL_NOT_PRESENT_IN_STORE");
    }

    // ─── list: reference metadata only ───────────────────────────────────────

    [Fact]
    public async Task List_ReturnsReferenceMetadataOnly()
    {
        var repo = new InMemoryCredentialReferenceRepository();
        var runtime = BuildRuntime(repo, new AlwaysAvailableCredentialStore());
        await RegisterCredential(runtime, "list_ref_1", "env_var");
        await RegisterCredential(runtime, "list_ref_2", "env_var");

        var request = new EndpointRequestDto("Search", "credential", "config", "list", null, null, null);
        var response = await runtime.ExecuteAsync(request, manifestId: null);

        Assert.True(response.Success);
        var listJson = response.Emission?.Data?.GetRawText() ?? "";
        Assert.Contains("list_ref_1", listJson);
        Assert.Contains("list_ref_2", listJson);

        // list must not contain credential values
        Assert.DoesNotContain("secret_value", listJson);
        Assert.DoesNotContain("password", listJson);
        Assert.DoesNotContain("private_key", listJson);
        Assert.DoesNotContain("api_key_value", listJson);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    private static SecretCredentialBundleRuntime BuildRuntime(
        CredentialReferenceRepository repo,
        ICredentialStore store)
    {
        var validationService = new CredentialValidationService(
            NullLogger<CredentialValidationService>.Instance,
            store,
            repo);
        return new SecretCredentialBundleRuntime(
            NullLogger<SecretCredentialBundleRuntime>.Instance,
            store,
            repo,
            validationService);
    }

    private static async Task RegisterCredential(
        SecretCredentialBundleRuntime runtime,
        string referenceKey,
        string providerKind)
    {
        var payload = BuildPayload(new { reference_key = referenceKey, provider_kind = providerKind });
        var request = new EndpointRequestDto("Create", "credential", "config", "register", null, payload, null);
        var response = await runtime.ExecuteAsync(request, manifestId: null);
        Assert.True(response.Success, $"Setup: register {referenceKey} failed: {string.Join(", ", response.Errors.Select(e => e.Code))}");
    }

    private static JsonElement BuildPayload(object data) =>
        JsonSerializer.SerializeToElement(data);

    private static IReadOnlyList<JsonElement> BuildRuntimeMappingTopology(string runtimeDestination)
    {
        var entry = JsonSerializer.SerializeToElement(new
        {
            type = "runtime_mapping",
            runtime_destination = runtimeDestination
        });
        return [entry];
    }
}

// ─── In-memory test doubles ───────────────────────────────────────────────────

/// <summary>
/// In-memory credential reference repository for unit tests.
/// Verifies that only reference metadata is stored — no credential values.
/// </summary>
internal sealed class InMemoryCredentialReferenceRepository : CredentialReferenceRepository
{
    private readonly Dictionary<string, CredentialReferenceDto> _store = new();
    private readonly List<CredentialAuditEventRecord> _auditLog = [];

    public IReadOnlyList<CredentialReferenceDto> AllRegistered => _store.Values.ToList();
    public IReadOnlyList<CredentialAuditEventRecord> AuditEvents => _auditLog;

    public InMemoryCredentialReferenceRepository() : base("in-memory") { }

    public override Task<CredentialReferenceDto> RegisterAsync(
        string referenceKey, string providerKind, CancellationToken ct = default)
    {
        if (_store.ContainsKey(referenceKey))
            throw new InvalidOperationException($"Duplicate reference_key: '{referenceKey}'.");

        var dto = new CredentialReferenceDto(
            CredentialReferenceId: Guid.NewGuid(),
            ReferenceKey: referenceKey,
            ProviderKind: providerKind,
            Status: "registered",
            ValidationStatus: "unchecked",
            LastValidatedAt: null,
            LastRotatedAt: null,
            RegisteredAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);
        _store[referenceKey] = dto;
        return Task.FromResult(dto);
    }

    public override Task<CredentialReferenceDto?> GetByKeyAsync(
        string referenceKey, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(referenceKey, out var dto) ? dto : null);

    public override Task<IReadOnlyList<CredentialReferenceDto>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CredentialReferenceDto>>(_store.Values.ToList());

    public override Task<bool> UpdateValidationStatusAsync(
        string referenceKey, string validationStatus, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(referenceKey, out var existing)) return Task.FromResult(false);
        _store[referenceKey] = existing with
        {
            ValidationStatus = validationStatus,
            Status = validationStatus == "valid" ? "active" : existing.Status,
            LastValidatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        return Task.FromResult(true);
    }

    public override Task<bool> UpdateRotationTimestampAsync(
        string referenceKey, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(referenceKey, out var existing)) return Task.FromResult(false);
        _store[referenceKey] = existing with
        {
            LastRotatedAt = DateTimeOffset.UtcNow,
            Status = "active",
            ValidationStatus = "unchecked",
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        return Task.FromResult(true);
    }

    public override Task AppendAuditEventAsync(
        CredentialAuditEventRecord record, CancellationToken ct = default)
    {
        _auditLog.Add(record);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Fake credential store where the store is always available and credentials are always present.
/// </summary>
internal sealed class AlwaysAvailableCredentialStore : ICredentialStore
{
    public Task<CredentialStoreResult> GetAsync(string referenceKey, CancellationToken ct = default) =>
        Task.FromResult(new CredentialStoreResult(
            StoreAvailable: true,
            CredentialPresent: true,
            FailureReason: null));

    public Task<CredentialStoreSetResult> SetAsync(string referenceKey, string providerKind, CancellationToken ct = default) =>
        Task.FromResult(new CredentialStoreSetResult(
            Success: true,
            StoreAvailable: true,
            FailureReason: null));
}

/// <summary>
/// Fake credential store where store is available but credential is not present.
/// Used to verify that missing credentials produce explicit validation errors.
/// </summary>
internal sealed class CredentialAbsentStore : ICredentialStore
{
    public Task<CredentialStoreResult> GetAsync(string referenceKey, CancellationToken ct = default) =>
        Task.FromResult(new CredentialStoreResult(
            StoreAvailable: true,
            CredentialPresent: false,
            FailureReason: $"Environment variable '{referenceKey}' is not set."));

    public Task<CredentialStoreSetResult> SetAsync(string referenceKey, string providerKind, CancellationToken ct = default) =>
        Task.FromResult(new CredentialStoreSetResult(
            Success: true,
            StoreAvailable: true,
            FailureReason: null));
}

/// <summary>
/// Fake credential store where the store itself is unavailable.
/// Used to verify fail-close behavior — no silent fallback to cached credentials.
/// </summary>
internal sealed class StoreUnavailableCredentialStore : ICredentialStore
{
    public Task<CredentialStoreResult> GetAsync(string referenceKey, CancellationToken ct = default) =>
        Task.FromResult(new CredentialStoreResult(
            StoreAvailable: false,
            CredentialPresent: false,
            FailureReason: "Secret store is unavailable (simulated for test)."));

    public Task<CredentialStoreSetResult> SetAsync(string referenceKey, string providerKind, CancellationToken ct = default) =>
        Task.FromResult(new CredentialStoreSetResult(
            Success: false,
            StoreAvailable: false,
            FailureReason: "Secret store is unavailable (simulated for test)."));
}

/// <summary>
/// Fake credential store where the store is available but rejects the binding for the given provider kind.
/// Used to verify that registration binding failures produce explicit structured errors.
/// </summary>
internal sealed class StoreBindingFailedCredentialStore : ICredentialStore
{
    public Task<CredentialStoreResult> GetAsync(string referenceKey, CancellationToken ct = default) =>
        Task.FromResult(new CredentialStoreResult(
            StoreAvailable: true,
            CredentialPresent: false,
            FailureReason: "Credential not present."));

    public Task<CredentialStoreSetResult> SetAsync(string referenceKey, string providerKind, CancellationToken ct = default) =>
        Task.FromResult(new CredentialStoreSetResult(
            Success: false,
            StoreAvailable: true,
            FailureReason: "PROVIDER_KIND_NOT_SUPPORTED_BY_STORE"));
}
