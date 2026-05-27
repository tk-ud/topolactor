using Topolactor.Schema;

namespace Topolactor.Repository;

public class CiAttentionGuidanceRepository
{
    public virtual Task<CiAttentionGuidanceFragmentStored> UpsertCurrentAppendHistoryAsync(
        CiAttentionGuidanceFragmentUpsert fragment,
        CancellationToken ct = default)
    {
        var payload = new CiAttentionGuidanceGuidanceEventPayload(
            Guid.NewGuid(),
            fragment.Kind.ToString().ToLowerInvariant(),
            fragment.Status.ToString().ToLowerInvariant(),
            fragment.Severity.ToString().ToLowerInvariant(),
            fragment.TargetKind,
            fragment.TargetKey,
            DateTimeOffset.UtcNow);
        return Task.FromResult(new CiAttentionGuidanceFragmentStored(payload.FragmentId, payload));
    }
}
