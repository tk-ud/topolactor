using System.Text.Json;
using Npgsql;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// Projects promoted public.manifest rows into hubs.topology_manifests (import/runtime canonical).
/// </summary>
public static class ManifestCanonicalProjection
{
    public const string HubGroupingEntryType = "hub_grouping";
    public const string ScreenDataShapeEntryType = "screen_data_shape";

    public static (Guid? HubId, string? ManifestKey) ExtractHubGrouping(IReadOnlyList<JsonElement> topology)
    {
        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("type", out var typeEl) ||
                typeEl.ValueKind != JsonValueKind.String ||
                !string.Equals(typeEl.GetString(), HubGroupingEntryType, StringComparison.Ordinal)) continue;

            Guid? hubId = null;
            if (entry.TryGetProperty("hubId", out var hubEl) &&
                hubEl.ValueKind == JsonValueKind.String &&
                Guid.TryParse(hubEl.GetString(), out var parsedHub))
            {
                hubId = parsedHub;
            }

            string? manifestKey = null;
            if (entry.TryGetProperty("manifestKey", out var keyEl) &&
                keyEl.ValueKind == JsonValueKind.String)
            {
                manifestKey = keyEl.GetString()?.Trim();
            }

            return (hubId, string.IsNullOrWhiteSpace(manifestKey) ? null : manifestKey);
        }

        return (null, null);
    }

    public static string BuildDefaultManifestKey(ManifestTopologySummary summary)
    {
        if (summary.DispatcherMapping is { } axes)
        {
            return $"{axes.Role}.{axes.Target}.{axes.Layer}.{axes.Action}";
        }

        return "manifest";
    }

    public static IReadOnlyList<JsonElement> MergeTopologyEntry(
        IReadOnlyList<JsonElement> topology,
        string entryType,
        JsonElement entryBody)
    {
        var list = new List<JsonElement>();
        var replaced = false;
        foreach (var entry in topology)
        {
            if (!replaced &&
                entry.ValueKind == JsonValueKind.Object &&
                entry.TryGetProperty("type", out var typeEl) &&
                typeEl.ValueKind == JsonValueKind.String &&
                string.Equals(typeEl.GetString(), entryType, StringComparison.Ordinal))
            {
                list.Add(entryBody);
                replaced = true;
            }
            else
            {
                list.Add(entry);
            }
        }

        if (!replaced) list.Add(entryBody);
        return list;
    }

    public static async Task<ValidationError?> ProjectOnPromoteAsync(
        NpgsqlConnection conn,
        ManifestDetailRecord detail,
        CancellationToken ct)
    {
        var summary = ManifestTopologyValidator.ExtractSummary(detail.Topology);
        var (hubId, manifestKey) = ExtractHubGrouping(detail.Topology);

        if (!hubId.HasValue && detail.RelationRegistryId.HasValue)
        {
            await using var hubCmd = conn.CreateCommand();
            hubCmd.CommandText =
                "SELECT hub_id FROM hubs.hub WHERE relation_registry_id = @rel ORDER BY created_at ASC LIMIT 1";
            hubCmd.Parameters.AddWithValue("rel", detail.RelationRegistryId.Value);
            var scalar = await hubCmd.ExecuteScalarAsync(ct);
            if (scalar is Guid g) hubId = g;
        }

        if (!hubId.HasValue)
        {
            await using var fallbackCmd = conn.CreateCommand();
            fallbackCmd.CommandText = "SELECT hub_id FROM hubs.hub ORDER BY created_at ASC LIMIT 1";
            var scalar = await fallbackCmd.ExecuteScalarAsync(ct);
            if (scalar is Guid g) hubId = g;
        }

        if (!hubId.HasValue)
        {
            return new ValidationError(
                "HUB_GROUPING_REQUIRED",
                "Promote requires hub_grouping.hubId on the manifest draft, or at least one row in hubs.hub.");
        }

        manifestKey ??= PromotionManifestValidator.ExtractMetadataDto(detail.Topology)?.ManifestKey;
        manifestKey ??= BuildDefaultManifestKey(summary);

        // Fail closed before either canonical projection table is mutated. The repository
        // transaction remains the final rollback boundary, but table_ref mismatch is a
        // validation failure and must not leave a topology_manifests partial write even when
        // this projection helper is called independently.
        var wiringPreflightError = await ValidateWiringTableRefAsync(conn, detail, ct);
        if (wiringPreflightError is not null) return wiringPreflightError;

        var topologyJsonb = JsonSerializer.Serialize(new
        {
            manifest_id = detail.ManifestId,
            entries = detail.Topology,
        });

        await using var upsert = conn.CreateCommand();
        upsert.CommandText =
            "INSERT INTO hubs.topology_manifests " +
            "(topology_manifest_id, hub_id, manifest_key, status, topology_jsonb) " +
            "VALUES (@id, @hub, @key, 'active', @topo::jsonb) " +
            "ON CONFLICT (topology_manifest_id) DO UPDATE SET " +
            "hub_id = EXCLUDED.hub_id, " +
            "manifest_key = EXCLUDED.manifest_key, " +
            "status = 'active', " +
            "topology_jsonb = EXCLUDED.topology_jsonb, " +
            "updated_at = now()";
        upsert.Parameters.AddWithValue("id", detail.ManifestId);
        upsert.Parameters.AddWithValue("hub", hubId.Value);
        upsert.Parameters.AddWithValue("key", manifestKey);
        upsert.Parameters.AddWithValue("topo", topologyJsonb);
        await upsert.ExecuteNonQueryAsync(ct);

        var wiringError = await TryProjectWiringAsync(conn, detail, ct);
        return wiringError;
    }

    /// <summary>
    /// Projects wiring from screen_data_shape.tableRef into topology.wiring_physical_to_package.
    /// Returns WIRING_TABLE_REF_NOT_FOUND if tableRef is present but not in topology.physical_tables.
    /// Returns null on success or when no tableRef is specified (no wiring intent).
    /// Never silently skips a tableRef mismatch.
    /// </summary>
    private static async Task<ValidationError?> TryProjectWiringAsync(
        NpgsqlConnection conn,
        ManifestDetailRecord detail,
        CancellationToken ct)
    {
        var tableRef = ExtractScreenDataShapeTableRef(detail.Topology);
        if (string.IsNullOrWhiteSpace(tableRef)) return null;

        var physicalTableId = await FindPhysicalTableIdAsync(conn, tableRef, ct);
        if (!physicalTableId.HasValue)
        {
            // ProjectOnPromoteAsync performs this preflight before any canonical write. Keep
            // this guard here as well so future callers cannot silently skip a broken ref.
            return WiringTableRefNotFound(tableRef);
        }

        var packageId = detail.ManifestId;
        var wiringDef = JsonSerializer.Serialize(new
        {
            manifest_id = detail.ManifestId,
            source = "manifest_promote",
            table_ref = tableRef,
        });

        await using var exists = conn.CreateCommand();
        exists.CommandText =
            "SELECT 1 FROM topology.wiring_physical_to_package " +
            "WHERE physical_table_id = @pt AND package_id = @pkg AND active = true LIMIT 1";
        exists.Parameters.AddWithValue("pt", physicalTableId.Value);
        exists.Parameters.AddWithValue("pkg", packageId);
        if (await exists.ExecuteScalarAsync(ct) is not null) return null;

        await using var insert = conn.CreateCommand();
        insert.CommandText =
            "INSERT INTO topology.wiring_physical_to_package " +
            "(physical_table_id, package_id, wiring_def, active) " +
            "VALUES (@pt, @pkg, @def::jsonb, true)";
        insert.Parameters.AddWithValue("pt", physicalTableId.Value);
        insert.Parameters.AddWithValue("pkg", packageId);
        insert.Parameters.AddWithValue("def", wiringDef);
        await insert.ExecuteNonQueryAsync(ct);
        return null;
    }

    private static async Task<ValidationError?> ValidateWiringTableRefAsync(
        NpgsqlConnection conn,
        ManifestDetailRecord detail,
        CancellationToken ct)
    {
        var tableRef = ExtractScreenDataShapeTableRef(detail.Topology);
        if (string.IsNullOrWhiteSpace(tableRef)) return null;

        return await FindPhysicalTableIdAsync(conn, tableRef, ct) is null
            ? WiringTableRefNotFound(tableRef)
            : null;
    }

    private static string? ExtractScreenDataShapeTableRef(IReadOnlyList<JsonElement> topology)
    {
        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("type", out var typeEl) ||
                typeEl.ValueKind != JsonValueKind.String ||
                !string.Equals(typeEl.GetString(), ScreenDataShapeEntryType, StringComparison.Ordinal)) continue;

            return ExtractTableRef(entry);
        }

        return null;
    }

    private static async Task<long?> FindPhysicalTableIdAsync(
        NpgsqlConnection conn,
        string tableRef,
        CancellationToken ct)
    {
        await using var lookup = conn.CreateCommand();
        lookup.CommandText =
            "SELECT physical_table_id FROM topology.physical_tables " +
            "WHERE table_ref = @ref AND active = true LIMIT 1";
        lookup.Parameters.AddWithValue("ref", tableRef);
        return await lookup.ExecuteScalarAsync(ct) is long physicalTableId ? physicalTableId : null;
    }

    private static ValidationError WiringTableRefNotFound(string tableRef) =>
        new(
            "WIRING_TABLE_REF_NOT_FOUND",
            $"table_ref '{tableRef}' is not registered in topology.physical_tables (active=true). " +
            "Register the physical table before promoting this manifest, or remove the tableRef intent.");

    public static string? ExtractTableRef(JsonElement shapeEntry)
    {
        if (shapeEntry.TryGetProperty("tableRef", out var refEl) &&
            refEl.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(refEl.GetString()))
        {
            return refEl.GetString()!.Trim();
        }

        if (shapeEntry.TryGetProperty("dbTableName", out var legacyEl) &&
            legacyEl.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(legacyEl.GetString()))
        {
            return legacyEl.GetString()!.Trim();
        }

        return null;
    }

    public static string? ExtractScreenOperationKind(IReadOnlyList<JsonElement> topology)
    {
        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("type", out var typeEl) ||
                typeEl.ValueKind != JsonValueKind.String ||
                !string.Equals(typeEl.GetString(), ScreenDataShapeEntryType, StringComparison.Ordinal)) continue;

            if (entry.TryGetProperty("screenOperationKind", out var kindEl) &&
                kindEl.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(kindEl.GetString()))
            {
                return kindEl.GetString()!.Trim();
            }
        }

        return null;
    }

    public static IReadOnlyList<string> ExtractScreenOperationKinds(IReadOnlyList<JsonElement> topology)
    {
        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("type", out var typeEl) ||
                typeEl.ValueKind != JsonValueKind.String ||
                !string.Equals(typeEl.GetString(), ScreenDataShapeEntryType, StringComparison.Ordinal)) continue;

            if (entry.TryGetProperty("screenOperationKinds", out var kindsEl) &&
                kindsEl.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>();
                foreach (var item in kindsEl.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(item.GetString()))
                        list.Add(item.GetString()!.Trim());
                }
                if (list.Count > 0) return list;
            }

            var single = ExtractScreenOperationKind(topology);
            if (!string.IsNullOrWhiteSpace(single))
                return new[] { single! };
        }

        return Array.Empty<string>();
    }

    public static IReadOnlyList<JsonElement> WithDispatcherMapping(
        IReadOnlyList<JsonElement> topology,
        string role,
        string target,
        string layer,
        string action,
        string runtimeDestination)
    {
        var rebuilt = ManifestTopologyValidator.BuildTopology(
            role,
            target,
            layer,
            action,
            runtimeDestination,
            projectionDefinition: null);

        var dispatcher = rebuilt.First(e =>
            e.ValueKind == JsonValueKind.Object &&
            e.TryGetProperty("type", out var t) &&
            t.GetString() == "dispatcher_mapping");

        var runtime = rebuilt.FirstOrDefault(e =>
            e.ValueKind == JsonValueKind.Object &&
            e.TryGetProperty("type", out var t) &&
            t.GetString() == "runtime_mapping");

        var list = new List<JsonElement>();
        var replacedDispatcher = false;
        var replacedRuntime = false;
        foreach (var entry in topology)
        {
            if (entry.ValueKind == JsonValueKind.Object &&
                entry.TryGetProperty("type", out var typeEl) &&
                typeEl.ValueKind == JsonValueKind.String)
            {
                var type = typeEl.GetString();
                if (type == "dispatcher_mapping")
                {
                    list.Add(dispatcher);
                    replacedDispatcher = true;
                    continue;
                }

                if (type == "runtime_mapping" && runtime.ValueKind != JsonValueKind.Undefined)
                {
                    list.Add(runtime);
                    replacedRuntime = true;
                    continue;
                }
            }

            list.Add(entry);
        }

        if (!replacedDispatcher) list.Insert(0, dispatcher);
        if (!replacedRuntime && runtime.ValueKind != JsonValueKind.Undefined && !replacedRuntime)
            list.Add(runtime);

        return list;
    }
}
