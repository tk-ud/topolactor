using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Topolactor.Service;

namespace Topolactor.Runtime.Tests;

public class AdminRuntimeMasterRosterTests
{
    [Fact]
    public async Task AuthUsersCreate_WithInvalidStatus_ReturnsBlockingError()
    {
        var runtime = BuildRuntime();
        var vector = new OperationVector(
            "admin", "admin", "auth_users", "create",
            Payload: JsonSerializer.SerializeToElement(new
            {
                username = "new_user",
                password = "secret",
                status = "not_a_real_status",
            }));

        var (_, error) = await runtime.ExecuteDataAsync(vector);
        Assert.NotNull(error);
        Assert.Equal("AUTH_USER_STATUS_INVALID", error!.Code);
    }

    [Fact]
    public async Task EnumDictionaryCreateGroup_ThenList_IncludesGroup()
    {
        var runtime = BuildRuntime();
        var createVector = new OperationVector(
            "admin", "admin", "enum_dictionary", "create_group",
            Payload: JsonSerializer.SerializeToElement(new { groupName = "test_group_roster" }));

        var (data, error) = await runtime.ExecuteDataAsync(createVector);
        Assert.Null(error);
        Assert.NotNull(data);

        var listVector = new OperationVector("admin", "admin", "enum_dictionary", "list_groups");
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

    private static AdminRuntime BuildRuntime()
    {
        var ctxRepo = new InMemoryContextRouteRepository();
        var registrar = new RegistrarValidationService();
        var pkg = new PackageGeneratorRuntime(
            NullLogger<PackageGeneratorRuntime>.Instance,
            new InMemoryUiTopologyRepository());
        var uiRepo = new InMemoryUiTopologyRepository();
        return new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            ctxRepo,
            registrar,
            pkg,
            uiRepo,
            enumDictionaryRepository: InMemoryEnumDictionaryRepository.WithDemoSeed(),
            authMasterRepository: new InMemoryAuthMasterRepository());
    }
}
