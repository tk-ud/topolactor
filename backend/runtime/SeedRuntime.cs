using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceProvider _serviceProvider;

    public SeedRuntime(
        ILogger<SeedRuntime> logger,
        SeedJsonRepository seedRepository,
        IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _seedRepository = seedRepository ?? throw new ArgumentNullException(nameof(seedRepository));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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
    /// Import: validates seed.json and applies each runtime declaration through canonical manifest dispatcher route.
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

        var dispatcher = _serviceProvider.GetRequiredService<ManifestDispatcher>();
        var importErrors = new List<SeedValidationError>();

        foreach (var runtime in runtimesEl.EnumerateArray())
        {
            runtimeCount++;

            var name = runtime.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty;
            var target = runtime.GetProperty("target").GetString();
            var layer = runtime.GetProperty("layer").GetString();
            var action = runtime.GetProperty("action").GetString();

            var payload = JsonSerializer.SerializeToElement(new
            {
                source = "seed_runtime_import",
                canonical_db_write_boundary = "runtime_handler_owned",
                runtime_name = name,
                runtime_definition = runtime
            });

            var request = new EndpointRequestDto(
                OperationType: "SeedImport",
                Target: target,
                Layer: layer,
                Action: action,
                IdOrHubId: null,
                Payload: payload,
                Context: null,
                TriggerKind: "client",
                Role: null);

            var response = await dispatcher.DispatchAsync(request, ct);
            if (response.Success)
            {
                var hasCanonicalWrite = response.Emission?.Data is JsonElement dataEl &&
                                       dataEl.ValueKind == JsonValueKind.Object &&
                                       dataEl.TryGetProperty("canonical_db_write_applied", out var writeEl) &&
                                       writeEl.ValueKind == JsonValueKind.True;

                if (hasCanonicalWrite)
                    continue;

                importErrors.Add(new SeedValidationError(
                    "SEED_IMPORT_CANONICAL_DB_WRITE_NOT_CONFIRMED",
                    $"runtimes[{runtimeCount - 1}] ({name}) dispatched but canonical_db_write_applied was not confirmed by runtime output."));
                continue;
            }

            var errorMessage = response.Errors.Count > 0
                ? string.Join("; ", response.Errors.Select(e => $"{e.Code}: {e.Message}"))
                : "Unknown import failure.";

            importErrors.Add(new SeedValidationError(
                "SEED_IMPORT_RUNTIME_DISPATCH_FAILED",
                $"runtimes[{runtimeCount - 1}] ({name}) failed: {errorMessage}"));
        }

        if (importErrors.Count > 0)
            return new SeedImportResult(false, runtimeCount, importErrors);

        _logger.LogInformation(
            "SeedRuntime.ImportAsync: imported {Count} runtime declaration(s) via canonical manifest dispatcher route.",
            runtimeCount);

        return new SeedImportResult(true, runtimeCount, []);
    }
}
