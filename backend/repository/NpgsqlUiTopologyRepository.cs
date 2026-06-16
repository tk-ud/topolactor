using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using System.Text.Json;
using System.Text.RegularExpressions;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// Production Npgsql implementation of UiTopologyRepository.
/// Operates against the canonical topology.* tables defined in db/ui_topology_tables.sql.
/// All table references use topology schema prefix (topology.components_bucket, etc.).
///
/// PromoteBucketItemAsync wraps the entire promotion pipeline in a single
/// NpgsqlConnection + NpgsqlTransaction so no partial state can remain on failure.
/// </summary>
public class NpgsqlUiTopologyRepository : UiTopologyRepository
{
    private static readonly Regex CssTokenYamlRegex = new(@"^\s{4}([a-z0-9_.-]+):\s*$", RegexOptions.Compiled | RegexOptions.Multiline);
    private readonly ILogger<NpgsqlUiTopologyRepository> _npgsqlLogger;

    public NpgsqlUiTopologyRepository(
        ILogger<NpgsqlUiTopologyRepository> logger,
        string connectionString)
        : base(NullLogger<UiTopologyRepository>.Instance, connectionString)
    {
        _npgsqlLogger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<IReadOnlyList<UiComponentBucketRecord>> ListBucketItemsAsync(
        string status = "bucketed",
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT bucket_item_id, component_key, source_path, component_kind, status " +
            "FROM topology.components_bucket " +
            "WHERE status = @status " +
            "ORDER BY created_at ASC";
        cmd.Parameters.AddWithValue("status", status);

        var records = new List<UiComponentBucketRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            records.Add(new UiComponentBucketRecord(
                BucketItemId:  reader.GetGuid(0),
                ComponentKey:  reader.GetString(1),
                SourcePath:    reader.GetString(2),
                ComponentKind: reader.GetString(3),
                Status:        reader.GetString(4)
            ));
        }

        return records;
    }

    public override async Task<UiComponentBucketCreateResult> CreateBucketItemAsync(
        string componentKey,
        string sourcePath,
        string componentKind,
        string? metadataJson = null,
        CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO topology.components_bucket (component_key, source_path, component_kind, metadata_json) " +
                "VALUES (@key, @path, @kind, @metadata::jsonb) " +
                "RETURNING bucket_item_id, component_key, source_path, component_kind, status";
            cmd.Parameters.AddWithValue("key", componentKey);
            cmd.Parameters.AddWithValue("path", sourcePath);
            cmd.Parameters.AddWithValue("kind", componentKind);
            cmd.Parameters.AddWithValue("metadata", metadataJson ?? "{}");

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            var record = new UiComponentBucketRecord(
                BucketItemId: reader.GetGuid(0),
                ComponentKey: reader.GetString(1),
                SourcePath: reader.GetString(2),
                ComponentKind: reader.GetString(3),
                Status: reader.GetString(4)
            );
            return new UiComponentBucketCreateResult(UiComponentBucketCreateCode.Success, record);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return new UiComponentBucketCreateResult(
                UiComponentBucketCreateCode.ConstraintViolation,
                null,
                "BUCKET_ALREADY_EXISTS",
                "A bucket item with the same componentKey and sourcePath already exists.");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidTextRepresentation
            || ex.SqlState == PostgresErrorCodes.InvalidParameterValue
            || ex.MessageText.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return new UiComponentBucketCreateResult(
                UiComponentBucketCreateCode.MalformedMetadataJson,
                null,
                "MALFORMED_METADATA_JSON",
                "metadataJson must be valid JSON.");
        }
        catch (Exception ex)
        {
            _npgsqlLogger.LogError(ex, "CreateBucketItemAsync failed.");
            return new UiComponentBucketCreateResult(
                UiComponentBucketCreateCode.DbUnavailable,
                null,
                "DB_UNAVAILABLE",
                ex.Message);
        }
    }

    public override async Task<IReadOnlyList<PromotedPaletteEntryDto>> ListPromotedPaletteEntriesAsync(
        CancellationToken ct = default)
    {
        var records = new List<PromotedPaletteEntryDto>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT c.component_key, c.component_kind, c.component_id, p.package_id, l.layout_id, w.wiring_id, t.tensor_id, t.route_key " +
            "FROM topology.ui_topology_tensor t " +
            "JOIN topology.ui_component_package p ON p.package_id = t.package_id " +
            "JOIN topology.components_layout_design l ON l.layout_id = t.layout_id " +
            "JOIN topology.ui_wiring_registry w ON w.wiring_id = t.wiring_id " +
            "JOIN topology.ui_package_component_map m ON m.package_id = p.package_id " +
            "JOIN topology.ui_component_registry c ON c.component_id = m.component_id " +
            "ORDER BY t.created_at DESC";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            records.Add(new PromotedPaletteEntryDto(
                ComponentKey: reader.GetString(0),
                ComponentKind: reader.GetString(1),
                ComponentId: reader.GetGuid(2).ToString(),
                PackageId: reader.GetGuid(3).ToString(),
                LayoutId: reader.GetGuid(4).ToString(),
                WiringId: reader.GetGuid(5).ToString(),
                TensorId: reader.GetGuid(6).ToString(),
                RouteKey: reader.GetString(7)
            ));
        }
        return records;
    }

    public override async Task<LayoutTensorContextDto?> ResolveLayoutTensorContextAsync(
        Guid layoutId,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT t.package_id, t.route_key,
                   COALESCE(t.layout_patch_json->'layoutClassRefs', '[]'::jsonb)::text AS root_class_refs
            FROM topology.ui_topology_tensor t
            WHERE t.layout_id = @layoutId
            LIMIT 2
            """;
        cmd.Parameters.AddWithValue("layoutId", layoutId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        string? routeKey = null;
        Guid? packageId = null;
        string? rootClassRefsJson = null;
        var rowCount = 0;

        while (await reader.ReadAsync(ct))
        {
            rowCount++;
            if (rowCount == 1)
            {
                packageId = reader.GetGuid(0);
                routeKey = reader.GetString(1);
                rootClassRefsJson = reader.IsDBNull(2) ? "[]" : reader.GetString(2);
            }
            if (rowCount == 2)
            {
                throw new InvalidOperationException(
                    $"LAYOUT_NODES_AMBIGUOUS_SELECTOR: multiple tensor rows for layout_id='{layoutId}'. " +
                    "Cannot safely resolve layout context without a disambiguating selector (route_key or package_id).");
            }
        }

        if (!packageId.HasValue || routeKey is null)
            return null;

        var rootLayoutClassRefs = ParseStringArrayJson(rootClassRefsJson);
        return new LayoutTensorContextDto(packageId.Value, routeKey, rootLayoutClassRefs);
    }

    private static IReadOnlyList<string> ParseStringArrayJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
                return [];
            var list = new List<string>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
                }
            }
            return list;
        }
        catch
        {
            return [];
        }
    }

    public override async Task<IReadOnlyList<LayoutCandidateDto>> ListLayoutCandidatesAsync(
        CancellationToken ct = default)
    {
        var records = new List<LayoutCandidateDto>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT l.layout_id, l.layout_key, t.route_key, l.layout_kind, " +
            "COALESCE(array_agg(DISTINCT t.slot_key) FILTER (WHERE t.slot_key IS NOT NULL AND t.slot_key <> ''), ARRAY[]::text[]) AS slot_keys " +
            "FROM topology.ui_topology_tensor t " +
            "JOIN topology.components_layout_design l ON l.layout_id = t.layout_id " +
            "GROUP BY l.layout_id, l.layout_key, t.route_key, l.layout_kind " +
            "ORDER BY t.route_key, l.layout_key";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var slotKeys = reader.IsDBNull(4)
                ? Array.Empty<string>()
                : reader.GetFieldValue<string[]>(4);
            records.Add(new LayoutCandidateDto(
                LayoutId: reader.GetGuid(0).ToString(),
                LayoutKey: reader.GetString(1),
                RouteKey: reader.GetString(2),
                LayoutKind: reader.GetString(3),
                SlotKeys: slotKeys
            ));
        }
        return records;
    }

    public override async Task<PackageGenerateResult> GenerateFromBucketAsync(
        Guid bucketItemId,
        string routeKey,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "UPDATE topology.components_bucket SET status = 'packaging', updated_at = now() " +
            "WHERE bucket_item_id = @id AND status = 'bucketed' RETURNING bucket_item_id";
        cmd.Parameters.AddWithValue("id", bucketItemId);

        var updated = await cmd.ExecuteScalarAsync(ct);
        if (updated is null)
        {
            await using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT status FROM topology.components_bucket WHERE bucket_item_id = @id";
            checkCmd.Parameters.AddWithValue("id", bucketItemId);
            var existing = await checkCmd.ExecuteScalarAsync(ct);
            return existing is null
                ? new PackageGenerateResult(PackageGenerateCode.NotFound, null, null, null, null, null, "NOT_FOUND", $"Bucket item {bucketItemId} not found.")
                : new PackageGenerateResult(PackageGenerateCode.NotBucketed, null, null, null, null, null, "NOT_BUCKETED", $"Bucket item {bucketItemId} is in status '{existing}', expected 'bucketed'.");
        }

        return new PackageGenerateResult(PackageGenerateCode.Success, null, null, null, null, null);
    }
/// <summary>
    /// Promotes a bucket item to ui_topology_tensor within a single transaction.
    /// All INSERTs + status updates are atomic: on any failure the transaction rolls back
    /// and no partial rows remain in any registry table.
    /// </summary>
    public override async Task<PackageGenerateResult> PromoteBucketItemAsync(
        Guid bucketItemId,
        string routeKey,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            // 1. Validate status precondition: packaging (already generated)
            await using var transitionCmd = conn.CreateCommand();
            transitionCmd.Transaction = tx;
            transitionCmd.CommandText =
                "SELECT component_key, source_path, component_kind FROM topology.components_bucket " +
                "WHERE bucket_item_id = @id AND status = 'packaging'";
            transitionCmd.Parameters.AddWithValue("id", bucketItemId);

            string? componentKey = null;
            string? sourcePath = null;
            string? componentKind = null;

            await using (var r = await transitionCmd.ExecuteReaderAsync(ct))
            {
                if (await r.ReadAsync(ct))
                {
                    componentKey  = r.GetString(0);
                    sourcePath    = r.GetString(1);
                    componentKind = r.GetString(2);
                }
            }

            if (componentKey is null)
            {
                // Either not found or not in packaging status — check which
                await using var checkCmd = conn.CreateCommand();
                checkCmd.Transaction = tx;
                checkCmd.CommandText =
                    "SELECT status FROM topology.components_bucket WHERE bucket_item_id = @id";
                checkCmd.Parameters.AddWithValue("id", bucketItemId);

                var existing = await checkCmd.ExecuteScalarAsync(ct);
                await tx.RollbackAsync(ct);

                return existing is null
                    ? new PackageGenerateResult(PackageGenerateCode.NotFound, null, null, null, null, null,
                        "NOT_FOUND", $"Bucket item {bucketItemId} not found.")
                    : new PackageGenerateResult(PackageGenerateCode.NotBucketed, null, null, null, null, null,
                        "NOT_BUCKETED",
                        $"Bucket item {bucketItemId} is in status '{existing}', expected 'packaging'.");
            }

            // 2. INSERT topology.ui_component_registry
            await using var compCmd = conn.CreateCommand();
            compCmd.Transaction = tx;
            compCmd.CommandText =
                "INSERT INTO topology.ui_component_registry (component_key, component_kind, source_path) " +
                "VALUES (@key, @kind, @path) RETURNING component_id";
            compCmd.Parameters.AddWithValue("key", componentKey);
            compCmd.Parameters.AddWithValue("kind", componentKind!);
            compCmd.Parameters.AddWithValue("path", sourcePath!);
            var componentId = (Guid)(await compCmd.ExecuteScalarAsync(ct))!;

            // 3. INSERT topology.ui_component_package
            var packageKey = $"{routeKey}:{componentKey}:pkg";
            await using var pkgCmd = conn.CreateCommand();
            pkgCmd.Transaction = tx;
            pkgCmd.CommandText =
                "INSERT INTO topology.ui_component_package (package_key, package_kind) " +
                "VALUES (@key, @kind) RETURNING package_id";
            pkgCmd.Parameters.AddWithValue("key", packageKey);
            pkgCmd.Parameters.AddWithValue("kind", componentKind!);
            var packageId = (Guid)(await pkgCmd.ExecuteScalarAsync(ct))!;

            // 4. INSERT topology.ui_package_component_map
            await using var mapCmd = conn.CreateCommand();
            mapCmd.Transaction = tx;
            mapCmd.CommandText =
                "INSERT INTO topology.ui_package_component_map (package_id, component_id) " +
                "VALUES (@packageId, @componentId)";
            mapCmd.Parameters.AddWithValue("packageId", packageId);
            mapCmd.Parameters.AddWithValue("componentId", componentId);
            await mapCmd.ExecuteNonQueryAsync(ct);

            // 5. INSERT topology.components_layout_design
            var layoutKey = $"{routeKey}:{componentKey}:layout";
            await using var layoutCmd = conn.CreateCommand();
            layoutCmd.Transaction = tx;
            layoutCmd.CommandText =
                "INSERT INTO topology.components_layout_design (layout_key, layout_kind) " +
                "VALUES (@key, @kind) RETURNING layout_id";
            layoutCmd.Parameters.AddWithValue("key", layoutKey);
            layoutCmd.Parameters.AddWithValue("kind", componentKind!);
            var layoutId = (Guid)(await layoutCmd.ExecuteScalarAsync(ct))!;

            // 6. INSERT topology.ui_wiring_registry
            var wiringKey = $"{routeKey}:{componentKey}:wiring";
            await using var wiringCmd = conn.CreateCommand();
            wiringCmd.Transaction = tx;
            wiringCmd.CommandText =
                "INSERT INTO topology.ui_wiring_registry (wiring_key, wiring_kind, target_surface) " +
                "VALUES (@key, @kind, 'route') RETURNING wiring_id";
            wiringCmd.Parameters.AddWithValue("key", wiringKey);
            wiringCmd.Parameters.AddWithValue("kind", componentKind!);
            var wiringId = (Guid)(await wiringCmd.ExecuteScalarAsync(ct))!;

            // 7. INSERT topology.ui_topology_tensor
            await using var tensorCmd = conn.CreateCommand();
            tensorCmd.Transaction = tx;
            tensorCmd.CommandText =
                "INSERT INTO topology.ui_topology_tensor (route_key, package_id, layout_id, wiring_id) " +
                "VALUES (@routeKey, @packageId, @layoutId, @wiringId) RETURNING tensor_id";
            tensorCmd.Parameters.AddWithValue("routeKey", routeKey);
            tensorCmd.Parameters.AddWithValue("packageId", packageId);
            tensorCmd.Parameters.AddWithValue("layoutId", layoutId);
            tensorCmd.Parameters.AddWithValue("wiringId", wiringId);
            var tensorId = (Guid)(await tensorCmd.ExecuteScalarAsync(ct))!;

            // 8. UPDATE status packaging -> promoted (must affect exactly 1 row)
            await using var promoteCmd = conn.CreateCommand();
            promoteCmd.Transaction = tx;
            promoteCmd.CommandText =
                "UPDATE topology.components_bucket " +
                "SET status = 'promoted', updated_at = now() " +
                "WHERE bucket_item_id = @id AND status = 'packaging'";
            promoteCmd.Parameters.AddWithValue("id", bucketItemId);
            var rows = await promoteCmd.ExecuteNonQueryAsync(ct);

            if (rows != 1)
            {
                await tx.RollbackAsync(ct);
                _npgsqlLogger.LogError(
                    "NpgsqlUiTopologyRepository.PromoteBucketItemAsync: final promoted update affected {Rows} rows for item {Id}.",
                    rows, bucketItemId);
                return new PackageGenerateResult(
                    PackageGenerateCode.PromotionFailed, null, null, null, null, null,
                    "PROMOTION_FAILED",
                    "Final status update to 'promoted' did not affect exactly one row.");
            }

            await tx.CommitAsync(ct);

            _npgsqlLogger.LogInformation(
                "NpgsqlUiTopologyRepository.PromoteBucketItemAsync: committed tensorId={TensorId}, bucketItemId={Id}.",
                tensorId, bucketItemId);

            return new PackageGenerateResult(
                PackageGenerateCode.Success,
                tensorId, componentId, packageId, layoutId, wiringId);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            await tx.RollbackAsync(ct);
            _npgsqlLogger.LogWarning(ex,
                "NpgsqlUiTopologyRepository.PromoteBucketItemAsync: constraint violation for item {Id}.", bucketItemId);
            return new PackageGenerateResult(
                PackageGenerateCode.ConstraintViolation, null, null, null, null, null,
                "CONSTRAINT_VIOLATION",
                "A registry key derived from this bucket item already exists.");
        }
        catch (Exception ex)
        {
            try { await tx.RollbackAsync(ct); } catch { /* best-effort */ }
            _npgsqlLogger.LogError(ex,
                "NpgsqlUiTopologyRepository.PromoteBucketItemAsync: DB error for item {Id}.", bucketItemId);
            return new PackageGenerateResult(
                PackageGenerateCode.DbUnavailable, null, null, null, null, null,
                "DB_UNAVAILABLE", "Repository unavailable during package promotion.");
        }
    }

    private static HashSet<string> LoadCssTokenVocabulary()
    {
        var overridePath = Environment.GetEnvironmentVariable("TOPOLACTOR_CSS_DICTIONARY_SSOT_PATH");
        string path;
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            path = Path.GetFullPath(overridePath!);
        }
        else
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            path = "";
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "docs", "design", "css-dictionary-ssot.yaml");
                if (File.Exists(candidate)) { path = candidate; break; }
                dir = dir.Parent;
            }
            if (string.IsNullOrWhiteSpace(path))
                return [];
        }
        if (!File.Exists(path)) return [];
        var text = File.ReadAllText(path);
        var lines = text.Split('\n');
        var inTokens = false;
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in lines)
        {
            var line = raw.Replace("\r", "");
            if (!inTokens)
            {
                if (line.Trim() == "tokens:")
                    inTokens = true;
                continue;
            }
            if (line.StartsWith("  ") && !line.StartsWith("    "))
                break;
            var m = CssTokenYamlRegex.Match(line);
            if (m.Success) tokens.Add(m.Groups[1].Value);
        }
        return tokens;
    }

    private static readonly Regex TopologyLayoutClassKeyYamlRegex =
        new(@"^    ([a-z][a-z0-9_.]+):\s*$", RegexOptions.Compiled);

    private static HashSet<string> LoadTopologyLayoutClassVocabulary()
    {
        var overridePath = Environment.GetEnvironmentVariable("TOPOLACTOR_TOPOLOGY_LAYOUT_CLASS_SSOT_PATH");
        string path;
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            path = Path.GetFullPath(overridePath!);
        }
        else
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            path = "";
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "docs", "design", "topology-layout-class-ssot.yaml");
                if (File.Exists(candidate)) { path = candidate; break; }
                dir = dir.Parent;
            }
            if (string.IsNullOrWhiteSpace(path))
                return [];
        }
        if (!File.Exists(path)) return [];
        var lines = File.ReadAllLines(path);
        var inClasses = false;
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in lines)
        {
            var line = raw.Replace("\r", "");
            if (line.Trim() == "classes:")
            {
                inClasses = true;
                continue;
            }
            if (!inClasses) continue;
            if (line.StartsWith("  generated_artifacts:") ||
                line.StartsWith("  concrete_css:") ||
                line.StartsWith("  ci_check_contract:"))
            {
                break;
            }
            var m = TopologyLayoutClassKeyYamlRegex.Match(line);
            if (m.Success) keys.Add(m.Groups[1].Value);
        }
        return keys;
    }

    // SSOT: admin-console-workflow-ssot.yaml layout_editor.node_kind_contract.structural_html.allowlist
    private static readonly HashSet<string> StructuralHtmlTagAllowlist = new(StringComparer.Ordinal)
    {
        // block
        "div", "section", "article", "aside", "header", "footer", "main", "nav",
        // heading
        "h1", "h2", "h3", "h4", "h5", "h6",
        // text
        "p", "span", "strong", "em", "blockquote", "pre", "code",
        // link
        "a",
        // form
        "form", "fieldset", "legend", "label", "button", "input", "textarea", "select", "option",
        // media
        "img", "picture", "figure", "figcaption", "video", "audio",
        // list
        "ul", "ol", "li", "dl", "dt", "dd",
        // table
        "table", "thead", "tbody", "tfoot", "tr", "th", "td", "caption",
    };

    private static List<string> ExtractLayoutClassRefs(string tensorPatchJson)
    {
        using var doc = JsonDocument.Parse(tensorPatchJson);
        var refs = new List<string>();
        if (doc.RootElement.TryGetProperty("layoutClassRefs", out var rootArr) &&
            rootArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in rootArr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value)) refs.Add(value!);
                }
            }
        }
        if (doc.RootElement.TryGetProperty("nodes", out var nodes) &&
            nodes.ValueKind == JsonValueKind.Array)
        {
            foreach (var node in nodes.EnumerateArray())
            {
                if (node.ValueKind != JsonValueKind.Object) continue;
                if (!node.TryGetProperty("layoutClassRefs", out var nodeArr) ||
                    nodeArr.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }
                foreach (var item in nodeArr.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var value = item.GetString();
                        if (!string.IsNullOrWhiteSpace(value)) refs.Add(value!);
                    }
                }
            }
        }
        return refs;
    }

    private static string? ValidateLayoutPatchNodes(string tensorPatchJson)
    {
        using var doc = JsonDocument.Parse(tensorPatchJson);
        if (!doc.RootElement.TryGetProperty("nodes", out var nodes) ||
            nodes.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var node in nodes.EnumerateArray())
        {
            if (node.ValueKind != JsonValueKind.Object)
                return "LAYOUT_PATCH_NODE_MUST_BE_OBJECT";

            var nodeId = node.TryGetProperty("nodeId", out var nodeIdEl) &&
                nodeIdEl.ValueKind == JsonValueKind.String
                ? nodeIdEl.GetString()?.Trim()
                : null;
            if (string.IsNullOrWhiteSpace(nodeId))
                return "LAYOUT_PATCH_NODE_ID_REQUIRED";

            var nodeKind = node.TryGetProperty("nodeKind", out var kindEl) &&
                kindEl.ValueKind == JsonValueKind.String
                ? kindEl.GetString()
                : "catalog_component";

            if (nodeKind == "structural_html")
            {
                var htmlTag = node.TryGetProperty("htmlTag", out var tagEl) &&
                    tagEl.ValueKind == JsonValueKind.String
                    ? tagEl.GetString()?.Trim()
                    : null;
                if (string.IsNullOrWhiteSpace(htmlTag))
                    return "LAYOUT_PATCH_STRUCTURAL_HTML_TAG_REQUIRED";
                if (!StructuralHtmlTagAllowlist.Contains(htmlTag))
                    return $"LAYOUT_PATCH_STRUCTURAL_HTML_TAG_UNKNOWN:{htmlTag}";

                if (!node.TryGetProperty("slotKey", out _))
                    return "LAYOUT_PATCH_STRUCTURAL_HTML_SLOT_KEY_REQUIRED";
                if (!node.TryGetProperty("orderIndex", out _))
                    return "LAYOUT_PATCH_STRUCTURAL_HTML_ORDER_INDEX_REQUIRED";
            }
            else if (nodeKind is "catalog_component" or null)
            {
                var componentKey = node.TryGetProperty("componentKey", out var keyEl) &&
                    keyEl.ValueKind == JsonValueKind.String
                    ? keyEl.GetString()?.Trim()
                    : null;
                if (string.IsNullOrWhiteSpace(componentKey))
                    return "LAYOUT_PATCH_CATALOG_COMPONENT_KEY_REQUIRED";
            }
            else
            {
                return $"LAYOUT_PATCH_NODE_KIND_UNKNOWN:{nodeKind}";
            }
        }

        return null;
    }


    private static string? ValidateRuntimeInteractions(JsonElement nodes)
    {
        var componentKindsByNodeId = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var node in nodes.EnumerateArray())
        {
            if (node.ValueKind != JsonValueKind.Object) continue;
            var nodeId = node.TryGetProperty("nodeId", out var nodeIdEl) && nodeIdEl.ValueKind == JsonValueKind.String
                ? nodeIdEl.GetString()?.Trim()
                : null;
            if (string.IsNullOrWhiteSpace(nodeId)) continue;
            var componentKind = node.TryGetProperty("componentKind", out var kindEl) && kindEl.ValueKind == JsonValueKind.String
                ? kindEl.GetString()?.Trim()
                : null;
            componentKindsByNodeId[nodeId!] = componentKind;
        }

        foreach (var sourceNode in nodes.EnumerateArray())
        {
            if (sourceNode.ValueKind != JsonValueKind.Object) continue;
            if (!sourceNode.TryGetProperty("runtimeInteractions", out var interactions)) continue;
            if (interactions.ValueKind != JsonValueKind.Array)
                return "RUNTIME_INTERACTIONS_MUST_BE_ARRAY";
            foreach (var interaction in interactions.EnumerateArray())
            {
                if (interaction.ValueKind != JsonValueKind.Object)
                    return "RUNTIME_INTERACTION_MUST_BE_OBJECT";
                var trigger = interaction.TryGetProperty("trigger", out var triggerEl) && triggerEl.ValueKind == JsonValueKind.String
                    ? triggerEl.GetString()?.Trim()
                    : null;
                if (string.IsNullOrWhiteSpace(trigger))
                    return "RUNTIME_INTERACTION_TRIGGER_REQUIRED";
                var actionType = interaction.TryGetProperty("actionType", out var actionEl) && actionEl.ValueKind == JsonValueKind.String
                    ? actionEl.GetString()?.Trim()
                    : null;
                if (string.IsNullOrWhiteSpace(actionType))
                    return "RUNTIME_INTERACTION_ACTION_TYPE_REQUIRED";
                var targetNodeId = interaction.TryGetProperty("targetNodeId", out var targetEl) && targetEl.ValueKind == JsonValueKind.String
                    ? targetEl.GetString()?.Trim()
                    : null;
                if (string.IsNullOrWhiteSpace(targetNodeId))
                    return "RUNTIME_INTERACTION_TARGET_NODE_REQUIRED";
                if (!componentKindsByNodeId.TryGetValue(targetNodeId!, out var targetKind))
                    return $"RUNTIME_INTERACTION_TARGET_NODE_NOT_FOUND:{targetNodeId}";
                var statePath = interaction.TryGetProperty("statePath", out var stateEl) && stateEl.ValueKind == JsonValueKind.String
                    ? stateEl.GetString()?.Trim()
                    : null;
                var isDisclosure = actionType is "openModal" or "closeModal" or "toggleModal" or "openDrawer" or "closeDrawer" or "toggleDrawer" or "openDialog" or "closeDialog" or "toggleDialog";
                var isActiveKey = actionType is "setActiveKey";
                var isSetState = actionType is "setState";
                if (!isDisclosure && !isActiveKey && !isSetState)
                    return $"RUNTIME_INTERACTION_ACTION_UNSUPPORTED:{actionType}";
                if (isDisclosure)
                {
                    if (statePath is not null && statePath != "open")
                        return $"RUNTIME_INTERACTION_STATE_PATH_UNSUPPORTED:{statePath}";
                    var expected = actionType.Contains("Modal", StringComparison.Ordinal) ? "disclosure/modal"
                        : actionType.Contains("Drawer", StringComparison.Ordinal) ? "disclosure/drawer"
                        : "disclosure/dialog";
                    if (!string.Equals(targetKind, expected, StringComparison.Ordinal))
                        return $"RUNTIME_INTERACTION_TARGET_KIND_MISMATCH:{targetNodeId}:{targetKind ?? "(missing)"}:{expected}";
                }
                else if (isActiveKey)
                {
                    if (statePath is not null && statePath != "activeKey")
                        return $"RUNTIME_INTERACTION_STATE_PATH_UNSUPPORTED:{statePath}";
                    if (targetKind is not ("disclosure/tabs" or "disclosure/accordion"))
                        return $"RUNTIME_INTERACTION_TARGET_KIND_MISMATCH:{targetNodeId}:{targetKind ?? "(missing)"}:disclosure/tabs|disclosure/accordion";
                }
                else if (isSetState)
                {
                    if (statePath is not ("open" or "activeKey"))
                        return $"RUNTIME_INTERACTION_STATE_PATH_UNSUPPORTED:{statePath ?? "(missing)"}";
                }
            }
        }
        return null;
    }

    private static LayoutPatchResult NormalizeLayoutPatch(
        Guid layoutId, string routeKey, string? tensorPatchJson,
        IReadOnlyList<string>? cssTokenRefs,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? responsiveTokenRefs)
    {
        var patch = string.IsNullOrWhiteSpace(tensorPatchJson) ? "{}" : tensorPatchJson!;
        // layout_patch owns placement/tensor only; design tokens use component_style_design:upsert.
        _ = cssTokenRefs;
        _ = responsiveTokenRefs;
        var css = new List<string>();
        var responsive = new Dictionary<string, IReadOnlyList<string>>();
        return new LayoutPatchResult(true, true, layoutId.ToString(), routeKey, patch, css, responsive, "Layout patch normalized (placement only).");
    }

    private static bool ContainsDraftOnlyNode(string tensorPatchJson)
    {
        using var doc = JsonDocument.Parse(tensorPatchJson);
        if (!doc.RootElement.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var node in nodes.EnumerateArray())
        {
            if (node.ValueKind != JsonValueKind.Object) continue;
            if (!node.TryGetProperty("_draftOnly", out var marker)) continue;
            if (marker.ValueKind == JsonValueKind.True)
                return true;
        }

        return false;
    }

    public override async Task<LayoutPatchDraftDto?> GetLayoutPatchDraftAsync(
        Guid packageId,
        Guid layoutId,
        string routeKey,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT
              COALESCE(layout_draft_tmp_json::text, layout_patch_json::text) AS effective_json,
              layout_draft_tmp_json IS NOT NULL AS has_tmp
            FROM topology.ui_topology_tensor
            WHERE package_id = @pkg AND layout_id = @layout AND route_key = @route
            ORDER BY updated_at DESC
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("pkg", packageId);
        cmd.Parameters.AddWithValue("layout", layoutId);
        cmd.Parameters.AddWithValue("route", routeKey);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;
        if (reader.IsDBNull(0))
            return null;
        var json = reader.GetString(0);
        var hasTmp = !reader.IsDBNull(1) && reader.GetBoolean(1);
        return new LayoutPatchDraftDto(
            packageId.ToString(),
            layoutId.ToString(),
            routeKey,
            json,
            Found: true,
            HasTmpDraft: hasTmp);
    }

    public override async Task<ValidationError?> SaveLayoutDraftTmpAsync(
        Guid packageId,
        Guid layoutId,
        string routeKey,
        string tmpJson,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "UPDATE topology.ui_topology_tensor SET layout_draft_tmp_json=@tmp::jsonb, updated_at=now() WHERE package_id=@pkg AND layout_id=@layout AND route_key=@route";
        cmd.Parameters.AddWithValue("pkg", packageId);
        cmd.Parameters.AddWithValue("layout", layoutId);
        cmd.Parameters.AddWithValue("route", routeKey);
        cmd.Parameters.AddWithValue("tmp", tmpJson);
        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows == 0
            ? new ValidationError("LAYOUT_PATCH_TMP_TARGET_NOT_FOUND", $"No ui_topology_tensor row for package {packageId}, layout {layoutId}, route {routeKey}.")
            : null;
    }

    public override Task<LayoutPatchResult> PreviewLayoutPatchAsync(Guid layoutId, string routeKey, string? tensorPatchJson, IReadOnlyList<string>? cssTokenRefs, IReadOnlyDictionary<string, IReadOnlyList<string>>? responsiveTokenRefs, CancellationToken ct = default)
        => Task.FromResult(NormalizeLayoutPatch(layoutId, routeKey, tensorPatchJson, cssTokenRefs, responsiveTokenRefs));

    public override Task<LayoutPatchResult> ValidateLayoutPatchAsync(Guid layoutId, string routeKey, string? tensorPatchJson, IReadOnlyList<string>? cssTokenRefs, IReadOnlyDictionary<string, IReadOnlyList<string>>? responsiveTokenRefs, CancellationToken ct = default)
    {
        var normalized = NormalizeLayoutPatch(layoutId, routeKey, tensorPatchJson, cssTokenRefs, responsiveTokenRefs);
        try
        {
            if (ContainsDraftOnlyNode(normalized.TensorPatchJson))
                return Task.FromResult(normalized with { Ok = false, Valid = false, Message = "DRAFT_ONLY_NODE_NOT_APPLICABLE:DRAFT_ONLY_NODE_CANNOT_BE_APPLIED" });
        }
        catch (JsonException)
        {
            return Task.FromResult(normalized with { Ok = false, Valid = false, Message = "TENSOR_PATCH_JSON_MALFORMED" });
        }
        var layoutClassVocab = LoadTopologyLayoutClassVocabulary();
        if (layoutClassVocab.Count == 0)
            return Task.FromResult(normalized with { Ok = false, Valid = false, Message = "TOPOLOGY_LAYOUT_CLASS_VOCABULARY_UNAVAILABLE" });
        try
        {
            var nodeError = ValidateLayoutPatchNodes(normalized.TensorPatchJson);
            if (nodeError is not null)
                return Task.FromResult(normalized with { Ok = false, Valid = false, Message = nodeError });
            using (var runtimeInteractionDoc = JsonDocument.Parse(normalized.TensorPatchJson))
            {
                if (runtimeInteractionDoc.RootElement.TryGetProperty("nodes", out var runtimeNodes) && runtimeNodes.ValueKind == JsonValueKind.Array)
                {
                    var runtimeInteractionError = ValidateRuntimeInteractions(runtimeNodes);
                    if (runtimeInteractionError is not null)
                        return Task.FromResult(normalized with { Ok = false, Valid = false, Message = runtimeInteractionError });
                }
            }

            foreach (var classRef in ExtractLayoutClassRefs(normalized.TensorPatchJson))
            {
                if (!layoutClassVocab.Contains(classRef))
                    return Task.FromResult(normalized with { Ok = false, Valid = false, Message = $"TOPOLOGY_LAYOUT_CLASS_REF_UNKNOWN:{classRef}" });
            }
        }
        catch (JsonException)
        {
            return Task.FromResult(normalized with { Ok = false, Valid = false, Message = "TENSOR_PATCH_JSON_MALFORMED" });
        }
        return Task.FromResult(normalized with { Message = "Layout patch validation passed." });
    }

    public override async Task<ValidationError?> VerifyLayoutPatchPackageBindingAsync(
        Guid packageId,
        Guid layoutId,
        string routeKey,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT 1 FROM topology.ui_topology_tensor
            WHERE package_id = @pkg AND layout_id = @layout AND route_key = @route
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("pkg", packageId);
        cmd.Parameters.AddWithValue("layout", layoutId);
        cmd.Parameters.AddWithValue("route", routeKey);
        var scalar = await cmd.ExecuteScalarAsync(ct);
        if (scalar is null)
        {
            return new ValidationError(
                "LAYOUT_PATCH_PACKAGE_MISMATCH",
                $"layout {layoutId} on route {routeKey} is not linked to package {packageId}.");
        }
        return null;
    }

    public override async Task<ValidationError?> VerifyPackageLayoutNodeAsync(
        Guid packageId,
        string layoutNodeId,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT 1
            FROM topology.ui_topology_tensor t
            CROSS JOIN LATERAL jsonb_array_elements(
              CASE
                WHEN jsonb_typeof(COALESCE(t.layout_draft_tmp_json, t.layout_patch_json)->'nodes') = 'array'
                  THEN COALESCE(t.layout_draft_tmp_json, t.layout_patch_json)->'nodes'
                ELSE '[]'::jsonb
              END
            ) AS node(elem)
            WHERE t.package_id = @pkg AND node.elem->>'nodeId' = @node
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("pkg", packageId);
        cmd.Parameters.AddWithValue("node", layoutNodeId);
        var scalar = await cmd.ExecuteScalarAsync(ct);
        if (scalar is null)
        {
            return new ValidationError(
                "COMPONENT_DESIGN_LAYOUT_NODE_NOT_FOUND",
                $"layoutNodeId {layoutNodeId} is not present in package {packageId} effective layout draft.");
        }
        return null;
    }

    public override async Task<LayoutPatchResult> ApplyConfirmedLayoutPatchAsync(
        Guid packageId,
        Guid layoutId,
        string routeKey,
        string? tensorPatchJson,
        IReadOnlyList<string>? cssTokenRefs,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? responsiveTokenRefs,
        CancellationToken ct = default)
    {
        // Pure validation (draft-only node, malformed JSON, CSS token vocabulary) runs first —
        // before any DB access so these explicit errors are never swallowed by a connection failure.
        var valid = await ValidateLayoutPatchAsync(layoutId, routeKey, tensorPatchJson, cssTokenRefs, responsiveTokenRefs, ct);
        if (!valid.Ok || !valid.Valid) return valid;

        var bindingError = await VerifyLayoutPatchPackageBindingAsync(packageId, layoutId, routeKey, ct);
        if (bindingError is not null)
        {
            return new LayoutPatchResult(
                false, false, layoutId.ToString(), routeKey, "{}", [], new Dictionary<string, IReadOnlyList<string>>(),
                bindingError.Message);
        }

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var responsiveJson = System.Text.Json.JsonSerializer.Serialize(valid.ResponsiveTokenRefs);
        var updateLayout = new NpgsqlCommand(
            "UPDATE topology.components_layout_design SET layout_schema_json=@schema::jsonb, css_token_refs=@css::jsonb, responsive_token_refs=@resp::jsonb, updated_at=now() WHERE layout_id=@layoutId", conn, tx);
        updateLayout.Parameters.AddWithValue("schema", valid.TensorPatchJson);
        updateLayout.Parameters.AddWithValue("css", System.Text.Json.JsonSerializer.Serialize(valid.CssTokenRefs));
        updateLayout.Parameters.AddWithValue("resp", responsiveJson);
        updateLayout.Parameters.AddWithValue("layoutId", layoutId);
        var rows = await updateLayout.ExecuteNonQueryAsync(ct);
        if (rows != 1)
        {
            await tx.RollbackAsync(ct);
            return valid with { Ok = false, Valid = false, Message = "LAYOUT_NOT_FOUND" };
        }

        var updateTensor = new NpgsqlCommand(
            "UPDATE topology.ui_topology_tensor SET layout_patch_json=@patch::jsonb, css_token_refs=@css::jsonb, responsive_token_refs=@resp::jsonb, updated_at=now() WHERE package_id=@pkg AND layout_id=@layoutId AND route_key=@routeKey", conn, tx);
        updateTensor.Parameters.AddWithValue("pkg", packageId);
        updateTensor.Parameters.AddWithValue("patch", valid.TensorPatchJson);
        updateTensor.Parameters.AddWithValue("css", System.Text.Json.JsonSerializer.Serialize(valid.CssTokenRefs));
        updateTensor.Parameters.AddWithValue("resp", responsiveJson);
        updateTensor.Parameters.AddWithValue("layoutId", layoutId);
        updateTensor.Parameters.AddWithValue("routeKey", routeKey);
        var tensorRows = await updateTensor.ExecuteNonQueryAsync(ct);
        if (tensorRows != 1)
        {
            await tx.RollbackAsync(ct);
            return valid with { Ok = false, Valid = false, Message = "TOPOLOGY_TENSOR_NOT_FOUND" };
        }
        // Clear _tmp draft on apply — explicit apply promotes canvas state to persisted.
        var clearTmp = new NpgsqlCommand(
            "UPDATE topology.ui_topology_tensor SET layout_draft_tmp_json=NULL WHERE package_id=@pkg AND layout_id=@layoutId AND route_key=@routeKey",
            conn, tx);
        clearTmp.Parameters.AddWithValue("pkg", packageId);
        clearTmp.Parameters.AddWithValue("layoutId", layoutId);
        clearTmp.Parameters.AddWithValue("routeKey", routeKey);
        await clearTmp.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return valid with { Message = "Layout patch applied." };
    }

    public override async Task<IReadOnlyList<AdminPackageListItemDto>> ListAdminPackagesAsync(
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT p.package_id::text, p.package_key, t.route_key, t.layout_id::text, t.wiring_id::text, " +
            "COALESCE(array_agg(DISTINCT m.component_id::text) FILTER (WHERE m.component_id IS NOT NULL), ARRAY[]::text[]) AS component_ids, " +
            "COALESCE((p.package_schema_json->>'bucketItemIds')::text, NULL) AS bucket_item_ids_json " +
            "FROM topology.ui_component_package p " +
            "LEFT JOIN topology.ui_topology_tensor t ON t.package_id = p.package_id " +
            "LEFT JOIN topology.ui_package_component_map m ON m.package_id = p.package_id " +
            "GROUP BY p.package_id, p.package_key, p.package_schema_json, t.route_key, t.layout_id, t.wiring_id " +
            "ORDER BY p.package_key ASC";
        var list = new List<AdminPackageListItemDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var componentIds = reader.IsDBNull(5)
                ? Array.Empty<string>()
                : reader.GetFieldValue<string[]>(5);
            IReadOnlyList<string>? bucketItemIds = null;
            if (!reader.IsDBNull(6))
            {
                var raw = reader.GetString(6);
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(raw);
                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            bucketItemIds = doc.RootElement.EnumerateArray()
                                .Where(e => e.ValueKind == JsonValueKind.String)
                                .Select(e => e.GetString()!)
                                .ToList();
                        }
                    }
                    catch (JsonException) { }
                }
            }
            list.Add(new AdminPackageListItemDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                componentIds.Length > 0 ? componentIds : null,
                bucketItemIds));
        }
        return list;
    }

    public override async Task<IReadOnlyList<ComponentStyleDesignListItemDto>> ListComponentStyleDesignsAsync(
        Guid? packageId,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        if (packageId.HasValue)
        {
            cmd.CommandText =
                """
                SELECT csd.design_id::text,
                       csd.name,
                       COALESCE(csd.design_draft_tmp_json, csd.design)->>'componentId',
                       COALESCE(csd.design_draft_tmp_json, csd.design)->>'layoutNodeId',
                       COALESCE(csd.design_draft_tmp_json, csd.design)->'cssTokenRefs',
                       COALESCE(csd.design_draft_tmp_json, csd.design)->'responsiveTokenRefs',
                       COALESCE(csd.design_draft_tmp_json, csd.design)->>'inlineText',
                       COALESCE(csd.design_draft_tmp_json, csd.design)->>'linkHref',
                       COALESCE(csd.design_draft_tmp_json, csd.design)->>'linkTarget',
                       COALESCE(csd.design_draft_tmp_json, csd.design)->>'reactionIntent',
                       COALESCE(csd.design_draft_tmp_json, csd.design)->>'classname',
                       COALESCE(csd.design_draft_tmp_json, csd.design)->>'tailwind',
                       csd.design_draft_tmp_json IS NOT NULL
                FROM topology.components_style_design csd
                WHERE EXISTS (
                    SELECT 1
                    FROM topology.components_package_design cpd,
                         jsonb_array_elements(cpd.layout) AS pair(elem)
                    WHERE cpd.package_id = @pkg
                      AND (pair.elem->>'designId')::uuid = csd.design_id
                )
                ORDER BY csd.name ASC
                """;
            cmd.Parameters.AddWithValue("pkg", packageId.Value);
        }
        else
        {
            cmd.CommandText =
                """
                SELECT design_id::text,
                       name,
                       COALESCE(design_draft_tmp_json, design)->>'componentId',
                       COALESCE(design_draft_tmp_json, design)->>'layoutNodeId',
                       COALESCE(design_draft_tmp_json, design)->'cssTokenRefs',
                       COALESCE(design_draft_tmp_json, design)->'responsiveTokenRefs',
                       COALESCE(design_draft_tmp_json, design)->>'inlineText',
                       COALESCE(design_draft_tmp_json, design)->>'linkHref',
                       COALESCE(design_draft_tmp_json, design)->>'linkTarget',
                       COALESCE(design_draft_tmp_json, design)->>'reactionIntent',
                       COALESCE(design_draft_tmp_json, design)->>'classname',
                       COALESCE(design_draft_tmp_json, design)->>'tailwind',
                       design_draft_tmp_json IS NOT NULL
                FROM topology.components_style_design
                ORDER BY name ASC
                """;
        }

        var list = new List<ComponentStyleDesignListItemDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ComponentStyleDesignListItemDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                ParseCssTokenRefsJson(reader.IsDBNull(4) ? null : reader.GetString(4)),
                ParseResponsiveTokenRefsJson(reader.IsDBNull(5) ? null : reader.GetString(5)),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                !reader.IsDBNull(12) && reader.GetBoolean(12)));
        }
        return list;
    }

    public override async Task<(Guid DesignId, ValidationError? Error)> UpsertComponentStyleDesignForPackageAsync(
        Guid packageId,
        Guid? componentId,
        string? layoutNodeId,
        string name,
        string designJson,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using var pkgCheck = conn.CreateCommand();
        pkgCheck.Transaction = tx;
        pkgCheck.CommandText = "SELECT 1 FROM topology.ui_component_package WHERE package_id = @id LIMIT 1";
        pkgCheck.Parameters.AddWithValue("id", packageId);
        if (await pkgCheck.ExecuteScalarAsync(ct) is null)
        {
            await tx.RollbackAsync(ct);
            return (Guid.Empty, new ValidationError("PACKAGE_NOT_FOUND", $"package {packageId} not found"));
        }

        Guid designId;
        await using var upsert = conn.CreateCommand();
        upsert.Transaction = tx;
        upsert.CommandText =
            "INSERT INTO topology.components_style_design (name, design, design_draft_tmp_json) VALUES (@name, @design::jsonb, NULL) " +
            "ON CONFLICT (name) DO UPDATE SET design = EXCLUDED.design, design_draft_tmp_json = NULL, updated_at = now() " +
            "RETURNING design_id";
        upsert.Parameters.AddWithValue("name", name);
        upsert.Parameters.AddWithValue("design", designJson);
        designId = (Guid)(await upsert.ExecuteScalarAsync(ct))!;

        object pairEntry = componentId.HasValue
            ? new { componentId = componentId.Value.ToString(), designId = designId.ToString() }
            : new { layoutNodeId = layoutNodeId!.Trim(), designId = designId.ToString() };
        var pairJson = System.Text.Json.JsonSerializer.Serialize(new[] { pairEntry });

        await using var pkgMeta = conn.CreateCommand();
        pkgMeta.Transaction = tx;
        pkgMeta.CommandText =
            "SELECT package_key FROM topology.ui_component_package WHERE package_id = @id LIMIT 1";
        pkgMeta.Parameters.AddWithValue("id", packageId);
        var packageKey = (string?)(await pkgMeta.ExecuteScalarAsync(ct));
        if (packageKey is null)
        {
            await tx.RollbackAsync(ct);
            return (Guid.Empty, new ValidationError("PACKAGE_NOT_FOUND", $"package {packageId} not found"));
        }

        await using var mergeLayout = conn.CreateCommand();
        mergeLayout.Transaction = tx;
        mergeLayout.CommandText =
            """
            INSERT INTO topology.components_package_design (package_id, name, state, layout)
            VALUES (@pkg, @name, 'draft', @pair::jsonb)
            ON CONFLICT (package_id) DO UPDATE SET
              layout = (
                SELECT COALESCE(jsonb_agg(elem), '[]'::jsonb)
                FROM (
                  SELECT DISTINCT ON (COALESCE(elem->>'componentId', elem->>'layoutNodeId')) elem
                  FROM jsonb_array_elements(
                    COALESCE(topology.components_package_design.layout, '[]'::jsonb) || @pair::jsonb
                  ) AS elem
                  ORDER BY COALESCE(elem->>'componentId', elem->>'layoutNodeId'), (elem->>'designId')
                ) AS sub
              ),
              updated_at = now()
            """;
        mergeLayout.Parameters.AddWithValue("pkg", packageId);
        mergeLayout.Parameters.AddWithValue("name", packageKey);
        mergeLayout.Parameters.AddWithValue("pair", pairJson);
        await mergeLayout.ExecuteNonQueryAsync(ct);

        await tx.CommitAsync(ct);
        return (designId, null);
    }



    public override async Task<(Guid DesignId, ValidationError? Error)> SaveComponentStyleDesignDraftTmpForPackageAsync(
        Guid packageId,
        Guid? componentId,
        string? layoutNodeId,
        string name,
        string designTmpJson,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using var pkgMeta = conn.CreateCommand();
        pkgMeta.Transaction = tx;
        pkgMeta.CommandText = "SELECT package_key FROM topology.ui_component_package WHERE package_id = @id LIMIT 1";
        pkgMeta.Parameters.AddWithValue("id", packageId);
        var packageKey = (string?)(await pkgMeta.ExecuteScalarAsync(ct));
        if (packageKey is null)
        {
            await tx.RollbackAsync(ct);
            return (Guid.Empty, new ValidationError("PACKAGE_NOT_FOUND", $"package {packageId} not found"));
        }

        Guid designId;
        await using var upsert = conn.CreateCommand();
        upsert.Transaction = tx;
        upsert.CommandText =
            "INSERT INTO topology.components_style_design (name, design, design_draft_tmp_json) VALUES (@name, '{}'::jsonb, @tmp::jsonb) " +
            "ON CONFLICT (name) DO UPDATE SET design_draft_tmp_json = EXCLUDED.design_draft_tmp_json, updated_at = now() " +
            "RETURNING design_id";
        upsert.Parameters.AddWithValue("name", name);
        upsert.Parameters.AddWithValue("tmp", designTmpJson);
        designId = (Guid)(await upsert.ExecuteScalarAsync(ct))!;

        object pairEntry = componentId.HasValue
            ? new { componentId = componentId.Value.ToString(), designId = designId.ToString() }
            : new { layoutNodeId = layoutNodeId!.Trim(), designId = designId.ToString() };
        var pairJson = System.Text.Json.JsonSerializer.Serialize(new[] { pairEntry });

        await using var mergeLayout = conn.CreateCommand();
        mergeLayout.Transaction = tx;
        mergeLayout.CommandText =
            """
            INSERT INTO topology.components_package_design (package_id, name, state, layout)
            VALUES (@pkg, @name, 'draft', @pair::jsonb)
            ON CONFLICT (package_id) DO UPDATE SET
              layout = (
                SELECT COALESCE(jsonb_agg(elem), '[]'::jsonb)
                FROM (
                  SELECT DISTINCT ON (COALESCE(elem->>'componentId', elem->>'layoutNodeId')) elem
                  FROM jsonb_array_elements(
                    COALESCE(topology.components_package_design.layout, '[]'::jsonb) || @pair::jsonb
                  ) AS elem
                  ORDER BY COALESCE(elem->>'componentId', elem->>'layoutNodeId'), (elem->>'designId')
                ) AS sub
              ),
              updated_at = now()
            """;
        mergeLayout.Parameters.AddWithValue("pkg", packageId);
        mergeLayout.Parameters.AddWithValue("name", packageKey);
        mergeLayout.Parameters.AddWithValue("pair", pairJson);
        await mergeLayout.ExecuteNonQueryAsync(ct);

        await tx.CommitAsync(ct);
        return (designId, null);
    }


    public override async Task<IReadOnlyList<AdminPackageComponentDto>> ListPackageComponentsAsync(
        Guid packageId,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT m.component_id::text, r.component_key, r.component_kind
            FROM topology.ui_package_component_map m
            JOIN topology.ui_component_registry r ON r.component_id = m.component_id
            WHERE m.package_id = @pkg
            ORDER BY r.component_key ASC
            """;
        cmd.Parameters.AddWithValue("pkg", packageId);
        var list = new List<AdminPackageComponentDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new AdminPackageComponentDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2)));
        }
        return list;
    }


    public override async Task<IReadOnlyList<ExternalPortAuthoringCandidateDto>> ListExternalPortAuthoringCandidatesAsync(
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT access_port_id::text AS port_id,
                   'access_port' AS port_kind,
                   required_by_bundle,
                   required_by_bundle AS consumer_bundle_binding,
                   provider_kind, credential_kind, reference_key,
                   url_or_env_reference, NULL::text AS hook_path, NULL::text AS route_key
            FROM topology.external_access_ports
            WHERE active = true
            UNION ALL
            SELECT response_port_id::text AS port_id,
                   'response_port' AS port_kind,
                   required_by_bundle,
                   required_by_bundle AS consumer_bundle_binding,
                   provider_kind, credential_kind, reference_key,
                   url_or_env_reference, NULL::text AS hook_path, NULL::text AS route_key
            FROM topology.external_response_ports
            WHERE active = true
            UNION ALL
            SELECT hook_port_id::text AS port_id,
                   'hook_port' AS port_kind,
                   required_by_bundle,
                   required_by_bundle AS consumer_bundle_binding,
                   provider_kind, credential_kind, reference_key,
                   NULL::text AS url_or_env_reference, hook_path, route_key
            FROM topology.external_hook_ports
            WHERE active = true
            ORDER BY required_by_bundle, port_kind, provider_kind, port_id
            """;
        var list = new List<ExternalPortAuthoringCandidateDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var portId = reader.GetString(reader.GetOrdinal("port_id"));
            var portKind = reader.GetString(reader.GetOrdinal("port_kind"));
            var requiredByBundle = reader.GetString(reader.GetOrdinal("required_by_bundle"));
            var providerKind = reader.GetString(reader.GetOrdinal("provider_kind"));
            var credentialKind = reader.GetString(reader.GetOrdinal("credential_kind"));
            var routeKey = GetNullableString(reader, "route_key");
            var targetRef = routeKey is null
                ? $"external-port:{portKind}:{portId}"
                : $"external-port:{portKind}:{portId}:{routeKey}";
            list.Add(new ExternalPortAuthoringCandidateDto(
                portId,
                portKind,
                providerKind,
                credentialKind,
                GetNullableString(reader, "reference_key"),
                requiredByBundle,
                reader.GetString(reader.GetOrdinal("consumer_bundle_binding")),
                GetNullableString(reader, "url_or_env_reference"),
                GetNullableString(reader, "hook_path"),
                routeKey,
                targetRef));
        }
        return list;
    }

    public override async Task<AdminPackageWiringDto?> GetPackageWiringAsync(
        Guid packageId,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT w.wiring_id::text, w.wiring_key, w.wiring_kind, w.target_surface, w.target_ref
            FROM topology.ui_topology_tensor t
            INNER JOIN topology.ui_wiring_registry w ON w.wiring_id = t.wiring_id
            WHERE t.package_id = @pkg
            ORDER BY t.updated_at DESC
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("pkg", packageId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new AdminPackageWiringDto(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4));
    }

    public override async Task<ValidationError?> UpdatePackageWiringAsync(
        Guid packageId,
        Guid wiringId,
        string wiringKind,
        string targetSurface,
        string? targetRef,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(wiringKind))
            return new ValidationError("WIRING_KIND_REQUIRED", "wiringKind is required.");
        if (string.IsNullOrWhiteSpace(targetSurface))
            return new ValidationError("TARGET_SURFACE_REQUIRED", "targetSurface is required.");

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            UPDATE topology.ui_wiring_registry w
            SET wiring_kind = @kind,
                target_surface = @surface,
                target_ref = @ref,
                updated_at = now()
            WHERE w.wiring_id = @wid
              AND EXISTS (
                SELECT 1 FROM topology.ui_topology_tensor t
                WHERE t.package_id = @pkg AND t.wiring_id = w.wiring_id
              )
            """;
        cmd.Parameters.AddWithValue("pkg", packageId);
        cmd.Parameters.AddWithValue("wid", wiringId);
        cmd.Parameters.AddWithValue("kind", wiringKind.Trim());
        cmd.Parameters.AddWithValue("surface", targetSurface.Trim());
        cmd.Parameters.AddWithValue("ref", (object?)targetRef?.Trim() ?? DBNull.Value);
        var rows = await cmd.ExecuteNonQueryAsync(ct);
        if (rows != 1)
        {
            return new ValidationError(
                "PACKAGE_WIRING_NOT_FOUND",
                $"No wiring {wiringId} linked to package {packageId}.");
        }
        return null;
    }

    /// <summary>
    /// Promotes multiple bucket items into a single package for routeKey.
    /// All items must be in 'packaging' status (call GenerateFromBucketAsync first).
    /// Uses ON CONFLICT semantics for idempotency on package/layout/wiring keys.
    /// package_schema_json stores { "bucketItemIds": [...], "componentKeys": [...] }.
    /// </summary>
    public override async Task<PackageGenerateBatchResult> PromotePackageFromBucketItemsAsync(
        string routeKey,
        IReadOnlyList<Guid> bucketItemIds,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            // 1. Validate all items are in 'packaging' status and collect component metadata
            var componentInfos = new List<(Guid BucketItemId, string ComponentKey, string ComponentKind, string SourcePath)>();
            foreach (var bucketItemId in bucketItemIds)
            {
                await using var checkCmd = conn.CreateCommand();
                checkCmd.Transaction = tx;
                checkCmd.CommandText =
                    "SELECT component_key, component_kind, source_path, status " +
                    "FROM topology.components_bucket WHERE bucket_item_id = @id";
                checkCmd.Parameters.AddWithValue("id", bucketItemId);
                await using var checkReader = await checkCmd.ExecuteReaderAsync(ct);
                if (!await checkReader.ReadAsync(ct))
                {
                    await tx.RollbackAsync(ct);
                    return new PackageGenerateBatchResult(
                        PackageGenerateCode.NotFound, null, null, null, null, [], [],
                        "NOT_FOUND", $"Bucket item {bucketItemId} not found.");
                }
                var status = checkReader.GetString(3);
                if (status != "packaging")
                {
                    await tx.RollbackAsync(ct);
                    return new PackageGenerateBatchResult(
                        PackageGenerateCode.NotBucketed, null, null, null, null, [], [],
                        "NOT_PACKAGED",
                        $"Bucket item {bucketItemId} is in status '{status}', expected 'packaging'.");
                }
                componentInfos.Add((bucketItemId, checkReader.GetString(0), checkReader.GetString(1), checkReader.GetString(2)));
            }

            // 2. INSERT ui_component_registry for each item (ON CONFLICT DO NOTHING, then SELECT)
            var componentIds = new List<Guid>();
            foreach (var (_, componentKey, componentKind, sourcePath) in componentInfos)
            {
                await using var insertComp = conn.CreateCommand();
                insertComp.Transaction = tx;
                insertComp.CommandText =
                    "INSERT INTO topology.ui_component_registry (component_key, component_kind, source_path) " +
                    "VALUES (@key, @kind, @path) ON CONFLICT (component_key) DO NOTHING";
                insertComp.Parameters.AddWithValue("key", componentKey);
                insertComp.Parameters.AddWithValue("kind", componentKind);
                insertComp.Parameters.AddWithValue("path", sourcePath);
                await insertComp.ExecuteNonQueryAsync(ct);

                await using var selComp = conn.CreateCommand();
                selComp.Transaction = tx;
                selComp.CommandText =
                    "SELECT component_id FROM topology.ui_component_registry WHERE component_key = @key";
                selComp.Parameters.AddWithValue("key", componentKey);
                var componentId = (Guid)(await selComp.ExecuteScalarAsync(ct))!;
                componentIds.Add(componentId);
            }

            // 3. INSERT ui_component_package — union existing schema arrays with this batch (additive promote).
            var packageKey = $"{routeKey}:pkg";
            var packageKind = componentInfos.Count > 0 ? componentInfos[0].ComponentKind : "page";

            // Read existing schema to merge arrays; null means this is the first promote for this route.
            var existingBucketIds = new List<string>();
            var existingComponentKeys = new List<string>();
            await using (var selPkg = conn.CreateCommand())
            {
                selPkg.Transaction = tx;
                selPkg.CommandText =
                    "SELECT package_schema_json FROM topology.ui_component_package WHERE package_key = @key";
                selPkg.Parameters.AddWithValue("key", packageKey);
                var raw = await selPkg.ExecuteScalarAsync(ct) as string;
                if (raw is not null)
                {
                    try
                    {
                        var doc = System.Text.Json.JsonDocument.Parse(raw);
                        if (doc.RootElement.TryGetProperty("bucketItemIds", out var bids))
                            foreach (var el in bids.EnumerateArray())
                            {
                                var s = el.GetString();
                                if (!string.IsNullOrEmpty(s)) existingBucketIds.Add(s);
                            }
                        if (doc.RootElement.TryGetProperty("componentKeys", out var ckeys))
                            foreach (var el in ckeys.EnumerateArray())
                            {
                                var s = el.GetString();
                                if (!string.IsNullOrEmpty(s)) existingComponentKeys.Add(s);
                            }
                    }
                    catch { /* malformed JSON — treat as empty existing */ }
                }
            }

            // Union: existing ∪ this-batch, deduplicated.
            var mergedBucketIds = existingBucketIds
                .Union(bucketItemIds.Select(id => id.ToString()), StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var mergedComponentKeys = existingComponentKeys
                .Union(componentInfos.Select(c => c.ComponentKey), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var bucketItemIdsJson = System.Text.Json.JsonSerializer.Serialize(mergedBucketIds);
            var componentKeysJson = System.Text.Json.JsonSerializer.Serialize(mergedComponentKeys);
            var schemaJson = $"{{\"bucketItemIds\":{bucketItemIdsJson},\"componentKeys\":{componentKeysJson}}}";

            await using var pkgCmd = conn.CreateCommand();
            pkgCmd.Transaction = tx;
            pkgCmd.CommandText =
                "INSERT INTO topology.ui_component_package (package_key, package_kind, package_schema_json) " +
                "VALUES (@key, @kind, @schema::jsonb) " +
                "ON CONFLICT (package_key) DO UPDATE SET package_schema_json = EXCLUDED.package_schema_json, updated_at = now() " +
                "RETURNING package_id";
            pkgCmd.Parameters.AddWithValue("key", packageKey);
            pkgCmd.Parameters.AddWithValue("kind", packageKind);
            pkgCmd.Parameters.AddWithValue("schema", schemaJson);
            var packageId = (Guid)(await pkgCmd.ExecuteScalarAsync(ct))!;

            // 4. INSERT ui_package_component_map — slot_key='default' (canonical non-NULL slot) makes
            //    ON CONFLICT (package_id, component_id, slot_key) correctly prevent duplicates for
            //    repeated promotes of the same component into the same package.
            for (var i = 0; i < componentIds.Count; i++)
            {
                await using var mapCmd = conn.CreateCommand();
                mapCmd.Transaction = tx;
                mapCmd.CommandText =
                    "INSERT INTO topology.ui_package_component_map (package_id, component_id, slot_key, order_index) " +
                    "VALUES (@pkg, @comp, 'default', @idx) ON CONFLICT (package_id, component_id, slot_key) DO NOTHING";
                mapCmd.Parameters.AddWithValue("pkg", packageId);
                mapCmd.Parameters.AddWithValue("comp", componentIds[i]);
                mapCmd.Parameters.AddWithValue("idx", i);
                await mapCmd.ExecuteNonQueryAsync(ct);
            }

            // 5. INSERT components_layout_design (ON CONFLICT DO NOTHING, then SELECT)
            var layoutKey = $"{routeKey}:layout";
            await using var layoutInsert = conn.CreateCommand();
            layoutInsert.Transaction = tx;
            layoutInsert.CommandText =
                "INSERT INTO topology.components_layout_design (layout_key, layout_kind) " +
                "VALUES (@key, @kind) ON CONFLICT (layout_key) DO NOTHING";
            layoutInsert.Parameters.AddWithValue("key", layoutKey);
            layoutInsert.Parameters.AddWithValue("kind", packageKind);
            await layoutInsert.ExecuteNonQueryAsync(ct);

            await using var layoutSel = conn.CreateCommand();
            layoutSel.Transaction = tx;
            layoutSel.CommandText =
                "SELECT layout_id FROM topology.components_layout_design WHERE layout_key = @key";
            layoutSel.Parameters.AddWithValue("key", layoutKey);
            var layoutId = (Guid)(await layoutSel.ExecuteScalarAsync(ct))!;

            // 6. INSERT ui_wiring_registry (ON CONFLICT DO NOTHING, then SELECT)
            var wiringKey = $"{routeKey}:wiring";
            await using var wiringInsert = conn.CreateCommand();
            wiringInsert.Transaction = tx;
            wiringInsert.CommandText =
                "INSERT INTO topology.ui_wiring_registry (wiring_key, wiring_kind, target_surface) " +
                "VALUES (@key, @kind, 'route') ON CONFLICT (wiring_key) DO NOTHING";
            wiringInsert.Parameters.AddWithValue("key", wiringKey);
            wiringInsert.Parameters.AddWithValue("kind", packageKind);
            await wiringInsert.ExecuteNonQueryAsync(ct);

            await using var wiringSel = conn.CreateCommand();
            wiringSel.Transaction = tx;
            wiringSel.CommandText =
                "SELECT wiring_id FROM topology.ui_wiring_registry WHERE wiring_key = @key";
            wiringSel.Parameters.AddWithValue("key", wiringKey);
            var wiringId = (Guid)(await wiringSel.ExecuteScalarAsync(ct))!;

            // 7. INSERT ui_topology_tensor — slot_key='default' canonical (non-NULL) so that
            //    ON CONFLICT (route_key, package_id, layout_id, wiring_id, slot_key, order_index)
            //    correctly prevents a second tensor row on repeated promotes for the same route.
            await using var tensorInsert = conn.CreateCommand();
            tensorInsert.Transaction = tx;
            tensorInsert.CommandText =
                "INSERT INTO topology.ui_topology_tensor (route_key, package_id, layout_id, wiring_id, slot_key) " +
                "VALUES (@route, @pkg, @layout, @wiring, 'default') " +
                "ON CONFLICT (route_key, package_id, layout_id, wiring_id, slot_key, order_index) DO NOTHING";
            tensorInsert.Parameters.AddWithValue("route", routeKey);
            tensorInsert.Parameters.AddWithValue("pkg", packageId);
            tensorInsert.Parameters.AddWithValue("layout", layoutId);
            tensorInsert.Parameters.AddWithValue("wiring", wiringId);
            await tensorInsert.ExecuteNonQueryAsync(ct);

            await using var tensorSel = conn.CreateCommand();
            tensorSel.Transaction = tx;
            tensorSel.CommandText =
                "SELECT tensor_id FROM topology.ui_topology_tensor " +
                "WHERE route_key = @route AND package_id = @pkg AND layout_id = @layout AND wiring_id = @wiring " +
                "AND slot_key = 'default' LIMIT 1";
            tensorSel.Parameters.AddWithValue("route", routeKey);
            tensorSel.Parameters.AddWithValue("pkg", packageId);
            tensorSel.Parameters.AddWithValue("layout", layoutId);
            tensorSel.Parameters.AddWithValue("wiring", wiringId);
            var tensorId = (Guid)(await tensorSel.ExecuteScalarAsync(ct))!;

            // 8. UPDATE components_bucket status = 'promoted' for all items (shell promote may pass none).
            if (bucketItemIds.Count > 0)
            {
                var bucketIdParams = bucketItemIds.Select((_, i) => $"@bid{i}").ToArray();
                await using var promoteCmd = conn.CreateCommand();
                promoteCmd.Transaction = tx;
                promoteCmd.CommandText =
                    $"UPDATE topology.components_bucket SET status = 'promoted', updated_at = now() " +
                    $"WHERE bucket_item_id IN ({string.Join(",", bucketIdParams)}) AND status = 'packaging'";
                for (var i = 0; i < bucketItemIds.Count; i++)
                    promoteCmd.Parameters.AddWithValue($"bid{i}", bucketItemIds[i]);
                await promoteCmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);

            _npgsqlLogger.LogInformation(
                "NpgsqlUiTopologyRepository.PromotePackageFromBucketItemsAsync: committed packageId={PkgId}, routeKey={Route}, components={Count}.",
                packageId, routeKey, componentIds.Count);

            return new PackageGenerateBatchResult(
                PackageGenerateCode.Success,
                tensorId, packageId, layoutId, wiringId,
                componentIds,
                bucketItemIds.ToList());
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            await tx.RollbackAsync(ct);
            _npgsqlLogger.LogWarning(ex,
                "NpgsqlUiTopologyRepository.PromotePackageFromBucketItemsAsync: constraint violation for routeKey={Route}.", routeKey);
            return new PackageGenerateBatchResult(
                PackageGenerateCode.ConstraintViolation, null, null, null, null, [], [],
                "CONSTRAINT_VIOLATION",
                "A registry key conflict occurred during batch promote.");
        }
        catch (Exception ex)
        {
            try { await tx.RollbackAsync(ct); } catch { /* best-effort */ }
            _npgsqlLogger.LogError(ex,
                "NpgsqlUiTopologyRepository.PromotePackageFromBucketItemsAsync: DB error for routeKey={Route}.", routeKey);
            return new PackageGenerateBatchResult(
                PackageGenerateCode.DbUnavailable, null, null, null, null, [], [],
                "DB_UNAVAILABLE", "Repository unavailable during batch package promotion.");
        }
    }

    private static Dictionary<string, IReadOnlyList<string>>? ParseResponsiveTokenRefsJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            var map = new Dictionary<string, IReadOnlyList<string>>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Array) continue;
                var tokens = new List<string>();
                foreach (var item in prop.Value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var value = item.GetString()?.Trim();
                        if (!string.IsNullOrEmpty(value)) tokens.Add(value);
                    }
                }
                if (tokens.Count > 0) map[prop.Name] = tokens;
            }
            return map.Count > 0 ? map : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string>? ParseCssTokenRefsJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var list = new List<string>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var value = item.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(value)) list.Add(value);
                }
            }
            return list;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Removes component keys from route package. Idempotent for keys not in package.
    /// </summary>
    public override async Task<PackageDetachComponentsResult> DetachPackageComponentsAsync(
        string routeKey,
        IReadOnlyList<string> componentKeys,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            var packageKey = $"{routeKey}:pkg";
            Guid? packageId = null;
            await using (var selPkg = conn.CreateCommand())
            {
                selPkg.Transaction = tx;
                selPkg.CommandText =
                    "SELECT package_id::text, package_schema_json::text FROM topology.ui_component_package WHERE package_key = @key";
                selPkg.Parameters.AddWithValue("key", packageKey);
                await using var reader = await selPkg.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                {
                    await tx.RollbackAsync(ct);
                    return new PackageDetachComponentsResult(
                        PackageGenerateCode.NotFound, null, [],
                        "PACKAGE_NOT_FOUND", $"No package for routeKey '{routeKey}'.");
                }
                packageId = Guid.Parse(reader.GetString(0));
            }

            var detached = new List<string>();
            var remainingBucketIds = new List<string>();
            var remainingComponentKeys = new List<string>();

            await using (var selSchema = conn.CreateCommand())
            {
                selSchema.Transaction = tx;
                selSchema.CommandText =
                    "SELECT package_schema_json::text FROM topology.ui_component_package WHERE package_id = @pkg";
                selSchema.Parameters.AddWithValue("pkg", packageId!.Value);
                var raw = await selSchema.ExecuteScalarAsync(ct) as string;
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    using var doc = JsonDocument.Parse(raw);
                    if (doc.RootElement.TryGetProperty("bucketItemIds", out var bids))
                        foreach (var el in bids.EnumerateArray())
                        {
                            var s = el.GetString();
                            if (!string.IsNullOrEmpty(s)) remainingBucketIds.Add(s);
                        }
                    if (doc.RootElement.TryGetProperty("componentKeys", out var ckeys))
                        foreach (var el in ckeys.EnumerateArray())
                        {
                            var s = el.GetString();
                            if (!string.IsNullOrEmpty(s)) remainingComponentKeys.Add(s);
                        }
                }
            }

            foreach (var key in componentKeys.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!remainingComponentKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                    continue;

                await using var selComp = conn.CreateCommand();
                selComp.Transaction = tx;
                selComp.CommandText =
                    "SELECT component_id FROM topology.ui_component_registry WHERE component_key = @key";
                selComp.Parameters.AddWithValue("key", key);
                var compObj = await selComp.ExecuteScalarAsync(ct);
                if (compObj is Guid componentId)
                {
                    await using var delMap = conn.CreateCommand();
                    delMap.Transaction = tx;
                    delMap.CommandText =
                        "DELETE FROM topology.ui_package_component_map WHERE package_id = @pkg AND component_id = @comp";
                    delMap.Parameters.AddWithValue("pkg", packageId!.Value);
                    delMap.Parameters.AddWithValue("comp", componentId);
                    await delMap.ExecuteNonQueryAsync(ct);
                }

                await using var demoteBucket = conn.CreateCommand();
                demoteBucket.Transaction = tx;
                demoteBucket.CommandText =
                    "UPDATE topology.components_bucket SET status = 'bucketed', updated_at = now() " +
                    "WHERE component_key = @key AND status = 'promoted'";
                demoteBucket.Parameters.AddWithValue("key", key);
                await demoteBucket.ExecuteNonQueryAsync(ct);

                remainingComponentKeys.RemoveAll(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
                var bucketIdsToRemove = new List<string>();
                await using (var selBucket = conn.CreateCommand())
                {
                    selBucket.Transaction = tx;
                    selBucket.CommandText =
                        "SELECT bucket_item_id::text FROM topology.components_bucket WHERE component_key = @key";
                    selBucket.Parameters.AddWithValue("key", key);
                    await using var br = await selBucket.ExecuteReaderAsync(ct);
                    while (await br.ReadAsync(ct))
                        bucketIdsToRemove.Add(br.GetString(0));
                }
                remainingBucketIds.RemoveAll(id =>
                    bucketIdsToRemove.Contains(id, StringComparer.OrdinalIgnoreCase));
                detached.Add(key);
            }

            var schemaJson = JsonSerializer.Serialize(new
            {
                bucketItemIds = remainingBucketIds,
                componentKeys = remainingComponentKeys,
            });
            await using (var updPkg = conn.CreateCommand())
            {
                updPkg.Transaction = tx;
                updPkg.CommandText =
                    "UPDATE topology.ui_component_package SET package_schema_json = @schema::jsonb, updated_at = now() WHERE package_id = @pkg";
                updPkg.Parameters.AddWithValue("schema", schemaJson);
                updPkg.Parameters.AddWithValue("pkg", packageId!.Value);
                await updPkg.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return new PackageDetachComponentsResult(
                PackageGenerateCode.Success,
                packageId,
                detached);
        }
        catch (Exception ex)
        {
            try { await tx.RollbackAsync(ct); } catch { /* best-effort */ }
            _npgsqlLogger.LogError(ex,
                "NpgsqlUiTopologyRepository.DetachPackageComponentsAsync: failed for routeKey={Route}.", routeKey);
            return new PackageDetachComponentsResult(
                PackageGenerateCode.DbUnavailable, null, [],
                "DB_UNAVAILABLE", "Repository unavailable during detach.");
        }
    }

    private static string? GetNullableString(NpgsqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
