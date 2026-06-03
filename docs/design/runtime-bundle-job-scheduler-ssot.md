# Runtime Bundle Job Scheduler SSOT

## Purpose

Job/Scheduler Bundleの設計SSOT。

cron / hook / client trigger を統合するJob Scheduler境界を定義する。runtime destination selectionはこのBundleが所有しない。manifest_dispatcherがruntime destinationを決定する。trigger_alignment / causal_order / collision_control の境界を本SSOTで定義する。

---

## Authority Boundary

| 権威 | 対象 |
|------|------|
| scheduler_authority | trigger_alignment_and_runtime_queue_only |
| 禁止権威 | runtime_destination_selection / topology_judgment |

---

## Trigger Kind

**許可:**
- `cron` — スケジュール駆動
- `hook` — イベント駆動
- `client` — UI操作駆動

**禁止:**
- `direct_runtime_execution_bypassing_scheduler`
- `silent_trigger_drop`

---

## Scheduler Contract

```
input: cron_trigger | hook_trigger | client_trigger
  → scheduler (trigger_alignment / causal_order / collision_control)
  → scheduler_aligned_runtime_event
  → manifest_dispatcher (runtime destination selection)
  → runtime_route
```

**Scheduler owns:**
- trigger_alignment / runtime_queue / runtime_phase
- causal_order / collision_control / execution_boundary

**Scheduler NOT owns:**
- runtime_destination_selection
- role_semantics / topology_meaning_judgment
- polling_primary_behavior

---

## Prohibited Operations

| 禁止 | 説明 |
|------|------|
| runtime_destination_selection_by_scheduler | 宛先選択はmanifest_dispatcher所有 |
| scheduler_bypassing_manifest_dispatcher | dispatcher bypass禁止 |
| silent_fallback_on_trigger_failure | サイレントフォールバック禁止 |
| topology_meaning_judgment_in_scheduler | topology判断はscheduler外部 |

---

## Secret Credential Boundary

外部スケジューラーprovider credential（endpoint / API key等）は公開SSOTに記載しない。runtime環境変数またはsecret store経由で注入する。topolactor内蔵schedulerの場合はcredentialは不要。

---

## Scheduler Boundary

全ての trigger (cron / hook / client) はscheduler経由でruntime routeに入力する。scheduler を bypass した direct runtime executionは禁止。

required_flow: `trigger_kind → scheduler → manifest_dispatcher → runtime_route`

---

## Approval Boundary

job実行前のapprovalが必要な場合、approval確認はscheduler外部のUI/approval境界で行う。scheduler自体がapproval判断を持たない。

---

## Audit Log Boundary

`runtime_event_log` への記録が必須。記録対象:
- trigger_received / scheduler_enqueued / execution_started
- execution_completed / execution_failed / collision_detected / queue_overflow

---

## Failure Policy

- queue_overflow: client→explicit error signal / cron・hook→explicit bool false / silent drop禁止
- trigger_failure: 明示的failure + runtime_event_log記録
- cancellation: client→explicit canceled signal / non-client→log and swallow (fire-and-forget)

---

## Relation to Extended Runtime Bundle Registry

- registry_entry: `job_scheduler_bundle`
- registry_path: `docs/design/extended-runtime-bundle-registry-ssot.yaml`
- owner_status: `assigned_to_design_ssot`

---

## Relation to CLI/MCP Port

`docs/design/cli-model-context-protocols-port-ssot.yaml` でscheduler executionはout_of_scopeとして扱う。このBundleはCLI/MCP portへのexposed interfaceを持たない。

---

## Relation to Runtime Orchestration

このBundleはruntime_orchestration_ssotのscheduler_contractを基礎とする。cron/hook/client trigger統合境界の詳細設計はこのSSOTが担う。runtime destination selectionはmanifest_dispatcherが所有する（変更なし）。

---

## Future Implementation Requirements

後段実装SSOTで設計する対象:
- job_queue_schema_design
- cron_trigger_driver_loop_design
- hook_trigger_intake_design / client_trigger_intake_design
- collision_control_implementation_design
- scheduler_overflow_policy_implementation
- job_execution_log_schema_design

実装はこのSSOTの境界定義に基づいて別途実装SSOTを作成してから行う。runtime_orchestration_ssotのscheduler_contractとの整合を維持する。
