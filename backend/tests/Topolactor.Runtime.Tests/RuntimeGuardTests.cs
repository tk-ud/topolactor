using Topolactor.Guard;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class RuntimeGuardTests
{
    [Fact]
    public void Validate_MissingRequiredFields_ReturnsExplicitValidationErrors()
    {
        var guard = new RuntimeGuard();
        var vector = new OperationVector(
            Target: null,
            Layer: "entity",
            Action: null,
            AttractorKey: null,
            UserRole: null,
            Payload: null,
            RequestedProjection: null);

        var errors = guard.Validate(vector);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Code == "MISSING_TARGET");
        Assert.Contains(errors, e => e.Code == "MISSING_ACTION");
        Assert.Contains(errors, e => e.Code == "MISSING_ATTRACTOR_KEY");
    }
}
