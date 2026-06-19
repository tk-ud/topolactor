using Topolactor.Runtime;

namespace Topolactor.Repository;

/// <summary>
/// Compatibility placeholder for the execute_db_function operation_key.
/// All file-storage DB operations have been migrated to execute_abstract_function manifest path.
/// No per-function switch or payload-derived table authority exists here.
/// </summary>
public sealed class NpgsqlExternalPortDbFunctionRepository : IExternalPortDbFunctionRepository
{
    public Task ExecuteAsync(
        string functionName,
        IReadOnlyDictionary<string, string> stepConfig,
        ExternalPortExecutionContext context,
        CancellationToken ct = default) =>
        throw new InvalidOperationException($"EXTERNAL_PORT_DB_FUNCTION_UNKNOWN: {functionName}");
}
