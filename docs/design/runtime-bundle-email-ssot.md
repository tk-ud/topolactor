# Runtime Bundle Email SSOT

## Purpose

Email送信Bundleの設計SSOT。

Email sendはUI/Human approvalを経た副作用として扱う。CLI/MCPからのemail sendは禁止。AI判断による自律送信は禁止。UI catalog / backend dispatch / runtime の実装は後段実装対象。

---

## Authority Boundary

| 権威 | 対象 |
|------|------|
| email_send_authority | ui_approval_then_backend_dispatch |
| 禁止権威 | cli_mcp_port / ai_autonomous_judgment / frontend_direct_send |

---

## Trigger Kind

**許可:**
- `ui_approval_confirmed` — UIでのユーザー承認完了後
- `backend_dispatch_after_approval` — 承認確認後のbackend dispatch

**禁止:**
- `cli_trigger` / `mcp_tool_trigger` / `ai_autonomous_trigger`
- `webhook_direct_trigger_without_scheduler`

---

## Email Flow Boundary

```
draft (UI catalog)
  → preview (ユーザー確認)
  → approval (明示的ユーザー承認 — 必須)
  → dispatch (backend dispatch)
  → delivery_log (runtime_event_log)
```

- **draft**: UI catalogで構造化テンプレートから作成
- **preview**: 送信前内容・宛先・件名確認（silent skipは禁止）
- **approval**: 必須。AI単独判断・暗黙承認は禁止
- **dispatch**: 承認確認後のbackend dispatch経由
- **delivery_log**: 成功・失敗ともにruntime_event_logへ記録

---

## Prohibited Operations

| 禁止 | 説明 |
|------|------|
| email_send_from_cli_or_mcp | CLI/MCP port境界違反 |
| email_send_from_ai_judgment_alone | AI単独判断での送信禁止 |
| email_send_without_human_approval | 承認なし送信禁止 |
| silent_fallback_on_send_failure | 送信失敗のサイレントフォールバック禁止 |
| smtp_credential_in_public_ssot | credential公開SSOT記載禁止 |

---

## Secret Credential Boundary

SMTP provider credential（host / port / API key / sender address）は公開SSOTに記載しない。runtime環境変数またはsecret store経由で注入する。

---

## Approval Boundary

Email sendは必ずUI/Human approvalを経てから実行する。承認なし・暗黙の承認・AI単独判断での送信は禁止。

---

## Scheduler Boundary

Email sendのバックグラウンド実行はscheduler経由で行う。`webhook_direct_runtime_execution_without_scheduler`は禁止。

---

## Audit Log Boundary

`runtime_event_log` への記録が必須。記録対象:
- dispatch_initiated / send_success / send_failure / approval_recorded

---

## Failure Policy

- 送信失敗は `runtime_event_log` に記録し、callerに明示的に返す
- silent retry / silent fallback は禁止

---

## Relation to Extended Runtime Bundle Registry

- registry_entry: `email_bundle`
- registry_path: `docs/design/extended-runtime-bundle-registry-ssot.yaml`
- owner_status: `assigned_to_design_ssot`

---

## Relation to CLI/MCP Port

`docs/design/cli-model-context-protocols-port-ssot.yaml` で `email_send` は `explicitly_out_of_scope` として宣言済み。このBundleはそのboundaryを尊重し、CLI/MCP経由のemail send経路を提供しない。

---

## Future Implementation Requirements

後段実装SSOTで設計する対象:
- email_draft_surface_design
- email_template_catalog_design
- backend_email_dispatch_service_design
- smtp_api_provider_integration_design
- delivery_log_schema_design
- approval_confirmation_ui_component
- idempotency_key_schema

実装はこのSSOTの境界定義に基づいて別途実装SSOTを作成してから行う。credential / endpoint実体はこのSSOTに含めない。
