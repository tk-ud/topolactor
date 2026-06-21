#!/usr/bin/env bash
set -euo pipefail

fail() { echo "FAIL: $*" >&2; exit 1; }
check() { local file="$1" term="$2"; rg -n --fixed-strings "$term" "$file" >/dev/null || fail "$file missing $term"; echo "OK  [term] $file -> $term"; }
absent() { local file="$1" term="$2"; ! rg -n --fixed-strings "$term" "$file" >/dev/null || fail "$file contains forbidden $term"; echo "OK  [absent] $file ∌ $term"; }

SEED=db/seed_empty.sql
RUNTIME=backend/runtime/ExternalPortDispatchRuntime.cs
PROGRAM=backend/Program.cs
TODO=.agent/tasks/todo.md
ROADMAP=docs/system-roadmap.yaml

printf '=== webhook_inbox port consumer guard ===\n'

printf '=== executing webhook_inbox runtime tests ===\n'
dotnet test backend/tests/Topolactor.Runtime.Tests/Topolactor.Runtime.Tests.csproj \
    --filter "WebhookInboxPortConsumer" --no-restore

# Seed: 12-step policy chain (full audit boundary) with webhook_received, signature_verification_success, and full intake→apply chain
check "$SEED" '"event_type":"webhook_received"'
check "$SEED" '"event_type":"signature_verification_success"'
check "$SEED" '"evidence_table_ref":"topology.signature_verification_evidence"'
check "$SEED" '"evidence_table_ref":"topology.webhook_intake_snapshots"'
check "$SEED" 'webhook_inbox_bundle'

# Runtime: event log injection and failure recording
check "$RUNTIME" "signature_verification_failure"
check "$RUNTIME" "IExternalPortRuntimeEventLogRepository"
absent "$RUNTIME" "ProviderKind =="
absent "$RUNTIME" "case \"generic-webhook\""
absent "$RUNTIME" "case \"webhook\""

# HTTP intake endpoint
check "$PROGRAM" "/hooks/webhook_inbox"
check "$PROGRAM" "x-webhook-signature"
check "$PROGRAM" "WEBHOOK_INTAKE_BODY_INVALID"

# No provider-specific branching
absent "$PROGRAM" "ProviderKind =="
absent "$PROGRAM" "WebhookInbox"

# Roadmap: status must be implemented
rg -A5 'product\.webhook_inbox_port_consumer:' "$ROADMAP" | grep -q 'status: implemented' \
    || fail "roadmap product.webhook_inbox_port_consumer status must be implemented"

# Todo: webhook-inbox-port-consumer must not be partial
! rg -n 'webhook-inbox-port-consumer.*partial' "$TODO" >/dev/null \
    || fail "todo.md still has webhook-inbox-port-consumer as partial"

printf '=== webhook_inbox port consumer guard passed ===\n'
