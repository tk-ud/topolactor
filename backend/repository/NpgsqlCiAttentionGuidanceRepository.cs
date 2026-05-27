using Npgsql;
using Topolactor.Schema;

namespace Topolactor.Repository;

public sealed class NpgsqlCiAttentionGuidanceRepository : CiAttentionGuidanceRepository
{
    private readonly string _connectionString;

    public NpgsqlCiAttentionGuidanceRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public override async Task<CiAttentionGuidanceFragmentStored> UpsertCurrentAppendHistoryAsync(CiAttentionGuidanceFragmentUpsert f, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var sql = """
WITH upserted AS (
INSERT INTO logs.ci_attention_guidance_current (
source_kind,source_id,source_table_or_surface,target_kind,target_key,diff_or_event_or_entity_id,scope,route,authoring_surface,kind,status,severity,blocks_promotion,message,actionable_guidance,evidence_json,checked_at,updated_at
) VALUES (
@source_kind,@source_id,@source_table_or_surface,@target_kind,@target_key,@diff_or_event_or_entity_id,@scope,@route,@authoring_surface,@kind,@status,@severity,@blocks_promotion,@message,@actionable_guidance,@evidence_json::jsonb,@checked_at,now()
)
ON CONFLICT (source_kind, source_id, target_kind, target_key, kind) DO UPDATE SET
source_table_or_surface=EXCLUDED.source_table_or_surface,diff_or_event_or_entity_id=EXCLUDED.diff_or_event_or_entity_id,scope=EXCLUDED.scope,route=EXCLUDED.route,authoring_surface=EXCLUDED.authoring_surface,status=EXCLUDED.status,severity=EXCLUDED.severity,blocks_promotion=EXCLUDED.blocks_promotion,message=EXCLUDED.message,actionable_guidance=EXCLUDED.actionable_guidance,evidence_json=EXCLUDED.evidence_json,checked_at=EXCLUDED.checked_at,updated_at=now()
RETURNING *
)
INSERT INTO logs.ci_attention_guidance_history (
fragment_id,source_kind,source_id,source_table_or_surface,target_kind,target_key,diff_or_event_or_entity_id,scope,route,authoring_surface,kind,status,severity,blocks_promotion,message,actionable_guidance,evidence_json,checked_at,resolved_at,dismissed_at
)
SELECT fragment_id,source_kind,source_id,source_table_or_surface,target_kind,target_key,diff_or_event_or_entity_id,scope,route,authoring_surface,kind,status,severity,blocks_promotion,message,actionable_guidance,evidence_json,checked_at,resolved_at,dismissed_at FROM upserted
RETURNING fragment_id,kind,status,severity,target_kind,target_key,snapshot_at;
""";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("source_kind", ToDb(f.SourceKind));
        cmd.Parameters.AddWithValue("source_id", f.SourceId);
        cmd.Parameters.AddWithValue("source_table_or_surface", f.SourceTableOrSurface);
        cmd.Parameters.AddWithValue("target_kind", f.TargetKind);
        cmd.Parameters.AddWithValue("target_key", f.TargetKey);
        cmd.Parameters.AddWithValue("diff_or_event_or_entity_id", (object?)f.DiffOrEventOrEntityId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("scope", f.Scope);
        cmd.Parameters.AddWithValue("route", (object?)f.Route ?? DBNull.Value);
        cmd.Parameters.AddWithValue("authoring_surface", f.AuthoringSurface);
        cmd.Parameters.AddWithValue("kind", ToDb(f.Kind));
        cmd.Parameters.AddWithValue("status", ToDb(f.Status));
        cmd.Parameters.AddWithValue("severity", ToDb(f.Severity));
        cmd.Parameters.AddWithValue("blocks_promotion", f.BlocksPromotion);
        cmd.Parameters.AddWithValue("message", f.Message);
        cmd.Parameters.AddWithValue("actionable_guidance", f.ActionableGuidance);
        cmd.Parameters.AddWithValue("evidence_json", f.EvidenceJson.GetRawText());
        cmd.Parameters.AddWithValue("checked_at", (object?)f.CheckedAt ?? DBNull.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        var fragmentId = r.GetGuid(0);
        var payload = new CiAttentionGuidanceGuidanceEventPayload(fragmentId, r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetFieldValue<DateTimeOffset>(6));
        await tx.CommitAsync(ct);
        return new CiAttentionGuidanceFragmentStored(fragmentId, payload);
    }

    public override async Task<IReadOnlyList<CiAttentionBlockingFragment>> GetActiveBlockingFragmentsAsync(
        string? targetKind = null,
        string? targetKey = null,
        string? scope = null,
        string? authoringSurface = null,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var sql = """
SELECT fragment_id, kind, target_kind, target_key, message, actionable_guidance
FROM logs.ci_attention_guidance_current
WHERE status = 'active'
  AND blocks_promotion = true
  AND (@target_kind::text IS NULL OR target_kind = @target_kind)
  AND (@target_key::text IS NULL OR target_key = @target_key)
  AND (@scope::text IS NULL OR scope = @scope)
  AND (@authoring_surface::text IS NULL OR authoring_surface = @authoring_surface)
""";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("target_kind", (object?)targetKind ?? DBNull.Value);
        cmd.Parameters.AddWithValue("target_key", (object?)targetKey ?? DBNull.Value);
        cmd.Parameters.AddWithValue("scope", (object?)scope ?? DBNull.Value);
        cmd.Parameters.AddWithValue("authoring_surface", (object?)authoringSurface ?? DBNull.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var result = new List<CiAttentionBlockingFragment>();
        while (await r.ReadAsync(ct))
        {
            result.Add(new CiAttentionBlockingFragment(
                r.GetGuid(0),
                r.GetString(1),
                r.GetString(2),
                r.GetString(3),
                r.GetString(4),
                r.GetString(5)));
        }
        return result;
    }

    private static string ToDb(CiAttentionSourceKind v) => v switch { CiAttentionSourceKind.EntityDiff=>"entity_diff",CiAttentionSourceKind.DraftDiff=>"draft_diff",CiAttentionSourceKind.RuntimeEvent=>"runtime_event",_=>"manual_check"};
    private static string ToDb(CiAttentionGuidanceKind v) => v switch { CiAttentionGuidanceKind.MissingInput=>"missing_input",CiAttentionGuidanceKind.ValidCandidate=>"valid_candidate",CiAttentionGuidanceKind.StructuralViolation=>"structural_violation",_=>"break_boundary"};
    private static string ToDb(CiAttentionGuidanceStatus v) => v switch { CiAttentionGuidanceStatus.NotChecked=>"not_checked",CiAttentionGuidanceStatus.Active=>"active",CiAttentionGuidanceStatus.Resolved=>"resolved",_=>"dismissed"};
    private static string ToDb(CiAttentionGuidanceSeverity v) => v switch { CiAttentionGuidanceSeverity.Info=>"info",CiAttentionGuidanceSeverity.Warning=>"warning",_=>"error"};
}
