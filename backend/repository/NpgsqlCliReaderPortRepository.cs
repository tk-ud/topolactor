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
            SELECT port_key, enabled, expires_at, allowed_roles, allowed_users, allowed_tables, allowed_columns,
                   allowed_filters, allowed_periods, row_scope, required_capabilities, audit_required, rate_limit_per_minute
            FROM topology.cli_reader_ports
            WHERE port_key = @port_key
            """;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("port_key", portKey);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var allowedColumnsJson = reader.GetString(6);
        return new CliReaderPortConfig(
            reader.GetString(0),
            reader.GetBoolean(1),
            reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2),
            ReadSet(reader.GetString(3)),
            ReadSet(reader.GetString(4)),
            ReadSet(reader.GetString(5)),
            ReadColumns(allowedColumnsJson),
            ReadSet(reader.GetString(7)),
            ReadSet(reader.GetString(8)),
            ReadMap(reader.GetString(9)),
            ReadSet(reader.GetString(10)),
            reader.GetBoolean(11),
            reader.IsDBNull(12) ? null : reader.GetInt32(12));
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
        var payloadJson = JsonSerializer.Serialize(command.Rows);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
        var payloadChecksum = Convert.ToHexString(SHA256.HashData(payloadBytes)).ToLowerInvariant();
        var fileName = $"cli-reader-export-{command.IdempotencyKey}.{command.ExportFormat.ToLowerInvariant()}";
        var generatedFiles = new[] { new CliReaderGeneratedFile(fileName, command.ExportFormat, payloadBytes.LongLength, payloadChecksum, "pending-export-job-id") };
        var manifestObject = new
        {
            manifest_version = "1.0",
            export_job_id = (Guid?)null,
            generated_at = command.RequestedAt,
            generated_by = command.Query.UserId,
            period = command.Query.Period,
            source_tables = new[] { command.Query.Table },
            source_record_ids = command.SourceRecordIds,
            files = generatedFiles,
            checksum = payloadChecksum
        };
        var manifestJson = JsonSerializer.Serialize(manifestObject);
        var manifestChecksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifestJson))).ToLowerInvariant();
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
                RETURNING export_job_id, status, source_record_ids, generated_files, checksum, manifest_path, true AS inserted
            )
            SELECT export_job_id, status, source_record_ids, generated_files, checksum, manifest_path, inserted FROM inserted
            UNION ALL
            SELECT export_job_id, status, source_record_ids, generated_files, checksum, manifest_path, false AS inserted
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
        cmd.Parameters.AddWithValue("checksum", manifestChecksum);
        cmd.Parameters.AddWithValue("completed_at", command.RequestedAt);

        Guid exportJobId;
        string status;
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
            sourceIdsDb = reader.GetString(2);
            filesDb = reader.GetString(3);
            checksumDb = reader.GetString(4);
            manifestPathDb = reader.GetString(5);
            inserted = reader.GetBoolean(6);
        }

        if (inserted)
        {
            manifestPathDb = $"topolactor://exports/{exportJobId}/manifest.json";
            generatedFiles = [new CliReaderGeneratedFile(fileName, command.ExportFormat, payloadBytes.LongLength, payloadChecksum, $"cli-reader-export-job://{exportJobId}/{fileName}")];
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
            command.ExportFormat,
            JsonSerializer.Deserialize<string[]>(sourceIdsDb) ?? [],
            JsonSerializer.Deserialize<CliReaderGeneratedFile[]>(filesDb) ?? [],
            checksumDb,
            manifestPathDb,
            JsonSerializer.Deserialize<JsonElement>(manifestJsonDb));
    }

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
