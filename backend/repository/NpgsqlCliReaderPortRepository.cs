using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using Topolactor.Schema;

namespace Topolactor.Repository;

public sealed class NpgsqlCliReaderPortRepository : CliReaderPortRepository
{
    private readonly string _connectionString;
    public NpgsqlCliReaderPortRepository(string connectionString) => _connectionString = string.IsNullOrWhiteSpace(connectionString) ? throw new ArgumentException("connectionString is required", nameof(connectionString)) : connectionString;

    public async Task<CliReaderPortConfig?> LoadPortAsync(string portKey, CancellationToken ct = default)
    {
        const string sql = """
            SELECT port_key, port_id, enabled, expires_at, allowed_roles, allowed_users, allowed_tables, allowed_columns,
                   allowed_filters, allowed_periods, row_scope, required_capabilities, audit_required, rate_limit_per_minute,
                   file_stream_enabled
            FROM topology.cli_reader_ports
            WHERE port_key = @port_key
            """;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("port_key", portKey);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var allowedColumnsJson = reader.GetString(7);
        return new CliReaderPortConfig(
            reader.GetString(0),
            reader.GetGuid(1),
            reader.GetBoolean(2),
            reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3),
            ReadSet(reader.GetString(4)),
            ReadSet(reader.GetString(5)),
            ReadSet(reader.GetString(6)),
            ReadColumns(allowedColumnsJson),
            ReadSet(reader.GetString(8)),
            ReadSet(reader.GetString(9)),
            ReadMap(reader.GetString(10)),
            ReadSet(reader.GetString(11)),
            reader.GetBoolean(12),
            reader.IsDBNull(13) ? null : reader.GetInt32(13),
            !reader.IsDBNull(14) && reader.GetBoolean(14));
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> ReadRowsAsync(AuthorizedCliReaderQuery query, CancellationToken ct = default)
    {
        var tableSql = QuoteQualifiedIdentifier(query.Table);
        var columnSql = query.Columns.Select(QuoteIdentifier).ToArray();
        var where = new List<string>();
        var parameters = new List<NpgsqlParameter>();
        var index = 0;

        foreach (var (filter, value) in query.Filters)
        {
            where.Add($"{QuoteIdentifier(filter)} = @p{index}");
            parameters.Add(new NpgsqlParameter($"p{index}", value));
            index++;
        }

        if (!string.IsNullOrWhiteSpace(query.Period))
        {
            // Period authority is resolved by the runtime. The repository keeps the value
            // as metadata for future snapshot windows and does not interpolate it into SQL.
        }

        if (!string.IsNullOrWhiteSpace(query.RowScope))
        {
            var parts = query.RowScope.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                throw new InvalidOperationException("CLI_READER_ROW_SCOPE_INVALID");
            where.Add($"{QuoteIdentifier(parts[0])} = @p{index}");
            parameters.Add(new NpgsqlParameter($"p{index}", parts[1]));
            index++;
        }

        var whereSql = where.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", where);
        var sql = query.Operation.ToLowerInvariant() switch
        {
            "aggregate" => $"SELECT COUNT(*)::bigint AS count FROM {tableSql}{whereSql}",
            "analyze" => $"SELECT COUNT(*)::bigint AS row_count FROM {tableSql}{whereSql}",
            "validate" => $"SELECT 1 AS valid FROM {tableSql}{whereSql} LIMIT 1",
            _ => $"SELECT {string.Join(", ", columnSql)} FROM {tableSql}{whereSql} LIMIT 100"
        };

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddRange(parameters.ToArray());
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }


    public async Task<CliReaderExportJobResult> CreateExportJobAsync(CreateCliReaderExportJobCommand command, CancellationToken ct = default)
    {
        var package = GenerateExportPackage(command);
        IReadOnlyList<CliReaderGeneratedFile> generatedFiles = [];
        var sourceRecordIdsJson = JsonSerializer.Serialize(command.SourceRecordIds);
        var targetScope = $"table={command.Query.Table};columns={string.Join(',', command.Query.Columns)};filters={string.Join(',', command.Query.Filters.Keys)};period={command.Query.Period ?? "none"};row_scope=resolved";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            WITH inserted AS (
                INSERT INTO topology.export_jobs
                    (export_job_id, port_id, port_kind, requested_by, requested_at, period, target_scope,
                     export_format, status, source_record_ids, generated_files, idempotency_key,
                     checksum, manifest_path, completed_at, approval_required, approval_status)
                VALUES
                    (gen_random_uuid(), @port_id, 'access_port', @requested_by, @requested_at, @period, @target_scope,
                     @export_format, 'completed', @source_record_ids::jsonb, '[]'::jsonb, @idempotency_key,
                     @checksum, '', @completed_at, false, 'not_required')
                ON CONFLICT (idempotency_key) DO NOTHING
                RETURNING export_job_id, status, export_format, source_record_ids, generated_files, checksum, manifest_path, true AS inserted
            )
            SELECT export_job_id, status, export_format, source_record_ids, generated_files, checksum, manifest_path, inserted FROM inserted
            UNION ALL
            SELECT export_job_id, status, export_format, source_record_ids, generated_files, checksum, manifest_path, false AS inserted
            FROM topology.export_jobs
            WHERE idempotency_key = @idempotency_key
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("port_id", command.PortId);
        cmd.Parameters.AddWithValue("requested_by", command.Query.UserId);
        cmd.Parameters.AddWithValue("requested_at", command.RequestedAt);
        cmd.Parameters.AddWithValue("period", command.Query.Period is not null ? (object)command.Query.Period : DBNull.Value);
        cmd.Parameters.AddWithValue("target_scope", targetScope);
        cmd.Parameters.AddWithValue("export_format", command.ExportFormat);
        cmd.Parameters.AddWithValue("source_record_ids", sourceRecordIdsJson);
        cmd.Parameters.AddWithValue("idempotency_key", command.IdempotencyKey);
        cmd.Parameters.AddWithValue("checksum", package.Checksum);
        cmd.Parameters.AddWithValue("completed_at", command.RequestedAt);

        Guid exportJobId;
        string status;
        string exportFormatDb;
        string sourceIdsDb;
        string filesDb;
        string checksumDb;
        string manifestPathDb;
        bool inserted;
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            await reader.ReadAsync(ct);
            exportJobId = reader.GetGuid(0);
            status = reader.GetString(1);
            exportFormatDb = reader.GetString(2);
            sourceIdsDb = reader.GetString(3);
            filesDb = reader.GetString(4);
            checksumDb = reader.GetString(5);
            manifestPathDb = reader.GetString(6);
            inserted = reader.GetBoolean(7);
        }

        if (inserted)
        {
            manifestPathDb = $"topolactor://exports/{exportJobId}/manifest.json";
            generatedFiles = [new CliReaderGeneratedFile(package.FileName, command.ExportFormat, package.Bytes.LongLength, package.Checksum, $"cli-reader-export-job://{exportJobId}/{package.FileName}")];
            filesDb = JsonSerializer.Serialize(generatedFiles);

            await using var updateJobCmd = conn.CreateCommand();
            updateJobCmd.Transaction = tx;
            updateJobCmd.CommandText = "UPDATE topology.export_jobs SET generated_files = @generated_files::jsonb, manifest_path = @manifest_path, updated_at = now() WHERE export_job_id = @export_job_id";
            updateJobCmd.Parameters.AddWithValue("generated_files", filesDb);
            updateJobCmd.Parameters.AddWithValue("manifest_path", manifestPathDb);
            updateJobCmd.Parameters.AddWithValue("export_job_id", exportJobId);
            await updateJobCmd.ExecuteNonQueryAsync(ct);

            var finalManifest = new
            {
                manifest_version = "1.0",
                export_job_id = exportJobId,
                generated_at = command.RequestedAt,
                generated_by = command.Query.UserId,
                period = command.Query.Period,
                source_tables = new[] { command.Query.Table },
                source_record_ids = command.SourceRecordIds,
                files = generatedFiles,
                checksum = checksumDb
            };
            var finalManifestJson = JsonSerializer.Serialize(finalManifest);
            await using var manifestCmd = conn.CreateCommand();
            manifestCmd.Transaction = tx;
            manifestCmd.CommandText = """
                INSERT INTO topology.export_manifests
                    (export_job_id, manifest_version, generated_at, generated_by, period, export_format,
                     checksum, file_artifact_ids, manifest_jsonb)
                VALUES
                    (@export_job_id, '1.0', @generated_at, @generated_by, @period, @export_format,
                     @checksum, '[]'::jsonb, @manifest_jsonb::jsonb)
                """;
            manifestCmd.Parameters.AddWithValue("export_job_id", exportJobId);
            manifestCmd.Parameters.AddWithValue("generated_at", command.RequestedAt);
            manifestCmd.Parameters.AddWithValue("generated_by", command.Query.UserId);
            manifestCmd.Parameters.AddWithValue("period", command.Query.Period is not null ? (object)command.Query.Period : DBNull.Value);
            manifestCmd.Parameters.AddWithValue("export_format", command.ExportFormat);
            manifestCmd.Parameters.AddWithValue("checksum", checksumDb);
            manifestCmd.Parameters.AddWithValue("manifest_jsonb", finalManifestJson);
            await manifestCmd.ExecuteNonQueryAsync(ct);

            await using var eventCmd = conn.CreateCommand();
            eventCmd.Transaction = tx;
            eventCmd.CommandText = "INSERT INTO topology.runtime_event_log (event_type, entity_id, required_by_bundle, logged_at) VALUES ('cli_mcp_export_job_created', @entity_id, 'cli_mcp_export_job_port', @logged_at)";
            eventCmd.Parameters.AddWithValue("entity_id", exportJobId.ToString());
            eventCmd.Parameters.AddWithValue("logged_at", command.RequestedAt);
            await eventCmd.ExecuteNonQueryAsync(ct);
        }

        string manifestJsonDb;
        await using (var loadManifestCmd = conn.CreateCommand())
        {
            loadManifestCmd.Transaction = tx;
            loadManifestCmd.CommandText = "SELECT manifest_jsonb FROM topology.export_manifests WHERE export_job_id = @export_job_id";
            loadManifestCmd.Parameters.AddWithValue("export_job_id", exportJobId);
            manifestJsonDb = (string)(await loadManifestCmd.ExecuteScalarAsync(ct) ?? "{}");
        }

        await tx.CommitAsync(ct);

        return new CliReaderExportJobResult(
            exportJobId,
            status,
            exportFormatDb,
            JsonSerializer.Deserialize<string[]>(sourceIdsDb) ?? [],
            JsonSerializer.Deserialize<CliReaderGeneratedFile[]>(filesDb) ?? [],
            checksumDb,
            manifestPathDb,
            JsonSerializer.Deserialize<JsonElement>(manifestJsonDb));
    }


    private static GeneratedExportPackage GenerateExportPackage(CreateCliReaderExportJobCommand command)
    {
        var format = command.ExportFormat.ToLowerInvariant();
        var fileName = $"cli-reader-export-{command.IdempotencyKey}.{format}";
        var bytes = format switch
        {
            "csv" => ToCsvBytes(command.Rows),
            "json" => JsonSerializer.SerializeToUtf8Bytes(command.Rows, new JsonSerializerOptions { WriteIndented = true }),
            "pdf" => ToPdfBytes(command.Rows),
            "zip" => ToZipBytes(command.Rows),
            _ => throw new InvalidOperationException("CLI_READER_EXPORT_FORMAT_DENIED")
        };
        var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new GeneratedExportPackage(fileName, bytes, checksum);
    }

    private static byte[] ToCsvBytes(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        var columns = rows.SelectMany(row => row.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', columns.Select(EscapeCsv)));
        foreach (var row in rows)
            builder.AppendLine(string.Join(',', columns.Select(column => EscapeCsv(row.TryGetValue(column, out var value) ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) : string.Empty))));
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static string EscapeCsv(string? value)
    {
        var text = value ?? string.Empty;
        return text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }

    private static byte[] ToPdfBytes(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        // Minimal deterministic PDF-like package. The file-stream bundle may replace this
        // with rich rendering later, but create_export_job must produce format-distinct bytes now.
        var escapedText = JsonSerializer.Serialize(rows).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        var pdf = $"%PDF-1.4\n1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >> endobj\n4 0 obj << /Length {escapedText.Length + 36} >> stream\nBT /F1 10 Tf 36 756 Td ({escapedText}) Tj ET\nendstream endobj\n%%EOF\n";
        return Encoding.UTF8.GetBytes(pdf);
    }

    private static byte[] ToZipBytes(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("export.json");
            using var entryStream = entry.Open();
            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(rows, new JsonSerializerOptions { WriteIndented = true });
            entryStream.Write(jsonBytes, 0, jsonBytes.Length);
        }
        return stream.ToArray();
    }

    private sealed record GeneratedExportPackage(string FileName, byte[] Bytes, string Checksum);

    public async Task AppendRuntimeEventAsync(CliReaderPortRuntimeEvent runtimeEvent, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO topology.cli_reader_port_runtime_events
                (port_key, operation, user_id, roles, status, code, request_id, idempotency_key, scope_summary, observed_at)
            VALUES
                (@port_key, @operation, @user_id, @roles::jsonb, @status, @code, @request_id, @idempotency_key, @scope_summary, @observed_at)
            """;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("port_key", runtimeEvent.PortKey);
        cmd.Parameters.AddWithValue("operation", runtimeEvent.Operation);
        cmd.Parameters.AddWithValue("user_id", (object?)runtimeEvent.UserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("roles", JsonSerializer.Serialize(runtimeEvent.Roles));
        cmd.Parameters.AddWithValue("status", runtimeEvent.Status);
        cmd.Parameters.AddWithValue("code", runtimeEvent.Code);
        cmd.Parameters.AddWithValue("request_id", (object?)runtimeEvent.RequestId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("idempotency_key", (object?)runtimeEvent.IdempotencyKey ?? DBNull.Value);
        cmd.Parameters.AddWithValue("scope_summary", runtimeEvent.ScopeSummary);
        cmd.Parameters.AddWithValue("observed_at", runtimeEvent.ObservedAt);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<AuthorizedExportFile?> LoadAuthorizedExportFileAsync(LoadAuthorizedExportFileQuery query, CancellationToken ct = default)
    {
        // Authorization is enforced in the WHERE clause: the export job must belong to the
        // dispatch-resolved port_id AND have been requested by the authenticated user. The
        // canonical export_jobs ledger and export_manifests record are the only source of truth.
        const string sql = """
            SELECT j.status, j.export_format, j.period, j.requested_by, j.completed_at, j.requested_at,
                   j.approval_required, j.approval_status, j.source_record_ids, j.generated_files, j.checksum,
                   (m.manifest_id IS NOT NULL) AS manifest_present,
                   COALESCE(m.manifest_version, '') AS manifest_version,
                   COALESCE(m.checksum, '') AS manifest_checksum,
                   COALESCE(m.manifest_jsonb, '{}'::jsonb) AS manifest_jsonb
            FROM topology.export_jobs j
            LEFT JOIN topology.export_manifests m ON m.export_job_id = j.export_job_id
            WHERE j.export_job_id = @export_job_id
              AND j.port_id = @port_id
              AND j.requested_by = @requested_by
            """;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("export_job_id", query.ExportJobId);
        cmd.Parameters.AddWithValue("port_id", query.PortId);
        cmd.Parameters.AddWithValue("requested_by", query.UserId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        var status = reader.GetString(0);
        var exportFormat = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        var period = reader.IsDBNull(2) ? null : reader.GetString(2);
        var generatedBy = reader.GetString(3);
        var generatedAt = reader.IsDBNull(4)
            ? reader.GetFieldValue<DateTimeOffset>(5)
            : reader.GetFieldValue<DateTimeOffset>(4);
        var approvalRequired = reader.GetBoolean(6);
        var approvalStatus = reader.IsDBNull(7) ? "not_required" : reader.GetString(7);
        var sourceRecordIds = JsonSerializer.Deserialize<string[]>(reader.GetString(8)) ?? [];
        var generatedFiles = JsonSerializer.Deserialize<CliReaderGeneratedFile[]>(reader.GetString(9)) ?? [];
        var jobChecksum = reader.IsDBNull(10) ? string.Empty : reader.GetString(10);
        var manifestPresent = reader.GetBoolean(11);
        var manifestVersion = reader.GetString(12);
        // Manifest-side checksum read directly into the projection so it can be cross-checked
        // against the job checksum at the runtime (manifest and job checksum must be identical).
        var manifestLedgerChecksum = reader.GetString(13);
        var manifestJsonb = JsonSerializer.Deserialize<JsonElement>(reader.GetString(14));
        var manifestSourceRecordIds = ExtractManifestSourceRecordIds(manifestJsonb);

        return new AuthorizedExportFile(
            query.ExportJobId,
            status,
            exportFormat,
            period,
            generatedBy,
            generatedAt,
            approvalRequired,
            approvalStatus,
            sourceRecordIds,
            generatedFiles,
            jobChecksum,
            manifestPresent,
            manifestVersion,
            manifestLedgerChecksum,
            manifestSourceRecordIds,
            manifestJsonb);
    }

    public async Task RecordExportDownloadEvidenceAsync(Guid exportJobId, bool checksumVerified, DateTimeOffset observedAt, CancellationToken ct = default)
    {
        // Append-only file-storage runtime evidence (no credential / bucket / endpoint / signed URL).
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        if (checksumVerified)
            await AppendRuntimeEventLogAsync(conn, tx, "cli_mcp_file_checksum_verified", exportJobId, observedAt, ct);
        await AppendRuntimeEventLogAsync(conn, tx, "cli_mcp_file_download_completed", exportJobId, observedAt, ct);

        await tx.CommitAsync(ct);
    }

    public async Task RecordExportDownloadFailureEvidenceAsync(Guid exportJobId, string failureCode, DateTimeOffset observedAt, CancellationToken ct = default)
    {
        // Append-only file-storage failure evidence for reject_file_explicitly + record_to_runtime_event_log.
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await AppendRuntimeEventLogAsync(conn, tx, MapFileStreamFailureEventType(failureCode), exportJobId, observedAt, ct);

        await tx.CommitAsync(ct);
    }

    private static string MapFileStreamFailureEventType(string failureCode) => failureCode switch
    {
        "CLI_READER_FILE_SOURCE_IDS_MISMATCH" => "cli_mcp_file_source_ids_mismatch_rejected",
        "CLI_READER_FILE_MANIFEST_MISSING" => "cli_mcp_file_manifest_missing_rejected",
        "CLI_READER_FILE_ARTIFACT_MISSING" => "cli_mcp_file_artifact_missing_rejected",
        "CLI_READER_FILE_CHECKSUM_MISSING" => "cli_mcp_file_checksum_missing_rejected",
        _ => "cli_mcp_file_checksum_mismatch_rejected"
    };

    private static async Task AppendRuntimeEventLogAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string eventType, Guid exportJobId, DateTimeOffset observedAt, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO topology.runtime_event_log (event_type, entity_id, required_by_bundle, logged_at) VALUES (@event_type, @entity_id, 'cli_mcp_file_stream_port', @logged_at)";
        cmd.Parameters.AddWithValue("event_type", eventType);
        cmd.Parameters.AddWithValue("entity_id", exportJobId.ToString());
        cmd.Parameters.AddWithValue("logged_at", observedAt);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static IReadOnlyList<string> ExtractManifestSourceRecordIds(JsonElement manifest)
    {
        if (manifest.ValueKind != JsonValueKind.Object) return [];
        if (!manifest.TryGetProperty("source_record_ids", out var ids) || ids.ValueKind != JsonValueKind.Array) return [];
        return ids.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString()!)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
    }

    private static string QuoteQualifiedIdentifier(string value)
    {
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is < 1 or > 2) throw new InvalidOperationException("CLI_READER_TABLE_IDENTIFIER_INVALID");
        return string.Join(".", parts.Select(QuoteIdentifier));
    }

    private static string QuoteIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !System.Text.RegularExpressions.Regex.IsMatch(value, "^[A-Za-z_][A-Za-z0-9_]*$"))
            throw new InvalidOperationException("CLI_READER_IDENTIFIER_INVALID");
        return $"\"{value}\"";
    }

    private static IReadOnlySet<string> ReadSet(string json) => JsonSerializer.Deserialize<string[]>(json)?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static IReadOnlyDictionary<string, string> ReadMap(string json) => JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
    private static IReadOnlyDictionary<string, IReadOnlySet<string>> ReadColumns(string json) => (JsonSerializer.Deserialize<Dictionary<string, string[]>>(json) ?? new()).ToDictionary(kvp => kvp.Key, kvp => (IReadOnlySet<string>)kvp.Value.ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
}
