#!/usr/bin/env bash
set -euo pipefail

forbidden_classes='(FreeeRefresher|FreeeApiClient|RefreshFreeeAccessTokenAsync|StripeRefresher)'
if rg -n "$forbidden_classes" backend docs db frontend .agent/tasks; then
  echo "provider-specific refresher/client class is forbidden" >&2
  exit 1
fi

if rg -n "provider_kind\s*(switch|if)|switch\s*\([^)]*provider_kind|if\s*\([^)]*provider_kind|ProviderKind\s*(switch|if)|switch\s*\([^)]*ProviderKind|if\s*\([^)]*ProviderKind" backend/runtime/ExternalPortCredentialRefresher.cs; then
  echo "provider_kind switch/if is forbidden in generic refresher" >&2
  exit 1
fi

if ! rg -n "CREATE TABLE IF NOT EXISTS topology\.external_credential_vault" db/topology_tables.sql >/dev/null; then
  echo "external credential vault DDL missing from topology schema" >&2
  exit 1
fi

if ! rg -n "encrypted_payload" db/topology_tables.sql >/dev/null; then
  echo "encrypted payload boundary missing" >&2
  exit 1
fi

if ! rg -n "CREATE TABLE IF NOT EXISTS auth\.credentials" db/auth_tables.sql >/dev/null; then
  echo "auth credentials table missing" >&2
  exit 1
fi

if rg -n "external_credential_vault|encrypted_payload|provider_kind|required_by_bundle" db/auth_tables.sql; then
  echo "external credential vault must not be mixed into auth.credentials/auth schema" >&2
  exit 1
fi

if rg -n "(access_token|refresh_token|client_secret)\s*[:=]\s*['\"][^'\"]{8,}" docs db frontend backend --glob '!backend/runtime/ExternalPortCredentialRefresher.cs'; then
  echo "possible plaintext token value found in SSOT/seed/projection/log surface" >&2
  exit 1
fi

for term in ExternalCredentialRefreshLease Version ExpiresAt RefreshBeforeSeconds IExternalCredentialVaultRepository IExternalCredentialCrypto IExternalTokenRefresher; do
  rg -n "$term" backend/runtime/ExternalPortCredentialRefresher.cs >/dev/null || {
    echo "generic refresher boundary missing: $term" >&2
    exit 1
  }
done

repo_file="backend/repository/NpgsqlExternalCredentialVaultRepository.cs"
if [[ ! -f "$repo_file" ]]; then
  echo "production Npgsql external credential vault repository missing" >&2
  exit 1
fi

for term in \
  "class NpgsqlExternalCredentialVaultRepository" \
  "IExternalCredentialVaultRepository" \
  "topology.external_credential_vault" \
  "topology.external_credential_refresh_attempt" \
  "AND active = true" \
  "locked_until IS NULL OR locked_until <= @now" \
  "RETURNING version" \
  "encrypted_payload = @encryptedPayload" \
  "token_hash = @tokenHash" \
  "expires_at = @expiresAt" \
  "version = version + 1" \
  "AND version = @expectedVersion" \
  "EXTERNAL_CREDENTIAL_STALE_VERSION_OR_INACTIVE"; do
  rg -n --fixed-strings "$term" "$repo_file" >/dev/null || {
    echo "Npgsql credential vault repository missing atomic guard: $term" >&2
    exit 1
  }
done

if ! rg -n "AddSingleton<IExternalCredentialVaultRepository>" backend/Program.cs >/dev/null; then
  echo "production credential vault repository DI registration missing" >&2
  exit 1
fi

if rg -n "provider_kind\s*(switch|if)|switch\s*\([^)]*provider_kind|if\s*\([^)]*provider_kind|ProviderKind\s*(switch|if)|switch\s*\([^)]*ProviderKind|if\s*\([^)]*ProviderKind" "$repo_file"; then
  echo "provider_kind switch/if is forbidden in credential vault repository" >&2
  exit 1
fi
