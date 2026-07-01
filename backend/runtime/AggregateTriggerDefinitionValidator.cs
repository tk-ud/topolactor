using System.Text.RegularExpressions;
using Topolactor.Schema;

namespace Topolactor.Runtime;

public static class AggregateTriggerDefinitionValidator
{
    private static readonly Regex SafeIdentifier = new("^[a-zA-Z_][a-zA-Z0-9_:.\\-]*$", RegexOptions.Compiled);
    private static readonly string[] SqlFragments = ["select ", " where ", " case ", ";", "--", "/*", "*/"];

    public static IReadOnlyList<ValidationError> Validate(
        AggregateTriggerDefinition definition,
        ISet<string> step2EntityIds,
        ISet<string> step25RelationIds)
    {
        var errors = new List<ValidationError>();
        if (!AggregateTriggerVocabulary.TriggerKinds.Contains(definition.TriggerKind)) errors.Add(new("AGGREGATE_TRIGGER_KIND_INVALID", "trigger_kind must be cron, hook, or client."));
        if (string.Equals(definition.SourceDetailKind, "scheduler_event", StringComparison.OrdinalIgnoreCase) || !AggregateTriggerVocabulary.SourceDetailKinds.Contains(definition.SourceDetailKind)) errors.Add(new("AGGREGATE_SOURCE_DETAIL_KIND_INVALID", "source_detail_kind must be canonical and must not be scheduler_event."));
        if (string.Equals(definition.TriggerKind, definition.SourceDetailKind, StringComparison.OrdinalIgnoreCase)) errors.Add(new("AGGREGATE_SOURCE_DETAIL_KIND_NOT_SEPARATED", "source_detail_kind must remain separated from canonical trigger_kind."));
        if (!AggregateTriggerVocabulary.ApprovalPolicies.Contains(definition.ApprovalPolicy)) errors.Add(new("AGGREGATE_APPROVAL_POLICY_INVALID", "approval_policy must be none or required."));
        if (!AggregateTriggerVocabulary.ComparisonOperators.Contains(definition.ThresholdPolicy.ComparisonOperator)) errors.Add(new("AGGREGATE_THRESHOLD_OPERATOR_INVALID", "comparison_operator is not allowed."));
        if (definition.ThresholdPolicy.MinimumTrialCount < 1) errors.Add(new("AGGREGATE_MINIMUM_TRIAL_COUNT_INVALID", "minimum_trial_count must be >= 1."));
        ValidateTarget("AGGREGATE_TARGET_INVALID", definition.AggregateTargetBinding, step2EntityIds, step25RelationIds, errors);
        ValidateTarget("AGGREGATE_MATERIALIZATION_TARGET_INVALID", definition.MaterializationTargetBinding, step2EntityIds, step25RelationIds, errors);
        foreach (var field in definition.ConflictKeyFields.Concat(definition.DeltaMap.Keys).Concat([definition.ThresholdPolicy.RatioNumeratorField, definition.ThresholdPolicy.RatioDenominatorField]))
            ValidateSafeToken("AGGREGATE_RAW_SQL_PROHIBITED", field, errors);
        foreach (var entry in definition.MaterializationPayloadMap)
        {
            ValidateSafeToken("AGGREGATE_RAW_SQL_PROHIBITED", entry.TargetField, errors);
            if (!AggregateTriggerVocabulary.PayloadSourceKinds.Contains(entry.SourceKind)) errors.Add(new("AGGREGATE_PAYLOAD_SOURCE_KIND_INVALID", $"materialization_payload_map source_kind '{entry.SourceKind}' is not allowed."));
            if (entry.SourceField is not null) ValidateSafeToken("AGGREGATE_RAW_SQL_PROHIBITED", entry.SourceField, errors);
        }
        return errors;
    }

    private static void ValidateTarget(string code, AggregateTriggerTargetBinding target, ISet<string> step2, ISet<string> step25, List<ValidationError> errors)
    {
        ValidateSafeToken("AGGREGATE_RAW_SQL_PROHIBITED", target.TargetId, errors);
        var ok = string.Equals(target.TargetKind, "step2_entity", StringComparison.OrdinalIgnoreCase) ? step2.Contains(target.TargetId) :
                 string.Equals(target.TargetKind, "step2_5_relation", StringComparison.OrdinalIgnoreCase) && step25.Contains(target.TargetId);
        if (!ok) errors.Add(new(code, "target must be a saved Step2 logical entity or Step2.5 relation definition."));
    }

    private static void ValidateSafeToken(string code, string value, List<ValidationError> errors)
    {
        var v = value.Trim();
        if (!SafeIdentifier.IsMatch(v) || SqlFragments.Any(f => v.Contains(f, StringComparison.OrdinalIgnoreCase))) errors.Add(new(code, "raw SQL, CASE, WHERE, or arbitrary table-name expressions are prohibited."));
    }
}

public static class AggregateTriggerConditionEvaluator
{
    public static bool Evaluate(AggregateTriggerThresholdPolicy policy, AggregateTriggerCurrentRow row)
    {
        row.Counters.TryGetValue(policy.RatioNumeratorField, out var numerator);
        row.Counters.TryGetValue(policy.RatioDenominatorField, out var denominator);
        if (denominator < policy.MinimumTrialCount || denominator == 0) return false;
        var ratio = numerator / denominator;
        return policy.ComparisonOperator switch { ">=" => ratio >= policy.TargetRatio, ">" => ratio > policy.TargetRatio, "=" => ratio == policy.TargetRatio, "<=" => ratio <= policy.TargetRatio, "<" => ratio < policy.TargetRatio, _ => false };
    }
}
