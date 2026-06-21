using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Scheduler;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Generic external_port consumer execution boundary for dispatchExternalPort.
/// Resolves a DB active port record and its active policy, then executes only
/// generic operation_key primitives. Provider kind remains DB data; this runtime
/// contains no provider-specific branching or client implementation.
/// After policy execution, broadcasts the dispatch result via SseEventBroadcaster
/// so the response enters the SSE lane.
/// </summary>
public sealed class ExternalPortDispatchRuntime : IDispatchableRuntime
{
    private readonly ILogger<ExternalPortDispatchRuntime> _logger;
    private readonly IExternalPortPolicyRepository _repository;
    private readonly IExternalPortPolicyStepExecutor _policyStepExecutor;
    private readonly SseEventBroadcaster? _sseBroadcaster;

    public ExternalPortDispatchRuntime(
        ILogger<ExternalPortDispatchRuntime> logger,
        IExternalPortPolicyRepository repository,
        IExternalPortPolicyStepExecutor policyStepExecutor,
        SseEventBroadcaster? sseBroadcaster = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _policyStepExecutor = policyStepExecutor ?? throw new ArgumentNullException(nameof(policyStepExecutor));
        _sseBroadcaster = sseBroadcaster;
    }

    public async Task<EndpointResponseDto> ExecuteAsync(EndpointRequestDto request, Guid? manifestId, CancellationToken ct = default)
    {
        try
        {
            ExternalPortRecord? record;
            string rawRef;
            string portKind;
            Guid portId;
            string? routeKey;
            if (TryReadPortTargetRef(request.Payload, out var rawTargetRef))
            {
                if (!TryParsePortTargetRef(rawTargetRef!, out portKind, out portId, out routeKey))
                    return Fail("EXTERNAL_PORT_TARGET_REF_INVALID", "portTargetRef must use external-port:<portKind>:<portId>[:routeKey].");
                rawRef = rawTargetRef!;
                record = await _repository.LoadPortRecordByIdAsync(portKind, portId, routeKey, ct);
            }
            else if (TryReadHookRoute(request.Payload, out var hookPath, out var hookRouteKey))
            {
                portKind = "hook_port";
                routeKey = hookRouteKey;
                record = await _repository.LoadHookPortRecordAsync(hookPath!, hookRouteKey!, ct);
                portId = record?.PortId ?? Guid.Empty;
                rawRef = record is null ? $"external-port:hook_port:missing:{hookRouteKey}" : $"external-port:hook_port:{record.PortId}:{hookRouteKey}";
            }
            else
            {
                return Fail("EXTERNAL_PORT_TARGET_REF_MISSING", "dispatchExternalPort payload must include port_target_ref/target_ref or hook_path with route_key.");
            }

            if (record is null)
                return Fail("EXTERNAL_PORT_RECORD_MISSING", "No active external port record matched the dispatch target.");

            ExternalPortResolver.FailCloseOnInvalidPortRecord(record);
            if (!string.Equals(record.PortKind, portKind, StringComparison.Ordinal) || record.PortId != portId)
                return Fail("EXTERNAL_PORT_RECORD_INVALID", "Resolved external port record does not match portTargetRef.");
            if (portKind == "hook_port" && !string.Equals(record.RouteKey, routeKey, StringComparison.Ordinal))
                return Fail("EXTERNAL_PORT_RECORD_INVALID", "Resolved hook_port routeKey does not match portTargetRef.");
            if (!string.Equals(record.CredentialKind, "none", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(record.ReferenceKey))
                return Fail("EXTERNAL_CREDENTIAL_REQUIREMENT_INVALID", "External port credential requirement is missing its reference key.");

            var policy = await _repository.LoadPolicyAsync(record, ct);
            if (policy is null || !policy.Active || policy.PolicySteps.Count == 0)
                return Fail("EXTERNAL_PORT_POLICY_MISSING", "No active external port policy/steps matched the resolved port record.");

            var context = new ExternalPortExecutionContext
            {
                PortRecord = record,
                PortKind = record.PortKind,
                RequiredByBundle = record.RequiredByBundle,
                RouteKey = routeKey,
                DispatchId = Guid.NewGuid().ToString("N"),
                RequestPayload = ReadObjectProperty(request.Payload, "dispatch_payload"),
                OutputProp = ReadStringProperty(request.Payload, "output_prop"),
                SignatureInput = ReadStringMapProperty(request.Payload, "signature_input"),
                SignatureConfig = ReadStringMapProperty(request.Payload, "signature_config")
            };
            await _policyStepExecutor.ExecutePolicyAsync(policy, context, ct);

            var dispatchResult = new
            {
                status = "boundary_reached",
                portTargetRef = rawRef,
                portKind = record.PortKind,
                portId = record.PortId,
                requiredByBundle = record.RequiredByBundle,
                providerKind = record.ProviderKind,
                credentialKind = record.CredentialKind,
                policyKey = policy.PolicyKey,
                dispatchId = context.DispatchId,
                executedOperationKeys = context.ExecutedOperationKeys,
                outputProp = context.OutputProp
            };

            _sseBroadcaster?.Broadcast(new SseEvent(
                "external_port_dispatch",
                JsonSerializer.Serialize(dispatchResult)));

            var data = JsonSerializer.SerializeToElement(new { externalPortDispatch = dispatchResult });
            return new EndpointResponseDto(true, new Emission(null, null, null, [], data, []), []);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            _logger.LogWarning(ex, "External port dispatch failed closed.");
            return Fail(ex.Message, "External port dispatch failed closed at the generic execution boundary.");
        }
    }

    private static EndpointResponseDto Fail(string code, string message) =>
        new(false, null, [new ValidationError(code, message)]);

    private static bool TryReadPortTargetRef(JsonElement? payload, out string? rawRef)
    {
        rawRef = ReadStringProperty(payload, "port_target_ref") ?? ReadStringProperty(payload, "target_ref");
        return !string.IsNullOrWhiteSpace(rawRef);
    }

    internal static bool TryParsePortTargetRef(string rawRef, out string portKind, out Guid portId, out string? routeKey)
    {
        portKind = string.Empty;
        routeKey = null;
        portId = Guid.Empty;
        var parts = rawRef.Split(':', StringSplitOptions.None);
        if (parts.Length is not (3 or 4)) return false;
        if (!string.Equals(parts[0], "external-port", StringComparison.Ordinal)) return false;
        if (parts[1] is not ("access_port" or "response_port" or "hook_port")) return false;
        if (!Guid.TryParse(parts[2], out portId)) return false;
        portKind = parts[1];
        routeKey = parts.Length == 4 && !string.IsNullOrWhiteSpace(parts[3]) ? parts[3] : null;
        if (portKind == "hook_port" && string.IsNullOrWhiteSpace(routeKey)) return false;
        return true;
    }

    private static string? ReadStringProperty(JsonElement? payload, string name)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } element) return null;
        return element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }

    private static bool TryReadHookRoute(JsonElement? payload, out string? hookPath, out string? routeKey)
    {
        hookPath = ReadStringProperty(payload, "hook_path");
        routeKey = ReadStringProperty(payload, "route_key");
        return !string.IsNullOrWhiteSpace(hookPath) && !string.IsNullOrWhiteSpace(routeKey);
    }

    private static JsonElement? ReadObjectProperty(JsonElement? payload, string name)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } element) return null;
        return element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Object ? prop.Clone() : null;
    }

    private static IReadOnlyDictionary<string, string> ReadStringMapProperty(JsonElement? payload, string name)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } element) return new Dictionary<string, string>();
        if (!element.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.Object) return new Dictionary<string, string>();
        return prop.EnumerateObject()
            .Where(static item => item.Value.ValueKind == JsonValueKind.String)
            .ToDictionary(static item => item.Name, static item => item.Value.GetString() ?? string.Empty, StringComparer.Ordinal);
    }
}
