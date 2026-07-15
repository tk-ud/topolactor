using System.Text.Json.Serialization;

namespace Topolactor.Schema;

public static class AuthMasterRosterConstants
{
    public static readonly Guid UserStatusGroupId =
        Guid.Parse("33333333-3333-3333-3333-333333333301");
}

public record AuthUserRosterDto(
    [property: JsonPropertyName("userId")] Guid UserId,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("active")] bool Active,
    [property: JsonPropertyName("approve")] bool Approve,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("suspendedFrom")] DateTimeOffset? SuspendedFrom,
    [property: JsonPropertyName("suspendedUntil")] DateTimeOffset? SuspendedUntil,
    [property: JsonPropertyName("stateNote")] string? StateNote,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("lastLoginAt")] DateTimeOffset? LastLoginAt,
    // Canonical role value from auth.grants (AuthRealm.AdminRole / AuthRealm.UserRole) — "admin"
    // when the account holds an admin/system grant, "user" otherwise. Read projection only; role
    // changes go through AuthMasterRepository.UpdateUserAsync(roleName:), never a raw grants write.
    [property: JsonPropertyName("role")] string Role = "user");

public record AuthUsersListRequestDto(
    [property: JsonPropertyName("query")] string? Query = null);

public record AuthUsersGetRequestDto(
    [property: JsonPropertyName("userId")] string UserId);

public record AuthUsersCreateRequestDto(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password,
    [property: JsonPropertyName("approve")] bool Approve = false,
    [property: JsonPropertyName("status")] string? Status = "active",
    [property: JsonPropertyName("roleName")] string RoleName = "user",
    [property: JsonPropertyName("realm")] string Realm = "user",
    [property: JsonPropertyName("suspendedFrom")] DateTimeOffset? SuspendedFrom = null,
    [property: JsonPropertyName("suspendedUntil")] DateTimeOffset? SuspendedUntil = null,
    [property: JsonPropertyName("stateNote")] string? StateNote = null);

public record AuthUsersUpdateRequestDto(
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("username")] string? Username = null,
    [property: JsonPropertyName("active")] bool? Active = null,
    [property: JsonPropertyName("approve")] bool? Approve = null,
    [property: JsonPropertyName("status")] string? Status = null,
    [property: JsonPropertyName("suspendedFrom")] DateTimeOffset? SuspendedFrom = null,
    [property: JsonPropertyName("suspendedUntil")] DateTimeOffset? SuspendedUntil = null,
    [property: JsonPropertyName("stateNote")] string? StateNote = null,
    [property: JsonPropertyName("clearSuspendedFrom")] bool ClearSuspendedFrom = false,
    [property: JsonPropertyName("clearSuspendedUntil")] bool ClearSuspendedUntil = false,
    // "admin" or "user" — never a password/credential field. Grants the admin/system realm grant
    // when set to "admin"; revokes it (keeping the baseline user-realm grant) when set to "user".
    [property: JsonPropertyName("roleName")] string? RoleName = null);

public record AuthUsersDeleteRequestDto(
    [property: JsonPropertyName("userId")] string UserId);

public record EnumDictionaryCreateGroupRequestDto(
    [property: JsonPropertyName("groupName")] string GroupName,
    [property: JsonPropertyName("indexNum")] int? IndexNum = null);

public record EnumDictionaryUpdateGroupRequestDto(
    [property: JsonPropertyName("groupId")] string GroupId,
    [property: JsonPropertyName("groupName")] string? GroupName = null,
    [property: JsonPropertyName("indexNum")] int? IndexNum = null);

public record EnumDictionaryDeleteGroupRequestDto(
    [property: JsonPropertyName("groupId")] string GroupId);

public record EnumDictionaryCreateItemRequestDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("indexNum")] int? IndexNum = null);

public record EnumDictionaryUpdateItemRequestDto(
    [property: JsonPropertyName("indexNum")] int IndexNum,
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("newIndexNum")] int? NewIndexNum = null);

public record EnumDictionaryDeleteItemRequestDto(
    [property: JsonPropertyName("indexNum")] int IndexNum);

public record EnumDictionarySetGroupItemsRequestDto(
    [property: JsonPropertyName("groupId")] string GroupId,
    [property: JsonPropertyName("enumIndexNums")] IReadOnlyList<int> EnumIndexNums);
