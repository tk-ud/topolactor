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
        var (runtime, enumRepo) = CreateRuntimeWithEnumRepo();
        // indexNum 10 ("active") is a member of the seeded user_status group -- use a fresh,
        // unreferenced item so this test isolates the confirmed-gate check from the separate
        // ENUM_ITEM_IN_USE check (see EnumDictionaryDeleteItem_WhileReferencedInGroup_FailsClose).
        await enumRepo.CreateItemAsync("orphan_item", 500, CancellationToken.None);
        var deleteVector = new OperationVector(
            "admin", "enum_dictionary", "delete_item", null, "admin",
            JsonSerializer.SerializeToElement(new { indexNum = 500 }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(deleteVector);
        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("ENUM_ITEM_WRITE_NOT_CONFIRMED", error!.Code);
    }

    [Fact]
    public async Task EnumDictionaryDeleteItem_WhileReferencedInGroup_FailsCloseEvenWithConfirmed()
    {
        // indexNum 10 ("active") is a member of the seeded user_status group -- deleting it must
        // fail close (dryRun and confirmed=true both) rather than silently orphaning enum.group_items.
        var runtime = CreateRuntime();
        var deleteVector = new OperationVector(
            "admin", "enum_dictionary", "delete_item", null, "admin",
            JsonSerializer.SerializeToElement(new { indexNum = 10, confirmed = true }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(deleteVector);
        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("ENUM_ITEM_IN_USE", error!.Code);

        var dryRunVector = new OperationVector(
            "admin", "enum_dictionary", "delete_item", null, "admin",
            JsonSerializer.SerializeToElement(new { indexNum = 10, dryRun = true }),
            null);
        var (dryRunData, dryRunError) = await runtime.ExecuteDataAsync(dryRunVector);
        Assert.Null(dryRunData);
        Assert.NotNull(dryRunError);
        Assert.Equal("ENUM_ITEM_IN_USE", dryRunError!.Code);
    }

    [Fact]
    public async Task EnumDictionaryDeleteItem_Nonexistent_FailsClose()
    {
        var runtime = CreateRuntime();
        var deleteVector = new OperationVector(
            "admin", "enum_dictionary", "delete_item", null, "admin",
            JsonSerializer.SerializeToElement(new { indexNum = 90210, confirmed = true }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(deleteVector);
        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("ENUM_ITEM_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task EnumDictionaryUpdateItem_IndexChangeOnUnaffiliatedItem_Persists()
    {
        var (runtime, enumRepo) = CreateRuntimeWithEnumRepo();
        await enumRepo.CreateItemAsync("orphan_item", 501, CancellationToken.None);
        var updateVector = new OperationVector(
            "admin", "enum_dictionary", "update_item", null, "admin",
            JsonSerializer.SerializeToElement(new { indexNum = 501, newIndexNum = 502, confirmed = true }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(updateVector);
        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Equal(502, (await enumRepo.GetItemAsync(502, CancellationToken.None))!.IndexNum);
        Assert.Null(await enumRepo.GetItemAsync(501, CancellationToken.None));
    }

    [Fact]
    public async Task EnumDictionaryUpdateItem_IndexChangeWhileReferencedInGroup_FailsCloseOnDryRunAndConfirmed()
    {
        // indexNum 10 ("active") is a member of the seeded user_status group -- enum.group_items.
        // enum_index_num REFERENCES enum.items(index_num) with no ON UPDATE CASCADE, so moving its
        // index_num must fail close (both dryRun and confirmed=true) rather than raise a raw
        // ForeignKeyViolation or silently orphan the group_items row.
        var runtime = CreateRuntime();
        var dryRunVector = new OperationVector(
            "admin", "enum_dictionary", "update_item", null, "admin",
            JsonSerializer.SerializeToElement(new { indexNum = 10, newIndexNum = 999, dryRun = true }),
            null);
        var (dryRunData, dryRunError) = await runtime.ExecuteDataAsync(dryRunVector);
        Assert.Null(dryRunData);
        Assert.NotNull(dryRunError);
        Assert.Equal("ENUM_ITEM_IN_USE", dryRunError!.Code);

        var confirmedVector = new OperationVector(
            "admin", "enum_dictionary", "update_item", null, "admin",
            JsonSerializer.SerializeToElement(new { indexNum = 10, newIndexNum = 999, confirmed = true }),
            null);
        var (data, error) = await runtime.ExecuteDataAsync(confirmedVector);
        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("ENUM_ITEM_IN_USE", error!.Code);
    }

    [Fact]
    public async Task EnumDictionaryUpdateItem_RenameOnlyWhileReferencedInGroup_StillPersists()
    {
        // Renaming (no index_num change) does not touch the enum.group_items FK, so it must remain
        // allowed for group members -- only an actual index_num change is blocked.
        var runtime = CreateRuntime();
        var updateVector = new OperationVector(
            "admin", "enum_dictionary", "update_item", null, "admin",
            JsonSerializer.SerializeToElement(new { indexNum = 10, name = "active_renamed", confirmed = true }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(updateVector);
        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Contains("active_renamed", data!.Value.GetRawText());
    }

    [Fact]
    public async Task EnumDictionaryUpdateItem_IndexChangeToExistingIndex_FailsCloseOnDryRunAndConfirmed()
    {
        var (runtime, enumRepo) = CreateRuntimeWithEnumRepo();
        await enumRepo.CreateItemAsync("orphan_a", 503, CancellationToken.None);
        await enumRepo.CreateItemAsync("orphan_b", 504, CancellationToken.None);

        var dryRunVector = new OperationVector(
            "admin", "enum_dictionary", "update_item", null, "admin",
            JsonSerializer.SerializeToElement(new { indexNum = 503, newIndexNum = 504, dryRun = true }),
            null);
        var (dryRunData, dryRunError) = await runtime.ExecuteDataAsync(dryRunVector);
        Assert.Null(dryRunData);
        Assert.NotNull(dryRunError);
        Assert.Equal("ENUM_ITEM_INDEX_CONFLICT", dryRunError!.Code);

        var confirmedVector = new OperationVector(
            "admin", "enum_dictionary", "update_item", null, "admin",
            JsonSerializer.SerializeToElement(new { indexNum = 503, newIndexNum = 504, confirmed = true }),
            null);
        var (data, error) = await runtime.ExecuteDataAsync(confirmedVector);
        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("ENUM_ITEM_INDEX_CONFLICT", error!.Code);
    }

    [Fact]
    public async Task EnumDictionaryUpdateGroup_Nonexistent_FailsClose()
    {
        var runtime = CreateRuntime();
        var updateVector = new OperationVector(
            "admin", "enum_dictionary", "update_group", null, "admin",
            JsonSerializer.SerializeToElement(new { groupId = Guid.NewGuid().ToString(), groupName = "renamed", confirmed = true }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(updateVector);
        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("ENUM_GROUP_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task EnumDictionaryCreateGroup_WithDuplicateIndexNum_FailsCloseOnDryRunAndWrite()
    {
        var runtime = CreateRuntime();
        var dryRunVector = new OperationVector(
            "admin", "enum_dictionary", "create_group", null, "admin",
            JsonSerializer.SerializeToElement(new { groupName = "dup_index_group", indexNum = 2, dryRun = true }),
            null);
        var (dryRunData, dryRunError) = await runtime.ExecuteDataAsync(dryRunVector);
        Assert.Null(dryRunData);
        Assert.NotNull(dryRunError);
        Assert.Equal("ENUM_GROUP_INDEX_CONFLICT", dryRunError!.Code);

        var writeVector = new OperationVector(
            "admin", "enum_dictionary", "create_group", null, "admin",
            JsonSerializer.SerializeToElement(new { groupName = "dup_index_group", indexNum = 2, confirmed = true }),
            null);
        var (data, error) = await runtime.ExecuteDataAsync(writeVector);
        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("ENUM_GROUP_INDEX_CONFLICT", error!.Code);
    }

    [Fact]
    public async Task EnumDictionaryCreateItem_WithDuplicateIndexNum_FailsClose()
    {
        var runtime = CreateRuntime();
        var writeVector = new OperationVector(
            "admin", "enum_dictionary", "create_item", null, "admin",
            JsonSerializer.SerializeToElement(new { name = "dup_index_item", indexNum = 10, confirmed = true }),
            null);
        var (data, error) = await runtime.ExecuteDataAsync(writeVector);
        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("ENUM_ITEM_INDEX_CONFLICT", error!.Code);
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
    public async Task EnumDictionarySetGroupItems_WithCsvStringEnumIndexNums_Persists()
    {
        // The set_group_items write manifest's single text field tracks a scalar node value; a
        // seed-authored payloadFrom binding for an array-shaped field like enumIndexNums produces
        // a comma-separated JSON string at the wire (no array-typed payloadFrom source or
        // CSV-to-array transform exists in the generic payloadFrom grammar), so the backend must
        // accept that shape directly rather than only a native JSON array.
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
                enumIndexNums = "10, 11",
                confirmed = true,
            }),
            null);
        var (data, error) = await runtime.ExecuteDataAsync(setVector);
        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Contains("\"itemsIndexNums\":[10,11]", data!.Value.GetRawText());
    }

    [Fact]
    public async Task EnumDictionarySetGroupItems_WithDuplicateMembership_FailsCloseOnDryRunAndWrite()
    {
        var runtime = CreateRuntime();
        var listVector = new OperationVector(
            "admin", "enum_dictionary", "list_groups", null, "admin", null, null);
        var (listData, listError) = await runtime.ExecuteDataAsync(listVector);
        Assert.Null(listError);
        using var listDoc = JsonDocument.Parse(listData!.Value.GetRawText());
        var groupId = listDoc.RootElement[0].GetProperty("groupId").GetString();

        var dryRunVector = new OperationVector(
            "admin", "enum_dictionary", "set_group_items", null, "admin",
            JsonSerializer.SerializeToElement(new { groupId, enumIndexNums = new[] { 10, 10 }, dryRun = true }),
            null);
        var (dryRunData, dryRunError) = await runtime.ExecuteDataAsync(dryRunVector);
        Assert.Null(dryRunData);
        Assert.NotNull(dryRunError);
        Assert.Equal("ENUM_GROUP_ITEMS_DUPLICATE_MEMBERSHIP", dryRunError!.Code);

        var writeVector = new OperationVector(
            "admin", "enum_dictionary", "set_group_items", null, "admin",
            JsonSerializer.SerializeToElement(new { groupId, enumIndexNums = new[] { 10, 10 }, confirmed = true }),
            null);
        var (data, error) = await runtime.ExecuteDataAsync(writeVector);
        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("ENUM_GROUP_ITEMS_DUPLICATE_MEMBERSHIP", error!.Code);
    }

    [Fact]
    public async Task EnumDictionarySetGroupItems_WithNonexistentItem_FailsClose()
    {
        var runtime = CreateRuntime();
        var listVector = new OperationVector(
            "admin", "enum_dictionary", "list_groups", null, "admin", null, null);
        var (listData, listError) = await runtime.ExecuteDataAsync(listVector);
        Assert.Null(listError);
        using var listDoc = JsonDocument.Parse(listData!.Value.GetRawText());
        var groupId = listDoc.RootElement[0].GetProperty("groupId").GetString();

        var writeVector = new OperationVector(
            "admin", "enum_dictionary", "set_group_items", null, "admin",
            JsonSerializer.SerializeToElement(new { groupId, enumIndexNums = new[] { 90210 }, confirmed = true }),
            null);
        var (data, error) = await runtime.ExecuteDataAsync(writeVector);
        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("ENUM_ITEM_NOT_FOUND", error!.Code);
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

    private static AdminRuntime CreateRuntime() => CreateRuntimeWithEnumRepo().runtime;

    private static (AdminRuntime runtime, InMemoryEnumDictionaryRepository enumRepo) CreateRuntimeWithEnumRepo()
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(
            NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        var enumRepo = InMemoryEnumDictionaryRepository.WithFixtureSeed();
        var runtime = new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            ctxRepo,
            registrar,
            pkg,
            uiRepo,
            enumDictionaryRepository: enumRepo,
            authMasterRepository: new InMemoryAuthMasterRepository());
        return (runtime, enumRepo);
    }
}
