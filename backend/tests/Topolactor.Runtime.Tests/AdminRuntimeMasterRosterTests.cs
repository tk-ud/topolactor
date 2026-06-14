using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Topolactor.Service;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class AdminRuntimeMasterRosterTests
{
    [Fact]
    public async Task AuthUsersCreate_WithInvalidStatus_ReturnsBlockingError()
    {
        var runtime = CreateRuntime();
        var vector = new OperationVector(
            "admin", "auth_users", "create", null, "admin",
            JsonSerializer.SerializeToElement(new
            {
                username = "new_user",
                password = "secret",
                status = "not_a_real_status",
            }),
            null);

        var (_, error) = await runtime.ExecuteDataAsync(vector);
        Assert.NotNull(error);
        Assert.Equal("AUTH_USER_STATUS_INVALID", error!.Code);
    }

    [Fact]
    public async Task EnumDictionaryCreateGroup_ThenList_IncludesGroup()
    {
        var runtime = CreateRuntime();
        var createVector = new OperationVector(
            "admin", "enum_dictionary", "create_group", null, "admin",
            JsonSerializer.SerializeToElement(new { groupName = "test_group_roster" }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(createVector);
        Assert.Null(error);
        Assert.NotNull(data);

        var listVector = new OperationVector(
            "admin", "enum_dictionary", "list_groups", null, "admin", null, null);
        var (listData, listError) = await runtime.ExecuteDataAsync(listVector);
        Assert.Null(listError);
        var json = listData!.Value.GetRawText();
        Assert.Contains("test_group_roster", json);
    }

    [Fact]
    public async Task AuthService_Login_BlocksUnapprovedUser()
    {
        var authRepo = new InMemoryAuthRepository();
        var userId = Guid.NewGuid();
        authRepo.SeedUser(new AuthUserRecord(userId, "blocked", true, false, "active", null, null),
            BCrypt.Net.BCrypt.HashPassword("pass"));
        authRepo.SeedGrant(userId, "user", "user");

        Environment.SetEnvironmentVariable("DEMO_JWT_SECRET", "test-secret-key-at-least-32-chars-long!!");
        Environment.SetEnvironmentVariable("DEMO_JWT_EXPIRY_HOURS", "1");
        var service = new AuthService(
            NullLogger<AuthService>.Instance,
            authRepo,
            new JwtTokenIssuer());

        var (response, _) = await service.LoginAsync(
            new LoginRequestDto("blocked", "pass"),
            AuthRealm.User);

        Assert.False(response.Success);
        Assert.Equal("AUTH_USER_NOT_APPROVED", response.Errors[0].Code);
    }

    private static AdminRuntime CreateRuntime()
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(
            NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            ctxRepo,
            registrar,
            pkg,
            uiRepo,
            enumDictionaryRepository: InMemoryEnumDictionaryRepository.WithFixtureSeed(),
            authMasterRepository: new InMemoryAuthMasterRepository());
    }
}
