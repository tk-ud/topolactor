using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Topolactor.Repository;

/// <summary>
/// Production Npgsql implementation of DiffLogRepository.
/// Persists edit diff log entries to topology.topology_edit_log (append-only).
///
/// Replaces the ILogger-only base class implementation.
/// Uses topology.topology_edit_log schema from db/topology_tables.sql.
///
/// Wiring: inject NpgsqlDiffLogRepository wherever DiffLogRepository is required
/// in production DI. Tests continue to use the in-memory base class via override.
/// </summary>
public class NpgsqlDiffLogRepository : DiffLogRepository
{
    private readonly ILogger<NpgsqlDiffLogRepository> _npgsqlLogger;
    private readonly string _connectionString;

    public NpgsqlDiffLogRepository(
        ILogger<NpgsqlDiffLogRepository> logger,
        string connectionString)
        : base(NullLogger<DiffLogRepository>.Instance)
    {
        _npgsqlLogger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Appends an edit diff log entry to topology.topology_edit_log (append-only INSERT).
    /// Non-fatal: callers catch exceptions and continue.
    /// </summary>
    public override async Task AppendEditAsync(
        string targetTable,
        string? targetId,
        string operation,
        string? beforeJson,
        string? afterJson,
        string? diffJson,
        string? actor,
        CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO topology.topology_edit_log " +
                "(target_table, target_id, operation, before_json, after_json, diff_json, actor) " +
                "VALUES (@targetTable, @targetId, @operation, @beforeJson::jsonb, @afterJson::jsonb, @diffJson::jsonb, @actor)";

            cmd.Parameters.AddWithValue("targetTable", targetTable);
            cmd.Parameters.AddWithValue("targetId",   targetId is not null ? (object)targetId : DBNull.Value);
            cmd.Parameters.AddWithValue("operation",  operation);
            cmd.Parameters.AddWithValue("beforeJson", beforeJson is not null ? (object)beforeJson : DBNull.Value);
            cmd.Parameters.AddWithValue("afterJson",  afterJson is not null ? (object)afterJson : DBNull.Value);
            cmd.Parameters.AddWithValue("diffJson",   diffJson is not null ? (object)diffJson : DBNull.Value);
            cmd.Parameters.AddWithValue("actor",      actor is not null ? (object)actor : DBNull.Value);

            await cmd.ExecuteNonQueryAsync(ct);

            _npgsqlLogger.LogDebug(
                "NpgsqlDiffLogRepository.AppendEditAsync: logged targetTable={TargetTable} operation={Operation}.",
                targetTable, operation);
        }
        catch (Exception ex)
        {
            _npgsqlLogger.LogError(ex,
                "NpgsqlDiffLogRepository.AppendEditAsync: INSERT to topology.topology_edit_log failed for targetTable={TargetTable} operation={Operation}.",
                targetTable, operation);
            throw;
        }
    }
}
