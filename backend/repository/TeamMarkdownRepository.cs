using Microsoft.Extensions.Logging;
using Topolactor.Schema;

namespace Topolactor.Repository;

// ---------------------------------------------------------------------------
// TeamMarkdownRepository — abstract base for topology.team_markdown_* tables.
// SSOT: docs/design/team-markdown-dashboard-saved-view-ssot.yaml
// Production implementation: NpgsqlTeamMarkdownRepository
//
// Authority invariants:
//   - saved Markdown view is a projection; physical records are canonical data authority
//   - completed_preset_seed_json is required (NOT NULL)
//   - refresh uses seed binding_json, not Markdown body parsing
// ---------------------------------------------------------------------------

public abstract class TeamMarkdownRepository
{
    protected readonly ILogger<TeamMarkdownRepository> _logger;

    protected TeamMarkdownRepository(ILogger<TeamMarkdownRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ─── template create ─────────────────────────────────────────────────────

    public virtual Task<(string? TemplateId, string? ErrorCode, string? Message)>
        CreateTemplateAsync(TeamMarkdownTemplateCreateRequest request, CancellationToken ct = default)
        => Task.FromResult<(string?, string?, string?)>((null, "TEAM_MARKDOWN_REPO_NOT_CONFIGURED", "TeamMarkdownRepository not configured"));

    // ─── template list ───────────────────────────────────────────────────────

    public virtual Task<(IReadOnlyList<TeamMarkdownTemplateListItem> Items, string? ErrorCode, string? Message)>
        ListTemplatesAsync(string status = "active", CancellationToken ct = default)
        => Task.FromResult<(IReadOnlyList<TeamMarkdownTemplateListItem>, string?, string?)>(([], null, null));

    // ─── template get ────────────────────────────────────────────────────────

    public virtual Task<(TeamMarkdownTemplateDetail? Detail, string? ErrorCode, string? Message)>
        GetTemplateAsync(Guid templateId, CancellationToken ct = default)
        => Task.FromResult<(TeamMarkdownTemplateDetail?, string?, string?)>((null, "TEAM_MARKDOWN_REPO_NOT_CONFIGURED", "TeamMarkdownRepository not configured"));

    // ─── template update ─────────────────────────────────────────────────────

    public virtual Task<(bool Updated, string? ErrorCode, string? Message)>
        UpdateTemplateAsync(Guid templateId, string label, string markdown, string status, CancellationToken ct = default)
        => Task.FromResult<(bool, string?, string?)>((false, "TEAM_MARKDOWN_REPO_NOT_CONFIGURED", "TeamMarkdownRepository not configured"));

    // ─── template archive ────────────────────────────────────────────────────

    public virtual Task<(bool Updated, string? ErrorCode)>
        ArchiveTemplateAsync(Guid templateId, CancellationToken ct = default)
        => Task.FromResult<(bool, string?)>((false, "TEAM_MARKDOWN_REPO_NOT_CONFIGURED"));

    // ─── saved view create ───────────────────────────────────────────────────

    public virtual Task<(string? SavedViewId, string? ErrorCode, string? Message)>
        CreateSavedViewAsync(TeamMarkdownSavedViewCreateRequest request, CancellationToken ct = default)
        => Task.FromResult<(string?, string?, string?)>((null, "TEAM_MARKDOWN_REPO_NOT_CONFIGURED", "TeamMarkdownRepository not configured"));

    // ─── saved view search ───────────────────────────────────────────────────

    public virtual Task<(IReadOnlyList<TeamMarkdownSavedViewCard> Cards, string? ErrorCode, string? Message)>
        SearchSavedViewsAsync(string? query, string status = "active", int limit = 50, CancellationToken ct = default)
        => Task.FromResult<(IReadOnlyList<TeamMarkdownSavedViewCard>, string?, string?)>(([], null, null));

    // ─── saved view get ──────────────────────────────────────────────────────

    public virtual Task<(TeamMarkdownSavedViewDetail? Detail, string? ErrorCode, string? Message)>
        GetSavedViewAsync(Guid savedViewId, CancellationToken ct = default)
        => Task.FromResult<(TeamMarkdownSavedViewDetail?, string?, string?)>((null, "TEAM_MARKDOWN_REPO_NOT_CONFIGURED", "TeamMarkdownRepository not configured"));

    // ─── saved view update ───────────────────────────────────────────────────

    public virtual Task<(bool Updated, string? ErrorCode, string? Message)>
        UpdateSavedViewAsync(Guid savedViewId, string? title, string? renderedMarkdown,
            System.Text.Json.JsonElement? userAdjustmentPatchJson,
            System.Text.Json.JsonElement? completedPresetSeedJson,
            string? searchIndexText,
            System.Text.Json.JsonElement? cardMetadataJson,
            System.Text.Json.JsonElement? bindingJson,
            CancellationToken ct = default)
        => Task.FromResult<(bool, string?, string?)>((false, "TEAM_MARKDOWN_REPO_NOT_CONFIGURED", "TeamMarkdownRepository not configured"));


    // ─── saved view update with durable diff evidence ────────────────────────
    // Combines the mutation and its diff-evidence event append into one transaction (unlike
    // UpdateSavedViewAsync + a separate best-effort AppendEventAsync call). This is the write step
    // of the preview -> validate -> explicit_confirm -> write -> diff_log authoring workflow: the
    // event row this produces is the completion proof, not a best-effort side effect.

    public virtual Task<(bool Updated, string? ErrorCode, string? Message, string? EventId)>
        UpdateSavedViewWithDiffEvidenceAsync(Guid savedViewId, string? title, string? renderedMarkdown,
            System.Text.Json.JsonElement? userAdjustmentPatchJson,
            System.Text.Json.JsonElement? completedPresetSeedJson,
            string? searchIndexText,
            System.Text.Json.JsonElement? cardMetadataJson,
            System.Text.Json.JsonElement? bindingJson,
            string actor,
            CancellationToken ct = default)
        => Task.FromResult<(bool, string?, string?, string?)>((false, "TEAM_MARKDOWN_REPO_NOT_CONFIGURED", "TeamMarkdownRepository not configured", null));

    // ─── saved view clone ────────────────────────────────────────────────────

    public virtual Task<(string? SavedViewId, string? ErrorCode, string? Message)>
        CloneSavedViewAsync(TeamMarkdownSavedViewCreateRequest request, CancellationToken ct = default)
        => CreateSavedViewAsync(request, ct);

    // ─── saved view archive ──────────────────────────────────────────────────

    public virtual Task<(bool Updated, string? ErrorCode)>
        ArchiveSavedViewAsync(Guid savedViewId, CancellationToken ct = default)
        => Task.FromResult<(bool, string?)>((false, "TEAM_MARKDOWN_REPO_NOT_CONFIGURED"));

    // ─── event append ────────────────────────────────────────────────────────

    public virtual Task<string?>
        AppendEventAsync(Guid savedViewId, string eventKind, Guid? actorId,
            System.Text.Json.JsonElement eventPayloadJson, CancellationToken ct = default)
        => Task.FromResult<string?>(null);
}
