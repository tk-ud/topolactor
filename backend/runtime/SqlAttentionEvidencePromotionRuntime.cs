using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Explicit append-only lifecycle boundary for SQL Attention phaseAT evidence.
/// This runtime never mutates canonical topology, manifests, hub relations, or registry rows.
/// </summary>
public class SqlAttentionEvidencePromotionRuntime
{
    private readonly SqlAttentionLogsRepository _repository;

    public SqlAttentionEvidencePromotionRuntime(SqlAttentionLogsRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<AttentionLifecycleResult> ExecuteAsync(AttentionLifecycleCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.SourceAttentionId == Guid.Empty)
            return Fail("INVALID_SOURCE_ATTENTION_ID", "SourceAttentionId must not be empty.");
        if (string.IsNullOrWhiteSpace(command.ActorOrSource) || string.IsNullOrWhiteSpace(command.CommandId))
            return Fail("EXPLICIT_COMMAND_REQUIRED", "ActorOrSource and CommandId are required for lifecycle commands.");

        var source = await _repository.LoadAttentionLifecycleSourceAsync(command.SourceAttentionId, ct);
        if (source is null) return Fail("ATTENTION_EVIDENCE_NOT_FOUND", "Selected logs.attention evidence row was not found.");

        var transition = ResolveTransition(source.EvidenceKind, command.Operation);
        if (transition is null)
            return Fail("INVALID_EVIDENCE_TRANSITION", $"Operation {command.Operation} is not allowed from evidence_kind={source.EvidenceKind}.");

        var evidenceJson = JsonSerializer.Serialize(new
        {
            lifecycle_operation = command.Operation.ToString(),
            explicit_command = true,
            actor_or_source = command.ActorOrSource,
            command_id = command.CommandId,
            source_attention_id = source.AttentionId,
            generation_line_id = source.GenerationLineId,
            no_automatic_topology_manifest_hub_relation_mutation = true
        });
        var id = await _repository.AppendAttentionLifecycleEvidenceAsync(
            new AttentionLifecycleAppendRequest(source, transition.Value.EvidenceKind, transition.Value.PhaseStatus,
                transition.Value.PromotionStatus, command.ActorOrSource, command.CommandId, evidenceJson), ct);
        return new AttentionLifecycleResult(true, id, source.GenerationLineId, transition.Value.PromotionStatus,
            $"Append-only {transition.Value.EvidenceKind} evidence row created. Canonical mutation was not performed.");
    }

    private static (string EvidenceKind, string PhaseStatus, string PromotionStatus)? ResolveTransition(
        string sourceEvidenceKind,
        AttentionLifecycleOperation operation) => operation switch
    {
        AttentionLifecycleOperation.CreateDraft when sourceEvidenceKind == "phaseAT" => ("draft_projection", "draft_projection", "draft"),
        AttentionLifecycleOperation.AdoptDraft when sourceEvidenceKind == "draft_projection" => ("adoption_result", "adoption_result", "adopted"),
        AttentionLifecycleOperation.Reject when sourceEvidenceKind is "phaseAT" or "draft_projection" => ("rejection_result", "rejection_result", "rejected"),
        _ => null
    };

    private static AttentionLifecycleResult Fail(string status, string detail) => new(false, null, null, status, detail);
}
