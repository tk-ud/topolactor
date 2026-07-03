using System.Text.RegularExpressions;
using Topolactor.Schema;

namespace Topolactor.Runtime;

public static class AggregateTriggerDefinitionValidator
{
    private static readonly Regex SafeIdentifier = new("^[a-zA-Z_][a-zA-Z0-9_:.\\-]*$", RegexOptions.Compiled);
    private static readonly string[] SqlFragments = ["select ", " where ", " case ", ";", "--", "/*", "*/"];

    public static IReadOnlyList<ValidationError> Validate(AggregateTriggerDefinition definition, ISet<string> step2LogicalEntityDefinitionIds, ISet<string> step25RelationDefinitionIds)
    {
        var errors = new List<ValidationError>();
        if (!AggregateTriggerVocabulary.CanonicalTriggerKinds.Contains(definition.TriggerSource.CanonicalTriggerKind)) errors.Add(new("AGGREGATE_TRIGGER_KIND_INVALID", "trigger_source.canonical_trigger_kind must be cron, hook, or client."));
        if (string.Equals(definition.TriggerSource.TriggerSourceDetailKind, "scheduler_event", StringComparison.OrdinalIgnoreCase) || !AggregateTriggerVocabulary.TriggerSourceDetailKinds.Contains(definition.TriggerSource.TriggerSourceDetailKind)) errors.Add(new("AGGREGATE_TRIGGER_SOURCE_DETAIL_KIND_INVALID", "trigger_source.trigger_source_detail_kind must be SSOT canonical and must not be scheduler_event."));
        if (string.Equals(definition.TriggerSource.CanonicalTriggerKind, definition.TriggerSource.TriggerSourceDetailKind, StringComparison.OrdinalIgnoreCase)) errors.Add(new("AGGREGATE_TRIGGER_SOURCE_DETAIL_KIND_NOT_SEPARATED", "trigger_source_detail_kind must remain separated from canonical_trigger_kind."));
        if (!AggregateTriggerVocabulary.ExecutionScopeAllowedValues.Contains(definition.ExecutionScope)) errors.Add(new("AGGREGATE_EXECUTION_SCOPE_INVALID", "execution_scope is not SSOT allowed."));
        if (!AggregateTriggerVocabulary.TransactionBoundaryAllowedValues.Contains(definition.TransactionBoundary)) errors.Add(new("AGGREGATE_TRANSACTION_BOUNDARY_INVALID", "transaction_boundary is not SSOT allowed."));
        if (!AggregateTriggerVocabulary.ApprovalPolicyAllowedValues.Contains(definition.ApprovalPolicy)) errors.Add(new("AGGREGATE_APPROVAL_POLICY_INVALID", "approval_policy is not SSOT allowed."));
        if (!AggregateTriggerVocabulary.ComparisonOperatorAllowedValues.Contains(definition.ThresholdPolicy.ComparisonOperator)) errors.Add(new("AGGREGATE_THRESHOLD_OPERATOR_INVALID", "comparison_operator is not SSOT allowed."));
        if (definition.ThresholdPolicy.MinimumTrialCount < 1) errors.Add(new("AGGREGATE_MINIMUM_TRIAL_COUNT_INVALID", "minimum_trial_count must be >= 1."));
        if (definition.ThresholdPolicy.TargetRatio is < 0m or > 1m) errors.Add(new("AGGREGATE_TARGET_RATIO_INVALID", "target_ratio must be 0.0 to 1.0 unless explicitly extended by SSOT."));
        ValidateTarget("AGGREGATE_TARGET_INVALID", definition.AggregateTargetBinding, step2LogicalEntityDefinitionIds, step25RelationDefinitionIds, errors);
        ValidateTarget("AGGREGATE_MATERIALIZATION_TARGET_INVALID", definition.MaterializationTargetBinding, step2LogicalEntityDefinitionIds, step25RelationDefinitionIds, errors);
        foreach (var field in definition.ConflictKeyFields.Concat(definition.DeltaMap.Keys).Concat([definition.ThresholdPolicy.RatioNumeratorField, definition.ThresholdPolicy.RatioDenominatorField, definition.ProcessingFunctionScope.FunctionId, definition.ProcessingFunctionScope.OperationDefinitionId, definition.ProcessingFunctionScope.AcceptedEventSchemaRef, definition.ProcessingFunctionScope.MaterializationPolicyRef]))
            ValidateSafeToken("AGGREGATE_RAW_SQL_PROHIBITED", field, errors);
        foreach (var source in definition.ProcessingFunctionScope.AllowedSourceKinds) ValidateSafeToken("AGGREGATE_RAW_SQL_PROHIBITED", source, errors);
        foreach (var entry in definition.MaterializationPayloadMap)
        {
            ValidateSafeToken("AGGREGATE_RAW_SQL_PROHIBITED", entry.TargetField, errors);
            if (!AggregateTriggerVocabulary.MaterializationPayloadMapAllowedSources.Contains(entry.Source)) errors.Add(new("AGGREGATE_PAYLOAD_SOURCE_INVALID", $"materialization_payload_map source '{entry.Source}' is not SSOT allowed."));
            if (entry.SourceField is not null) ValidateSafeToken("AGGREGATE_RAW_SQL_PROHIBITED", entry.SourceField, errors);
        }
        return errors;
    }

    private static void ValidateTarget(string code, AggregateTriggerTargetBinding target, ISet<string> step2, ISet<string> step25, List<ValidationError> errors)
    {
        ValidateSafeToken("AGGREGATE_RAW_SQL_PROHIBITED", target.TargetId, errors);
        var ok = string.Equals(target.TargetSource, "step2_logical_entity_definition", StringComparison.OrdinalIgnoreCase) ? step2.Contains(target.TargetId) :
                 string.Equals(target.TargetSource, "step2_5_relation_definition", StringComparison.OrdinalIgnoreCase) && step25.Contains(target.TargetId);
        if (!ok) errors.Add(new(code, "target must be a saved Step2 logical entity definition or Step2.5 relation definition."));
    }

    private static void ValidateSafeToken(string code, string value, List<ValidationError> errors)
    {
        var v = value.Trim();
        if (!SafeIdentifier.IsMatch(v) || SqlFragments.Any(f => v.Contains(f, StringComparison.OrdinalIgnoreCase))) errors.Add(new(code, "raw SQL, CASE, WHERE, or arbitrary table-name expressions are prohibited."));
    }

    /// <summary>
    /// processing_function_scope.function_id authority check against the existing
    /// topology.abstract_function_manifests registry (the same registry execute_abstract_function
    /// resolves against). Safe-identifier validation in <see cref="Validate"/> is a syntactic guard
    /// only; this is the backend authority check and is fail-close on missing/inactive/wrong-lane/
    /// scope-mismatch. Mirrors the manifest.AuthorityScope == context.AuthorityScope check
    /// AbstractFunctionExecutor already enforces for execute_abstract_function.
    /// </summary>
    public static async Task<ValidationError?> ValidateProcessingFunctionAuthorityAsync(
        AggregateTriggerDefinition definition,
        IAbstractFunctionManifestRepository abstractFunctionManifestRepository,
        CancellationToken ct = default)
    {
        var functionId = definition.ProcessingFunctionScope.FunctionId;
        var manifest = await abstractFunctionManifestRepository.LoadAsync(functionId, ct);
        if (manifest is null)
            return new("AGGREGATE_PROCESSING_FUNCTION_NOT_FOUND", $"processing_function_scope.function_id '{functionId}' is not a registered abstract function.");
        if (!manifest.Active)
            return new("AGGREGATE_PROCESSING_FUNCTION_INACTIVE", $"processing_function_scope.function_id '{functionId}' is registered but inactive.");
        if (!string.Equals(manifest.RuntimeLane, AggregateTriggerVocabulary.RuntimeDestination, StringComparison.Ordinal))
            return new("AGGREGATE_PROCESSING_FUNCTION_RUNTIME_LANE_INVALID", $"processing_function_scope.function_id '{functionId}' runtime_lane must be {AggregateTriggerVocabulary.RuntimeDestination}.");
        if (!string.Equals(manifest.AuthorityScope, definition.ProcessingFunctionScope.OperationDefinitionId, StringComparison.Ordinal))
            return new("AGGREGATE_PROCESSING_FUNCTION_AUTHORITY_SCOPE_MISMATCH", $"processing_function_scope.function_id '{functionId}' authority_scope does not match operation_definition_id '{definition.ProcessingFunctionScope.OperationDefinitionId}'.");
        return null;
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
        return policy.ComparisonOperator switch { ">=" => ratio >= policy.TargetRatio, ">" => ratio > policy.TargetRatio, "=" => ratio == policy.TargetRatio, "!=" => ratio != policy.TargetRatio, "<=" => ratio <= policy.TargetRatio, "<" => ratio < policy.TargetRatio, _ => false };
    }
}
