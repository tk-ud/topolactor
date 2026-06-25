#!/usr/bin/env bash
set -euo pipefail
ROOT=$(cd "$(dirname "$0")/../.." && pwd)
cd "$ROOT"

fail() { echo "FAIL $*" >&2; exit 1; }

rg -n "class NpgsqlExternalPortPolicyRepository" backend/repository/NpgsqlExternalPortPolicyRepository.cs >/dev/null || fail "missing NpgsqlExternalPortPolicyRepository"
rg -n "IExternalPortPolicyRepository" backend/repository/NpgsqlExternalPortPolicyRepository.cs backend/Program.cs >/dev/null || fail "missing production IExternalPortPolicyRepository implementation or DI"
rg -n "AddSingleton<IExternalPortPolicyRepository>" backend/Program.cs >/dev/null || fail "missing IExternalPortPolicyRepository production DI registration"

# Guard: canonical_binding_ must NOT appear in ExternalPortDispatchRuntime consumer path.
if rg -n "canonical_binding_" backend/runtime/ExternalPortDispatchRuntime.cs; then
  fail "canonical_binding_ found in ExternalPortDispatchRuntime.cs — consumer dispatch path must use port_target_ref lane only"
fi

# Guard: canonical_binding_ must NOT appear in frontend runtime.
if find frontend/runtime -name "*.ts" -o -name "*.tsx" 2>/dev/null | xargs rg -ln "canonical_binding_" 2>/dev/null | grep -q .; then
  fail "canonical_binding_ found in frontend/runtime — consumer dispatch must use port_target_ref"
fi

for table in external_access_ports external_response_ports external_hook_ports external_port_policies external_port_policy_steps; do
  rg -n "CREATE TABLE IF NOT EXISTS topology\.${table}" db/topology_tables.sql >/dev/null || fail "missing topology.${table} DDL"
done

for kind in access_port response_port hook_port; do
  rg -n "${kind}" docs/design/external-port-substrate-ssot.yaml >/dev/null || fail "missing SSOT port_kind ${kind}"
  rg -n "${kind}" db/topology_tables.sql db/seed_empty.sql >/dev/null || fail "missing DB/seed port_kind ${kind}"
done

for kind in auth external none; do
  rg -n "credential_kind IN \('auth', 'external', 'none'\)" db/topology_tables.sql >/dev/null || fail "credential_kind constraint missing allowed values"
  rg -n -- "- ${kind}" docs/design/external-port-substrate-ssot.yaml >/dev/null || fail "missing SSOT credential_kind ${kind}"
done

python3 - <<'PY'
from pathlib import Path
import re
ssot = Path('docs/design/external-port-substrate-ssot.yaml').read_text()
seed = Path('db/seed_empty.sql').read_text()
allowed_block = ssot.split('operation_key_allowed_values:',1)[1].split('execution_rule:',1)[0]
allowed = set(re.findall(r'^\s*-\s+([a-z_]+)\s*$', allowed_block, re.M))
seed_ops = set(re.findall(r"\([^\n]*?,\s*'([a-z_]+)'\s*,\s*'\{", seed.split('external_port_policy_steps',1)[-1]))
missing = seed_ops - allowed
if missing:
    raise SystemExit(f'seed operation_key outside SSOT: {sorted(missing)}')
PY

python3 - <<'PY'
from pathlib import Path
import re
seed = Path('db/seed_empty.sql').read_text()
runtime = Path('backend/runtime/ExternalPortCredentialRefresher.cs').read_text()
seed_ops = set(re.findall(r"\([^\n]*?,\s*'([a-z_]+)'\s*,\s*'\{", seed.split('external_port_policy_steps',1)[-1]))
registry = set(re.findall(r'\["([a-z_]+)"\]\s*=', runtime))
missing = seed_ops - registry
if missing:
    raise SystemExit(f'seed operation_key missing C# registry handler: {sorted(missing)}')
PY

if rg -n "if \s*\([^\)]*(provider_kind|ProviderKind)|switch\s*\([^\)]*(provider_kind|ProviderKind)" backend/runtime backend/repository; then
  fail "provider_kind if/switch found in backend runtime/repository"
fi

if rg -n "case \"(freee|stripe|smtp|s3|sftp|sendgrid|aws|oauth)" backend/runtime backend/repository; then
  fail "provider-specific operation_key/provider handler switch found"
fi

if rg -n "class\s+(Freee|Stripe|Smtp|S3|Sftp)|Service|Refresher|ApiClient" backend/runtime | rg -n "Freee|Stripe|Smtp|S3|Sftp"; then
  fail "provider-specific runtime class/service found"
fi

if rg -n "BEGIN (RSA|OPENSSH) PRIVATE KEY|sk_live_|xox[baprs]-|AKIA[0-9A-Z]{16}|password\s*[:=]\s*[^,} ]+|client_secret\s*[:=]\s*[^,} ]+" db/seed_empty.sql docs/design/external-port-substrate-ssot.yaml backend/repository/NpgsqlExternalPortPolicyRepository.cs; then
  fail "raw credential plaintext marker found"
fi

if rg -n "external_credential_vault|encrypted_payload|provider_kind|required_by_bundle" db/auth_tables.sql; then
  fail "external credential vault leaked into auth tables"
fi

rg -n "enqueue_scheduler_event" db/seed_empty.sql docs/design/external-port-substrate-ssot.yaml >/dev/null || fail "hook scheduler boundary missing"
if rg -n "webhook_direct_runtime_execution" db/seed_empty.sql backend/runtime; then
  fail "webhook direct runtime execution marker found"
fi

rg -n "ExecutePolicyAsync_HookSeedOperations_VerifiesSignatureAndReachesSchedulerBoundary" backend/tests/Topolactor.Runtime.Tests/ExternalPortCredentialRefresherTests.cs >/dev/null || fail "missing hook seeded policy scheduler-boundary test"

# Guard: consumer bundle seed binding rows must be present (required_by_bundle).
for bundle in file_storage_bundle email_bundle stripe_bundle webhook_inbox_bundle audit_approval_bundle export_sftp_bundle; do
  rg -n "'${bundle}'" db/seed_empty.sql >/dev/null || fail "consumer bundle seed binding missing: ${bundle}"
done

# Guard: per-bundle required port_kind rows must be present in seed.
python3 - <<'PY'
from pathlib import Path
import re
seed = Path('db/seed_empty.sql').read_text()
required = {
    'file_storage_bundle':   ['access_port', 'response_port'],
    'email_bundle':          ['response_port'],
    'stripe_bundle':         ['hook_port'],
    'webhook_inbox_bundle':  ['hook_port'],
    'audit_approval_bundle': ['response_port'],
    'export_sftp_bundle':    ['response_port'],
}
tables = {
    'access_port':  'external_access_ports',
    'response_port': 'external_response_ports',
    'hook_port':    'external_hook_ports',
}
errors = []
for bundle, port_kinds in required.items():
    for pk in port_kinds:
        table = tables[pk]
        block_match = re.search(
            rf"INSERT INTO topology\.{re.escape(table)}[^;]*?'{re.escape(bundle)}'[^;]*?;",
            seed, re.S)
        if not block_match:
            errors.append(f'{bundle} missing {pk} row in topology.{table}')
if errors:
    raise SystemExit('consumer bundle port_kind guard failed:\n' + '\n'.join(errors))
PY

# Guard: consumer bundle seed rows must not contain plaintext credential values.
if rg -n "vault:[^:r]|sk_live_|AKIA[0-9A-Z]{16}|secret_access_key|smtp_password|sftp_password" db/seed_empty.sql; then
  fail "plaintext credential value found in consumer bundle seed rows"
fi

echo "OK external port substrate seed coding guard"


echo "=== canonical physical binding execution guard ==="
for needle in \
  "topology.external_access_ports" \
  "topology.external_response_ports" \
  "topology.external_hook_ports" \
  "topology.external_port_policy_steps" \
  "topology.external_credential_vault" \
  "topology.external_credential_refresh_attempt"; do
  rg -n "$needle" db/seed_empty.sql >/dev/null || fail "missing $needle in seed"
done
rg -n "INSERT INTO topology.physical_tables" db/seed_empty.sql >/dev/null || fail "missing physical_tables seed insert"
rg -n "INSERT INTO topology.physical_table_manifest_bindings" db/seed_empty.sql >/dev/null || fail "missing physical_table_manifest_bindings seed insert"
rg -n "INSERT INTO hubs.topology_manifests" db/seed_empty.sql >/dev/null || fail "missing topology_manifests seed insert"
rg -n "runtime_resolves_tableRef_to_physical_table_manifest_bindings" db/seed_empty.sql >/dev/null || fail "missing canonical_execution marker in seed"
rg -n "canonical_port_bindings" db/seed_empty.sql >/dev/null || fail "missing canonical_port_bindings entry in seed"
rg -n "LoadPortRecordByCanonicalBindingAsync" backend/runtime/ExternalPortCredentialRefresher.cs backend/repository/NpgsqlExternalPortPolicyRepository.cs >/dev/null || fail "missing LoadPortRecordByCanonicalBindingAsync implementation"
rg -n "LoadPortRecordByCanonicalBindingAsync" backend/tests >/dev/null || fail "missing LoadPortRecordByCanonicalBindingAsync tests"
rg -n "physical_table_manifest_bindings" backend/repository/NpgsqlExternalPortPolicyRepository.cs >/dev/null || fail "canonical binding SQL must use physical_table_manifest_bindings"
rg -n "wiring_physical_to_package" backend/repository/NpgsqlExternalPortPolicyRepository.cs 2>/dev/null && fail "LoadPortRecordByCanonicalBindingAsync must not use wiring_physical_to_package" || true
rg -n "EXTERNAL_PORT_CANONICAL_BINDING_TABLE_REF_PORT_KIND_MISMATCH" backend/repository/NpgsqlExternalPortPolicyRepository.cs >/dev/null || fail "missing tableRef/portKind fail-close guard"
echo "OK canonical physical binding execution guard"
