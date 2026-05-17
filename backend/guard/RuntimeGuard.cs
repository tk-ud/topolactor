using Topolactor.Schema;

namespace Topolactor.Guard;

/// <summary>
/// Validates an OperationVector before the runtime proceeds with resolution.
/// Returns explicit validation errors for any missing or malformed fields.
/// There is no silent fallback: callers must check the returned error list.
/// </summary>
public class RuntimeGuard
{
    /// <summary>
    /// Validates the resolved OperationVector.
    /// Returns a non-empty list if Target, Layer, Action, or AttractorKey is missing.
    /// An empty list means the vector is valid and execution may proceed.
    /// </summary>
    public IReadOnlyList<ValidationError> Validate(OperationVector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(vector.Target))
            errors.Add(new ValidationError("MISSING_TARGET", "OperationVector.Target is required."));

        if (string.IsNullOrWhiteSpace(vector.Layer))
            errors.Add(new ValidationError("MISSING_LAYER", "OperationVector.Layer is required."));

        if (string.IsNullOrWhiteSpace(vector.Action))
            errors.Add(new ValidationError("MISSING_ACTION", "OperationVector.Action is required."));

        if (string.IsNullOrWhiteSpace(vector.AttractorKey))
            errors.Add(new ValidationError("MISSING_ATTRACTOR_KEY",
                "OperationVector.AttractorKey could not be derived. Target, Layer, and Action must all be present."));

        return errors;
    }
}
