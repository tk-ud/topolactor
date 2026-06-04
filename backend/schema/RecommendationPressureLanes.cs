namespace Topolactor.Schema;

/// <summary>
/// SSOT-fixed recommendation pressure lane vocabulary.
/// Canonical definitions: docs/design/context-route-recommendation.yaml,
/// docs/design/sql-attention-logs-ssot.yaml
/// </summary>
public static class RecommendationPressureLanes
{
    public const string UiPressure = "ui_pressure";
    public const string StatePressure = "state_pressure";
    public const string SqlAttentionProjection = "sql_attention_projection";
}
