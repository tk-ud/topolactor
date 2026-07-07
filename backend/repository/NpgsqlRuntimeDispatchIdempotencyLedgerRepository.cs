using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Topolactor.Runtime;

namespace Topolactor.Repository;

public sealed class NpgsqlRuntimeDispatchIdempotencyLedgerRepository : IRuntimeDispatchIdempotencyLedger
{
    private readonly string _connectionString;

    public NpgsqlRuntimeDispatchIdempotencyLedgerRepository(string connectionString)
        => _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public async Task<RuntimeDispatchIdempotencyClaimResult> ClaimAsync(
        string idempotencyKey, string dispatchLane, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT claim_status, result_json FROM topology.rt_claim_dispatch_idempotency_key(@key, @lane)";
        cmd.Parameters.AddWithValue("key", idempotencyKey);
        cmd.Parameters.AddWithValue("lane", dispatchLane);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException("RUNTIME_DISPATCH_IDEMPOTENCY_CLAIM_NO_ROW");

        var status = reader.GetString(0);
        JsonElement? previousResult = reader.IsDBNull(1)
            ? null
            : JsonDocument.Parse(reader.GetString(1)).RootElement.Clone();

        var claimStatus = status switch
        {
            "claimed" => RuntimeDispatchIdempotencyClaimStatus.Claimed,
            "already_completed" => RuntimeDispatchIdempotencyClaimStatus.AlreadyCompleted,
            "in_flight" => RuntimeDispatchIdempotencyClaimStatus.InFlight,
            _ => throw new InvalidOperationException(
                $"RUNTIME_DISPATCH_IDEMPOTENCY_CLAIM_STATUS_UNKNOWN: {status}"),
        };
        return new RuntimeDispatchIdempotencyClaimResult(claimStatus, previousResult);
    }

    public async Task CompleteAsync(string idempotencyKey, JsonElement resultJson, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT topology.rt_complete_dispatch_idempotency_key(@key, @result)";
        cmd.Parameters.AddWithValue("key", idempotencyKey);
        cmd.Parameters.AddWithValue("result", NpgsqlDbType.Jsonb, resultJson.GetRawText());
        await cmd.ExecuteScalarAsync(ct);
    }

    public async Task FailAsync(string idempotencyKey, string failureCode, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT topology.rt_fail_dispatch_idempotency_key(@key, @code)";
        cmd.Parameters.AddWithValue("key", idempotencyKey);
        cmd.Parameters.AddWithValue("code", failureCode);
        await cmd.ExecuteScalarAsync(ct);
    }
}
