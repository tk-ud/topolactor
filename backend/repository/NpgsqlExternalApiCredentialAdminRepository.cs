using Npgsql;
using Topolactor.Runtime;

namespace Topolactor.Repository;

/// <summary>
/// credential-management external_api_credential category (docs/design/admin-normal-surface-
/// projection-seed-ssot.yaml surface_axes.admin.surfaces.credentials.categories.
/// external_api_credential): create/read/update/delete/search admin projection over
/// topology.external_credential_vault / external_access_ports / external_response_ports /
/// external_hook_ports.
///
/// Generic, data-defined execution: <see cref="RecordKindTables"/> is the single source of table
/// identity (table name + PK column) every method below reads from — no provider_kind /
/// required_by_bundle / table-name C# if/switch selector exists anywhere in this file; the only
/// place recordKind selects behavior is this one lookup table, structurally unavoidable because
/// the four tables have different column shapes (never because of a business-logic branch on a
/// data value). Search/Get are ONE generic UNION ALL query across all four tables (zero C#
/// branching at all). Create/Update accept only the columns admin-normal-surface-projection-seed-
/// ssot.yaml's existing_schema_fields_allowed_for_projection permits; token_hash, encrypted_payload,
/// and encryption_key_reference are never selected/returned by any method here -- the only place a
/// plaintext secret or an encryption key reference is ever accepted is the explicit
/// PlaintextSecret/EncryptionKeyReference write-only input on create/replace-rotate, immediately
/// encrypted via IExternalCredentialCrypto and never echoed back.
/// </summary>
public interface IExternalApiCredentialAdminRepository
{
    /// <summary>
    /// expiresBefore/expiresAfter: admin-normal-surface-projection-seed-ssot.yaml
    /// surface_axes.admin.surfaces.credentials.capability_requirements.filter's expires_at
    /// dimension, implemented as a boundary range (only vault records carry expires_at; the other
    /// three record kinds always have it NULL and are excluded by either bound, matching an
    /// "expiring/expired credential" admin lookup use case rather than an exact-timestamp match).
    /// </summary>
    Task<IReadOnlyList<ExternalApiCredentialRecord>> SearchAsync(
        string? query, string? recordKind, string? providerKind, string? requiredByBundle, bool? active,
        DateTimeOffset? expiresBefore = null, DateTimeOffset? expiresAfter = null,
        CancellationToken ct = default);

    Task<ExternalApiCredentialRecord?> GetAsync(string recordKind, Guid recordId, CancellationToken ct = default);

    /// <summary>
    /// Read-only field-shape validation for a create candidate -- no DB write. Used for the
    /// mutation_confirmation_contract preview/validate stage (payload.dryRun=true) so an invalid
    /// candidate is never reported valid without ever having been checked; CreateAsync re-runs the
    /// identical checks itself rather than trusting this result as prior proof.
    /// </summary>
    ExternalApiCredentialWriteResult ValidateCreateRequest(ExternalApiCredentialCreateRequest request);

    Task<ExternalApiCredentialWriteResult> CreateAsync(
        ExternalApiCredentialCreateRequest request, CancellationToken ct = default);

    /// <summary>
    /// Read-only existence + field-shape validation for an update candidate -- no DB write.
    /// </summary>
    Task<ExternalApiCredentialWriteResult> ValidateUpdateRequestAsync(
        ExternalApiCredentialUpdateRequest request, CancellationToken ct = default);

    Task<ExternalApiCredentialWriteResult> UpdateAsync(
        ExternalApiCredentialUpdateRequest request, CancellationToken ct = default);

    Task<ExternalApiCredentialOutcome> DeactivateAsync(
        string recordKind, Guid recordId, CancellationToken ct = default);
}

public static class ExternalApiCredentialRecordKinds
{
    public const string Vault = "vault";
    public const string AccessPort = "access_port";
    public const string ResponsePort = "response_port";
    public const string HookPort = "hook_port";
    public static readonly IReadOnlyList<string> All = new[] { Vault, AccessPort, ResponsePort, HookPort };

    /// <summary>
    /// Single source of canonical physical table identity for recordKind -> table name, shared by
    /// <see cref="NpgsqlExternalApiCredentialAdminRepository"/>'s own query/write targets AND
    /// AdminRuntime.ExternalApiCredential.cs's diff_log physical_table_name -- never independently
    /// derived (e.g. by string-concatenating recordKind), so the two can never drift apart. Matches
    /// db/topology_tables.sql / docs/design/db-schema.yaml table identity exactly.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> CanonicalPhysicalTableNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Vault] = "topology.external_credential_vault",
            [AccessPort] = "topology.external_access_ports",
            [ResponsePort] = "topology.external_response_ports",
            [HookPort] = "topology.external_hook_ports",
        };
}

public enum ExternalApiCredentialOutcome
{
    Ok,
    RecordKindInvalid,
    NotFound,
    RequiredFieldMissing,
    ProhibitedFieldPresent,
}

/// <summary>
/// Unified, secret-free projection across all four record kinds. Fields not applicable to a given
/// RecordKind are null (e.g. TokenKind/ExpiresAt/RefreshBeforeSeconds/Version are vault-only;
/// UrlOrEnvReference is access_port/response_port-only; HookPath/HeaderKey/RouteKey are
/// hook_port-only; CredentialKind is port-only). Exactly the field set
/// admin-normal-surface-projection-seed-ssot.yaml's existing_schema_fields_allowed_for_projection
/// permits -- token_hash/encrypted_payload/encryption_key_reference are never members of this type.
/// </summary>
public sealed record ExternalApiCredentialRecord(
    string RecordKind,
    Guid RecordId,
    string ProviderKind,
    string RequiredByBundle,
    string? ReferenceKey,
    bool Active,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? TokenKind,
    DateTimeOffset? ExpiresAt,
    int? RefreshBeforeSeconds,
    int? Version,
    string? UrlOrEnvReference,
    string? CredentialKind,
    string? HookPath,
    string? HeaderKey,
    string? RouteKey);

/// <summary>
/// PlaintextSecret/EncryptionKeyReference are vault-only, write-only: accepted here, immediately
/// encrypted (IExternalCredentialCrypto.EncryptForVaultStorage/ComputeTokenHash), never stored on
/// ExternalApiCredentialRecord, never echoed in a response/preview/diff_log. EncryptionKeyReference
/// is admin-supplied data (deployment env var name), never a hardcoded C# default -- per
/// external-port-substrate-ssot.yaml db_guarded_credential_vault_refresher.config_defaulting_policy,
/// runtime defaults must come from data, not C#.
/// </summary>
public sealed record ExternalApiCredentialCreateRequest(
    string RecordKind,
    string ProviderKind,
    string RequiredByBundle,
    string? ReferenceKey,
    string? TokenKind,
    int? RefreshBeforeSeconds,
    string? UrlOrEnvReference,
    string? CredentialKind,
    string? HookPath,
    string? HeaderKey,
    string? RouteKey,
    string? PlaintextSecret,
    string? EncryptionKeyReference);

public sealed record ExternalApiCredentialUpdateRequest(
    string RecordKind,
    Guid RecordId,
    string? ProviderKind,
    string? RequiredByBundle,
    string? ReferenceKey,
    bool? Active,
    string? TokenKind,
    int? RefreshBeforeSeconds,
    string? UrlOrEnvReference,
    string? CredentialKind,
    string? HookPath,
    string? HeaderKey,
    string? RouteKey,
    string? NewPlaintextSecret,
    string? EncryptionKeyReference);

public sealed record ExternalApiCredentialWriteResult(
    ExternalApiCredentialOutcome Outcome,
    ExternalApiCredentialRecord? Record = null,
    string? Detail = null);

public sealed class NpgsqlExternalApiCredentialAdminRepository : IExternalApiCredentialAdminRepository
{
    private readonly string _connectionString;
    private readonly IExternalCredentialCrypto _crypto;

    private sealed record TableIdentity(string Table, string PkColumn);

    // Table names come from ExternalApiCredentialRecordKinds.CanonicalPhysicalTableNames (the single
    // source shared with AdminRuntime.ExternalApiCredential.cs's diff_log physical_table_name) --
    // only the PK column name (a physical detail this repository alone needs for SQL identifiers)
    // is added here.
    private static readonly IReadOnlyDictionary<string, TableIdentity> RecordKindTables =
        new Dictionary<string, TableIdentity>(StringComparer.Ordinal)
        {
            [ExternalApiCredentialRecordKinds.Vault] = new(
                ExternalApiCredentialRecordKinds.CanonicalPhysicalTableNames[ExternalApiCredentialRecordKinds.Vault], "credential_vault_id"),
            [ExternalApiCredentialRecordKinds.AccessPort] = new(
                ExternalApiCredentialRecordKinds.CanonicalPhysicalTableNames[ExternalApiCredentialRecordKinds.AccessPort], "access_port_id"),
            [ExternalApiCredentialRecordKinds.ResponsePort] = new(
                ExternalApiCredentialRecordKinds.CanonicalPhysicalTableNames[ExternalApiCredentialRecordKinds.ResponsePort], "response_port_id"),
            [ExternalApiCredentialRecordKinds.HookPort] = new(
                ExternalApiCredentialRecordKinds.CanonicalPhysicalTableNames[ExternalApiCredentialRecordKinds.HookPort], "hook_port_id"),
        };

    public NpgsqlExternalApiCredentialAdminRepository(string connectionString, IExternalCredentialCrypto crypto)
    {
        _connectionString = connectionString;
        _crypto = crypto;
    }

    private const string SearchColumns = """
        record_kind, record_id, provider_kind, required_by_bundle, reference_key, active,
        created_at, updated_at, token_kind, expires_at, refresh_before_seconds, version,
        url_or_env_reference, credential_kind, hook_path, header_key, route_key
        """;

    // Single UNION ALL across all four tables -- generic by construction, no per-recordKind C#
    // branch. NULL columns pad each branch to the same shape; the secret-bearing columns
    // (token_hash, encrypted_payload, encryption_key_reference) are never selected here at all.
    private const string SearchUnionSql = """
        WITH combined AS (
            SELECT 'vault' AS record_kind, credential_vault_id AS record_id, provider_kind,
                   required_by_bundle, reference_key, active, created_at, updated_at,
                   token_kind, expires_at, refresh_before_seconds, version,
                   NULL::text AS url_or_env_reference, NULL::text AS credential_kind,
                   NULL::text AS hook_path, NULL::text AS header_key, NULL::text AS route_key
            FROM topology.external_credential_vault
            UNION ALL
            SELECT 'access_port', access_port_id, provider_kind, required_by_bundle, reference_key,
                   active, created_at, updated_at,
                   NULL, NULL, NULL, NULL,
                   url_or_env_reference, credential_kind, NULL, NULL, NULL
            FROM topology.external_access_ports
            UNION ALL
            SELECT 'response_port', response_port_id, provider_kind, required_by_bundle, reference_key,
                   active, created_at, updated_at,
                   NULL, NULL, NULL, NULL,
                   url_or_env_reference, credential_kind, NULL, NULL, NULL
            FROM topology.external_response_ports
            UNION ALL
            SELECT 'hook_port', hook_port_id, provider_kind, required_by_bundle, reference_key,
                   active, created_at, updated_at,
                   NULL, NULL, NULL, NULL,
                   NULL, credential_kind, hook_path, header_key, route_key
            FROM topology.external_hook_ports
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
                OR provider_kind ILIKE '%' || @query || '%'
                OR required_by_bundle ILIKE '%' || @query || '%'
                OR COALESCE(reference_key, '') ILIKE '%' || @query || '%'
              )
          AND (@expiresBefore::timestamptz IS NULL OR expires_at < @expiresBefore)
          AND (@expiresAfter::timestamptz IS NULL OR expires_at > @expiresAfter)
        ORDER BY updated_at DESC
        LIMIT 200
        """;

    public async Task<IReadOnlyList<ExternalApiCredentialRecord>> SearchAsync(
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

        var results = new List<ExternalApiCredentialRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) results.Add(MapRecord(reader));
        return results;
    }

    public async Task<ExternalApiCredentialRecord?> GetAsync(
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

    private static ExternalApiCredentialRecord MapRecord(NpgsqlDataReader r) => new(
        RecordKind: r.GetString(0),
        RecordId: r.GetGuid(1),
        ProviderKind: r.GetString(2),
        RequiredByBundle: r.GetString(3),
        ReferenceKey: r.IsDBNull(4) ? null : r.GetString(4),
        Active: r.GetBoolean(5),
        CreatedAt: r.GetFieldValue<DateTimeOffset>(6),
        UpdatedAt: r.GetFieldValue<DateTimeOffset>(7),
        TokenKind: r.IsDBNull(8) ? null : r.GetString(8),
        ExpiresAt: r.IsDBNull(9) ? null : r.GetFieldValue<DateTimeOffset>(9),
        RefreshBeforeSeconds: r.IsDBNull(10) ? null : r.GetInt32(10),
        Version: r.IsDBNull(11) ? null : r.GetInt32(11),
        UrlOrEnvReference: r.IsDBNull(12) ? null : r.GetString(12),
        CredentialKind: r.IsDBNull(13) ? null : r.GetString(13),
        HookPath: r.IsDBNull(14) ? null : r.GetString(14),
        HeaderKey: r.IsDBNull(15) ? null : r.GetString(15),
        RouteKey: r.IsDBNull(16) ? null : r.GetString(16));

    /// <summary>
    /// Pure field-shape validation shared by <see cref="ValidateCreateRequest"/> (dryRun preview,
    /// no DB) and <see cref="CreateAsync"/> (confirmed write re-runs the identical check rather
    /// than trusting the dryRun result as prior proof).
    /// </summary>
    private static ExternalApiCredentialWriteResult? ValidateCreateFields(ExternalApiCredentialCreateRequest request)
    {
        if (!RecordKindTables.ContainsKey(request.RecordKind))
            return new(ExternalApiCredentialOutcome.RecordKindInvalid, Detail: $"Unknown recordKind '{request.RecordKind}'.");
        if (string.IsNullOrWhiteSpace(request.ProviderKind) || string.IsNullOrWhiteSpace(request.RequiredByBundle))
            return new(ExternalApiCredentialOutcome.RequiredFieldMissing, Detail: "providerKind and requiredByBundle are required.");

        if (request.RecordKind == ExternalApiCredentialRecordKinds.Vault)
        {
            if (string.IsNullOrWhiteSpace(request.TokenKind))
                return new(ExternalApiCredentialOutcome.RequiredFieldMissing, Detail: "tokenKind is required for vault records.");
            if (!string.IsNullOrWhiteSpace(request.PlaintextSecret) && string.IsNullOrWhiteSpace(request.EncryptionKeyReference))
                return new(ExternalApiCredentialOutcome.RequiredFieldMissing,
                    Detail: "encryptionKeyReference is required when plaintextSecret is supplied.");
        }
        else if (request.RecordKind is ExternalApiCredentialRecordKinds.AccessPort or ExternalApiCredentialRecordKinds.ResponsePort)
        {
            if (string.IsNullOrWhiteSpace(request.UrlOrEnvReference) || string.IsNullOrWhiteSpace(request.CredentialKind))
                return new(ExternalApiCredentialOutcome.RequiredFieldMissing,
                    Detail: "urlOrEnvReference and credentialKind are required for access_port/response_port records.");
            if (!string.Equals(request.CredentialKind, "none", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(request.ReferenceKey))
                return new(ExternalApiCredentialOutcome.RequiredFieldMissing,
                    Detail: "referenceKey is required unless credentialKind is 'none'.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.HookPath) || string.IsNullOrWhiteSpace(request.RouteKey) ||
                string.IsNullOrWhiteSpace(request.CredentialKind))
                return new(ExternalApiCredentialOutcome.RequiredFieldMissing,
                    Detail: "hookPath, routeKey, and credentialKind are required for hook_port records.");
            if (!string.Equals(request.CredentialKind, "none", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(request.ReferenceKey))
                return new(ExternalApiCredentialOutcome.RequiredFieldMissing,
                    Detail: "referenceKey is required unless credentialKind is 'none'.");
        }

        return null;
    }

    public ExternalApiCredentialWriteResult ValidateCreateRequest(ExternalApiCredentialCreateRequest request) =>
        ValidateCreateFields(request) ?? new(ExternalApiCredentialOutcome.Ok);

    public async Task<ExternalApiCredentialWriteResult> CreateAsync(
        ExternalApiCredentialCreateRequest request, CancellationToken ct = default)
    {
        var fieldError = ValidateCreateFields(request);
        if (fieldError is not null) return fieldError;
        var table = RecordKindTables[request.RecordKind];

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        Guid newId;
        if (request.RecordKind == ExternalApiCredentialRecordKinds.Vault)
        {
            byte[]? encryptedPayload = null;
            string? tokenHash = null;
            string? encryptionKeyReference = null;
            if (!string.IsNullOrWhiteSpace(request.PlaintextSecret))
            {
                // encryptionKeyReference presence already confirmed by ValidateCreateFields above.
                encryptionKeyReference = request.EncryptionKeyReference;
                encryptedPayload = _crypto.EncryptForVaultStorage(request.PlaintextSecret, encryptionKeyReference!);
                tokenHash = _crypto.ComputeTokenHash(request.PlaintextSecret);
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO topology.external_credential_vault
                    (provider_kind, required_by_bundle, token_kind, token_hash, encrypted_payload,
                     encryption_key_reference, refresh_before_seconds, reference_key, active)
                VALUES (@providerKind, @requiredByBundle, @tokenKind, @tokenHash, @encryptedPayload,
                        @encryptionKeyReference, @refreshBeforeSeconds, @referenceKey, true)
                RETURNING credential_vault_id
                """;
            cmd.Parameters.AddWithValue("providerKind", request.ProviderKind);
            cmd.Parameters.AddWithValue("requiredByBundle", request.RequiredByBundle);
            cmd.Parameters.AddWithValue("tokenKind", request.TokenKind!);
            cmd.Parameters.AddWithValue("tokenHash", (object?)tokenHash ?? DBNull.Value);
            cmd.Parameters.AddWithValue("encryptedPayload", (object?)encryptedPayload ?? DBNull.Value);
            cmd.Parameters.AddWithValue("encryptionKeyReference", (object?)encryptionKeyReference ?? DBNull.Value);
            cmd.Parameters.AddWithValue("refreshBeforeSeconds", request.RefreshBeforeSeconds ?? 300);
            cmd.Parameters.AddWithValue("referenceKey", (object?)request.ReferenceKey ?? DBNull.Value);
            newId = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        }
        else if (request.RecordKind is ExternalApiCredentialRecordKinds.AccessPort or ExternalApiCredentialRecordKinds.ResponsePort)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                INSERT INTO {table.Table}
                    (required_by_bundle, provider_kind, url_or_env_reference, credential_kind, reference_key, active)
                VALUES (@requiredByBundle, @providerKind, @urlOrEnvReference, @credentialKind, @referenceKey, true)
                RETURNING {table.PkColumn}
                """;
            cmd.Parameters.AddWithValue("requiredByBundle", request.RequiredByBundle);
            cmd.Parameters.AddWithValue("providerKind", request.ProviderKind);
            cmd.Parameters.AddWithValue("urlOrEnvReference", request.UrlOrEnvReference!);
            cmd.Parameters.AddWithValue("credentialKind", request.CredentialKind!);
            cmd.Parameters.AddWithValue("referenceKey", (object?)request.ReferenceKey ?? DBNull.Value);
            newId = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        }
        else
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO topology.external_hook_ports
                    (required_by_bundle, provider_kind, hook_path, header_key, route_key, credential_kind, reference_key, active)
                VALUES (@requiredByBundle, @providerKind, @hookPath, @headerKey, @routeKey, @credentialKind, @referenceKey, true)
                RETURNING hook_port_id
                """;
            cmd.Parameters.AddWithValue("requiredByBundle", request.RequiredByBundle);
            cmd.Parameters.AddWithValue("providerKind", request.ProviderKind);
            cmd.Parameters.AddWithValue("hookPath", request.HookPath!);
            cmd.Parameters.AddWithValue("headerKey", (object?)request.HeaderKey ?? DBNull.Value);
            cmd.Parameters.AddWithValue("routeKey", request.RouteKey!);
            cmd.Parameters.AddWithValue("credentialKind", request.CredentialKind!);
            cmd.Parameters.AddWithValue("referenceKey", (object?)request.ReferenceKey ?? DBNull.Value);
            newId = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        }

        var created = await GetAsync(request.RecordKind, newId, ct);
        return new(ExternalApiCredentialOutcome.Ok, created);
    }

    /// <summary>
    /// Field-shape check shared by <see cref="ValidateUpdateRequestAsync"/> (dryRun preview) and
    /// <see cref="UpdateAsync"/> (confirmed write re-runs it rather than trusting the dryRun result).
    /// </summary>
    private static ExternalApiCredentialWriteResult? ValidateUpdateFields(ExternalApiCredentialUpdateRequest request)
    {
        if (!RecordKindTables.ContainsKey(request.RecordKind))
            return new(ExternalApiCredentialOutcome.RecordKindInvalid, Detail: $"Unknown recordKind '{request.RecordKind}'.");
        if (request.RecordKind == ExternalApiCredentialRecordKinds.Vault &&
            !string.IsNullOrWhiteSpace(request.NewPlaintextSecret) && string.IsNullOrWhiteSpace(request.EncryptionKeyReference))
            return new(ExternalApiCredentialOutcome.RequiredFieldMissing,
                Detail: "encryptionKeyReference is required when newPlaintextSecret is supplied.");
        return null;
    }

    public async Task<ExternalApiCredentialWriteResult> ValidateUpdateRequestAsync(
        ExternalApiCredentialUpdateRequest request, CancellationToken ct = default)
    {
        var fieldError = ValidateUpdateFields(request);
        if (fieldError is not null) return fieldError;
        var existing = await GetAsync(request.RecordKind, request.RecordId, ct);
        return existing is null
            ? new(ExternalApiCredentialOutcome.NotFound, Detail: $"{request.RecordKind} {request.RecordId} was not found.")
            : new(ExternalApiCredentialOutcome.Ok, existing);
    }

    public async Task<ExternalApiCredentialWriteResult> UpdateAsync(
        ExternalApiCredentialUpdateRequest request, CancellationToken ct = default)
    {
        var fieldError = ValidateUpdateFields(request);
        if (fieldError is not null) return fieldError;
        var table = RecordKindTables[request.RecordKind];

        var existing = await GetAsync(request.RecordKind, request.RecordId, ct);
        if (existing is null)
            return new(ExternalApiCredentialOutcome.NotFound, Detail: $"{request.RecordKind} {request.RecordId} was not found.");

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Generic, data-defined metadata column set: identical field list handled the same way
        // regardless of recordKind (table name only changes which physical table the UPDATE
        // targets), never a business-logic branch on provider_kind/required_by_bundle values.
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
        if (request.RecordKind == ExternalApiCredentialRecordKinds.Vault)
        {
            if (request.TokenKind is not null) AddSet("token_kind", request.TokenKind);
            if (request.RefreshBeforeSeconds is not null) AddSet("refresh_before_seconds", request.RefreshBeforeSeconds.Value);
        }
        else
        {
            if (request.CredentialKind is not null) AddSet("credential_kind", request.CredentialKind);
            if (request.RecordKind is ExternalApiCredentialRecordKinds.AccessPort or ExternalApiCredentialRecordKinds.ResponsePort)
            {
                if (request.UrlOrEnvReference is not null) AddSet("url_or_env_reference", request.UrlOrEnvReference);
            }
            else
            {
                if (request.HookPath is not null) AddSet("hook_path", request.HookPath);
                if (request.HeaderKey is not null) AddSet("header_key", request.HeaderKey);
                if (request.RouteKey is not null) AddSet("route_key", request.RouteKey);
            }
        }

        // replace/rotate: vault-only, write-only secret input, immediately encrypted, version bumped.
        if (request.RecordKind == ExternalApiCredentialRecordKinds.Vault && !string.IsNullOrWhiteSpace(request.NewPlaintextSecret))
        {
            // encryptionKeyReference presence already confirmed by ValidateUpdateFields above.
            var encryptedPayload = _crypto.EncryptForVaultStorage(request.NewPlaintextSecret, request.EncryptionKeyReference!);
            var tokenHash = _crypto.ComputeTokenHash(request.NewPlaintextSecret);
            AddSet("encrypted_payload", encryptedPayload);
            AddSet("token_hash", tokenHash);
            AddSet("encryption_key_reference", request.EncryptionKeyReference);
            setClauses.Add("version = version + 1");
        }

        cmd.Parameters.AddWithValue("id", request.RecordId);
        cmd.CommandText = $"UPDATE {table.Table} SET {string.Join(", ", setClauses)} WHERE {table.PkColumn} = @id";
        await cmd.ExecuteNonQueryAsync(ct);

        var updated = await GetAsync(request.RecordKind, request.RecordId, ct);
        return new(ExternalApiCredentialOutcome.Ok, updated);
    }

    public async Task<ExternalApiCredentialOutcome> DeactivateAsync(
        string recordKind, Guid recordId, CancellationToken ct = default)
    {
        if (!RecordKindTables.TryGetValue(recordKind, out var table))
            return ExternalApiCredentialOutcome.RecordKindInvalid;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE {table.Table} SET active = false, updated_at = now() WHERE {table.PkColumn} = @id AND active = true";
        cmd.Parameters.AddWithValue("id", recordId);
        var affected = await cmd.ExecuteNonQueryAsync(ct);
        return affected > 0 ? ExternalApiCredentialOutcome.Ok : ExternalApiCredentialOutcome.NotFound;
    }
}
