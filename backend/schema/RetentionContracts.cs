using System.Text.Json.Serialization;

namespace Topolactor.Schema;

/// <summary>
/// Resolved retention policy for context_event cleanup.
/// Loaded from function_parameters (function_name='context_event_retention', parameter_key='retention_policy').
/// Values come entirely from the topology data store — no production defaults in runtime code.
/// Policy-missing or policy-malformed → explicit status, no silent fallback.
///
/// HotDays: events within this window (created_at >= NOW() - hot_days) are never deleted or archived,
/// even if they are older than cold_days. Acts as a safety floor. If null, no hot-window protection.
/// If present, must be a positive integer — 0 is invalid (MalformedPolicy).
/// </summary>
public record ContextEventRetentionPolicy(
    [property: JsonPropertyName("cold_days")]
    int ColdDays,

    [property: JsonPropertyName("archive_strategy")]
    string ArchiveStrategy,

    [property: JsonPropertyName("batch_size")]
    int BatchSize,

    [property: JsonPropertyName("enabled")]
    bool Enabled,

    [property: JsonPropertyName("schedule_interval_hours")]
    int ScheduleIntervalHours,

    [property: JsonPropertyName("hot_days")]
    int? HotDays
);

/// <summary>
/// Explicit status of a single retention execution.
/// Disabled is always logged — never a silent skip.
/// </summary>
public enum RetentionExecutionStatus
{
    Ok,
    Disabled,
    MissingPolicy,
    MalformedPolicy,
}

/// <summary>
/// Result returned by LogRetentionRuntime after each execution.
/// ScheduleIntervalHours is 0 when the policy could not be loaded (scheduler uses bootstrap interval).
/// </summary>
public record RetentionRunResult(
    RetentionExecutionStatus Status,
    string? StatusDetail,
    int RowsAffected,
    DateTimeOffset ExecutedAt,
    int ScheduleIntervalHours
);
