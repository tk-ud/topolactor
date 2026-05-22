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

    private static readonly HashSet<string> AllowedRuntimeDestinations =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "topology_transform_runtime",
            "admin_runtime"
        };

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
                if (rt.ValueKind != JsonValueKind.Object)
                {
                    errors.Add(new SeedValidationError("SEED_RUNTIME_NOT_OBJECT",
                        $"runtimes[{idx}] must be a JSON object"));
                    idx++;
                    continue;
                }

                var name = ReadRequiredString(rt, "name", idx, errors, "SEED_RUNTIME_NAME_INVALID");
                var target = ReadRequiredString(rt, "target", idx, errors, "SEED_RUNTIME_TARGET_INVALID");
                var layer = ReadRequiredString(rt, "layer", idx, errors, "SEED_RUNTIME_LAYER_INVALID");
                var action = ReadRequiredString(rt, "action", idx, errors, "SEED_RUNTIME_ACTION_INVALID");

                string runtimeDestination = "topology_transform_runtime";
                if (rt.TryGetProperty("runtimeDestination", out var rd))
                {
                    if (rd.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(rd.GetString()))
                    {
                        errors.Add(new SeedValidationError("SEED_RUNTIME_DESTINATION_INVALID",
                            $"runtimes[{idx}].runtimeDestination must be a non-empty string when specified"));
                    }
                    else
                    {
                        runtimeDestination = rd.GetString()!;
                    }
                }

                if (!AllowedRuntimeDestinations.Contains(runtimeDestination))
                {
                    errors.Add(new SeedValidationError("SEED_RUNTIME_DESTINATION_UNKNOWN",
                        $"runtimes[{idx}].runtimeDestination '{runtimeDestination}' is not allowed. Allowed: {string.Join(", ", AllowedRuntimeDestinations)}"));
                }

                if (!string.IsNullOrWhiteSpace(target) && !string.IsNullOrWhiteSpace(layer) && !string.IsNullOrWhiteSpace(action) &&
                    string.Equals(target, "admin", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(layer, "seed_runtime", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(action, "import", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new SeedValidationError("SEED_IMPORT_RECURSIVE_ROUTE_FORBIDDEN",
                        $"runtimes[{idx}] maps to admin:seed_runtime:import and is explicitly forbidden"));
                }

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

            var runtimeDestination = runtime.TryGetProperty("runtimeDestination", out var destEl)
                ? destEl.GetString() ?? "topology_transform_runtime"
                : "topology_transform_runtime";

            var apply = await _seedImportApplyRepository.ApplyRuntimeDeclarationAsync(
                target,
                layer,
                action,
                runtimeDestination,
                ct);

            switch (apply.Status)
            {
                case SeedImportApplyStatus.Inserted:
                case SeedImportApplyStatus.AlreadyApplied:
                    continue;
                case SeedImportApplyStatus.Conflict:
                    importErrors.Add(new SeedValidationError(
                        "SEED_IMPORT_CONFLICTING_ACTIVE_MAPPING",
                        $"runtimes[{runtimeCount - 1}] ({name}) conflict: {apply.Message}"));
                    break;
                default:
                    importErrors.Add(new SeedValidationError(
                        "SEED_IMPORT_RUNTIME_DISPATCH_FAILED",
                        $"runtimes[{runtimeCount - 1}] ({name}) failed: {apply.Message ?? "unknown failure"}"));
                    break;
            }
        }

        if (importErrors.Count > 0)
            return new SeedImportResult(false, runtimeCount, importErrors);

        _logger.LogInformation(
            "SeedRuntime.ImportAsync: imported {Count} runtime declaration(s) via canonical seed import apply boundary.",
            runtimeCount);

        return new SeedImportResult(true, runtimeCount, []);
    }
    private static string? ReadRequiredString(
        JsonElement obj,
        string property,
        int idx,
        List<SeedValidationError> errors,
        string code)
    {
        if (!obj.TryGetProperty(property, out var el) ||
            el.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(el.GetString()))
        {
            errors.Add(new SeedValidationError(code,
                $"runtimes[{idx}].{property} must be a non-empty string"));
            return null;
        }

        return el.GetString();
    }

}
