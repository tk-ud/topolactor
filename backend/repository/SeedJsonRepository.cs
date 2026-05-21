using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// File-based repository for /storage/seed.json.
///
/// Seed Runtime SSOT position: controlled registration boundary (Issue #84).
/// /storage/seed.json is a UI-managed topology payload candidate file.
/// The canonical runtime authority remains topolactor DB.
///
/// save:  writes JSON to /storage/seed.json (validates JSON structure first).
/// load:  reads /storage/seed.json; returns explicit SEED_NOT_FOUND if absent.
/// All failures are explicit — no silent fallback.
/// </summary>
public class SeedJsonRepository
{
    private readonly ILogger<SeedJsonRepository> _logger;
    private readonly string _storagePath;

    public SeedJsonRepository(ILogger<SeedJsonRepository> logger, string storagePath)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storagePath = storagePath ?? throw new ArgumentNullException(nameof(storagePath));
    }

    public string SeedFilePath => Path.Combine(_storagePath, "seed.json");

    public async Task<SeedSaveResult> SaveAsync(string json, CancellationToken ct = default)
    {
        try
        {
            JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return new SeedSaveResult(false, "SEED_PARSE_ERROR", ex.Message);
        }

        try
        {
            if (!Directory.Exists(_storagePath))
                Directory.CreateDirectory(_storagePath);

            await File.WriteAllTextAsync(SeedFilePath, json, ct);
            _logger.LogDebug("SeedJsonRepository: saved seed.json to {Path}", SeedFilePath);
            return new SeedSaveResult(true, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SeedJsonRepository: failed to save seed.json to {Path}", SeedFilePath);
            return new SeedSaveResult(false, "SEED_WRITE_FAILED", ex.Message);
        }
    }

    public async Task<SeedLoadResult> LoadAsync(CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(SeedFilePath))
                return new SeedLoadResult(false, null, "SEED_NOT_FOUND",
                    $"seed.json not found at {SeedFilePath}");

            var json = await File.ReadAllTextAsync(SeedFilePath, ct);
            _logger.LogDebug("SeedJsonRepository: loaded seed.json from {Path}", SeedFilePath);
            return new SeedLoadResult(true, json, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SeedJsonRepository: failed to load seed.json from {Path}", SeedFilePath);
            return new SeedLoadResult(false, null, "SEED_READ_FAILED", ex.Message);
        }
    }
}
