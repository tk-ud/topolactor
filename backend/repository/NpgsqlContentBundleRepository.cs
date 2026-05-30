using System.Text.Json;
using Microsoft.Extensions.Logging;
using Npgsql;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// Npgsql implementation of ContentBundleRepository.
/// Browse converged topology data and manage entity registration drafts.
/// </summary>
public class NpgsqlContentBundleRepository : ContentBundleRepository
{
    private readonly string _connectionString;

    public NpgsqlContentBundleRepository(
        ILogger<NpgsqlContentBundleRepository> logger,
        string connectionString)
        : base(logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public override async Task<IReadOnlyList<ContentBundleListItemDto>> ListContentHubsAsync(CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT h.hub_id::text, COALESCE(rr.name, 'hub'), COALESCE(sr.name, 'unknown'), " +
            "       h.relation_registry_id::text " +
            "FROM hubs.hub h " +
            "LEFT JOIN topology.relation_registry rr ON rr.relation_registry_id = h.relation_registry_id " +
            "LEFT JOIN topology.state_registry sr ON sr.state_id = h.state_id " +
            "ORDER BY h.created_at DESC";

        var items = new List<ContentBundleListItemDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var hubId = reader.GetString(0);
            var relationName = reader.IsDBNull(1) ? "hub" : reader.GetString(1);
            var stateName = reader.IsDBNull(2) ? "unknown" : reader.GetString(2);
            items.Add(new ContentBundleListItemDto(
                hubId, "hub", $"Hub {hubId[..8]}…", stateName, hubId, null,
                $"relation={relationName}, state={stateName}"));
        }
        return items;
    }

    public override async Task<IReadOnlyList<ContentBundleListItemDto>> ListContentEntitiesAsync(CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT e.entity_id::text, COALESCE(e.entity_jsonb->>'label', 'Untitled'), " +
            "       COALESCE(sr.name, e.entity_jsonb->>'state', 'unknown'), " +
            "       e.hub_id::text, e.relation_ids " +
            "FROM topology.entities e " +
            "LEFT JOIN topology.state_registry sr ON sr.state_id = e.state_id " +
            "ORDER BY e.created_at DESC";

        var items = new List<ContentBundleListItemDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var entityId = reader.GetString(0);
            var label = reader.GetString(1);
            var stateName = reader.GetString(2);
            var hubId = reader.GetString(3);
            var relationIds = ReadUuidArray(reader, 4);
            items.Add(new ContentBundleListItemDto(
                entityId, "entity", label, stateName, hubId, relationIds,
                $"hub={hubId}, relations={relationIds.Count}"));
        }
        return items;
    }

    public override async Task<IReadOnlyList<ContentBundleListItemDto>> ListContentRelationsAsync(CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT relation_registry_id::text, name, active::text " +
            "FROM topology.relation_registry " +
            "WHERE active = true ORDER BY name";

        var items = new List<ContentBundleListItemDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetString(0);
            var name = reader.GetString(1);
            items.Add(new ContentBundleListItemDto(
                id, "relation", name, "active", null, null,
                $"relation_registry name={name}"));
        }
        return items;
    }

    public override async Task<IReadOnlyList<ContentBundleStateItemDto>> ListContentStatesAsync(CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT state_id::text, name, owner FROM topology.state_registry ORDER BY name";

        var items = new List<ContentBundleStateItemDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new ContentBundleStateItemDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }
        return items;
    }

    public override async Task<IReadOnlyList<ContentBundleListItemDto>> ListContentHubRelationsAsync(CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT hr.hub_relation_id::text, COALESCE(rr.name, hr.hub_relation_id::text), " +
            "       hr.hub_id::text, hr.relation_registry_id::text, hr.weight::text " +
            "FROM hubs.hub_relations hr " +
            "LEFT JOIN topology.relation_registry rr ON rr.relation_registry_id = hr.relation_registry_id " +
            "ORDER BY hr.created_at DESC";

        var items = new List<ContentBundleListItemDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetString(0);
            var label = reader.GetString(1);
            var hubId = reader.GetString(2);
            var relationId = reader.IsDBNull(3) ? null : reader.GetString(3);
            var weight = reader.GetString(4);
            items.Add(new ContentBundleListItemDto(
                id, "hub_relation", label, "active", hubId,
                relationId is null ? null : [relationId],
                $"hub={hubId}, relation={relationId ?? "—"}, weight={weight}"));
        }
        return items;
    }

    public override async Task<ContentBundleHubDetailDto?> LoadContentHubAsync(Guid hubId, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT h.hub_id::text, COALESCE(sr.name, 'unknown'), h.state_id::text, " +
            "       h.relation_registry_id::text, COALESCE(rr.name, '—') " +
            "FROM hubs.hub h " +
            "LEFT JOIN topology.state_registry sr ON sr.state_id = h.state_id " +
            "LEFT JOIN topology.relation_registry rr ON rr.relation_registry_id = h.relation_registry_id " +
            "WHERE h.hub_id = @id LIMIT 1";
        cmd.Parameters.AddWithValue("id", hubId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        var hubIdStr = reader.GetString(0);
        var stateName = reader.GetString(1);
        var stateId = reader.IsDBNull(2) ? null : reader.GetString(2);
        var relationRegistryId = reader.IsDBNull(3) ? null : reader.GetString(3);
        var relationLabel = reader.GetString(4);
        await reader.CloseAsync();

        var entityIds = new List<string>();
        await using (var entityCmd = conn.CreateCommand())
        {
            entityCmd.CommandText = "SELECT entity_id::text FROM topology.entities WHERE hub_id = @hubId ORDER BY created_at";
            entityCmd.Parameters.AddWithValue("hubId", hubId);
            await using var entityReader = await entityCmd.ExecuteReaderAsync(ct);
            while (await entityReader.ReadAsync(ct))
                entityIds.Add(entityReader.GetString(0));
        }

        var hubRelationCount = 0;
        await using (var hrCmd = conn.CreateCommand())
        {
            hrCmd.CommandText = "SELECT COUNT(*)::int FROM hubs.hub_relations WHERE hub_id = @hubId";
            hrCmd.Parameters.AddWithValue("hubId", hubId);
            hubRelationCount = (int)(await hrCmd.ExecuteScalarAsync(ct) ?? 0);
        }

        return new ContentBundleHubDetailDto(
            hubIdStr, stateName, stateId, relationRegistryId, relationLabel,
            entityIds.Count, hubRelationCount, entityIds,
            $"hub {hubIdStr[..Math.Min(8, hubIdStr.Length)]}… with {entityIds.Count} entity(ies), {hubRelationCount} hub_relation(s)");
    }

    public override async Task<ContentBundleRelationDetailDto?> LoadContentRelationAsync(
        Guid relationRegistryId, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT relation_registry_id::text, name, active FROM topology.relation_registry " +
            "WHERE relation_registry_id = @id LIMIT 1";
        cmd.Parameters.AddWithValue("id", relationRegistryId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        var idStr = reader.GetString(0);
        var name = reader.GetString(1);
        var active = reader.GetBoolean(2);
        await reader.CloseAsync();

        var entityCount = 0;
        await using (var entityCmd = conn.CreateCommand())
        {
            entityCmd.CommandText =
                "SELECT COUNT(*)::int FROM topology.entities WHERE @id = ANY(relation_ids)";
            entityCmd.Parameters.AddWithValue("id", relationRegistryId);
            entityCount = (int)(await entityCmd.ExecuteScalarAsync(ct) ?? 0);
        }

        var hubRelationCount = 0;
        await using (var hrCmd = conn.CreateCommand())
        {
            hrCmd.CommandText =
                "SELECT COUNT(*)::int FROM hubs.hub_relations WHERE relation_registry_id = @id";
            hrCmd.Parameters.AddWithValue("id", relationRegistryId);
            hubRelationCount = (int)(await hrCmd.ExecuteScalarAsync(ct) ?? 0);
        }

        return new ContentBundleRelationDetailDto(
            idStr, name, active, entityCount, hubRelationCount,
            $"relation {name} — {entityCount} entity(ies), {hubRelationCount} hub_relation(s)");
    }

    public override async Task<ContentBundleEntityDetailDto?> LoadContentEntityAsync(Guid entityId, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT e.entity_id::text, COALESCE(e.entity_jsonb->>'label', 'Untitled'), " +
            "       COALESCE(sr.name, e.entity_jsonb->>'state', 'unknown'), e.state_id::text, " +
            "       e.hub_id::text, COALESCE(rr.name, e.hub_id::text), e.relation_ids, e.entity_jsonb::text " +
            "FROM topology.entities e " +
            "LEFT JOIN topology.state_registry sr ON sr.state_id = e.state_id " +
            "LEFT JOIN hubs.hub h ON h.hub_id = e.hub_id " +
            "LEFT JOIN topology.relation_registry rr ON rr.relation_registry_id = h.relation_registry_id " +
            "WHERE e.entity_id = @id LIMIT 1";
        cmd.Parameters.AddWithValue("id", entityId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        var relationIds = ReadUuidArray(reader, 6);
        var relationLabels = await ResolveRelationLabelsAsync(conn, relationIds, ct);
        var entityJsonb = reader.GetString(7);
        var label = reader.GetString(1);

        return new ContentBundleEntityDetailDto(
            reader.GetString(0), label, reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(4), reader.GetString(5),
            relationIds, relationLabels, entityJsonb,
            $"entity {label} in hub {reader.GetString(4)}");
    }

    public override async Task<IReadOnlyList<ContentBundleListItemDto>> SearchContentBundleAsync(
        string? keyword, string? kind, string? state, CancellationToken ct = default)
    {
        var all = new List<ContentBundleListItemDto>();
        var kindNorm = kind?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(kindNorm) || kindNorm == "hub")
            all.AddRange(await ListContentHubsAsync(ct));
        if (string.IsNullOrWhiteSpace(kindNorm) || kindNorm == "entity")
            all.AddRange(await ListContentEntitiesAsync(ct));
        if (string.IsNullOrWhiteSpace(kindNorm) || kindNorm == "relation")
            all.AddRange(await ListContentRelationsAsync(ct));
        if (string.IsNullOrWhiteSpace(kindNorm) || kindNorm == "hub_relation")
            all.AddRange(await ListContentHubRelationsAsync(ct));

        IEnumerable<ContentBundleListItemDto> query = all;

        if (!string.IsNullOrWhiteSpace(state))
        {
            var stateNorm = state.Trim().ToLowerInvariant();
            query = query.Where(i => i.State.Equals(stateNorm, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLowerInvariant();
            query = query.Where(i =>
                i.Label.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                i.Id.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                i.Summary.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }

        return query.ToList();
    }

    public override async Task<(ContentBundleDraftDetailDto? Draft, ValidationError? Error)> CreateEntityDraftAsync(
        Guid hubId,
        JsonElement entityJsonb,
        IReadOnlyList<Guid> relationIds,
        string stateName,
        CancellationToken ct = default)
    {
        if (entityJsonb.ValueKind != JsonValueKind.Object)
            return (null, new ValidationError("ENTITY_JSONB_NOT_OBJECT", "entityJsonb must be a JSON object."));

        if (string.IsNullOrWhiteSpace(stateName))
            return (null, new ValidationError("STATE_NAME_REQUIRED", "stateName is required."));

        await using var conn = await OpenAsync(ct);

        var refs = await LoadRefContextInternalAsync(conn, hubId, relationIds, stateName, ct);
        var tempDraft = new ContentEntityDraftRecord(
            Guid.Empty, hubId, entityJsonb.GetRawText(), relationIds,
            refs.StateNames.FirstOrDefault(kv => kv.Value.Equals(stateName, StringComparison.OrdinalIgnoreCase)).Key,
            "draft", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var validation = ContentBundleValidator.ValidateDraft(tempDraft, refs);
        if (!validation.Valid)
        {
            var first = validation.Issues.First(i => i.IsBlocking);
            return (null, new ValidationError(first.Code, first.Message));
        }

        var stateId = refs.StateNames.First(kv => kv.Value.Equals(stateName, StringComparison.OrdinalIgnoreCase)).Key;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO topology.content_entity_drafts (hub_id, entity_jsonb, relation_ids, state_id) " +
            "VALUES (@hubId, @jsonb::jsonb, @relationIds, @stateId) " +
            "RETURNING draft_id, status, created_at, updated_at";
        cmd.Parameters.AddWithValue("hubId", hubId);
        cmd.Parameters.AddWithValue("jsonb", entityJsonb.GetRawText());
        cmd.Parameters.AddWithValue("relationIds", relationIds.ToArray());
        cmd.Parameters.AddWithValue("stateId", stateId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return (null, new ValidationError("DRAFT_CREATE_FAILED", "Failed to create draft."));

        return (new ContentBundleDraftDetailDto(
            reader.GetGuid(0).ToString(), reader.GetString(1),
            hubId.ToString(), entityJsonb.GetRawText(),
            relationIds.Select(r => r.ToString()).ToList(),
            stateName, stateId.ToString(), null,
            reader.GetFieldValue<DateTimeOffset>(2).ToString("o"),
            reader.GetFieldValue<DateTimeOffset>(3).ToString("o")), null);
    }

    public override async Task<(ContentBundleDraftDetailDto? Draft, ValidationError? Error)> UpdateEntityDraftAsync(
        Guid draftId,
        Guid hubId,
        JsonElement entityJsonb,
        IReadOnlyList<Guid> relationIds,
        string stateName,
        CancellationToken ct = default)
    {
        var existing = await LoadDraftAsync(draftId, ct);
        if (existing is null)
            return (null, new ValidationError("DRAFT_NOT_FOUND", $"Draft {draftId} was not found."));
        if (existing.Status != "draft")
            return (null, new ValidationError("DRAFT_NOT_EDITABLE", $"Draft {draftId} is not in draft status."));

        if (entityJsonb.ValueKind != JsonValueKind.Object)
            return (null, new ValidationError("ENTITY_JSONB_NOT_OBJECT", "entityJsonb must be a JSON object."));
        if (string.IsNullOrWhiteSpace(stateName))
            return (null, new ValidationError("STATE_NAME_REQUIRED", "stateName is required."));

        await using var conn = await OpenAsync(ct);
        var refs = await LoadRefContextInternalAsync(conn, hubId, relationIds, stateName, ct);
        var tempDraft = new ContentEntityDraftRecord(
            draftId, hubId, entityJsonb.GetRawText(), relationIds,
            refs.StateNames.FirstOrDefault(kv => kv.Value.Equals(stateName, StringComparison.OrdinalIgnoreCase)).Key,
            "draft", null, existing.CreatedAt, DateTimeOffset.UtcNow);

        var validation = ContentBundleValidator.ValidateDraft(tempDraft, refs);
        if (!validation.Valid)
        {
            var first = validation.Issues.First(i => i.IsBlocking);
            return (null, new ValidationError(first.Code, first.Message));
        }

        var stateId = refs.StateNames.First(kv => kv.Value.Equals(stateName, StringComparison.OrdinalIgnoreCase)).Key;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "UPDATE topology.content_entity_drafts " +
            "SET hub_id = @hubId, entity_jsonb = @jsonb::jsonb, relation_ids = @relationIds, " +
            "    state_id = @stateId, updated_at = now() " +
            "WHERE draft_id = @draftId AND status = 'draft' " +
            "RETURNING draft_id, status, created_at, updated_at";
        cmd.Parameters.AddWithValue("hubId", hubId);
        cmd.Parameters.AddWithValue("jsonb", entityJsonb.GetRawText());
        cmd.Parameters.AddWithValue("relationIds", relationIds.ToArray());
        cmd.Parameters.AddWithValue("stateId", stateId);
        cmd.Parameters.AddWithValue("draftId", draftId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return (null, new ValidationError("DRAFT_UPDATE_FAILED", "Failed to update draft."));

        return (new ContentBundleDraftDetailDto(
            reader.GetGuid(0).ToString(), reader.GetString(1),
            hubId.ToString(), entityJsonb.GetRawText(),
            relationIds.Select(r => r.ToString()).ToList(),
            stateName, stateId.ToString(), null,
            reader.GetFieldValue<DateTimeOffset>(2).ToString("o"),
            reader.GetFieldValue<DateTimeOffset>(3).ToString("o")), null);
    }

    public override async Task<ContentEntityDraftRecord?> LoadDraftAsync(Guid draftId, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT draft_id, hub_id, entity_jsonb::text, relation_ids, state_id, status, " +
            "       promoted_entity_id, created_at, updated_at " +
            "FROM topology.content_entity_drafts WHERE draft_id = @id LIMIT 1";
        cmd.Parameters.AddWithValue("id", draftId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new ContentEntityDraftRecord(
            reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2),
            ReadUuidArrayAsGuids(reader, 3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetGuid(6),
            reader.GetFieldValue<DateTimeOffset>(7),
            reader.GetFieldValue<DateTimeOffset>(8));
    }

    public override async Task<ContentBundleRefContext> LoadRefContextAsync(
        Guid hubId, IReadOnlyList<Guid> relationIds, string? stateName, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        return await LoadRefContextInternalAsync(conn, hubId, relationIds, stateName, ct);
    }

    public override async Task<(ContentBundleLifecycleResponseDto Response, ValidationError? Error)> PromoteDraftAsync(
        Guid draftId, CancellationToken ct = default)
    {
        var draft = await LoadDraftAsync(draftId, ct);
        if (draft is null)
        {
            return (new ContentBundleLifecycleResponseDto(
                false, draftId.ToString(), null, "draft", "Draft not found.", null, "DRAFT_NOT_FOUND"), null);
        }

        var stateName = draft.StateId is null
            ? null
            : (await ListContentStatesAsync(ct)).FirstOrDefault(s => s.StateId == draft.StateId.Value.ToString())?.Name;

        var refs = await LoadRefContextAsync(draft.HubId, draft.RelationIds, stateName, ct);
        var validation = ContentBundleValidator.ValidateDraft(draft, refs);
        if (!validation.Valid)
        {
            var first = validation.Issues.First(i => i.IsBlocking);
            return (new ContentBundleLifecycleResponseDto(
                false, draftId.ToString(), null, draft.Status, first.Message, null, first.Code), null);
        }

        var entityId = Guid.NewGuid();
        await using var conn = await OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            var insert = conn.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText =
                "INSERT INTO topology.entities (entity_id, hub_id, entity_jsonb, relation_ids, state_id) " +
                "VALUES (@entityId, @hubId, @jsonb::jsonb, @relationIds, @stateId)";
            insert.Parameters.AddWithValue("entityId", entityId);
            insert.Parameters.AddWithValue("hubId", draft.HubId);
            insert.Parameters.AddWithValue("jsonb", draft.EntityJsonb);
            insert.Parameters.AddWithValue("relationIds", draft.RelationIds.ToArray());
            insert.Parameters.AddWithValue("stateId", draft.StateId!.Value);
            await insert.ExecuteNonQueryAsync(ct);

            var update = conn.CreateCommand();
            update.Transaction = tx;
            update.CommandText =
                "UPDATE topology.content_entity_drafts " +
                "SET status = 'promoted', promoted_entity_id = @entityId, updated_at = now() " +
                "WHERE draft_id = @draftId AND status = 'draft'";
            update.Parameters.AddWithValue("entityId", entityId);
            update.Parameters.AddWithValue("draftId", draftId);
            var rows = await update.ExecuteNonQueryAsync(ct);
            if (rows == 0)
            {
                await tx.RollbackAsync(ct);
                return (new ContentBundleLifecycleResponseDto(
                    false, draftId.ToString(), null, draft.Status,
                    "Draft is not in promotable state.", null, "DRAFT_NOT_PROMOTABLE"), null);
            }

            var log = conn.CreateCommand();
            log.Transaction = tx;
            log.CommandText =
                "INSERT INTO topology.topology_edit_log (target_table, target_id, operation, after_json) " +
                "VALUES ('content_bundle:entity', @targetId, 'promote', @after::jsonb)";
            log.Parameters.AddWithValue("targetId", entityId.ToString());
            log.Parameters.AddWithValue("after", draft.EntityJsonb);
            await log.ExecuteNonQueryAsync(ct);

            await tx.CommitAsync(ct);
        }
        catch (PostgresException ex)
        {
            await tx.RollbackAsync(ct);
            return (new ContentBundleLifecycleResponseDto(
                false, draftId.ToString(), null, draft.Status, ex.MessageText, null, "PERSISTENCE_ERROR"), null);
        }

        var readback = await LoadContentEntityAsync(entityId, ct);
        if (readback is null)
        {
            return (new ContentBundleLifecycleResponseDto(
                false, draftId.ToString(), entityId.ToString(), "promoted",
                "Entity promoted but post-write readback failed — explicit inconsistency.",
                null, "READBACK_INCONSISTENCY"), null);
        }

        return (new ContentBundleLifecycleResponseDto(
            true, draftId.ToString(), entityId.ToString(), "promoted",
            "Draft promoted to active entity.", readback), null);
    }

    private async Task<ContentBundleRefContext> LoadRefContextInternalAsync(
        NpgsqlConnection conn,
        Guid hubId,
        IReadOnlyList<Guid> relationIds,
        string? stateName,
        CancellationToken ct)
    {
        var hubExists = false;
        string? hubRelationName = null;

        await using (var hubCmd = conn.CreateCommand())
        {
            hubCmd.CommandText =
                "SELECT rr.name FROM hubs.hub h " +
                "LEFT JOIN topology.relation_registry rr ON rr.relation_registry_id = h.relation_registry_id " +
                "WHERE h.hub_id = @hubId LIMIT 1";
            hubCmd.Parameters.AddWithValue("hubId", hubId);
            var result = await hubCmd.ExecuteScalarAsync(ct);
            if (result is not null and not DBNull)
            {
                hubExists = true;
                hubRelationName = result.ToString();
            }
        }

        var relationNames = new Dictionary<Guid, string>();
        if (relationIds.Count > 0)
        {
            await using var relCmd = conn.CreateCommand();
            relCmd.CommandText =
                "SELECT relation_registry_id, name FROM topology.relation_registry " +
                "WHERE active = true AND relation_registry_id = ANY(@ids)";
            relCmd.Parameters.AddWithValue("ids", relationIds.ToArray());
            await using var relReader = await relCmd.ExecuteReaderAsync(ct);
            while (await relReader.ReadAsync(ct))
                relationNames[relReader.GetGuid(0)] = relReader.GetString(1);
        }

        var stateNames = new Dictionary<Guid, string>();
        await using (var stateCmd = conn.CreateCommand())
        {
            stateCmd.CommandText = "SELECT state_id, name FROM topology.state_registry";
            await using var stateReader = await stateCmd.ExecuteReaderAsync(ct);
            while (await stateReader.ReadAsync(ct))
                stateNames[stateReader.GetGuid(0)] = stateReader.GetString(1);
        }

        return new ContentBundleRefContext(hubExists, hubRelationName, relationNames, stateNames);
    }

    private async Task<IReadOnlyList<string>> ResolveRelationLabelsAsync(
        NpgsqlConnection conn, IReadOnlyList<string> relationIds, CancellationToken ct)
    {
        if (relationIds.Count == 0) return [];
        var guids = relationIds.Select(Guid.Parse).ToArray();
        var labels = new List<string>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT name FROM topology.relation_registry WHERE relation_registry_id = ANY(@ids)";
        cmd.Parameters.AddWithValue("ids", guids);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            labels.Add(reader.GetString(0));
        return labels;
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }

    private static IReadOnlyList<string> ReadUuidArray(NpgsqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return [];
        var arr = reader.GetFieldValue<Guid[]>(ordinal);
        return arr.Select(g => g.ToString()).ToList();
    }

    private static IReadOnlyList<Guid> ReadUuidArrayAsGuids(NpgsqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return [];
        return reader.GetFieldValue<Guid[]>(ordinal);
    }
}
