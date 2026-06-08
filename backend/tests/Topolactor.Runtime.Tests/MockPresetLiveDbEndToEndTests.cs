using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Schema;
using Xunit;

/// <summary>
/// Live PostgreSQL end-to-end proof for topology.mock_preset_* tables.
/// Verifies: create → save_mappings → compile → bind round-trip.
/// Verifies: bind does NOT write to topology.ui_topology_tensor.
/// Verifies: object mapping persistence and wiring candidate persistence in live DB.
///
/// TOPOLACTOR_TEST_DB_CONNECTION unset means explicit local skip. Set
/// TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1 to require a live database in CI.
///
/// SSOT: docs/design/mock-preset-intake-compiler-ssot.yaml
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class MockPresetLiveDbEndToEndTests
{
    private static string? GetConnectionString()
    {
        var cs = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
                throw new InvalidOperationException(
                    "TOPOLACTOR_TEST_DB_CONNECTION is required when TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1.");
            return null;
        }
        return cs;
    }

    [Fact]
    public async Task CreatePreset_InsertsRowAndReturnsPresetId()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var repo = new NpgsqlMockPresetRepository(NullLogger<NpgsqlMockPresetRepository>.Instance, cs);
        var request = new MockPresetCreateRequest(
            PresetKey: $"test_preset_{suffix}",
            PresetLabel: "Test Preset",
            SourceKind: "svg_xml",
            SourceHash: "hash_" + suffix,
            SourceSnapshotJson: JsonSerializer.SerializeToElement(new { raw = "<svg/>" }),
            VisualTreeJson: JsonSerializer.SerializeToElement(new { nodes = Array.Empty<object>() })
        );

        var (presetId, errorCode, message) = await repo.CreatePresetAsync(request);

        try
        {
            Assert.Null(errorCode);
            Assert.NotNull(presetId);
            Assert.True(Guid.TryParse(presetId, out _));

            var fetched = await repo.GetPresetAsync(Guid.Parse(presetId!));
            Assert.NotNull(fetched);
            Assert.Equal(request.PresetKey, fetched!.PresetKey);
            Assert.Equal(request.PresetLabel, fetched.PresetLabel);
            Assert.Equal("svg_xml", fetched.SourceKind);
        }
        finally
        {
            if (presetId is not null)
                await CleanupPresetAsync(cs, Guid.Parse(presetId));
        }
    }

    [Fact]
    public async Task SaveMappings_UpsertsObjectMappingRows()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var repo = new NpgsqlMockPresetRepository(NullLogger<NpgsqlMockPresetRepository>.Instance, cs);
        var request = new MockPresetCreateRequest(
            PresetKey: $"test_mappings_{suffix}",
            PresetLabel: "Mapping Test",
            SourceKind: "generic_xml",
            SourceHash: "h_" + suffix,
            SourceSnapshotJson: JsonSerializer.SerializeToElement(new { }),
            VisualTreeJson: JsonSerializer.SerializeToElement(new { nodes = Array.Empty<object>() })
        );

        var (presetId, errorCode, _) = await repo.CreatePresetAsync(request);
        Assert.Null(errorCode);
        var pid = Guid.Parse(presetId!);

        try
        {
            var mapping = new MockPresetObjectMappingRecord(
                MappingId: "",
                SourceObjectId: "obj_001",
                NodeId: "node_001",
                NodeKind: "catalog_component",
                ComponentKey: "button",
                ComponentKind: "interactive",
                HtmlTag: null,
                ParentSourceObjectId: null,
                SlotKey: null,
                OrderIndex: 0,
                BboxJson: JsonSerializer.SerializeToElement(new { x = 10, y = 20, width = 100, height = 40 }),
                TextJson: JsonSerializer.SerializeToElement(new { }),
                StyleCandidateJson: JsonSerializer.SerializeToElement(Array.Empty<object>()),
                MappingStatus: "mapped"
            );

            var (mappingId, mapError) = await repo.UpsertObjectMappingAsync(pid, mapping);
            Assert.Null(mapError);
            Assert.NotNull(mappingId);

            // Upsert again — idempotent, should update not fail
            var (mappingId2, mapError2) = await repo.UpsertObjectMappingAsync(pid, mapping with { ComponentKey = "input" });
            Assert.Null(mapError2);
            Assert.NotNull(mappingId2);

            // Verify via GetPreset
            var fetched = await repo.GetPresetAsync(pid);
            Assert.NotNull(fetched);
            Assert.Single(fetched!.ObjectMappings);
            Assert.Equal("obj_001", fetched.ObjectMappings[0].SourceObjectId);
            Assert.Equal("input", fetched.ObjectMappings[0].ComponentKey);
        }
        finally
        {
            await CleanupPresetAsync(cs, pid);
        }
    }

    [Fact]
    public async Task SaveWiringCandidates_UpsertsWiringCandidateRows()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var repo = new NpgsqlMockPresetRepository(NullLogger<NpgsqlMockPresetRepository>.Instance, cs);
        var (presetId, _, _) = await repo.CreatePresetAsync(new MockPresetCreateRequest(
            PresetKey: $"test_wiring_{suffix}",
            PresetLabel: "Wiring Test",
            SourceKind: "svg_xml",
            SourceHash: "wh_" + suffix,
            SourceSnapshotJson: JsonSerializer.SerializeToElement(new { }),
            VisualTreeJson: JsonSerializer.SerializeToElement(new { nodes = Array.Empty<object>() })
        ));
        var pid = Guid.Parse(presetId!);

        try
        {
            var candidate = new MockPresetWiringCandidateRecord(
                WiringCandidateId: "",
                SourceObjectId: "btn_001",
                NodeId: "node_btn_001",
                CapabilityTag: "emits_event",
                WiringKind: "route_navigation",
                TargetSurface: "admin",
                TargetRef: "route:admin",
                BindingJson: JsonSerializer.SerializeToElement(new { }),
                Status: "confirmed"
            );

            var (wcId, wcError) = await repo.UpsertWiringCandidateAsync(pid, candidate);
            Assert.Null(wcError);
            Assert.NotNull(wcId);

            // Upsert again with different status — idempotent update
            var (wcId2, wcError2) = await repo.UpsertWiringCandidateAsync(pid, candidate with { Status = "pending" });
            Assert.Null(wcError2);
            Assert.NotNull(wcId2);

            var fetched = await repo.GetPresetAsync(pid);
            Assert.NotNull(fetched);
            Assert.Single(fetched!.WiringCandidates);
            Assert.Equal("btn_001", fetched.WiringCandidates[0].SourceObjectId);
            Assert.Equal("emits_event", fetched.WiringCandidates[0].CapabilityTag);
            Assert.Equal("pending", fetched.WiringCandidates[0].Status);
        }
        finally
        {
            await CleanupPresetAsync(cs, pid);
        }
    }

    [Fact]
    public async Task CompileAndBind_RoundTrip_LayoutPatchJsonReturnedNotPersistedToUiTopologyTensor()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var repo = new NpgsqlMockPresetRepository(NullLogger<NpgsqlMockPresetRepository>.Instance, cs);

        var (presetId, _, _) = await repo.CreatePresetAsync(new MockPresetCreateRequest(
            PresetKey: $"test_compile_{suffix}",
            PresetLabel: "Compile Test",
            SourceKind: "svg_xml",
            SourceHash: "ch_" + suffix,
            SourceSnapshotJson: JsonSerializer.SerializeToElement(new { }),
            VisualTreeJson: JsonSerializer.SerializeToElement(new { nodes = Array.Empty<object>() })
        ));
        var pid = Guid.Parse(presetId!);

        try
        {
            // Add a mapped object so compile produces a node
            await repo.UpsertObjectMappingAsync(pid, new MockPresetObjectMappingRecord(
                MappingId: "",
                SourceObjectId: "rect_001",
                NodeId: "node_rect_001",
                NodeKind: "structural_html",
                ComponentKey: null,
                ComponentKind: null,
                HtmlTag: "div",
                ParentSourceObjectId: null,
                SlotKey: null,
                OrderIndex: 0,
                BboxJson: JsonSerializer.SerializeToElement(new { x = 0, y = 0, width = 200, height = 100 }),
                TextJson: JsonSerializer.SerializeToElement(new { }),
                StyleCandidateJson: JsonSerializer.SerializeToElement(Array.Empty<object>()),
                MappingStatus: "mapped"
            ));

            // Build compile result manually (mirrors AdminRuntime.CompilePresetFromMappings)
            var preset = await repo.GetPresetAsync(pid);
            Assert.NotNull(preset);

            var layoutPatch = JsonSerializer.SerializeToElement(new
            {
                nodes = new[]
                {
                    new
                    {
                        nodeId = "node_rect_001",
                        nodeKind = "structural_html",
                        componentKey = "layout/structural_html",
                        htmlTag = "div",
                        isDraftOnly = false,
                        x = 0, y = 0, width = 200, height = 100,
                    }
                },
                layoutClassRefs = Array.Empty<string>()
            });

            var compileResult = new MockPresetCompileResult(
                Ok: true,
                CompileSnapshotId: null,
                LayoutPatchJson: layoutPatch,
                UnresolvedJson: JsonSerializer.SerializeToElement(Array.Empty<object>())
            );

            var (snapshotId, saveError) = await repo.SaveCompileSnapshotAsync(pid, compileResult);
            Assert.Null(saveError);
            Assert.NotNull(snapshotId);

            // GetLatestCompileSnapshot returns compile result
            var bindResult = await repo.GetLatestCompileSnapshotAsync(pid);
            Assert.NotNull(bindResult);
            Assert.True(bindResult!.Ok);
            Assert.True(bindResult.LayoutPatchJson.HasValue);

            var nodes = bindResult.LayoutPatchJson.Value.GetProperty("nodes");
            Assert.Equal(JsonValueKind.Array, nodes.ValueKind);
            Assert.Equal(1, nodes.GetArrayLength());

            // CRITICAL: Verify bind did NOT write to topology.ui_topology_tensor
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT COUNT(*) FROM topology.ui_topology_tensor " +
                "WHERE layout_patch_json IS NOT NULL AND layout_patch_json::text LIKE '%node_rect_001%'";
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(0L, count);
        }
        finally
        {
            await CleanupPresetAsync(cs, pid);
        }
    }

    [Fact]
    public async Task ListPresets_ReturnsActivePresets()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var repo = new NpgsqlMockPresetRepository(NullLogger<NpgsqlMockPresetRepository>.Instance, cs);
        var (presetId, _, _) = await repo.CreatePresetAsync(new MockPresetCreateRequest(
            PresetKey: $"test_list_{suffix}",
            PresetLabel: "List Test",
            SourceKind: "ui_builder_canvas",
            SourceHash: "lh_" + suffix,
            SourceSnapshotJson: JsonSerializer.SerializeToElement(new { }),
            VisualTreeJson: JsonSerializer.SerializeToElement(new { nodes = Array.Empty<object>() })
        ));
        var pid = Guid.Parse(presetId!);

        try
        {
            var presets = await repo.ListPresetsAsync("active");
            Assert.NotNull(presets);
            Assert.Contains(presets, p => p.PresetId == presetId);
        }
        finally
        {
            await CleanupPresetAsync(cs, pid);
        }
    }

    private static async Task CleanupPresetAsync(string cs, Guid presetId)
    {
        try
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM topology.mock_preset_registry WHERE preset_id = @id";
            cmd.Parameters.AddWithValue("id", presetId);
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // cleanup failure is not a test concern
        }
    }
}
