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
    // /dispatch boundary) and is preferred over client-supplied ContextUserId -- per
    // docs/design/auth-db-session-credential-ssot.yaml non_spoofable_actor_identity ("it now
    // prefers AuthenticatedUserId"), ContextUserId is a documented, intentional fallback for
    // dispatches without a verified identity, not primary/authoritative actor identity. The same
    // AuthenticatedUserId ?? ContextUserId ?? TriggerKind chain is used unmodified in
    // AdminRuntime.TeamMarkdown.cs's 4 write actions. (2026-07-27 PR #600 review: this comment
    // previously and incorrectly said ContextUserId "must never be trusted as an audit actor",
    // which contradicted the code's own fallback and the SSOT note above -- corrected to describe
    // actual, SSOT-documented behavior; the fallback logic itself is unchanged.)
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
            [
                new AuditChangedField("username", null, created.Username),
                new AuditChangedField("approve", null, created.Approve),
                new AuditChangedField("status", null, created.Status),
            ],
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

        var changed = new List<AuditChangedField>();
        if (request.Username is not null) changed.Add(new AuditChangedField("username", before.Username, updated.Username));
        if (request.Active.HasValue) changed.Add(new AuditChangedField("active", before.Active, updated.Active));
        if (request.Approve.HasValue) changed.Add(new AuditChangedField("approve", before.Approve, updated.Approve));
        if (request.Status is not null) changed.Add(new AuditChangedField("status", before.Status, updated.Status));
        if (request.SuspendedFrom.HasValue || request.ClearSuspendedFrom)
            changed.Add(new AuditChangedField("suspended_from", before.SuspendedFrom, updated.SuspendedFrom));
        if (request.SuspendedUntil.HasValue || request.ClearSuspendedUntil)
            changed.Add(new AuditChangedField("suspended_until", before.SuspendedUntil, updated.SuspendedUntil));
        if (request.StateNote is not null) changed.Add(new AuditChangedField("state_note", before.StateNote, updated.StateNote));
        if (request.RoleName is not null) changed.Add(new AuditChangedField("role", before.Role, updated.Role));

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
            [new AuditChangedField("user_id", before.UserId, null)],
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
        if (request.IndexNum.HasValue &&
            (await _enumDictionaryRepository.ListGroupsAsync(ct)).Any(g => g.IndexNum == request.IndexNum.Value))
            return (null, new ValidationError("ENUM_GROUP_INDEX_CONFLICT", $"indexNum {request.IndexNum} is already in use."));

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

        EnumDictionaryGroupDto created;
        try
        {
            created = await _enumDictionaryRepository.CreateGroupAsync(request.GroupName.Trim(), request.IndexNum, ct);
        }
        catch (InvalidOperationException)
        {
            return (null, new ValidationError("ENUM_GROUP_INDEX_CONFLICT", $"indexNum {request.IndexNum} is already in use."));
        }
        await AdminMasterRosterAudit.AppendAsync(_sqlAttentionLogsRepository, ResolveAuditActor(vector),
            "enum.groups", created.GroupId.ToString(), "create", null, created,
            [new AuditChangedField("group_name", null, created.GroupName)], ct);
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
        if (request.IndexNum.HasValue && request.IndexNum.Value != before.IndexNum &&
            (await _enumDictionaryRepository.ListGroupsAsync(ct)).Any(g => g.GroupId != groupId && g.IndexNum == request.IndexNum.Value))
            return (null, new ValidationError("ENUM_GROUP_INDEX_CONFLICT", $"indexNum {request.IndexNum} is already in use."));

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

        EnumDictionaryGroupDto? updated;
        try
        {
            updated = await _enumDictionaryRepository.UpdateGroupAsync(groupId, request.GroupName?.Trim(), request.IndexNum, ct);
        }
        catch (InvalidOperationException)
        {
            return (null, new ValidationError("ENUM_GROUP_INDEX_CONFLICT", $"indexNum {request.IndexNum} is already in use."));
        }
        if (updated is null)
            return (null, new ValidationError("ENUM_GROUP_NOT_FOUND", $"Enum group {groupId} was not found."));
        await AdminMasterRosterAudit.AppendAsync(_sqlAttentionLogsRepository, ResolveAuditActor(vector),
            "enum.groups", groupId.ToString(), "update", before, updated,
            [
                new AuditChangedField("group_name", before.GroupName, updated.GroupName),
                new AuditChangedField("index_num", before.IndexNum, updated.IndexNum),
            ], ct);
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
            "enum.groups", groupId.ToString(), "delete", before, null,
            [new AuditChangedField("group_id", before.GroupId, null)], ct);
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
        if (request.IndexNum.HasValue &&
            await _enumDictionaryRepository.GetItemAsync(request.IndexNum.Value, ct) is not null)
            return (null, new ValidationError("ENUM_ITEM_INDEX_CONFLICT", $"indexNum {request.IndexNum} is already in use."));

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

        EnumDictionaryItemDto created;
        try
        {
            created = await _enumDictionaryRepository.CreateItemAsync(request.Name.Trim(), request.IndexNum, ct);
        }
        catch (InvalidOperationException)
        {
            return (null, new ValidationError("ENUM_ITEM_INDEX_CONFLICT", $"indexNum {request.IndexNum} is already in use."));
        }
        await AdminMasterRosterAudit.AppendAsync(_sqlAttentionLogsRepository, ResolveAuditActor(vector),
            "enum.items", created.IndexNum.ToString(), "create", null, created,
            [new AuditChangedField("name", null, created.Name)], ct);
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
        var before = await _enumDictionaryRepository.GetItemAsync(request.IndexNum, ct);
        if (before is null)
            return (null, new ValidationError("ENUM_ITEM_NOT_FOUND", $"Enum item index {request.IndexNum} was not found."));
        var indexChanging = request.NewIndexNum.HasValue && request.NewIndexNum.Value != request.IndexNum;
        if (indexChanging &&
            await _enumDictionaryRepository.GetItemAsync(request.NewIndexNum!.Value, ct) is not null)
            return (null, new ValidationError("ENUM_ITEM_INDEX_CONFLICT", $"indexNum {request.NewIndexNum} is already in use."));
        // enum.group_items.enum_index_num REFERENCES enum.items(index_num) with no ON UPDATE CASCADE
        // (db/enum_tables.sql) -- moving index_num out from under an existing group membership row
        // violates that FK. Renaming (name-only, index_num unchanged) does not touch this FK and
        // remains allowed for group members; only an actual index_num change is blocked. Mirrors
        // delete_item's existing ENUM_ITEM_IN_USE gate for the same underlying constraint, run
        // identically for dryRun and confirmed (validation_parity_rule).
        if (indexChanging && await _enumDictionaryRepository.IsItemReferencedInGroupsAsync(request.IndexNum, ct))
            return (null, new ValidationError("ENUM_ITEM_IN_USE", "Enum item is a member of an enum group."));

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
                    name = request.Name?.Trim() ?? before.Name,
                },
            }), null);
        }
        if (!IsTruthyPayloadFlag(vector.Payload, "confirmed"))
            return (null, new ValidationError("ENUM_ITEM_WRITE_NOT_CONFIRMED",
                "Write requires payload.confirmed=true after an explicit user confirmation step."));

        EnumDictionaryItemDto? updated;
        try
        {
            updated = await _enumDictionaryRepository.UpdateItemAsync(
                request.IndexNum, request.Name?.Trim(), request.NewIndexNum, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message == "ENUM_ITEM_IN_USE")
        {
            return (null, new ValidationError("ENUM_ITEM_IN_USE", "Enum item is a member of an enum group."));
        }
        catch (InvalidOperationException)
        {
            return (null, new ValidationError("ENUM_ITEM_INDEX_CONFLICT", $"indexNum {request.NewIndexNum} is already in use."));
        }
        if (updated is null)
            return (null, new ValidationError("ENUM_ITEM_NOT_FOUND", $"Enum item index {request.IndexNum} was not found."));
        await AdminMasterRosterAudit.AppendAsync(_sqlAttentionLogsRepository, ResolveAuditActor(vector),
            "enum.items", updated.IndexNum.ToString(), "update", before, updated,
            [
                new AuditChangedField("name", before.Name, updated.Name),
                new AuditChangedField("index_num", before.IndexNum, updated.IndexNum),
            ], ct);
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
        var before = await _enumDictionaryRepository.GetItemAsync(request.IndexNum, ct);
        if (before is null)
            return (null, new ValidationError("ENUM_ITEM_NOT_FOUND", $"Enum item index {request.IndexNum} was not found."));
        if (await _enumDictionaryRepository.IsItemReferencedInGroupsAsync(request.IndexNum, ct))
            return (null, new ValidationError("ENUM_ITEM_IN_USE", "Enum item is a member of an enum group."));

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

        bool deleted;
        try
        {
            deleted = await _enumDictionaryRepository.DeleteItemAsync(request.IndexNum, ct);
        }
        catch (InvalidOperationException)
        {
            return (null, new ValidationError("ENUM_ITEM_IN_USE", "Enum item is a member of an enum group."));
        }
        if (!deleted)
            return (null, new ValidationError("ENUM_ITEM_NOT_FOUND", $"Enum item index {request.IndexNum} was not found."));
        await AdminMasterRosterAudit.AppendAsync(_sqlAttentionLogsRepository, ResolveAuditActor(vector),
            "enum.items", request.IndexNum.ToString(), "delete", before, null,
            [new AuditChangedField("index_num", before.IndexNum, null)], ct);
        return (JsonSerializer.SerializeToElement(new { ok = true, indexNum = request.IndexNum }), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataEnumDictionarySetGroupItemsAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_enumDictionaryRepository is null)
            return (null, EnumDictionaryNotAvailable());
        if (!TryParseSetGroupItemsPayload(vector.Payload, out var groupId, out var enumIndexNums, out var parseError))
            return (null, parseError);
        var before = await _enumDictionaryRepository.GetGroupDetailAsync(groupId, ct);
        if (before is null)
            return (null, new ValidationError("ENUM_GROUP_NOT_FOUND", $"Enum group {groupId} was not found."));
        if (enumIndexNums.Count != enumIndexNums.Distinct().Count())
            return (null, new ValidationError("ENUM_GROUP_ITEMS_DUPLICATE_MEMBERSHIP",
                "enumIndexNums must not contain duplicate values."));
        foreach (var indexNum in enumIndexNums)
        {
            if (await _enumDictionaryRepository.GetItemAsync(indexNum, ct) is null)
                return (null, new ValidationError("ENUM_ITEM_NOT_FOUND", $"Enum item index {indexNum} was not found."));
        }

        if (IsTruthyPayloadFlag(vector.Payload, "dryRun"))
        {
            return (JsonSerializer.SerializeToElement(new
            {
                ok = true,
                dryRun = true,
                valid = true,
                groupId,
                preview = new { itemsIndexNums = enumIndexNums },
            }), null);
        }
        if (!IsTruthyPayloadFlag(vector.Payload, "confirmed"))
            return (null, new ValidationError("ENUM_GROUP_WRITE_NOT_CONFIRMED",
                "Write requires payload.confirmed=true after an explicit user confirmation step."));

        EnumDictionaryGroupDetailDto? detail;
        try
        {
            detail = await _enumDictionaryRepository.SetGroupItemsAsync(groupId, enumIndexNums, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message == "ENUM_GROUP_ITEMS_DUPLICATE_MEMBERSHIP")
        {
            return (null, new ValidationError("ENUM_GROUP_ITEMS_DUPLICATE_MEMBERSHIP",
                "enumIndexNums must not contain duplicate values."));
        }
        catch (InvalidOperationException)
        {
            return (null, new ValidationError("ENUM_ITEM_NOT_FOUND", "One or more enumIndexNums were not found."));
        }
        if (detail is null)
            return (null, new ValidationError("ENUM_GROUP_NOT_FOUND", $"Enum group {groupId} was not found."));
        await AdminMasterRosterAudit.AppendAsync(_sqlAttentionLogsRepository, ResolveAuditActor(vector),
            "enum.group_items", groupId.ToString(), "update", before, detail,
            [new AuditChangedField("itemsIndexNums", before.ItemsIndexNums, detail.ItemsIndexNums)], ct);
        return (JsonSerializer.SerializeToElement(detail), null);
    }

    // update_group_members (SSOT mutation intent) resolves to this same set_group_items action.
    // enumIndexNums accepts either a native JSON array of numbers (the shape any programmatic
    // caller / existing test uses) or a comma-separated string (e.g. "10,11,12") -- the shape a
    // single seed-authored text field's tracked node value produces via the existing node:/
    // literal: payloadFrom grammar, which has no array-typed source or CSV-to-array transform.
    // This is ordinary backend request-shape leniency (mirrors IsTruthyPayloadFlag accepting a
    // JSON string alongside a JSON boolean for the same reason), not a new frontend payload
    // resolver or transform.
    private static bool TryParseSetGroupItemsPayload(
        JsonElement? payload, out Guid groupId, out IReadOnlyList<int> enumIndexNums, out ValidationError? error)
    {
        groupId = default;
        enumIndexNums = [];
        if (payload is not { ValueKind: JsonValueKind.Object } p)
        {
            error = new ValidationError("ENUM_GROUP_ID_MALFORMED", "groupId must be a valid UUID.");
            return false;
        }

        if (!p.TryGetProperty("groupId", out var groupIdEl) ||
            groupIdEl.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(groupIdEl.GetString(), out groupId))
        {
            error = new ValidationError("ENUM_GROUP_ID_MALFORMED", "groupId must be a valid UUID.");
            return false;
        }

        if (!p.TryGetProperty("enumIndexNums", out var itemsEl))
        {
            error = new ValidationError("ENUM_GROUP_PAYLOAD_REQUIRED", "enumIndexNums is required.");
            return false;
        }

        if (itemsEl.ValueKind == JsonValueKind.Array)
        {
            var list = new List<int>();
            foreach (var el in itemsEl.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var n))
                {
                    error = new ValidationError("ENUM_GROUP_PAYLOAD_REQUIRED", "enumIndexNums entries must be integers.");
                    return false;
                }
                list.Add(n);
            }
            enumIndexNums = list;
            error = null;
            return true;
        }

        if (itemsEl.ValueKind == JsonValueKind.String)
        {
            var raw = itemsEl.GetString() ?? "";
            var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var list = new List<int>();
            foreach (var part in parts)
            {
                if (!int.TryParse(part, out var n))
                {
                    error = new ValidationError("ENUM_GROUP_PAYLOAD_REQUIRED",
                        $"enumIndexNums entry '{part}' is not an integer.");
                    return false;
                }
                list.Add(n);
            }
            enumIndexNums = list;
            error = null;
            return true;
        }

        error = new ValidationError("ENUM_GROUP_PAYLOAD_REQUIRED",
            "enumIndexNums must be an array of integers or a comma-separated string of integers.");
        return false;
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

    private static readonly JsonSerializerOptions LenientNumberDeserializeOptions = new()
    {
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
    };

    // Numeric request fields (e.g. EnumDictionaryUpdateItemRequestDto.IndexNum) must tolerate a
    // JSON string value, not only a JSON number: docs/design/ui-builder-preset-ecosystem-ssot.yaml
    // payloadFrom_resolver_contract's node:/literal: sources always resolve to a JS string, so a
    // seed-authored dispatchPayloadFromByTrigger binding for a numeric field produces a JSON
    // string at the wire, never a JSON number. Same rationale as IsTruthyPayloadFlag above,
    // generalized once here for every enum dictionary request DTO rather than per-field.
    private static T? DeserializePayload<T>(JsonElement? payload) where T : class
    {
        if (payload is null || payload.Value.ValueKind != JsonValueKind.Object) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(payload.Value.GetRawText(), LenientNumberDeserializeOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
