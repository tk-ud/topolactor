using System.Text.Json;

namespace Topolactor.Schema;

public enum CiAttentionSourceKind { EntityDiff, DraftDiff, RuntimeEvent, ManualCheck }
public enum CiAttentionGuidanceKind { MissingInput, ValidCandidate, StructuralViolation, BreakBoundary }
public enum CiAttentionGuidanceStatus { NotChecked, Active, Resolved, Dismissed }
public enum CiAttentionGuidanceSeverity { Info, Warning, Error }

public record CiAttentionGuidanceFragmentUpsert(
    CiAttentionSourceKind SourceKind,
    string SourceId,
    string SourceTableOrSurface,
    string TargetKind,
    string TargetKey,
    string? DiffOrEventOrEntityId,
    string Scope,
    string? Route,
    string AuthoringSurface,
    CiAttentionGuidanceKind Kind,
    CiAttentionGuidanceStatus Status,
    CiAttentionGuidanceSeverity Severity,
    bool BlocksPromotion,
    string Message,
    string ActionableGuidance,
    JsonElement EvidenceJson,
    DateTimeOffset? CheckedAt
);

public record CiAttentionGuidanceFragmentStored(
    Guid FragmentId,
    CiAttentionGuidanceGuidanceEventPayload EventPayload
);

public record CiAttentionGuidanceGuidanceEventPayload(
    Guid FragmentId,
    string Kind,
    string Status,
    string Severity,
    string TargetKind,
    string TargetKey,
    DateTimeOffset UpdatedAt
);

public record CiAttentionBlockingFragment(
    Guid FragmentId,
    string Kind,
    string TargetKind,
    string TargetKey,
    string Message,
    string ActionableGuidance
);
