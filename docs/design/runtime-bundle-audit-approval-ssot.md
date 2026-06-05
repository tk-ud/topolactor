# Runtime Bundle Audit/Approval SSOT

## Purpose

Audit/Approval Bundleの設計SSOT。

承認フロー・監査ログ・export job approval境界を定義する。CLI/MCP audit logはcli-model-context-protocols-port-ssotで定義済みであり、このBundleはCLI/MCP read/export境界を破らない。UI approval / audit log write / export job approvalの境界を本SSOTで定義する。

---

## Authority Boundary

| 権威 | 対象 |
|------|------|
| approval_authority | ui_human_explicit_action_only |
| audit_log_authority | runtime_event_log_write_on_all_operations |
| 禁止権威 | ai_autonomous_approval / implicit_approval / cli_mcp_approval_trigger |

---

## Trigger Kind

**許可:**
- `ui_approval_action` — UI上での承認操作
- `admin_approval_action` — admin操作による承認
- `export_job_approval_action` — export job承認

**禁止:**
- `ai_autonomous_approval_trigger`
- `implicit_approval_trigger`
- `cli_mcp_approval_trigger`

---

## Approval Flow Boundary

```
approval_request (UI/admin から明示的作成)
  → review (validation済みの対象をレビュー)
  → approval / rejection (human_approver必須)
  → runtime_event_log
```

- **approval**: 必須。AI単独判断・暗黙的承認・タイムアウト自動承認は禁止
- **rejection**: 拒否理由をaudit logに記録
- **export_job_approval**: このBundleの承認フロー境界を通す

---

## Prohibited Operations

| 禁止 | 説明 |
|------|------|
| ai_autonomous_approval_without_human_action | AI単独承認禁止 |
| implicit_approval_on_timeout | タイムアウト自動承認禁止 |
| silent_audit_log_omission | 監査ログ省略禁止 |
| cli_mcp_read_export_boundary_violation | CLI/MCP境界違反禁止 |
| approval_state_mutation_without_audit_log | audit logなしのstate変更禁止 |

---

## Secret Credential Boundary

承認フローで使用するsecret token / credential実体は公開SSOTに記載しない。runtime環境変数またはsecret store経由で注入する。

---

## Scheduler Boundary

承認後のruntime executionはscheduler経由で行う。`approval_direct_runtime_execution_without_scheduler`は禁止。

---

## Approval Boundary

承認は必ずUI/Human explicit actionを経てから反映する。暗黙的・AI単独・タイムアウト自動承認は禁止。

export_job approval: CLI/MCP portからのexport_job approvalは禁止（CLI/MCPはcreate_export_jobまで）。

---

## Audit Log Boundary

`runtime_event_log` への記録が必須。記録対象:
- approval_requested / approval_reviewed / approval_granted / approval_rejected
- export_job_approval_initiated / export_job_approved / export_job_rejected
- audit_log_write_failure

---

## Failure Policy

- approval失敗: 明示的failure + runtime_event_log記録 + silent fallback禁止
- audit log write失敗: 明示的failure + silent skip禁止

---

## Relation to Extended Runtime Bundle Registry

- registry_entry: `audit_approval_bundle`
- registry_path: `docs/design/extended-runtime-bundle-registry-ssot.yaml`
- owner_status: `assigned_to_design_ssot`

---

## Relation to CLI/MCP Port

`docs/design/cli-model-context-protocols-port-ssot.yaml` でapproval operationはout_of_scopeとして宣言済み。このBundleはCLI/MCP経由のapproval経路を提供しない。CLI/MCP audit log読み取りはcli-model-context-protocols-port-ssotのread/export範囲内でのみ許可する。

---

## Future Implementation Requirements

後段実装SSOTで設計する対象:
- approval_request_schema_design
- approval_state_machine_design
- export_job_approval_schema_design
- audit_log_schema_design
- approval_notification_design
- approval_idempotency_key_schema
- approval_ui_component_design

実装はこのSSOTの境界定義に基づいて別途実装SSOTを作成してから行う。CLI/MCP read/export境界との整合をcli-model-context-protocols-port-ssotで確認してから実装する。
