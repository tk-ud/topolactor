using Microsoft.Extensions.Logging;
using Topolactor.Schema;

namespace Topolactor.Repository;

// ---------------------------------------------------------------------------
// MockPresetRepository — abstract base for topology.mock_preset_* tables.
// SSOT: docs/design/mock-preset-intake-compiler-ssot.yaml
// Production implementation: NpgsqlMockPresetRepository
// ---------------------------------------------------------------------------

public abstract class MockPresetRepository
{
    protected readonly ILogger<MockPresetRepository> _logger;

    protected MockPresetRepository(ILogger<MockPresetRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ─── create ─────────────────────────────────────────────────────────────

    public virtual Task<(string? PresetId, string? ErrorCode, string? Message)>
        CreatePresetAsync(MockPresetCreateRequest request, CancellationToken ct = default)
        => Task.FromResult<(string?, string?, string?)>((null, "MOCK_PRESET_REPO_NOT_CONFIGURED", "MockPresetRepository not configured"));

    // ─── list ────────────────────────────────────────────────────────────────

    public virtual Task<IReadOnlyList<MockPresetListItem>>
        ListPresetsAsync(string status = "active", CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MockPresetListItem>>([]);

    // ─── get ─────────────────────────────────────────────────────────────────

    public virtual Task<MockPresetDetail?>
        GetPresetAsync(Guid presetId, CancellationToken ct = default)
        => Task.FromResult<MockPresetDetail?>(null);

    // ─── object mapping upsert ───────────────────────────────────────────────

    public virtual Task<(string? MappingId, string? ErrorCode)>
        UpsertObjectMappingAsync(Guid presetId, MockPresetObjectMappingRecord mapping, CancellationToken ct = default)
        => Task.FromResult<(string?, string?)>((null, "MOCK_PRESET_REPO_NOT_CONFIGURED"));

    // ─── compile snapshot ────────────────────────────────────────────────────

    public virtual Task<(string? CompileSnapshotId, string? ErrorCode)>
        SaveCompileSnapshotAsync(Guid presetId, MockPresetCompileResult result, CancellationToken ct = default)
        => Task.FromResult<(string?, string?)>((null, "MOCK_PRESET_REPO_NOT_CONFIGURED"));

    public virtual Task<MockPresetBindResult?>
        GetLatestCompileSnapshotAsync(Guid presetId, CancellationToken ct = default)
        => Task.FromResult<MockPresetBindResult?>(null);
}
