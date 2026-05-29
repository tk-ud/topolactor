using System.Text.Json;
using Topolactor.Schema;

namespace Topolactor.Repository;

public record ContentBundleValidationResult(
    bool Valid,
    bool IsBlocking,
    IReadOnlyList<ContentBundleValidationIssueDto> Issues
);

/// <summary>
/// Backend entity draft ref validation — frontend must not substitute this boundary.
/// Validate is read-only (no DB writes).
/// </summary>
public static class ContentBundleValidator
{
    public static ContentBundleValidationResult ValidateDraft(
        ContentEntityDraftRecord draft,
        ContentBundleRefContext refs)
    {
        var issues = new List<ContentBundleValidationIssueDto>();

        if (!refs.HubExists)
        {
            issues.Add(new ContentBundleValidationIssueDto(
                "HUB_NOT_FOUND",
                $"hub_id {draft.HubId} does not exist.",
                true));
        }

        if (draft.RelationIds.Count == 0)
        {
            issues.Add(new ContentBundleValidationIssueDto(
                "RELATION_IDS_REQUIRED",
                "At least one relationId is required.",
                true));
        }

        foreach (var relationId in draft.RelationIds)
        {
            if (!refs.RelationNames.ContainsKey(relationId))
            {
                issues.Add(new ContentBundleValidationIssueDto(
                    "RELATION_NOT_FOUND",
                    $"relation_id {relationId} does not exist or is not active.",
                    true));
            }
        }

        if (draft.StateId is null)
        {
            issues.Add(new ContentBundleValidationIssueDto(
                "STATE_NOT_FOUND",
                "state_id could not be resolved — stateName may be missing or invalid.",
                true));
        }
        else if (!refs.StateNames.ContainsKey(draft.StateId.Value))
        {
            issues.Add(new ContentBundleValidationIssueDto(
                "STATE_NOT_FOUND",
                $"state_id {draft.StateId} does not exist.",
                true));
        }

        JsonElement jsonRoot;
        try
        {
            jsonRoot = JsonDocument.Parse(draft.EntityJsonb).RootElement;
        }
        catch (JsonException)
        {
            issues.Add(new ContentBundleValidationIssueDto(
                "ENTITY_JSONB_MALFORMED",
                "entityJsonb must be valid JSON.",
                true));
            return Blocked(issues);
        }

        if (jsonRoot.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ContentBundleValidationIssueDto(
                "ENTITY_JSONB_NOT_OBJECT",
                "entityJsonb must be a JSON object.",
                true));
        }
        else
        {
            if (!jsonRoot.TryGetProperty("label", out var labelEl) ||
                labelEl.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(labelEl.GetString()))
            {
                issues.Add(new ContentBundleValidationIssueDto(
                    "LABEL_REQUIRED",
                    "entityJsonb.label is required.",
                    true));
            }
        }

        if (draft.Status != "draft")
        {
            issues.Add(new ContentBundleValidationIssueDto(
                "DRAFT_NOT_PROMOTABLE",
                $"Draft status is '{draft.Status}' — only draft status can be promoted.",
                true));
        }

        var blocking = issues.Any(i => i.IsBlocking);
        return new ContentBundleValidationResult(!blocking, blocking, issues);
    }

    public static ContentBundleValidateResponseDto ToResponse(ContentBundleValidationResult result) =>
        new(result.Valid, result.IsBlocking, result.Issues);

    private static ContentBundleValidationResult Blocked(IReadOnlyList<ContentBundleValidationIssueDto> issues) =>
        new(false, true, issues);
}
