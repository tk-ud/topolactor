# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `in_progress` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `helper-manual` | helper reference artifact / admin helper projection | not_started | 1 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `seed-template-runtime-interaction-assignment` | Seed/template projection runtimeInteractionId assignment path | implemented | 1 | `product.dynamic_support_nocode_loop` / seed-template projection adoption carry-over | `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`, `docs/design/react-schema-topology-seed-translator-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 2 | `product.dynamic_support_nocode_loop` | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |
| `admin-surface-topology-seed-conversion` | Admin hardcoded surface topology seed conversion | not_started | 5 subBundle | `product.dynamic_support_nocode_loop` / admin hardcoded surface retirement | `docs/design/admin-normal-surface-projection-seed-ssot.yaml`, `docs/design/react-schema-topology-seed-translator-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`, `docs/design/instance-port-substrate-ssot.yaml` |
| `test-orchestration-review` | Seed conversion後の proof / test orchestration review | not_started | 1 | proof surface carry-over | `docs/design/pipeline-continuity-ssot.yaml` |
| `frontend-canonical-surface-structure-label-boundary` | Seed conversion後の frontend canonical surface label boundary | not_started | 1 | frontend canonical UI structure/wiring surfaces | canonical surface UI structure/wiring SSOTs, `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml` |
| `admin-console-workflow-step-wording-boundary` | Seed conversion後の admin console workflow wording boundary | not_started | 1 | `product.admin_topology_authoring` | `docs/design/admin-console-workflow-ssot.yaml` |

注: 上記 consumer bundle は PR#460 により seed binding / credential_requirement / policy_steps が完了済み。client/UI consumer (email / audit_approval) は UI Builder portTargetRef 配線前提が完了済み。hook consumer (stripe / webhook_inbox) は hook_port seed binding が完了済み (UI Builder portTargetRef 配線ではない)。残作業は各 bundle consumer todo 参照。provider-specific runtime / client は追加しない。UI Builder form preset は docs/design/ui-builder-preset-ecosystem-ssot.yaml / db/physical_search_crud_aggregate_preset_seed.sql の CRUD preset seed の写像/派生であり、新規 UI runtime / 専用 component 実装ではない。

---

## Report scope migration classification (2026-07-07; reorganized 2026-07-09)

削除前 ref `018b80fa23949a67a7b03f1853cc9c3f2e45ce3c` の `.agent/reports/frontend-ui-audit-bundle-semantic-frame.md` と `.agent/reports/ui-projection-surface-gap-audit-2026-07-07.md` を全文確認した分類。report 由来 scope は finding 番号や route 名ではなく owning SSOT / Bundle / 意味要素単位で扱う。プロンプト発行者の scope が狭い可能性があるため、実装 Agent は関連箇所を追加調査し、SSOT / wiring / test-proof surface の不足を blocking として記録してから product 実装へ進む。

- `admin-surface-topology-seed-conversion`: **統合 / 主実装順序**。旧 `runtime-route-taxonomy-hardcoded-route-retirement`、`initial-projection-side-admin-crud-seed-route-retirement`、および hardcoded admin surface seed replacement scope を統合する。`/admin/enums` / `/admin/users` / `/admin/scheduler` / `/admin/team-dashboard` の route 別 Bundle 化は禁止し、意味要素 subBundle として `admin-dashboard` / `team-dashboard` / `credential-management` / `admin-enum` / `scheduler-settings` を扱う。
- `test-orchestration-review`: **seed conversion 完了後の後段**。旧 `pipeline-continuity-frontend-route-seed-proof` は実装 Bundle ではなく、seed conversion 後に test tier / scenario harness / route-presence-test replacement を点検・見直す proof orchestration review として扱う。route absence 単独や hardcoded route presence test を canonical proof として残さない。
- `frontend-canonical-surface-structure-label-boundary`: **seed conversion 後の後段**。語彙・label boundary 修正は seed conversion 実装に混ぜず、conversion 完了後に canonical surface label / technical disclosure scope として扱う。
- `admin-console-workflow-step-wording-boundary`: **seed conversion 後の後段**。Step wording 修正は seed conversion 実装に混ぜず、conversion 完了後に admin console workflow wording scope として扱う。
- `product-nocode-loop-acceptance`: **acceptance_pending 維持**。Agent が仕様確定や受入完了を代行せず、オーナーが統合 UX / manual acceptance scope を精査する。
- `helper-manual`: **仕様確定後 scope 維持**。helper manual は仕様確定前に実装へ進めず、user-facing helper manual SSOT に従う後段 scope とする。
- `ui-projection-surface-architecture-reinforcement`: **移管済み / 維持**。PR574 reference evidence、`/demo` cleanup、UI Builder inspection、`ProjectionShell` route/package/manifest awareness、`projectionInput` collection preservation、`runtimeInteraction identity / projection-time idempotency identity` future direction はこの bundle の PR574後残 scope として維持する。route seed 化 / label boundary / admin Step wording / broad pipeline proof は無理に混ぜ潰さない。

---
## Bundle `seed-template-runtime-interaction-assignment`

**Status:** `implemented`
**Primary SSOT:** `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`, `docs/design/react-schema-topology-seed-translator-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`
**追加 governance SSOT:** `docs/governance/agent-ui-protocol-ssot.yaml`, `docs/governance/agent-governance-routing-ssot.yaml`
**追加 governance Reference:** `docs/governance/reference/agent-ui-tool-output-reference.yaml`, `docs/governance/reference/agent-ui-senario-tmp-reference.yaml`, `docs/governance/reference/agent-ui-negative-boundary-reference.yaml`
**関連 implemented evidence:** PR577 `runtimeInteraction identity / projection-time idempotency identity` implemented scope。UI Builder `layout_patch:apply` 経路の `AssignRuntimeInteractionIds` は implemented として扱い、未実装へ戻さない。PR574 backend ledger execution gate も implemented evidence として維持し、再実装対象へ戻さない。
**追加 governance scope:** `agent-ui-initial-contract` が routed prompt / required protocol / triggered protocol を tool-first route で返すため、旧 manual protocol read 表現が prompt / tool / governance reference / local_test に残っていないかを同一 Bundle 内で閉じる。索引は変更しない。

### 問題点

PR577 で UI Builder apply 経路では `runtimeInteractions[]` の `runtimeInteractionId` が backend persistence boundary で付与され、projection UI が persisted identity を idempotency key 生成へ渡す経路は成立した。

一方で、translator / template generator / credential management screen seed 由来の projection 昇格経路について、active persisted `layout_patch_json` になる前に同じ runtimeInteractionId assignment boundary を必ず通る保証が TODO 化されていない。

この未整理のままだと、seed / template 由来 projection が `runtimeInteractions[]` を持つ場合に、`AssignRuntimeInteractionIds` を通らず persisted projection へ到達し、projection UI が stable `runtimeInteractionId` を持てず、PR574 ledger idempotency gate への identity component が fallback `nodeId + interactionIndex` へ残る危険がある。

併せて、`agent-ui-initial-contract` は routed prompt / required protocol / triggered protocol を tool-first output として返す route へ移行しているが、prompt router / governance reference / tool docstring / output wording / local_test summary wording に旧 manual protocol read 表現が残ると、Agent が UI contract 後に別途 protocol を読む route へ戻る危険がある。

**PR580 follow-up (`agent-ui-protocol-obligations-normalization`) で用語更新**: required/triggered protocol の tool-first 表現は、protocol 全文を execution procedure として inline する `protocol_trigger_hints[].content` から、その protocol 自身の見出しに基づく正規化構造field群 `protocol_obligations[]`（`protocol_path` / `route_mode` / `applies` / `trigger_condition` / `judgment_scope` / `foundation_ssot_read_gate` / `blocking_conditions` / `pass_conditions` / `required_fields` / `classification_vocab` / `output_boundary` / `fallback_protocol_ref` / `tool_first_instruction_note`）へ置き換わった。以下の本文中の `protocol_trigger_hints[].content` 表現は `protocol_obligations[]` の意味で読み替える（本Bundleの `implemented` 判定・受入条件の実質は変わらない — tool-first route が required/triggered protocol を返す、という契約自体は維持されている）。

特に、tool-first route では `prompt_content`（routed prompt の full text）/ `protocol_obligations[]`（routed protocol の normalized structured fields; full text は各entryの `fallback_protocol_ref` 経由）が resolved worktype / trigger の contract body であり、manual protocol file read は fallback route、または tool output 欠損・routing不整合を検証する場合に限定される必要がある。

### 目的

seed / template generator / credential management screen seed 由来 projection でも、active persisted `layout_patch_json` に昇格する時点で backend runtimeInteractionId assignment boundary を必ず通し、ProjectionShell / renderEmission / uiEventEffectRunner が persisted `runtimeInteractionId` を読み、PR574 ledger idempotency gate へ安定した idempotency key を渡せる経路を確定する。

同時に、`agent-ui-initial-contract` を manual protocol file read 置換 route として扱う governance 表現を、SSOT / reference / tool / prompt router / local_test / tests で整合させる。manual protocol read は fallback route、または tool output 欠損・routing不整合を検証する場合に限定し、tool-first route では `prompt_content`（full text）/ `protocol_obligations[]`（normalized structured fields）が routed contract body であることを明確化する。

### 改善方針

- translator / template generator は `runtimeInteractionId` を生成しない。
- ここで禁止する生成は、final / persisted identity authority としての `runtimeInteractionId` 生成である。
- translator / template generator は `runtimeInteractions[]` を含む candidate / template を扱ってよい。
- translator / template generator / seed artifact は `runtimeInteractionId` の final authority を持たない。
- `runtimeInteractionId` assignment authority は backend persistence boundary に限定する。
- seed JSON / fixture / generated artifact へ固定 `runtimeInteractionId` を直書きしない。
- seed candidate / template output / credential management screen seed が active projection に昇格する adoption / apply / compile / persist 経路を調査する。
- active persisted `layout_patch_json` へ到達する直前に、`ApplyConfirmedLayoutPatchAsync` / `AssignRuntimeInteractionIds` と同等の backend persistence assignment boundary を必ず通す。
- `runtimeInteractions[]` を含む `layout_patch_json` が assignment boundary を通らず persist される bypass を検出する。
- 必要に応じて seed-adoption proof surface / translator proof surface / credential management seed proof を追加する。
- PR577 の UI Builder apply 経路 implemented 判定、PR574 の backend ledger execution gate implemented 判定を未実装扱いへ戻さない。
- `agent-ui-initial-contract` の tool-first route を正本化し、旧表現 `protocol excerpts` / manual protocol required read / fallback-only route の混在を解消する。
- `agent-ui-initial-contract start` が返す `prompt_content`（full text）と `protocol_obligations[]`（normalized structured fields; full text は各entryの `fallback_protocol_ref` 経由）を、resolved worktype / trigger の routed contract body として扱う。
- prompt router の `required_reads` は tool-first route と fallback route の意味を分離する。
- `.agent/prompt/*.md` の protocol file 直指定は、tool-first route では `protocol_obligations[]` として取得済みであることを明示する。
- manual protocol read は fallback route、または tool output 欠損・routing不整合を検証する場合に限定し、tool-first route の追加必須手順として残さない。
- `agent-ui-local-test` 側の output / summary / checklist route も、旧 manual protocol route へ戻す表現を持たないよう確認・修正する。
- UI contract output を SSOT authority / proof completion / implemented judgment として扱わない境界は維持する。
- `SSOT -> wiring -> test/proof surface -> implementation` の順序を崩さない。
- tool更新を伴う場合は、Agent UI route reference / prompt router / worktype routing / local_test / tests を併せて更新する。

### 対応資料

- `AGENTS.md`
- `.agent/rules/rule.md`
- `.agent/README.md`
- `.agent/skills/agent-workflow.md`
- `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`
- `docs/design/react-schema-topology-seed-translator-ssot.yaml`
- `docs/design/react-schema-topology-seed-translator-production-policy.md`
- `docs/design/instance-port-substrate-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `docs/governance/agent-ui-protocol-ssot.yaml`
- `docs/governance/agent-governance-routing-ssot.yaml`
- `docs/governance/reference/agent-ui-tool-output-reference.yaml`
- `docs/governance/reference/agent-ui-senario-tmp-reference.yaml`
- `docs/governance/reference/agent-ui-negative-boundary-reference.yaml`
- `.agent/routes/worktype-required-protocols.yaml`
- `.agent/tools/README.md`
- `db/seed_empty.sql`
- `.agent/tests/fixtures/react-schema-topology-seed-translator/credential-management-0092.input.json`
- `.agent/tests/fixtures/react-schema-topology-seed-translator/credential-management-0092.topology-seed.input.json`

### 対象ファイル名

- `.agent/scripts/react_schema_topology_seed_translator.py`
- `.agent/tools/react-schema-topology-seed-translator`
- `.agent/scripts/check_react_schema_topology_seed_translator.py`
- `.agent/scripts/agent_tools/schema_seed_translator_entry_gate.py`
- `backend/repository/NpgsqlUiTopologyRepository.cs`
- `frontend/runtime/renderEmission.ts`
- `frontend/runtime/uiEventEffectRunner.ts`
- `frontend/runtime/visualLayoutUtils.ts`
- ProjectionShell / projection runtime identity forwarding related files discovered by Agent
- `db/seed_empty.sql`
- `.agent/tests/fixtures/react-schema-topology-seed-translator/credential-management-0092.input.json`
- `.agent/tests/fixtures/react-schema-topology-seed-translator/credential-management-0092.topology-seed.input.json`
- future seed/template adoption path files that persist active `layout_patch_json`
- `.agent/tools/agent-ui-initial-contract`
- `.agent/scripts/agent_tools/agent_ui_initial_contract.py`
- `.agent/tools/agent-ui-local-test`
- `.agent/scripts/agent_tools/agent_ui_local_test.py`
- `.agent/scripts/agent_tools/agent_ui_common.py`
- `docs/governance/agent-ui-protocol-ssot.yaml`
- `docs/governance/reference/agent-ui-tool-output-reference.yaml`
- `docs/governance/reference/agent-ui-senario-tmp-reference.yaml`
- `docs/governance/reference/agent-ui-negative-boundary-reference.yaml`
- `docs/governance/agent-governance-routing-ssot.yaml`
- `.agent/routes/worktype-required-protocols.yaml`
- `.agent/prompt/audit.md`
- `.agent/prompt/specific.md`
- `.agent/prompt/implementation-change.md`
- `.agent/prompt/design-change.md`
- `.agent/prompt/todo-maintenance.md`
- `.agent/prompt/existing-pr-update.md`
- `.agent/tests/check-worktype-routing.sh`
- `.agent/tests/check-completion-judgment.sh`
- `.agent/tests/check-structure.sh`
- `AGENTS.md` / `.agent/rules/rule.md` / `.agent/README.md` / `.agent/skills/agent-workflow.md` は確認対象。旧表現が確認された場合のみ変更候補。
- future Agent UI route / prompt wording proof files

### 対象関数名

- `ApplyConfirmedLayoutPatchAsync`
- `AssignRuntimeInteractionIds`
- `HasValidRuntimeInteractionId`
- `convert_node_to_seed_record`
- `build_topology_ui_seed_candidate`
- `flatten_topology_ui_seed_tree`
- `validate_flat_seed_records`
- `validate_translator_entry`
- `extract_compile_snapshot`
- future seed/template adoption functions that persist active `layout_patch_json`
- `_cmd_start`
- `_read_full`
- `_cmd_resolve_ssot`
- `_cmd_sections`
- `_cmd_end`
- `build_parser`
- `_cmd_run_worktype_tests`
- `_cmd_read_senario_tmp`
- `_cmd_checklist`
- `_cmd_checks`
- `_cmd_summary`
- `_run_check`
- `_checklist_items`
- `worktypes`
- `reject_output_flag`
- `parse_senario_tmp`

### 受入条件

- translator / template generator は `runtimeInteractionId` を生成しない。
- ここで禁止する生成は、final / persisted identity authority としての `runtimeInteractionId` 生成である。
- translator / template generator は `runtimeInteractions[]` を含む candidate / template を扱ってよい。
- translator / template generator / seed artifact は `runtimeInteractionId` の final authority を持たない。
- seed JSON / fixture / generated artifact へ固定 `runtimeInteractionId` を直書きしない。
- credential management screen seed 由来 projection の昇格経路が特定されている。
- seed / template 由来 projection が active persisted `layout_patch_json` になる前に backend runtimeInteractionId assignment boundary を通る。
- `runtimeInteractions[]` を含む active projection persist bypass がない、または blocking proof で検出される。
- ProjectionShell / renderEmission / uiEventEffectRunner は persisted `runtimeInteractionId` を forward する。
- PR574 backend ledger execution gate と PR577 UI Builder apply assignment は implemented evidence として維持され、再実装対象へ戻さない。
- `agent-ui-initial-contract start` は routed prompt の full text と、required protocol / triggered protocol の normalized `protocol_obligations[]` を tool-first contract body として返す。
- `protocol excerpts` 等の旧表現が tool / reference / prompt router / local_test から除去または fallback-only 表現へ修正されている。
- `.agent/prompt/*.md` の protocol file 直指定は、tool-first route では `protocol_obligations[]` として取得済みであることが明示されている。
- manual protocol read は fallback route、または tool output 欠損・routing不整合を検証する場合に限定し、tool-first route の追加必須手順として残さない。
- fallback-only prompt / protocol / checklist が resolved worktype の正規 contract として扱われない。
- `agent-ui-local-test` の `run-worktype-tests` / `read-senario-tmp` / `checklist` / `checks` / `summary` が、tool-first route と矛盾する旧 manual protocol read を誘導しない。
- UI contract output は SSOT authority / proof completion / implemented judgment として扱われない。
- `agent-ui-local-test` と required checks で、Agent UI route / prompt wording / worktype routing の整合が検出可能になっている。
- SSOT / wiring / test-proof / implementation / Bundle範囲の整合が報告されている。

### Governance NG boundary

- `SSOT -> wiring -> test/proof surface -> implementation` の順序を崩す。
- SSOT確定前に実装へ進む。
- wiring未特定のまま関数単位・ファイル単位の小粒修正へ縮退する。
- test / proof surface 未定義のまま実装変更で完了を主張する。
- 実装既存状態をSSOTとして扱う。
- todo本文の対象ファイル・対象関数を落としてBundle scopeを狭める。
- `.agent/tools/react-schema-topology-seed-translator` / entry gate / check script / fixture / `db/seed_empty.sql` をscope外にする。
- candidate / generated artifact / template output を active topology authority として扱う。
- backend assignment boundary のwiring確認なしに persisted `layout_patch_json` の成立を判断する。
- PR574 / PR577 の既存 implemented evidence を再実装scopeへ戻す。
- `agent-ui-initial-contract` を使わず、manual protocol read を tool-first 正規routeとして扱う。
- UI contract が返した routed prompt full text / required protocol / triggered protocol の `protocol_obligations[]` を使わず、Agent判断で別protocolへ逸脱する。
- fallback-only の prompt / protocol / checklist を resolved worktype の正規contractとして扱う。
- `agent-ui-initial-contract` の scenario contract を作らず実装へ進む。
- UI contract output を SSOT authority / implemented judgment / proof completion として扱う。
- toolを更新しながら、reference / prompt router / worktype routing / local_test / tests を更新しない。
- `agent-ui-local-test` を省略し、required checks を完了代替なしに飛ばす。
- todo status更新だけで implemented を主張する。

---


## Bundle `admin-surface-topology-seed-conversion`

**Status:** `not_started`
**Primary SSOT:** `docs/design/admin-normal-surface-projection-seed-ssot.yaml`（PR587で追加されたowning / primary design authority）, `docs/design/react-schema-topology-seed-translator-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`, `docs/design/instance-port-substrate-ssot.yaml`
**補助 SSOT:** `docs/design/pipeline-continuity-ssot.yaml`（proof 更新観点のみ）, `docs/design/user-facing-helper-manual-ssot.yaml`（helper/manual 後段境界のみ）
**統合元:** `runtime-route-taxonomy-hardcoded-route-retirement`, `initial-projection-side-admin-crud-seed-route-retirement`
**SubBundle:** `admin-dashboard`, `team-dashboard`, `credential-management`, `admin-enum`, `scheduler-settings`

### 問題点

現行 TODO は hardcoded route retirement / initial admin CRUD seed / proof / label / wording / helper manual を別 Bundle として分けすぎており、admin hardcoded surface の seed replacement 実装順序が分断されている。

route 名単位で `/admin/enums` / `/admin/users` / `/admin/scheduler` / `/admin/team-dashboard` を別 Bundle 化すると、React-like Schema → translator tool → topology UI seed → seed登録 → projection render の共通工程が見えなくなり、route 削除だけを先行する危険がある。

また、React-like Schema candidate / generated topology seed candidate を active topology authority と誤認したり、translator が db seed SQL / runtime DB state / frontend runtime state を source authority として読む危険がある。

### 目的

admin hardcoded surface を意味要素ごとの topology UI seed conversion scope に統合し、各 subBundle を route 名ではなく admin surface の意味責務で実装する。共通順序は、hardcoded surface 読込、React-like Schema 作成、translator tool 変換、topology UI seed 生成、seed 登録、canonical projection/admin mechanism による render / action wiring 確認、必要な proof 更新、最後に hardcoded route / island / old route-presence test 削除とする。

### 改善方針

- 共通工程を全 subBundle で固定する。
  1. hardcoded surface 読込。
  2. React-like Schema 作成。
  3. `.agent/tools/react-schema-topology-seed-translator` で topology UI seed candidate へ変換。
  4. topology UI seed 生成結果を seed 登録へ写像。
  5. canonical projection/admin mechanism で projection render / backend action wiring を確認。
  6. 必要な proof 更新を行う。
  7. 最後に hardcoded route / island / old route-presence test を削除する。
- route 削除は seed conversion と render/action wiring 確認の後にのみ行う。
- React-like Schema candidate / generated topology UI seed candidate は intake / draft artifact であり、active topology authority として扱わない。
- translator の source authority は SSOT YAML と caller-supplied inputText に限定し、db seed SQL / runtime DB state / backend response / frontend runtime state を読ませない。
- `/demo` と `/runtime-status` はこの seed replacement scope に混ぜない。
- proof orchestration の総点検は後段 `test-orchestration-review` に分離し、この実装 Bundle の completion 判定へ混ぜない。
- 語彙修正・label boundary・Step wording は seed conversion 後段 scope とし、この seed 実装 Bundle に混ぜない。

### Implemented history guard

- PR574 / PR577 / PR578 の UI Builder idempotency 系実装は implemented history として扱い、`admin-surface-topology-seed-conversion` の再実装 scope へ戻さない。
- 実装済み境界: UI Builder `layout_patch:apply` 経路の backend persistence boundary、`AssignRuntimeInteractionIds`、persisted `runtimeInteractionId` forwarding、ProjectionShell / renderEmission / uiEventEffectRunner の projection-time idempotency identity、PR574 backend ledger execution gate。
- `admin-surface-topology-seed-conversion` は React-like Schema / translator / topology UI seed / seed registration / projection render / backend action wiring を扱う。idempotency authority の再設計・再実装は scope 外であり、必要な場合は regression proof / wiring確認のみ扱う。

### SubBundle scope

#### `admin-dashboard`

- **意味 scope:** admin landing / navigation / guide entry。
- **含めるもの:** `/admin` の landing 表示責務、admin navigation、authoring guide entry、canonical admin route への導線。
- **含めないもの:** business projection としての `/admin` 扱い、route registry authority の新規定義。

#### `team-dashboard`

- **意味 scope:** saved markdown / team projection view。
- **含めるもの:** saved Markdown view、rendered Markdown、source / binding / completed preset seed summary の projection boundary、md_viewer read projection boundary。
- **含めないもの:** Markdown body を runtime SSOT と見なすこと、standalone route authority の追加。

#### `credential-management`

- **意味 scope:** credential / admin user / auth / external / instance settings の統合管理。
- **含めるもの:** `/admin/users`、admin user、user_auth、external、instance_settings、credential requirement projection、credential-backed instance connection setting。
- **統合方針:** credential management は user_auth / external / instance_settings の mode/category 統合方針に従い、admin user を分離しない。
- **NG:** standalone route / dedicated panel / raw physical table row editor を追加しない。credential management から admin user を分離しない。

#### `admin-enum`

- **意味 scope:** enum dictionary / enum group / enum item / status enum dependency。
- **含めるもの:** enum CRUD seed、user-role-status など status enum dependency、enum dictionary / group / item の管理 projection。
- **含めないもの:** physical table row editor としての enum route 復活、route presence を proof とすること。

#### `scheduler-settings`

- **意味 scope:** scheduler job settings projection / create-edit-disable action wiring。
- **含めるもの:** scheduler job settings projection、create / edit / disable action wiring、scheduler configuration CRUD seed。
- **含めないもの:** scheduler runtime policy hidden in frontend constants、diagnostics route replacement。

### 対応資料

- `AGENTS.md`
- `.agent/rules/rule.md`
- `.agent/README.md`
- `.agent/tasks/todo.md`
- `docs/design/react-schema-topology-seed-translator-ssot.yaml`
- `docs/design/instance-port-substrate-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `docs/design/db-schema.yaml`
- `docs/design/user-facing-helper-manual-ssot.yaml`
- `docs/design/component-catalog-classification-ssot.yaml`
- `docs/design/ui-ux-primitive-catalog-ssot.yaml`
- `docs/design/ui-builder-preset-ecosystem-ssot.yaml`
- `docs/design/auth-db-session-credential-ssot.yaml`
- `docs/design/admin-master-roster-management-ssot.yaml`
- `docs/design/enum-dictionary-ssot.yaml`
- `docs/design/team-markdown-dashboard-saved-view-ssot.yaml`
- `docs/design/scheduler-job-manifest-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- PR587 design checkpoint / proof completion evidence: `c2c87cc Add admin/normal surface projection seed SSOT and proof (#587)`（design SSOT completion only; seed conversion implementation completionではない）

### 対象ファイル名

- `.agent/tasks/todo.md`
- `db/seed_empty.sql`
- `.agent/scripts/react_schema_topology_seed_translator.py`
- `.agent/tools/react-schema-topology-seed-translator`
- `.agent/tests/fixtures/react-schema-topology-seed-translator/*`
- translator fixtures discovered by Agent
- `frontend/routes/admin/index.tsx`
- `frontend/routes/admin/enums.tsx`
- `frontend/routes/admin/users.tsx`
- `frontend/routes/admin/team-dashboard.tsx`
- `frontend/routes/admin/scheduler.tsx`
- `frontend/routes/admin/*`
- related admin islands discovered by Agent
- canonical projection/admin render surfaces that consume seed-backed CRUD
- frontend tests for admin route registry, seed renderability, projection render, backend action wiring, and old route-presence replacement

### 対象関数名

- `build_topology_ui_seed_candidate`
- `convert_node_to_seed_record`
- `flatten_topology_ui_seed_tree`
- `validate_flat_seed_records`
- future seed registration functions discovered by Agent
- future projection render functions discovered by Agent
- future backend action wiring functions discovered by Agent
- future hardcoded route retirement proof helpers discovered by Agent
- future credential management mode/category mapping functions discovered by Agent
- future scheduler create/edit/disable action wiring functions discovered by Agent

### 受入条件

- `.agent/tasks/todo.md` のみを変更する TODO 再編 scope として記録されている。
- seed関連は統合 Bundle `admin-surface-topology-seed-conversion` へ集約されている。
- 統合 Bundle 内に `admin-dashboard` / `team-dashboard` / `credential-management` / `admin-enum` / `scheduler-settings` が意味要素 subBundle として明示されている。
- 各 subBundle は route 名ではなく意味要素で scope を持つ。
- 共通工程が hardcoded surface読込 → React-like Schema作成 → translator tool変換 → topology UI seed生成 → seed登録 → projection render / backend action wiring確認 → 必要なproof更新 → 最後にhardcoded route/island/old route-presence test削除、の順序である。
- `/admin/users` は `credential-management` subBundle に含まれている。
- `credential-management` は user_auth / external / instance_settings の mode/category 統合方針に従う。
- `admin-enum` は enum dictionary / enum group / enum item / status enum dependency を扱う。
- `team-dashboard` は saved markdown / team projection view を扱う。
- `admin-dashboard` は admin landing / navigation / guide entry を扱う。
- `scheduler-settings` は scheduler job settings projection / create-edit-disable action wiring を扱う。

### Governance NG boundary

- route別に `/admin/enums` / `/admin/users` / `/admin/scheduler` / `/admin/team-dashboard` を別 Bundle 化する。
- seed conversion と proof orchestration を同一実装 Bundle として混ぜる。
- proof bundle を実装 scope として扱う。
- route 削除を先行する。
- route absence を proof とする。
- hardcoded route presence test を canonical proof として残す。
- React-like Schema candidate / generated topology seed candidate を active topology authority として扱う。
- translator に db seed SQL / runtime DB state / frontend runtime state を source authority として読ませる。
- credential management から admin user を分離する。
- credential management に standalone route / dedicated panel / raw physical table row editor を追加する。
- 語彙修正を seed conversion 実装前に混ぜる。
- 実装既存状態を SSOT として扱う。
- PR574 / PR577 / PR578 の idempotency 系実装を未実装扱いに戻す。
- proof 更新を idempotency authority 再設計・再実装として読める状態にする。
- **empty/fake topology manifest（ui_projection を持たない manifest/hub）を hub_relations 接続 source を作る目的だけで新規作成する（owner 明示禁止, PR #584 review comment）。**

### 現在の状態（正本、2026-07-12b 時点）

以下がこの Bundle の現在の状態の正本である。下の各「記録」節（2026-07-11 / 2026-07-12 / 2026-07-12b）は対応履歴（監査証跡）として保持するが、判断の正本ではない。特に 2026-07-11 節は判断の前提が誤っていたため **INVALIDATED** としている（詳細は当該節を参照）。

- Bundle status: **`not_started`**（未実装）。
- credential-management subBundle には現在 **一切の専用 route/island が存在しない**。既存の `/admin/users`（auth_users CRUD、未変更）と manifest 092 の既存 `?manifest=`/`canonical_default_entry` アクセス（user_auth/external/instance_settings、未変更）のみが到達経路。
- admin hub relation navigation: source は `/admin` 自身である必要はなく、既存の `/admin/manifests` authoring surface（`ManifestsAdmin.tsx` + `HubNavigationAdmin.tsx`、`hub_navigation:*` dispatch action、すべて既存実装・production dispatcher_mapping 済み）から任意の既存 manifest を source として選択できる。
  - credential-management: ターゲット側（manifest 092）に blocker はない。実際に relation が authoring されているかどうかは runtime data であり、この todo は追跡しない。
  - enum_dictionary / team_dashboard / scheduler_settings: ターゲット側の per-screen `ui_projection` manifest が存在しないことのみが blocker。
- **topology UI seed production は owner 指示により停止中。再開には明示的な今後の owner 指示が必要。**
- fake manifest（`ad100`/`ad101`/`ad102`）撤回、`/admin/credentials`・`AdminCredentialsShell` 撤去、`auth_users:*`/`team_markdown:*` dispatcher_mapping 追加、generic `LayoutSchemaTensorComposer` → `LayoutNode[]` fixture proof、`hub_navigation:create` end-to-end proof はすべて維持済み。


### PR587後 現在の状態（正本、2026-07-14）

以下がPR587 merge後の現在判断正本である。2026-07-12bまでの各記録は履歴証跡として保持するが、現在判断はこの節と上記scope/NG boundaryを優先する。PR587（`c2c87cc Add admin/normal surface projection seed SSOT and proof (#587)`）は `docs/design/admin-normal-surface-projection-seed-ssot.yaml` と `.agent/tests/check-admin-normal-surface-projection-seed-ssot.sh` による **design SSOT / proof completion evidence** であり、topology UI seed production / DB seed登録 / runtime/frontend実装 / route retirement の完了ではない。Bundle status は **`not_started`** のまま維持する。

- Foundation read gate / target SSOT connection: `docs/framework-core.yaml`, `docs/framework-policy.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml`, `docs/design/db-schema.yaml` を前提に、PR587 SSOT の `foundation_inputs` / `authority.references` / `design_blocking` / `seed_implementation_start_conditions` を本Bundleへ接続する。後続seed実装は同SSOTの全 authority references を再読し、design blocker全件を解決してからでなければ開始しない。
- Component catalog authority: `docs/design/component-catalog-classification-ssot.yaml` と `docs/design/ui-ux-primitive-catalog-ssot.yaml` に固定する。`frontend/components/catalog.ts` は `md_translation_authoring_surface.authoring` が `runtimeConnected=false` である等の physical evidence に限り、設計正本へ昇格させない。
- CRUD preset authority boundary: `db/physical_search_crud_aggregate_preset_seed.sql` は generic composition / wiring shape の physical seed reference に限る。`content_bundle:*` operation refs、UUID、row identity、layout座標、label、current seed row status は対象surface authorityへコピーしない。
- Normal dashboard responsibility split: viewer は normal read projection。inputer mutation は `operation_bindings[*].capability_requirement.required_role=admin` を持つ。viewer read/search/filter は explicit / inferred admin requirement を持たず、`required_role: none` 等の sentinel role を再導入しない。
- Credentials/users identity split: `credentials.users` と `users(status)` は同一 `auth.users` を対象にする別projectionとして維持する。`credentials.users` は `auth.users` + `auth.credentials` の composite account lifecycle projection、`users(status)` は `auth.users` の status-only projection。
- Admin dashboard boundary: `admin-dashboard` は business projection や fake hub/manifest ではなく、admin landing / navigation / guide entry 責務として維持する。hub relation source確保目的の empty/fake topology manifest は追加しない。
- Hub relation authority: `/admin/manifests` による既存 hub relation authoring authority、`hub_navigation:*` dispatcher mapping、`ManifestDispatcher` projection / relation resolution を維持する。hub relation resolution の再設計、role gate再発明、runtime DB接続状態のTODO固定台帳化はしない。
- Topology UI seed production: owner明示指示まで停止中。仮にdesign blockerが全件解消済みと判断されても、seed production再開は別途owner明示判断が必要。

#### PR587 design_blocking 再監査

| design_blocking id | 分類 | 根拠 / 残scope |
|---|---|---|
| `target_surface_manifest_readiness` | credential-management active target manifest: `resolved_existing_substrate`; credential-management explicit navigation binding authoring / verification: `unresolved_before_seed`; admin-enum / team-dashboard / scheduler-settings target manifest + later binding: `unresolved_before_seed`; admin-dashboard: `subBundle_not_applicable` | Canonical hub relation authoring/resolution substrateは既存 `/admin/manifests` + `HubNavigationAdmin.tsx` + `hub_navigation:*` dispatch + `hubs.hub_relations` + `ManifestDispatcher` で成立済み。credential-managementはmanifest 092の active `ui_projection` target manifest readiness はresolved。ただし live `hubs.hub_relations` row状態はTODO固定台帳にせず、seed開始前に明示的なnavigation binding authoring / verificationを行う残scopeは落とさない。admin-enum / team-dashboard / scheduler-settings は各画面固有 `ui_projection` manifest 生成後、同じ canonical authorityで明示binding authoring / verificationが必要。admin-dashboardはlanding / navigation / guide entry責務であり、business target surface manifestやfake source manifestを作る対象ではない。 |
| `external_instance_projection_columns` | physical schema / approved candidate source: `resolved_existing_substrate` for credential-management instance_settings; projection seed binding / authoring surface / operation wiring / proof: `unresolved_before_seed`; other subBundles: `subBundle_not_applicable` | 既存substrateとして `topology.db_instance_port` / `topology.runtime_instance_port` / `topology.instance_connection_policy` / `topology.instance_operation_authority_binding`、`NpgsqlInstancePortPolicyRepository`、および `instance_operation_authority_binding` を admin-approved candidate source とする既存契約がある。新table/column/JSON key/candidate sourceは設計しない。残scopeは既存column / approved candidate sourceからprojection seedへのbinding、既存credential-management projection extension上のauthoring surface、operation wiring、secret-deny proofに限定する。 |
| `normal_dashboard_authoring_runtime_adapter` | `owner_decision_required` for team-dashboard normal.dashboard; `subBundle_not_applicable` for admin-dashboard / credential-management / admin-enum / scheduler-settings | `frontend/components/catalog.ts` physical evidence上、`md_translation_authoring_surface.authoring` は `runtimeConnected=false`。PR587 SSOTはprops `[onSaved, onCancel, placement]` と admin-gated preview/validate/write/diff emissionのadapter/route-composition binding選定をseed前必須とする。既存route compositionを使うかruntime adapterを設計するかはowner decisionであり、Agentが推測で新設しない。 |
| `normal_dashboard_write_operation_binding` | `resolved_existing_substrate` for backend operation substrate; `unresolved_before_seed` for seed binding selection; `subBundle_not_applicable` for admin-dashboard / credential-management / admin-enum / scheduler-settings | `team_markdown:*` dispatcher_mapping / AdminRuntime / repository substrateはPR584 remediationで維持済みで、team Markdown SSOTがpersistence authorityを持つ。一方、PR587 SSOTはpreview/validate/write/diffのseed側 operation binding key選定を後続seed作業で行う必要があるため、seed generation前に既存team Markdown runtime actionへ明示bindし、conflict時はdesign conflictとして止める。 |
| `credentials_users_account_transaction_binding` | create account + initial credential: `resolved_existing_substrate`; delete account + credential consistency: `resolved_existing_substrate`; password replace / rotate: `unresolved_before_seed` / `owner_decision_required`; other subBundles: `subBundle_not_applicable` | `docs/design/admin-master-roster-management-ssot.yaml` の現行 action は `auth_users:list/search/get/create/update/delete` で、`backend/schema/AuthMasterContracts.cs` のDTOも create は password を持つが update は metadata/status系のみで password replace / rotate DTOを持たない。したがって create account + initial credential と delete account + credential consistency は既存 `auth_users:*` / `auth.credentials` substrateありとして扱う一方、password replace / rotate は seed operation key選定だけでは解消できない。canonical operation authority、backend transaction contract、secret non-projection boundary、`auth.users` / `auth.credentials` consistency proof、seed operation bindingのdesign resolutionがseed前に必要。`auth_users:update` をpassword replace / rotate代替として扱わず、存在しない `auth_users:replace_password` / `auth_users:rotate_password` も実装済みauthorityとして記録しない。 |

#### 5 subBundle別 残scope分類

- `admin-dashboard`: landing / navigation / guide entry のみ。business projection化、fake hub/manifest作成、`/admin` 自身をhub relation source必須とする設計はNG。残scopeはseed production再開後のhardcoded surface読込・React-like Schema化・translator変換・seed登録写像・canonical admin mechanismでのrender/navigation確認・proof更新・最後のhardcoded route/island/old route-presence test撤去。
- `credential-management`: manifest 092 / existing `?manifest=` / canonical_default_entry / `/admin/users` auth_users CRUD は既存到達経路として扱う。active `ui_projection` target manifest readinessは既存substrate resolved。ただし explicit navigation binding authoring / verification はseed前残scopeであり、live `hubs.hub_relations` row状態はTODO固定台帳にしない。instance_settingsは既存 `topology.db_instance_port` / `topology.runtime_instance_port` / `topology.instance_connection_policy` / `topology.instance_operation_authority_binding` と `NpgsqlInstancePortPolicyRepository` / approved `instance_operation_authority_binding` candidate sourceからprojection seedへbindする残scope。`credentials.users` は create account + initial credential / delete account + credential consistency は既存substrateあり、password replace / rotate は canonical operation authority / backend transaction contract / secret non-projection boundary / consistency proof / seed bindingが未解決。admin user分離、standalone route/dedicated panel/raw table editorはNG。
- `admin-enum`: enum dictionary/group/item/status dependency authorityは既存SSOTと `enum_dictionary:*` substrateに従う。target `ui_projection` manifest は未作成で `unresolved_before_seed`。target manifest生成後は `/admin/manifests` / `hub_navigation:*` canonical authorityで明示binding authoring / verificationを行う。CRUD presetのgeneric shapeは参考にできるが `content_bundle:*` refsをコピーせず、enum authority operationへbindする。
- `team-dashboard`: team Markdown saved view / rendered Markdown / completed preset seed summary authorityは既存SSOTに従う。target `ui_projection` manifest は未作成。target manifest生成後は明示binding authoring / verificationを行う。normal.dashboard viewer/inputer責務分離、`md_translation_authoring_surface.authoring` runtime adapter/route-composition owner decision、preview/validate/write/diff operation bindingがseed前残scope。
- `scheduler-settings`: scheduler job manifest / create-edit-disable authorityは既存SSOTと `scheduler_jobs:*` substrateに従う。target `ui_projection` manifest は未作成で `unresolved_before_seed`。target manifest生成後は明示binding authoring / verificationを行う。scheduler runtime policyをfrontend constantsへ隠さず、既存backend/dispatcher substrateへ明示bindする。

### [INVALIDATED — DO_NOT_USE — superseded_by_2026_07_12b] Owner補正記録（PR #584 review comment, 2026-07-11, topology UI seed production 停止）

**INVALIDATED: この節の「現在の状態」の判断（admin hub relation navigation が4 subBundle すべて unconnected というくだり）は、「hub relation の source は `/admin` 自身でなければならない」という誤った前提に基づいており、2026-07-12b 節で訂正済み。正本は上の「現在の状態（正本）」を参照すること。** 以下は対応履歴としてのみ保持する。

**この節は Bundle の Status を `not_started` から変更しない。** PR #584 で行った作業は topology UI seed conversion の完了ではなく、Agent による暫定実装（provisional implementation）として以下に記録する。

- **問題点:** PR #584 で `/admin/credentials` route・`AdminCredentialsShell` island 等の hardcoded route/island 追加、および auth_users/team_markdown 向け dispatcher_mapping 追加を、この Bundle の「共通工程」（React-like Schema 作成 → translator 変換 → topology UI seed 生成 → seed 登録 → projection render 確認）を経由せずに行った。さらに `/admin` に outbound hub relation を持たせるためだけに、ui_projection を持たない fake manifest/hub（`00000000-0000-0000-0000-0000000ad100` / `ad101` とその `hubs.hub_relations` row `ad102`）を db/seed_empty.sql に追加していた。owner から PR #584 review comment で、後者が「hub relation 接続のためだけの空/fake topology manifest」パターンに該当し明示的に禁止対象であるとの指摘を受けた。
- **目的:** topology UI seed production を明示的な owner 指示があるまで停止し、PR #584 で追加した hardcoded route/island は削除せず Agent の暫定実装として明記した上で保持し、fabricated hub relation construct のみを撤回する。
- **改善方針・対応内容:**
  - `db/seed_empty.sql` から admin landing hub-manifest ブロック（`ad100`/`ad101`/`ad102`）を削除し、理由を説明するコメントに置き換えた。
  - `backend/tests/Topolactor.Integration.Tests/CredentialManagementHubRelationUiProjectionLiveDbTests.cs` から対応する `AdminLandingHub_HubRelation_ResolvesToCredentialManagementManifest092_SeedOnly` テストを削除し、説明コメントに置き換えた。
  - `docs/design/admin-console-workflow-ssot.yaml` の `admin_hub_relation_navigation_contract` を修正し、`admin_landing_hub_manifest` サブブロックを削除、`wired_relations.credentials` を `status: wired` から `status: unconnected_no_legitimate_source` に訂正し、`/admin` は既存の hub/manifest を持たないため現時点で hub relation source になり得ないことを明記した。`page_responsibility.admin_index` の記述も同様に訂正した。
  - `docs/design/runtime-orchestration-ssot.yaml` の `admin_route_retirement_matrix` は全 route が既に `status: pending` かつ正しい precondition を持っており、fabricated relation の成功を前提とする記述はなかったため変更不要と確認した。
  - `enum-dictionary-ssot.yaml` / `team-markdown-dashboard-saved-view-ssot.yaml` / `scheduler-job-manifest-ssot.yaml` の `admin_hub_relation_navigation` は元々 `status: blocked_pending_seed_catalog`（target 側 manifest 不在が理由）であり、fabrication を主張していなかったため変更不要と確認した。
  - `frontend/tests/layoutSchemaStructuralRender.test.ts`（PR #583 由来の共通 test 基盤）から、manifest 092 固有の UUID literal（`layoutId`/`packageId` の `...cd002`/`...cd005`）と credential 画面固有の node 名（`instance_settings`/`instance_address_form`/`instance_authority_key`/`validate`/`json_template_download` 等）を汎用プレースホルダー（`sample_category`/`sample_form`/`sample_field`/`sample_action` 等、汎用 UUID `...000101`/`...000102`）に置き換えた。manifest 092 の実 fixture を使う代表 regression proof（1番目・最後のテスト）はそのまま維持した。9 tests すべて pass 確認済み。
  - `auth_users:*` / `team_markdown:*` の dispatcher_mapping 追加（db/seed_empty.sql）は `AdminRuntime.ExecuteDataAsync` の switch 実装および canonical bootstrap seed と照合済みで、この停止指示の対象外（本 Bundle の実装ではなく、既存 backend 実装に対する欠落 wiring の是正）として維持する。
- **現在の状態（INVALIDATED — 使用しないこと。正本は本 Bundle 冒頭の「現在の状態（正本）」節を参照）:**
  - ~~admin hub relation navigation は 4 subBundle（credential-management / admin-enum / team-dashboard / scheduler-settings）すべてで unconnected（未接続、fabrication なし）~~ — 誤り。「source は `/admin` 自身でなければならない」という前提が誤りだった（2026-07-12b 節参照）。
  - topology UI seed production は owner 指示により停止中という点のみ、現在も正しい。

### Gate0 remediation記録（PR #584 review comment, 2026-07-12）

**この節は Bundle の Status を `not_started` から変更しない。** 2026-07-11 の Owner補正記録で「削除せず保持する」としていた `/admin/credentials` route・`AdminCredentialsShell` island は、この Gate0 remediation により **完全に削除された**（暫定実装としての保持ではなく、撤去）。上記 2026-07-11 節の「現在の状態」の一部はこの節により訂正される。

- **問題点:** CI は green だったが、Gate0 監査により、この Bundle 自身の共通工程（React-like Schema → translator → topology UI seed → seed 登録 → projection render 確認）を経由せずに追加した hardcoded route/Island（`/admin/credentials`・`AdminCredentialsShell`）を「暫定実装」として保持し続けることが blocking と判定された。加えて、それを canonical projection entry・manifest 092 の4カテゴリ・`/admin/users` retiring 予定として記述する SSOT 文言、および PR #583 由来の共通 test 基盤（`layoutSchemaStructuralRender.test.ts`）が seed / `LayoutSchemaTensorComposer` / `Emission.LayoutNodes` の入力境界を実際には経由せず、hand-authored `LayoutNode[]` literal のみを入力にしていた点も blocking と判定された。
- **目的:** fake manifest 撤回・dispatcher_mapping 追加・proof drift 修正・todo.md owner-pause 記録は維持したまま、architecture substrate 不整合（hardcoded UI・未実装前提の SSOT 記述・非 seed-grounded 共通 test）のみを是正する。
- **改善方針・対応内容:**
  - `frontend/routes/admin/credentials/index.tsx` と `frontend/islands/AdminCredentialsShell.tsx` を完全に削除（`git rm`。暫定実装マーカーとしての保持ではない）。`frontend/fresh.gen.ts` を再生成しルート登録を除去。
  - `frontend/content/adminGuides.ts` から `/admin/credentials` カードと `/admin/users` の「統合準備中」caution を削除し、元の状態に復元。
  - `frontend/tests/projectionEntry.test.ts` の credential route test を「credential 名を含む route ディレクトリが0件であること」を検証する元の形に復元。`frontend/tests/adminMainFlow.test.ts` の `ADMIN_ROUTE_CARDS` リストから `/admin/credentials` を削除。
  - `docs/design/admin-console-workflow-ssot.yaml` から `/admin/credentials` の `canonical_routes` エントリ、`master_roster_routes` の `/admin/credentials` ブロック（`status: canonical_projection_entry`、manifest 092 の4カテゴリ記述）、`projection_entry_vs_data_authority_split` サブセクション、`page_responsibility.admin_credentials` ブロックを削除。`/admin/users` を `status: retiring_pending_proof` から通常の canonical route に復元。
  - `docs/design/admin-master-roster-management-ssot.yaml`・`docs/design/instance-port-substrate-ssot.yaml`・`docs/projection_design/credential-management-projection-design.md` から同様に `/admin/credentials` 前提の記述（`retiring_pending_proof`・`canonical_url_amendment`・admin_user 4カテゴリ目・「`/admin/users` は retiring」）を削除し、元の3カテゴリ・「この bundle は `/admin/users` に触れない」という記述へ復元。
  - `docs/design/runtime-orchestration-ssot.yaml` の `frontend_routes.admin` から `/admin/credentials` を削除し、`admin_route_retirement_matrix.routes` から `/admin/users → /admin/credentials` の redirect row を削除。`/admin/enums`・`/admin/team-dashboard`・`/admin/scheduler` の row は `/admin/credentials` に依存しない独立した記述であることを確認し維持。`/admin/scheduler` の registry ratification（route file・dispatcher_mapping は既存、registry entry のみ欠落していた是正）も独立して正当であることを再確認し維持。
  - `frontend/tests/layoutSchemaStructuralRender.test.ts` の共通/generic test 6件を、hand-typed `LayoutNode[]` literal から、実際の `LayoutSchemaTensorComposer` 出力に基づく checked-in fixture（`frontend/tests/fixtures/layout_schema_composed_scenarios/`）読み込みへ差し替えた。これらの fixture は `backend/tests/Topolactor.Runtime.Tests/LayoutSchemaStructuralCompositionTests.cs` に追加した新規テスト（`ComposeAndMapToLayoutNode_*_MatchesCheckedInFrontendFixture`、6件）が、seed 形状の `records[]` literal JSON（db/seed_empty.sql への追加ではなく、同ファイル既存の non-seed literal-JSON test パターンを再利用）を実際に `LayoutSchemaTensorComposer.Compose()` + `StructureMapResolver.ToLayoutNode()` に通した結果と byte-exact 一致することを証明している（manifest 092 の実 fixture と同じ「checked-in fixture + byte-exact companion backend proof」の規律）。manifest 092 の実 fixture を使う代表 regression proof（先頭・末尾の2テスト）は変更なし。
  - `.agent/reports/admin-surface-topology-seed-conversion-design-resolution.json` を現在状態へ正規化: `phase` を訂正、`report_meta.gate0_remediation_2026_07_12` を追加、撤回前の `phase2_verification_performed`/`phase2_ci_failure_fix` を `superseded_history` へ隔離、`implementation_changes`（`/admin/credentials` 関連）を空にし旧内容を `implementation_changes_SUPERSEDED_2026_07_12` へ隔離、`route_retirement_matrix` から `/admin/users` row を削除、関連 `issues[]`（issue-04/06/12/14/19/36）・`subbundle_states`（credential-management）・`remaining_gap`（gap-10 追加）・`final_proof_result`・`handoff_to_reviewer` を訂正。
  - `auth_users:*`/`team_markdown:*` dispatcher_mapping、fake manifest 撤回、proof drift 修正（`adminDispatchManifestSeed.test.ts`・`adminMainFlow.test.ts` の SSOT 読み込み修正）は変更なく維持。
- **現在の状態（重要）:**
  - この Bundle は依然 **`not_started`**（未実装）である。credential-management subBundle には現在 **一切の専用 route/island が存在しない**（2026-07-11 時点の「暫定実装として保持」からさらに後退し、完全撤去された）。既存の `/admin/users`（auth_users CRUD、未変更）と manifest 092 の既存 `?manifest=`/`canonical_default_entry` アクセス（user_auth/external/instance_settings、未変更）のみが到達経路である。
  - topology UI seed production は引き続き owner 指示により停止中。再開には明示的な今後の owner 指示が必要。将来 `/admin/credentials` を実装する場合は、この Bundle 自身の共通工程（React-like Schema → translator → topology UI seed → seed 登録 → projection render/action wiring 確認）を経由しなければならない。

### Hub relation語彙訂正記録（PR #584 review comment, 2026-07-12b）

**この節は Bundle の Status を `not_started` から変更しない。** 2026-07-11 節の「admin hub relation navigation は4 subBundle すべて unconnected（`/admin` に既存 manifest がないため）」という記述は、前提自体が誤りだったと訂正する。

- **問題点:** 「hub relation の source は `/admin` 自身でなければならない」という前提が誤りだった。実際には `/admin/manifests`（`frontend/routes/admin/manifests.tsx` → `ManifestsAdmin.tsx` island が既存の `hubs.topology_manifests` 一覧を表示し、`HubNavigationAdmin.tsx` island が選択された任意の既存 manifest に対して `hub_navigation:create`/`update`/`deprecate`/`reorder` を実行する）という、この Bundle が作るのではなく既に実装済みの authoring surface が存在し、`hub_navigation:*` の6軸すべてが `db/seed_empty.sql` に production dispatcher_mapping 済みである。「`/admin` 自身に manifest がないので hub relation source を作れない」という 2026-07-11 節の判断は、この既存 authoring surface を見落としたまま「/admin 自身が source であるべき」という誤った前提で導かれたものであり、fake manifest 撤回の判断自体（ad100/ad101/ad102 の撤去）は正しいが、その後の「4 subBundle すべて unconnected（fabrication なし）」という記述は、SSOT に runtime DB の接続状態（`wired`/`unconnected`）を固定語彙として複製していた点も含めて不正確だった。
- **目的:** hub relation authoring・DB保存・`ManifestDispatcher` projection を既存実装として扱い、SSOT から「fake `/admin` source 不存在」を未達理由として除去し、SSOT が runtime DB 接続状態を固定台帳として複製しないようにする。
- **改善方針・対応内容:**
  - `docs/design/admin-console-workflow-ssot.yaml` の `admin_hub_relation_navigation_contract` を修正: `wired_relations`（4 subBundle 分の `status: unconnected_no_legitimate_source` 固定台帳）を削除し、代わりに `authoring`（`/admin/manifests` 経由の既存 authoring surface・6つの `hub_navigation:*` dispatch action・repository method・end-to-end proof への参照）、`connection_state_authority`（接続状態は runtime/admin data であり SSOT が複製しないことの明記）、`subbundle_target_readiness`（credential-management はターゲット側 blocker なし、enum/team-dashboard/scheduler-settings はターゲット側 `ui_projection` manifest 不在のみが blocker）を追加。`prohibited` に `duplicating_live_hubs_hub_relations_connection_state_as_a_fixed_SSOT_status_ledger` と `requiring_admin_own_landing_page_to_be_a_hub_relation_source` を追加。`page_responsibility.admin_index` の記述も同様に訂正。
  - `docs/design/runtime-orchestration-ssot.yaml` の `admin_route_retirement_matrix.retirement_kind.hub_navigation_only` の定義を「`/admin` からの hub relation navigation」固定表現から、「`/admin/manifests` で authoring された、manifest-scoped outbound navigation」という正確な表現へ訂正。
  - `docs/design/enum-dictionary-ssot.yaml`・`docs/design/team-markdown-dashboard-saved-view-ssot.yaml`・`docs/design/scheduler-job-manifest-ssot.yaml` の `admin_hub_relation_navigation` ノートから「`/admin` からの hub relation navigation」固定表現を除去し、「authoring 自体は既存 `/admin/manifests` 経由で可能であり、blocker はターゲット側 `ui_projection` manifest 不在のみ」という正確な表現へ訂正。
  - `db/seed_empty.sql` の fake manifest 撤回コメントを訂正: 「`/admin` に既存 manifest がないため撤回」という説明から、「`/admin/manifests` 経由で任意の既存 manifest を source にできるため、そもそも `/admin` 専用の manifest は不要だった」という正確な説明へ書き換え。具体的な relation row は本 seed ファイルへ追加しない（authoring は runtime/admin action であり seed content ではない）。
  - `backend/tests/Topolactor.Integration.Tests/CredentialManagementHubRelationUiProjectionLiveDbTests.cs` に新規テスト `DispatchAsync_HubNavigationCreate_RealAuthoringPath_SourceManifestDispatchReflectsRelationInNavigationSequence_AndFailClosesOnZeroActiveTarget` を追加: 実 `ManifestDispatcher` 経由で `hub_navigation:create` を dispatch → 実 `NpgsqlContentBundleRepository` で永続化 → readback → source manifest 再 dispatch で `Emission.NavigationSequence` に反映されることを確認 → target manifest を deprecated にして zero-active-target の fail-close（`TargetManifestId: null`）を確認、という一連を1シナリオで証明。既存の direct-SQL-insert テスト（`DispatchAsync_CredentialManagementManifest_E2E_RelationVectorToScalarEmission`）は read/NavigationSequence 側の regression proof として維持するが、authoring completion proof の代替とはしない。
  - fake manifest 撤回・`/admin/credentials` 撤去・`auth_users:*`/`team_markdown:*` dispatcher mapping・generic `LayoutSchemaTensorComposer` → `LayoutNode[]` fixture proof・topology UI seed production 停止は変更なく維持。
- **現在の状態（重要）:**
  - credential-management: manifest 092 は既に `ui_projection` を持つ実在の manifest であり、hub relation の **ターゲット側に blocker はない**。`/admin/manifests` 経由で任意の既存 manifest から credential-management のハブへ実際に relation を張ることは、既存の authoring 機構だけで今すぐ可能である（ただし本 remediation はそれを実行しない — 「具体的な relation row を追加しない」という owner 指示のスコープ外）。現時点でそのような relation が実際に張られているかどうかは runtime data であり、SSOT はそれを記録しない。
  - enum_dictionary / team_dashboard / scheduler_settings: **ターゲット側**（各画面固有の `ui_projection` manifest）が存在しないことのみが blocker。authoring 機構自体の gap ではない。topology UI seed production が owner 指示により再開されない限り解消しない。
  - topology UI seed production は引き続き owner 指示により停止中。再開には明示的な今後の owner 指示が必要。

### 旧 `.agent/reports/admin-surface-topology-seed-conversion-design-resolution.json` からの移管内容（削除前、2026-07-12b）

owner 指示により、一時監査 report である上記 JSON ファイルは本節への必要内容の移管後、PR closure 前に削除した。以下は削除された report の追跡先として todo へ移管した内容であり、**SSOT authority ではない**（`gap-01` などの ID は削除済み report 内での参照 ID）。

- **response-binding architecture 未実装**（旧 gap-01）: `dispatchExternalPort`/`dispatchInstanceOperation` の runtimeInteractions レーンに対する response-binding / invalidation アーキテクチャが未実装。`admin_runtime`（auth_users/team_markdown/scheduler_jobs/enum_dictionary）CRUD を真に seed-backed category として authoring するには、`AdminRuntime` の `layer:action` axis を直接 dispatch できる新しい runtimeInteractions actionType も必要。cross-cutting・high-blast-radius につき、owner_decision_required のアーキテクチャ選択が前提。
- **declared_seed_surface_catalog 未整備**（旧 gap-02）: admin-dashboard / team-dashboard / admin-enum / scheduler-settings 向けの catalog entry が未追加。各 surface ごとに React-like Schema 作成 + translator 実行が必要。
- **hub relation ターゲット不在**（旧 gap-03、上の「Hub relation語彙訂正記録」で正確な理由に訂正済み）: enum_dictionary/team_dashboard/scheduler_jobs 向けの `ui_projection` manifest が未作成。
- **`scheduler_jobs:edit` の UI 未実装**（旧 gap-04）: backend/dispatcher は完全に wiring 済みだが、`frontend/routes/admin/scheduler.tsx` に UI control がない。
- **instance_settings placeholder targetRef 未解決**（旧 gap-05）: manifest 092 の `instance_settings` category にある seeded placeholder `instanceTargetRef` が実 UUID に未解決。`InstancePortDispatchRuntime` に明示的な fail-close guard もない。実在する登録済み instance-port record が存在しないため（`instance_settings_admin_authoring_ui_pending` は明示的にこの Bundle の scope 外）。
- **root `/` の非 admin ユーザー向け fail-close 未検証**（旧 gap-06）: root `/` の `canonical_default_entry` は認証済みセッションであれば誰でも admin-only な manifest 092 へ解決される。owner_decision_required。
- **manifest clone-authoring / admin_csv_json_import ファミリーの dispatcher_mapping gap**（旧 gap-07）: `admin_csv_json_import:list_snapshot_records`、`manifest:create_clone_new_topology_draft_from_active`、`manifest:create_clone_replacement_draft_from_active`、`manifest:create_new_topology_draft`、`manifest:list_aggregate_trigger_processing_functions`、`manifest:list_screen_read_query_wiring`、`manifest:load_clone_source_evidence`、`manifest:merge_clone_replacement_draft_to_active`、`manifest:validate_clone_replacement_draft`、`physical_record:list_history` の約10件。この Bundle の5 subBundle scope 外（別の admin-authoring pipeline）だが、発見時に記録。
- **roadmap/todo drift**（旧 gap-08）: `docs/system-roadmap.yaml` 側の team_markdown roadmap drift は実質解消済み（`team_markdown:*` dispatcher_mapping closure により）だが、roadmap 側の記述自体は未更新。file-path drift も残る。
- **将来候補 Bundle**（旧 future_bundle_candidates）:
  - `admin-runtime-dispatch-response-binding`: response-binding/invalidation アーキテクチャの設計・実装（上記 response-binding gap を解消）。
  - `admin-surface-seed-catalog-conversion`: admin-dashboard / admin-enum / team-dashboard / scheduler-settings 向けの React-like Schema 作成・translator 実行・topology UI seed 登録。
  - `instance-settings-admin-authoring-ui`: `docs/system-roadmap.yaml` の `instance_settings_admin_authoring_ui_pending` として既に追跡済み。JSON template download/import/validate/preview/apply/approve の UI・backend action。
  - `presentation-participant-audience-authority`: 必要になった場合のみ。presenter-to-participant forced projection、participant membership、targeted per-viewer SSE delivery。現状すべての SSOT に不在確認済み。専用 SSOT authority が必要。

---

## Bundle `test-orchestration-review`

**Status:** `not_started`
**Primary SSOT:** `docs/design/pipeline-continuity-ssot.yaml`
**Position:** `admin-surface-topology-seed-conversion` 完了後の後段 review。実装 Bundle ではなく、動作証明 / test orchestration 点検 scope。

### 問題点

旧 `pipeline-continuity-frontend-route-seed-proof` は seed conversion と proof orchestration を同じ実装 Bundle に混ぜる危険があった。route absence や old route-presence test を proof とすると、seed CRUD renderability / projection render / backend action wiring の証明が欠落する。

### 目的

seed conversion 完了後に test tier / scenario harness / route-presence-test replacement を点検し、route registry proof、seed CRUD renderability proof、route removal replacement proof、label boundary proof、admin Step wording proof の位置づけを整理する。

### 改善方針

- `admin-surface-topology-seed-conversion` 完了後にのみ開始する。
- proof orchestration review は実装完了判定ではなく、pipeline-continuity SSOT に基づく test tier / scenario harness の点検として扱う。
- old route-presence tests は canonical proof として残さず、seed/render/action wiring proof へ置換する。
- route absence 単独を proof としない。
- 必要に応じて frontend route registry / seed renderability / backend action wiring / admin wording / label boundary の proof surface を再分類する。

### 対応資料

- `docs/design/pipeline-continuity-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`
- `docs/design/react-schema-topology-seed-translator-ssot.yaml`
- `.agent/tasks/todo.md`

### 対象ファイル名

- `.agent/tasks/todo.md`
- `frontend/tests/adminUxGuard.test.ts`
- `frontend/tests/adminMainFlow.test.ts`
- `frontend/tests/visualLayoutBuilder.test.ts`
- `frontend/tests/uiBuilderPackageWiring.test.ts`
- `frontend/tests/runtimeUiInteractionScenario.test.ts`
- `frontend/tests/adminWiringExecutionLane.test.ts`
- `frontend/tests/uiBuilderWiringProjection.test.ts`
- future route registry / seed renderability / route-presence-test replacement proof files

### 対象関数名

- future canonical route registry proof helpers
- future non-canonical route absence assertions
- future seed CRUD renderability assertions
- future projection render proof helpers
- future backend action wiring proof helpers
- future label boundary assertions
- future admin Step wording assertions

### 受入条件

- seed conversion 実装後の後段 scope として扱われている。
- test tier / scenario harness / route-presence-test replacement の点検 scope として記述されている。
- proof bundle を seed conversion 実装 Bundle として扱わない。
- route absence 単独や hardcoded route presence test を canonical proof として残さない。

---

## Bundle `frontend-canonical-surface-structure-label-boundary`

**Status:** `not_started`
**Primary SSOT:** canonical surface UI structure/wiring SSOTs, including `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`
**Position:** `admin-surface-topology-seed-conversion` 完了後の後段語彙修正 scope。

### 問題点

normal view label boundary と technical disclosure boundary を seed conversion 実装前に混ぜると、seed conversion の route retirement / projection render wiring と語彙修正が同時変更になり、証明範囲が不明確になる。

### 目的

seed conversion 完了後に、canonical surfaces ごとの UI structure/wiring と表示 label boundary を owning SSOT へ戻す。

### 改善方針

- seed conversion 実装には混ぜず、後段 scope として実施する。
- normal view label boundary では raw id / UUID / topology / manifest / screen_data_shape / DB / backend / Route / Primary Table / UI Builder Key 等を通常表示の意味にしない。
- technical details は explicit technical disclosure として扱う。
- operator visible labels は raw-first にしない。

### 対応資料

- canonical surface UI structure/wiring SSOTs
- `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- `.agent/tasks/todo.md`

### 対象ファイル名

- `.agent/tasks/todo.md`
- `frontend/routes/index.tsx`
- `frontend/routes/auth.tsx`
- `frontend/routes/super_auth.tsx`
- `frontend/routes/admin/index.tsx`
- `frontend/routes/admin/contents.tsx`
- `frontend/routes/admin/ui-builder.tsx`
- `frontend/routes/admin/manifests.tsx`
- frontend components / islands that render canonical surface labels and technical disclosure

### 対象関数名

- future normal label mapping functions
- future technical disclosure rendering functions
- future operator label mapping functions
- future canonical surface view model builders

### 受入条件

- 語彙修正は seed conversion 実装後の後段 scope として扱われている。
- normal user-facing views do not expose raw ids / UUIDs / internal vocabulary as primary meaning.
- technical details, if needed, are behind explicit technical disclosure and not normal operation labels.

---

## Bundle `admin-console-workflow-step-wording-boundary`

**Status:** `not_started`
**Primary SSOT:** `docs/design/admin-console-workflow-ssot.yaml`
**Position:** `admin-surface-topology-seed-conversion` 完了後の後段語彙修正 scope。

### 問題点

admin console workflow Step wording を seed conversion 実装前に混ぜると、route retirement / seed render wiring と wording repair が同時変更になり、`/admin/ui-builder` を `/admin/contents` local Step 4 と誤表記する余地が残る。

### 目的

seed conversion 完了後に admin authoring workflow の Step wording boundary を明示し、local submit pipeline と whole-admin workflow を混同しない。

### 改善方針

- seed conversion 実装には混ぜず、後段 scope として実施する。
- `/admin/contents = local submit pipeline Step 1-3`。
- `/admin/ui-builder = whole-admin Step 4`。
- `/admin/manifests = whole-admin Step 5`。
- `/admin/contents -> /admin/ui-builder -> /admin/manifests` の flow を維持する。

### 対応資料

- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `.agent/tasks/todo.md`

### 対象ファイル名

- `.agent/tasks/todo.md`
- `frontend/routes/admin/contents.tsx`
- `frontend/routes/admin/ui-builder.tsx`
- `frontend/routes/admin/manifests.tsx`
- frontend admin navigation / header / stepper components
- tests that assert admin wording and flow labels

### 対象関数名

- future admin workflow step label builders
- future admin navigation view model functions
- future admin route stepper rendering functions

### 受入条件

- Step wording repair is seed conversion 後段 scope として扱われている。
- `/admin/ui-builder` is whole-admin Step 4 and `/admin/manifests` is whole-admin Step 5.
- `/admin/contents` local submit pipeline remains Step 1-3 and is not extended to own whole-admin Step 4/5 wording.

---

## Bundle `helper-manual`

**Status:** not_started
**Roadmap/status SSOT:** `product.helper_manual_policy`
**Primary SSOT:** `docs/design/user-facing-helper-manual-ssot.yaml`
**Design prerequisite status:** schema / seed / admin helper viewer 実装前に `docs/design/user-facing-helper-manual-ssot.yaml` の clone lifecycle reference contract を正本として読むこと。

目的:
仕様確定後 scope として、helper/manual をユーザー向け文章方針だけでなく、人間 / Agent / MCP / External AI / Local LLM / admin UI が共通参照する JSON helper reference artifact として実装可能にする。現行MCPの import-candidate / draft_operation / commit_candidate lane に topology authoring draft を載せる参照点を作り、admin では同じ内容を viewer として表示する。最新 `/admin/contents` Step 1 の `create_new_topology` / `clone_active_as_replacement_draft` / `clone_active_as_new_topology_draft`、`draft_origin`、`clone_mode`、replacement merge authority boundary、SQL Attention candidate boundary、`layoutPatchDraft` と production manifest replacement merge の分離を JSON contract 上で混同しない。

残問題:
- `helper_reference_artifact` の schema / seed がまだ無い。実装前に `admin_topology_clone_lifecycle_reference_contract` / `replacement_merge_authority_boundary` / `sql_attention_candidate_reference_boundary` / `layout_patch_draft_vs_manifest_replacement_boundary` を schema required / strongly-recommended fields へ写像する必要がある。
- MCPで topology authoring draft を構築する際の `structured_output_payload` / `assigned_business_object_candidate` / `assignment_target_scope` / `preview_diff` / `unresolved_fields` の具体例が未実装。`entry_mode` / `draft_origin` / `clone_mode` / `source_active_manifest_id` / `source_active_evidence` / `lineage_evidence_only` / `replacement_merge_intent` / `replacement_merge_blockers` / `backend_merge_authority` を含め、replacement clone と clone-as-new topology を payload 上で混同しない必要がある。
- admin 共通ヘッダから開く helper viewer が未実装。viewer は clone lifecycle badge、replacement-vs-lineage-only badge、backend authority notice、stale source / active identity conflict blocker、SQL Attention candidate boundary、layout patch not replacement merge notice を表示する必要がある。
- helper viewer が admin submit / apply / promote / approval / merge target decision / active mutation を実行しない projection-only surface であることを実装上確認する guard が無い。
- AI/MCP由来 candidate evidence、SQL Attention candidate、人間の admin 手作業 draft、manual replacement clone draft、clone-as-new topology draft の origin / lineage / authority を混同しない表示・保存・監査境界が未検証。
- source evidence / lineage evidence だけで replacement authority を得ないこと、replacement merge は backend AdminRuntime / ManifestRepository transaction のみが source evidence・validation・diff/log evidence・stale source check・active identity conflict check 後に existing active row update + working draft row delete として成立することを schema / seed / tests へ渡す必要がある。
- `layoutPatchDraft` / `layout_patch:apply` は UI Builder layout draft / layout persistence であり production manifest replacement merge ではない、という helper artifact 上の boundary が未実装。

改善方針:
implementation_change で、SSOTに従って helper schema / seed artifact を追加し、admin common header から Drawer helper viewer を開けるようにする。viewer は検索・カテゴリ選択・tree viewer・detail modal mount と clone lifecycle boundary 表示までに限定し、runtime/admin/MCP authority を持たせない。MCP新規tool surfaceは作らず、既存 import-candidate lane の payload reference として実装する。schema / seed は `create_new_topology`、`clone_active_as_replacement_draft`、`clone_active_as_new_topology_draft`、`manual_new`、`manual_clone_replacement`、`manual_clone_new_topology`、`sql_attention_candidate`、`none`、`replacement`、`new_topology` を明示し、SQL Attention candidate は explicit human/admin adoption まで candidate/evidence surface に留める。

対応資料:
- `docs/design/user-facing-helper-manual-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/cli-model-context-protocols-port-ssot.yaml`
- `docs/design/cli-mcp-port-implementation-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `docs/design/ci-contract-ssot.yaml`
- `docs/design/runtime-bundle-audit-approval-ssot.yaml`
- `docs/framework-policy.yaml`

対象ファイル名候補:
- `docs/helper/helper-manual.schema.json` (new helper artifact schema)
- `docs/helper/helper-manual.seed.json` (new helper reference seed)
- `frontend/islands/*Admin*.tsx` or common admin shell/header files (helper launch button)
- `frontend/islands/*Helper*.tsx` or future `AdminHelper*` component files
- `frontend/lib/*helper*` (helper artifact load/filter/tree utility)
- `backend/runtime/AuthorizedCliReaderPortRuntime.cs` (reference only; MCP operation expansionは原則しない)
- `backend/tests/Topolactor.Runtime.Tests/AuthorizedCliReaderPortRuntimeTests.cs` (reference only; candidate origin regression確認)

対象ブロック名:
- `helper_reference_artifact`
- `admin_topology_clone_lifecycle_reference_contract`
- `replacement_merge_authority_boundary`
- `sql_attention_candidate_reference_boundary`
- `layout_patch_draft_vs_manifest_replacement_boundary`
- `mcp_topology_authoring_draft_reference`
- `admin_helper_projection`
- `provenance_boundary`
- `user_facing_message_policy`
- `language_policy`
- `helper_manual_category_candidates`
- `safety_boundary`
- `relation_to_other_ssot`

対象関数名候補:
- future `loadHelperManualSeed`
- future `filterHelperManualItems`
- future `buildHelperManualTree`
- future `renderHelperManualDetail`
- future `openAdminHelperDrawer`
- future `mountDetailHelperModal`
- future `renderCloneLifecycleBadge`
- future `renderReplacementAuthorityNotice`
- future `renderLineageOnlyBoundaryNotice`
- future `renderSqlAttentionCandidateBoundaryNotice`
- future `renderLayoutPatchNotReplacementMergeNotice`

残受入条件:
- [ ] helper schema / seed artifact が追加され、SSOTの required fields と clone lifecycle reference contract を満たしている。
- [ ] helper seed に admin authoring flow / admin topology clone lifecycle / MCP topology authoring draft / UI Builder / CI Attention / approval boundary のカテゴリがある。
- [ ] internal vocabulary と user-facing vocabulary の対応が helper artifact に定義されている。
- [ ] MCP topology authoring draft の payload example が、既存 import-candidate lane の field に対応している。
- [ ] `create_new_topology` / replacement clone / clone-as-new topology が JSON contract 上で混同されない。
- [ ] source evidence / lineage evidence が replacement authority ではないことを schema / seed / viewer 表示で確認できる。
- [ ] SQL Attention candidate が explicit adoption 前に draft row / production merge authority へ化けない。
- [ ] `layoutPatchDraft` / `layout_patch:apply` が production manifest replacement merge ではないことを artifact と viewer で確認できる。
- [ ] admin common header から helper Drawer を開け、検索・カテゴリ選択・tree viewer・detail modal が使える。
- [ ] helper viewer は admin submit / apply / promote / approval / merge target decision / active mutation / MCP operation を実行しない。
- [ ] AI/MCP由来 candidate evidence と human manual admin draft の origin を混同しない表示・監査境界が確認できる。
- [ ] 新規 MCP tool surface / admin submit direct execution / active topology mutation は追加していない。

---

## Bundle `product-nocode-loop-acceptance`

**Status:** acceptance_pending
**Roadmap/status SSOT:** `docs/system-roadmap.yaml`

### 問題点

統合 UX の手動受入 / hand-debug evidence gap は残っているが、これは product 実装 Bundle ではなくオーナーが仕様を精査する acceptance_pending scope である。Agent が仕様確定や受入完了を代行すると、manual acceptance を実装完了判定へ誤変換する危険がある。

### 目的

オーナーが `product.dynamic_support_nocode_loop` と `product.admin_topology_authoring` の統合 UX を手動受入し、仕様確定済み範囲と未確定範囲を判断できるようにする。

### 改善方針

- Agent は仕様確定や受入完了を代行しない。
- runtime dispatch loop、ProjectionShell SSE refresh、recommend child island、SQL Attention feedback projection、admin CSV/JSON import、admin authoring routes、external port consumer projection、team Markdown dashboard は実装済みとして扱い、未実装扱いに戻さない。
- `scheduler-job-manifest-admin-ui` と `helper-manual` は別 canonical bundle で扱い、この手動受入に混ぜない。

### 対応資料

- `docs/system-roadmap.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `.agent/tasks/todo.md`

### 対象ファイル名

- `.agent/tasks/todo.md`
- `frontend/routes/admin/contents.tsx`
- `frontend/routes/admin/ui-builder.tsx`
- `frontend/routes/admin/manifests.tsx`
- `frontend/routes/admin/team-dashboard.tsx`
- ProjectionShell / SSE refresh / SQL Attention feedback projection files discovered by owner or Agent during acceptance support

### 対象関数名

- future acceptance checklist support helpers discovered by Agent
- future ProjectionShell refresh observation helpers discovered by Agent
- future admin import / clone / replacement merge acceptance helpers discovered by Agent

### 受入条件

- [ ] `product.dynamic_support_nocode_loop` の combined UX を、authoring guidance → SQL Attention feedback → M6 admin loop の通し手動受入 / hand-debug で確認する。
- [ ] `product.admin_topology_authoring` の `/admin/contents` Step 1 3 entry mode → clone draft → edit → backend replacement merge を、統合UX手動受入 / hand-debug evidence として確認する。

### 手動受入 checklist
- [ ] `/admin/contents` で、作成・編集・import・apply の現在位置、draft / preview / validate / apply / saved / failed の関係、apply前後の変更差分と反映先を誤認しない。
- [ ] validation 失敗後、画面を離れずに修正へ戻れ、作業文脈が途切れない。
- [ ] `/admin/ui-builder` で、配置・style・binding の編集中状態と反映済み状態、modal / drawer / preview の関係を混同しない。
- [ ] advanced / internal vocabulary が通常操作の判断を邪魔せず、必要な説明だけが出ている。
- [ ] Admin import の CSV / JSON import → preview → editor merge → validate → apply が一連の体験として見え、apply後の projection 反映先を追える。
- [ ] recommendation / SQL Attention feedback は現在状態ではなく候補・観察結果として見え、採用しない限り route / topology / 画面状態が変わったように見えない。
- [ ] 古い・対象なし・根拠が弱い candidate が、ユーザーに採用を強制する表示に見えない。
- [ ] webhook / hook / external port consumer projection で、route / credential requirement reference、secret非表示、受信・拒否・成功・失敗、承認前・承認後・拒否後、provider未接続/future scope の状態を誤認しない。
- [ ] file export / transfer / email / audit approval の結果 projection が成功・失敗・保留として追え、失敗時に再試行すべきか設定を直すべきか判断できる。
- [ ] `/admin/team-dashboard` / MdViewer で、saved view / rendered Markdown / source / binding / completed_preset_seed summary の関係を誤認せず、Markdown body を runtime SSOT と見なさない。
- [ ] refresh / clone / rebind の可否、seed invalid の explicit error、md_viewer read projection boundary が画面上で自然に読め、mutation authority と混同しない。
