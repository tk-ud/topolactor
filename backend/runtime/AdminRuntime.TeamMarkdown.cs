using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

// ---------------------------------------------------------------------------
// AdminRuntime — Team Markdown Dashboard Saved View actions.
// SSOT: docs/design/team-markdown-dashboard-saved-view-ssot.yaml
// Entry: AdminRuntime.ExecuteDataAsync layer=team_markdown actions:
//
//   template: create | list | get | update | archive
//   saved_view: create | search | get | refresh | update | archive
//
// Boundary invariants:
//   - completed_preset_seed_json is required for saved view create/refresh/clone/projection
//   - seed validation failure is an explicit blocking error (no silent fallback)
//   - refresh uses seed binding_json NOT Markdown body parsing
//   - Markdown body is NOT runtime SSOT
//   - frontend does NOT write to topology.team_markdown_* directly
//   - active topology is NOT mutated by saved view operations
// ---------------------------------------------------------------------------

public partial class AdminRuntime
{
    // ─── template create ─────────────────────────────────────────────────────

    private async Task<(JsonElement? data, ValidationError? error)>
        DataTeamMarkdownTemplateCreateAsync(OperationVector vector, CancellationToken ct)
    {
        if (_teamMarkdownRepository is null)
            return (null, new ValidationError("TEAM_MARKDOWN_NOT_CONFIGURED", "TeamMarkdownRepository is not registered"));

        var payload = vector.Payload;
        if (payload is null)
            return (null, new ValidationError("TEMPLATE_PAYLOAD_REQUIRED", "payload is required for team_markdown:template:create"));

        var p = payload.Value;
        var templateKey = p.TryGetProperty("templateKey", out var tk) ? tk.GetString() : null;
        var templateLabel = p.TryGetProperty("templateLabel", out var tl) ? tl.GetString() : null;
        var templateMarkdown = p.TryGetProperty("templateMarkdown", out var tm) ? tm.GetString() ?? "" : "";
        var placeholderSchema = p.TryGetProperty("placeholderSchemaJson", out var ps)
            ? ps : JsonSerializer.SerializeToElement(new { });

        if (string.IsNullOrWhiteSpace(templateKey))
            return (null, new ValidationError("TEMPLATE_KEY_REQUIRED", "templateKey is required"));
        if (string.IsNullOrWhiteSpace(templateLabel))
            return (null, new ValidationError("TEMPLATE_LABEL_REQUIRED", "templateLabel is required"));

        var request = new TeamMarkdownTemplateCreateRequest(
            TemplateKey: templateKey!,
            TemplateLabel: templateLabel!,
            TemplateMarkdown: templateMarkdown,
            PlaceholderSchemaJson: placeholderSchema
        );

        var (templateId, errorCode, message) = await _teamMarkdownRepository.CreateTemplateAsync(request, ct);
        if (errorCode is not null)
            return (null, new ValidationError(errorCode, message ?? errorCode));

        _logger.LogInformation("AdminRuntime.TeamMarkdown.TemplateCreate: templateId={TemplateId} key={Key}", templateId, templateKey);
        return (JsonSerializer.SerializeToElement(new { ok = true, templateId, templateKey }), null);
    }

    // ─── template list ───────────────────────────────────────────────────────

    private async Task<(JsonElement? data, ValidationError? error)>
        DataTeamMarkdownTemplateListAsync(OperationVector vector, CancellationToken ct)
    {
        if (_teamMarkdownRepository is null)
            return (null, new ValidationError("TEAM_MARKDOWN_NOT_CONFIGURED", "TeamMarkdownRepository is not registered"));

        var status = "active";
        if (vector.Payload.HasValue &&
            vector.Payload.Value.TryGetProperty("status", out var statusEl))
            status = statusEl.GetString() ?? "active";

        var (items, dbError, dbMsg) = await _teamMarkdownRepository.ListTemplatesAsync(status, ct);
        if (dbError is not null)
            return (null, new ValidationError(dbError, dbMsg ?? dbError));
        return (JsonSerializer.SerializeToElement(new { ok = true, templates = items }), null);
    }

    // ─── template get ────────────────────────────────────────────────────────

    private async Task<(JsonElement? data, ValidationError? error)>
        DataTeamMarkdownTemplateGetAsync(OperationVector vector, CancellationToken ct)
    {
        if (_teamMarkdownRepository is null)
            return (null, new ValidationError("TEAM_MARKDOWN_NOT_CONFIGURED", "TeamMarkdownRepository is not registered"));

        var idStr = vector.IdOrHubId;
        if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var templateId))
            return (null, new ValidationError("TEMPLATE_ID_REQUIRED", "idOrHubId must be a valid template UUID"));

        var (detail, dbError, dbMsg) = await _teamMarkdownRepository.GetTemplateAsync(templateId, ct);
        if (dbError is not null)
            return (null, new ValidationError(dbError, dbMsg ?? dbError));
        if (detail is null)
            return (null, new ValidationError("TEMPLATE_NOT_FOUND", $"Template {templateId} not found"));

        return (JsonSerializer.SerializeToElement(new { ok = true, template = detail }), null);
    }

    // ─── template update ─────────────────────────────────────────────────────

    private async Task<(JsonElement? data, ValidationError? error)>
        DataTeamMarkdownTemplateUpdateAsync(OperationVector vector, CancellationToken ct)
    {
        if (_teamMarkdownRepository is null)
            return (null, new ValidationError("TEAM_MARKDOWN_NOT_CONFIGURED", "TeamMarkdownRepository is not registered"));

        var idStr = vector.IdOrHubId;
        if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var templateId))
            return (null, new ValidationError("TEMPLATE_ID_REQUIRED", "idOrHubId must be a valid template UUID"));

        if (vector.Payload is null)
            return (null, new ValidationError("TEMPLATE_PAYLOAD_REQUIRED", "payload is required for team_markdown:template:update"));

        var p = vector.Payload.Value;
        var label = p.TryGetProperty("templateLabel", out var tl) ? tl.GetString() ?? "" : "";
        var markdown = p.TryGetProperty("templateMarkdown", out var tm) ? tm.GetString() ?? "" : "";
        var status = p.TryGetProperty("status", out var stEl) ? stEl.GetString() ?? "active" : "active";

        if (string.IsNullOrWhiteSpace(label))
            return (null, new ValidationError("TEMPLATE_LABEL_REQUIRED", "templateLabel is required"));

        var (updated, errorCode, message) = await _teamMarkdownRepository.UpdateTemplateAsync(templateId, label, markdown, status, ct);
        if (errorCode is not null)
            return (null, new ValidationError(errorCode, message ?? errorCode));

        _logger.LogInformation("AdminRuntime.TeamMarkdown.TemplateUpdate: templateId={TemplateId}", templateId);
        return (JsonSerializer.SerializeToElement(new { ok = true, templateId = templateId.ToString() }), null);

    }

    // ─── template archive ────────────────────────────────────────────────────

    private async Task<(JsonElement? data, ValidationError? error)>
        DataTeamMarkdownTemplateArchiveAsync(OperationVector vector, CancellationToken ct)
    {
        if (_teamMarkdownRepository is null)
            return (null, new ValidationError("TEAM_MARKDOWN_NOT_CONFIGURED", "TeamMarkdownRepository is not registered"));

        var idStr = vector.IdOrHubId;
        if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var templateId))
            return (null, new ValidationError("TEMPLATE_ID_REQUIRED", "idOrHubId must be a valid template UUID"));

        var (updated, errorCode) = await _teamMarkdownRepository.ArchiveTemplateAsync(templateId, ct);
        if (errorCode is not null)
            return (null, new ValidationError(errorCode, $"Archive failed for template {templateId}"));

        return (JsonSerializer.SerializeToElement(new { ok = true, templateId = templateId.ToString() }), null);
    }

    // ─── saved view create ───────────────────────────────────────────────────

    private async Task<(JsonElement? data, ValidationError? error)>
        DataTeamMarkdownSavedViewCreateAsync(OperationVector vector, CancellationToken ct)
    {
        if (_teamMarkdownRepository is null)
            return (null, new ValidationError("TEAM_MARKDOWN_NOT_CONFIGURED", "TeamMarkdownRepository is not registered"));

        if (vector.Payload is null)
            return (null, new ValidationError("SAVED_VIEW_PAYLOAD_REQUIRED", "payload is required for team_markdown:saved_view:create"));

        var p = vector.Payload.Value;
        var templateIdStr = p.TryGetProperty("templateId", out var tid) ? tid.GetString() : null;
        var title = p.TryGetProperty("title", out var t) ? t.GetString() : null;
        var sourceTableRef = p.TryGetProperty("sourceTableRef", out var str_) ? str_.GetString() : null;
        var sourceRecordRef = p.TryGetProperty("sourceRecordRef", out var srr) ? srr.GetString() : null;
        var renderedMarkdown = p.TryGetProperty("renderedMarkdown", out var rm) ? rm.GetString() ?? "" : "";

        if (string.IsNullOrWhiteSpace(templateIdStr) || !Guid.TryParse(templateIdStr, out _))
            return (null, new ValidationError("TEMPLATE_ID_REQUIRED", "templateId must be a valid UUID"));
        if (string.IsNullOrWhiteSpace(title))
            return (null, new ValidationError("SAVED_VIEW_TITLE_REQUIRED", "title is required"));
        if (string.IsNullOrWhiteSpace(sourceTableRef))
            return (null, new ValidationError("PHYSICAL_TABLE_REF_NOT_REGISTERED", "sourceTableRef is required"));
        if (string.IsNullOrWhiteSpace(sourceRecordRef))
            return (null, new ValidationError("SOURCE_RECORD_NOT_FOUND", "sourceRecordRef is required"));

        if (!p.TryGetProperty("completedPresetSeedJson", out var seedEl) || seedEl.ValueKind == JsonValueKind.Null)
            return (null, new ValidationError("COMPLETED_PRESET_SEED_MISSING",
                "completedPresetSeedJson is required; saved view cannot be created without a valid preset seed"));

        var seedValidationError = CompletedPresetSeedValidator.Validate(seedEl);
        if (seedValidationError is not null)
            return (null, seedValidationError);

        var bindingJson = p.TryGetProperty("bindingJson", out var bj)
            ? bj : JsonSerializer.SerializeToElement(new { });
        var adjustmentJson = p.TryGetProperty("userAdjustmentPatchJson", out var ua)
            ? ua : JsonSerializer.SerializeToElement(new { });
        var searchIndex = p.TryGetProperty("searchIndexText", out var si) ? si.GetString() ?? "" : "";
        var cardMetaJson = p.TryGetProperty("cardMetadataJson", out var cm)
            ? cm : JsonSerializer.SerializeToElement(new { });

        var request = new TeamMarkdownSavedViewCreateRequest(
            TemplateId: templateIdStr!,
            Title: title!,
            SourceTableRef: sourceTableRef!,
            SourceRecordRef: sourceRecordRef!,
            BindingJson: bindingJson,
            CompletedPresetSeedJson: seedEl,
            RenderedMarkdown: renderedMarkdown,
            UserAdjustmentPatchJson: adjustmentJson,
            SearchIndexText: searchIndex,
            CardMetadataJson: cardMetaJson
        );

        var (savedViewId, errorCode, message) = await _teamMarkdownRepository.CreateSavedViewAsync(request, ct);
        if (errorCode is not null)
            return (null, new ValidationError(errorCode, message ?? errorCode));

        if (!Guid.TryParse(savedViewId, out var savedViewGuid))
        {
            _logger.LogError("CreateSavedViewAsync returned null or unparseable savedViewId after successful insert");
            return (null, new ValidationError("DB_INCONSISTENCY", "CreateSavedViewAsync returned invalid savedViewId"));
        }

        // Event append is best-effort: failure does not roll back the saved view creation.
        _ = await _teamMarkdownRepository.AppendEventAsync(
            savedViewGuid, "create", null,
            JsonSerializer.SerializeToElement(new { title, templateId = templateIdStr }), ct);

        _logger.LogInformation("AdminRuntime.TeamMarkdown.SavedViewCreate: savedViewId={SavedViewId}", savedViewId);
        return (JsonSerializer.SerializeToElement(new { ok = true, savedViewId }), null);
    }

    // ─── saved view search ───────────────────────────────────────────────────

    private async Task<(JsonElement? data, ValidationError? error)>
        DataTeamMarkdownSavedViewSearchAsync(OperationVector vector, CancellationToken ct)
    {
        if (_teamMarkdownRepository is null)
            return (null, new ValidationError("TEAM_MARKDOWN_NOT_CONFIGURED", "TeamMarkdownRepository is not registered"));

        string? query = null;
        var status = "active";
        var limit = 50;
        if (vector.Payload.HasValue)
        {
            var p = vector.Payload.Value;
            if (p.TryGetProperty("query", out var qEl)) query = qEl.GetString();
            if (p.TryGetProperty("status", out var stEl)) status = stEl.GetString() ?? "active";
            if (p.TryGetProperty("limit", out var lEl) && lEl.TryGetInt32(out var l)) limit = Math.Clamp(l, 1, 200);
        }

        var (cards, searchDbError, searchDbMsg) = await _teamMarkdownRepository.SearchSavedViewsAsync(query, status, limit, ct);
        if (searchDbError is not null)
            return (null, new ValidationError(searchDbError, searchDbMsg ?? searchDbError));
        return (JsonSerializer.SerializeToElement(new { ok = true, savedViews = cards }), null);
    }

    // ─── saved view get ──────────────────────────────────────────────────────

    private async Task<(JsonElement? data, ValidationError? error)>
        DataTeamMarkdownSavedViewGetAsync(OperationVector vector, CancellationToken ct)
    {
        if (_teamMarkdownRepository is null)
            return (null, new ValidationError("TEAM_MARKDOWN_NOT_CONFIGURED", "TeamMarkdownRepository is not registered"));

        var idStr = vector.IdOrHubId;
        if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var savedViewId))
            return (null, new ValidationError("SAVED_VIEW_ID_REQUIRED", "idOrHubId must be a valid saved view UUID"));

        var (detail, getDbError, getDbMsg) = await _teamMarkdownRepository.GetSavedViewAsync(savedViewId, ct);
        if (getDbError is not null)
            return (null, new ValidationError(getDbError, getDbMsg ?? getDbError));
        if (detail is null)
            return (null, new ValidationError("SAVED_VIEW_NOT_FOUND", $"Saved view {savedViewId} not found"));

        var seedError = CompletedPresetSeedValidator.Validate(detail.CompletedPresetSeedJson);
        var seedValid = seedError is null;

        // Event append is best-effort: click_expand event failure does not block the get response.
        _ = await _teamMarkdownRepository.AppendEventAsync(savedViewId, "click_expand", null,
            JsonSerializer.SerializeToElement(new { savedViewId = savedViewId.ToString() }), ct);

        return (JsonSerializer.SerializeToElement(new { ok = true, savedView = detail, seedValid, seedError = seedError?.Message }), null);
    }

    // ─── saved view refresh ──────────────────────────────────────────────────

    private async Task<(JsonElement? data, ValidationError? error)>
        DataTeamMarkdownSavedViewRefreshAsync(OperationVector vector, CancellationToken ct)
    {
        if (_teamMarkdownRepository is null)
            return (null, new ValidationError("TEAM_MARKDOWN_NOT_CONFIGURED", "TeamMarkdownRepository is not registered"));

        var idStr = vector.IdOrHubId;
        if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var savedViewId))
            return (null, new ValidationError("SAVED_VIEW_ID_REQUIRED", "idOrHubId must be a valid saved view UUID"));

        var (existing, refreshDbError, refreshDbMsg) = await _teamMarkdownRepository.GetSavedViewAsync(savedViewId, ct);
        if (refreshDbError is not null)
            return (null, new ValidationError(refreshDbError, refreshDbMsg ?? refreshDbError));
        if (existing is null)
            return (null, new ValidationError("SAVED_VIEW_NOT_FOUND", $"Saved view {savedViewId} not found"));

        var seedError = CompletedPresetSeedValidator.Validate(existing.CompletedPresetSeedJson);
        if (seedError is not null)
            return (null, seedError);

        if (vector.Payload is null)
            return (null, new ValidationError("REFRESH_PAYLOAD_REQUIRED",
                "payload with refreshedRenderedMarkdown, updatedCompletedPresetSeedJson, and searchIndexText is required for refresh"));

        var p = vector.Payload.Value;
        var refreshedMarkdown = p.TryGetProperty("refreshedRenderedMarkdown", out var rrm) ? rrm.GetString() ?? "" : "";
        if (!p.TryGetProperty("updatedCompletedPresetSeedJson", out var updatedSeed) || updatedSeed.ValueKind == JsonValueKind.Null)
            return (null, new ValidationError("COMPLETED_PRESET_SEED_MISSING",
                "updatedCompletedPresetSeedJson is required for refresh; refresh uses seed binding_json not Markdown body parsing"));

        var updatedSeedError = CompletedPresetSeedValidator.Validate(updatedSeed);
        if (updatedSeedError is not null)
            return (null, updatedSeedError);

        var searchIndex = p.TryGetProperty("searchIndexText", out var si) ? si.GetString() ?? "" : "";
        var cardMeta = p.TryGetProperty("cardMetadataJson", out var cm) ? (JsonElement?)cm : null;

        var (updated, errorCode, message) = await _teamMarkdownRepository.UpdateSavedViewAsync(
            savedViewId, null, refreshedMarkdown, null, updatedSeed, searchIndex, cardMeta, ct);
        if (errorCode is not null)
            return (null, new ValidationError(errorCode, message ?? errorCode));

        // Event append is best-effort.
        _ = await _teamMarkdownRepository.AppendEventAsync(savedViewId, "refresh", null,
            JsonSerializer.SerializeToElement(new { savedViewId = savedViewId.ToString() }), ct);

        _logger.LogInformation("AdminRuntime.TeamMarkdown.SavedViewRefresh: savedViewId={SavedViewId}", savedViewId);
        return (JsonSerializer.SerializeToElement(new { ok = true, savedViewId = savedViewId.ToString() }), null);
    }

    // ─── saved view update ───────────────────────────────────────────────────

    private async Task<(JsonElement? data, ValidationError? error)>
        DataTeamMarkdownSavedViewUpdateAsync(OperationVector vector, CancellationToken ct)
    {
        if (_teamMarkdownRepository is null)
            return (null, new ValidationError("TEAM_MARKDOWN_NOT_CONFIGURED", "TeamMarkdownRepository is not registered"));

        var idStr = vector.IdOrHubId;
        if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var savedViewId))
            return (null, new ValidationError("SAVED_VIEW_ID_REQUIRED", "idOrHubId must be a valid saved view UUID"));

        if (vector.Payload is null)
            return (null, new ValidationError("SAVED_VIEW_PAYLOAD_REQUIRED", "payload is required for team_markdown:saved_view:update"));

        var p = vector.Payload.Value;
        var title = p.TryGetProperty("title", out var t) ? t.GetString() : null;
        var renderedMarkdown = p.TryGetProperty("renderedMarkdown", out var rm) ? rm.GetString() : null;
        JsonElement? adjustmentPatch = p.TryGetProperty("userAdjustmentPatchJson", out var ua) ? ua : null;
        JsonElement? seedJson = null;
        if (p.TryGetProperty("completedPresetSeedJson", out var sj) && sj.ValueKind != JsonValueKind.Null)
        {
            var seedError = CompletedPresetSeedValidator.Validate(sj);
            if (seedError is not null) return (null, seedError);
            seedJson = sj;
        }
        var searchIndex = p.TryGetProperty("searchIndexText", out var si) ? si.GetString() : null;
        JsonElement? cardMeta = p.TryGetProperty("cardMetadataJson", out var cm) ? cm : null;

        var (updated, errorCode, message) = await _teamMarkdownRepository.UpdateSavedViewAsync(
            savedViewId, title, renderedMarkdown, adjustmentPatch, seedJson, searchIndex, cardMeta, ct);
        if (errorCode is not null)
            return (null, new ValidationError(errorCode, message ?? errorCode));

        // Event append is best-effort.
        _ = await _teamMarkdownRepository.AppendEventAsync(savedViewId, "update", null,
            JsonSerializer.SerializeToElement(new { savedViewId = savedViewId.ToString() }), ct);

        return (JsonSerializer.SerializeToElement(new { ok = true, savedViewId = savedViewId.ToString() }), null);
    }

    // ─── saved view archive ──────────────────────────────────────────────────

    private async Task<(JsonElement? data, ValidationError? error)>
        DataTeamMarkdownSavedViewArchiveAsync(OperationVector vector, CancellationToken ct)
    {
        if (_teamMarkdownRepository is null)
            return (null, new ValidationError("TEAM_MARKDOWN_NOT_CONFIGURED", "TeamMarkdownRepository is not registered"));

        var idStr = vector.IdOrHubId;
        if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var savedViewId))
            return (null, new ValidationError("SAVED_VIEW_ID_REQUIRED", "idOrHubId must be a valid saved view UUID"));

        var (updated, errorCode) = await _teamMarkdownRepository.ArchiveSavedViewAsync(savedViewId, ct);
        if (errorCode is not null)
            return (null, new ValidationError(errorCode, $"Archive failed for saved view {savedViewId}"));

        // Event append is best-effort.
        _ = await _teamMarkdownRepository.AppendEventAsync(savedViewId, "archive", null,
            JsonSerializer.SerializeToElement(new { savedViewId = savedViewId.ToString() }), ct);

        return (JsonSerializer.SerializeToElement(new { ok = true, savedViewId = savedViewId.ToString() }), null);
    }

    // ─── team_markdown dispatch router ───────────────────────────────────────

    internal async Task<(JsonElement? data, ValidationError? error)>
        ExecuteTeamMarkdownAsync(OperationVector vector, CancellationToken ct)
    {
        var action = vector.Action ?? "";
        return action switch
        {
            "template:create"      => await DataTeamMarkdownTemplateCreateAsync(vector, ct),
            "template:list"        => await DataTeamMarkdownTemplateListAsync(vector, ct),
            "template:get"         => await DataTeamMarkdownTemplateGetAsync(vector, ct),
            "template:update"      => await DataTeamMarkdownTemplateUpdateAsync(vector, ct),
            "template:archive"     => await DataTeamMarkdownTemplateArchiveAsync(vector, ct),
            "saved_view:create"    => await DataTeamMarkdownSavedViewCreateAsync(vector, ct),
            "saved_view:search"    => await DataTeamMarkdownSavedViewSearchAsync(vector, ct),
            "saved_view:get"       => await DataTeamMarkdownSavedViewGetAsync(vector, ct),
            "saved_view:refresh"   => await DataTeamMarkdownSavedViewRefreshAsync(vector, ct),
            "saved_view:update"    => await DataTeamMarkdownSavedViewUpdateAsync(vector, ct),
            "saved_view:archive"   => await DataTeamMarkdownSavedViewArchiveAsync(vector, ct),
            _ => (null, new ValidationError("TEAM_MARKDOWN_ACTION_UNKNOWN",
                    $"Unknown team_markdown action: {action}. Valid: template:create|list|get|update|archive, saved_view:create|search|get|refresh|update|archive"))
        };
    }
}
