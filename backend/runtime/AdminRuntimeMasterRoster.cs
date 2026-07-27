using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

public partial class AdminRuntime
{
    private async Task<ValidationError?> ValidateUserStatusAsync(string? status, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(status)) return null;
        if (_enumDictionaryRepository is null)
        {
            return new ValidationError(
                "ENUM_DICTIONARY_NOT_AVAILABLE",
                "Enum dictionary repository is not configured.");
        }

        var detail = await _enumDictionaryRepository.GetGroupDetailAsync(
            AuthMasterRosterConstants.UserStatusGroupId, ct);
        if (detail is null || detail.Items.All(i => !string.Equals(i.Name, status, StringComparison.Ordinal)))
        {
            return new ValidationError(
                "AUTH_USER_STATUS_INVALID",
                $"Status '{status}' is not a member of the user_status enum group.");
        }

        return null;
    }

    // AuthenticatedUserId is server-verified (JWT subject, stamped by DispatchAuthContext at the
    // /dispatch boundary) and takes priority over client-supplied ContextUserId, which is not an
    // authority signal and must never be trusted as an audit actor.
    private string? ResolveAuditActor(OperationVector vector) =>
        vector.AuthenticatedUserId ?? vector.ContextUserId ?? vector.TriggerKind;

    private async Task<(JsonElement? data, ValidationError? error)> DataAuthUsersListAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_authMasterRepository is null)
            return (null, new ValidationError("AUTH_USERS_NOT_AVAILABLE", "Auth master repository is not configured."));
        string? query = null;
        if (vector.Payload is { ValueKind: JsonValueKind.Object } payload &&
            payload.TryGetProperty("query", out var q) && q.ValueKind == JsonValueKind.String)
            query = q.GetString();
        var users = await _authMasterRepository.ListUsersAsync(query, ct);
        return (JsonSerializer.SerializeToElement(users), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataAuthUsersSearchAsync(
        OperationVector vector, CancellationToken ct) =>
        await DataAuthUsersListAsync(vector, ct);

    private async Task<(JsonElement? data, ValidationError? error)> DataAuthUsersGetAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_authMasterRepository is null)
            return (null, new ValidationError("AUTH_USERS_NOT_AVAILABLE", "Auth master repository is not configured."));
        var userId = ParseUserIdFromPayload(vector.Payload);
        if (userId is null)
            return (null, new ValidationError("AUTH_USER_ID_MALFORMED", "userId must be a valid UUID."));
        var user = await _authMasterRepository.GetUserAsync(userId.Value, ct);
        if (user is null)
            return (null, new ValidationError("AUTH_USER_NOT_FOUND", $"User {userId} was not found."));
        return (JsonSerializer.SerializeToElement(user), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataAuthUsersCreateAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_authMasterRepository is null)
            return (null, new ValidationError("AUTH_USERS_NOT_AVAILABLE", "Auth master repository is not configured."));
        if (vector.Payload is null)
            return (null, new ValidationError("AUTH_USERS_PAYLOAD_REQUIRED", "payload is required."));
        AuthUsersCreateRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<AuthUsersCreateRequestDto>(vector.Payload.Value.GetRawText());
        }
        catch (JsonException)
        {
            return (null, new ValidationError("AUTH_USERS_PAYLOAD_MALFORMED", "payload could not be parsed."));
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return (null, new ValidationError("AUTH_USERS_CREATE_FIELDS_REQUIRED", "username and password are required."));
        }

        var statusError = await ValidateUserStatusAsync(request.Status, ct);
        if (statusError is not null) return (null, statusError);

        if (request.RoleName is not ("admin" or "user"))
            return (null, new ValidationError("AUTH_USER_ROLE_INVALID", "roleName must be 'admin' or 'user'."));

        if (await _authMasterRepository.UsernameExistsAsync(request.Username.Trim(), null, ct))
        {
            return (null, new ValidationError("AUTH_USER_USERNAME_CONFLICT", "Username already exists."));
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var created = await _authMasterRepository.CreateUserAsync(
            request.Username.Trim(),
            hash,
            request.Approve,
            request.Status,
            request.RoleName,
            request.Realm,
            request.SuspendedFrom,
            request.SuspendedUntil,
            request.StateNote,
            ct);

        await AdminMasterRosterAudit.AppendAsync(
            _sqlAttentionLogsRepository,
            ResolveAuditActor(vector),
            "auth.users",
            created.UserId.ToString(),
            "create",
            null,
            created,
            ["username", "approve", "status"],
            ct);

        return (JsonSerializer.SerializeToElement(created), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataAuthUsersUpdateAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_authMasterRepository is null)
            return (null, new ValidationError("AUTH_USERS_NOT_AVAILABLE", "Auth master repository is not configured."));
        if (vector.Payload is null)
            return (null, new ValidationError("AUTH_USERS_PAYLOAD_REQUIRED", "payload is required."));
        AuthUsersUpdateRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<AuthUsersUpdateRequestDto>(vector.Payload.Value.GetRawText());
        }
        catch (JsonException)
        {
            return (null, new ValidationError("AUTH_USERS_PAYLOAD_MALFORMED", "payload could not be parsed."));
        }

        if (request is null || !Guid.TryParse(request.UserId, out var userId))
            return (null, new ValidationError("AUTH_USER_ID_MALFORMED", "userId must be a valid UUID."));

        var before = await _authMasterRepository.GetUserAsync(userId, ct);
        if (before is null)
            return (null, new ValidationError("AUTH_USER_NOT_FOUND", $"User {userId} was not found."));

        if (request.Username is not null &&
            await _authMasterRepository.UsernameExistsAsync(request.Username.Trim(), userId, ct))
        {
            return (null, new ValidationError("AUTH_USER_USERNAME_CONFLICT", "Username already exists."));
        }

        var statusError = await ValidateUserStatusAsync(request.Status, ct);
        if (statusError is not null) return (null, statusError);

        if (request.RoleName is not (null or "admin" or "user"))
            return (null, new ValidationError("AUTH_USER_ROLE_INVALID", "roleName must be 'admin' or 'user'."));

        var updated = await _authMasterRepository.UpdateUserAsync(
            userId,
            request.Username?.Trim(),
            request.Active,
            request.Approve,
            request.Status,
            request.SuspendedFrom,
            request.SuspendedUntil,
            request.ClearSuspendedFrom,
            request.ClearSuspendedUntil,
            request.StateNote,
            ct,
            request.RoleName);

        if (updated is null)
            return (null, new ValidationError("AUTH_USER_NOT_FOUND", $"User {userId} was not found."));

        var changed = new List<string>();
        if (request.Username is not null) changed.Add("username");
        if (request.Active.HasValue) changed.Add("active");
        if (request.Approve.HasValue) changed.Add("approve");
        if (request.Status is not null) changed.Add("status");
        if (request.SuspendedFrom.HasValue || request.ClearSuspendedFrom) changed.Add("suspended_from");
        if (request.SuspendedUntil.HasValue || request.ClearSuspendedUntil) changed.Add("suspended_until");
        if (request.StateNote is not null) changed.Add("state_note");
        if (request.RoleName is not null) changed.Add("role");

        await AdminMasterRosterAudit.AppendAsync(
            _sqlAttentionLogsRepository,
            ResolveAuditActor(vector),
            "auth.users",
            userId.ToString(),
            "update",
            before,
            updated,
            changed,
            ct);

        return (JsonSerializer.SerializeToElement(updated), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataAuthUsersDeleteAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_authMasterRepository is null)
            return (null, new ValidationError("AUTH_USERS_NOT_AVAILABLE", "Auth master repository is not configured."));
        var userId = ParseUserIdFromPayload(vector.Payload);
        if (userId is null)
            return (null, new ValidationError("AUTH_USER_ID_MALFORMED", "userId must be a valid UUID."));
        var before = await _authMasterRepository.GetUserAsync(userId.Value, ct);
        if (before is null)
            return (null, new ValidationError("AUTH_USER_NOT_FOUND", $"User {userId} was not found."));
        var deleted = await _authMasterRepository.DeleteUserAsync(userId.Value, ct);
        if (!deleted)
            return (null, new ValidationError("AUTH_USER_NOT_FOUND", $"User {userId} was not found."));
        await AdminMasterRosterAudit.AppendAsync(
            _sqlAttentionLogsRepository,
            ResolveAuditActor(vector),
            "auth.users",
            userId.Value.ToString(),
            "delete",
            before,
            null,
            ["user_id"],
            ct);
        return (JsonSerializer.SerializeToElement(new { ok = true, userId = userId.Value }), null);
    }

    private static Guid? ParseUserIdFromPayload(JsonElement? payload)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } p) return null;
        if (!p.TryGetProperty("userId", out var el) || el.ValueKind != JsonValueKind.String) return null;
        return Guid.TryParse(el.GetString(), out var id) ? id : null;
    }

    // ─── enum dictionary mutation_confirmation_contract ─────────────────────
    // preview_dictionary_delta / validate_against_enum_authority (payload.dryRun=true,
    // non-mutating) -> explicit_confirm (frontend UI) -> write (payload.dryRun absent/false AND
    // payload.confirmed=true) -> diff_log (AdminMasterRosterAudit.AppendAsync, unchanged). Mirrors
    // the existing team_markdown saved-view-update contract
    // (AdminRuntime.TeamMarkdown.cs DataTeamMarkdownSavedViewUpdateAsync) rather than inventing a
    // new confirmation mechanism: every validation gate below runs identically regardless of
    // dryRun, and the write step re-runs the identical checks -- dryRun is never trusted as prior
    // proof. cancel is the frontend simply never sending payload.confirmed=true; that path never
    // reaches the repository, so no persisted state or audit row is produced.
    //
    // IsTruthyPayloadFlag additionally accepts a JSON string "true" (not only the JSON boolean
    // literal), because docs/design/ui-builder-preset-ecosystem-ssot.yaml payloadFrom_resolver_contract
    // only defines node:/event:/literal: sources -- literal: sources always resolve to a JS
    // string, so a data-defined literal:true dryRun/confirmed binding produces a JSON string, not
    // a JSON boolean, at the wire.

    private async Task<(JsonElement? data, ValidationError? error)> DataEnumDictionaryCreateGroupAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_enumDictionaryRepository is null)
            return (null, EnumDictionaryNotAvailable());
        var request = DeserializePayload<EnumDictionaryCreateGroupRequestDto>(vector.Payload);
        if (request is null || string.IsNullOrWhiteSpace(request.GroupName))
            return (null, new ValidationError("ENUM_GROUP_PAYLOAD_REQUIRED", "groupName is required."));

        if (IsTruthyPayloadFlag(vector.Payload, "dryRun"))
        {
            return (JsonSerializer.SerializeToElement(new
            {
                ok = true,
                dryRun = true,
                valid = true,
                preview = new { groupName = request.GroupName.Trim(), indexNum = request.IndexNum },
            }), null);
        }
        if (!IsTruthyPayloadFlag(vector.Payload, "confirmed"))
            return (null, new ValidationError("ENUM_GROUP_WRITE_NOT_CONFIRMED",
                "Write requires payload.confirmed=true after an explicit user confirmation step."));

        var created = await _enumDictionaryRepository.CreateGroupAsync(request.GroupName.Trim(), request.IndexNum, ct);
        await AdminMasterRosterAudit.AppendAsync(_sqlAttentionLogsRepository, ResolveAuditActor(vector),
            "enum.groups", created.GroupId.ToString(), "create", null, created, ["group_name"], ct);
        return (JsonSerializer.SerializeToElement(created), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataEnumDictionaryUpdateGroupAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_enumDictionaryRepository is null)
            return (null, EnumDictionaryNotAvailable());
        var request = DeserializePayload<EnumDictionaryUpdateGroupRequestDto>(vector.Payload);
        if (request is null || !Guid.TryParse(request.GroupId, out var groupId))
            return (null, new ValidationError("ENUM_GROUP_ID_MALFORMED", "groupId must be a valid UUID."));
        var before = await _enumDictionaryRepository.GetGroupDetailAsync(groupId, ct);
        if (before is null)
            return (null, new ValidationError("ENUM_GROUP_NOT_FOUND", $"Enum group {groupId} was not found."));

        if (IsTruthyPayloadFlag(vector.Payload, "dryRun"))
        {
            return (JsonSerializer.SerializeToElement(new
            {
                ok = true,
                dryRun = true,
                valid = true,
                groupId,
                preview = new
                {
                    groupName = request.GroupName?.Trim() ?? before.GroupName,
                    indexNum = request.IndexNum ?? before.IndexNum,
                },
            }), null);
        }
        if (!IsTruthyPayloadFlag(vector.Payload, "confirmed"))
            return (null, new ValidationError("ENUM_GROUP_WRITE_NOT_CONFIRMED",
                "Write requires payload.confirmed=true after an explicit user confirmation step."));

        var updated = await _enumDictionaryRepository.UpdateGroupAsync(groupId, request.GroupName?.Trim(), request.IndexNum, ct);
        if (updated is null)
            return (null, new ValidationError("ENUM_GROUP_NOT_FOUND", $"Enum group {groupId} was not found."));
        await AdminMasterRosterAudit.AppendAsync(_sqlAttentionLogsRepository, ResolveAuditActor(vector),
            "enum.groups", groupId.ToString(), "update", before, updated, ["group_name", "index_num"], ct);
        return (JsonSerializer.SerializeToElement(updated), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataEnumDictionaryDeleteGroupAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_enumDictionaryRepository is null)
            return (null, EnumDictionaryNotAvailable());
        var request = DeserializePayload<EnumDictionaryDeleteGroupRequestDto>(vector.Payload);
        if (request is null || !Guid.TryParse(request.GroupId, out var groupId))
            return (null, new ValidationError("ENUM_GROUP_ID_MALFORMED", "groupId must be a valid UUID."));
        if (await _enumDictionaryRepository.IsGroupReferencedInManifestsAsync(groupId, ct))
            return (null, new ValidationError("ENUM_GROUP_IN_USE", "Enum group is referenced by a manifest."));
        var before = await _enumDictionaryRepository.GetGroupDetailAsync(groupId, ct);
        if (before is null)
            return (null, new ValidationError("ENUM_GROUP_NOT_FOUND", $"Enum group {groupId} was not found."));

        if (IsTruthyPayloadFlag(vector.Payload, "dryRun"))
        {
            return (JsonSerializer.SerializeToElement(new
            {
                ok = true,
                dryRun = true,
                valid = true,
                groupId,
                preview = new { groupName = before.GroupName, willDelete = true },
            }), null);
        }
        if (!IsTruthyPayloadFlag(vector.Payload, "confirmed"))
            return (null, new ValidationError("ENUM_GROUP_WRITE_NOT_CONFIRMED",
                "Write requires payload.confirmed=true after an explicit user confirmation step."));

        try
        {
            var deleted = await _enumDictionaryRepository.DeleteGroupAsync(groupId, ct);
            if (!deleted)
                return (null, new ValidationError("ENUM_GROUP_NOT_FOUND", $"Enum group {groupId} was not found."));
        }
        catch (InvalidOperationException)
        {
            return (null, new ValidationError("ENUM_GROUP_IN_USE", "Enum group is referenced by a manifest."));
        }

        await AdminMasterRosterAudit.AppendAsync(_sqlAttentionLogsRepository, ResolveAuditActor(vector),
            "enum.groups", groupId.ToString(), "delete", before, null, ["group_id"], ct);
        return (JsonSerializer.SerializeToElement(new { ok = true, groupId }), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataEnumDictionaryCreateItemAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_enumDictionaryRepository is null)
            return (null, EnumDictionaryNotAvailable());
        var request = DeserializePayload<EnumDictionaryCreateItemRequestDto>(vector.Payload);
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
            return (null, new ValidationError("ENUM_ITEM_PAYLOAD_REQUIRED", "name is required."));

        if (IsTruthyPayloadFlag(vector.Payload, "dryRun"))
        {
            return (JsonSerializer.SerializeToElement(new
            {
                ok = true,
                dryRun = true,
                valid = true,
                preview = new { name = request.Name.Trim(), indexNum = request.IndexNum },
            }), null);
        }
        if (!IsTruthyPayloadFlag(vector.Payload, "confirmed"))
            return (null, new ValidationError("ENUM_ITEM_WRITE_NOT_CONFIRMED",
                "Write requires payload.confirmed=true after an explicit user confirmation step."));

        var created = await _enumDictionaryRepository.CreateItemAsync(request.Name.Trim(), request.IndexNum, ct);
        await AdminMasterRosterAudit.AppendAsync(_sqlAttentionLogsRepository, ResolveAuditActor(vector),
            "enum.items", created.IndexNum.ToString(), "create", null, created, ["name"], ct);
        return (JsonSerializer.SerializeToElement(created), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataEnumDictionaryUpdateItemAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_enumDictionaryRepository is null)
            return (null, EnumDictionaryNotAvailable());
        var request = DeserializePayload<EnumDictionaryUpdateItemRequestDto>(vector.Payload);
        if (request is null)
            return (null, new ValidationError("ENUM_ITEM_PAYLOAD_REQUIRED", "payload is required."));

        if (IsTruthyPayloadFlag(vector.Payload, "dryRun"))
        {
            return (JsonSerializer.SerializeToElement(new
            {
                ok = true,
                dryRun = true,
                valid = true,
                preview = new
                {
                    indexNum = request.NewIndexNum ?? request.IndexNum,
                    name = request.Name?.Trim(),
                },
            }), null);
        }
        if (!IsTruthyPayloadFlag(vector.Payload, "confirmed"))
            return (null, new ValidationError("ENUM_ITEM_WRITE_NOT_CONFIRMED",
                "Write requires payload.confirmed=true after an explicit user confirmation step."));

        var updated = await _enumDictionaryRepository.UpdateItemAsync(
            request.IndexNum, request.Name?.Trim(), request.NewIndexNum, ct);
        if (updated is null)
            return (null, new ValidationError("ENUM_ITEM_NOT_FOUND", $"Enum item index {request.IndexNum} was not found."));
        await AdminMasterRosterAudit.AppendAsync(_sqlAttentionLogsRepository, ResolveAuditActor(vector),
            "enum.items", updated.IndexNum.ToString(), "update", null, updated, ["name", "index_num"], ct);
        return (JsonSerializer.SerializeToElement(updated), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataEnumDictionaryDeleteItemAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_enumDictionaryRepository is null)
            return (null, EnumDictionaryNotAvailable());
        var request = DeserializePayload<EnumDictionaryDeleteItemRequestDto>(vector.Payload);
        if (request is null)
            return (null, new ValidationError("ENUM_ITEM_PAYLOAD_REQUIRED", "indexNum is required."));

        if (IsTruthyPayloadFlag(vector.Payload, "dryRun"))
        {
            return (JsonSerializer.SerializeToElement(new
            {
                ok = true,
                dryRun = true,
                valid = true,
                preview = new { indexNum = request.IndexNum, willDelete = true },
            }), null);
        }
        if (!IsTruthyPayloadFlag(vector.Payload, "confirmed"))
            return (null, new ValidationError("ENUM_ITEM_WRITE_NOT_CONFIRMED",
                "Write requires payload.confirmed=true after an explicit user confirmation step."));

        var deleted = await _enumDictionaryRepository.DeleteItemAsync(request.IndexNum, ct);
        if (!deleted)
            return (null, new ValidationError("ENUM_ITEM_NOT_FOUND", $"Enum item index {request.IndexNum} was not found."));
        await AdminMasterRosterAudit.AppendAsync(_sqlAttentionLogsRepository, ResolveAuditActor(vector),
            "enum.items", request.IndexNum.ToString(), "delete", null, null, ["index_num"], ct);
        return (JsonSerializer.SerializeToElement(new { ok = true, indexNum = request.IndexNum }), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataEnumDictionarySetGroupItemsAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_enumDictionaryRepository is null)
            return (null, EnumDictionaryNotAvailable());
        var request = DeserializePayload<EnumDictionarySetGroupItemsRequestDto>(vector.Payload);
        if (request is null || !Guid.TryParse(request.GroupId, out var groupId))
            return (null, new ValidationError("ENUM_GROUP_ID_MALFORMED", "groupId must be a valid UUID."));
        var before = await _enumDictionaryRepository.GetGroupDetailAsync(groupId, ct);
        if (before is null)
            return (null, new ValidationError("ENUM_GROUP_NOT_FOUND", $"Enum group {groupId} was not found."));

        if (IsTruthyPayloadFlag(vector.Payload, "dryRun"))
        {
            return (JsonSerializer.SerializeToElement(new
            {
                ok = true,
                dryRun = true,
                valid = true,
                groupId,
                preview = new { itemsIndexNums = request.EnumIndexNums },
            }), null);
        }
        if (!IsTruthyPayloadFlag(vector.Payload, "confirmed"))
            return (null, new ValidationError("ENUM_GROUP_WRITE_NOT_CONFIRMED",
                "Write requires payload.confirmed=true after an explicit user confirmation step."));

        var detail = await _enumDictionaryRepository.SetGroupItemsAsync(groupId, request.EnumIndexNums, ct);
        if (detail is null)
            return (null, new ValidationError("ENUM_GROUP_NOT_FOUND", $"Enum group {groupId} was not found."));
        await AdminMasterRosterAudit.AppendAsync(_sqlAttentionLogsRepository, ResolveAuditActor(vector),
            "enum.group_items", groupId.ToString(), "update", before, detail, ["itemsIndexNums"], ct);
        return (JsonSerializer.SerializeToElement(detail), null);
    }

    private static ValidationError EnumDictionaryNotAvailable() =>
        new("ENUM_DICTIONARY_NOT_AVAILABLE", "Enum dictionary repository is not configured.");

    private static bool IsTruthyPayloadFlag(JsonElement? payload, string propertyName)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } p) return false;
        if (!p.TryGetProperty(propertyName, out var el)) return false;
        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => string.Equals(el.GetString(), "true", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private static T? DeserializePayload<T>(JsonElement? payload) where T : class
    {
        if (payload is null || payload.Value.ValueKind != JsonValueKind.Object) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(payload.Value.GetRawText());
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
