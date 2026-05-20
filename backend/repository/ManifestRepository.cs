using System.Text.Json;
using Microsoft.Extensions.Logging;

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
}
