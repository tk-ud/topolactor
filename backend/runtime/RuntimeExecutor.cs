using Microsoft.Extensions.Logging;
using System.Text.Json;
using Topolactor.Guard;
using Topolactor.Mapper;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Single canonical execution path for all operations.
/// Orchestrates the full pipeline: vector → attractor → structure map → package → schema → emission.
/// Route missing is handled as a canonical fallback jump event.
/// Dispatcher/manifest and runtime infrastructure failures remain explicit error responses.
/// </summary>
public class RuntimeExecutor : IDispatchableRuntime
{
    private readonly ILogger<RuntimeExecutor> _logger;
    private readonly OperationVectorResolver _operationVectorResolver;
    private readonly AttractorResolver _attractorResolver;
    private readonly StructureMapResolver _structureMapResolver;
    private readonly PackageResolver _packageResolver;
    private readonly SchemaResolver _schemaResolver;
    private readonly EmissionBuilder _emissionBuilder;
    private readonly SemanticMapper _semanticMapper;
    private readonly DiffLogRepository _diffLogRepository;
    private readonly SqlAttentionLogsRepository _sqlAttentionLogsRepository;
    private readonly RuntimeGuard _runtimeGuard;
    private readonly ContextRouteRecommendationResolver _contextRouteRecommendationResolver;
    private readonly OutputLaneRouter? _outputLaneRouter;

    public RuntimeExecutor(
        ILogger<RuntimeExecutor> logger,
        OperationVectorResolver operationVectorResolver,
        AttractorResolver attractorResolver,
        StructureMapResolver structureMapResolver,
        PackageResolver packageResolver,
        SchemaResolver schemaResolver,
        EmissionBuilder emissionBuilder,
        SemanticMapper semanticMapper,
        DiffLogRepository diffLogRepository,
        SqlAttentionLogsRepository sqlAttentionLogsRepository,
        RuntimeGuard runtimeGuard,
        ContextRouteRecommendationResolver contextRouteRecommendationResolver,
        OutputLaneRouter? outputLaneRouter = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _operationVectorResolver = operationVectorResolver ?? throw new ArgumentNullException(nameof(operationVectorResolver));
        _attractorResolver = attractorResolver ?? throw new ArgumentNullException(nameof(attractorResolver));
        _structureMapResolver = structureMapResolver ?? throw new ArgumentNullException(nameof(structureMapResolver));
        _packageResolver = packageResolver ?? throw new ArgumentNullException(nameof(packageResolver));
        _schemaResolver = schemaResolver ?? throw new ArgumentNullException(nameof(schemaResolver));
        _emissionBuilder = emissionBuilder ?? throw new ArgumentNullException(nameof(emissionBuilder));
        _semanticMapper = semanticMapper ?? throw new ArgumentNullException(nameof(semanticMapper));
        _diffLogRepository = diffLogRepository ?? throw new ArgumentNullException(nameof(diffLogRepository));
        _sqlAttentionLogsRepository = sqlAttentionLogsRepository ?? throw new ArgumentNullException(nameof(sqlAttentionLogsRepository));
        _runtimeGuard = runtimeGuard ?? throw new ArgumentNullException(nameof(runtimeGuard));
        _contextRouteRecommendationResolver = contextRouteRecommendationResolver ?? throw new ArgumentNullException(nameof(contextRouteRecommendationResolver));
        _outputLaneRouter = outputLaneRouter;
    }

    /// <summary>
    /// Executes the canonical pipeline.
    /// Route-missing attractor resolution emits canonical fallback jump events.
    /// Infrastructure failures (manifest/dispatcher/package/schema, etc.) remain explicit errors.
    /// manifestId is forwarded from the manifest dispatcher to the output lane router for db_notify.
    /// </summary>
    public async Task<EndpointResponseDto> ExecuteAsync(
        EndpointRequestDto request,
        Guid? manifestId = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("RuntimeExecutor.ExecuteAsync started.");

        // Step 1: Resolve operation vector from request
        var vector = _operationVectorResolver.Resolve(request);

        // Step 2: Guard validation — return errors immediately if invalid
        var guardErrors = _runtimeGuard.Validate(vector);
        if (guardErrors.Count > 0)
        {
            _logger.LogWarning("RuntimeGuard rejected vector with {ErrorCount} error(s).", guardErrors.Count);
            return new EndpointResponseDto(
                Success: false,
                Emission: null,
                Errors: guardErrors);
        }

        // Step 3: Resolve attractor — no silent fallback
        AttractorResult attractorResult;
        try
        {
            attractorResult = await _attractorResolver.Resolve(vector, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No structure map found for attractor key", StringComparison.Ordinal))
        {
            _logger.LogInformation("Route missing detected for key '{AttractorKey}'. Returning fallback jump event.", vector.AttractorKey);
            var routeMissingJumpEvents = BuildRouteMissingJumpEvents(request.Context);
            var routeMissingEmission = new Emission(
                StructureMapId: null,
                PackageId: null,
                SchemaId: null,
                ComponentIds: [],
                Data: null,
                Errors: [],
                JumpEvents: routeMissingJumpEvents,
                ContextRouteRecommendation: null);
            return new EndpointResponseDto(
                Success: true,
                Emission: routeMissingEmission,
                Errors: []);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AttractorResolver failed for key '{AttractorKey}'.", vector.AttractorKey);
            return ErrorResponse("ATTRACTOR_RESOLVE_FAILED", ex.Message);
        }

        // Step 4: Resolve structure map
        RuntimeWorkingShape workingShape;
        try
        {
            workingShape = await _structureMapResolver.Resolve(attractorResult, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StructureMapResolver failed for attractor '{AttractorKey}'.", attractorResult.AttractorKey);
            return ErrorResponse("STRUCTURE_MAP_RESOLVE_FAILED", ex.Message);
        }

        // Attach vector to working shape
        workingShape = workingShape with { Vector = vector };

        // Step 5: Resolve package — error if not found
        try
        {
            workingShape = await _packageResolver.Resolve(workingShape, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PackageResolver failed for PackageId '{PackageId}'.", workingShape.PackageId);
            return ErrorResponse("PACKAGE_RESOLVE_FAILED", ex.Message);
        }

        // Step 6: Resolve schema — error if not found
        try
        {
            workingShape = await _schemaResolver.Resolve(workingShape, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SchemaResolver failed for SchemaId '{SchemaId}'.", workingShape.SchemaId);
            return ErrorResponse("SCHEMA_RESOLVE_FAILED", ex.Message);
        }

        // Step 7: Map to repository command (semantic mapping, not ORM)
        var repositoryCommand = _semanticMapper.MapToRepositoryCommand(workingShape);

        // Step 8: Append topology edit audit log (topology_edit_log only; not logs.diff physical mutation pressure).
        try
        {
            await _diffLogRepository.AppendEditAsync(
                targetTable: vector.AttractorKey ?? "dispatch",
                targetId: request.IdOrHubId?.ToString(),
                operation: vector.Action ?? "unknown",
                beforeJson: null,
                afterJson: JsonSerializer.Serialize(repositoryCommand),
                diffJson: null,
                actor: vector.ContextUserId,
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DiffLogRepository.AppendEditAsync (topology_edit_log audit) failed. Continuing execution.");
        }

        if (ShouldAppendLogsDiff(vector.Action))
        {
            try
            {
                var sourceSetId = ResolveContextValue(request.Context, "sql_attention_source_set_id", "SQL_ATTENTION_SOURCE_SET_ID");
                var basisWindow = ResolveContextValue(request.Context, "sql_attention_basis_window", "SQL_ATTENTION_BASIS_WINDOW");
                var recordId = vector.ContextRecordId ?? request.IdOrHubId?.ToString() ?? "unknown";

                await _sqlAttentionLogsRepository.AppendLogsDiffAsync(new LogsDiffAppendRequest(
                    SourceSetId: sourceSetId,
                    BasisWindow: basisWindow,
                    PhysicalTableId: vector.Target ?? vector.AttractorKey ?? "unknown",
                    PhysicalTableName: vector.Target ?? vector.AttractorKey ?? "unknown",
                    RecordId: recordId,
                    OperationKind: vector.Action ?? "unknown",
                    BeforeStateOrDiffJson: "{}",
                    AfterStateOrDiffJson: JsonSerializer.Serialize(repositoryCommand),
                    ObservedAt: DateTimeOffset.UtcNow,
                    ActorOrSource: vector.ContextUserId ?? vector.TriggerKind,
                    ArchivePolicy: "required"), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SqlAttentionLogsRepository.AppendLogsDiffAsync failed. Continuing execution.");
            }
        }

        // Step 9: Context route recommendation (non-fatal — failure yields ExplicitError status)
        ContextRouteRecommendationResult? recommendation = null;
        try
        {
            recommendation = await _contextRouteRecommendationResolver.ResolveAsync(workingShape, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ContextRouteRecommendationResolver.ResolveAsync failed.");
            recommendation = new ContextRouteRecommendationResult(
                NextOperations: [],
                NextTokens: [],
                NearestPrefixSessionIds: [],
                ContributingTokens: [],
                Status: RecommendationStatus.ExplicitError,
                StatusDetail: ex.Message
            );
        }

        var userActionJump = BuildUserActionJumpEvent(request.Context);
        var jumpEvents = userActionJump is null ? null : new[] { userActionJump };
        workingShape = workingShape with { ContextRouteRecommendation = recommendation, JumpEvents = jumpEvents };

        // Step 10: Build emission from resolved working shape
        var emission = _emissionBuilder.Build(workingShape);

        var response = new EndpointResponseDto(
            Success: emission.Errors.Count == 0,
            Emission: emission,
            Errors: emission.Errors);

        if (_outputLaneRouter is not null)
        {
            await _outputLaneRouter.RouteAsync(vector, response, manifestId, ct);
        }

        _logger.LogInformation("RuntimeExecutor.ExecuteAsync completed successfully.");

        return response;
    }


    private static bool ShouldAppendLogsDiff(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return false;

        return action.Trim().ToLowerInvariant() switch
        {
            "select" or "read" or "list" or "search" => false,
            "create" or "update" or "logical_delete" or "restore" or "physical_delete" or "delete" => true,
            _ => false
        };
    }

    private static string ResolveContextValue(Dictionary<string, string>? context, string primaryKey, string fallbackEnvKey)
    {
        if (context is not null && context.TryGetValue(primaryKey, out var value) && !string.IsNullOrWhiteSpace(value))
            return value;

        var env = Environment.GetEnvironmentVariable(fallbackEnvKey);
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        throw new InvalidOperationException($"Missing required SQL Attention context value: '{primaryKey}' (or env '{fallbackEnvKey}').");
    }

    private static EndpointResponseDto ErrorResponse(string code, string message) =>
        new(
            Success: false,
            Emission: null,
            Errors: [new ValidationError(code, message)]);

    private static IReadOnlyList<RuntimeJumpEvent> BuildRouteMissingJumpEvents(Dictionary<string, string>? context)
    {
        var pastHubAddress = TryReadInt(context, "pastHubAddress");
        var currentHubAddress = TryReadInt(context, "currentHubAddress");
        var pastTopologyAddress = TryReadInt(context, "pastTopologyAddress");
        var currentTopologyAddress = TryReadInt(context, "currentTopologyAddress");
        return
        [
            new RuntimeJumpEvent("hub", pastHubAddress, currentHubAddress, 0, "route_missing"),
            new RuntimeJumpEvent("topology", pastTopologyAddress, currentTopologyAddress, 0, "route_missing")
        ];
    }

    private static RuntimeJumpEvent? BuildUserActionJumpEvent(Dictionary<string, string>? context)
    {
        if (context is null) return null;
        if (!context.TryGetValue("jumpReason", out var reason) || !string.Equals(reason, "user_action", StringComparison.Ordinal))
            return null;
        if (!context.TryGetValue("jumpScope", out var scope))
            return null;

        if (string.Equals(scope, "hub", StringComparison.Ordinal))
        {
            var pastAddress = TryReadInt(context, "pastHubAddress");
            var currentAddress = TryReadInt(context, "currentHubAddress");
            var plannedAddress = TryReadInt(context, "plannedHubAddress");
            return new RuntimeJumpEvent("hub", pastAddress, currentAddress, plannedAddress, "user_action");
        }

        if (string.Equals(scope, "topology", StringComparison.Ordinal))
        {
            var pastAddress = TryReadInt(context, "pastTopologyAddress");
            var currentAddress = TryReadInt(context, "currentTopologyAddress");
            var plannedAddress = TryReadInt(context, "plannedTopologyAddress");
            return new RuntimeJumpEvent("topology", pastAddress, currentAddress, plannedAddress, "user_action");
        }

        return null;
    }

    private static int TryReadInt(Dictionary<string, string>? context, string key)
    {
        if (context is null) return 0;
        return context.TryGetValue(key, out var raw) && int.TryParse(raw, out var parsed) ? parsed : 0;
    }
}
