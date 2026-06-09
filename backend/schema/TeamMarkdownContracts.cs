using System.Security.Cryptography;
using System.Text;
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
            if (!seed.TryGetProperty(key, out var val) || val.ValueKind == JsonValueKind.Null || val.ValueKind == JsonValueKind.Undefined)
                return new ValidationError("COMPLETED_PRESET_SEED_MISSING",
                    $"completed_preset_seed_json is missing or null required field: {key}");
        }

        if (!TryGetObject(seed, "template_ref", out var templateRef)) return InvalidObject("template_ref");
        if (RequireNonEmptyString(templateRef, "template_id") is { } templateIdError) return templateIdError;
        if (RequireNonEmptyString(templateRef, "template_key") is { } templateKeyError) return templateKeyError;

        if (!TryGetObject(seed, "source_ref", out var sourceRef)) return InvalidObject("source_ref");
        if (RequireNonEmptyString(sourceRef, "source_table_ref") is { } sourceTableError) return sourceTableError;
        if (RequireNonEmptyString(sourceRef, "source_record_ref") is { } sourceRecordError) return sourceRecordError;

        if (!TryGetObject(seed, "binding_ref", out var bindingRef)) return InvalidObject("binding_ref");
        if (RequireObject(bindingRef, "binding_json") is { } bindingJsonError) return bindingJsonError;
        if (RequireObject(bindingRef, "placeholder_to_field_map") is { } mapError) return mapError;
        if (RequireArray(bindingRef, "required_placeholder_keys") is { } requiredError) return requiredError;
        if (RequireArray(bindingRef, "optional_placeholder_keys") is { } optionalError) return optionalError;
        if (ContainsAny(bindingRef, "unresolved_required_placeholder_keys"))
            return new ValidationError("REQUIRED_PLACEHOLDER_UNBOUND",
                "completed_preset_seed_json.binding_ref.unresolved_required_placeholder_keys must be empty before saved view persistence");

        if (!TryGetObject(seed, "render_ref", out var renderRef)) return InvalidObject("render_ref");
        if (RequireNonEmptyString(renderRef, "rendered_markdown_hash", "COMPLETED_PRESET_SEED_RENDER_HASH_MISMATCH") is { } hashError) return hashError;
        if (RequireNonEmptyString(renderRef, "rendered_at") is { } renderedAtError) return renderedAtError;
        if (RequireNonEmptyString(renderRef, "renderer_version") is { } rendererVersionError) return rendererVersionError;
        if (RequireArray(renderRef, "unresolved_placeholder_keys") is { } unresolvedArrayError) return unresolvedArrayError;
        if (ContainsAny(renderRef, "unresolved_placeholder_keys"))
            return new ValidationError("REQUIRED_PLACEHOLDER_UNBOUND",
                "completed_preset_seed_json.render_ref.unresolved_placeholder_keys must be empty before saved view persistence");

        if (!TryGetObject(seed, "adjustment_ref", out var adjustmentRef)) return InvalidObject("adjustment_ref");
        if (RequireObject(adjustmentRef, "user_adjustment_patch_json") is { } patchError) return patchError;
        if (RequireNonEmptyString(adjustmentRef, "adjustment_mode") is { } modeError) return modeError;

        if (!TryGetObject(seed, "dashboard_ref", out var dashboardRef)) return InvalidObject("dashboard_ref");
        if (RequireNonEmptyString(dashboardRef, "title") is { } titleError) return titleError;
        if (RequireString(dashboardRef, "excerpt") is { } excerptError) return excerptError;
        if (RequireArray(dashboardRef, "tags") is { } tagsError) return tagsError;
        if (RequireObject(dashboardRef, "card_metadata_json") is { } cardError) return cardError;
        if (RequireObject(dashboardRef, "search_index_basis_json") is { } searchBasisError) return searchBasisError;

        if (!TryGetObject(seed, "lineage_ref", out var lineageRef)) return InvalidObject("lineage_ref");
        if (RequireNonEmptyString(lineageRef, "created_from") is { } createdFromError) return createdFromError;
        if (!lineageRef.TryGetProperty("parent_saved_view_id", out _))
            return MissingNested("lineage_ref.parent_saved_view_id");

        return null;
    }

    public static ValidationError? ValidateRenderedMarkdownHash(JsonElement seed, string renderedMarkdown)
    {
        var structuralError = Validate(seed);
        if (structuralError is not null) return structuralError;
        var expectedHash = seed.GetProperty("render_ref").GetProperty("rendered_markdown_hash").GetString();
        var actualHash = MarkdownBindingRenderer.ComputeRenderedMarkdownHash(renderedMarkdown ?? string.Empty);
        if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
        {
            return new ValidationError("COMPLETED_PRESET_SEED_RENDER_HASH_MISMATCH",
                "completed_preset_seed_json.render_ref.rendered_markdown_hash must match the renderedMarkdown payload at save time");
        }
        return null;
    }

    public static ValidationError? ValidateAdjustmentPatch(JsonElement seed, JsonElement userAdjustmentPatchJson)
    {
        var structuralError = Validate(seed);
        if (structuralError is not null) return structuralError;
        var seedPatch = seed.GetProperty("adjustment_ref").GetProperty("user_adjustment_patch_json");
        if (!MarkdownBindingRenderer.JsonSemanticallyEquals(seedPatch, userAdjustmentPatchJson))
        {
            return new ValidationError("COMPLETED_PRESET_SEED_INVALID",
                "completed_preset_seed_json.adjustment_ref.user_adjustment_patch_json must match userAdjustmentPatchJson at save time");
        }
        return null;
    }

    public static ValidationError? ValidateBindingJson(JsonElement seed, JsonElement bindingJson)
    {
        var structuralError = Validate(seed);
        if (structuralError is not null) return structuralError;
        var seedBindingJson = seed.GetProperty("binding_ref").GetProperty("binding_json");
        if (!MarkdownBindingRenderer.JsonSemanticallyEquals(seedBindingJson, bindingJson))
        {
            return new ValidationError("COMPLETED_PRESET_SEED_INVALID",
                "completed_preset_seed_json.binding_ref.binding_json must match bindingJson at save time");
        }
        return null;
    }

    private static bool TryGetObject(JsonElement parent, string key, out JsonElement obj)
    {
        if (parent.TryGetProperty(key, out obj) && obj.ValueKind == JsonValueKind.Object) return true;
        obj = default;
        return false;
    }

    private static ValidationError InvalidObject(string path) =>
        new("COMPLETED_PRESET_SEED_INVALID", $"completed_preset_seed_json.{path} must be a JSON object");

    private static ValidationError MissingNested(string path) =>
        new("COMPLETED_PRESET_SEED_MISSING", $"completed_preset_seed_json.{path} is required");

    private static ValidationError? RequireNonEmptyString(JsonElement parent, string key, string code = "COMPLETED_PRESET_SEED_INVALID")
    {
        if (!parent.TryGetProperty(key, out var val)) return MissingNested(key);
        if (val.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(val.GetString()))
            return new ValidationError(code, $"completed_preset_seed_json.{key} must be a non-empty string");
        return null;
    }

    private static ValidationError? RequireString(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var val)) return MissingNested(key);
        if (val.ValueKind != JsonValueKind.String)
            return new ValidationError("COMPLETED_PRESET_SEED_INVALID", $"completed_preset_seed_json.{key} must be a string");
        return null;
    }

    private static ValidationError? RequireObject(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var val)) return MissingNested(key);
        if (val.ValueKind != JsonValueKind.Object)
            return new ValidationError("COMPLETED_PRESET_SEED_INVALID", $"completed_preset_seed_json.{key} must be a JSON object");
        return null;
    }

    private static ValidationError? RequireArray(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var val)) return MissingNested(key);
        if (val.ValueKind != JsonValueKind.Array)
            return new ValidationError("COMPLETED_PRESET_SEED_INVALID", $"completed_preset_seed_json.{key} must be a JSON array");
        return null;
    }

    private static bool ContainsAny(JsonElement parent, string key) =>
        parent.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.Array && val.GetArrayLength() > 0;
}

public record MarkdownBindingRenderResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("renderedMarkdown")] string RenderedMarkdown,
    [property: JsonPropertyName("renderedMarkdownHash")] string RenderedMarkdownHash,
    [property: JsonPropertyName("rendererVersion")] string RendererVersion,
    [property: JsonPropertyName("unresolvedRequiredPlaceholderKeys")] IReadOnlyList<string> UnresolvedRequiredPlaceholderKeys,
    [property: JsonPropertyName("explicitOptionalEmptyPlaceholderKeys")] IReadOnlyList<string> ExplicitOptionalEmptyPlaceholderKeys,
    [property: JsonPropertyName("errorCode")] string? ErrorCode,
    [property: JsonPropertyName("message")] string? Message
);

public static class MarkdownBindingRenderer
{
    public const string RendererVersion = "team_markdown.explicit_binding_renderer.v1";
    private static readonly System.Text.RegularExpressions.Regex PlaceholderPattern =
        new("{{\\s*([A-Za-z0-9_.-]+)\\s*}}", System.Text.RegularExpressions.RegexOptions.Compiled);

    public static MarkdownBindingRenderResult Render(string templateMarkdown, JsonElement seed, JsonElement? sourceRecordJson)
    {
        var seedError = CompletedPresetSeedValidator.Validate(seed);
        if (seedError is not null)
            return Error(seedError.Code, seedError.Message);
        if (sourceRecordJson is null || sourceRecordJson.Value.ValueKind != JsonValueKind.Object)
            return Error("SOURCE_RECORD_NOT_FOUND", "sourceRecordJson object is required for explicit binding render");

        var bindingRef = seed.GetProperty("binding_ref");
        var bindingJson = bindingRef.GetProperty("binding_json");
        var required = ReadStringSet(bindingRef.GetProperty("required_placeholder_keys"));
        var optional = ReadStringSet(bindingRef.GetProperty("optional_placeholder_keys"));
        var explicitOptionalEmpty = new List<string>();
        var unresolvedRequired = new List<string>();
        string rendered;
        try
        {
            rendered = PlaceholderPattern.Replace(templateMarkdown, match =>
            {
                var key = match.Groups[1].Value;
                var binding = FindBinding(bindingJson, key);
                if (binding is null || IsExplicitOptionalEmpty(binding.Value))
                {
                    if (required.Contains(key)) unresolvedRequired.Add(key);
                    else if (optional.Contains(key)) explicitOptionalEmpty.Add(key);
                    return "";
                }
                var resolved = ResolveBindingValue(binding.Value, sourceRecordJson.Value, out var errorCode, out var message);
                if (errorCode is not null)
                    throw new MarkdownBindingRenderException(errorCode, message ?? errorCode);
                if (resolved is null)
                {
                    if (required.Contains(key)) unresolvedRequired.Add(key);
                    else if (optional.Contains(key)) explicitOptionalEmpty.Add(key);
                    return "";
                }
                return resolved;
            });
        }
        catch (MarkdownBindingRenderException ex)
        {
            return Error(ex.ErrorCode, ex.Message);
        }

        if (unresolvedRequired.Count > 0)
            return new MarkdownBindingRenderResult(false, rendered, ComputeRenderedMarkdownHash(rendered), RendererVersion,
                unresolvedRequired.Distinct().ToArray(), explicitOptionalEmpty.Distinct().ToArray(),
                "REQUIRED_PLACEHOLDER_UNBOUND", $"Unresolved required placeholders: {string.Join(", ", unresolvedRequired.Distinct())}");

        return new MarkdownBindingRenderResult(true, rendered, ComputeRenderedMarkdownHash(rendered), RendererVersion,
            Array.Empty<string>(), explicitOptionalEmpty.Distinct().ToArray(), null, null);
    }

    public static JsonElement BuildUpdatedSeed(JsonElement seed, string renderedHash, IEnumerable<string> unresolvedRequired, IEnumerable<string> explicitOptionalEmpty, string? sourceTableRef = null, string? sourceRecordRef = null, string? parentSavedViewId = null, string? createdFrom = null)
    {
        var doc = JsonSerializer.Deserialize<Dictionary<string, object?>>(seed.GetRawText())!;
        if (sourceTableRef is not null || sourceRecordRef is not null)
        {
            var source = JsonSerializer.Deserialize<Dictionary<string, object?>>(seed.GetProperty("source_ref").GetRawText())!;
            if (sourceTableRef is not null) source["source_table_ref"] = sourceTableRef;
            if (sourceRecordRef is not null) source["source_record_ref"] = sourceRecordRef;
            doc["source_ref"] = source;
        }
        var render = JsonSerializer.Deserialize<Dictionary<string, object?>>(seed.GetProperty("render_ref").GetRawText())!;
        render["rendered_markdown_hash"] = renderedHash;
        render["rendered_at"] = DateTime.UtcNow.ToString("o");
        render["renderer_version"] = RendererVersion;
        render["unresolved_placeholder_keys"] = unresolvedRequired.ToArray();
        doc["render_ref"] = render;

        var binding = JsonSerializer.Deserialize<Dictionary<string, object?>>(seed.GetProperty("binding_ref").GetRawText())!;
        binding["explicit_optional_empty_placeholder_keys"] = explicitOptionalEmpty.ToArray();
        binding["unresolved_required_placeholder_keys"] = unresolvedRequired.ToArray();
        doc["binding_ref"] = binding;

        if (parentSavedViewId is not null || createdFrom is not null)
        {
            var lineage = JsonSerializer.Deserialize<Dictionary<string, object?>>(seed.GetProperty("lineage_ref").GetRawText())!;
            if (parentSavedViewId is not null) lineage["parent_saved_view_id"] = parentSavedViewId;
            if (createdFrom is not null) lineage["created_from"] = createdFrom;
            doc["lineage_ref"] = lineage;
        }
        return JsonSerializer.SerializeToElement(doc);
    }

    public static string ComputeRenderedMarkdownHash(string markdown)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(markdown ?? string.Empty));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool JsonSemanticallyEquals(JsonElement left, JsonElement right) =>
        NormalizeJson(left) == NormalizeJson(right);

    private static string NormalizeJson(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(",", element.EnumerateObject()
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => JsonSerializer.Serialize(p.Name) + ":" + NormalizeJson(p.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(",", element.EnumerateArray().Select(NormalizeJson)) + "]",
        JsonValueKind.String => JsonSerializer.Serialize(element.GetString()),
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null or JsonValueKind.Undefined => "null",
        _ => element.GetRawText()
    };

    private static MarkdownBindingRenderResult Error(string code, string message) =>
        new(false, "", "", RendererVersion, Array.Empty<string>(), Array.Empty<string>(), code, message);

    private static HashSet<string> ReadStringSet(JsonElement array) =>
        array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

    private static JsonElement? FindBinding(JsonElement bindingJson, string key)
    {
        if (bindingJson.ValueKind != JsonValueKind.Object) return null;
        if (bindingJson.TryGetProperty(key, out var direct)) return direct;
        if (bindingJson.TryGetProperty("placeholder_bindings", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
                if (item.ValueKind == JsonValueKind.Object &&
                    item.TryGetProperty("placeholder_key", out var k) && k.GetString() == key)
                    return item;
        }
        return null;
    }

    private static bool IsExplicitOptionalEmpty(JsonElement binding)
    {
        var kind = ReadString(binding, "source_kind") ?? ReadString(binding, "binding_kind") ?? ReadString(binding, "empty_state");
        return kind == "explicit_optional_empty" || ReadString(binding, "empty_state") == "explicit_optional_empty";
    }

    private static string? ResolveBindingValue(JsonElement binding, JsonElement sourceRecord, out string? errorCode, out string? message)
    {
        errorCode = null; message = null;
        var kind = ReadString(binding, "source_kind") ?? ReadString(binding, "binding_kind");
        if (kind == "source_field") kind = "physical_table_column";
        if (kind == "jsonb_path") kind = "physical_table_jsonb_path";
        if (kind == "saved_query_field") kind = "saved_query_result_field";
        if (kind == "static_text") return ReadString(binding, "static_text") ?? ReadString(binding, "staticText") ?? ReadString(binding, "value") ?? "";
        if (kind == "explicit_optional_empty") return null;

        var fieldRef = ReadString(binding, "field_ref") ?? ReadString(binding, "source_field_ref") ?? ReadString(binding, "saved_query_field_ref");
        if (kind == "physical_table_jsonb_path") fieldRef = ReadString(binding, "jsonb_path") ?? fieldRef;
        if (string.IsNullOrWhiteSpace(fieldRef)) return null;
        if (!TryReadPath(sourceRecord, fieldRef!, out var value))
        {
            errorCode = kind == "physical_table_jsonb_path" ? "JSONB_PATH_INVALID" : "SOURCE_RECORD_NOT_FOUND";
            message = $"Binding path '{fieldRef}' was not found in sourceRecordJson";
            return null;
        }
        return ElementToString(value);
    }

    private static string? ReadString(JsonElement obj, string key) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool TryReadPath(JsonElement root, string path, out JsonElement value)
    {
        value = root;
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(part, out value)) return false;
        }
        return true;
    }

    private static string ElementToString(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? "",
        JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
        JsonValueKind.Null or JsonValueKind.Undefined => "",
        _ => value.GetRawText()
    };

    private sealed class MarkdownBindingRenderException : Exception
    {
        public string ErrorCode { get; }
        public MarkdownBindingRenderException(string errorCode, string message) : base(message) => ErrorCode = errorCode;
    }
}
