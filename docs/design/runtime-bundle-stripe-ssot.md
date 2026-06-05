# Runtime Bundle Stripe SSOT

## Purpose

Stripe決済BundleのSSOT。

webhook verificationなしにpaid stateを確定しない。UI表示やclient callbackだけで支払済み状態にしない。webhook inbox / event verification / payment state projection / ledger binding の境界を本SSOTで定義する。

---

## Authority Boundary

| 権威 | 対象 |
|------|------|
| payment_state_authority | verified_webhook_event_only |
| 禁止権威 | ui_display_confirmation / client_callback_confirmation / unverified_webhook_payload |

---

## Trigger Kind

**許可:**
- `webhook_intake_with_verification` — 署名検証済みwebhook
- `scheduler_driven_event_reconciliation` — scheduler経由の調整処理

**禁止:**
- `ui_callback_direct_state_mutation`
- `unverified_webhook_direct_execution`
- `client_side_payment_confirmation`

---

## Payment Flow Boundary

```
webhook_inbox (受信 — 検証前は信頼しない)
  → event_verification (署名検証 — webhook_verification_required: true)
  → payment_state_projection (検証済みeventから投影)
  → ledger_binding (確定後のledger記録)
  → runtime_event_log
```

- **webhook_inbox**: 受信のみ。検証前の直接state変更は禁止
- **event_verification**: `webhook_verification_required: true`。失敗時はexplicit rejection
- **payment_state_projection**: 検証済みeventのみ許可。UI callbackのみは禁止
- **ledger_binding**: payment state確定後に実行

---

## Prohibited Operations

| 禁止 | 説明 |
|------|------|
| paid_state_from_ui_callback_only | UI callback確認のみでのpaid state確定禁止 |
| paid_state_from_unverified_webhook | 未検証webhookからのstate確定禁止 |
| silent_fallback_on_verification_failure | 検証失敗のサイレントフォールバック禁止 |
| stripe_secret_key_in_public_ssot | secret key公開SSOT記載禁止 |
| webhook_secret_in_public_ssot | webhook secret公開SSOT記載禁止 |
| direct_db_write_without_verification | 未検証でのDB直接書き込み禁止 |

---

## Secret Credential Boundary

Stripe secret key / webhook secret / endpoint実体は公開SSOTに記載しない。runtime環境変数またはsecret store経由で注入する。

---

## Approval Boundary

payment UIは自作可能。ただし最終的なpaid state反映は必ずwebhook event verificationを経てから行う。UI/client confirmationのみでのstate確定は禁止。

---

## Scheduler Boundary

Stripe webhookのruntime executionはscheduler経由で行う。`webhook_direct_runtime_execution_without_scheduler`は禁止。

---

## Audit Log Boundary

`runtime_event_log` への記録が必須。記録対象:
- webhook_received / verification_success / verification_failure / payment_state_projected / ledger_binding_completed

---

## Failure Policy

- verification失敗: explicit rejection + runtime_event_log記録。unverified stateへのfallback禁止
- state projection失敗: 明示的expose。partial commitのsilent fallback禁止

---

## Relation to Extended Runtime Bundle Registry

- registry_entry: `stripe_bundle`
- registry_path: `docs/design/extended-runtime-bundle-registry-ssot.yaml`
- owner_status: `assigned_to_design_ssot`

---

## Relation to CLI/MCP Port

`docs/design/cli-model-context-protocols-port-ssot.yaml` で `payment_approval` は `explicitly_out_of_scope` として宣言済み。このBundleはそのboundaryを尊重する。

---

## Future Implementation Requirements

後段実装SSOTで設計する対象:
- webhook_inbox_schema_design
- stripe_event_verification_service_design
- payment_state_projection_schema_design
- ledger_binding_schema_design
- idempotency_key_schema

実装はこのSSOTの境界定義に基づいて別途実装SSOTを作成してから行う。credential / endpoint実体はこのSSOTに含めない。
