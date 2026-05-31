using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// Persistence boundary for admin CSV/JSON import scaffold.
/// Owns snapshot creation, record persistence, apply log writing, and list queries.
///
/// DB tables (db/manifest_tables.sql, integrated in manifest topology boundary):
///   topology.admin_import_snapshot (topology_manifest_id FK -> hubs.topology_manifests)
///   topology.admin_import_records
///   topology.admin_import_apply_log
///   hubs.topology_manifests (list query — canonical, replaced manifest FK)
///   topology.schema_registry (list query)
///
/// In-memory base: test double only. All write methods are no-ops (returns true).
/// Production: override in NpgsqlAdminImportRepository.
/// </summary>
public class AdminImportRepository
{
    protected readonly ILogger<AdminImportRepository> _logger;

    public AdminImportRepository(ILogger<AdminImportRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new admin_import_snapshot row.
    /// topologyManifestId references hubs.topology_manifests (canonical).
    /// Returns true on success.
    /// </summary>
    public virtual Task<bool> CreateSnapshotAsync(
        Guid snapshotId,
        string sourceType,
        string fileName,
        Guid topologyManifestId,
        JsonElement rawHeaderJsonb,
        JsonElement rawRowsJsonb,
        JsonElement validationSummaryJsonb,
        CancellationToken ct = default)
    {
        _logger.LogDebug("AdminImportRepository.CreateSnapshotAsync: test-double no-op, snapshotId={Id}", snapshotId);
        return Task.FromResult(true);
    }

    /// <summary>
    /// Inserts admin_import_records rows for the given snapshot.
    /// topologyManifestId is a soft reference (no FK constraint on records table).
    /// status is manifest/schema conformity — not business state or hub lifecycle state.
    /// </summary>
    public virtual Task<bool> CreateRecordsAsync(
        Guid topologyManifestId,
        Guid snapshotId,
        IReadOnlyList<(JsonElement records, string status, JsonElement validationErrors)> rows,
        CancellationToken ct = default)
    {
        _logger.LogDebug("AdminImportRepository.CreateRecordsAsync: test-double no-op, rows={Count}", rows.Count);
        return Task.FromResult(true);
    }

    /// <summary>
    /// Creates an admin_import_apply_log row.
    /// At MVP, status is 'staged' — no canonical mutation is performed.
    /// applied_diff_jsonb records source snapshot / applied count / target manifest for audit.
    /// </summary>
    public virtual Task<bool> CreateApplyLogAsync(
        Guid applyLogId,
        Guid snapshotId,
        int appliedRecordCount,
        JsonElement appliedDiffJsonb,
        string status,
        CancellationToken ct = default)
    {
        _logger.LogDebug("AdminImportRepository.CreateApplyLogAsync: test-double no-op, applyLogId={Id}", applyLogId);
        return Task.FromResult(true);
    }

    /// <summary>
    /// Returns the count of valid records for the given snapshot.
    /// Used by apply to determine how many records would be applied.
    /// </summary>
    public virtual Task<int> CountValidRecordsAsync(
        Guid snapshotId,
        CancellationToken ct = default)
    {
        return Task.FromResult(0);
    }

    /// <summary>
    /// Returns a summary list of all manifests (id, status, created_at).
    /// Used by the admin import UI selector.
    /// </summary>
    public virtual Task<IReadOnlyList<AdminImportManifestSummary>> ListManifestsAsync(
        CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<AdminImportManifestSummary>>(Array.Empty<AdminImportManifestSummary>());
    }

    /// <summary>
    /// Returns active schema_registry entries (schema_id, name).
    /// Used by the admin import UI schema selector.
    /// </summary>
    public virtual Task<IReadOnlyList<AdminImportSchemaSummary>> ListSchemasAsync(
        CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<AdminImportSchemaSummary>>(Array.Empty<AdminImportSchemaSummary>());
    }

    /// <summary>
    /// Verifies that the given snapshot_id exists.
    /// Returns false when not found — caller emits explicit error.
    /// </summary>
    public virtual Task<bool> SnapshotExistsAsync(
        Guid snapshotId,
        CancellationToken ct = default)
    {
        return Task.FromResult(false);
    }

    /// <summary>
    /// Checks whether the given topology_manifest_id exists in hubs.topology_manifests.
    /// Returns false when not found — caller emits MANIFEST_NOT_FOUND explicit error.
    /// </summary>
    public virtual Task<bool> ManifestExistsAsync(
        Guid topologyManifestId,
        CancellationToken ct = default)
    {
        return Task.FromResult(false);
    }

    /// <summary>
    /// Loads snapshot metadata (topology_manifest_id as ManifestId, source_type, file_name) for diff linkage.
    /// Returns null when not found — caller may skip linkage or emit explicit error.
    /// </summary>
    public virtual Task<AdminImportSnapshotMeta?> GetSnapshotMetaAsync(
        Guid snapshotId,
        CancellationToken ct = default)
    {
        return Task.FromResult<AdminImportSnapshotMeta?>(null);
    }
}
