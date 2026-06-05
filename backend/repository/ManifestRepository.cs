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
