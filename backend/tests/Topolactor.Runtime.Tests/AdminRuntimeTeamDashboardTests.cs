using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

/// <summary>
/// Unit-level (no DB) tests for AdminRuntime.TeamDashboard.cs's role-gating idiom, mirroring
/// AdminRuntime.TeamMarkdown.cs ExecuteTeamMarkdownAsync's own established pattern: read (get) is
/// open to any authenticated caller reaching admin_runtime; mutation (update) additionally requires
/// AuthenticatedRole=="admin", an explicit in-method check independent of the request's
/// client-supplied Role field.
///
/// SSOT: docs/design/admin-normal-surface-projection-seed-ssot.yaml
///   surface_axes.admin.surfaces.team_dashboard.canonical_role_gate_binding
/// </summary>
public class AdminRuntimeTeamDashboardTests
{
    private sealed class FakeTeamDashboardRepository : TeamDashboardRepository
    {
        public string BodyMarkdown = "# Original";
        public int UpdateCallCount;

        public FakeTeamDashboardRepository() : base(NullLogger<TeamDashboardRepository>.Instance) { }

        public override Task<TeamDashboardNote> GetNoteAsync(CancellationToken ct = default) =>
            Task.FromResult(new TeamDashboardNote("note-1", "Team Dashboard", BodyMarkdown, DateTimeOffset.UtcNow));

        public override Task<TeamDashboardNote> UpdateNoteAsync(string bodyMarkdown, string? title, CancellationToken ct = default)
        {
            UpdateCallCount++;
            BodyMarkdown = bodyMarkdown;
            return Task.FromResult(new TeamDashboardNote("note-1", title ?? "Team Dashboard", bodyMarkdown, DateTimeOffset.UtcNow));
        }
    }

    private static AdminRuntime BuildAdminRuntime(TeamDashboardRepository repo)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var vecRuntime = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, vecRuntime);
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            ctxRepo,
            registrar,
            pkg,
            uiRepo,
            teamDashboardRepository: repo);
    }

    [Fact]
    public async Task ExecuteDataAsync_TeamDashboardGet_SucceedsWithoutAuthenticatedRole()
    {
        var repo = new FakeTeamDashboardRepository { BodyMarkdown = "# Hello" };
        var runtime = BuildAdminRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "team_dashboard", "get", null, "normal", null, null, AuthenticatedRole: null),
            default);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Equal("# Hello", data!.Value.GetProperty("bodyMarkdown").GetString());
    }

    [Fact]
    public async Task ExecuteDataAsync_TeamDashboardUpdate_WithoutAdminAuthenticatedRole_FailsCloseWithAuthCapabilityDenied()
    {
        var repo = new FakeTeamDashboardRepository();
        var runtime = BuildAdminRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { bodyMarkdown = "should not be written" });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "team_dashboard", "update", null, "admin", payload, null, AuthenticatedRole: null),
            default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("AUTH_CAPABILITY_DENIED", error!.Code);
        Assert.Equal(0, repo.UpdateCallCount);
        Assert.Equal("# Original", repo.BodyMarkdown);
    }

    [Fact]
    public async Task ExecuteDataAsync_TeamDashboardUpdate_WithAdminAuthenticatedRole_WritesAndReturnsUpdatedNote()
    {
        var repo = new FakeTeamDashboardRepository();
        var runtime = BuildAdminRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { bodyMarkdown = "# Edited", confirmed = true });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "team_dashboard", "update", null, "admin", payload, null, AuthenticatedRole: "admin"),
            default);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Equal("# Edited", data!.Value.GetProperty("bodyMarkdown").GetString());
        Assert.Equal(1, repo.UpdateCallCount);
        Assert.Equal("# Edited", repo.BodyMarkdown);
    }

    [Fact]
    public async Task ExecuteDataAsync_TeamDashboardUpdate_WithDryRun_ReturnsPreviewWithoutWriting()
    {
        var repo = new FakeTeamDashboardRepository();
        var runtime = BuildAdminRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { bodyMarkdown = "# Previewed", dryRun = true });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "team_dashboard", "update", null, "admin", payload, null, AuthenticatedRole: "admin"),
            default);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
        Assert.True(data.Value.GetProperty("dryRun").GetBoolean());
        Assert.True(data.Value.GetProperty("valid").GetBoolean());
        Assert.Equal("# Previewed", data.Value.GetProperty("preview").GetProperty("bodyMarkdown").GetString());
        Assert.Equal(0, repo.UpdateCallCount);
        Assert.Equal("# Original", repo.BodyMarkdown);
    }

    [Fact]
    public async Task ExecuteDataAsync_TeamDashboardUpdate_WithoutConfirmedOrDryRun_FailsCloseWithNotConfirmed()
    {
        var repo = new FakeTeamDashboardRepository();
        var runtime = BuildAdminRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { bodyMarkdown = "# Not confirmed" });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "team_dashboard", "update", null, "admin", payload, null, AuthenticatedRole: "admin"),
            default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("TEAM_DASHBOARD_WRITE_NOT_CONFIRMED", error!.Code);
        Assert.Equal(0, repo.UpdateCallCount);
        Assert.Equal("# Original", repo.BodyMarkdown);
    }

    [Fact]
    public async Task ExecuteDataAsync_TeamDashboardUpdate_MissingBodyMarkdown_FailsCloseExplicitly()
    {
        var repo = new FakeTeamDashboardRepository();
        var runtime = BuildAdminRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { title = "Team Dashboard" });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "team_dashboard", "update", null, "admin", payload, null, AuthenticatedRole: "admin"),
            default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("BODY_MARKDOWN_REQUIRED", error!.Code);
        Assert.Equal(0, repo.UpdateCallCount);
    }

    [Fact]
    public async Task ExecuteDataAsync_TeamDashboardUnknownAction_FailsCloseExplicitly()
    {
        var repo = new FakeTeamDashboardRepository();
        var runtime = BuildAdminRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "team_dashboard", "delete", null, "admin", null, null, AuthenticatedRole: "admin"),
            default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("TEAM_DASHBOARD_UNKNOWN_ACTION", error!.Code);
    }
}
