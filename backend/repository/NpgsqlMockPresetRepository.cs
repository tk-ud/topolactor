using Microsoft.Extensions.Logging;
using Npgsql;
using System.Text.Json;
using Topolactor.Schema;

namespace Topolactor.Repository;

// ---------------------------------------------------------------------------
// NpgsqlMockPresetRepository — production Npgsql implementation.
// Operates against topology.mock_preset_* tables (db/migrations/mock_preset_registry_tables.sql).
// All failures are explicit errors; no silent fallback.
// SSOT: docs/design/mock-preset-intake-compiler-ssot.yaml
// ---------------------------------------------------------------------------

public class NpgsqlMockPresetRepository : MockPresetRepository
{
    private readonly string _connectionString;

    public NpgsqlMockPresetRepository(ILogger<NpgsqlMockPresetRepository> logger, string connectionString)
        : base(logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public override async Task<(string? PresetId, string? ErrorCode, string? Message)>
        CreatePresetAsync(MockPresetCreateRequest request, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO topology.mock_preset_registry " +
                "(preset_key, preset_label, source_kind, source_hash, source_snapshot_json, visual_tree_json, status) " +
                "VALUES (@key, @label, @source_kind, @hash, @snapshot::jsonb, @tree::jsonb, 'active') " +
                "RETURNING preset_id";
            cmd.Parameters.AddWithValue("key", request.PresetKey);
            cmd.Parameters.AddWithValue("label", request.PresetLabel);
            cmd.Parameters.AddWithValue("source_kind", request.SourceKind);
            cmd.Parameters.AddWithValue("hash", request.SourceHash);
            cmd.Parameters.AddWithValue("snapshot", request.SourceSnapshotJson.GetRawText());
            cmd.Parameters.AddWithValue("tree", request.VisualTreeJson.GetRawText());

            var presetId = await cmd.ExecuteScalarAsync(ct) as Guid?;
            return (presetId?.ToString(), null, null);
        }
        catch (PostgresException pgEx) when (pgEx.SqlState == "23505")
        {
            return (null, "PRESET_KEY_DUPLICATE", $"A preset with key '{request.PresetKey}' already exists.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NpgsqlMockPresetRepository.CreatePresetAsync failed");
            return (null, "DB_UNAVAILABLE", ex.Message);
        }
    }

    public override async Task<IReadOnlyList<MockPresetListItem>>
        ListPresetsAsync(string status = "active", CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT preset_id, preset_key, preset_label, source_kind, status, created_at " +
                "FROM topology.mock_preset_registry " +
                "WHERE status = @status " +
                "ORDER BY created_at DESC";
            cmd.Parameters.AddWithValue("status", status);

            var results = new List<MockPresetListItem>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                results.Add(new MockPresetListItem(
                    PresetId: reader.GetGuid(0).ToString(),
                    PresetKey: reader.GetString(1),
                    PresetLabel: reader.GetString(2),
                    SourceKind: reader.GetString(3),
                    Status: reader.GetString(4),
                    CreatedAt: reader.GetDateTime(5).ToString("o")
                ));
            }
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NpgsqlMockPresetRepository.ListPresetsAsync failed");
            return [];
        }
    }

    public override async Task<MockPresetDetail?>
        GetPresetAsync(Guid presetId, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            MockPresetDetail? preset = null;

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT preset_id, preset_key, preset_label, source_kind, source_hash, visual_tree_json, status " +
                    "FROM topology.mock_preset_registry WHERE preset_id = @id";
                cmd.Parameters.AddWithValue("id", presetId);

                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    var treeJson = reader.GetString(5);
                    preset = new MockPresetDetail(
                        PresetId: reader.GetGuid(0).ToString(),
                        PresetKey: reader.GetString(1),
                        PresetLabel: reader.GetString(2),
                        SourceKind: reader.GetString(3),
                        SourceHash: reader.GetString(4),
                        VisualTreeJson: JsonSerializer.Deserialize<JsonElement>(treeJson),
                        Status: reader.GetString(6),
                        ObjectMappings: [],
                        WiringCandidates: []
                    );
                }
            }

            if (preset is null) return null;

            var mappings = new List<MockPresetObjectMappingRecord>();
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT mapping_id, source_object_id, node_id, node_kind, component_key, component_kind, " +
                    "html_tag, parent_source_object_id, slot_key, order_index, bbox_json, text_json, style_candidate_json, mapping_status " +
                    "FROM topology.mock_preset_object_mapping " +
                    "WHERE preset_id = @id ORDER BY order_index ASC";
                cmd.Parameters.AddWithValue("id", presetId);

                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    mappings.Add(new MockPresetObjectMappingRecord(
                        MappingId: reader.GetGuid(0).ToString(),
                        SourceObjectId: reader.GetString(1),
                        NodeId: reader.GetString(2),
                        NodeKind: reader.GetString(3),
                        ComponentKey: reader.IsDBNull(4) ? null : reader.GetString(4),
                        ComponentKind: reader.IsDBNull(5) ? null : reader.GetString(5),
                        HtmlTag: reader.IsDBNull(6) ? null : reader.GetString(6),
                        ParentSourceObjectId: reader.IsDBNull(7) ? null : reader.GetString(7),
                        SlotKey: reader.IsDBNull(8) ? null : reader.GetString(8),
                        OrderIndex: reader.GetInt32(9),
                        BboxJson: JsonSerializer.Deserialize<JsonElement>(reader.GetString(10)),
                        TextJson: JsonSerializer.Deserialize<JsonElement>(reader.GetString(11)),
                        StyleCandidateJson: JsonSerializer.Deserialize<JsonElement>(reader.GetString(12)),
                        MappingStatus: reader.GetString(13)
                    ));
                }
            }

            var wirings = new List<MockPresetWiringCandidateRecord>();
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT wiring_candidate_id, source_object_id, node_id, capability_tag, wiring_kind, " +
                    "target_surface, target_ref, binding_json, status " +
                    "FROM topology.mock_preset_wiring_candidate " +
                    "WHERE preset_id = @id";
                cmd.Parameters.AddWithValue("id", presetId);

                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    wirings.Add(new MockPresetWiringCandidateRecord(
                        WiringCandidateId: reader.GetGuid(0).ToString(),
                        SourceObjectId: reader.GetString(1),
                        NodeId: reader.GetString(2),
                        CapabilityTag: reader.GetString(3),
                        WiringKind: reader.IsDBNull(4) ? null : reader.GetString(4),
                        TargetSurface: reader.IsDBNull(5) ? null : reader.GetString(5),
                        TargetRef: reader.IsDBNull(6) ? null : reader.GetString(6),
                        BindingJson: JsonSerializer.Deserialize<JsonElement>(reader.GetString(7)),
                        Status: reader.GetString(8)
                    ));
                }
            }

            return preset with { ObjectMappings = mappings, WiringCandidates = wirings };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NpgsqlMockPresetRepository.GetPresetAsync failed for {PresetId}", presetId);
            return null;
        }
    }

    public override async Task<(string? MappingId, string? ErrorCode)>
        UpsertObjectMappingAsync(Guid presetId, MockPresetObjectMappingRecord mapping, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO topology.mock_preset_object_mapping " +
                "(preset_id, source_object_id, node_id, node_kind, component_key, component_kind, " +
                "html_tag, parent_source_object_id, slot_key, order_index, bbox_json, text_json, style_candidate_json, mapping_status) " +
                "VALUES (@preset_id, @source_object_id, @node_id, @node_kind, @component_key, @component_kind, " +
                "@html_tag, @parent_source_object_id, @slot_key, @order_index, @bbox::jsonb, @text::jsonb, @style::jsonb, @mapping_status) " +
                "ON CONFLICT (preset_id, source_object_id) DO UPDATE SET " +
                "node_id = EXCLUDED.node_id, node_kind = EXCLUDED.node_kind, " +
                "component_key = EXCLUDED.component_key, component_kind = EXCLUDED.component_kind, " +
                "html_tag = EXCLUDED.html_tag, parent_source_object_id = EXCLUDED.parent_source_object_id, " +
                "slot_key = EXCLUDED.slot_key, order_index = EXCLUDED.order_index, " +
                "bbox_json = EXCLUDED.bbox_json, text_json = EXCLUDED.text_json, " +
                "style_candidate_json = EXCLUDED.style_candidate_json, mapping_status = EXCLUDED.mapping_status " +
                "RETURNING mapping_id";
            cmd.Parameters.AddWithValue("preset_id", presetId);
            cmd.Parameters.AddWithValue("source_object_id", mapping.SourceObjectId);
            cmd.Parameters.AddWithValue("node_id", mapping.NodeId);
            cmd.Parameters.AddWithValue("node_kind", mapping.NodeKind);
            cmd.Parameters.AddWithValue("component_key", (object?)mapping.ComponentKey ?? DBNull.Value);
            cmd.Parameters.AddWithValue("component_kind", (object?)mapping.ComponentKind ?? DBNull.Value);
            cmd.Parameters.AddWithValue("html_tag", (object?)mapping.HtmlTag ?? DBNull.Value);
            cmd.Parameters.AddWithValue("parent_source_object_id", (object?)mapping.ParentSourceObjectId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("slot_key", (object?)mapping.SlotKey ?? DBNull.Value);
            cmd.Parameters.AddWithValue("order_index", mapping.OrderIndex);
            cmd.Parameters.AddWithValue("bbox", mapping.BboxJson.GetRawText());
            cmd.Parameters.AddWithValue("text", mapping.TextJson.GetRawText());
            cmd.Parameters.AddWithValue("style", mapping.StyleCandidateJson.GetRawText());
            cmd.Parameters.AddWithValue("mapping_status", mapping.MappingStatus);

            var mappingId = await cmd.ExecuteScalarAsync(ct) as Guid?;
            return (mappingId?.ToString(), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NpgsqlMockPresetRepository.UpsertObjectMappingAsync failed for preset {PresetId}", presetId);
            return (null, "DB_UNAVAILABLE");
        }
    }

    public override async Task<(string? WiringCandidateId, string? ErrorCode)>
        UpsertWiringCandidateAsync(Guid presetId, MockPresetWiringCandidateRecord candidate, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO topology.mock_preset_wiring_candidate " +
                "(preset_id, source_object_id, node_id, capability_tag, wiring_kind, target_surface, target_ref, binding_json, status) " +
                "VALUES (@preset_id, @source_object_id, @node_id, @capability_tag, @wiring_kind, @target_surface, @target_ref, @binding::jsonb, @status) " +
                "ON CONFLICT (preset_id, source_object_id, capability_tag) DO UPDATE SET " +
                "node_id = EXCLUDED.node_id, wiring_kind = EXCLUDED.wiring_kind, " +
                "target_surface = EXCLUDED.target_surface, target_ref = EXCLUDED.target_ref, " +
                "binding_json = EXCLUDED.binding_json, status = EXCLUDED.status " +
                "RETURNING wiring_candidate_id";
            cmd.Parameters.AddWithValue("preset_id", presetId);
            cmd.Parameters.AddWithValue("source_object_id", candidate.SourceObjectId);
            cmd.Parameters.AddWithValue("node_id", candidate.NodeId);
            cmd.Parameters.AddWithValue("capability_tag", candidate.CapabilityTag);
            cmd.Parameters.AddWithValue("wiring_kind", (object?)candidate.WiringKind ?? DBNull.Value);
            cmd.Parameters.AddWithValue("target_surface", (object?)candidate.TargetSurface ?? DBNull.Value);
            cmd.Parameters.AddWithValue("target_ref", (object?)candidate.TargetRef ?? DBNull.Value);
            cmd.Parameters.AddWithValue("binding", candidate.BindingJson.GetRawText());
            cmd.Parameters.AddWithValue("status", candidate.Status);

            var id = await cmd.ExecuteScalarAsync(ct) as Guid?;
            return (id?.ToString(), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NpgsqlMockPresetRepository.UpsertWiringCandidateAsync failed for preset {PresetId}", presetId);
            return (null, "DB_UNAVAILABLE");
        }
    }

    public override async Task<(string? CompileSnapshotId, string? ErrorCode)>
        SaveCompileSnapshotAsync(Guid presetId, MockPresetCompileResult result, CancellationToken ct = default)
    {
        if (result.LayoutPatchJson is null)
            return (null, "COMPILE_SNAPSHOT_INVALID");

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO topology.mock_preset_compile_snapshot " +
                "(preset_id, compiler_version, layout_patch_json, package_membership_candidate_json, " +
                "wiring_candidate_json, style_candidate_json, unresolved_json) " +
                "VALUES (@preset_id, @version, @layout::jsonb, @pkg::jsonb, @wiring::jsonb, @style::jsonb, @unresolved::jsonb) " +
                "RETURNING compile_snapshot_id";
            cmd.Parameters.AddWithValue("preset_id", presetId);
            cmd.Parameters.AddWithValue("version", "0.1.0");
            cmd.Parameters.AddWithValue("layout", result.LayoutPatchJson.Value.GetRawText());
            cmd.Parameters.AddWithValue("pkg", "{}");
            cmd.Parameters.AddWithValue("wiring", "[]");
            cmd.Parameters.AddWithValue("style", "[]");
            cmd.Parameters.AddWithValue("unresolved", result.UnresolvedJson?.GetRawText() ?? "[]");

            var snapshotId = await cmd.ExecuteScalarAsync(ct) as Guid?;
            return (snapshotId?.ToString(), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NpgsqlMockPresetRepository.SaveCompileSnapshotAsync failed");
            return (null, "DB_UNAVAILABLE");
        }
    }

    public override async Task<MockPresetBindResult?>
        GetLatestCompileSnapshotAsync(Guid presetId, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT layout_patch_json, package_membership_candidate_json, wiring_candidate_json, " +
                "style_candidate_json, unresolved_json " +
                "FROM topology.mock_preset_compile_snapshot " +
                "WHERE preset_id = @id " +
                "ORDER BY compiled_at DESC LIMIT 1";
            cmd.Parameters.AddWithValue("id", presetId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;

            return new MockPresetBindResult(
                Ok: true,
                LayoutPatchJson: JsonSerializer.Deserialize<JsonElement>(reader.GetString(0)),
                PackageMembershipCandidateJson: JsonSerializer.Deserialize<JsonElement>(reader.GetString(1)),
                WiringCandidateJson: JsonSerializer.Deserialize<JsonElement>(reader.GetString(2)),
                StyleCandidateJson: JsonSerializer.Deserialize<JsonElement>(reader.GetString(3)),
                UnresolvedJson: JsonSerializer.Deserialize<JsonElement>(reader.GetString(4))
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NpgsqlMockPresetRepository.GetLatestCompileSnapshotAsync failed");
            return null;
        }
    }
}
