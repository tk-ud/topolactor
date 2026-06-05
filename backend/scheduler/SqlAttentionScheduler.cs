using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;

namespace Topolactor.Scheduler;

/// <summary>
/// Cron excitation trigger for SQL Attention hub-attractor exploration.
///
/// Design boundary:
///   This class is the cron trigger layer for SQL Attention watch + exploration.
///   It calls logs.refresh_logs_current_watch via SqlAttentionLogsRepository,
///   and when change candidates exist, invokes HubAttractorExplorationRuntime.
///   All policy and exploration logic live in HubAttractorExplorationRuntime.
///   SQL Attention write boundary is AppendAttentionGenerationAsync — request-based
///   append-only SQLAT hit / phaseAT persistence, not exploration execution.
///
/// Route:
///   cron trigger
///   → SqlAttentionLogsRepository.LoadWatchCandidatesAsync
///   → if no change candidates: skip
///   → else: HubAttractorExplorationRuntime.ExploreAsync
///   → if Ok AND hits > 0: SqlAttentionLogsRepository.AppendAttentionGenerationAsync
///
/// This scheduler does NOT route through the user-facing dispatch canonical route
/// (stored_topology_data → user_operation → … → emission_or_projection) because
/// SQL Attention exploration is a background cron operation, not a user dispatch.
///
/// Bootstrap delay: SQL_ATTENTION_STARTUP_DELAY_SECONDS (env, 1-3600, default 60).
/// Run interval: SQL_ATTENTION_INTERVAL_SECONDS (env, 1-86400, default 300).
///
/// source_set_id and basis_window are resolved from env:
///   SQL_ATTENTION_SOURCE_SET_ID (required)
///   SQL_ATTENTION_BASIS_WINDOW (required)
/// When either is missing or empty, scheduler logs error and skips the run.
/// </summary>
public class SqlAttentionScheduler : BackgroundService
{
    private static readonly TimeSpan DefaultStartupDelay = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(300);

    private readonly ILogger<SqlAttentionScheduler> _logger;
    private readonly SqlAttentionLogsRepository _sqlAttentionLogsRepository;
    private readonly HubAttractorExplorationRuntime _explorationRuntime;

    public SqlAttentionScheduler(
        ILogger<SqlAttentionScheduler> logger,
        SqlAttentionLogsRepository sqlAttentionLogsRepository,
        HubAttractorExplorationRuntime explorationRuntime)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sqlAttentionLogsRepository = sqlAttentionLogsRepository ?? throw new ArgumentNullException(nameof(sqlAttentionLogsRepository));
        _explorationRuntime = explorationRuntime ?? throw new ArgumentNullException(nameof(explorationRuntime));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var startupDelay = ParseEnvSeconds("SQL_ATTENTION_STARTUP_DELAY_SECONDS", DefaultStartupDelay);
        _logger.LogInformation(
            "SqlAttentionScheduler: starting — startup delay {Delay}.", startupDelay);

        try
        {
            await Task.Delay(startupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SqlAttentionScheduler: stopped during startup delay.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

            var interval = ParseEnvSeconds("SQL_ATTENTION_INTERVAL_SECONDS", DefaultInterval);
            _logger.LogInformation(
                "SqlAttentionScheduler: run complete — next run in {Interval}.", interval);

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("SqlAttentionScheduler: stopped.");
    }

    // internal for test access
    internal async Task RunOnceAsync(CancellationToken ct)
    {
        var sourceSetId = Environment.GetEnvironmentVariable("SQL_ATTENTION_SOURCE_SET_ID");
        var basisWindow = Environment.GetEnvironmentVariable("SQL_ATTENTION_BASIS_WINDOW");

        if (string.IsNullOrWhiteSpace(sourceSetId) || string.IsNullOrWhiteSpace(basisWindow))
        {
            _logger.LogError(
                "SqlAttentionScheduler: SQL_ATTENTION_SOURCE_SET_ID and SQL_ATTENTION_BASIS_WINDOW are required. Run skipped.");
            return;
        }

        IReadOnlyList<WatchChangeCandidate> candidates;
        try
        {
            candidates = await _sqlAttentionLogsRepository.LoadWatchCandidatesAsync(
                sourceSetId, basisWindow, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SqlAttentionScheduler: LoadWatchCandidatesAsync failed — exploration skipped for this run.");
            return;
        }

        var changeCount = candidates.Count(c => c.ChangeDetected);
        if (changeCount == 0)
        {
            _logger.LogDebug(
                "SqlAttentionScheduler: no change candidates — exploration skipped for sourceSetId={SourceSetId} basisWindow={BasisWindow}.",
                sourceSetId, basisWindow);
            return;
        }

        _logger.LogInformation(
            "SqlAttentionScheduler: {ChangeCount} change candidate(s) detected — starting exploration for sourceSetId={SourceSetId} basisWindow={BasisWindow}.",
            changeCount, sourceSetId, basisWindow);

        HubAttractorExplorationRunResult result;
        try
        {
            result = await _explorationRuntime.ExploreAsync(candidates, sourceSetId, basisWindow, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SqlAttentionScheduler: ExploreAsync failed — exploration result not available for this run.");
            return;
        }

        switch (result.Status)
        {
            case HubAttractorExplorationStatus.Ok:
                var explorationResult = result.Result!;
                if (explorationResult.Hits.Count == 0)
                {
                    _logger.LogDebug(
                        "SqlAttentionScheduler: exploration Ok — no hits produced (no-change). sourceSetId={SourceSetId} basisWindow={BasisWindow}.",
                        sourceSetId, basisWindow);
                    break;
                }

                int rowsWritten;
                try
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
                    rowsWritten = explorationResult.Hits.Count * 2;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "SqlAttentionScheduler: AppendAttentionGenerationAsync failed for sourceSetId={SourceSetId} basisWindow={BasisWindow}.",
                        sourceSetId, basisWindow);
                    return;
                }

                _logger.LogInformation(
                    "SqlAttentionScheduler: {RowsWritten} attention row(s) written for sourceSetId={SourceSetId} basisWindow={BasisWindow}.",
                    rowsWritten, sourceSetId, basisWindow);
                break;

            case HubAttractorExplorationStatus.NoChange:
                _logger.LogDebug(
                    "SqlAttentionScheduler: exploration returned NoChange. sourceSetId={SourceSetId} basisWindow={BasisWindow}.",
                    sourceSetId, basisWindow);
                break;

            case HubAttractorExplorationStatus.MissingPolicy:
            case HubAttractorExplorationStatus.MalformedPolicy:
            case HubAttractorExplorationStatus.NoRelatedTopologyManifest:
            case HubAttractorExplorationStatus.NoHubRelations:
            case HubAttractorExplorationStatus.CanonicalRelationExplorationPending:
                _logger.LogError(
                    "SqlAttentionScheduler: exploration fail-close — status={Status} detail={Detail}.",
                    result.Status, result.Detail);
                break;
        }
    }

    internal static TimeSpan ParseEnvSeconds(string envVar, TimeSpan fallback)
    {
        var raw = Environment.GetEnvironmentVariable(envVar);
        return int.TryParse(raw, out var v) && v >= 1 && v <= 86400
            ? TimeSpan.FromSeconds(v)
            : fallback;
    }

    internal static string BuildStatisticsJson(HubAttractorExplorationHit hit, string basisWindow)
    {
        var vectorKeyCount = 0;
        try
        {
            using var vectorDoc = JsonDocument.Parse(hit.VectorJson);
            if (vectorDoc.RootElement.ValueKind == JsonValueKind.Object)
                vectorKeyCount = vectorDoc.RootElement.EnumerateObject().Count();
        }
        catch (JsonException)
        {
            vectorKeyCount = 0;
        }

        var sharedKeyCount = 0;
        try
        {
            using var evidenceDoc = JsonDocument.Parse(hit.EvidenceJson);
            if (evidenceDoc.RootElement.TryGetProperty("shared_key_count", out var shared)
                && shared.TryGetInt32(out var parsed))
            {
                sharedKeyCount = parsed;
            }
        }
        catch (JsonException)
        {
            sharedKeyCount = 0;
        }

        return JsonSerializer.Serialize(new
        {
            source_set_id = hit.SourceSetId,
            basis_window = basisWindow,
            hit_count = 1,
            hit_rank = hit.HitRank,
            score_band = hit.ScoreBand,
            neighbor_score = hit.NeighborScore,
            l2_norm = hit.L2Norm,
            vector_key_count = vectorKeyCount,
            shared_key_count = sharedKeyCount,
            generated_by = "sql_attention_scheduler",
            statistics_scope = "logs.attention_write",
            ema_score_status = "not_implemented_null"
        });
    }
}
