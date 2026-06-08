using Microsoft.Extensions.Logging;
using Npgsql;
using System.Text.Json;
using Topolactor.Schema;

namespace Topolactor.Repository;

// ---------------------------------------------------------------------------
// NpgsqlTeamMarkdownRepository — production Npgsql implementation.
// Operates against topology.team_markdown_* tables
//   (db/migrations/team_markdown_registry_tables.sql).
// All failures are explicit errors; no silent fallback.
// SSOT: docs/design/team-markdown-dashboard-saved-view-ssot.yaml
// ---------------------------------------------------------------------------

public class NpgsqlTeamMarkdownRepository : TeamMarkdownRepository
{
    private readonly string _connectionString;

    public NpgsqlTeamMarkdownRepository(ILogger<NpgsqlTeamMarkdownRepository> logger, string connectionString)
        : base(logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    // ─── template create ─────────────────────────────────────────────────────

    public override async Task<(string? TemplateId, string? ErrorCode, string? Message)>
        CreateTemplateAsync(TeamMarkdownTemplateCreateRequest request, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO topology.team_markdown_template_registry " +
                "(template_key, template_label, template_markdown, placeholder_schema_json, status) " +
                "VALUES (@key, @label, @markdown, @schema::jsonb, 'active') " +
                "RETURNING template_id";
            cmd.Parameters.AddWithValue("key", request.TemplateKey);
            cmd.Parameters.AddWithValue("label", request.TemplateLabel);
            cmd.Parameters.AddWithValue("markdown", request.TemplateMarkdown);
            cmd.Parameters.AddWithValue("schema", request.PlaceholderSchemaJson.GetRawText());
            var templateId = await cmd.ExecuteScalarAsync(ct) as Guid?;
            return (templateId?.ToString(), null, null);
        }
        catch (PostgresException pgEx) when (pgEx.SqlState == "23505")
        {
            return (null, "TEMPLATE_KEY_DUPLICATE", $"A template with key '{request.TemplateKey}' already exists.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NpgsqlTeamMarkdownRepository.CreateTemplateAsync failed");
            return (null, "DB_UNAVAILABLE", ex.Message);
        }
    }

    // ─── template list ───────────────────────────────────────────────────────

    public override async Task<(IReadOnlyList<TeamMarkdownTemplateListItem> Items, string? ErrorCode, string? Message)>
        ListTemplatesAsync(string status = "active", CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT template_id, template_key, template_label, status, created_at, updated_at " +
                "FROM topology.team_markdown_template_registry " +
                "WHERE status = @status " +
                "ORDER BY created_at DESC";
            cmd.Parameters.AddWithValue("status", status);
            var results = new List<TeamMarkdownTemplateListItem>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                results.Add(new TeamMarkdownTemplateListItem(
                    TemplateId: reader.GetGuid(0).ToString(),
                    TemplateKey: reader.GetString(1),
                    TemplateLabel: reader.GetString(2),
                    Status: reader.GetString(3),
                    CreatedAt: reader.GetDateTime(4).ToString("o"),
                    UpdatedAt: reader.GetDateTime(5).ToString("o")
                ));
            }
            return (results, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NpgsqlTeamMarkdownRepository.ListTemplatesAsync failed");
            return ([], "DB_UNAVAILABLE", ex.Message);
        }
    }

    // ─── template get ────────────────────────────────────────────────────────

    public override async Task<(TeamMarkdownTemplateDetail? Detail, string? ErrorCode, string? Message)>
        GetTemplateAsync(Guid templateId, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT template_id, template_key, template_label, template_markdown, " +
                "placeholder_schema_json, status, created_at, updated_at " +
                "FROM topology.team_markdown_template_registry " +
                "WHERE template_id = @id";
            cmd.Parameters.AddWithValue("id", templateId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return (null, null, null);
            return (new TeamMarkdownTemplateDetail(
                TemplateId: reader.GetGuid(0).ToString(),
                TemplateKey: reader.GetString(1),
                TemplateLabel: reader.GetString(2),
                TemplateMarkdown: reader.GetString(3),
                PlaceholderSchemaJson: JsonSerializer.Deserialize<JsonElement>(reader.GetString(4)),
                Status: reader.GetString(5),
                CreatedAt: reader.GetDateTime(6).ToString("o"),
                UpdatedAt: reader.GetDateTime(7).ToString("o")
            ), null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NpgsqlTeamMarkdownRepository.GetTemplateAsync failed");
            return (null, "DB_UNAVAILABLE", ex.Message);
        }
    }

    // ─── template update ─────────────────────────────────────────────────────

    public override async Task<(bool Updated, string? ErrorCode, string? Message)>
        UpdateTemplateAsync(Guid templateId, string label, string markdown, string status, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "UPDATE topology.team_markdown_template_registry " +
                "SET template_label = @label, template_markdown = @markdown, status = @status, updated_at = now() " +
                "WHERE template_id = @id";
            cmd.Parameters.AddWithValue("id", templateId);
            cmd.Parameters.AddWithValue("label", label);
            cmd.Parameters.AddWithValue("markdown", markdown);
            cmd.Parameters.AddWithValue("status", status);
            var rows = await cmd.ExecuteNonQueryAsync(ct);
            if (rows == 0) return (false, "TEMPLATE_NOT_FOUND", $"Template {templateId} not found");
            return (true, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NpgsqlTeamMarkdownRepository.UpdateTemplateAsync failed");
            return (false, "DB_UNAVAILABLE", ex.Message);
        }
    }

    // ─── template archive ────────────────────────────────────────────────────

    public override async Task<(bool Updated, string? ErrorCode)>
        ArchiveTemplateAsync(Guid templateId, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "UPDATE topology.team_markdown_template_registry " +
                "SET status = 'archived', updated_at = now() " +
                "WHERE template_id = @id";
            cmd.Parameters.AddWithValue("id", templateId);
            var rows = await cmd.ExecuteNonQueryAsync(ct);
            if (rows == 0) return (false, "TEMPLATE_NOT_FOUND");
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NpgsqlTeamMarkdownRepository.ArchiveTemplateAsync failed");
            return (false, "DB_UNAVAILABLE");
        }
    }

    // ─── saved view create ───────────────────────────────────────────────────

    public override async Task<(string? SavedViewId, string? ErrorCode, string? Message)>
        CreateSavedViewAsync(TeamMarkdownSavedViewCreateRequest request, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO topology.team_markdown_saved_view " +
                "(template_id, title, source_table_ref, source_record_ref, binding_json, " +
                "completed_preset_seed_json, rendered_markdown, user_adjustment_patch_json, " +
                "search_index_text, card_metadata_json, status) " +
                "VALUES (@template_id, @title, @source_table, @source_record, @binding::jsonb, " +
                "@seed::jsonb, @rendered, @adjustment::jsonb, @search_index, @card_meta::jsonb, 'active') " +
                "RETURNING saved_view_id";
            cmd.Parameters.AddWithValue("template_id", Guid.Parse(request.TemplateId));
            cmd.Parameters.AddWithValue("title", request.Title);
            cmd.Parameters.AddWithValue("source_table", request.SourceTableRef);
            cmd.Parameters.AddWithValue("source_record", request.SourceRecordRef);
            cmd.Parameters.AddWithValue("binding", request.BindingJson.GetRawText());
            cmd.Parameters.AddWithValue("seed", request.CompletedPresetSeedJson.GetRawText());
            cmd.Parameters.AddWithValue("rendered", request.RenderedMarkdown);
            cmd.Parameters.AddWithValue("adjustment", request.UserAdjustmentPatchJson.GetRawText());
            cmd.Parameters.AddWithValue("search_index", request.SearchIndexText);
            cmd.Parameters.AddWithValue("card_meta", request.CardMetadataJson.GetRawText());
            var savedViewId = await cmd.ExecuteScalarAsync(ct) as Guid?;
            return (savedViewId?.ToString(), null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NpgsqlTeamMarkdownRepository.CreateSavedViewAsync failed");
            return (null, "DB_UNAVAILABLE", ex.Message);
        }
    }

    // ─── saved view search ───────────────────────────────────────────────────

    public override async Task<(IReadOnlyList<TeamMarkdownSavedViewCard> Cards, string? ErrorCode, string? Message)>
        SearchSavedViewsAsync(string? query, string status = "active", int limit = 50, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();

            var searchFilter = "";
            if (!string.IsNullOrWhiteSpace(query))
            {
                searchFilter =
                    "AND (title ILIKE @q OR search_index_text ILIKE @q OR source_table_ref ILIKE @q) ";
                cmd.Parameters.AddWithValue("q", $"%{query}%");
            }

            cmd.CommandText =
                "SELECT sv.saved_view_id, sv.title, t.template_key, sv.source_table_ref, " +
                "sv.source_record_ref, sv.status, sv.updated_at, sv.card_metadata_json " +
                "FROM topology.team_markdown_saved_view sv " +
                "JOIN topology.team_markdown_template_registry t ON t.template_id = sv.template_id " +
                $"WHERE sv.status = @status {searchFilter}" +
                "ORDER BY sv.updated_at DESC " +
                "LIMIT @limit";
            cmd.Parameters.AddWithValue("status", status);
            cmd.Parameters.AddWithValue("limit", limit);

            var results = new List<TeamMarkdownSavedViewCard>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                results.Add(new TeamMarkdownSavedViewCard(
                    SavedViewId: reader.GetGuid(0).ToString(),
                    Title: reader.GetString(1),
                    TemplateKey: reader.GetString(2),
                    SourceTableRef: reader.GetString(3),
                    SourceRecordRef: reader.GetString(4),
                    Status: reader.GetString(5),
                    UpdatedAt: reader.GetDateTime(6).ToString("o"),
                    CardMetadataJson: JsonSerializer.Deserialize<JsonElement>(reader.GetString(7))
                ));
            }
            return (results, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NpgsqlTeamMarkdownRepository.SearchSavedViewsAsync failed");
            return ([], "DB_UNAVAILABLE", ex.Message);
        }
    }

    // ─── saved view get ──────────────────────────────────────────────────────

    public override async Task<(TeamMarkdownSavedViewDetail? Detail, string? ErrorCode, string? Message)>
        GetSavedViewAsync(Guid savedViewId, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT sv.saved_view_id, sv.template_id, t.template_key, sv.title, " +
                "sv.source_table_ref, sv.source_record_ref, sv.binding_json, " +
                "sv.completed_preset_seed_json, sv.rendered_markdown, sv.user_adjustment_patch_json, " +
                "sv.search_index_text, sv.card_metadata_json, sv.status, sv.created_at, sv.updated_at " +
                "FROM topology.team_markdown_saved_view sv " +
                "JOIN topology.team_markdown_template_registry t ON t.template_id = sv.template_id " +
                "WHERE sv.saved_view_id = @id";
            cmd.Parameters.AddWithValue("id", savedViewId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return (null, null, null);
            return (new TeamMarkdownSavedViewDetail(
                SavedViewId: reader.GetGuid(0).ToString(),
                TemplateId: reader.GetGuid(1).ToString(),
                TemplateKey: reader.GetString(2),
                Title: reader.GetString(3),
                SourceTableRef: reader.GetString(4),
                SourceRecordRef: reader.GetString(5),
                BindingJson: JsonSerializer.Deserialize<JsonElement>(reader.GetString(6)),
                CompletedPresetSeedJson: JsonSerializer.Deserialize<JsonElement>(reader.GetString(7)),
                RenderedMarkdown: reader.GetString(8),
                UserAdjustmentPatchJson: JsonSerializer.Deserialize<JsonElement>(reader.GetString(9)),
                SearchIndexText: reader.GetString(10),
                CardMetadataJson: JsonSerializer.Deserialize<JsonElement>(reader.GetString(11)),
                Status: reader.GetString(12),
                CreatedAt: reader.GetDateTime(13).ToString("o"),
                UpdatedAt: reader.GetDateTime(14).ToString("o")
            ), null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NpgsqlTeamMarkdownRepository.GetSavedViewAsync failed");
            return (null, "DB_UNAVAILABLE", ex.Message);
        }
    }

    // ─── saved view update ───────────────────────────────────────────────────

    public override async Task<(bool Updated, string? ErrorCode, string? Message)>
        UpdateSavedViewAsync(Guid savedViewId, string? title, string? renderedMarkdown,
            JsonElement? userAdjustmentPatchJson,
            JsonElement? completedPresetSeedJson,
            string? searchIndexText,
            JsonElement? cardMetadataJson,
            CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "UPDATE topology.team_markdown_saved_view SET " +
                "title = COALESCE(@title, title), " +
                "rendered_markdown = COALESCE(@rendered, rendered_markdown), " +
                "user_adjustment_patch_json = COALESCE(@adjustment::jsonb, user_adjustment_patch_json), " +
                "completed_preset_seed_json = COALESCE(@seed::jsonb, completed_preset_seed_json), " +
                "search_index_text = COALESCE(@search_index, search_index_text), " +
                "card_metadata_json = COALESCE(@card_meta::jsonb, card_metadata_json), " +
                "updated_at = now() " +
                "WHERE saved_view_id = @id AND status != 'archived'";
            cmd.Parameters.AddWithValue("id", savedViewId);
            cmd.Parameters.AddWithValue("title", (object?)title ?? DBNull.Value);
            cmd.Parameters.AddWithValue("rendered", (object?)renderedMarkdown ?? DBNull.Value);
            cmd.Parameters.AddWithValue("adjustment",
                userAdjustmentPatchJson.HasValue ? (object)userAdjustmentPatchJson.Value.GetRawText() : DBNull.Value);
            cmd.Parameters.AddWithValue("seed",
                completedPresetSeedJson.HasValue ? (object)completedPresetSeedJson.Value.GetRawText() : DBNull.Value);
            cmd.Parameters.AddWithValue("search_index", (object?)searchIndexText ?? DBNull.Value);
            cmd.Parameters.AddWithValue("card_meta",
                cardMetadataJson.HasValue ? (object)cardMetadataJson.Value.GetRawText() : DBNull.Value);
            var rows = await cmd.ExecuteNonQueryAsync(ct);
            if (rows == 0) return (false, "SAVED_VIEW_NOT_FOUND", $"Saved view {savedViewId} not found or archived");
            return (true, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NpgsqlTeamMarkdownRepository.UpdateSavedViewAsync failed");
            return (false, "DB_UNAVAILABLE", ex.Message);
        }
    }

    // ─── saved view archive ──────────────────────────────────────────────────

    public override async Task<(bool Updated, string? ErrorCode)>
        ArchiveSavedViewAsync(Guid savedViewId, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "UPDATE topology.team_markdown_saved_view " +
                "SET status = 'archived', updated_at = now() " +
                "WHERE saved_view_id = @id";
            cmd.Parameters.AddWithValue("id", savedViewId);
            var rows = await cmd.ExecuteNonQueryAsync(ct);
            if (rows == 0) return (false, "SAVED_VIEW_NOT_FOUND");
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NpgsqlTeamMarkdownRepository.ArchiveSavedViewAsync failed");
            return (false, "DB_UNAVAILABLE");
        }
    }

    // ─── event append ────────────────────────────────────────────────────────

    public override async Task<string?> AppendEventAsync(
        Guid savedViewId, string eventKind, Guid? actorId,
        JsonElement eventPayloadJson, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO topology.team_markdown_saved_view_event " +
                "(saved_view_id, event_kind, actor_id, event_payload_json) " +
                "VALUES (@saved_view_id, @kind, @actor, @payload::jsonb) " +
                "RETURNING event_id";
            cmd.Parameters.AddWithValue("saved_view_id", savedViewId);
            cmd.Parameters.AddWithValue("kind", eventKind);
            cmd.Parameters.AddWithValue("actor", actorId.HasValue ? (object)actorId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("payload", eventPayloadJson.GetRawText());
            var eventId = await cmd.ExecuteScalarAsync(ct) as Guid?;
            return eventId?.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NpgsqlTeamMarkdownRepository.AppendEventAsync failed");
            return null;
        }
    }
}
