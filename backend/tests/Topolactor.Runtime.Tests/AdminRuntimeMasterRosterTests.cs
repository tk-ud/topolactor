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
            JsonSerializer.SerializeToElement(new { groupName = "test_group_roster", confirmed = true }),
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
    public async Task EnumDictionaryCreateGroup_WithoutConfirmed_FailsCloseAndDoesNotPersist()
    {
        var runtime = CreateRuntime();
        var createVector = new OperationVector(
            "admin", "enum_dictionary", "create_group", null, "admin",
            JsonSerializer.SerializeToElement(new { groupName = "unconfirmed_group_roster" }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(createVector);
        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("ENUM_GROUP_WRITE_NOT_CONFIRMED", error!.Code);

        var listVector = new OperationVector(
            "admin", "enum_dictionary", "list_groups", null, "admin", null, null);
        var (listData, listError) = await runtime.ExecuteDataAsync(listVector);
        Assert.Null(listError);
        Assert.DoesNotContain("unconfirmed_group_roster", listData!.Value.GetRawText());
    }

    [Fact]
    public async Task EnumDictionaryCreateGroup_WithDryRun_ReturnsPreviewAndDoesNotPersist()
    {
        var runtime = CreateRuntime();
        var previewVector = new OperationVector(
            "admin", "enum_dictionary", "create_group", null, "admin",
            JsonSerializer.SerializeToElement(new { groupName = "dry_run_group_roster", dryRun = true }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(previewVector);
        Assert.Null(error);
        Assert.NotNull(data);
        var previewJson = data!.Value.GetRawText();
        Assert.Contains("\"dryRun\":true", previewJson);
        Assert.Contains("dry_run_group_roster", previewJson);

        var listVector = new OperationVector(
            "admin", "enum_dictionary", "list_groups", null, "admin", null, null);
        var (listData, listError) = await runtime.ExecuteDataAsync(listVector);
        Assert.Null(listError);
        Assert.DoesNotContain("dry_run_group_roster", listData!.Value.GetRawText());
    }

    [Fact]
    public async Task EnumDictionaryDeleteItem_WithoutConfirmed_FailsCloseAndDoesNotPersist()
    {
        var runtime = CreateRuntime();
        var deleteVector = new OperationVector(
            "admin", "enum_dictionary", "delete_item", null, "admin",
            JsonSerializer.SerializeToElement(new { indexNum = 10 }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(deleteVector);
        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("ENUM_ITEM_WRITE_NOT_CONFIRMED", error!.Code);
    }

    [Fact]
    public async Task EnumDictionarySetGroupItems_WithConfirmedLiteralString_Persists()
    {
        // literal:true payloadFrom sources (docs/design/ui-builder-preset-ecosystem-ssot.yaml
        // payloadFrom_resolver_contract) always resolve to a JS string at the wire, never a JSON
        // boolean -- this proves the backend confirmed/dryRun gate accepts that shape too.
        var runtime = CreateRuntime();
        var listVector = new OperationVector(
            "admin", "enum_dictionary", "list_groups", null, "admin", null, null);
        var (listData, listError) = await runtime.ExecuteDataAsync(listVector);
        Assert.Null(listError);
        using var listDoc = JsonDocument.Parse(listData!.Value.GetRawText());
        var groupId = listDoc.RootElement[0].GetProperty("groupId").GetString();

        var setVector = new OperationVector(
            "admin", "enum_dictionary", "set_group_items", null, "admin",
            JsonSerializer.SerializeToElement(new
            {
                groupId,
                enumIndexNums = new[] { 10 },
                confirmed = "true",
            }),
            null);
        var (data, error) = await runtime.ExecuteDataAsync(setVector);
        Assert.Null(error);
        Assert.NotNull(data);
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
