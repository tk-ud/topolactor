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
TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1 dotnet test backend/tests/Topolactor.Integration.Tests/Topolactor.Integration.Tests.csproj --filter "ExternalPortConsumerEvidenceRepositoryLiveDbTests" --no-restore


for table in \
  topology.email_drafts \
  topology.email_approval_records \
  topology.email_delivery_evidence \
  topology.webhook_intake_snapshots \
  topology.signature_verification_evidence \
  topology.payment_state_projections \
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
  approval_reviewed; do
  check "$SEED" "\"event_type\":\"$event_type\""
done

# export_sftp transfer lifecycle is seeded as a single record_transfer_lifecycle_evidence
# step whose event types are data-defined via step_config (initiated/completed/failed/
# checksum_mismatch/retry), owned by ExportSftpBundleStepHandler — not per-event
# append_runtime_event_log rows.
check "$SEED" "record_transfer_lifecycle_evidence"
check "$SEED" "\"initiated_event_type\":\"transfer_initiated\""

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

# Bundle lifecycle evacuation: record_transfer_lifecycle_evidence is owned by the dedicated
# ExportSftpBundleStepHandler (IExternalPortBundleStepHandler), not the generic executor.
HANDLER=backend/runtime/ExportSftpBundleStepHandler.cs
check  "$HANDLER" "IExternalPortBundleStepHandler"
check  "$HANDLER" "record_transfer_lifecycle_evidence"
absent "$EXEC" "record_transfer_lifecycle_evidence"

check "$PROGRAM" "IExternalPortRuntimeEventLogRepository"
check "$PROGRAM" "IExternalPortConsumerEvidenceRepository"
check "backend/repository/NpgsqlExternalPortConsumerEvidenceRepository.cs" "physical_table_manifest_bindings"
# Evidence append/load SQL shape (incl. the dispatch_id correlation / anti-leakage predicate)
# moved from a C# tableRef switch into the topology.epce_* DB functions (hardcode-reduction).
# The repository delegates to them while keeping the allowlist + manifest-binding fail-close guard.
check "backend/repository/NpgsqlExternalPortConsumerEvidenceRepository.cs" "topology.epce_append_evidence"
check "backend/repository/NpgsqlExternalPortConsumerEvidenceRepository.cs" "topology.epce_load_projection"
check "db/topology_tables.sql" "evidence_json->>'dispatch_id' = p_entity_id"
check "$PROGRAM" "ExternalPortDispatchRuntime"
check "$PROGRAM" "RuntimeTimelineScheduler"

absent "$RUNTIME" "LoadPortRecordByCanonicalBindingAsync"
absent "$RUNTIME" "canonical_binding"
absent "$EXEC" "switch (context.PortRecord.ProviderKind"
absent "$EXEC" "switch (context.RequiredByBundle"
absent "$PROGRAM" "Smtp"
# export_sftp is a response_port consumer: the ExportSftpBundleStepHandler registration
# (SSOT consumer_bundle_step_handler_surface) is allowed, but no SFTP provider-specific
# client library/class (mirrors the stripe exception below).
check  "$PROGRAM" "ExportSftpBundleStepHandler"
absent "$PROGRAM" "SftpClient"
absent "$PROGRAM" "Renci.SshNet"
absent "$PROGRAM" "using Renci"
# stripe is a hook consumer: the /hooks/stripe generic intake endpoint is allowed
# (mirrors /hooks/webhook_inbox), but no Stripe provider-specific client library/class.
check  "$PROGRAM" "/hooks/stripe"
check  "$PROGRAM" "stripe-signature"
absent "$PROGRAM" "StripeClient"
absent "$PROGRAM" "Stripe.net"
absent "$PROGRAM" "using Stripe;"

check "$ROADMAP" "product.external_port_substrate:"
check "$ROADMAP" "status: implemented"
! rg -n '^## Bundle `external-port-substrate-implementation`|`external-port-substrate-implementation` .* partial' "$TODO" >/dev/null || fail "parent external-port-substrate-implementation todo must be removed when implemented"

printf '=== external_port_substrate parent completion guard passed ===\n'