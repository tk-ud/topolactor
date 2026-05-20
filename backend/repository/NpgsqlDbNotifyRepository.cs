using System.Text.Json;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Topolactor.Repository;

/// <summary>
/// Npgsql implementation of DbNotifyRepository.
/// Sends pg_notify events on the "topolactor_topology_changed" channel.
///
/// Payload shape (per SSOT notify_listen_contract.db_notify):
///   { "table_id": "...", "table_registry_id": "...", "manifest_id": "..." }
/// </summary>
public class NpgsqlDbNotifyRepository : DbNotifyRepository
{
    private const string NotifyChannel = "topolactor_topology_changed";
    private readonly string _connectionString;

    public NpgsqlDbNotifyRepository(
        ILogger<NpgsqlDbNotifyRepository> logger,
        string connectionString)
        : base(logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <inheritdoc/>
    public override async Task NotifyAsync(
        string? tableId,
        string? tableRegistryId,
        Guid? manifestId,
        CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            table_id = tableId,
            table_registry_id = tableRegistryId,
            manifest_id = manifestId?.ToString(),
        });

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT pg_notify(@channel, @payload)";
            cmd.Parameters.AddWithValue("channel", NotifyChannel);
            cmd.Parameters.AddWithValue("payload", payload);
            await cmd.ExecuteNonQueryAsync(ct);

            _logger.LogDebug(
                "DbNotifyRepository: notified channel={Channel} payload={Payload}",
                NotifyChannel, payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "DbNotifyRepository: pg_notify failed (non-blocking). channel={Channel} payload={Payload}",
                NotifyChannel, payload);
        }
    }
}
