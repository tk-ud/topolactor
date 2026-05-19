using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// Production Npgsql implementation of UiTopologyRepository.
/// Operates against the tables defined in db/ui_topology_tables.sql.
///
/// PromoteBucketItemAsync wraps the entire promotion pipeline in a single
/// NpgsqlConnection + NpgsqlTransaction so no partial state can remain on failure.
/// </summary>
public class NpgsqlUiTopologyRepository : UiTopologyRepository
{
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
            "FROM ui_component_bucket " +
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
            // 1. Atomically transition bucketed -> packaging
            await using var transitionCmd = conn.CreateCommand();
            transitionCmd.Transaction = tx;
            transitionCmd.CommandText =
                "UPDATE ui_component_bucket " +
                "SET status = 'packaging', updated_at = now() " +
                "WHERE bucket_item_id = @id AND status = 'bucketed' " +
                "RETURNING component_key, source_path, component_kind";
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
                // Either not found or not in bucketed status — check which
                await using var checkCmd = conn.CreateCommand();
                checkCmd.Transaction = tx;
                checkCmd.CommandText =
                    "SELECT status FROM ui_component_bucket WHERE bucket_item_id = @id";
                checkCmd.Parameters.AddWithValue("id", bucketItemId);

                var existing = await checkCmd.ExecuteScalarAsync(ct);
                await tx.RollbackAsync(ct);

                return existing is null
                    ? new PackageGenerateResult(PackageGenerateCode.NotFound, null, null, null, null, null,
                        "NOT_FOUND", $"Bucket item {bucketItemId} not found.")
                    : new PackageGenerateResult(PackageGenerateCode.NotBucketed, null, null, null, null, null,
                        "NOT_BUCKETED",
                        $"Bucket item {bucketItemId} is in status '{existing}', expected 'bucketed'.");
            }

            // 2. INSERT ui_component_registry
            await using var compCmd = conn.CreateCommand();
            compCmd.Transaction = tx;
            compCmd.CommandText =
                "INSERT INTO ui_component_registry (component_key, component_kind, source_path) " +
                "VALUES (@key, @kind, @path) RETURNING component_id";
            compCmd.Parameters.AddWithValue("key", componentKey);
            compCmd.Parameters.AddWithValue("kind", componentKind!);
            compCmd.Parameters.AddWithValue("path", sourcePath!);
            var componentId = (Guid)(await compCmd.ExecuteScalarAsync(ct))!;

            // 3. INSERT ui_component_package
            var packageKey = $"{routeKey}:{componentKey}:pkg";
            await using var pkgCmd = conn.CreateCommand();
            pkgCmd.Transaction = tx;
            pkgCmd.CommandText =
                "INSERT INTO ui_component_package (package_key, package_kind) " +
                "VALUES (@key, @kind) RETURNING package_id";
            pkgCmd.Parameters.AddWithValue("key", packageKey);
            pkgCmd.Parameters.AddWithValue("kind", componentKind!);
            var packageId = (Guid)(await pkgCmd.ExecuteScalarAsync(ct))!;

            // 4. INSERT ui_package_component_map
            await using var mapCmd = conn.CreateCommand();
            mapCmd.Transaction = tx;
            mapCmd.CommandText =
                "INSERT INTO ui_package_component_map (package_id, component_id) " +
                "VALUES (@packageId, @componentId)";
            mapCmd.Parameters.AddWithValue("packageId", packageId);
            mapCmd.Parameters.AddWithValue("componentId", componentId);
            await mapCmd.ExecuteNonQueryAsync(ct);

            // 5. INSERT ui_layout_registry
            var layoutKey = $"{routeKey}:{componentKey}:layout";
            await using var layoutCmd = conn.CreateCommand();
            layoutCmd.Transaction = tx;
            layoutCmd.CommandText =
                "INSERT INTO ui_layout_registry (layout_key, layout_kind) " +
                "VALUES (@key, @kind) RETURNING layout_id";
            layoutCmd.Parameters.AddWithValue("key", layoutKey);
            layoutCmd.Parameters.AddWithValue("kind", componentKind!);
            var layoutId = (Guid)(await layoutCmd.ExecuteScalarAsync(ct))!;

            // 6. INSERT ui_wiring_registry
            var wiringKey = $"{routeKey}:{componentKey}:wiring";
            await using var wiringCmd = conn.CreateCommand();
            wiringCmd.Transaction = tx;
            wiringCmd.CommandText =
                "INSERT INTO ui_wiring_registry (wiring_key, wiring_kind, target_surface) " +
                "VALUES (@key, @kind, 'route') RETURNING wiring_id";
            wiringCmd.Parameters.AddWithValue("key", wiringKey);
            wiringCmd.Parameters.AddWithValue("kind", componentKind!);
            var wiringId = (Guid)(await wiringCmd.ExecuteScalarAsync(ct))!;

            // 7. INSERT ui_topology_tensor
            await using var tensorCmd = conn.CreateCommand();
            tensorCmd.Transaction = tx;
            tensorCmd.CommandText =
                "INSERT INTO ui_topology_tensor (route_key, package_id, layout_id, wiring_id) " +
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
                "UPDATE ui_component_bucket " +
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
}
