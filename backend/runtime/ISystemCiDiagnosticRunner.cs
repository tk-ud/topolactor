using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Read-only callable surface for System Operation CI diagnostics.
/// Consumer surfaces: AdminRuntime and tests.
/// </summary>
public interface ISystemCiDiagnosticRunner
{
    IReadOnlyList<SystemCiTargetDto> ListTargets();
    Task<SystemCiDiagnosticResult> InspectAsync(string target, CancellationToken ct = default);
}
