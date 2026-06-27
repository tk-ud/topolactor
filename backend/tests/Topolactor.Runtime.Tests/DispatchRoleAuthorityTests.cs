using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Topolactor.Endpoint;
using Topolactor.Guard;
using Topolactor.Repository;
using Topolactor.Schema;
using Topolactor.Runtime;
using Xunit;

namespace Topolactor.Runtime.Tests;

[Collection("env-sequential")]
public class DispatchRoleAuthorityTests
{
    [Fact]
    public async Task JwtRoleClaim_IsAuthoritative_OverBodyAndContextRole()
    {
        const string secret = "dispatch-role-authority-test-secret";
        using var env = new EnvScope( "DEMO_JWT_SECRET", secret );
        var token = BuildToken(secret, subject: "actor-1", role: "admin");
        var guard = new JwtGuard();

        Assert.Empty(guard.Validate(token));
        var jwtRole = guard.TryGetRole(token);
        Assert.Equal("admin", jwtRole);

        var request = new EndpointRequestDto(
            "Search",
            "admin",
            "seed_runtime",
            "save",
            null,
            null,
            new Dictionary<string, string> { ["UserRole"] = "viewer" },
            TriggerKind: "client",
            Role: "viewer");

        var authoritative = request with { Role = jwtRole };
        var vector = new OperationVectorResolver().Resolve(authoritative);

        Assert.Equal("admin", authoritative.Role);
        Assert.Equal("admin", vector.UserRole);

        var manifest = new ManifestRecord(Guid.NewGuid(), null, JsonSerializer.SerializeToElement(new[] { new { type = "runtime_mapping", runtime_destination = "admin_runtime" } }).EnumerateArray().ToArray(), "active");
        var roleFiltered = new RoleFilteredManifestRepository("admin", manifest);
        var hit = await roleFiltered.ResolveActiveManifestAsync(vector.UserRole, vector.Target, vector.Layer, vector.Action);
        Assert.NotNull(hit);

        var miss = await roleFiltered.ResolveActiveManifestAsync("viewer", vector.Target, vector.Layer, vector.Action);
        Assert.Null(miss);
    }

    [Fact]
    public void JwtRoleClaim_Overwrites_RequestBodyRole_WhenMismatched()
    {
        const string secret = "dispatch-overwrite-role-test-secret";
        using var env = new EnvScope("DEMO_JWT_SECRET", secret);

        // Token carries role=user; request body tries to claim role=admin.
        var token = BuildToken(secret, subject: "actor-2", role: "user");
        var guard = new JwtGuard();

        var jwtRole = guard.TryGetRole(token);
        Assert.Equal("user", jwtRole);

        // Simulate what Program.cs /dispatch does: override request.Role with JWT claim.
        var manipulatedRequest = new EndpointRequestDto(
            "Search", "admin", "screen_list", "Search",
            IdOrHubId: null, Payload: null, Context: null,
            TriggerKind: "client",
            Role: "admin"); // <-- body claims admin

        var authoritative = manipulatedRequest with { Role = jwtRole };

        // After override, role must be what JWT says, not what request body claimed.
        Assert.Equal("user", authoritative.Role);
        Assert.NotEqual("admin", authoritative.Role);
    }

    [Fact]
    public async Task UserToken_AdminCapabilityRequired_Dispatch_ReturnsDenied()
    {
        const string secret = "dispatch-user-denied-admin-test";
        using var env = new EnvScope("DEMO_JWT_SECRET", secret);
        var token = BuildToken(secret, subject: "user-actor", role: "user");
        var guard = new JwtGuard();
        var jwtRole = guard.TryGetRole(token);
        Assert.Equal("user", jwtRole);

        // Manifest requires admin capability.
        var adminManifest = new ManifestRecord(
            Guid.NewGuid(), null,
            System.Text.Json.JsonSerializer.SerializeToElement(new object[]
            {
                new { type = "runtime_mapping", runtime_destination = "topology_transform_runtime" },
                new { type = "capability_requirement", required_role = "admin" },
            }).EnumerateArray().ToArray(),
            "active");

        var repo = new RoleFilteredManifestRepository("user", adminManifest); // returns manifest even for "user"
        var targetOverride = RuntimeExecutorTests.CreateTargetDispatchOverride();
        var handlers = new Dictionary<string, IDispatchableRuntime>
        {
            ["topology_transform_runtime"] = new StubSuccessRuntime(),
        };
        var dispatcher = new ManifestDispatcher(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ManifestDispatcher>.Instance,
            handlers, new OperationVectorResolver(), targetOverride, repo);

        var request = new EndpointRequestDto("Search", "screen", "screen_list", "Search",
            null, null, null, "client", Role: jwtRole);
        var response = await dispatcher.DispatchAsync(request);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_CAPABILITY_DENIED");
    }

    [Fact]
    public async Task AdminToken_AdminCapabilityRequired_Dispatch_Succeeds()
    {
        const string secret = "dispatch-admin-allowed-test";
        using var env = new EnvScope("DEMO_JWT_SECRET", secret);
        var token = BuildToken(secret, subject: "admin-actor", role: "admin");
        var guard = new JwtGuard();
        var jwtRole = guard.TryGetRole(token);
        Assert.Equal("admin", jwtRole);

        var adminManifest = new ManifestRecord(
            Guid.NewGuid(), null,
            System.Text.Json.JsonSerializer.SerializeToElement(new object[]
            {
                new { type = "runtime_mapping", runtime_destination = "topology_transform_runtime" },
                new { type = "capability_requirement", required_role = "admin" },
            }).EnumerateArray().ToArray(),
            "active");

        var repo = new RoleFilteredManifestRepository("admin", adminManifest);
        var targetOverride = RuntimeExecutorTests.CreateTargetDispatchOverride();
        var handlers = new Dictionary<string, IDispatchableRuntime>
        {
            ["topology_transform_runtime"] = new StubSuccessRuntime(),
        };
        var dispatcher = new ManifestDispatcher(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ManifestDispatcher>.Instance,
            handlers, new OperationVectorResolver(), targetOverride, repo);

        var request = new EndpointRequestDto("Search", "screen", "screen_list", "Search",
            null, null, null, "client", Role: jwtRole);
        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success);
    }


    [Fact]
    public void DispatchAuthContext_Overwrites_ClientSuppliedAuthAndCapabilityContext()
    {
        var forged = new EndpointRequestDto(
            "read", "cli_reader_port", "reader", "read",
            null, null,
            new Dictionary<string, string>
            {
                ["authenticated_user_id"] = "forged-user",
                ["authenticated_roles"] = "admin",
                ["resolved_capabilities"] = "cli_reader_port.read",
                ["safe_correlation_id"] = "corr-1"
            },
            "client",
            Role: "admin");

        var authoritative = DispatchAuthContext.ApplyJwtAuthority(
            forged,
            jwtSubject: "jwt-user",
            jwtRole: "reader",
            jwtCapabilities: [],
            routingRole: "reader");

        Assert.Equal("reader", authoritative.Role);
        Assert.Equal("jwt-user", authoritative.Context!["authenticated_user_id"]);
        Assert.Equal("reader", authoritative.Context!["authenticated_roles"]);
        Assert.False(authoritative.Context!.ContainsKey("resolved_capabilities"));
        Assert.Equal("corr-1", authoritative.Context!["safe_correlation_id"]);
    }


    [Fact]
    public void JwtGuard_ReadsCapabilityClaims_ForDispatchServerContext()
    {
        const string secret = "dispatch-capability-claim-test";
        using var env = new EnvScope("DEMO_JWT_SECRET", secret);
        var token = BuildToken(secret, subject: "reader-user", role: "reader", capabilities: ["cli_reader_port.read"]);
        var guard = new JwtGuard();

        Assert.Empty(guard.Validate(token));
        Assert.Equal(["cli_reader_port.read"], guard.TryGetCapabilities(token));
    }

    [Fact]
    public async Task ForgedClientContext_DoesNotSatisfyCliReaderCapability_ButJwtResolvedContextDoes()
    {
        var repo = new InMemoryCliReaderPortRepository(CliReaderConfig());
        var runtime = new AuthorizedCliReaderPortRuntime(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthorizedCliReaderPortRuntime>.Instance,
            repo);
        var forged = CliReaderRequest(new Dictionary<string, string>
        {
            ["authenticated_user_id"] = "reader-user",
            ["authenticated_roles"] = "reader",
            ["resolved_capabilities"] = "cli_reader_port.read"
        });

        var stripped = DispatchAuthContext.ApplyJwtAuthority(forged, "reader-user", "reader", [], "reader");
        var denied = await runtime.ExecuteAsync(stripped, Guid.NewGuid());

        Assert.False(denied.Success);
        Assert.Contains(denied.Errors, e => e.Code == "CLI_READER_CAPABILITY_UNRESOLVED");

        var authorized = DispatchAuthContext.ApplyJwtAuthority(forged, "reader-user", "reader", ["cli_reader_port.read"], "reader");
        var allowed = await runtime.ExecuteAsync(authorized, Guid.NewGuid());

        Assert.True(allowed.Success);
        Assert.Contains(repo.Queries, q => q.UserId == "reader-user");
    }

    private sealed class EnvScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _prev;
        public EnvScope(string name, string value)
        {
            _name = name;
            _prev = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
        public void Dispose() => Environment.SetEnvironmentVariable(_name, _prev);
    }

    private static string BuildToken(string secret, string subject, string role, string[]? capabilities = null)
    {
        var exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { sub = subject, role, capabilities = capabilities ?? [], exp })));
        var signingInput = Encoding.UTF8.GetBytes($"{header}.{payload}");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var sig = Base64UrlEncode(hmac.ComputeHash(signingInput));
        return $"{header}.{payload}.{sig}";
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');


    private static EndpointRequestDto CliReaderRequest(Dictionary<string, string> context)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            port_key = "cli_reader_port.default",
            table = "topology.entity",
            columns = new[] { "entity_id", "state_id" },
            filters = new Dictionary<string, string> { ["state_id"] = "active" },
            period = "today",
            request_id = "req-1",
            idempotency_key = "idem-1"
        });
        return new EndpointRequestDto("read", "cli_reader_port", "reader", "read", null, payload, context, "client", "reader");
    }

    private static CliReaderPortConfig CliReaderConfig() => new(
        "cli_reader_port.default",
        true,
        DateTimeOffset.UtcNow.AddHours(1),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "reader" },
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "reader-user" },
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "topology.entity" },
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["topology.entity"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "entity_id", "entity_jsonb", "state_id" }
        },
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "state_id" },
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "today" },
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["reader-user"] = "state_id=active" },
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cli_reader_port.read" },
        true,
        60);

    private sealed class InMemoryCliReaderPortRepository(CliReaderPortConfig? config) : CliReaderPortRepository
    {
        public List<AuthorizedCliReaderQuery> Queries { get; } = [];
        public List<CliReaderPortRuntimeEvent> Events { get; } = [];
        public Task<CliReaderPortConfig?> LoadPortAsync(string portKey, CancellationToken ct = default) => Task.FromResult(config);
        public Task<IReadOnlyList<Dictionary<string, object?>>> ReadRowsAsync(AuthorizedCliReaderQuery query, CancellationToken ct = default)
        {
            Queries.Add(query);
            IReadOnlyList<Dictionary<string, object?>> rows = [query.Columns.ToDictionary(c => c, c => (object?)$"value:{c}")];
            return Task.FromResult(rows);
        }
        public Task AppendRuntimeEventAsync(CliReaderPortRuntimeEvent runtimeEvent, CancellationToken ct = default)
        {
            Events.Add(runtimeEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class RoleFilteredManifestRepository(string expectedRole, ManifestRecord manifest)
        : ManifestRepository(Microsoft.Extensions.Logging.Abstractions.NullLogger<ManifestRepository>.Instance)
    {
        public override Task<ManifestRecord?> ResolveActiveManifestAsync(string? role, string? target, string? layer, string? action, CancellationToken ct = default)
            => Task.FromResult(role == expectedRole ? manifest : null);
        public override Task<ManifestRecord?> LoadByIdAsync(Guid manifestId, CancellationToken ct = default)
            => Task.FromResult(manifestId == manifest.ManifestId ? manifest : null);

        public override Task<IReadOnlyList<ManifestListItem>> ListManifestsAsync(string? statusFilter, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ManifestListItem>>([]);

        public override Task<ManifestDetailRecord?> LoadDetailByIdAsync(Guid manifestId, CancellationToken ct = default) =>
            Task.FromResult<ManifestDetailRecord?>(null);

        public override Task<int> CountActiveAxisConflictsAsync(
            string role, string target, string layer, string action, Guid? excludeManifestId, CancellationToken ct = default) =>
            Task.FromResult(0);

        public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> CreateDraftAsync(
            Guid? relationRegistryId, IReadOnlyList<System.Text.Json.JsonElement> topology, CancellationToken ct = default) =>
            Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("STUB", "stub")));

        public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdateDraftAsync(
            Guid manifestId, Guid? relationRegistryId, IReadOnlyList<System.Text.Json.JsonElement> topology, CancellationToken ct = default) =>
            Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("STUB", "stub")));

        public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> PromoteAsync(
            Guid manifestId, IReadOnlySet<string> allowedRuntimeDestinations, CancellationToken ct = default) =>
            Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("STUB", "stub")));

        public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> DeprecateAsync(
            Guid manifestId, CancellationToken ct = default) =>
            Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("STUB", "stub")));

        public override Task<IReadOnlyList<PromotionManifestListItem>> ListPromotionManifestsAsync(
            string? statusFilter, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PromotionManifestListItem>>([]);

        public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdatePromotionMetadataDraftAsync(
            Guid manifestId, System.Text.Json.JsonElement promotionEntry, CancellationToken ct = default) =>
            Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("STUB", "stub")));

        public override Task<int> CountActivePromotionKeyConflictsAsync(
            string manifestKey, string versionLabel, Guid? excludeManifestId, CancellationToken ct = default) =>
            Task.FromResult(0);

        public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> MergeTopologyExtensionDraftAsync(
            Guid manifestId, string entryType, JsonElement entryBody, CancellationToken ct = default) =>
            ManifestRepositoryStubDefaults.NotImplementedMerge();
    }
}
