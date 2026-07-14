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

        if (vector.IdOrHubId is null)
            return (null, new ValidationError("TEMPLATE_ID_REQUIRED", "idOrHubId must be a valid template UUID"));
        var templateId = vector.IdOrHubId.Value;

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

        if (vector.IdOrHubId is null)
            return (null, new ValidationError("TEMPLATE_ID_REQUIRED", "idOrHubId must be a valid template UUID"));
        var templateId = vector.IdOrHubId.Value;

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

        if (vector.IdOrHubId is null)
            return (null, new ValidationError("TEMPLATE_ID_REQUIRED", "idOrHubId must be a valid template UUID"));
        var templateId = vector.IdOrHubId.Value;

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

        var hashValidationError = CompletedPresetSeedValidator.ValidateRenderedMarkdownHash(seedEl, renderedMarkdown);
        if (hashValidationError is not null) return (null, hashValidationError);
        var bindingValidationError = CompletedPresetSeedValidator.ValidateBindingJson(seedEl, bindingJson);
        if (bindingValidationError is not null) return (null, bindingValidationError);
        var adjustmentValidationError = CompletedPresetSeedValidator.ValidateAdjustmentPatch(seedEl, adjustmentJson);
        if (adjustmentValidationError is not null) return (null, adjustmentValidationError);

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

        if (vector.IdOrHubId is null)
            return (null, new ValidationError("SAVED_VIEW_ID_REQUIRED", "idOrHubId must be a valid saved view UUID"));
        var savedViewId = vector.IdOrHubId.Value;

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

        if (vector.IdOrHubId is null)
            return (null, new ValidationError("SAVED_VIEW_ID_REQUIRED", "idOrHubId must be a valid saved view UUID"));
        var savedViewId = vector.IdOrHubId.Value;

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
                "payload with templateMarkdown and sourceRecordJson is required for seed-driven refresh"));

        var p = vector.Payload.Value;
        JsonElement updatedSeed;
        string refreshedMarkdown;
        JsonElement? cardMeta = p.TryGetProperty("cardMetadataJson", out var cm) ? (JsonElement?)cm : null;
        var searchIndex = p.TryGetProperty("searchIndexText", out var si) ? si.GetString() ?? existing.SearchIndexText : existing.SearchIndexText;

        if (p.TryGetProperty("templateMarkdown", out var tm) && p.TryGetProperty("sourceRecordJson", out var srj))
        {
            var render = MarkdownBindingRenderer.Render(tm.GetString() ?? "", existing.CompletedPresetSeedJson, srj);
            if (!render.Ok)
                return (null, new ValidationError(render.ErrorCode ?? "MARKDOWN_BINDING_RENDER_FAILED", render.Message ?? "Markdown binding render failed"));
            refreshedMarkdown = render.RenderedMarkdown;
            updatedSeed = MarkdownBindingRenderer.BuildUpdatedSeed(existing.CompletedPresetSeedJson,
                render.RenderedMarkdownHash, render.UnresolvedRequiredPlaceholderKeys, render.ExplicitOptionalEmptyPlaceholderKeys);
        }
        else if (p.TryGetProperty("updatedCompletedPresetSeedJson", out var explicitSeed) &&
                 p.TryGetProperty("refreshedRenderedMarkdown", out var rrm))
        {
            // Compatibility path for callers that pre-render via the same explicit seed contract.
            refreshedMarkdown = rrm.GetString() ?? "";
            updatedSeed = explicitSeed;
        }
        else
        {
            return (null, new ValidationError("REFRESH_PAYLOAD_REQUIRED",
                "refresh requires templateMarkdown+sourceRecordJson or explicit updatedCompletedPresetSeedJson+refreshedRenderedMarkdown"));
        }

        var updatedSeedError = CompletedPresetSeedValidator.ValidateRenderedMarkdownHash(updatedSeed, refreshedMarkdown);
        if (updatedSeedError is not null)
            return (null, updatedSeedError);
        var adjustmentValidationError = CompletedPresetSeedValidator.ValidateAdjustmentPatch(updatedSeed, existing.UserAdjustmentPatchJson);
        if (adjustmentValidationError is not null)
            return (null, adjustmentValidationError);
        var bindingValidationError = CompletedPresetSeedValidator.ValidateBindingJson(updatedSeed, existing.BindingJson);
        if (bindingValidationError is not null)
            return (null, bindingValidationError);

        // Refresh writes are gated by the same explicit payload.confirmed=true requirement
        // saved_view:update enforces — full validation always runs first (above), but a direct
        // dispatch that bypasses the frontend's confirm dialog must still be rejected here.
        var confirmed = p.TryGetProperty("confirmed", out var cfEl) && cfEl.ValueKind == JsonValueKind.True;
        if (!confirmed)
            return (null, new ValidationError("WRITE_CONFIRMATION_REQUIRED",
                "Refresh requires payload.confirmed=true after an explicit user confirmation step."));

        var actor = vector.AuthenticatedUserId ?? vector.ContextUserId ?? vector.TriggerKind ?? "unknown";
        var (updated, errorCode, message, eventId) = await _teamMarkdownRepository.UpdateSavedViewWithEventEvidenceAsync(
            savedViewId, "refresh_confirmed_write", null, refreshedMarkdown, existing.UserAdjustmentPatchJson, updatedSeed, searchIndex, cardMeta, null, actor, ct);
        if (errorCode is not null)
            return (null, new ValidationError(errorCode, message ?? errorCode));

        _logger.LogInformation("AdminRuntime.TeamMarkdown.SavedViewRefresh: savedViewId={SavedViewId}", savedViewId);
        return (JsonSerializer.SerializeToElement(new { ok = true, savedViewId = savedViewId.ToString(), eventId }), null);
    }

    // ─── saved view clone ────────────────────────────────────────────────────

    private async Task<(JsonElement? data, ValidationError? error)>
        DataTeamMarkdownSavedViewCloneAsync(OperationVector vector, CancellationToken ct)
    {
        if (_teamMarkdownRepository is null)
            return (null, new ValidationError("TEAM_MARKDOWN_NOT_CONFIGURED", "TeamMarkdownRepository is not registered"));
        if (vector.IdOrHubId is null)
            return (null, new ValidationError("SAVED_VIEW_ID_REQUIRED", "idOrHubId must be a valid saved view UUID"));
        if (vector.Payload is null)
            return (null, new ValidationError("CLONE_PAYLOAD_REQUIRED", "payload with targetSourceTableRef, targetSourceRecordRef, templateMarkdown, and sourceRecordJson is required"));

        var sourceSavedViewId = vector.IdOrHubId.Value;
        var (existing, getError, getMsg) = await _teamMarkdownRepository.GetSavedViewAsync(sourceSavedViewId, ct);
        if (getError is not null) return (null, new ValidationError(getError, getMsg ?? getError));
        if (existing is null) return (null, new ValidationError("SAVED_VIEW_NOT_FOUND", $"Saved view {sourceSavedViewId} not found"));
        var seedError = CompletedPresetSeedValidator.Validate(existing.CompletedPresetSeedJson);
        if (seedError is not null) return (null, seedError);

        var p = vector.Payload.Value;
        var targetTable = p.TryGetProperty("targetSourceTableRef", out var ts) ? ts.GetString() : null;
        var targetRecord = p.TryGetProperty("targetSourceRecordRef", out var tr) ? tr.GetString() : null;
        if (string.IsNullOrWhiteSpace(targetTable)) return (null, new ValidationError("PHYSICAL_TABLE_REF_NOT_REGISTERED", "targetSourceTableRef is required"));
        if (string.IsNullOrWhiteSpace(targetRecord)) return (null, new ValidationError("SOURCE_RECORD_NOT_FOUND", "targetSourceRecordRef is required"));
        if (!p.TryGetProperty("templateMarkdown", out var templateMarkdown) || !p.TryGetProperty("sourceRecordJson", out var sourceRecordJson))
            return (null, new ValidationError("CLONE_PAYLOAD_REQUIRED", "clone requires templateMarkdown and sourceRecordJson for explicit binding render"));

        var seedForTarget = MarkdownBindingRenderer.BuildUpdatedSeed(existing.CompletedPresetSeedJson,
            existing.CompletedPresetSeedJson.GetProperty("render_ref").GetProperty("rendered_markdown_hash").GetString() ?? "",
            Array.Empty<string>(), Array.Empty<string>(), targetTable, targetRecord, sourceSavedViewId.ToString(), "clone_from_saved_view");
        var render = MarkdownBindingRenderer.Render(templateMarkdown.GetString() ?? "", seedForTarget, sourceRecordJson);
        if (!render.Ok) return (null, new ValidationError(render.ErrorCode ?? "MARKDOWN_BINDING_RENDER_FAILED", render.Message ?? "Markdown binding render failed"));
        var completedSeed = MarkdownBindingRenderer.BuildUpdatedSeed(seedForTarget, render.RenderedMarkdownHash,
            render.UnresolvedRequiredPlaceholderKeys, render.ExplicitOptionalEmptyPlaceholderKeys, targetTable, targetRecord, sourceSavedViewId.ToString(), "clone_from_saved_view");
        var completedSeedError = CompletedPresetSeedValidator.ValidateRenderedMarkdownHash(completedSeed, render.RenderedMarkdown);
        if (completedSeedError is not null) return (null, completedSeedError);
        var cloneBindingError = CompletedPresetSeedValidator.ValidateBindingJson(completedSeed, existing.BindingJson);
        if (cloneBindingError is not null) return (null, cloneBindingError);
        var cloneAdjustmentError = CompletedPresetSeedValidator.ValidateAdjustmentPatch(completedSeed, existing.UserAdjustmentPatchJson);
        if (cloneAdjustmentError is not null) return (null, cloneAdjustmentError);

        var title = p.TryGetProperty("title", out var titleEl) ? titleEl.GetString() ?? existing.Title : existing.Title;
        var searchIndex = p.TryGetProperty("searchIndexText", out var si) ? si.GetString() ?? "" : string.Join(" ", title, targetTable, targetRecord, render.RenderedMarkdown);
        var cardMeta = p.TryGetProperty("cardMetadataJson", out var cm) ? cm : existing.CardMetadataJson;

        var confirmed = p.TryGetProperty("confirmed", out var cfEl) && cfEl.ValueKind == JsonValueKind.True;
        if (!confirmed)
            return (null, new ValidationError("WRITE_CONFIRMATION_REQUIRED",
                "Clone requires payload.confirmed=true after an explicit user confirmation step."));

        var actor = vector.AuthenticatedUserId ?? vector.ContextUserId ?? vector.TriggerKind ?? "unknown";
        var request = new TeamMarkdownSavedViewCreateRequest(existing.TemplateId, title, targetTable!, targetRecord!,
            existing.BindingJson, completedSeed, render.RenderedMarkdown, existing.UserAdjustmentPatchJson, searchIndex, cardMeta);
        var (newId, createError, createMsg, eventId) = await _teamMarkdownRepository.CloneSavedViewWithEvidenceAsync(request, sourceSavedViewId, actor, ct);
        if (createError is not null) return (null, new ValidationError(createError, createMsg ?? createError));
        return (JsonSerializer.SerializeToElement(new { ok = true, savedViewId = newId, sourceSavedViewId = sourceSavedViewId.ToString(), eventId }), null);
    }

    // ─── saved view rebind ───────────────────────────────────────────────────

    private async Task<(JsonElement? data, ValidationError? error)>
        DataTeamMarkdownSavedViewRebindAsync(OperationVector vector, CancellationToken ct)
    {
        if (_teamMarkdownRepository is null)
            return (null, new ValidationError("TEAM_MARKDOWN_NOT_CONFIGURED", "TeamMarkdownRepository is not registered"));
        if (vector.IdOrHubId is null)
            return (null, new ValidationError("SAVED_VIEW_ID_REQUIRED", "idOrHubId must be a valid saved view UUID"));
        if (vector.Payload is null)
            return (null, new ValidationError("REBIND_PAYLOAD_REQUIRED", "payload with bindingJson, templateMarkdown, and sourceRecordJson is required"));

        var savedViewId = vector.IdOrHubId.Value;
        var (existing, getError, getMsg) = await _teamMarkdownRepository.GetSavedViewAsync(savedViewId, ct);
        if (getError is not null) return (null, new ValidationError(getError, getMsg ?? getError));
        if (existing is null) return (null, new ValidationError("SAVED_VIEW_NOT_FOUND", $"Saved view {savedViewId} not found"));
        var seedError = CompletedPresetSeedValidator.Validate(existing.CompletedPresetSeedJson);
        if (seedError is not null) return (null, seedError);

        var p = vector.Payload.Value;
        if (!p.TryGetProperty("bindingJson", out var bindingJson) || !p.TryGetProperty("completedPresetSeedJson", out var proposedSeed))
            return (null, new ValidationError("REBIND_PAYLOAD_REQUIRED", "rebind requires user-selected bindingJson and completedPresetSeedJson"));
        var proposedSeedError = CompletedPresetSeedValidator.Validate(proposedSeed);
        if (proposedSeedError is not null) return (null, proposedSeedError);
        var proposedBindingError = CompletedPresetSeedValidator.ValidateBindingJson(proposedSeed, bindingJson);
        if (proposedBindingError is not null) return (null, proposedBindingError);
        if (!p.TryGetProperty("templateMarkdown", out var templateMarkdown) || !p.TryGetProperty("sourceRecordJson", out var sourceRecordJson))
            return (null, new ValidationError("REBIND_PAYLOAD_REQUIRED", "rebind requires templateMarkdown and sourceRecordJson for explicit binding render"));

        var render = MarkdownBindingRenderer.Render(templateMarkdown.GetString() ?? "", proposedSeed, sourceRecordJson);
        if (!render.Ok) return (null, new ValidationError(render.ErrorCode ?? "MARKDOWN_BINDING_RENDER_FAILED", render.Message ?? "Markdown binding render failed"));
        var completedSeed = MarkdownBindingRenderer.BuildUpdatedSeed(proposedSeed, render.RenderedMarkdownHash,
            render.UnresolvedRequiredPlaceholderKeys, render.ExplicitOptionalEmptyPlaceholderKeys, createdFrom: "rebind_saved_view");
        var completedSeedError = CompletedPresetSeedValidator.ValidateRenderedMarkdownHash(completedSeed, render.RenderedMarkdown);
        if (completedSeedError is not null) return (null, completedSeedError);
        var completedBindingError = CompletedPresetSeedValidator.ValidateBindingJson(completedSeed, bindingJson);
        if (completedBindingError is not null) return (null, completedBindingError);
        var rebindAdjustmentError = CompletedPresetSeedValidator.ValidateAdjustmentPatch(completedSeed, existing.UserAdjustmentPatchJson);
        if (rebindAdjustmentError is not null) return (null, rebindAdjustmentError);

        var searchIndex = p.TryGetProperty("searchIndexText", out var si) ? si.GetString() ?? existing.SearchIndexText : existing.SearchIndexText;
        var cardMeta = p.TryGetProperty("cardMetadataJson", out var cm) ? (JsonElement?)cm : null;

        var confirmed = p.TryGetProperty("confirmed", out var cfEl) && cfEl.ValueKind == JsonValueKind.True;
        if (!confirmed)
            return (null, new ValidationError("WRITE_CONFIRMATION_REQUIRED",
                "Rebind requires payload.confirmed=true after an explicit user confirmation step."));

        var actor = vector.AuthenticatedUserId ?? vector.ContextUserId ?? vector.TriggerKind ?? "unknown";
        var (updated, updateError, updateMsg, eventId) = await _teamMarkdownRepository.UpdateSavedViewWithEventEvidenceAsync(savedViewId,
            "rebind_confirmed_write", null, render.RenderedMarkdown, existing.UserAdjustmentPatchJson, completedSeed, searchIndex, cardMeta, bindingJson, actor, ct);
        if (updateError is not null) return (null, new ValidationError(updateError, updateMsg ?? updateError));
        return (JsonSerializer.SerializeToElement(new { ok = true, savedViewId = savedViewId.ToString(), updated, eventId }), null);
    }

    // ─── saved view update ───────────────────────────────────────────────────
    // Authoring workflow: preview/validate (payload.dryRun=true, non-mutating) -> explicit_confirm
    // (frontend UI) -> write (payload.dryRun absent/false AND payload.confirmed=true). The write step
    // re-runs the identical validation against the identical input every time — dryRun is not trusted
    // as prior proof — and the actual UPDATE + its diff-evidence event are one atomic transaction via
    // UpdateSavedViewWithDiffEvidenceAsync, never a best-effort side call.

    private async Task<(JsonElement? data, ValidationError? error)>
        DataTeamMarkdownSavedViewUpdateAsync(OperationVector vector, CancellationToken ct)
    {
        if (_teamMarkdownRepository is null)
            return (null, new ValidationError("TEAM_MARKDOWN_NOT_CONFIGURED", "TeamMarkdownRepository is not registered"));

        if (vector.IdOrHubId is null)
            return (null, new ValidationError("SAVED_VIEW_ID_REQUIRED", "idOrHubId must be a valid saved view UUID"));
        var savedViewId = vector.IdOrHubId.Value;

        if (vector.Payload is null)
            return (null, new ValidationError("SAVED_VIEW_PAYLOAD_REQUIRED", "payload is required for team_markdown:saved_view:update"));

        var p = vector.Payload.Value;
        var dryRun = p.TryGetProperty("dryRun", out var drEl) && drEl.ValueKind == JsonValueKind.True;
        var confirmed = p.TryGetProperty("confirmed", out var cfEl) && cfEl.ValueKind == JsonValueKind.True;
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

        var touchesSeedGatedProjection = renderedMarkdown is not null || adjustmentPatch.HasValue;
        var (detail, getError, getMsg) = await _teamMarkdownRepository.GetSavedViewAsync(savedViewId, ct);
        if (getError is not null) return (null, new ValidationError(getError, getMsg ?? getError));
        if (detail is null) return (null, new ValidationError("SAVED_VIEW_NOT_FOUND", $"Saved view {savedViewId} not found"));
        var existing = detail;

        if (touchesSeedGatedProjection && !seedJson.HasValue)
            return (null, new ValidationError("COMPLETED_PRESET_SEED_MISSING",
                "completedPresetSeedJson is required when renderedMarkdown or userAdjustmentPatchJson changes"));

        if (seedJson.HasValue)
        {
            var markdownForHash = renderedMarkdown ?? existing.RenderedMarkdown;
            var hashError = CompletedPresetSeedValidator.ValidateRenderedMarkdownHash(seedJson.Value, markdownForHash);
            if (hashError is not null) return (null, hashError);
            var patchForValidation = adjustmentPatch ?? existing.UserAdjustmentPatchJson;
            var adjustmentError = CompletedPresetSeedValidator.ValidateAdjustmentPatch(seedJson.Value, patchForValidation);
            if (adjustmentError is not null) return (null, adjustmentError);
            var bindingError = CompletedPresetSeedValidator.ValidateBindingJson(seedJson.Value, existing.BindingJson);
            if (bindingError is not null) return (null, bindingError);
        }

        var searchIndex = p.TryGetProperty("searchIndexText", out var si) ? si.GetString() : null;
        JsonElement? cardMeta = p.TryGetProperty("cardMetadataJson", out var cm) ? cm : null;

        // preview / validate: every validation gate above already ran; return without mutating.
        if (dryRun)
        {
            return (JsonSerializer.SerializeToElement(new
            {
                ok = true,
                dryRun = true,
                valid = true,
                savedViewId = savedViewId.ToString(),
                preview = new
                {
                    title = title ?? existing.Title,
                    renderedMarkdown = renderedMarkdown ?? existing.RenderedMarkdown,
                },
            }), null);
        }

        if (!confirmed)
            return (null, new ValidationError("SAVED_VIEW_WRITE_NOT_CONFIRMED",
                "Write requires payload.confirmed=true after an explicit user confirmation step."));

        var actor = vector.AuthenticatedUserId ?? vector.ContextUserId ?? vector.TriggerKind ?? "unknown";
        var (updated, errorCode, message, eventId) = await _teamMarkdownRepository.UpdateSavedViewWithDiffEvidenceAsync(
            savedViewId, title, renderedMarkdown, adjustmentPatch, seedJson, searchIndex, cardMeta, null, actor, ct);
        if (errorCode is not null)
            return (null, new ValidationError(errorCode, message ?? errorCode));

        return (JsonSerializer.SerializeToElement(new
        {
            ok = true,
            savedViewId = savedViewId.ToString(),
            diffEventId = eventId,
        }), null);
    }

    // ─── saved view archive ──────────────────────────────────────────────────

    private async Task<(JsonElement? data, ValidationError? error)>
        DataTeamMarkdownSavedViewArchiveAsync(OperationVector vector, CancellationToken ct)
    {
        if (_teamMarkdownRepository is null)
            return (null, new ValidationError("TEAM_MARKDOWN_NOT_CONFIGURED", "TeamMarkdownRepository is not registered"));

        if (vector.IdOrHubId is null)
            return (null, new ValidationError("SAVED_VIEW_ID_REQUIRED", "idOrHubId must be a valid saved view UUID"));
        var savedViewId = vector.IdOrHubId.Value;

        var (updated, errorCode) = await _teamMarkdownRepository.ArchiveSavedViewAsync(savedViewId, ct);
        if (errorCode is not null)
            return (null, new ValidationError(errorCode, $"Archive failed for saved view {savedViewId}"));

        // Event append is best-effort.
        _ = await _teamMarkdownRepository.AppendEventAsync(savedViewId, "archive", null,
            JsonSerializer.SerializeToElement(new { savedViewId = savedViewId.ToString() }), ct);

        return (JsonSerializer.SerializeToElement(new { ok = true, savedViewId = savedViewId.ToString() }), null);
    }

    // ─── team_markdown dispatch router ───────────────────────────────────────
    // Read actions (list/get/search) are open to any authenticated caller reaching admin_runtime.
    // Mutation actions additionally require AuthenticatedRole=="admin" — an explicit, non-spoofable,
    // in-method check (AuthenticatedRole is sourced only from the JWT-verified DispatchAuthContext
    // stamp, never from client-supplied context) that does not depend on manifest/seed content, as
    // defense-in-depth alongside the existing manifest capability_requirement inference. This is the
    // backend authority re-check every Team Markdown mutation now goes through, independent of
    // whichever role the frontend chose to display authoring UI for.

    private static readonly HashSet<string> TeamMarkdownMutationActions = new(StringComparer.Ordinal)
    {
        "template:create", "template:update", "template:archive",
        "saved_view:create", "saved_view:refresh", "saved_view:clone",
        "saved_view:rebind", "saved_view:update", "saved_view:archive",
    };

    internal async Task<(JsonElement? data, ValidationError? error)>
        ExecuteTeamMarkdownAsync(OperationVector vector, CancellationToken ct)
    {
        var action = vector.Action ?? "";

        if (TeamMarkdownMutationActions.Contains(action) &&
            !string.Equals(vector.AuthenticatedRole, "admin", StringComparison.Ordinal))
        {
            return (null, new ValidationError("AUTH_CAPABILITY_DENIED",
                $"team_markdown:{action} requires admin role."));
        }

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
            "saved_view:clone"     => await DataTeamMarkdownSavedViewCloneAsync(vector, ct),
            "saved_view:rebind"    => await DataTeamMarkdownSavedViewRebindAsync(vector, ct),
            "saved_view:update"    => await DataTeamMarkdownSavedViewUpdateAsync(vector, ct),
            "saved_view:archive"   => await DataTeamMarkdownSavedViewArchiveAsync(vector, ct),
            _ => (null, new ValidationError("TEAM_MARKDOWN_ACTION_UNKNOWN",
                    $"Unknown team_markdown action: {action}. Valid: template:create|list|get|update|archive, saved_view:create|search|get|refresh|clone|rebind|update|archive"))
        };
    }
}
