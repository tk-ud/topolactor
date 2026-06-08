using System.Text.Json;
using System.Text.Json.Serialization;

namespace Topolactor.Schema;

// ---------------------------------------------------------------------------
// Team Markdown Dashboard Saved View — backend contract types
// SSOT: docs/design/team-markdown-dashboard-saved-view-ssot.yaml
// Authority: AdminRuntime.TeamMarkdown partial class
//
// Boundary invariants:
//   - saved Markdown view is a projection; physical records are canonical data authority
//   - completed_preset_seed_json is the required gate for refresh/rebind/clone/projection
//   - Markdown body is NOT runtime SSOT; refresh uses seed binding_json
//   - frontend does NOT write to topology.team_markdown_* directly
// ---------------------------------------------------------------------------

// ─── template create ────────────────────────────────────────────────────────

public record TeamMarkdownTemplateCreateRequest(
    string TemplateKey,
    string TemplateLabel,
    string TemplateMarkdown,
    JsonElement PlaceholderSchemaJson
);

public record TeamMarkdownTemplateCreateResponse(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("templateId")] string? TemplateId,
    [property: JsonPropertyName("templateKey")] string? TemplateKey,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("errorCode")] string? ErrorCode = null
);

// ─── template list / get ────────────────────────────────────────────────────

public record TeamMarkdownTemplateListItem(
    [property: JsonPropertyName("templateId")] string TemplateId,
    [property: JsonPropertyName("templateKey")] string TemplateKey,
    [property: JsonPropertyName("templateLabel")] string TemplateLabel,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("createdAt")] string CreatedAt,
    [property: JsonPropertyName("updatedAt")] string UpdatedAt
);

public record TeamMarkdownTemplateDetail(
    [property: JsonPropertyName("templateId")] string TemplateId,
    [property: JsonPropertyName("templateKey")] string TemplateKey,
    [property: JsonPropertyName("templateLabel")] string TemplateLabel,
    [property: JsonPropertyName("templateMarkdown")] string TemplateMarkdown,
    [property: JsonPropertyName("placeholderSchemaJson")] JsonElement PlaceholderSchemaJson,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("createdAt")] string CreatedAt,
    [property: JsonPropertyName("updatedAt")] string UpdatedAt
);

// ─── saved view create ───────────────────────────────────────────────────────

public record TeamMarkdownSavedViewCreateRequest(
    string TemplateId,
    string Title,
    string SourceTableRef,
    string SourceRecordRef,
    JsonElement BindingJson,
    JsonElement CompletedPresetSeedJson,
    string RenderedMarkdown,
    JsonElement UserAdjustmentPatchJson,
    string SearchIndexText,
    JsonElement CardMetadataJson
);

public record TeamMarkdownSavedViewCreateResponse(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("savedViewId")] string? SavedViewId,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("errorCode")] string? ErrorCode = null
);

// ─── saved view list item (search card) ─────────────────────────────────────

public record TeamMarkdownSavedViewCard(
    [property: JsonPropertyName("savedViewId")] string SavedViewId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("templateKey")] string TemplateKey,
    [property: JsonPropertyName("sourceTableRef")] string SourceTableRef,
    [property: JsonPropertyName("sourceRecordRef")] string SourceRecordRef,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("updatedAt")] string UpdatedAt,
    [property: JsonPropertyName("cardMetadataJson")] JsonElement CardMetadataJson
);

// ─── saved view detail (expanded view) ──────────────────────────────────────

public record TeamMarkdownSavedViewDetail(
    [property: JsonPropertyName("savedViewId")] string SavedViewId,
    [property: JsonPropertyName("templateId")] string TemplateId,
    [property: JsonPropertyName("templateKey")] string TemplateKey,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("sourceTableRef")] string SourceTableRef,
    [property: JsonPropertyName("sourceRecordRef")] string SourceRecordRef,
    [property: JsonPropertyName("bindingJson")] JsonElement BindingJson,
    [property: JsonPropertyName("completedPresetSeedJson")] JsonElement CompletedPresetSeedJson,
    [property: JsonPropertyName("renderedMarkdown")] string RenderedMarkdown,
    [property: JsonPropertyName("userAdjustmentPatchJson")] JsonElement UserAdjustmentPatchJson,
    [property: JsonPropertyName("searchIndexText")] string SearchIndexText,
    [property: JsonPropertyName("cardMetadataJson")] JsonElement CardMetadataJson,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("createdAt")] string CreatedAt,
    [property: JsonPropertyName("updatedAt")] string UpdatedAt
);

// ─── completed preset seed validation ───────────────────────────────────────

public static class CompletedPresetSeedValidator
{
    private static readonly string[] RequiredTopLevelKeys =
        ["seed_version", "template_ref", "source_ref", "binding_ref", "render_ref", "adjustment_ref", "dashboard_ref", "lineage_ref"];

    /// <summary>
    /// Validates that the completed_preset_seed_json contains all required structural fields.
    /// Returns null on success; returns a ValidationError on failure.
    /// Blocking gate for refresh / rebind / clone / projection per SSOT.
    /// </summary>
    public static ValidationError? Validate(JsonElement seed)
    {
        if (seed.ValueKind != JsonValueKind.Object)
            return new ValidationError("COMPLETED_PRESET_SEED_INVALID", "completed_preset_seed_json must be a JSON object");

        foreach (var key in RequiredTopLevelKeys)
        {
            if (!seed.TryGetProperty(key, out _))
                return new ValidationError("COMPLETED_PRESET_SEED_MISSING",
                    $"completed_preset_seed_json is missing required field: {key}");
        }

        if (!seed.TryGetProperty("render_ref", out var renderRef) ||
            renderRef.ValueKind != JsonValueKind.Object ||
            !renderRef.TryGetProperty("rendered_markdown_hash", out var hashEl) ||
            string.IsNullOrWhiteSpace(hashEl.GetString()))
        {
            return new ValidationError("COMPLETED_PRESET_SEED_RENDER_HASH_MISMATCH",
                "completed_preset_seed_json.render_ref.rendered_markdown_hash is required and must not be empty");
        }

        return null;
    }
}
