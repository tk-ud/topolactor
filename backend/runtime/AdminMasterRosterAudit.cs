using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// One changed business field for the generic JSONB audit envelope (logs.diff.changed_fields_json).
/// Before/After carry the actual field values (not just the field name) so the envelope satisfies
/// admin-master-roster-management-ssot.yaml logs_diff_admin_projection's changed_fields contract.
/// </summary>
public readonly record struct AuditChangedField(string Name, object? Before, object? After);

public static class AdminMasterRosterAudit
{
    public const string SourceSetId = "admin_master_roster";
    public const string BasisWindow = "admin_operation";
    private const int ChangedFieldsEnvelopeSchemaVersion = 1;

    public static async Task AppendAsync(
        SqlAttentionLogsRepository? logsRepository,
        string? actor,
        string targetTable,
        string targetId,
        string operation,
        object? before,
        object? after,
        IReadOnlyList<AuditChangedField>? changedFields,
        CancellationToken ct = default)
    {
        if (logsRepository is null) return;

        var changedFieldsJson = JsonSerializer.Serialize(new
        {
            schemaVersion = ChangedFieldsEnvelopeSchemaVersion,
            changedFields = (changedFields ?? Array.Empty<AuditChangedField>())
                .Select(f => new
                {
                    name = f.Name,
                    type = InferJsonType(f.After ?? f.Before),
                    before = f.Before,
                    after = f.After,
                }),
        });

        await logsRepository.AppendLogsDiffAsync(new LogsDiffAppendRequest(
            SourceSetId,
            BasisWindow,
            targetTable,
            targetTable,
            targetId,
            operation,
            before is null ? "{}" : JsonSerializer.Serialize(before),
            after is null ? "{}" : JsonSerializer.Serialize(after),
            DateTimeOffset.UtcNow,
            actor,
            "required",
            changedFieldsJson), ct);
    }

    // Type is derived from the field's own (After, falling back to Before) value, not declared by
    // the caller -- a single inference point keeps this consistent across all 10 call sites.
    private static string InferJsonType(object? value) => value switch
    {
        null => "null",
        bool => "boolean",
        sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal => "number",
        DateTime or DateTimeOffset => "string",
        Guid => "string",
        string => "string",
        _ => "object",
    };
}
