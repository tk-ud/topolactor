using Npgsql;

namespace Topolactor.Repository;

/// <summary>
/// credential-management external_instance_credential category (docs/design/admin-normal-surface-
/// projection-seed-ssot.yaml surface_axes.admin.surfaces.credentials.categories.
/// external_instance_credential; docs/design/instance-port-substrate-ssot.yaml
/// existing_credential_management_projection_extension): create/read/update/delete/search admin
/// projection over topology.db_instance_port / topology.runtime_instance_port.
///
/// mutation_boundary: "instance credential settings may be selected/updated only through existing
/// credential-management projection extension; no new credential table or plane is defined here."
/// This repository never writes topology.external_credential_vault -- create/update only let an
/// admin point a db_instance_port/runtime_instance_port row at an EXISTING vault reference_key (FK
/// enforced by the database itself); the vault row's own lifecycle (create/rotate/deactivate)
/// remains exclusively external_api_credential's (NpgsqlExternalApiCredentialAdminRepository).
///
/// Generic, data-defined execution: <see cref="RecordKindTables"/> is the single source of table
/// identity every method below reads from -- no port_kind/provider_kind/required_by_bundle C#
/// if/switch selector exists anywhere in this file. Search/Get are ONE generic UNION ALL query
/// (LEFT JOIN external_credential_vault by reference_key, secret-free columns only) across both
/// tables, zero C# branching. expires_at/version come from the joined vault row -- neither
/// db_instance_port nor runtime_instance_port has its own expires_at/version column, since the
/// underlying credential (and its expiry/rotation lifecycle) is the SAME shared vault row
/// external_api_credential owns; the port row is a reference to it, not a copy.
/// </summary>
public interface IExternalInstanceCredentialAdminRepository
{
    Task<IReadOnlyList<ExternalInstanceCredentialRecord>> SearchAsync(
        string? query, string? recordKind, string? providerKind, string? requiredByBundle, bool? active,
        DateTimeOffset? expiresBefore = null, DateTimeOffset? expiresAfter = null,
        CancellationToken ct = default);

    Task<ExternalInstanceCredentialRecord?> GetAsync(string recordKind, Guid recordId, CancellationToken ct = default);

    ExternalInstanceCredentialWriteResult ValidateCreateRequest(ExternalInstanceCredentialCreateRequest request);

    // Field validation (ValidateCreateRequest above) plus the reference_key-exists-in-vault check
    // CreateAsync itself enforces -- used so a dryRun preview reports the SAME real validation
    // outcome the confirmed write would reach, never a partial check that skips the FK-shaped
    // precondition (mirrors ValidateUpdateRequestAsync's existing-record check below).
    Task<ExternalInstanceCredentialWriteResult> ValidateCreateRequestAsync(
        ExternalInstanceCredentialCreateRequest request, CancellationToken ct = default);

    Task<ExternalInstanceCredentialWriteResult> CreateAsync(
        ExternalInstanceCredentialCreateRequest request, CancellationToken ct = default);

    Task<ExternalInstanceCredentialWriteResult> ValidateUpdateRequestAsync(
        ExternalInstanceCredentialUpdateRequest request, CancellationToken ct = default);

    Task<ExternalInstanceCredentialWriteResult> UpdateAsync(
        ExternalInstanceCredentialUpdateRequest request, CancellationToken ct = default);

    Task<ExternalInstanceCredentialOutcome> DeactivateAsync(
        string recordKind, Guid recordId, CancellationToken ct = default);
}

public static class ExternalInstanceCredentialRecordKinds
{
    public const string DbInstancePort = "db_instance_port";
    public const string RuntimeInstancePort = "runtime_instance_port";
    public static readonly IReadOnlyList<string> All = new[] { DbInstancePort, RuntimeInstancePort };

    /// <summary>
    /// Single source of canonical physical table identity, shared by this repository's own
    /// query/write targets AND AdminRuntime.ExternalInstanceCredential.cs's diff_log
    /// physical_table_name -- never independently derived, so the two can never drift apart.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> CanonicalPhysicalTableNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [DbInstancePort] = "topology.db_instance_port",
            [RuntimeInstancePort] = "topology.runtime_instance_port",
        };
}

public enum ExternalInstanceCredentialOutcome
{
    Ok,
    RecordKindInvalid,
    NotFound,
    RequiredFieldMissing,
    ReferenceKeyNotFound,
}

/// <summary>
/// Secret-free projection. existing_schema_fields_allowed_for_projection: reference_key,
/// provider_kind, required_by_bundle, active, expires_at, version (instance-port-substrate-ssot.yaml
/// admin-normal-surface-projection-seed-ssot.yaml) -- ExpiresAt/Version are read from the JOINED
/// external_credential_vault row, never a port-table column. RecordKind/RecordId/
/// InstanceAuthorityKey are structural identity, not credential data (same precedent as
/// ExternalApiCredentialRecord's RecordKind/RecordId).
/// </summary>
public sealed record ExternalInstanceCredentialRecord(
    string RecordKind,
    Guid RecordId,
    string InstanceAuthorityKey,
    string ProviderKind,
    string RequiredByBundle,
    string ReferenceKey,
    bool Active,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ExpiresAt,
    int? Version);

public sealed record ExternalInstanceCredentialCreateRequest(
    string RecordKind,
    string InstanceAuthorityKey,
    string ProviderKind,
    string RequiredByBundle,
    string ReferenceKey);

public sealed record ExternalInstanceCredentialUpdateRequest(
    string RecordKind,
    Guid RecordId,
    string? ProviderKind,
    string? RequiredByBundle,
    string? ReferenceKey,
    bool? Active);

public sealed record ExternalInstanceCredentialWriteResult(
    ExternalInstanceCredentialOutcome Outcome,
    ExternalInstanceCredentialRecord? Record = null,
    string? Detail = null);

public sealed class NpgsqlExternalInstanceCredentialAdminRepository : IExternalInstanceCredentialAdminRepository
{
    private readonly string _connectionString;

    private static readonly IReadOnlyDictionary<string, string> RecordKindTables =
        ExternalInstanceCredentialRecordKinds.CanonicalPhysicalTableNames;

    public NpgsqlExternalInstanceCredentialAdminRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    private const string SearchColumns = """
        record_kind, record_id, instance_authority_key, provider_kind, required_by_bundle,
        reference_key, active, created_at, updated_at, expires_at, version
        """;

    // Single UNION ALL across both port tables, LEFT JOIN the vault by reference_key for
    // expires_at/version -- generic by construction, no per-recordKind C# branch. The vault's
    // secret-bearing columns (token_hash, encrypted_payload, encryption_key_reference) are never
    // selected here at all.
    private const string SearchUnionSql = """
        WITH combined AS (
            SELECT 'db_instance_port' AS record_kind, p.instance_port_id AS record_id,
                   p.instance_authority_key, p.provider_kind, p.required_by_bundle, p.reference_key,
                   p.active, p.created_at, p.updated_at, v.expires_at, v.version
            FROM topology.db_instance_port p
            LEFT JOIN topology.external_credential_vault v ON v.reference_key = p.reference_key
            UNION ALL
            SELECT 'runtime_instance_port', p.instance_port_id,
                   p.instance_authority_key, p.provider_kind, p.required_by_bundle, p.reference_key,
                   p.active, p.created_at, p.updated_at, v.expires_at, v.version
            FROM topology.runtime_instance_port p
            LEFT JOIN topology.external_credential_vault v ON v.reference_key = p.reference_key
        )
        SELECT {0}
        FROM combined
        WHERE (@recordKind::text IS NULL OR record_kind = @recordKind)
          AND (@recordId::uuid IS NULL OR record_id = @recordId)
          AND (@providerKind::text IS NULL OR provider_kind = @providerKind)
          AND (@requiredByBundle::text IS NULL OR required_by_bundle = @requiredByBundle)
          AND (@active::boolean IS NULL OR active = @active)
          AND (
                @query::text IS NULL
                OR instance_authority_key ILIKE '%' || @query || '%'
                OR provider_kind ILIKE '%' || @query || '%'
                OR required_by_bundle ILIKE '%' || @query || '%'
                OR reference_key ILIKE '%' || @query || '%'
              )
          AND (@expiresBefore::timestamptz IS NULL OR expires_at < @expiresBefore)
          AND (@expiresAfter::timestamptz IS NULL OR expires_at > @expiresAfter)
        ORDER BY updated_at DESC
        LIMIT 200
        """;

    public async Task<IReadOnlyList<ExternalInstanceCredentialRecord>> SearchAsync(
        string? query, string? recordKind, string? providerKind, string? requiredByBundle, bool? active,
        DateTimeOffset? expiresBefore = null, DateTimeOffset? expiresAfter = null,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = string.Format(SearchUnionSql, SearchColumns);
        cmd.Parameters.AddWithValue("recordKind", (object?)recordKind ?? DBNull.Value);
        cmd.Parameters.AddWithValue("recordId", DBNull.Value);
        cmd.Parameters.AddWithValue("providerKind", (object?)providerKind ?? DBNull.Value);
        cmd.Parameters.AddWithValue("requiredByBundle", (object?)requiredByBundle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("active", (object?)active ?? DBNull.Value);
        cmd.Parameters.AddWithValue("query", (object?)query ?? DBNull.Value);
        cmd.Parameters.AddWithValue("expiresBefore", (object?)expiresBefore ?? DBNull.Value);
        cmd.Parameters.AddWithValue("expiresAfter", (object?)expiresAfter ?? DBNull.Value);

        var results = new List<ExternalInstanceCredentialRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) results.Add(MapRecord(reader));
        return results;
    }

    public async Task<ExternalInstanceCredentialRecord?> GetAsync(
        string recordKind, Guid recordId, CancellationToken ct = default)
    {
        if (!RecordKindTables.ContainsKey(recordKind)) return null;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = string.Format(SearchUnionSql, SearchColumns);
        cmd.Parameters.AddWithValue("recordKind", recordKind);
        cmd.Parameters.AddWithValue("recordId", recordId);
        cmd.Parameters.AddWithValue("providerKind", DBNull.Value);
        cmd.Parameters.AddWithValue("requiredByBundle", DBNull.Value);
        cmd.Parameters.AddWithValue("active", DBNull.Value);
        cmd.Parameters.AddWithValue("query", DBNull.Value);
        cmd.Parameters.AddWithValue("expiresBefore", DBNull.Value);
        cmd.Parameters.AddWithValue("expiresAfter", DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapRecord(reader) : null;
    }

    private static ExternalInstanceCredentialRecord MapRecord(NpgsqlDataReader r) => new(
        RecordKind: r.GetString(0),
        RecordId: r.GetGuid(1),
        InstanceAuthorityKey: r.GetString(2),
        ProviderKind: r.GetString(3),
        RequiredByBundle: r.GetString(4),
        ReferenceKey: r.GetString(5),
        Active: r.GetBoolean(6),
        CreatedAt: r.GetFieldValue<DateTimeOffset>(7),
        UpdatedAt: r.GetFieldValue<DateTimeOffset>(8),
        ExpiresAt: r.IsDBNull(9) ? null : r.GetFieldValue<DateTimeOffset>(9),
        Version: r.IsDBNull(10) ? null : r.GetInt32(10));

    private static ExternalInstanceCredentialWriteResult? ValidateCreateFields(ExternalInstanceCredentialCreateRequest request)
    {
        if (!RecordKindTables.ContainsKey(request.RecordKind))
            return new(ExternalInstanceCredentialOutcome.RecordKindInvalid, Detail: $"Unknown recordKind '{request.RecordKind}'.");
        if (string.IsNullOrWhiteSpace(request.InstanceAuthorityKey) || string.IsNullOrWhiteSpace(request.ProviderKind) ||
            string.IsNullOrWhiteSpace(request.RequiredByBundle) || string.IsNullOrWhiteSpace(request.ReferenceKey))
            return new(ExternalInstanceCredentialOutcome.RequiredFieldMissing,
                Detail: "instanceAuthorityKey, providerKind, requiredByBundle, and referenceKey are required.");
        return null;
    }

    public ExternalInstanceCredentialWriteResult ValidateCreateRequest(ExternalInstanceCredentialCreateRequest request) =>
        ValidateCreateFields(request) ?? new(ExternalInstanceCredentialOutcome.Ok);

    private async Task<bool> ReferenceKeyExistsAsync(NpgsqlConnection conn, string referenceKey, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM topology.external_credential_vault WHERE reference_key = @referenceKey";
        cmd.Parameters.AddWithValue("referenceKey", referenceKey);
        return await cmd.ExecuteScalarAsync(ct) is not null;
    }

    public async Task<ExternalInstanceCredentialWriteResult> ValidateCreateRequestAsync(
        ExternalInstanceCredentialCreateRequest request, CancellationToken ct = default)
    {
        var fieldError = ValidateCreateFields(request);
        if (fieldError is not null) return fieldError;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        if (!await ReferenceKeyExistsAsync(conn, request.ReferenceKey, ct))
            return new(ExternalInstanceCredentialOutcome.ReferenceKeyNotFound,
                Detail: $"referenceKey '{request.ReferenceKey}' does not exist in the external credential vault.");
        return new(ExternalInstanceCredentialOutcome.Ok);
    }

    public async Task<ExternalInstanceCredentialWriteResult> CreateAsync(
        ExternalInstanceCredentialCreateRequest request, CancellationToken ct = default)
    {
        var validation = await ValidateCreateRequestAsync(request, ct);
        if (validation.Outcome != ExternalInstanceCredentialOutcome.Ok) return validation;
        var table = RecordKindTables[request.RecordKind];

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO {table}
                (instance_authority_key, provider_kind, required_by_bundle, reference_key, active)
            VALUES (@instanceAuthorityKey, @providerKind, @requiredByBundle, @referenceKey, true)
            RETURNING instance_port_id
            """;
        cmd.Parameters.AddWithValue("instanceAuthorityKey", request.InstanceAuthorityKey);
        cmd.Parameters.AddWithValue("providerKind", request.ProviderKind);
        cmd.Parameters.AddWithValue("requiredByBundle", request.RequiredByBundle);
        cmd.Parameters.AddWithValue("referenceKey", request.ReferenceKey);
        var newId = (Guid)(await cmd.ExecuteScalarAsync(ct))!;

        var created = await GetAsync(request.RecordKind, newId, ct);
        return new(ExternalInstanceCredentialOutcome.Ok, created);
    }

    private static ExternalInstanceCredentialWriteResult? ValidateUpdateFields(ExternalInstanceCredentialUpdateRequest request)
    {
        if (!RecordKindTables.ContainsKey(request.RecordKind))
            return new(ExternalInstanceCredentialOutcome.RecordKindInvalid, Detail: $"Unknown recordKind '{request.RecordKind}'.");
        return null;
    }

    public async Task<ExternalInstanceCredentialWriteResult> ValidateUpdateRequestAsync(
        ExternalInstanceCredentialUpdateRequest request, CancellationToken ct = default)
    {
        var fieldError = ValidateUpdateFields(request);
        if (fieldError is not null) return fieldError;
        var existing = await GetAsync(request.RecordKind, request.RecordId, ct);
        return existing is null
            ? new(ExternalInstanceCredentialOutcome.NotFound, Detail: $"{request.RecordKind} {request.RecordId} was not found.")
            : new(ExternalInstanceCredentialOutcome.Ok, existing);
    }

    public async Task<ExternalInstanceCredentialWriteResult> UpdateAsync(
        ExternalInstanceCredentialUpdateRequest request, CancellationToken ct = default)
    {
        var fieldError = ValidateUpdateFields(request);
        if (fieldError is not null) return fieldError;
        var table = RecordKindTables[request.RecordKind];

        var existing = await GetAsync(request.RecordKind, request.RecordId, ct);
        if (existing is null)
            return new(ExternalInstanceCredentialOutcome.NotFound, Detail: $"{request.RecordKind} {request.RecordId} was not found.");

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        if (request.ReferenceKey is not null && !await ReferenceKeyExistsAsync(conn, request.ReferenceKey, ct))
            return new(ExternalInstanceCredentialOutcome.ReferenceKeyNotFound,
                Detail: $"referenceKey '{request.ReferenceKey}' does not exist in the external credential vault.");

        var setClauses = new List<string> { "updated_at = now()" };
        await using var cmd = conn.CreateCommand();
        void AddSet(string column, object? value)
        {
            setClauses.Add($"{column} = @{column}");
            cmd.Parameters.AddWithValue(column, value ?? DBNull.Value);
        }

        if (request.ProviderKind is not null) AddSet("provider_kind", request.ProviderKind);
        if (request.RequiredByBundle is not null) AddSet("required_by_bundle", request.RequiredByBundle);
        if (request.ReferenceKey is not null) AddSet("reference_key", request.ReferenceKey);
        if (request.Active is not null) AddSet("active", request.Active.Value);

        cmd.Parameters.AddWithValue("id", request.RecordId);
        cmd.CommandText = $"UPDATE {table} SET {string.Join(", ", setClauses)} WHERE instance_port_id = @id";
        await cmd.ExecuteNonQueryAsync(ct);

        var updated = await GetAsync(request.RecordKind, request.RecordId, ct);
        return new(ExternalInstanceCredentialOutcome.Ok, updated);
    }

    public async Task<ExternalInstanceCredentialOutcome> DeactivateAsync(
        string recordKind, Guid recordId, CancellationToken ct = default)
    {
        if (!RecordKindTables.TryGetValue(recordKind, out var table))
            return ExternalInstanceCredentialOutcome.RecordKindInvalid;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE {table} SET active = false, updated_at = now() WHERE instance_port_id = @id AND active = true";
        cmd.Parameters.AddWithValue("id", recordId);
        var affected = await cmd.ExecuteNonQueryAsync(ct);
        return affected > 0 ? ExternalInstanceCredentialOutcome.Ok : ExternalInstanceCredentialOutcome.NotFound;
    }
}
