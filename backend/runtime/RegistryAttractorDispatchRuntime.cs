using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Manifest-dispatched IDispatchableRuntime handler for registry_attractor_runtime.
///
/// Registered as "registry_attractor_runtime" in the production ManifestDispatcher handler dictionary.
/// Enables manifest-driven dispatch to registry attractor observation, as distinct from the
/// SqlAttentionScheduler BackgroundService route (cron trigger, not manifest-dispatched).
///
/// Per SSOT registry_attractor_contract:
///   role: sql_attention_observation_surface
///   output: continuity_evidence, attention_weight_observation, projection_or_ci_input
///
/// Required payload fields:
///   source_set_id (string, non-empty) — scopes the watch candidate load and exploration.
///   basis_window  (string, non-empty) — time window for logs.refresh_logs_current_watch.
///
/// Explicit failure — no silent fallback:
///   Missing source_set_id → REGISTRY_ATTRACTOR_SOURCE_SET_ID_REQUIRED
///   Missing basis_window  → REGISTRY_ATTRACTOR_BASIS_WINDOW_REQUIRED
///   Watch load failure    → REGISTRY_ATTRACTOR_WATCH_LOAD_FAILED
///
/// Persistence boundary:
///   When exploration returns Ok with hits, AppendAttentionGenerationAsync is called per hit.
///   This mirrors the SqlAttentionScheduler persistence boundary but enters via manifest dispatch.
/// </summary>
public sealed class RegistryAttractorDispatchRuntime : IDispatchableRuntime
{
    private readonly ILogger<RegistryAttractorDispatchRuntime> _logger;
    private readonly HubAttractorExplorationRuntime _explorationRuntime;
    private readonly SqlAttentionLogsRepository _sqlAttentionLogsRepository;

    public RegistryAttractorDispatchRuntime(
        ILogger<RegistryAttractorDispatchRuntime> logger,
        HubAttractorExplorationRuntime explorationRuntime,
        SqlAttentionLogsRepository sqlAttentionLogsRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _explorationRuntime = explorationRuntime ?? throw new ArgumentNullException(nameof(explorationRuntime));
        _sqlAttentionLogsRepository = sqlAttentionLogsRepository ?? throw new ArgumentNullException(nameof(sqlAttentionLogsRepository));
    }

    public async Task<EndpointResponseDto> ExecuteAsync(
        EndpointRequestDto request,
        Guid? manifestId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryExtractString(request.Payload, "source_set_id", out var sourceSetId) || string.IsNullOrWhiteSpace(sourceSetId))
        {
            return new EndpointResponseDto(
                Success: false,
                Emission: null,
                Errors: [new ValidationError(
                    "REGISTRY_ATTRACTOR_SOURCE_SET_ID_REQUIRED",
                    "registry_attractor_runtime: payload must include non-empty 'source_set_id'.")]);
        }

        if (!TryExtractString(request.Payload, "basis_window", out var basisWindow) || string.IsNullOrWhiteSpace(basisWindow))
        {
            return new EndpointResponseDto(
                Success: false,
                Emission: null,
                Errors: [new ValidationError(
                    "REGISTRY_ATTRACTOR_BASIS_WINDOW_REQUIRED",
                    "registry_attractor_runtime: payload must include non-empty 'basis_window'.")]);
        }

        _logger.LogDebug(
            "RegistryAttractorDispatchRuntime: loading watch candidates for sourceSetId={SourceSetId} basisWindow={BasisWindow}.",
            sourceSetId, basisWindow);

        IReadOnlyList<WatchChangeCandidate> candidates;
        try
        {
            candidates = await _sqlAttentionLogsRepository.LoadWatchCandidatesAsync(sourceSetId, basisWindow, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "RegistryAttractorDispatchRuntime: LoadWatchCandidatesAsync failed for sourceSetId={SourceSetId} basisWindow={BasisWindow}.",
                sourceSetId, basisWindow);
            return new EndpointResponseDto(
                Success: false,
                Emission: null,
                Errors: [new ValidationError(
                    "REGISTRY_ATTRACTOR_WATCH_LOAD_FAILED",
                    $"registry_attractor_runtime: failed to load watch candidates for source_set_id='{sourceSetId}'.")]);
        }

        _logger.LogDebug(
            "RegistryAttractorDispatchRuntime: running exploration for sourceSetId={SourceSetId} basisWindow={BasisWindow}.",
            sourceSetId, basisWindow);

        var result = await _explorationRuntime.ExploreAsync(candidates, sourceSetId, basisWindow, ct);

        var hitCount = 0;
        if (result.Status == HubAttractorExplorationStatus.Ok && result.Result is { Hits.Count: > 0 } explorationResult)
        {
            foreach (var hit in explorationResult.Hits)
            {
                await _sqlAttentionLogsRepository.AppendAttentionGenerationAsync(
                    new AttentionGenerationAppendRequest(
                        hit.GenerationLineId,
                        hit.CurrentId,
                        hit.SourceSetId,
                        hit.TopologyManifestId!.Value,
                        hit.HubRelationId!.Value,
                        hit.HubId!.Value,
                        hit.NeighborScore,
                        hit.HitRank,
                        hit.ScoreBand,
                        hit.L2Norm,
                        hit.PhaseVectorJson,
                        hit.EvidenceJson,
                        hit.SourceTopologyManifestIds!,
                        hit.ExpandedHubRelationIds!,
                        hit.ExpandedTopologyManifestIds!,
                        hit.ExpandedHubIds!), ct);
            }
            hitCount = explorationResult.Hits.Count;
        }

        _logger.LogInformation(
            "RegistryAttractorDispatchRuntime: exploration complete — status={Status} hits={HitCount} sourceSetId={SourceSetId}.",
            result.Status, hitCount, sourceSetId);

        var data = JsonSerializer.SerializeToElement(new
        {
            status = result.Status.ToString(),
            detail = result.Detail,
            hit_count = hitCount,
            source_set_id = sourceSetId,
            basis_window = basisWindow,
        });

        return new EndpointResponseDto(
            Success: true,
            Emission: new Emission(
                StructureMapId: null,
                PackageId: null,
                SchemaId: null,
                ComponentIds: [],
                Data: data,
                Errors: []),
            Errors: []);
    }

    private static bool TryExtractString(JsonElement? payload, string key, out string? value)
    {
        value = null;
        if (!payload.HasValue || payload.Value.ValueKind != JsonValueKind.Object)
            return false;
        if (!payload.Value.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.String)
            return false;
        value = el.GetString();
        return true;
    }
}
