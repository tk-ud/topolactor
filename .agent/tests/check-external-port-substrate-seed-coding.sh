#!/usr/bin/env bash
set -euo pipefail
ROOT=$(cd "$(dirname "$0")/../.." && pwd)
cd "$ROOT"

fail() { echo "FAIL $*" >&2; exit 1; }

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
seed_ops = set(re.findall(r"'([a-z_]+)'", seed.split('external_port_policy_steps',1)[-1]))
seed_ops = {op for op in seed_ops if op.endswith('_record') or op.endswith('_reference') or op.endswith('_request') or op.endswith('_http') or op.endswith('_response') or op.endswith('_config') or op.endswith('_event') or op.endswith('_log') or op == 'fail_close'}
missing = seed_ops - allowed
if missing:
    raise SystemExit(f'seed operation_key outside SSOT: {sorted(missing)}')
PY

python3 - <<'PY'
from pathlib import Path
import re
seed = Path('db/seed_empty.sql').read_text()
runtime = Path('backend/runtime/ExternalPortCredentialRefresher.cs').read_text()
seed_ops = set(re.findall(r"'([a-z_]+)'", seed.split('external_port_policy_steps',1)[-1]))
seed_ops = {op for op in seed_ops if op.endswith('_record') or op.endswith('_reference') or op.endswith('_request') or op.endswith('_http') or op.endswith('_response') or op.endswith('_config') or op.endswith('_event') or op.endswith('_log') or op == 'fail_close'}
registry = set(re.findall(r'\["([a-z_]+)"\]\s*=', runtime))
missing = seed_ops - registry
if missing:
    raise SystemExit(f'seed operation_key missing C# registry handler: {sorted(missing)}')
PY

if rg -n "if \s*\([^\)]*(provider_kind|ProviderKind)|switch\s*\([^\)]*(provider_kind|ProviderKind)" backend/runtime; then
  fail "provider_kind if/switch found in backend/runtime"
fi

if rg -n "class\s+(Freee|Stripe|Smtp|S3|Sftp)|Service|Refresher|ApiClient" backend/runtime | rg -n "Freee|Stripe|Smtp|S3|Sftp"; then
  fail "provider-specific runtime class/service found"
fi

if rg -n "BEGIN (RSA|OPENSSH) PRIVATE KEY|sk_live_|xox[baprs]-|AKIA[0-9A-Z]{16}|password\s*[:=]\s*[^,} ]+|client_secret\s*[:=]\s*[^,} ]+" db/seed_empty.sql docs/design/external-port-substrate-ssot.yaml; then
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

echo "OK external port substrate seed coding guard"
