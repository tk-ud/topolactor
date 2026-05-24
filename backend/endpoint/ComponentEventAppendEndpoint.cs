using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Endpoint;

public sealed record ComponentEventAppendRequestDto(IReadOnlyList<ComponentOperationEventDto>? Events);

public sealed record ComponentOperationEventDto(
    string? ComponentId,
    string? PackageId,
    string? LayoutId,
    string? WiringId,
    string? EventType,
    Dictionary<string, object?>? Payload,
    string? ActorOrSource,
    string? OccurredAt,
    string? IdempotencyKey);

public sealed record ComponentEventAppendResponseDto(bool Success, int Accepted, IReadOnlyList<ValidationError>? Errors = null);

public class ComponentEventAppendEndpoint
{
    private readonly ContextRouteRepository _repo;

    public ComponentEventAppendEndpoint(ContextRouteRepository repo)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    public async Task<ComponentEventAppendResponseDto> HandleAsync(ComponentEventAppendRequestDto request, CancellationToken ct)
    {
        if (request.Events is null || request.Events.Count == 0)
            return new ComponentEventAppendResponseDto(false, 0, [new ValidationError("COMPONENT_EVENT_BATCH_REQUIRED", "events must be non-empty array")]);

        var accepted = 0;
        foreach (var e in request.Events)
        {
            if (string.IsNullOrWhiteSpace(e.ComponentId) || string.IsNullOrWhiteSpace(e.EventType) || string.IsNullOrWhiteSpace(e.ActorOrSource) || string.IsNullOrWhiteSpace(e.OccurredAt) || string.IsNullOrWhiteSpace(e.IdempotencyKey))
                return new ComponentEventAppendResponseDto(false, accepted, [new ValidationError("COMPONENT_EVENT_INVALID", "required fields missing")]);

            if (!DateTimeOffset.TryParse(e.OccurredAt, out var occurredAt))
                return new ComponentEventAppendResponseDto(false, accepted, [new ValidationError("COMPONENT_EVENT_OCCURRED_AT_INVALID", "occurred_at must be ISO datetime")]);

            var appended = await _repo.AppendComponentOperationEventLogAsync(new ComponentOperationEventLogRecord(
                ComponentId: e.ComponentId!,
                PackageId: e.PackageId,
                LayoutId: e.LayoutId,
                WiringId: e.WiringId,
                EventType: e.EventType!,
                PayloadJson: JsonSerializer.Serialize(e.Payload ?? new Dictionary<string, object?>()),
                ActorOrSource: e.ActorOrSource!,
                OccurredAt: occurredAt,
                IdempotencyKey: e.IdempotencyKey!), ct);
            _ = appended; // false means duplicate idempotency; still accepted
            accepted += 1;
        }

        return new ComponentEventAppendResponseDto(true, accepted);
    }

}
