#!/usr/bin/env bash
set -euo pipefail

fail() { echo "FAIL: $*" >&2; exit 1; }
check() { local file="$1" term="$2"; rg -n --fixed-strings "$term" "$file" >/dev/null || fail "$file missing $term"; echo "OK  [term] $file -> $term"; }
absent() { local file="$1" term="$2"; ! rg -n --fixed-strings "$term" "$file" >/dev/null || fail "$file contains forbidden $term"; echo "OK  [absent] $file ∌ $term"; }

DB=db/topology_tables.sql
SEED=db/seed_empty.sql
RUNTIME=backend/runtime/ExternalPortDispatchRuntime.cs
EXEC=backend/runtime/ExternalPortCredentialRefresher.cs
PROGRAM=backend/Program.cs
TODO=.agent/tasks/todo.md
ROADMAP=docs/system-roadmap.yaml

printf '=== external_port_substrate parent completion guard ===\n'
printf '=== executing runtime boundary tests ===\n'
dotnet test backend/tests/Topolactor.Runtime.Tests/Topolactor.Runtime.Tests.csproj --filter "ExternalPortParentCompletion" --no-restore
dotnet test backend/tests/Topolactor.Integration.Tests/Topolactor.Integration.Tests.csproj --filter "ExternalPortConsumerEvidenceRepositoryLiveDbTests" --no-restore


for table in \
  topology.email_drafts \
  topology.email_approval_records \
  topology.email_delivery_evidence \
  topology.webhook_intake_snapshots \
  topology.signature_verification_evidence \
  topology.payment_state_projections \
  topology.scheduler_external_event_evidence \
  topology.audit_approval_requests \
  topology.audit_approval_evidence \
  topology.audit_notification_evidence \
  topology.sftp_transfer_log; do
  check "$DB" "CREATE TABLE IF NOT EXISTS $table"
  check "$SEED" "'$table'"
done

for bundle in \
  email_bundle \
  stripe_bundle \
  webhook_inbox_bundle \
  job_scheduler_bundle \
  audit_approval_bundle \
  export_sftp_bundle; do
  check "$SEED" "$bundle"
done

check "$SEED" "physical_search_crud_aggregate.v1"
check "$SEED" "portTargetRefActionWiring"
check "$SEED" "hook_port_receive"
check "$SEED" "checksumBoundary"
check "$SEED" "credentialProjection"
check "$SEED" "reference_only"

for event_type in \
  send_success \
  scheduler_enqueued \
  trigger_received \
  approval_reviewed \
  transfer_initiated; do
  check "$SEED" "\"event_type\":\"$event_type\""
done

check "$RUNTIME" "TryReadPortTargetRef"
check "$RUNTIME" "TryReadHookRoute"
check "$RUNTIME" "LoadHookPortRecordAsync"
check "$RUNTIME" "LoadPortRecordByIdAsync"
check "$RUNTIME" "SseEventBroadcaster"
check "$RUNTIME" "external_port_dispatch"
check "$EXEC" "load_encrypted_credential_payload"
check "$EXEC" "decrypt_for_runtime_use"
check "$EXEC" "inject_authorization_header"
check "$EXEC" "verify_signature_by_config"
check "$EXEC" "enqueue_scheduler_event"
check "$EXEC" "append_runtime_event_log"
check "$PROGRAM" "IExternalPortRuntimeEventLogRepository"
check "$PROGRAM" "IExternalPortConsumerEvidenceRepository"
check "backend/repository/NpgsqlExternalPortConsumerEvidenceRepository.cs" "physical_table_manifest_bindings"
check "backend/repository/NpgsqlExternalPortConsumerEvidenceRepository.cs" "evidence_json->>'dispatch_id' = @entityId"
check "$PROGRAM" "ExternalPortDispatchRuntime"
check "$PROGRAM" "RuntimeTimelineScheduler"

absent "$RUNTIME" "LoadPortRecordByCanonicalBindingAsync"
absent "$RUNTIME" "canonical_binding"
absent "$EXEC" "switch (context.PortRecord.ProviderKind"
absent "$EXEC" "switch (context.RequiredByBundle"
absent "$PROGRAM" "Smtp"
absent "$PROGRAM" "Stripe"
absent "$PROGRAM" "Sftp"

check "$ROADMAP" "product.external_port_substrate:"
check "$ROADMAP" "status: implemented"
! rg -n '^## Bundle `external-port-substrate-implementation`|`external-port-substrate-implementation` .* partial' "$TODO" >/dev/null || fail "parent external-port-substrate-implementation todo must be removed when implemented"

printf '=== external_port_substrate parent completion guard passed ===\n'
