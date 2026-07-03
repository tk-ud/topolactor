using Topolactor.Schema;

namespace Topolactor.Repository;

public record AggregateTriggerAppendResult(bool Appended);
public record AggregateTriggerMaterializationResult(bool Created, Guid MaterializationId);
public record AggregateTriggerUpsertAndMaterializeResult(
    AggregateTriggerCurrentRow Current,
    AggregateTriggerMaterializationDecision Decision,
    AggregateTriggerMaterializationResult? Materialization);

public interface AggregateTriggerRepository
{
    Task<AggregateTriggerAppendResult> AppendEventEvidenceAsync(AggregateTriggerEventEvidence evidence, CancellationToken ct = default);
    Task<AggregateTriggerCurrentRow> AtomicUpsertCurrentAsync(Guid definitionId, string conflictKey, IReadOnlyDictionary<string, decimal> deltaMap, CancellationToken ct = default);
    Task<AggregateTriggerMaterializationResult> TryMaterializeAsync(AggregateTriggerDefinition definition, AggregateTriggerCurrentRow currentRow, string eventId, CancellationToken ct = default);

    /// <summary>
    /// Executes the aggregate current upsert and the conditional materialization evidence append
    /// in a single backend transaction, per aggregate_trigger_substrate_contract.transaction_boundary
    /// atomicity_rule. <paramref name="decide"/> is evaluated in-process against the freshly
    /// upserted row while the transaction is still open, so the materialization decision (threshold +
    /// approval) never races a concurrent upsert on the same conflict key.
    /// </summary>
    Task<AggregateTriggerUpsertAndMaterializeResult> AtomicUpsertAndMaterializeAsync(
        AggregateTriggerDefinition definition,
        string conflictKey,
        string eventId,
        Func<AggregateTriggerCurrentRow, AggregateTriggerMaterializationDecision> decide,
        CancellationToken ct = default);

    /// <summary>Canonical structured definition persistence (runtime_orchestration.aggregate_trigger_definitions authority).</summary>
    Task SaveDefinitionAsync(AggregateTriggerDefinition definition, CancellationToken ct = default);
    Task<AggregateTriggerDefinition?> LoadDefinitionAsync(Guid triggerDefinitionId, CancellationToken ct = default);
}

public class InMemoryAggregateTriggerRepository : AggregateTriggerRepository
{
    private readonly object _gate = new();
    private readonly HashSet<(Guid,string)> _events = [];
    private readonly Dictionary<(Guid,string), AggregateTriggerCurrentRow> _currents = [];
    private readonly Dictionary<(Guid,string), Guid> _materializations = [];
    private readonly Dictionary<Guid, AggregateTriggerDefinition> _definitions = [];

    public Task<AggregateTriggerAppendResult> AppendEventEvidenceAsync(AggregateTriggerEventEvidence evidence, CancellationToken ct = default)
    {
        lock (_gate) return Task.FromResult(new AggregateTriggerAppendResult(_events.Add((evidence.TriggerDefinitionId, evidence.EventId))));
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
            var key = (definition.TriggerDefinitionId, currentRow.ConflictKey);
            if (_materializations.TryGetValue(key, out var existing)) return Task.FromResult(new AggregateTriggerMaterializationResult(false, existing));
            var id = Guid.NewGuid();
            _materializations[key] = id;
            return Task.FromResult(new AggregateTriggerMaterializationResult(true, id));
        }
    }

    public Task<AggregateTriggerUpsertAndMaterializeResult> AtomicUpsertAndMaterializeAsync(
        AggregateTriggerDefinition definition,
        string conflictKey,
        string eventId,
        Func<AggregateTriggerCurrentRow, AggregateTriggerMaterializationDecision> decide,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            _currents.TryGetValue((definition.TriggerDefinitionId, conflictKey), out var existing);
            var counters = new Dictionary<string, decimal>(existing?.Counters ?? new Dictionary<string, decimal>(), StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in definition.DeltaMap) counters[k] = counters.GetValueOrDefault(k) + v;
            var row = new AggregateTriggerCurrentRow(definition.TriggerDefinitionId, conflictKey, counters, DateTimeOffset.UtcNow);
            var decision = decide(row);
            // Commit the upsert together with the materialization decision under the same lock,
            // mirroring the single-transaction semantics of the Npgsql implementation.
            _currents[(definition.TriggerDefinitionId, conflictKey)] = row;
            if (decision != AggregateTriggerMaterializationDecision.Materialize)
                return Task.FromResult(new AggregateTriggerUpsertAndMaterializeResult(row, decision, null));

            var key = (definition.TriggerDefinitionId, conflictKey);
            AggregateTriggerMaterializationResult materialization;
            if (_materializations.TryGetValue(key, out var existingMaterializationId))
            {
                materialization = new AggregateTriggerMaterializationResult(false, existingMaterializationId);
            }
            else
            {
                var id = Guid.NewGuid();
                _materializations[key] = id;
                materialization = new AggregateTriggerMaterializationResult(true, id);
            }
            return Task.FromResult(new AggregateTriggerUpsertAndMaterializeResult(row, decision, materialization));
        }
    }

    public Task SaveDefinitionAsync(AggregateTriggerDefinition definition, CancellationToken ct = default)
    {
        lock (_gate) { _definitions[definition.TriggerDefinitionId] = definition; }
        return Task.CompletedTask;
    }

    public Task<AggregateTriggerDefinition?> LoadDefinitionAsync(Guid triggerDefinitionId, CancellationToken ct = default)
    {
        lock (_gate) { return Task.FromResult(_definitions.TryGetValue(triggerDefinitionId, out var d) ? d : null); }
    }
}
