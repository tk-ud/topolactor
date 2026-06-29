# Roadmap Manual Acceptance Checksheet TODO

目的: `docs/system-roadmap.yaml` の feature bundle 全文を分類し、手動受入 / hand-debug 用チェックシートを作るためのユーザー向け TODO。

このファイルは `.agent/tasks/todo.md` の canonical unresolved bundle queue を置き換えない。Roadmap status / implemented 判定の正本でもない。`product-nocode-loop-acceptance` の実施準備として、Roadmap 全体から受入観点を抽出する作業メモとして扱う。

---

## Scope

対象repo: `github.com/tk-ud/topolactor`

Worktype: `todo_maintenance` / manual acceptance planning

正本:
- `docs/system-roadmap.yaml`

必ず読む:
- `AGENTS.md`
- `.agent/rules/rule.md`
- `.agent/README.md`
- `.agent/prompt/todo-maintenance.md`
- `.agent/protocols/todo-carry-over.md`

Foundation SSOT:
- `docs/framework-core.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`

補助 SSOT:
- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/sql-attention-logs-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/team-markdown-dashboard-saved-view-ssot.yaml`
- `docs/design/user-facing-helper-manual-ssot.yaml`

---

## Boundary

- Roadmap は status SSOT / feature catalog として読む。
- 実装完了判定は Roadmap だけで行わない。
- implemented 済み bundle を未実装扱いへ戻さない。
- production_ready と manual acceptance pending を混同しない。
- 手動受入は product experience acceptance 用であり、API atom ごとの必須 gate ではない。
- optional external surfaces / helper manual policy を M6 combined UX acceptance の必須確認へ混入させない。

---

## Feature Classification TODO

### 1. Runtime 基盤

対象 bundle:
- `product.core_runtime_route`
- `product.projection_and_output_lanes`
- `product.registry_attractor_runtime_dispatch_handler`
- `product.instance_port_substrate`
- `product.scheduler_job_manifest_substrate`

確認観点:
- [ ] client trigger が scheduler -> manifest dispatcher -> dispatchable runtime を通る。
- [ ] runtime destination は manifest / SSOT vocabulary に従い、silent fallback しない。
- [ ] response / db_notify / SSE projection boundary が崩れていない。
- [ ] frontend が topology / SQL Attention / persistence authority を持たない。
- [ ] production_ready pending と implementation residue を混同していない。

### 2. Admin / No-code Authoring

対象 bundle:
- `product.admin_topology_authoring`
- `product.dynamic_support_nocode_loop`
- `product.frontend_projection_surface_ux_acceptance`

確認観点:
- [ ] admin authoring guidance から操作開始できる。
- [ ] preview / validate / apply boundary が UI 上で見える。
- [ ] validation error が原因・対象・修復示唆を表示する。
- [ ] lifecycle state が draft / validated / applied / failed / persisted で区別できる。
- [ ] admin CSV/JSON import と authoring route が M6 loop として通し確認できる。
- [ ] frontend は DB direct write / topology judgment / promotion authority を持たない。

### 3. SQL Attention / Feedback

対象 bundle:
- `product.sql_attention_observation_runtime`
- `backend.sql_attention_logs_current`
- `backend.sql_attention_hub_current`
- `backend.sql_attention_scheduler_exploration`
- `backend.sql_attention_logs_attention_persistence`
- `backend.sql_attention_phase_vector`
- `backend.sql_attention_topology_projection`

確認観点:
- [ ] SQL Attention feedback projection が UI 上で確認できる。
- [ ] recommendation / attention candidate が fixed route を自動上書きしない。
- [ ] feedback は projection-only surface として表示される。
- [ ] candidate adoption は explicit user action を要求する。
- [ ] topology projection は evidence read-only で、topology mutation authority を持たない。

### 4. External Port / Consumer

対象 bundle:
- `product.external_port_substrate`
- `product.email_port_consumer`
- `product.stripe_port_consumer`
- `product.file_storage_port_consumer`
- `product.export_sftp_port_consumer`
- `product.webhook_inbox_port_consumer`
- `product.audit_approval_port_consumer`

確認観点:
- [ ] external tool は human editing / intake / preview / approval surface であり system SSOT ではない。
- [ ] external_source -> connector_adapter -> intake_snapshot -> validate -> preview -> explicit_apply -> canonical_runtime_route の境界が崩れていない。
- [ ] credential / secret / raw provider payload が projection されない。
- [ ] failure は explicit rejection / runtime_event_log / evidence として残り silent fallback しない。
- [ ] provider-specific runtime / client / handler を追加しない方針が維持されている。

### 5. Markdown / Saved View

対象 bundle:
- `product.preset_db_seed_registration`
- `product.component_markdown_authoring_projection`
- `product.md_viewer_projection_component`
- `product.completed_preset_seed_projection_gate`

確認観点:
- [ ] Markdown body を runtime SSOT として扱っていない。
- [ ] saved view は persisted rendered projection として表示される。
- [ ] completed preset seed validation が refresh / clone / rebind の gate になっている。
- [ ] md_viewer は projection component であり physical record / topology mutation authority を持たない。
- [ ] seed invalid state が explicit error になり silent fallback しない。

### 6. CI / Governance

対象 bundle:
- `product.system_ci_contract_audit`
- `system_ci.dotnet_ssot_wiring_audit_tests`
- `system_ci.topology_registration`
- `system_ci.hub_registration`
- `system_ci.scheduler_runtime`
- `system_ci.component_registration`

確認観点:
- [ ] CI は SSOT wiring audit / diagnostics evidence eligibility であり product runtime authority ではない。
- [ ] shell check と dotnet semantic test の責務を混同しない。
- [ ] CI compatibility entry を新規 product bundle として重複扱いしない。
- [ ] check result を manual acceptance の補助 evidence として扱い、UX受入そのものと混同しない。

### 7. Future / Out of Scope

対象 bundle:
- `product.external_optional_surface_bundle_gate`
- `product.helper_manual_policy`

確認観点:
- [ ] optional external connector は future surface として分類する。
- [ ] helper manual policy は helper artifact / onboarding / language policy の作業であり、M6 combined UX acceptance の必須実装範囲へ混入させない。
- [ ] future bundle を production gap と誤判定しない。

---

## Manual Acceptance Checksheet Output Requirements

作成先候補:
- `.agent/checklists/check-roadmap-manual-acceptance.md`

チェックシートに必ず含める列:
- feature classification
- Roadmap bundle id
- status
- production_ready
- user operation / manual step
- expected visible result
- authority boundary
- explicit failure / no silent fallback
- evidence_ref / detail_ref
- NG condition
- relation to `product.dynamic_support_nocode_loop`
- implemented regression guard memo

---

## Acceptance Criteria

- [ ] `docs/system-roadmap.yaml` の `implementation_registry` 全文を分類している。
- [ ] Roadmap 全文の内容を TODO へ写経せず、手動受入観点へ圧縮している。
- [ ] `product.dynamic_support_nocode_loop` combined UX を中心に据えている。
- [ ] authoring guidance -> SQL Attention feedback -> M6 admin loop の通し確認に落とせる。
- [ ] implemented 済み bundle を未実装へ戻していない。
- [ ] production_ready pending / live CI pending / manual acceptance pending を区別している。
- [ ] authority boundary と explicit failure を各分類に含めている。
- [ ] optional / future / helper policy を必須受入 scope へ混入させていない。
