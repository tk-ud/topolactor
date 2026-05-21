using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Seed Runtime: validate / preview / import pipeline for /storage/seed.json.
///
/// Per Issue #84 SSOT position:
///   Seed Runtime is a controlled registration boundary.
///   /storage/seed.json is a UI-managed topology payload candidate.
///   The canonical runtime authority is topolactor DB.
///
/// import is a skeleton: validates structure and counts runtimes.
/// Full import through manifest-driven canonical route requires Gap-1 resolution.
/// Import failure is explicit (fail-close). No silent fallback.
/// </summary>
public class SeedRuntime
{
    private readonly ILogger<SeedRuntime> _logger;
    private readonly SeedJsonRepository _seedRepository;

    public SeedRuntime(ILogger<SeedRuntime> logger, SeedJsonRepository seedRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _seedRepository = seedRepository ?? throw new ArgumentNullException(nameof(seedRepository));
    }

    public async Task<SeedSaveResult> SaveAsync(string json, CancellationToken ct = default)
        => await _seedRepository.SaveAsync(json, ct);

    public async Task<SeedLoadResult> LoadAsync(CancellationToken ct = default)
        => await _seedRepository.LoadAsync(ct);

    public async Task<SeedValidationResult> ValidateAsync(CancellationToken ct = default)
    {
        var loadResult = await _seedRepository.LoadAsync(ct);
        if (!loadResult.Success)
            return new SeedValidationResult(false,
                [new SeedValidationError(loadResult.ErrorCode!, loadResult.ErrorMessage!)]);

        JsonElement root;
        try
        {
            root = JsonDocument.Parse(loadResult.Json!).RootElement;
        }
        catch (JsonException ex)
        {
            return new SeedValidationResult(false,
                [new SeedValidationError("SEED_PARSE_ERROR", ex.Message)]);
        }

        var errors = new List<SeedValidationError>();

        if (root.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new SeedValidationError("SEED_NOT_OBJECT",
                "seed.json root must be a JSON object"));
            return new SeedValidationResult(false, errors);
        }

        if (!root.TryGetProperty("version", out _))
            errors.Add(new SeedValidationError("SEED_MISSING_VERSION",
                "version field is required"));

        if (!root.TryGetProperty("runtimes", out var runtimesEl))
        {
            errors.Add(new SeedValidationError("SEED_MISSING_RUNTIMES",
                "runtimes array is required"));
        }
        else if (runtimesEl.ValueKind != JsonValueKind.Array)
        {
            errors.Add(new SeedValidationError("SEED_RUNTIMES_NOT_ARRAY",
                "runtimes must be a JSON array"));
        }
        else
        {
            var idx = 0;
            foreach (var rt in runtimesEl.EnumerateArray())
            {
                if (!rt.TryGetProperty("name", out _))
                    errors.Add(new SeedValidationError("SEED_RUNTIME_MISSING_NAME",
                        $"runtimes[{idx}].name is required"));
                if (!rt.TryGetProperty("target", out _))
                    errors.Add(new SeedValidationError("SEED_RUNTIME_MISSING_TARGET",
                        $"runtimes[{idx}].target is required"));
                if (!rt.TryGetProperty("layer", out _))
                    errors.Add(new SeedValidationError("SEED_RUNTIME_MISSING_LAYER",
                        $"runtimes[{idx}].layer is required"));
                if (!rt.TryGetProperty("action", out _))
                    errors.Add(new SeedValidationError("SEED_RUNTIME_MISSING_ACTION",
                        $"runtimes[{idx}].action is required"));
                idx++;
            }
        }

        return new SeedValidationResult(errors.Count == 0, errors);
    }

    public async Task<SeedPreviewResult> PreviewAsync(CancellationToken ct = default)
    {
        var validation = await ValidateAsync(ct);
        if (!validation.IsValid)
            return new SeedPreviewResult(false, null, validation.Errors);

        var loadResult = await _seedRepository.LoadAsync(ct);
        var root = JsonDocument.Parse(loadResult.Json!).RootElement;

        var runtimes = new List<SeedRuntimePreview>();
        if (root.TryGetProperty("runtimes", out var runtimesEl))
        {
            foreach (var rt in runtimesEl.EnumerateArray())
            {
                runtimes.Add(new SeedRuntimePreview(
                    rt.TryGetProperty("name",   out var n) ? n.GetString() ?? "" : "",
                    rt.TryGetProperty("target", out var t) ? t.GetString() ?? "" : "",
                    rt.TryGetProperty("layer",  out var l) ? l.GetString() ?? "" : "",
                    rt.TryGetProperty("action", out var a) ? a.GetString() ?? "" : ""));
            }
        }

        return new SeedPreviewResult(true,
            new SeedPreviewData(runtimes, runtimes.Count), []);
    }

    /// <summary>
    /// Import skeleton: validates seed.json and counts runtime declarations.
    /// Full import through manifest-driven canonical route requires Gap-1 resolution.
    /// Failure is explicit (fail-close). No silent fallback.
    /// </summary>
    public async Task<SeedImportResult> ImportAsync(CancellationToken ct = default)
    {
        var validation = await ValidateAsync(ct);
        if (!validation.IsValid)
            return new SeedImportResult(false, 0, validation.Errors);

        var loadResult = await _seedRepository.LoadAsync(ct);
        var root = JsonDocument.Parse(loadResult.Json!).RootElement;

        var runtimeCount = root.TryGetProperty("runtimes", out var runtimesEl) &&
                           runtimesEl.ValueKind == JsonValueKind.Array
            ? runtimesEl.GetArrayLength()
            : 0;

        _logger.LogInformation(
            "SeedRuntime.ImportAsync: seed validated — {Count} runtime(s) declared. " +
            "Import blocked pending Gap-1 resolution (manifest-driven routing).",
            runtimeCount);

        return new SeedImportResult(
            false,
            runtimeCount,
            [new SeedValidationError(
                "SEED_IMPORT_PENDING_GAP1",
                $"Import blocked pending Gap-1 resolution. " +
                $"{runtimeCount} runtime(s) declared and validated. " +
                "Full canonical import requires manifest-driven routing (ManifestDispatcher migration).")]);
    }
}
