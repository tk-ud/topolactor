using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Topolactor.Guard;
using Topolactor.Repository;
using Topolactor.Schema;
using Topolactor.Runtime;
using Xunit;

namespace Topolactor.Runtime.Tests;

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

    private static string BuildToken(string secret, string subject, string role)
    {
        var exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { sub = subject, role, exp })));
        var signingInput = Encoding.UTF8.GetBytes($"{header}.{payload}");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var sig = Base64UrlEncode(hmac.ComputeHash(signingInput));
        return $"{header}.{payload}.{sig}";
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

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
