using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// Record representing an active manifest with its topology wiring entries.
/// </summary>
public record ManifestRecord(
    Guid ManifestId,
    Guid? RelationRegistryId,
    IReadOnlyList<JsonElement> Topology,
    string Status
);

public record ManifestListItem(
    Guid ManifestId,
    Guid? RelationRegistryId,
    string Status,
    string? Role,
    string? Target,
    string? Layer,
    string? Action,
    string? RuntimeDestination,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record ManifestDetailRecord(
    Guid ManifestId,
    Guid? RelationRegistryId,
    IReadOnlyList<JsonElement> Topology,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

/// <summary>
/// Read-only evidence captured from an active source manifest at clone time.
///
/// SSOT: admin-console-workflow-ssot.yaml replacement_clone_merge_lifecycle.required_evidence.
/// Source evidence and lineage are evidence only — they NEVER grant replacement authority on
/// their own. Replacement authority requires draft_origin=manual_clone_replacement plus the
/// full backend merge guard (UUID integrity, stale check, validation, diff/log, conflict check).
/// </summary>
public record CloneSourceEvidence(
    Guid SourceActiveManifestId,
    string? TopologySystemName,
    string? RouteKey,
    AdminManifestDispatcherMappingDto? DispatcherAxes,
    DateTimeOffset SourceUpdatedAt,
    string SourceTopologyHash,
    string Status
);

/// <summary>
/// Result of a backend replacement clone merge. On success the EXISTING active row was updated
/// in place and the working draft row was deleted (no draft-row promotion, no draft-row audit
/// persistence). Diff/log evidence is preserved in the existing edit-log surface.
/// </summary>
public record CloneReplacementMergeResult(
    bool Ok,
    Guid? ActiveManifestId,
    Guid DraftManifestId,
    string? DiffJson,
    int ChangeCount,
    ValidationError? Error
);

/// <summary>
/// Shared dispatcher_mapping axis-matching authority. Extracted from
/// NpgsqlManifestRepository so that both the axes-based resolution path
/// (ResolveActiveManifestAsync, CountActiveAxisConflictsAsync) and any other
/// caller that needs to verify a SPECIFIC already-resolved manifest's own
/// dispatcher_mapping entries against a set of axes (e.g. ManifestDispatcher's
/// target_ref admin_runtime layer/action authorization — round 17 hardening)
/// use the exact same matching semantics. A null axis value is a wildcard
/// (matches any); a non-null axis value requires an exact case-insensitive
/// match against a dispatcher_mapping entry's own field.
/// </summary>
public static class DispatcherMappingAxisAuthority
{
    public static bool MatchesAxes(
        IReadOnlyList<JsonElement> topology,
        string? role,
        string? target,
        string? layer,
        string? action)
    {
        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object)
                continue;

            if (!entry.TryGetProperty("type", out var typeEl) ||
                !string.Equals(typeEl.GetString(), "dispatcher_mapping", StringComparison.Ordinal))
                continue;

            if (!AxisMatches(entry, "role", role)) continue;
            if (!AxisMatches(entry, "target", target)) continue;
            if (!AxisMatches(entry, "layer", layer)) continue;
            if (!AxisMatches(entry, "action", action)) continue;

            return true;
        }
        return false;
    }

    private static bool AxisMatches(JsonElement entry, string propName, string? value)
    {
        if (value is null) return true;
        if (!entry.TryGetProperty(propName, out var prop)) return false;
        return string.Equals(prop.GetString(), value, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Round 18: finds the dispatcher_mapping entry for (layer, action) — role/target wildcarded,
    /// mirroring MatchesAxes' own existence check — and returns ITS OWN declared role value (null
    /// when the entry has no role field, i.e. that layer/action is role-wildcard by the manifest
    /// author's own declaration). Deliberately separate from MatchesAxes: that method's AxisMatches
    /// treats a null QUERY value as "don't care" (correct for ResolveActiveManifestAsync's optional
    /// filter semantics), which is the wrong direction for authorization — comparing a REQUEST's
    /// role against a declared requirement must never treat a missing/null request role as
    /// automatically satisfying a declared role. Callers compare the returned declared role against
    /// the request's own role with plain equality (null declared role = wildcard passes; non-null
    /// declared role requires an exact, non-null request role match).
    /// </summary>
    public static string? FindDeclaredRole(
        IReadOnlyList<JsonElement> topology, string layer, string action)
    {
        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("type", out var typeEl) ||
                !string.Equals(typeEl.GetString(), "dispatcher_mapping", StringComparison.Ordinal))
                continue;
            if (!AxisMatches(entry, "layer", layer)) continue;
            if (!AxisMatches(entry, "action", action)) continue;

            return entry.TryGetProperty("role", out var roleEl) && roleEl.ValueKind == JsonValueKind.String
                ? roleEl.GetString()
                : null;
        }
        return null;
    }

    /// <summary>
    /// Round 19: true when the topology's dispatcher_mapping entry for (layer, action) declares
    /// itself, via its OWN "identity_selector_read": true field, as a read-only,
    /// manifest-identity-agnostic operation safe to reach via a bare "runtime_mapping only"
    /// manifest's target_ref (see ManifestDispatcher.IsBareManifestNavigationReadTargetRefAsync).
    /// This is the SSOT-owned classification authority: the seed/manifest author declares it
    /// per-entry (db/seed_empty.sql), not a closed action-name allowlist hardcoded in runtime code
    /// -- adding a new identity-selector-compatible read action means authoring this field on its
    /// own dispatcher_mapping entry, never editing ManifestDispatcher.cs. False when no matching
    /// entry exists or the field is absent/not literally true (default-closed, never inferred).
    /// </summary>
    public static bool IsDeclaredIdentitySelectorRead(
        IReadOnlyList<JsonElement> topology, string layer, string action)
    {
        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("type", out var typeEl) ||
                !string.Equals(typeEl.GetString(), "dispatcher_mapping", StringComparison.Ordinal))
                continue;
            if (!AxisMatches(entry, "layer", layer)) continue;
            if (!AxisMatches(entry, "action", action)) continue;

            return entry.TryGetProperty("identity_selector_read", out var flagEl) &&
                flagEl.ValueKind == JsonValueKind.True;
        }
        return false;
    }

    /// <summary>
    /// Round 19: true when the topology contains 2+ dispatcher_mapping entries for the SAME
    /// (layer, action) whose declared role and/or identity_selector_read values disagree -- i.e.
    /// authorization for this exact operation would otherwise depend on JSONB array order (which
    /// entry FindDeclaredRole/IsDeclaredIdentitySelectorRead happens to see first), an undefined,
    /// unauditable authority. Save-time validation (ManifestTopologyValidator.Validate) rejects ANY
    /// duplicate dispatcher_mapping entry in a manifest topology outright, agreeing or not -- this
    /// runtime check is defense-in-depth against topology that reached the DB outside that boundary
    /// (hand-authored seed SQL, a future write path). Two entries that fully agree are not flagged
    /// here (no ambiguity to fail closed on), even though save-time validation still rejects them as
    /// redundant.
    /// </summary>
    public static bool HasConflictingDispatcherMappingEntries(
        IReadOnlyList<JsonElement> topology, string layer, string action)
    {
        string? firstRole = null;
        bool? firstIdentitySelectorRead = null;
        var matchCount = 0;

        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("type", out var typeEl) ||
                !string.Equals(typeEl.GetString(), "dispatcher_mapping", StringComparison.Ordinal))
                continue;
            if (!AxisMatches(entry, "layer", layer)) continue;
            if (!AxisMatches(entry, "action", action)) continue;

            var role = entry.TryGetProperty("role", out var roleEl) && roleEl.ValueKind == JsonValueKind.String
                ? roleEl.GetString()
                : null;
            var identitySelectorRead = entry.TryGetProperty("identity_selector_read", out var flagEl) &&
                flagEl.ValueKind == JsonValueKind.True;

            matchCount++;
            if (matchCount == 1)
            {
                firstRole = role;
                firstIdentitySelectorRead = identitySelectorRead;
                continue;
            }

            if (!string.Equals(firstRole, role, StringComparison.OrdinalIgnoreCase) ||
                firstIdentitySelectorRead != identitySelectorRead)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Round 18: true when the topology declares AT LEAST ONE dispatcher_mapping entry, regardless
    /// of its axis values. Distinguishes an authored, operation-scoped manifest (e.g. ae210,
    /// scoped to create_group) — which always has one — from a deliberately bare
    /// "runtime_mapping only" manifest that makes no operation-scope claim whatsoever (used purely
    /// as an identity/context selector for target_ref, e.g. the hub_navigation:get_hub_relations
    /// navigation-enrichment convention in ManifestDispatcher.IsBareManifestNavigationReadTargetRefAsync).
    /// </summary>
    public static bool HasAnyDispatcherMapping(IReadOnlyList<JsonElement> topology)
    {
        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (entry.TryGetProperty("type", out var typeEl) &&
                string.Equals(typeEl.GetString(), "dispatcher_mapping", StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}

/// <summary>
/// Abstract manifest repository. Resolves active manifests by dispatcher axes
/// (role, target, layer, action) from the manifest table.
///
/// Per SSOT dispatcher_contract.manifest_resolution.api:
///   key: [role, target, layer, action]
///   strategy: resolve_active_manifest_by_axes
///
/// Broken reference policy: missing active manifest returns null (no silent fallback).
/// </summary>
public abstract class ManifestRepository
{
    protected readonly ILogger<ManifestRepository> _logger;

    protected ManifestRepository(ILogger<ManifestRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Resolves an active manifest for the given dispatcher axes.
    /// Returns null when no active manifest matches — caller must treat as broken reference.
    /// Throws InvalidOperationException when multiple active manifests match the same axes
    /// (ambiguity is an explicit error, not a silent pick-first fallback).
    /// </summary>
    public abstract Task<ManifestRecord?> ResolveActiveManifestAsync(
        string? role,
        string? target,
        string? layer,
        string? action,
        CancellationToken ct = default);

    /// <summary>
    /// Loads a manifest by manifest_id (for SSE projection lane resolution).
    /// Returns null when not found.
    /// </summary>
    public abstract Task<ManifestRecord?> LoadByIdAsync(
        Guid manifestId,
        CancellationToken ct = default);

    /// <summary>
    /// Lists manifests optionally filtered by status (draft/active/deprecated).
    /// </summary>
    public abstract Task<IReadOnlyList<ManifestListItem>> ListManifestsAsync(
        string? statusFilter,
        CancellationToken ct = default);

    /// <summary>
    /// Active table_ref values from topology.physical_tables for manifest:list physical filter.
    /// </summary>
    public virtual Task<IReadOnlySet<string>> ListActivePhysicalTableRefsAsync(
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));

    /// <summary>
    /// Loads manifest detail including timestamps.
    /// </summary>
    public abstract Task<ManifestDetailRecord?> LoadDetailByIdAsync(
        Guid manifestId,
        CancellationToken ct = default);

    /// <summary>
    /// Counts active manifests matching dispatcher axes, optionally excluding one manifest id.
    /// </summary>
    public abstract Task<int> CountActiveAxisConflictsAsync(
        string role,
        string target,
        string layer,
        string action,
        Guid? excludeManifestId,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a draft manifest with the given topology wiring.
    /// </summary>
    public abstract Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> CreateDraftAsync(
        Guid? relationRegistryId,
        IReadOnlyList<JsonElement> topology,
        CancellationToken ct = default);

    /// <summary>
    /// Updates topology on a draft manifest only.
    /// </summary>
    public abstract Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdateDraftAsync(
        Guid manifestId,
        Guid? relationRegistryId,
        IReadOnlyList<JsonElement> topology,
        CancellationToken ct = default);

    /// <summary>
    /// Promotes a validated draft manifest to active status.
    /// </summary>
    public abstract Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> PromoteAsync(
        Guid manifestId,
        IReadOnlySet<string> allowedRuntimeDestinations,
        CancellationToken ct = default);

    /// <summary>
    /// Deprecates an active manifest.
    /// </summary>
    public abstract Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> DeprecateAsync(
        Guid manifestId,
        CancellationToken ct = default);

    /// <summary>
    /// Lists manifests that contain promotion_metadata_mapping entries.
    /// </summary>
    public abstract Task<IReadOnlyList<PromotionManifestListItem>> ListPromotionManifestsAsync(
        string? statusFilter,
        CancellationToken ct = default);

    /// <summary>
    /// Updates promotion_metadata_mapping on a draft manifest.
    /// </summary>
    public abstract Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdatePromotionMetadataDraftAsync(
        Guid manifestId,
        JsonElement promotionEntry,
        CancellationToken ct = default);

    /// <summary>
    /// Counts active manifests with the same promotion manifestKey + versionLabel.
    /// </summary>
    public abstract Task<int> CountActivePromotionKeyConflictsAsync(
        string manifestKey,
        string versionLabel,
        Guid? excludeManifestId,
        CancellationToken ct = default);

    /// <summary>
    /// Merges or replaces a typed topology extension entry on a draft manifest.
    /// </summary>
    public abstract Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> MergeTopologyExtensionDraftAsync(
        Guid manifestId,
        string entryType,
        JsonElement entryBody,
        CancellationToken ct = default);

    // -----------------------------------------------------------------------
    // Clone / replacement draft lifecycle
    // SSOT: admin-console-workflow-ssot.yaml admin_contents_step1_entry_modes /
    //       replacement_clone_merge_lifecycle
    // -----------------------------------------------------------------------

    /// <summary>
    /// Loads read-only source evidence for an active manifest. Returns null when the manifest
    /// does not exist or is not active (lineage/replacement source must resolve to active).
    /// Base default fails closed (null); ManifestRepository implementations override it.
    /// </summary>
    public virtual Task<CloneSourceEvidence?> LoadCloneSourceEvidenceAsync(
        Guid sourceActiveManifestId,
        CancellationToken ct = default) =>
        Task.FromResult<CloneSourceEvidence?>(null);

    /// <summary>
    /// Creates a replacement clone draft from an active source: copies the source topology,
    /// stamps clone_metadata (draft_origin=manual_clone_replacement, clone_mode=replacement,
    /// source_active_manifest_id + source evidence). The draft is authored through the existing
    /// contents pipeline and completed only via MergeCloneReplacementDraftToActiveAsync.
    /// Base default fails closed; ManifestRepository implementations override it.
    /// </summary>
    public virtual Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> CreateCloneReplacementDraftFromActiveAsync(
        Guid sourceActiveManifestId,
        CancellationToken ct = default) =>
        Task.FromResult<(ManifestDetailRecord?, ValidationError?)>(
            (null, new ValidationError("CLONE_NOT_SUPPORTED", "Clone replacement draft is not supported by this repository.")));

    /// <summary>
    /// Creates a clone-as-new-topology draft from an active source: copies the source content
    /// but assigns a NEW topologySystemName identity and stamps clone_metadata
    /// (draft_origin=manual_clone_new_topology, clone_mode=new_topology, lineage source evidence).
    /// Completion uses the conventional register/promote path — it can never replace the source.
    /// Base default fails closed; ManifestRepository implementations override it.
    /// </summary>
    public virtual Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> CreateCloneNewTopologyDraftFromActiveAsync(
        Guid sourceActiveManifestId,
        string newTopologySystemName,
        string? userFacingLabel,
        CancellationToken ct = default) =>
        Task.FromResult<(ManifestDetailRecord?, ValidationError?)>(
            (null, new ValidationError("CLONE_NOT_SUPPORTED", "Clone-as-new-topology draft is not supported by this repository.")));

    /// <summary>
    /// Counts active manifests (optionally excluding one id) whose dispatcher axes collide with
    /// the given axes — the replacement-merge active identity conflict check.
    /// </summary>
    public virtual Task<int> CountActiveIdentityConflictsAsync(
        string role,
        string target,
        string layer,
        string action,
        Guid? excludeManifestId,
        CancellationToken ct = default) =>
        CountActiveAxisConflictsAsync(role, target, layer, action, excludeManifestId, ct);

    /// <summary>
    /// Backend replacement merge — the ONLY authority that turns a replacement clone draft into
    /// the production active topology. Inside a single transaction this MUST:
    ///   - resolve the draft and its clone_metadata; require draft_origin=manual_clone_replacement
    ///     (source evidence / lineage / SQL Attention candidate alone never grant authority);
    ///   - verify clone.source_active_manifest_id matches a live ACTIVE row (merge target);
    ///   - fail close on stale source active (source topology hash changed since clone);
    ///   - fail close on active identity conflict (another active shares the merge-target axes);
    ///   - validate the draft topology (no blocking issue);
    ///   - produce diff/log evidence (no-op merges fail close);
    ///   - UPDATE the existing active row in place and DELETE the working draft row.
    /// The draft row is never promoted to active and never preserved as audit evidence.
    /// Base default fails closed; ManifestRepository implementations override it.
    /// </summary>
    public virtual Task<CloneReplacementMergeResult> MergeCloneReplacementDraftToActiveAsync(
        Guid draftManifestId,
        IReadOnlySet<string> allowedRuntimeDestinations,
        string? actor,
        CancellationToken ct = default) =>
        Task.FromResult(new CloneReplacementMergeResult(
            false, null, draftManifestId, null, 0,
            new ValidationError("CLONE_NOT_SUPPORTED", "Replacement merge is not supported by this repository.")));

    /// <summary>
    /// Builds read-only source evidence from an active source record. Shared by all
    /// repository implementations so evidence shape stays identical.
    /// </summary>
    protected static CloneSourceEvidence BuildSourceEvidence(ManifestDetailRecord source)
    {
        var summary = ManifestTopologyValidator.ExtractSummary(source.Topology);
        var topologySystemName = CloneDraftMetadata.ExtractTopologySystemName(source.Topology);
        var routeKey = string.IsNullOrWhiteSpace(topologySystemName) ? null : topologySystemName;
        return new CloneSourceEvidence(
            source.ManifestId,
            topologySystemName,
            routeKey,
            summary.DispatcherMapping,
            source.UpdatedAt,
            CloneDraftMetadata.ComputeTopologyHash(source.Topology),
            source.Status);
    }
}

public record PromotionManifestListItem(
    Guid ManifestId,
    string Status,
    string ManifestKey,
    string VersionLabel,
    bool HasDisclosure,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
