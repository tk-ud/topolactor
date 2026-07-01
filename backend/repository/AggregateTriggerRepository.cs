using Topolactor.Schema;

namespace Topolactor.Repository;

public record AggregateTriggerAppendResult(bool Appended);
public record AggregateTriggerMaterializationResult(bool Created, Guid MaterializationId);

public interface AggregateTriggerRepository
{
    Task<AggregateTriggerAppendResult> AppendEventEvidenceAsync(AggregateTriggerEventEvidence evidence, CancellationToken ct = default);
    Task<AggregateTriggerCurrentRow> AtomicUpsertCurrentAsync(Guid definitionId, string conflictKey, IReadOnlyDictionary<string, decimal> deltaMap, CancellationToken ct = default);
    Task<AggregateTriggerMaterializationResult> TryMaterializeAsync(AggregateTriggerDefinition definition, AggregateTriggerCurrentRow currentRow, string eventId, CancellationToken ct = default);
}

public class InMemoryAggregateTriggerRepository : AggregateTriggerRepository
{
    private readonly object _gate = new();
    private readonly HashSet<(Guid,string)> _events = [];
    private readonly Dictionary<(Guid,string), AggregateTriggerCurrentRow> _currents = [];
    private readonly Dictionary<(Guid,string), Guid> _materializations = [];

    public Task<AggregateTriggerAppendResult> AppendEventEvidenceAsync(AggregateTriggerEventEvidence evidence, CancellationToken ct = default)
    {
        lock (_gate) return Task.FromResult(new AggregateTriggerAppendResult(_events.Add((evidence.DefinitionId, evidence.EventId))));
    }

    public Task<AggregateTriggerCurrentRow> AtomicUpsertCurrentAsync(Guid definitionId, string conflictKey, IReadOnlyDictionary<string, decimal> deltaMap, CancellationToken ct = default)
    {
        lock (_gate)
        {
            _currents.TryGetValue((definitionId, conflictKey), out var existing);
            var counters = new Dictionary<string, decimal>(existing?.Counters ?? new Dictionary<string, decimal>(), StringComparer.OrdinalIgnoreCase);
            foreach (var (k,v) in deltaMap) counters[k] = counters.GetValueOrDefault(k) + v;
            var row = new AggregateTriggerCurrentRow(definitionId, conflictKey, counters, DateTimeOffset.UtcNow);
            _currents[(definitionId, conflictKey)] = row;
            return Task.FromResult(row);
        }
    }

    public Task<AggregateTriggerMaterializationResult> TryMaterializeAsync(AggregateTriggerDefinition definition, AggregateTriggerCurrentRow currentRow, string eventId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var key = (definition.DefinitionId, currentRow.ConflictKey);
            if (_materializations.TryGetValue(key, out var existing)) return Task.FromResult(new AggregateTriggerMaterializationResult(false, existing));
            var id = Guid.NewGuid();
            _materializations[key] = id;
            return Task.FromResult(new AggregateTriggerMaterializationResult(true, id));
        }
    }
}
