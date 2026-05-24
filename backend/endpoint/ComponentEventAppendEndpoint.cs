using System.Security.Cryptography;
using System.Text;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Endpoint;

public sealed record ComponentEventAppendRequestDto(IReadOnlyList<ComponentOperationEventDto>? Events);

public sealed record ComponentOperationEventDto(
    string? ComponentId,
    string? PackageId,
    string? LayoutId,
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

            var eventId = DeterministicGuidFromString($"component-event:{e.IdempotencyKey}");
            var sessionSeed = $"{e.PackageId ?? "none"}:{e.LayoutId ?? "none"}:{e.ActorOrSource}";
            var sessionId = DeterministicGuidFromString($"component-session:{sessionSeed}");

            try
            {
                await _repo.AppendContextEventAsync(new ContextEventRecord(
                    EventId: eventId,
                    SessionId: sessionId,
                    UserId: null,
                    Role: "frontend_component_event",
                    TableName: "ui_component",
                    RecordId: e.ComponentId,
                    Operation: e.EventType,
                    TokenIds: [],
                    CreatedAt: occurredAt,
                    NextOperationHint: null,
                    NextTokenIdsHint: null), ct);
                accepted += 1;
            }
            catch (Exception ex) when (ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("23505", StringComparison.OrdinalIgnoreCase))
            {
                // idempotency duplicate: accept as already appended
                accepted += 1;
            }
        }

        return new ComponentEventAppendResponseDto(true, accepted);
    }

    private static Guid DeterministicGuidFromString(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }
}
