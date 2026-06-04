using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

public static class AdminMasterRosterAudit
{
    public const string SourceSetId = "admin_master_roster";
    public const string BasisWindow = "admin_operation";

    public static async Task AppendAsync(
        SqlAttentionLogsRepository? logsRepository,
        string? actor,
        string targetTable,
        string targetId,
        string operation,
        object? before,
        object? after,
        IReadOnlyList<string>? changedFields,
        CancellationToken ct = default)
    {
        if (logsRepository is null) return;

        var envelope = new
        {
            actor,
            target_table = targetTable,
            target_id = targetId,
            operation,
            before,
            after,
            changed_fields = changedFields ?? Array.Empty<string>(),
            timestamp = DateTimeOffset.UtcNow,
        };

        var json = JsonSerializer.Serialize(envelope);
        await logsRepository.AppendLogsDiffAsync(new LogsDiffAppendRequest(
            SourceSetId,
            BasisWindow,
            targetTable,
            targetTable,
            targetId,
            operation,
            before is null ? "{}" : JsonSerializer.Serialize(before),
            after is null ? "{}" : json,
            DateTimeOffset.UtcNow,
            actor,
            "required"), ct);
    }
}
