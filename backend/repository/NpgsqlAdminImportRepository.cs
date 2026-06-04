using System.Text.Json;
using Microsoft.Extensions.Logging;
using Npgsql;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// Production Npgsql implementation of AdminImportRepository.
///
/// Writes to:
///   topology.admin_import_snapshot
///   topology.admin_import_records
///   topology.admin_import_apply_log
///
/// Reads from:
///   hubs.topology_manifests (list — canonical, replaced public.manifest FK)
///   topology.schema_registry (list)
/// </summary>
public class NpgsqlAdminImportRepository : AdminImportRepository
{
    private readonly string _connectionString;

    public NpgsqlAdminImportRepository(
        ILogger<NpgsqlAdminImportRepository> logger,
        string connectionString)
        : base(logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public override async Task<bool> CreateSnapshotAsync(
        Guid snapshotId,
        string sourceType,
        string fileName,
        Guid topologyManifestId,
        JsonElement rawHeaderJsonb,
        JsonElement rawRowsJsonb,
        JsonElement validationSummaryJsonb,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO topology.admin_import_snapshot " +
            "(snapshot_id, source_type, file_name, topology_manifest_id, raw_header_jsonb, raw_rows_jsonb, validation_summary_jsonb) " +
            "VALUES (@sid, @st, @fn, @mid, @rh::jsonb, @rr::jsonb, @vs::jsonb)";
        cmd.Parameters.AddWithValue("sid", snapshotId);
        cmd.Parameters.AddWithValue("st", sourceType);
        cmd.Parameters.AddWithValue("fn", fileName);
        cmd.Parameters.AddWithValue("mid", topologyManifestId);
        cmd.Parameters.AddWithValue("rh", rawHeaderJsonb.GetRawText());
        cmd.Parameters.AddWithValue("rr", rawRowsJsonb.GetRawText());
        cmd.Parameters.AddWithValue("vs", validationSummaryJsonb.GetRawText());
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    public override async Task<bool> CreateRecordsAsync(
        Guid topologyManifestId,
        Guid snapshotId,
        IReadOnlyList<(JsonElement records, string status, JsonElement validationErrors)> rows,
        CancellationToken ct = default)
    {
        if (rows.Count == 0) return true;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        foreach (var (records, status, errors) in rows)
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                "INSERT INTO topology.admin_import_records " +
                "(topology_manifest_id, snapshot_id, records, status, validation_errors_jsonb) " +
                "VALUES (@mid, @sid, @rec::jsonb, @st, @ve::jsonb)";
            cmd.Parameters.AddWithValue("mid", topologyManifestId);
            cmd.Parameters.AddWithValue("sid", snapshotId);
            cmd.Parameters.AddWithValue("rec", records.GetRawText());
            cmd.Parameters.AddWithValue("st", status);
            cmd.Parameters.AddWithValue("ve", errors.GetRawText());
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return true;
    }

    public override async Task<bool> CreateApplyLogAsync(
        Guid applyLogId,
        Guid snapshotId,
        int appliedRecordCount,
        JsonElement appliedDiffJsonb,
        string status,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO topology.admin_import_apply_log " +
            "(apply_log_id, snapshot_id, applied_record_count, applied_diff_jsonb, status) " +
            "VALUES (@lid, @sid, @cnt, @diff::jsonb, @st)";
        cmd.Parameters.AddWithValue("lid", applyLogId);
        cmd.Parameters.AddWithValue("sid", snapshotId);
        cmd.Parameters.AddWithValue("cnt", appliedRecordCount);
        cmd.Parameters.AddWithValue("diff", appliedDiffJsonb.GetRawText());
        cmd.Parameters.AddWithValue("st", status);
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    public override async Task<int> CountValidRecordsAsync(
        Guid snapshotId,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*)::int FROM topology.admin_import_records " +
            "WHERE snapshot_id = @sid AND status = 'valid'";
        cmd.Parameters.AddWithValue("sid", snapshotId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int i ? i : 0;
    }

    public override async Task<IReadOnlyList<AdminImportManifestSummary>> ListManifestsAsync(
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT topology_manifest_id, status, created_at, manifest_key, hub_id::text " +
            "FROM hubs.topology_manifests " +
            "ORDER BY created_at DESC " +
            "LIMIT 200";
        var results = new List<AdminImportManifestSummary>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new AdminImportManifestSummary(
                ManifestId: reader.GetGuid(0),
                Status: reader.GetString(1),
                CreatedAt: reader.GetFieldValue<DateTimeOffset>(2),
                ManifestKey: reader.GetString(3),
                HubId: reader.IsDBNull(4) ? null : reader.GetString(4)));
        }
        return results;
    }

    public override async Task<IReadOnlyList<AdminImportSchemaSummary>> ListSchemasAsync(
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT schema_id, name " +
            "FROM topology.schema_registry " +
            "WHERE active = true " +
            "ORDER BY name";
        var results = new List<AdminImportSchemaSummary>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new AdminImportSchemaSummary(
                SchemaId: reader.GetGuid(0),
                Name: reader.GetString(1)));
        }
        return results;
    }

    public override async Task<bool> SnapshotExistsAsync(
        Guid snapshotId,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT 1 FROM topology.admin_import_snapshot WHERE snapshot_id = @sid LIMIT 1";
        cmd.Parameters.AddWithValue("sid", snapshotId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is not null;
    }

    public override async Task<bool> ManifestExistsAsync(
        Guid topologyManifestId,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM hubs.topology_manifests WHERE topology_manifest_id = @mid LIMIT 1";
        cmd.Parameters.AddWithValue("mid", topologyManifestId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is not null;
    }

    public override async Task<AdminImportSnapshotMeta?> GetSnapshotMetaAsync(
        Guid snapshotId,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT topology_manifest_id, source_type, file_name " +
            "FROM topology.admin_import_snapshot WHERE snapshot_id = @sid LIMIT 1";
        cmd.Parameters.AddWithValue("sid", snapshotId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return new AdminImportSnapshotMeta(
                ManifestId: reader.GetGuid(0),
                SourceType: reader.GetString(1),
                FileName: reader.GetString(2));
        return null;
    }

    public override async Task<IReadOnlyList<AdminImportRecordData>> ListSnapshotRecordsAsync(
        Guid snapshotId,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT records, status, validation_errors_jsonb " +
            "FROM topology.admin_import_records " +
            "WHERE snapshot_id = @sid " +
            "ORDER BY id";
        cmd.Parameters.AddWithValue("sid", snapshotId);
        var results = new List<AdminImportRecordData>();
        int rowIndex = 0;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var recordsJson = reader.GetString(0);
            var status = reader.GetString(1);
            var errorsJson = reader.GetString(2);
            Dictionary<string, object?> row;
            try
            {
                using var doc = JsonDocument.Parse(recordsJson);
                row = new Dictionary<string, object?>();
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        row[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => prop.Value.GetString(),
                            JsonValueKind.Number => prop.Value.TryGetDouble(out var d) ? d : prop.Value.GetRawText(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.Null => null,
                            _ => prop.Value.GetRawText(),
                        };
                    }
                }
            }
            catch (JsonException)
            {
                row = new Dictionary<string, object?>();
            }

            List<string> errors;
            try
            {
                errors = JsonSerializer.Deserialize<List<string>>(errorsJson) ?? new List<string>();
            }
            catch (JsonException)
            {
                errors = new List<string>();
            }

            results.Add(new AdminImportRecordData(
                RowIndex: rowIndex,
                Records: row,
                Status: status,
                ValidationErrors: errors));
            rowIndex++;
        }

        return results;
    }
}
