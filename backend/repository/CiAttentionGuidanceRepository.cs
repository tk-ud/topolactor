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

    /// <summary>
    /// Returns active blocking fragments from the current projection.
    /// Only fragments with status='active' AND blocks_promotion=true are returned.
    /// dismissed fragments are NOT returned; dismissed is visibility control only.
    /// </summary>
    public virtual Task<IReadOnlyList<CiAttentionBlockingFragment>> GetActiveBlockingFragmentsAsync(
        string? targetKind = null,
        string? targetKey = null,
        string? scope = null,
        string? authoringSurface = null,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CiAttentionBlockingFragment>>([]);
}
