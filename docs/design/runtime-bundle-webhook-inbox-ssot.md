# Runtime Bundle Webhook Inbox SSOT

## Purpose

Webhook Inbox Bundleの設計SSOT。

外部からのwebhookをscheduler / runtime route境界を通して受信する。webhookから直接runtime executionすることは禁止。intake_snapshot → validate → preview → explicit_apply → canonical_runtime_routeの境界を本SSOTで定義する。

---

## Authority Boundary

| 権威 | 対象 |
|------|------|
| webhook_execution_authority | scheduler_then_runtime_route_only |
| 禁止権威 | direct_runtime_execution_from_webhook / ai_autonomous_webhook_processing |

---

## Trigger Kind

**許可:**
- `webhook_intake_with_signature_verification` — 署名検証後のwebhook intake
- `scheduler_driven_event_processing` — scheduler経由でのevent処理

**禁止:**
- `webhook_direct_runtime_execution`
- `unverified_webhook_direct_execution`

---

## Webhook Intake Flow Boundary

```
webhook_received
  → signature_verification (必須・失敗時は明示的拒否)
  → intake_snapshot (検証済みpayloadのsnapshot化)
  → validate
  → preview
  → explicit_apply (scheduler → runtime_route 経由)
  → runtime_event_log
```

- **signature_verification**: 必須。失敗時はsilent fallback禁止
- **intake_snapshot**: canonical runtime routeへの入力候補
- **explicit_apply**: scheduler経由のみ。direct execution禁止

---

## Prohibited Operations

| 禁止 | 説明 |
|------|------|
| webhook_direct_runtime_execution_without_scheduler | scheduler bypass禁止 |
| intake_without_signature_verification | 未検証webhook処理禁止 |
| silent_fallback_on_intake_failure | サイレントフォールバック禁止 |
| webhook_signing_key_in_public_ssot | credential公開SSOT記載禁止 |
| runtime_destination_selection_by_webhook_inbox | 宛先選択はmanifest_dispatcher所有 |

---

## Secret Credential Boundary

webhook signing key / endpoint secret / provider URL は公開SSOTに記載しない。runtime環境変数またはsecret store経由で注入する。

---

## Scheduler Boundary

webhook eventのruntime executionは必ずscheduler経由で行う。`webhook_direct_runtime_execution_without_scheduler`は禁止。

required_flow: `webhook_inbox → scheduler → runtime_route`

---

## Approval Boundary

webhook eventからのcanonical runtime mutationはvalidate → preview → explicit apply の境界を維持する。silent implicit applicationは禁止。

---

## Audit Log Boundary

`runtime_event_log` への記録が必須。記録対象:
- webhook_received / signature_verification_success / signature_verification_failure
- intake_snapshot_created / scheduler_enqueued
- validation_completed / preview_generated / explicit_apply_initiated
- apply_completed / apply_failed

---

## Failure Policy

- 署名検証失敗: webhook明示的拒否 + runtime_event_log記録 + unverified処理fallback禁止
- intake snapshot失敗: 明示的failure + runtime_event_log記録
- apply失敗: 明示的failure + silent fallback禁止

---

## Relation to Extended Runtime Bundle Registry

- registry_entry: `webhook_inbox_bundle`
- registry_path: `docs/design/extended-runtime-bundle-registry-ssot.yaml`
- owner_status: `assigned_to_design_ssot`

---

## Relation to CLI/MCP Port

`docs/design/cli-model-context-protocols-port-ssot.yaml` でwebhook executionはout_of_scopeとして扱う。このBundleはCLI/MCP経由のwebhook execution経路を提供しない。

---

## Relation to Runtime Orchestration

webhook eventはruntime_orchestration_ssotのhook trigger kindとしてscheduler → manifest_dispatcher → runtime_route 経由で処理する。runtime_destination_selectionはmanifest_dispatcherが所有する。

---

## Future Implementation Requirements

後段実装SSOTで設計する対象:
- webhook_intake_schema_design
- webhook_event_signature_verification_service_design
- intake_snapshot_schema_design
- scheduler_hook_trigger_wiring_design
- idempotency_key_schema
- webhook_provider_adapter_design

実装はこのSSOTの境界定義に基づいて別途実装SSOTを作成してから行う。signing key / endpoint secret実体はこのSSOTに含めない。
