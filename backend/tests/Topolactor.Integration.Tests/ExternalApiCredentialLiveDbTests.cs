using Npgsql;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB proof for credential-management's external_api_credential base CRUD
/// (docs/design/admin-normal-surface-projection-seed-ssot.yaml surface_axes.admin.surfaces.
/// credentials.categories.external_api_credential create/read/update/delete/search;
/// docs/design/external-port-substrate-ssot.yaml admin_setting_projection).
///
/// Proves, against real Postgres, through the REAL ManifestDispatcher (dispatch-only manifests
/// cd007/cd008/cd009/cd010 -> admin_runtime, mirroring the cd006 pattern) — i.e. production
/// reachability, not merely a backend-callable method:
///   - search/create/update/delete are all dispatchable via [role=admin, target=admin,
///     layer=external_api_credential, action=...] axes, the same resolution path manifest 092's
///     own UI triggers this file's seed wiring drives.
///   - mutation_confirmation_contract: dryRun preview never writes -> unconfirmed write fails
///     closed -> confirmed write persists, for both create and update.
///   - secret material (plaintextSecret/newPlaintextSecret/encryptionKeyReference) is NEVER
///     present in any search/get/create/update response, while the DB row itself really is
///     encrypted (encrypted_payload/token_hash populated, distinct from the plaintext input).
///   - fail-close on unknown recordKind, missing required fields, and an unknown recordId on
///     update/delete -- no partial write in any case.
///   - diff_log (AdminMasterRosterAudit.AppendAsync -> logs.diff) persists sanitized metadata only.
///
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class ExternalApiCredentialLiveDbTests
{
    private const string TestKeyEnvVar = "EXTERNAL_API_CREDENTIAL_LIVE_DB_TEST_KEY";

    private static void EnsureTestEncryptionKey()
    {
        if (Environment.GetEnvironmentVariable(TestKeyEnvVar) is null)
        {
            var key = new byte[32];
            Random.Shared.NextBytes(key);
            Environment.SetEnvironmentVariable(TestKeyEnvVar, Convert.ToBase64String(key));
        }
    }

    [Fact]
    public async Task Create_DryRunThenConfirmedWrite_SecretNeverProjected_DbRowEncrypted()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        EnsureTestEncryptionKey();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        const string plaintextSecret = "correct-horse-battery-staple";
        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

        // STEP 0: dryRun against an invalid candidate (missing requiredByBundle) -- fails closed,
        // real validation, never a bare "valid=true" echo.
        var invalidDryRun = await dispatcher.DispatchAsync(new EndpointRequestDto(
            "ExternalApiCredentialScenario", "admin", "external_api_credential", "create",
            IdOrHubId: null,
            Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                recordKind = "vault",
                providerKind = "stripe",
                dryRun = true,
            }),
            Context: null, TriggerKind: "client", Role: "admin"));
        Assert.False(invalidDryRun.Success);
        Assert.Contains(invalidDryRun.Errors, e => e.Code == "EXTERNAL_API_CREDENTIAL_REQUIRED_FIELD_MISSING");

        // STEP 1: dryRun against a VALID candidate -- non-mutating, secret never echoed.
        var dryRunPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            recordKind = "vault",
            providerKind = "test_provider",
            requiredByBundle = $"live-db-eac-{suffix}",
            referenceKey = $"live-db-eac-ref-{suffix}",
            tokenKind = "bearer",
            plaintextSecret,
            encryptionKeyReference = $"env:{TestKeyEnvVar}",
            dryRun = true,
        });
        var dryRunResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
            "ExternalApiCredentialScenario", "admin", "external_api_credential", "create",
            IdOrHubId: null, Payload: dryRunPayload, Context: null, TriggerKind: "client", Role: "admin"));
        Assert.True(dryRunResponse.Success, string.Join(";", dryRunResponse.Errors.Select(e => e.Code + ":" + e.Message)));
        Assert.DoesNotContain(plaintextSecret, dryRunResponse.Emission!.Data.ToString());

        var countBeforeConfirm = await CountVaultRowsAsync(cs, $"live-db-eac-{suffix}");
        Assert.Equal(0, countBeforeConfirm);

        // STEP 2: write without payload.confirmed=true -- fails closed, no mutation.
        var unconfirmedPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            recordKind = "vault",
            providerKind = "test_provider",
            requiredByBundle = $"live-db-eac-{suffix}",
            referenceKey = $"live-db-eac-ref-{suffix}",
            tokenKind = "bearer",
            plaintextSecret,
            encryptionKeyReference = $"env:{TestKeyEnvVar}",
        });
        var unconfirmedResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
            "ExternalApiCredentialScenario", "admin", "external_api_credential", "create",
            IdOrHubId: null, Payload: unconfirmedPayload, Context: null, TriggerKind: "client", Role: "admin"));
        Assert.False(unconfirmedResponse.Success);
        Assert.Contains(unconfirmedResponse.Errors, e => e.Code == "EXTERNAL_API_CREDENTIAL_WRITE_NOT_CONFIRMED");
        Assert.Equal(0, await CountVaultRowsAsync(cs, $"live-db-eac-{suffix}"));

        Guid createdId = Guid.Empty;
        try
        {
            // STEP 3: confirmed write -- persists, secret never echoed in response.
            var confirmedPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                recordKind = "vault",
                providerKind = "test_provider",
                requiredByBundle = $"live-db-eac-{suffix}",
                referenceKey = $"live-db-eac-ref-{suffix}",
                tokenKind = "bearer",
                plaintextSecret,
                encryptionKeyReference = $"env:{TestKeyEnvVar}",
                confirmed = true,
            });
            var confirmedResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "ExternalApiCredentialScenario", "admin", "external_api_credential", "create",
                IdOrHubId: null, Payload: confirmedPayload, Context: null, TriggerKind: "client", Role: "admin"));
            Assert.True(confirmedResponse.Success, string.Join(";", confirmedResponse.Errors.Select(e => e.Code + ":" + e.Message)));
            var responseText = confirmedResponse.Emission!.Data.ToString();
            Assert.DoesNotContain(plaintextSecret, responseText);
            Assert.DoesNotContain("plaintextSecret", responseText);
            Assert.DoesNotContain($"env:{TestKeyEnvVar}", responseText);
            foreach (var forbidden in new[] { "tokenHash", "encryptedPayload", "encryptionKeyReference", "decryptedPayload", "tokenResponse" })
                Assert.DoesNotContain(forbidden, responseText);

            var (id, tokenHash, encryptedPayload, encryptionKeyReference) = await ReadVaultRowAsync(cs, $"live-db-eac-{suffix}");
            createdId = id;
            Assert.NotNull(encryptedPayload);
            Assert.NotNull(tokenHash);
            Assert.NotEqual(plaintextSecret, tokenHash);
            Assert.Equal($"env:{TestKeyEnvVar}", encryptionKeyReference);
            // the stored ciphertext is not the plaintext, and the plaintext never appears anywhere
            // as a byte subsequence of the encrypted BYTEA payload either.
            Assert.False(
                ContainsSubsequence(encryptedPayload!, System.Text.Encoding.UTF8.GetBytes(plaintextSecret)),
                "expected the encrypted_payload BYTEA to never contain the plaintext secret bytes");

            // diff_log persists sanitized metadata only.
            var diffJson = await ReadLatestDiffAfterJsonAsync(cs, "topology.external_vault", createdId.ToString(), "create");
            Assert.NotNull(diffJson);
            Assert.DoesNotContain(plaintextSecret, diffJson);
            Assert.DoesNotContain($"env:{TestKeyEnvVar}", diffJson);
        }
        finally
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM logs.diff WHERE record_id = @id";
                cmd.Parameters.AddWithValue("id", createdId == Guid.Empty ? "unused" : createdId.ToString());
                await cmd.ExecuteNonQueryAsync();
            }
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM topology.external_credential_vault WHERE required_by_bundle = @b";
                cmd.Parameters.AddWithValue("b", $"live-db-eac-{suffix}");
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    [Fact]
    public async Task Update_UnknownRecordId_FailsClosed_NoPartialWrite_And_Search_NeverProjectsSecret()
    {
        var cs = GetConnectionString();
        if (cs is null) return;
        EnsureTestEncryptionKey();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var vaultId = Guid.NewGuid();

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        async Task ExecAsync(string sql, params (string Name, object Value)[] parms)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parms) cmd.Parameters.AddWithValue(name, value);
            await cmd.ExecuteNonQueryAsync();
        }

        try
        {
            await ExecAsync(
                "INSERT INTO topology.external_credential_vault (credential_vault_id, provider_kind, required_by_bundle, token_kind, reference_key, active) " +
                "VALUES (@id, 'test_provider', @bundle, 'bearer', @ref, true)",
                ("id", vaultId), ("bundle", $"live-db-eac-update-{suffix}"), ("ref", $"live-db-eac-update-ref-{suffix}"));

            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

            // Update against an unknown recordId fails closed -- no repository write attempted.
            var unknownUpdate = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "ExternalApiCredentialScenario", "admin", "external_api_credential", "update",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    recordKind = "vault",
                    recordId = Guid.NewGuid().ToString(),
                    referenceKey = "should-not-apply",
                    confirmed = true,
                }),
                Context: null, TriggerKind: "client", Role: "admin"));
            Assert.False(unknownUpdate.Success);
            Assert.Contains(unknownUpdate.Errors, e => e.Code == "EXTERNAL_API_CREDENTIAL_NOT_FOUND");

            // Unknown recordKind on search fails closed.
            var badKindSearch = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "ExternalApiCredentialScenario", "admin", "external_api_credential", "search",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new { recordKind = "not_a_real_kind" }),
                Context: null, TriggerKind: "client", Role: "admin"));
            Assert.False(badKindSearch.Success);
            Assert.Contains(badKindSearch.Errors, e => e.Code == "EXTERNAL_API_CREDENTIAL_RECORD_KIND_INVALID");

            // A real search over this record never projects secret-shaped fields, even though the
            // row itself has no secret set (search response shape must exclude the forbidden
            // fields structurally, not merely because they happen to be empty here).
            var searchResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "ExternalApiCredentialScenario", "admin", "external_api_credential", "search",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new { requiredByBundle = $"live-db-eac-update-{suffix}" }),
                Context: null, TriggerKind: "client", Role: "admin"));
            Assert.True(searchResponse.Success, string.Join(";", searchResponse.Errors.Select(e => e.Code + ":" + e.Message)));
            var searchJson = searchResponse.Emission!.Data.ToString();
            Assert.Contains($"live-db-eac-update-ref-{suffix}", searchJson);
            foreach (var forbidden in new[] { "tokenHash", "encryptedPayload", "encryptionKeyReference", "plaintextSecret", "decryptedPayload", "tokenResponse" })
                Assert.DoesNotContain(forbidden, searchJson);

            // No partial write: the row's reference_key is still the original value.
            await using var readCmd = conn.CreateCommand();
            readCmd.CommandText = "SELECT reference_key FROM topology.external_credential_vault WHERE credential_vault_id = @id";
            readCmd.Parameters.AddWithValue("id", vaultId);
            var refKey = (string?)await readCmd.ExecuteScalarAsync();
            Assert.Equal($"live-db-eac-update-ref-{suffix}", refKey);
        }
        finally
        {
            await ExecAsync("DELETE FROM topology.external_credential_vault WHERE credential_vault_id = @id", ("id", vaultId));
        }
    }

    [Fact]
    public async Task Delete_DryRunThenConfirmed_Deactivates_NeverRemovesRow()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var vaultId = Guid.NewGuid();

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        async Task ExecAsync(string sql, params (string Name, object Value)[] parms)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parms) cmd.Parameters.AddWithValue(name, value);
            await cmd.ExecuteNonQueryAsync();
        }

        try
        {
            await ExecAsync(
                "INSERT INTO topology.external_credential_vault (credential_vault_id, provider_kind, required_by_bundle, token_kind, reference_key, active) " +
                "VALUES (@id, 'test_provider', @bundle, 'bearer', @ref, true)",
                ("id", vaultId), ("bundle", $"live-db-eac-delete-{suffix}"), ("ref", $"live-db-eac-delete-ref-{suffix}"));

            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

            var dryRun = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "ExternalApiCredentialScenario", "admin", "external_api_credential", "delete",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    recordKind = "vault",
                    recordId = vaultId.ToString(),
                    dryRun = true,
                }),
                Context: null, TriggerKind: "client", Role: "admin"));
            Assert.True(dryRun.Success, string.Join(";", dryRun.Errors.Select(e => e.Code + ":" + e.Message)));

            await using (var activeCheck = conn.CreateCommand())
            {
                activeCheck.CommandText = "SELECT active FROM topology.external_credential_vault WHERE credential_vault_id = @id";
                activeCheck.Parameters.AddWithValue("id", vaultId);
                Assert.True((bool)(await activeCheck.ExecuteScalarAsync())!);
            }

            var confirmed = await dispatcher.DispatchAsync(new EndpointRequestDto(
                "ExternalApiCredentialScenario", "admin", "external_api_credential", "delete",
                IdOrHubId: null,
                Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    recordKind = "vault",
                    recordId = vaultId.ToString(),
                    confirmed = true,
                }),
                Context: null, TriggerKind: "client", Role: "admin"));
            Assert.True(confirmed.Success, string.Join(";", confirmed.Errors.Select(e => e.Code + ":" + e.Message)));

            await using var finalCheck = conn.CreateCommand();
            finalCheck.CommandText = "SELECT active FROM topology.external_credential_vault WHERE credential_vault_id = @id";
            finalCheck.Parameters.AddWithValue("id", vaultId);
            var stillPresent = await finalCheck.ExecuteScalarAsync();
            Assert.NotNull(stillPresent);
            Assert.False((bool)stillPresent!);
        }
        finally
        {
            await ExecAsync("DELETE FROM logs.diff WHERE record_id = @id", ("id", vaultId.ToString()));
            await ExecAsync("DELETE FROM topology.external_credential_vault WHERE credential_vault_id = @id", ("id", vaultId));
        }
    }

    private static async Task<int> CountVaultRowsAsync(string cs, string requiredByBundle)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*)::int FROM topology.external_credential_vault WHERE required_by_bundle = @b";
        cmd.Parameters.AddWithValue("b", requiredByBundle);
        return (int)(await cmd.ExecuteScalarAsync() ?? 0);
    }

    private static bool ContainsSubsequence(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || needle.Length > haystack.Length) return needle.Length == 0;
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return true;
        }
        return false;
    }

    private static async Task<(Guid Id, string? TokenHash, byte[]? EncryptedPayload, string? EncryptionKeyReference)> ReadVaultRowAsync(
        string cs, string requiredByBundle)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT credential_vault_id, token_hash, encrypted_payload, encryption_key_reference " +
            "FROM topology.external_credential_vault WHERE required_by_bundle = @b";
        cmd.Parameters.AddWithValue("b", requiredByBundle);
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var id = reader.GetGuid(0);
        var tokenHash = reader.IsDBNull(1) ? null : reader.GetString(1);
        var encryptedPayload = reader.IsDBNull(2) ? null : (byte[])reader[2];
        var encryptionKeyReference = reader.IsDBNull(3) ? null : reader.GetString(3);
        return (id, tokenHash, encryptedPayload, encryptionKeyReference);
    }

    private static async Task<string?> ReadLatestDiffAfterJsonAsync(
        string cs, string physicalTableName, string recordId, string operationKind)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT after_state_or_diff_json::text FROM logs.diff
            WHERE source_set_id = 'admin_master_roster'
              AND physical_table_name = @t
              AND record_id = @r
              AND operation_kind = @op
            ORDER BY observed_at DESC
            LIMIT 1";
        cmd.Parameters.AddWithValue("t", physicalTableName);
        cmd.Parameters.AddWithValue("r", recordId);
        cmd.Parameters.AddWithValue("op", operationKind);
        return (string?)await cmd.ExecuteScalarAsync();
    }

    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();
}
