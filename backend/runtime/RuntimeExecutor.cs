using Microsoft.Extensions.Logging;
using Topolactor.Guard;
using Topolactor.Mapper;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Single canonical execution path for all operations.
/// Orchestrates the full pipeline: vector → attractor → structure map → package → schema → emission.
/// There are no fallback paths. Any broken reference returns a validation error response.
/// </summary>
public class RuntimeExecutor
{
    private readonly ILogger<RuntimeExecutor> _logger;
    private readonly OperationVectorResolver _operationVectorResolver;
    private readonly AttractorResolver _attractorResolver;
    private readonly StructureMapResolver _structureMapResolver;
    private readonly PackageResolver _packageResolver;
    private readonly SchemaResolver _schemaResolver;
    private readonly EmissionBuilder _emissionBuilder;
    private readonly SemanticMapper _semanticMapper;
    private readonly TopologyRepository _topologyRepository;
    private readonly DiffLogRepository _diffLogRepository;
    private readonly RuntimeGuard _runtimeGuard;

    public RuntimeExecutor(
        ILogger<RuntimeExecutor> logger,
        OperationVectorResolver operationVectorResolver,
        AttractorResolver attractorResolver,
        StructureMapResolver structureMapResolver,
        PackageResolver packageResolver,
        SchemaResolver schemaResolver,
        EmissionBuilder emissionBuilder,
        SemanticMapper semanticMapper,
        TopologyRepository topologyRepository,
        DiffLogRepository diffLogRepository,
        RuntimeGuard runtimeGuard)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _operationVectorResolver = operationVectorResolver ?? throw new ArgumentNullException(nameof(operationVectorResolver));
        _attractorResolver = attractorResolver ?? throw new ArgumentNullException(nameof(attractorResolver));
        _structureMapResolver = structureMapResolver ?? throw new ArgumentNullException(nameof(structureMapResolver));
        _packageResolver = packageResolver ?? throw new ArgumentNullException(nameof(packageResolver));
        _schemaResolver = schemaResolver ?? throw new ArgumentNullException(nameof(schemaResolver));
        _emissionBuilder = emissionBuilder ?? throw new ArgumentNullException(nameof(emissionBuilder));
        _semanticMapper = semanticMapper ?? throw new ArgumentNullException(nameof(semanticMapper));
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
        _diffLogRepository = diffLogRepository ?? throw new ArgumentNullException(nameof(diffLogRepository));
        _runtimeGuard = runtimeGuard ?? throw new ArgumentNullException(nameof(runtimeGuard));
    }

    /// <summary>
    /// Executes the canonical pipeline. No fallbacks. Broken references yield explicit errors.
    /// </summary>
    public async Task<EndpointResponseDto> ExecuteAsync(
        EndpointRequestDto request,
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

        // Step 8: Append diff log (append-only, non-blocking for response)
        try
        {
            await _diffLogRepository.AppendAsync(
                hubId: request.IdOrHubId,
                action: vector.Action ?? "unknown",
                before: null,
                after: repositoryCommand,
                ct: ct);
        }
        catch (Exception ex)
        {
            // Log but do not abort — diff log failure is non-fatal to the emission
            _logger.LogError(ex, "DiffLogRepository.AppendAsync failed. Continuing execution.");
        }

        // Step 9: Build emission from resolved working shape
        var emission = _emissionBuilder.Build(workingShape);

        _logger.LogInformation("RuntimeExecutor.ExecuteAsync completed successfully.");

        return new EndpointResponseDto(
            Success: emission.Errors.Count == 0,
            Emission: emission,
            Errors: emission.Errors);
    }

    private static EndpointResponseDto ErrorResponse(string code, string message) =>
        new(
            Success: false,
            Emission: null,
            Errors: [new ValidationError(code, message)]);
}
