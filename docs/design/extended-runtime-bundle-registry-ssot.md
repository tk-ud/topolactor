# Extended Runtime Bundle Registry SSOT

## Purpose

Topolactor の Runtime が依存する Bundle 群を分類・管理する SSOT。

- **core_runtime_bundles**: 即時設計・実装候補の Bundle セット
- **future_optional_external_surface_bundles**: roadmap aligned candidates（実装前に別SSOT必須）

外部サービスは Topolactor の runtime SSOT ではなく、human editing / intake / notification / approval / external trigger surface として扱う。

---

## External Service Policy

### Canonical Authority（Topolactorが権威を持つ対象）

- topolactor_db
- manifest / registry / schema / relation_registry
- ui_topology_tensor
- diff_log / runtime_event_log

### External Surface Role

- human_friendly_business_input
- draft_authoring / intake_queue
- preview_surface / notification_surface
- approval_surface / external_trigger_source

### Prohibited

| 禁止 | 説明 |
|------|------|
| notion_as_system_ssot | Notion を canonical SSOT にしない |
| external_tool_direct_db_write | 外部ツールからの DB 直接書き込み禁止 |
| silent_external_sync_fallback | サイレントフォールバック禁止 |
| webhook_direct_runtime_execution_without_scheduler | scheduler 経由必須 |

### Required Flow

```
external_source
  → connector_adapter
  → intake_snapshot
  → topology_payload_candidate
  → validate
  → preview
  → explicit_apply
  → canonical_runtime_route
  → diff_log_or_runtime_event_log
```

---

## Core Runtime Bundles

Topolactor runtime に直接関与する Bundle セット。各 Bundle は別 SSOT が必要。

| Bundle | Status | owner_status | SSOT |
|--------|--------|--------------|------|
| Email Bundle | not_started | assigned_to_design_ssot | [runtime-bundle-email-ssot.yaml](runtime-bundle-email-ssot.yaml) |
| Stripe Bundle | not_started | assigned_to_design_ssot | [runtime-bundle-stripe-ssot.yaml](runtime-bundle-stripe-ssot.yaml) |
| File/Storage Bundle | not_started | assigned_to_design_ssot | [runtime-bundle-file-storage-ssot.yaml](runtime-bundle-file-storage-ssot.yaml) |
| Export/SFTP Bundle | not_started | assigned_to_design_ssot | [runtime-bundle-export-sftp-ssot.yaml](runtime-bundle-export-sftp-ssot.yaml) |
| Webhook Inbox Bundle | not_started | — | 後段SSOT必要 |
| Job/Scheduler Bundle | not_started | — | 後段SSOT必要 |
| Audit/Approval Bundle | not_started | — | 後段SSOT必要 |
| Secret/Credential Bundle | not_started | — | 後段SSOT必要 |

> **Email Bundle について:**
> Email send は CLI/MCP port の **out of scope**。
> UI catalog / backend dispatch / runtime の別 SSOT で扱う。
> 現時点では CLI/MCP から email send を実行しない。

> **Stripe Bundle について:**
> Stripe paid state は webhook verification なしに確定しない。

---

## Future Optional External Surface Bundles

roadmap aligned candidates。**implemented 扱いにしない**。
各 Bundle は実装前に **separate SSOT** が必要。

roadmap 参照元:
- `docs/system-roadmap.yaml` → `M6_external_integration.future_optional_external_surfaces`
- `.agent/tasks/todo.md`

| Bundle | owner_status | scheduler_boundary |
|--------|-------------|-------------------|
| Notion Bundle | unresolved_by_design | no_direct_runtime_execution |
| Google Sheets Bundle | unresolved_by_design | no_direct_runtime_execution |
| Slack Bundle | unresolved_by_design | no_direct_runtime_execution |
| GitHub Issues Bundle | unresolved_by_design | no_direct_runtime_execution |
| Generic Webhook Bundle | unresolved_by_design | scheduler_intake_required_before_runtime |
| External REST API Connector Bundle | unresolved_by_design | no_direct_runtime_execution |
| Dynamic Support No-code Loop Bundle | unresolved_by_design | no_direct_runtime_execution |

各 Bundle の `owner_status: unresolved_by_design` は、個別SSOTが未作成であることを明示している。`implementation_gate: separate_ssot_required` が解消されるまで実装不可。

各 Bundle の必須宣言（`owner` / `trigger_kind` / `intake_snapshot_shape` / `validation_boundary` / `approval_boundary` / `scheduler_boundary` / `audit_log_boundary`）は YAML 側に記載。
詳細 SSOT 未作成の Bundle は public-safe placeholder 値を使用する（実 endpoint・credential は記載しない）。

### Generic Webhook Bundle 設計境界

```
webhook_inbox
  → connector_adapter
  → intake_snapshot
  → validate
  → preview
  → explicit_apply
  → canonical_runtime_route
```

webhook direct runtime execution without scheduler は禁止。

---

## Future Bundle Policy

1. future bundles are roadmap-aligned candidates — **implemented 扱い不可**
2. future bundles must not bypass preview / validate / apply
3. future bundles must not become canonical runtime SSOT
4. future bundles require **separate SSOT before implementation**
5. each future bundle must declare all 7 fields:
   - owner
   - trigger_kind
   - intake_snapshot_shape
   - validation_boundary
   - approval_boundary
   - scheduler_boundary
   - audit_log_boundary
6. **placeholder policy**: separate SSOT 未作成の Bundle は public-safe placeholder 値を使用してよい。実 endpoint・credential の記載は禁止。7 項目の欄を省略することは禁止。

---

## Related SSOT

- `docs/design/cli-model-context-protocols-port-ssot.yaml` — CLI/MCP read/export port
- `docs/design/runtime-orchestration-ssot.yaml` — canonical runtime route
- `docs/design/admin-console-workflow-ssot.yaml` — admin UI configuration surface
- `docs/system-roadmap.yaml` — M6 external integration roadmap
