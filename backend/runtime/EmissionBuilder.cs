using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Builds the validated Emission output from a fully resolved RuntimeWorkingShape.
/// The Emission is the final validated output of the pipeline — it contains only
/// resolved identifiers and data, never internal working state.
/// </summary>
public class EmissionBuilder
{
    /// <summary>
    /// Builds an Emission from the resolved RuntimeWorkingShape.
    /// Validation errors from the shape are forwarded to the Emission.
    /// </summary>
    public Emission Build(RuntimeWorkingShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);

        var errors = shape.Errors ?? [];

        return new Emission(
            StructureMapId: shape.StructureMapId,
            PackageId: shape.PackageId,
            SchemaId: shape.SchemaId,
            ComponentIds: shape.ComponentIds,
            Data: shape.ResolvedData,
            Errors: errors,
            JumpEvents: shape.JumpEvents,
            ContextRouteRecommendation: shape.ContextRouteRecommendation,
            LayoutId: shape.LayoutId
        );
    }
}
