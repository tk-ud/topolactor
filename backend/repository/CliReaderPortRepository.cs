using Topolactor.Schema;

namespace Topolactor.Repository;

public interface CliReaderPortRepository
{
    Task<CliReaderPortConfig?> LoadPortAsync(string portKey, CancellationToken ct = default);
    Task<IReadOnlyList<Dictionary<string, object?>>> ReadRowsAsync(AuthorizedCliReaderQuery query, CancellationToken ct = default);
    Task AppendRuntimeEventAsync(CliReaderPortRuntimeEvent runtimeEvent, CancellationToken ct = default);
}
