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
/// import applies validated runtime declarations through canonical manifest dispatcher route.
/// Import failure is explicit (fail-close). No silent fallback.
/// </summary>
public class SeedRuntime
{
    private readonly ILogger<SeedRuntime> _logger;
    private readonly SeedJsonRepository _seedRepository;
    private readonly SeedImportApplyRepository _seedImportApplyRepository;

    public SeedRuntime(
        ILogger<SeedRuntime> logger,
        SeedJsonRepository seedRepository,
        SeedImportApplyRepository seedImportApplyRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _seedRepository = seedRepository ?? throw new ArgumentNullException(nameof(seedRepository));
        _seedImportApplyRepository = seedImportApplyRepository ?? throw new ArgumentNullException(nameof(seedImportApplyRepository));
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
    /// Import: validates seed.json and applies each runtime declaration through canonical seed import apply boundary.
    /// Failure is explicit (fail-close). No silent fallback.
    /// </summary>
    public async Task<SeedImportResult> ImportAsync(CancellationToken ct = default)
    {
        var validation = await ValidateAsync(ct);
        if (!validation.IsValid)
            return new SeedImportResult(false, 0, validation.Errors);

        var loadResult = await _seedRepository.LoadAsync(ct);
        var root = JsonDocument.Parse(loadResult.Json!).RootElement;

        var runtimeCount = 0;
        if (!root.TryGetProperty("runtimes", out var runtimesEl) ||
            runtimesEl.ValueKind != JsonValueKind.Array)
            return new SeedImportResult(true, runtimeCount, []);

        var importErrors = new List<SeedValidationError>();

        foreach (var runtime in runtimesEl.EnumerateArray())
        {
            runtimeCount++;

            var name = runtime.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty;
            var target = runtime.GetProperty("target").GetString() ?? string.Empty;
            var layer = runtime.GetProperty("layer").GetString() ?? string.Empty;
            var action = runtime.GetProperty("action").GetString() ?? string.Empty;

            if (string.Equals(target, "admin", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(layer, "seed_runtime", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(action, "import", StringComparison.OrdinalIgnoreCase))
            {
                importErrors.Add(new SeedValidationError(
                    "SEED_IMPORT_RECURSIVE_ROUTE_FORBIDDEN",
                    $"runtimes[{runtimeCount - 1}] ({name}) maps to admin:seed_runtime:import and is explicitly forbidden."));
                continue;
            }

            var runtimeDestination = runtime.TryGetProperty("runtimeDestination", out var destEl)
                ? destEl.GetString() ?? "topology_transform_runtime"
                : "topology_transform_runtime";

            try
            {
                var canonicalWriteApplied = await _seedImportApplyRepository.ApplyRuntimeDeclarationAsync(
                    target,
                    layer,
                    action,
                    runtimeDestination,
                    ct);

                if (!canonicalWriteApplied)
                {
                    importErrors.Add(new SeedValidationError(
                        "SEED_IMPORT_CANONICAL_DB_WRITE_NOT_CONFIRMED",
                        $"runtimes[{runtimeCount - 1}] ({name}) was not applied because active mapping already exists or canonical write was not performed."));
                }
            }
            catch (Exception ex)
            {
                importErrors.Add(new SeedValidationError(
                    "SEED_IMPORT_RUNTIME_DISPATCH_FAILED",
                    $"runtimes[{runtimeCount - 1}] ({name}) failed: {ex.Message}"));
            }
        }

        if (importErrors.Count > 0)
            return new SeedImportResult(false, runtimeCount, importErrors);

        _logger.LogInformation(
            "SeedRuntime.ImportAsync: imported {Count} runtime declaration(s) via canonical seed import apply boundary.",
            runtimeCount);

        return new SeedImportResult(true, runtimeCount, []);
    }
}
