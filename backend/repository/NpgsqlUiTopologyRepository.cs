using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// Production Npgsql implementation of UiTopologyRepository.
/// Operates against the tables defined in db/ui_topology_tables.sql.
///
/// Canonical tables:
///   ui_component_bucket, ui_component_registry, ui_component_package,
///   ui_package_component_map, ui_layout_registry, ui_wiring_registry,
///   ui_topology_tensor
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

    public override async Task<UiComponentBucketRecord?> LoadBucketItemAsync(
        Guid bucketItemId,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT bucket_item_id, component_key, source_path, component_kind, status " +
            "FROM ui_component_bucket " +
            "WHERE bucket_item_id = @id";
        cmd.Parameters.AddWithValue("id", bucketItemId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            _npgsqlLogger.LogDebug(
                "NpgsqlUiTopologyRepository.LoadBucketItemAsync: not found id={Id}.", bucketItemId);
            return null;
        }

        return new UiComponentBucketRecord(
            BucketItemId:  reader.GetGuid(0),
            ComponentKey:  reader.GetString(1),
            SourcePath:    reader.GetString(2),
            ComponentKind: reader.GetString(3),
            Status:        reader.GetString(4)
        );
    }

    public override async Task<bool> TransitionBucketStatusAsync(
        Guid bucketItemId,
        string expectedStatus,
        string newStatus,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "UPDATE ui_component_bucket " +
            "SET status = @newStatus, updated_at = now() " +
            "WHERE bucket_item_id = @id AND status = @expectedStatus";
        cmd.Parameters.AddWithValue("id", bucketItemId);
        cmd.Parameters.AddWithValue("expectedStatus", expectedStatus);
        cmd.Parameters.AddWithValue("newStatus", newStatus);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows == 1;
    }

    public override async Task<Guid> InsertComponentRegistryAsync(
        string componentKey,
        string componentKind,
        string sourcePath,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO ui_component_registry (component_key, component_kind, source_path) " +
            "VALUES (@key, @kind, @path) " +
            "RETURNING component_id";
        cmd.Parameters.AddWithValue("key", componentKey);
        cmd.Parameters.AddWithValue("kind", componentKind);
        cmd.Parameters.AddWithValue("path", sourcePath);

        var result = await cmd.ExecuteScalarAsync(ct);
        return (Guid)result!;
    }

    public override async Task<Guid> InsertComponentPackageAsync(
        string packageKey,
        string packageKind,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO ui_component_package (package_key, package_kind) " +
            "VALUES (@key, @kind) " +
            "RETURNING package_id";
        cmd.Parameters.AddWithValue("key", packageKey);
        cmd.Parameters.AddWithValue("kind", packageKind);

        var result = await cmd.ExecuteScalarAsync(ct);
        return (Guid)result!;
    }

    public override async Task InsertPackageComponentMapAsync(
        Guid packageId,
        Guid componentId,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO ui_package_component_map (package_id, component_id) " +
            "VALUES (@packageId, @componentId)";
        cmd.Parameters.AddWithValue("packageId", packageId);
        cmd.Parameters.AddWithValue("componentId", componentId);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public override async Task<Guid> InsertLayoutRegistryAsync(
        string layoutKey,
        string layoutKind,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO ui_layout_registry (layout_key, layout_kind) " +
            "VALUES (@key, @kind) " +
            "RETURNING layout_id";
        cmd.Parameters.AddWithValue("key", layoutKey);
        cmd.Parameters.AddWithValue("kind", layoutKind);

        var result = await cmd.ExecuteScalarAsync(ct);
        return (Guid)result!;
    }

    public override async Task<Guid> InsertWiringRegistryAsync(
        string wiringKey,
        string wiringKind,
        string targetSurface,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO ui_wiring_registry (wiring_key, wiring_kind, target_surface) " +
            "VALUES (@key, @kind, @surface) " +
            "RETURNING wiring_id";
        cmd.Parameters.AddWithValue("key", wiringKey);
        cmd.Parameters.AddWithValue("kind", wiringKind);
        cmd.Parameters.AddWithValue("surface", targetSurface);

        var result = await cmd.ExecuteScalarAsync(ct);
        return (Guid)result!;
    }

    public override async Task<Guid> InsertTopologyTensorAsync(
        string routeKey,
        Guid packageId,
        Guid layoutId,
        Guid wiringId,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO ui_topology_tensor (route_key, package_id, layout_id, wiring_id) " +
            "VALUES (@routeKey, @packageId, @layoutId, @wiringId) " +
            "RETURNING tensor_id";
        cmd.Parameters.AddWithValue("routeKey", routeKey);
        cmd.Parameters.AddWithValue("packageId", packageId);
        cmd.Parameters.AddWithValue("layoutId", layoutId);
        cmd.Parameters.AddWithValue("wiringId", wiringId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return (Guid)result!;
    }
}
