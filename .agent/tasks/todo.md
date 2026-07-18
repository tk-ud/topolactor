# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `in_progress` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `helper-manual` | helper reference artifact / admin helper projection | not_started | 1 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `seed-template-runtime-interaction-assignment` | Seed/template projection runtimeInteractionId assignment path | implemented | 1 | `product.dynamic_support_nocode_loop` / seed-template projection adoption carry-over | `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`, `docs/design/react-schema-topology-seed-translator-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 2 | `product.dynamic_support_nocode_loop` | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |
| `admin-surface-topology-seed-conversion` | Admin hardcoded surface topology seed conversion（`role-based-surface-separation` はこの Bundle の pre-seed-implementation evidence として統合済み — 2026-07-14、下記 Bundle 本文の該当 subsection 参照） | not_started | 5 subBundle | `product.dynamic_support_nocode_loop` / admin hardcoded surface retirement | `docs/design/admin-normal-surface-projection-seed-ssot.yaml`, `docs/design/react-schema-topology-seed-translator-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml`, `docs/design/instance-port-substrate-ssot.yaml` |
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

**用語補足（読み違い防止、`docs/design/admin-normal-surface-projection-seed-ssot.yaml` `seed_physical_hierarchy_and_definition` と同一の定義）:** ここでの「seed」は抽象的なスキーマ設計成果物ではなく、`hub_relation`（`hubs.hub_relations`）→ `topology`（`hubs.topology_manifests`／`manifest.topology`、`/admin/contents` が作るものと同種）→ `hub`（`hubs.hub`、物理DB関係ノード）・`package`（`topology.ui_component_package`/`components_package_design`/`components_layout_design`/`ui_wiring_registry`/`ui_topology_tensor`、`/admin/ui-builder` が作るものと同種）という具体的なDB行そのものを指す。`/admin/contents`・`/admin/ui-builder`・`/admin/manifests` を実際に使えば得られたはずの成果物と同型の、初期構築済みの見本データである。translator（`.agent/tools/react-schema-topology-seed-translator`）はこの行を作るための変換costをagentが肩代わりしなくて済むための便宜ツールに過ぎず、独立した第二の生成経路や本Bundleの成果物そのものではない。**本Bundleの todo は translator の修正・拡張ではなく、投影側（上記の行）の初期実装を構築することである。**

### 改善方針

- 共通工程を全 subBundle で固定する（工程2〜4は一体で「seed行を構築する」作業であり、translatorはその内部で使う変換ツールに過ぎない）。
  1. hardcoded surface 読込。
  2. React-like Schema 作成。
  3. `.agent/tools/react-schema-topology-seed-translator` で topology UI seed candidate へ変換。
  4. topology UI seed 生成結果を seed 登録へ写像（＝ `hub_relation`/`topology_manifests`/`hub`/`package` 系テーブルへの実際のDB行構築）。
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
- Topology UI seed production: 2026-07-18、owner明示決定によりpause解除済み（詳細は下記「Owner pause lifted」節参照）。design blockerの個別解決はpause解除とは別軸であり、subBundle単位でsubbundle_statusの`unresolved_before_seed`を個別に解消（またはSSOT裏付けのある`subBundle_not_applicable`証明）してからでなければ、そのsubBundleのseed generation/registrationへは進めない。

#### PR587 design_blocking 再監査

| design_blocking id | 分類 | 根拠 / 残scope |
|---|---|---|
| `target_surface_manifest_readiness` | credential-management active target manifest: `resolved_existing_substrate`; credential-management explicit navigation binding authoring / verification: `unresolved_before_seed`; admin-enum / team-dashboard / scheduler-settings target manifest + later binding: `unresolved_before_seed`; admin-dashboard: `subBundle_not_applicable` | Canonical hub relation authoring/resolution substrateは既存 `/admin/manifests` + `HubNavigationAdmin.tsx` + `hub_navigation:*` dispatch + `hubs.hub_relations` + `ManifestDispatcher` で成立済み。credential-managementはmanifest 092の active `ui_projection` target manifest readiness はresolved。ただし live `hubs.hub_relations` row状態はTODO固定台帳にせず、seed開始前に明示的なnavigation binding authoring / verificationを行う残scopeは落とさない。admin-enum / team-dashboard / scheduler-settings は各画面固有 `ui_projection` manifest 生成後、同じ canonical authorityで明示binding authoring / verificationが必要。admin-dashboardはlanding / navigation / guide entry責務であり、business target surface manifestやfake source manifestを作る対象ではない。 |
| `external_instance_projection_columns` | physical schema / approved candidate source: `resolved_existing_substrate` for credential-management instance_settings; projection seed binding / authoring surface / operation wiring / proof: `unresolved_before_seed`; other subBundles: `subBundle_not_applicable` | 既存substrateとして `topology.db_instance_port` / `topology.runtime_instance_port` / `topology.instance_connection_policy` / `topology.instance_operation_authority_binding`、`NpgsqlInstancePortPolicyRepository`、および `instance_operation_authority_binding` を admin-approved candidate source とする既存契約がある。新table/column/JSON key/candidate sourceは設計しない。残scopeは既存column / approved candidate sourceからprojection seedへのbinding、既存credential-management projection extension上のauthoring surface、operation wiring、secret-deny proofに限定する。 |
| `normal_dashboard_authoring_runtime_adapter` | `resolved`（role-based-surface-impl bundle, 2026-07-14）for team-dashboard normal.dashboard; `subBundle_not_applicable` for admin-dashboard / credential-management / admin-enum / scheduler-settings | `frontend/components/catalog.ts` の `md_translation_authoring_surface.authoring` は `runtimeConnected=false`, `runtimeReachability: "existing_route_composition"`, `routeCompositionFile: "frontend/islands/TeamDashboardRoleSurface.tsx"` として route composition経由の到達可能性を明示。route compositionをcanonicalな到達手段として確定済みであり、runtime factory登録のowner decisionは次PRへ持ち越さない（次PRで残るのは production seed row登録自体のみ）。admin-gated preview/validate/write/diff emissionのadapter bindingは`SavedViewOperationPanel({mode, savedView, onWritten, onCancel})` / `SavedViewAdjustmentAuthoringPanel({savedView, onWritten, onCancel})`として実装済み（当初想定していた`props [onSaved, onCancel, placement]`ではなく、round 5でこの実props形へ訂正済み）。 |
| `normal_dashboard_write_operation_binding` | `resolved_existing_substrate` for backend operation substrate; `unresolved_before_seed` for seed binding selection; `subBundle_not_applicable` for admin-dashboard / credential-management / admin-enum / scheduler-settings | `team_markdown:*` dispatcher_mapping / AdminRuntime / repository substrateはPR584 remediationで維持済みで、team Markdown SSOTがpersistence authorityを持つ。一方、PR587 SSOTはpreview/validate/write/diffのseed側 operation binding key選定を後続seed作業で行う必要があるため、seed generation前に既存team Markdown runtime actionへ明示bindし、conflict時はdesign conflictとして止める。 |
| `credentials_users_account_transaction_binding` | `resolved`（2026-07-14 round 3 proof-first closure）— create account + initial credential: `resolved_existing_substrate`; delete account + credential consistency: `resolved_existing_substrate`; password replace / rotate: `retired_permanent_ng`（owner決定済み、pending判断ではない）; self password change: `resolved`（self-service surface, POST /auth/me/password); session/credential/role revocation consistency proof: `resolved`（real PostgreSQL live-DB test証拠あり）; other subBundles: `subBundle_not_applicable` | `docs/design/admin-master-roster-management-ssot.yaml` の現行 action は `auth_users:list/search/get/create/update/delete` で、`backend/schema/AuthMasterContracts.cs` のDTOも create は password を持つが update は metadata/status系のみで password replace / rotate DTOを持たない。password replace / rotate（admin が他ユーザーの password を指定する操作）は owner NG 指示により永続的に未実装（admin credential REVOKE のみ提供、`backend/service/AuthService.cs AdminRevokeCredentialAsync`）であり、これは `owner_decision_required` な保留ではなく確定した設計決定である。`auth.users` / `auth.credentials` consistency proof は `JwtGuardSessionRevocationLiveDbTests` / `AuthSessionRevocationLiveDbTests`（実 NpgsqlAuthRepository / NpgsqlAuthMasterRepository、実 PostgreSQL）が session-identity cross-check・password change/session revoke/credential revoke/role change後の旧JWT拒否・inactive/unapproved/suspended account拒否を証明済み。`auth_users:update` をpassword replace / rotate代替として扱わず、存在しない `auth_users:replace_password` / `auth_users:rotate_password` も実装済みauthorityとして記録しない。 |

#### 5 subBundle別 残scope分類

- `admin-dashboard`: landing / navigation / guide entry のみ。business projection化、fake hub/manifest作成、`/admin` 自身をhub relation source必須とする設計はNG。残scopeはseed production再開後のhardcoded surface読込・React-like Schema化・translator変換・seed登録写像・canonical admin mechanismでのrender/navigation確認・proof更新・最後のhardcoded route/island/old route-presence test撤去。
- `credential-management`: manifest 092 / existing `?manifest=` / canonical_default_entry / `/admin/users` auth_users CRUD は既存到達経路として扱う。active `ui_projection` target manifest readinessは既存substrate resolved。ただし explicit navigation binding authoring / verification はseed前残scopeであり、live `hubs.hub_relations` row状態はTODO固定台帳にしない。instance_settingsは既存 `topology.db_instance_port` / `topology.runtime_instance_port` / `topology.instance_connection_policy` / `topology.instance_operation_authority_binding` と `NpgsqlInstancePortPolicyRepository` / approved `instance_operation_authority_binding` candidate sourceからprojection seedへbindする残scope。`credentials.users` は create account + initial credential / delete account + credential consistency は既存substrateあり、password replace / rotate は owner NG により永続的に対象外（`retired_permanent_ng`、pending扱いではない）、consistency proofは実PostgreSQL live-DB testで解決済み。admin user分離、standalone route/dedicated panel/raw table editorはNG。
- `admin-enum`: enum dictionary/group/item/status dependency authorityは既存SSOTと `enum_dictionary:*` substrateに従う。target `ui_projection` manifest は未作成で `unresolved_before_seed`。target manifest生成後は `/admin/manifests` / `hub_navigation:*` canonical authorityで明示binding authoring / verificationを行う。CRUD presetのgeneric shapeは参考にできるが `content_bundle:*` refsをコピーせず、enum authority operationへbindする。
- `team-dashboard`: team Markdown saved view / rendered Markdown / completed preset seed summary authorityは既存SSOTに従う。target `ui_projection` manifest は未作成。target manifest生成後は明示binding authoring / verificationを行う。normal.dashboard viewer/inputer責務分離、`md_translation_authoring_surface.authoring` runtime adapter/route-composition binding、preview/validate/write/diff operation bindingは全て解決済み（role-based-surface-impl bundle, 2026-07-14）。seed前残scopeは target manifest生成とbinding authoring / verificationのみ。
- `scheduler-settings`: scheduler job manifest / create-edit-disable authorityは既存SSOTと `scheduler_jobs:*` substrateに従う。target `ui_projection` manifest は未作成で `unresolved_before_seed`。target manifest生成後は明示binding authoring / verificationを行う。scheduler runtime policyをfrontend constantsへ隠さず、既存backend/dispatcher substrateへ明示bindする。

### Owner pause lifted（2026-07-18、PR592 gate0 audit 受け owner 明示決定）

**この節が topology UI seed production pause 状態に関する現在の正本である。** 上記「PR587後 現在の状態（正本、2026-07-14）」節の pause 関連記述はこの節により更新済み（Bundle Status・SubBundle scope・design_blocking の内容自体はこの節により一切変更されない — 変更されるのは pause 状態の記述のみ）。

- **決定:** `admin-surface-topology-seed-conversion` の topology UI seed production owner pause（PR #584 review comment, 2026-07-11 由来、本ファイル該当節参照）は 2026-07-18 時点で解除された。契機は PR592（`admin-surface-topology-seed-conversion: add per-subBundle granularity to design_blocking`、`docs/design/admin-normal-surface-projection-seed-ssot.yaml` の5件の `design_blocking` entry へ `subbundle_status` を追加）の gate0 監査。
- **意味すること:** `implementation_change` はこの Bundle の work として着手してよい（subBundle 単位）。
- **意味しないこと（誤読厳禁 — pause解除とdesign_blocking解決の混同は禁止）:**
  - pause解除は design_blocking の解決を意味しない。未解決の design_blocking entry を resolved 扱いにしない。
  - `docs/design/admin-normal-surface-projection-seed-ssot.yaml` `design_blocking[*].subbundle_status` に現在も `unresolved_before_seed` として残る項目——`target_surface_manifest_readiness`（credential-managementのnavigation binding authoring/verification; admin-enum/team-dashboard/scheduler-settingsのtarget manifest自体）、`external_instance_projection_columns`（credential-managementのprojection seed binding/authoring surface/operation wiring/proof）、`normal_dashboard_authoring_runtime_adapter`（team-dashboardのruntime adapter、reopened）——は、pause解除後も個別に解消されるまで unresolved のまま維持する。
  - `credentials_users_account_transaction_binding.subbundle_status.credential-management.admin_driven_password_replace_or_rotate: retired_permanent_ng` は pending gap として復活させない（永久決定のまま）。
  - `subBundle_not_applicable` を resolved と誤読しない（「このsubBundleをそのidが一切gateしない」の意味であり、「解決済み」の意味ではない）。
- **進行ゲート（subBundle単位、必須）:** seed generation / seed registration は、対象subBundleについて `subbundle_status` が `subBundle_not_applicable` ではない applicable な design_blocking entry がすべて `unresolved_before_seed` ではない状態（`resolved` / `resolved_existing_substrate` / `retired_permanent_ng` のいずれか）になっている場合にのみ進めてよい。現時点でこの条件を満たすのは `admin-dashboard`（5 id すべて `subBundle_not_applicable`）のみ。他の4 subBundle（credential-management / admin-enum / team-dashboard / scheduler-settings）はそれぞれの残 `unresolved_before_seed` を個別に解消（またはSSOT裏付けのある `subBundle_not_applicable` 証明）してから着手する。
- **safe owner decision record（正本文言）:**
  > Owner pause is lifted for `admin-surface-topology-seed-conversion` (2026-07-18). implementation_change may proceed as Bundle work, subBundle by subBundle, following the common process fixed in .agent/tasks/todo.md. This does not mark unresolved design_blocking entries as resolved. Seed generation / seed registration for any subBundle requires either applicable design_blocking resolution for that subBundle (per design_blocking[*].subbundle_status), or SSOT-backed subBundle_not_applicable proof. Generated artifacts and translator output are not seed adoption authority. No route deletion before render/action wiring proof.
- **同時実施したSSOT訂正（design_change、本節と同一コミット）:**
  - `docs/design/admin-normal-surface-projection-seed-ssot.yaml` `seed_implementation_start_conditions`: 「All design_blocking entries are resolved」という Bundle一括表現を、`design_blocking[*].subbundle_status` 粒度（対象subBundleに applicable な entry のみが対象、`subBundle_not_applicable` はゲート対象外）へ訂正。
  - 同ファイル `design_blocking.normal_dashboard_authoring_runtime_adapter.required_resolution_before_seed` および `surface_axes.normal.normal_hub_relation_navigation_contract.current_target_readiness.dashboard_team_markdown`: 「owner instruction to resume ... required first」「remains owner-paused pending explicit future owner instruction」という pause待ち文言を、pause解除済み・残るblockerは個別の未実装状態のみである旨へ訂正。
  - `docs/design/admin-console-workflow-ssot.yaml` `subbundle_target_readiness.enum_dictionary`（`team_dashboard`/`scheduler_settings` はこれを参照する文言のため連動）: 同様に owner-paused 文言を訂正。
  - `docs/design/auth-db-session-credential-ssot.yaml` の self-service endpoint note内 pause文言も同様に訂正（この self-service pattern はそもそも pause の対象外だったことを明記）。
  - いずれも design_blocking の値（`subbundle_status`・`status`・`resolution_record`）自体は変更していない。変更したのは「pauseを理由に待っている」という表現の除去のみ。

### admin-dashboard subBundle 実装記録（2026-07-18、Owner pause解除後の最初の implementation_change）

**この節は Bundle の Status を `not_started` から変更しない。** `admin-dashboard` は5件の design_blocking すべてが `subBundle_not_applicable` である唯一の subBundle（上記「Owner pause lifted」節参照）であり、本記録はその subBundle 単独の実装作業である。他 4 subBundle（`credential-management`/`admin-enum`/`team-dashboard`/`scheduler-settings`）は未着手のまま。

- **調査結果（正本判断の根拠）:** `docs/design/admin-console-workflow-ssot.yaml` `page_responsibility.admin_index` および `admin_hub_relation_navigation_contract.prohibited`（`empty_or_fake_topology_manifest_created_solely_as_a_hub_relation_connection_source`・`requiring_admin_own_landing_page_to_be_a_hub_relation_source`）は、`/admin` 自身に `hubs.hub` / `hubs.topology_manifests` row を一切 fabricate しないことを明示している（"`/admin` has no hubs.hub / hubs.topology_manifests row of its own, and none is fabricated to give it one"）。また `docs/design/runtime-orchestration-ssot.yaml` `frontend_routes.admin_route_retirement_matrix.routes` に `/admin` 自身の retirement row は存在しない（`/admin/enums`・`/admin/team-dashboard`・`/admin/scheduler` のみ）。このため、`admin-dashboard` の seed 対象である `surface_axes.admin.surfaces.dashboard`（hub_relation search/list/navigate、`hub_relation_search`/`hub_relation_link_list`/`target_projection_shell` の component_tree）を **`/admin` 専用の live/reachable manifest として物理登録することは、本 PR のスコープでは行わない** — 行えば上記 prohibited パターンに直接抵触する。
- **実施内容（React-like Schema → translator → topology UI seed candidate → 構造 render 証明）:**
  1. `docs/design/admin-normal-surface-projection-seed-ssot.yaml` `surface_axes.admin.surfaces.dashboard.seed_contract` に基づき React-like Schema（markup形式 inputText）を作成。
  2. `.agent/tools/react-schema-topology-seed-translator generate-react-schema` → `generate-topology-seed` で変換し、`gateStatus: pass`（validationErrors 0件）の `topolactor.react_schema.v1` / `topolactor.topology_ui_seed.v1` candidate を生成。入力は checked-in fixture として保存: `.agent/tests/fixtures/react-schema-topology-seed-translator/admin-dashboard-hub-relation-navigation.input.json` / `.topology-seed.input.json`（`.agent/tools/logs/generate.log` に regeneration trace 記録済み）。`targetSurface` は `declared_seed_surface_catalog` 未登録の新規キーのため `knownGapRefs`（`ssot_ambiguity_gap`）を明示的に付与（catalog未登録の告白であり、隠蔽ではない）。
  3. `hub_relation_link_list` の `component_key` は SSOT上 `table.primitive` と明記されているが、`table.primitive` は `db/ui_component_registry_preset_catalog_bootstrap.sql` にまだ `topology.ui_component_registry` row を持たない（`components_bucket` 側で `registrationRequired:true`/`code_only_drift` のまま、未promote）。一方 `card_list.primitive` は既に登録済みで、かつ `ui-builder-preset-ecosystem-ssot.yaml` `hub_search_preset`（既存の active_seed、`db/migrations/hub_search_preset_seed.sql`）が全く同じ「search_input.alias → panel.alias → card_list.primitive」構成をこの用途向けに既に使っている。既存substrateを優先する原則（Repository Design Order Invariant）に従い、本PRでは `display=card_list`（`card_list.primitive`）を採用した。`table.primitive` の registry 未promoteは別途の follow-up として残す（下記参照）。
  4. `backend/repository/LayoutSchemaTensorComposer.cs` の `FieldControlToComponentKey` に `form_input/search_input`→`search_input.alias`、`disclosure_structure/panel`→`panel.alias` を追加（両方とも既に `topology.ui_component_registry` に固定UUIDで登録済み・`runtimeConnected:true` の既存コンポーネントの、resolver側の欠落を埋めただけであり、新規コンポーネント登録ではない）。
  5. `backend/tests/Topolactor.Runtime.Tests/LayoutSchemaStructuralCompositionTests.cs` に `ParseRecords_AdminDashboardHubRelationNavigation_AllSevenRecordsRecognized`（round 2 訂正前は `AllNineRecordsRecognized`、下記「round 2 監査」参照）と `ComposeAndMapToLayoutNode_AdminDashboardHubRelationNavigation_MatchesCheckedInFrontendFixture` を追加。後者は translator が生成した実際の `topologyUiSeedFlatRecords`（`topology_ui_projection` は永続化対象外のため除外、既存 credential-management-092 と同じ規律）を実際に `LayoutSchemaTensorComposer.Compose()` に通し、`frontend/tests/fixtures/layout_schema_composed_scenarios/scenario_admin_dashboard_hub_relation_navigation.json`（新規 checked-in fixture, byte-exact）と一致することを証明。ローカルで実行し pass 確認済み（`dotnet test ... --filter LayoutSchemaStructuralCompositionTests` 49/49 pass）。
  6. `frontend/tests/layoutSchemaStructuralRender.test.ts` に上記 fixture を読み込み `renderEmission()` が zero `componentType==='error'` specs を返すことを検証するテストを追加（既存 `scenario_table_workflow_step.json` 等と同じ規律）。ローカル `deno` 不在のため `REQUIRED_NOT_EXECUTED`、remote CI (`check-frontend-types`/frontend test job) 側の green を要求する。
- **やっていないこと（意図的な NG boundary 遵守）:**
  - `/admin` 向けの `hubs.hub` / `hubs.topology_manifests` / `hubs.hub_relations` row を `db/seed_empty.sql` へ追加していない（上記 prohibited 抵触のため）。
  - `frontend/routes/admin/index.tsx` / `frontend/content/adminGuides.ts`（`ADMIN_ROUTE_CARDS`/`ADMIN_INDEX_GUIDE`/`ACCEPTANCE_FLOW_STEPS`/`ACCEPTANCE_CHECKLIST`）を変更していない。hardcoded route/island の削除はもちろん、render/action wiring の変更も行っていない — 上記の通り、live に到達可能な manifest を `/admin` に持たせられない以上、既存 route の描画元を安全に切り替える対象が存在しない。
  - `.agent/scripts/react_schema_topology_seed_translator.py`（translator本体）は一切変更していない。
- **Auditor への引き継ぎ事項（blocking / follow-up、本PRでは解決しない）:**
  1. `topology.ui_component_registry` に `table.primitive` の row が存在しない（`component_catalog_gap`）。SSOT `surface_axes.admin.surfaces.dashboard.seed_contract` の文言通り `table.primitive` を使いたい場合は、まずこの registry promotion が必要。
  2. `admin-dashboard` の topology UI seed candidate を **live な物理 manifest として登録できる到達点が現状存在しない**（`/admin` 自身は禁止、既存 admin authoring 経路 `/admin/contents`→`/admin/ui-builder`→`/admin/manifests` はいずれも他の意味要素 subBundle 向けの target manifest 作成に使われるものであり、"hub_relation search/list/navigate" 自体を表す manifest の置き場所ではない）。この pattern を実際に live 化するには、(a) 既に seed 済みの他 manifest（例: credential-management 092）の `ui_projection` に埋め込む形で採用するか、(b) 新しい到達経路の設計判断のいずれかが owner/design_change 側で必要。本PRはこの決定を代行しない。
  3. `declared_seed_surface_catalog` に `admin.dashboard.hub_relation_navigation`（またはこれに準ずる seed_surface_key）が未登録（`ssot_ambiguity_gap`、今回の translator入力で明示済み）。上記2の到達点が決まった時点で、`docs/design/react-schema-topology-seed-translator-ssot.yaml` `declared_seed_surface_catalog` への正式追加が必要。

#### admin-dashboard round 2 監査（PR #594 owner review comment、4件のblocking候補調査）

owner から PR #594 へ、既存正常経路・SSOT authority・Bundle完了境界と比較して4件のblocking候補を確定判定するよう指示があった。全4件を最後まで調査した（1件確定時点で停止していない）。判定はこの節が正本であり、PR本文は開始時点の証跡として編集していない。

1. **`select_hub_relation_link` の `internal_instance_wiring/localStateMutation` が既存hub navigationからtarget projectionへ到達する経路を成立させているか — 確定BLOCKING、同一PRで修正済み。**
   `frontend/islands/ProjectionShell.tsx` から `frontend/runtime/projectionEntry.ts` `resolveHubNavigationLinks(emission.navigationSequence)` を追跡した結果、既存の hub navigation 到達経路（`hubs.hub_relations` → backend `NavigationSequence` 生成 → `resolveHubNavigationLinks` → `<a href="?manifest=<id>">` → `projectionEntry.ts` の `?manifest=` 解決 → backend manifest 解決 → target projection）は**すでに完全に汎用実装済みであり、per-manifest の authored wiring を一切必要としない**（`ProjectionShell.tsx` が `emission.navigationSequence` があるマニフェストなら自動的にこの `<nav data-projection-hub-navigation>` を描画する）。一方、当初PRで authored した `select_hub_relation_link` Action（`ui-local:hub_relation_link_list.selected_row` へのローカル状態変更）は、その state slot を読む consumer が実装内に一つも存在せず、`target_projection_shell` にも何の影響も与えない。すなわち後段処理を含めて経路が成立していないことを確認した。**修正: `select_hub_relation_link` Action と対応する `runtimeInteractions` をReact-like Schema・translator fixture・backend/frontend proof から完全に削除した**（既存の汎用 `resolveHubNavigationLinks` 経路を重複実装するだけで、かつ非機能的だったため）。`hub_relation_search`/`hub_relation_link_list`/`target_projection_shell` は構造・表示のみのplaceholderとして残し、機能実装を主張しない。
2. **`admin-dashboard` subBundleの完了境界がReact-like Schema/translator/structural render proofまでか、physical seed adoptionまで含むか — non-blocking（本PRの範囲としては）、ただし重要な注記あり。**
   `docs/design/admin-console-workflow-ssot.yaml` `admin_hub_relation_navigation_contract` の `connection_state_authority` と、本 Bundle の "checkpoint" governance（`.agent/protocols/implementation-change.md` PR/Bundle/checkpoint境界: 「小粒な実装進行や途中checkpointは、同一PR内でBundle completionへ進むための作業単位」）から、1PRで必ず physical seed adoption まで到達する必要はない。ただし、上記1の発見が示す通り、`surface_axes.admin.surfaces.dashboard`（hub_relation search/list/navigate）が求める **機能自体はすでに resolved_existing_substrate として存在しており**、admin-dashboard に「変換すべき hardcoded surface」が実は存在しない可能性がある——todo本文が対象ファイルとして明記する実際の hardcoded surface は `frontend/routes/admin/index.tsx` / `frontend/content/adminGuides.ts`（`ADMIN_ROUTE_CARDS`/`ADMIN_INDEX_GUIDE`）であり、本PRはそちらには一切触れていない。`page_responsibility.admin_index` は当該コンテンツの wording を別Bundle（`frontend-canonical-surface-structure-label-boundary`）の責務とし、本Bundleは「どこから sourcing するか」のみを持つとしているが、その sourcing 先決定自体は本PRでは行っていない。これは blocking ではなく、次PRへの明示的な残scopeとして記録する（Bundle Status は引き続き `not_started`）。
3. **`table.primitive`指定と`card_list.primitive`採用の整合性 — non-blocking、既存判断を維持。**
   `db/ui_component_registry_preset_catalog_bootstrap.sql` を authority 順に再確認した。事実のみを記録する（不在から設計意図を推定しない、round 3 監査の NG 軸遵守）: `topology.ui_component_registry` への行挿入は `component_registration:register_or_update_projection_component`（UI Builder canvas drop = 実authoring証跡）またはこの bootstrap ファイルへの明示的追加のいずれかを経由する。`table.primitive` はどちらの経路の記録も現状存在しない（`components_bucket` には `registrationRequired:true`/`lifecycleStatus:"code_only_drift"` の行のみ存在し、対応する `ui_component_registry` 行は無い）。この不在の原因（未使用なのか、単なる未対応なのか）を判定する証拠はなく、意図の推定はしない。他方 `card_list.primitive` は既に登録済み・`runtimeConnected:true` であり、`hub_search_preset`（`docs/design/ui-builder-preset-ecosystem-ssot.yaml`、`status: active_seed`）が「search_input.alias → panel.alias → card_list.primitive」という**全く同一の意味用途**（読み取り専用の検索結果/リンク一覧）に既に使用している。ここで `table.primitive` を実authoring証跡なしに bootstrap SQL へ手動追加することは、「実装都合でSSOTを後追いでratifyする」禁止パターン（`implementation_first_shape_ssot_ratification`）の縮小版に相当するため行わない。`card_list.primitive` 採用を維持する。
4. **今回のaction wiringがpipeline-continuityのTier 2を要するか — 確定BLOCKING、上記1の修正で解消。**
   `docs/design/pipeline-continuity-ssot.yaml` `test_tier_policy.tiers.tier_2_scenario_harness.required_when` は `ui_operation_wiring_added` を明示的に含む。当初PRの `select_hub_relation_link` Action は新規 UI operation wiring に該当し、Tier 2 (「state source の変化と、それを consume する最終描画/状態」を証明するharness) を要した。しかし上記1の調査で判明した通り、`ui-local:hub_relation_link_list.selected_row` を読む consumer が存在しないため、正当な Tier 2 harness（例: `button_click_modal_open_close` 型の「click → local state → 最終DOM/props反映」証明）を**捏造なしに構築することが原理的に不可能**だった。Action を削除したことで Tier 2 適用条件（`ui_operation_wiring_added`）自体が消滅し、残る structural proof（Tier 0/1 相当: 構文・構造の合成が real composer を通ることの証明）のみが適用範囲となった。
   
   **修正後の再監査:** `backend/tests/Topolactor.Runtime.Tests/LayoutSchemaStructuralCompositionTests.cs` の該当テストを7レコード版に更新（`ParseRecords_AdminDashboardHubRelationNavigation_AllSevenRecordsRecognized`）、`frontend/tests/fixtures/layout_schema_composed_scenarios/scenario_admin_dashboard_hub_relation_navigation.json` を再生成（Action node なし、7 nodes）、`frontend/tests/layoutSchemaStructuralRender.test.ts` の対応テストも更新。`.agent/tests/fixtures/react-schema-topology-seed-translator/admin-dashboard-hub-relation-navigation.{input,topology-seed.input}.json` を再生成し `gateStatus: pass` を再確認、`.agent/tools/logs/generate.log` に新しい regeneration trace を追記。`dotnet test backend/tests/Topolactor.Runtime.Tests` 1445/1445 pass 再確認。

#### admin-dashboard round 3 監査（PR #594 owner review comment、round 2 checkpoint clear後の残候補確定）

owner から round 2 checkpoint clear を確認した上で、(a) `select_hub_relation_link` が abstract 契約か physical record 契約か、(b) generic substrate 充足時の canonical omission 表現の有無、(c) `knownGapRefs` 省略説明の妥当性、(d) admin-dashboard の真の変換対象（hub_relation navigation か `ADMIN_ROUTE_CARDS`/`ADMIN_INDEX_GUIDE` か）、(e) wording/sourcing 責務の Bundle 間混同有無、(f) `table.primitive` 不在からの意図推定回避、の6点について、SSOT逐語比較による確定判定の指示があった。全件調査完了（1件確定時点で停止していない）。

1. **`select_hub_relation_link` は abstract 契約か physical record 契約か — 確定：abstract 契約（既存 generic substrate で充足）。**
   `docs/design/admin-normal-surface-projection-seed-ssot.yaml` `surface_axes.admin.surfaces.dashboard.seed_contract.event_bindings[0]` を逐語確認: `{event_key: select_hub_relation_link, event_kind: select, trigger: "projection_change", fail_close_on_ambiguous_target: true}`。`trigger` 値 `"projection_change"` は `docs/design/react-schema-topology-seed-translator-ssot.yaml` `wiring_lane_contract.event_binding_shape.field_rules.trigger`（`one of [click, change, submit, "item.click", mount, "custom:<name>"]`）のいずれにも該当しない——これは DOM event ではなく、projection 層の効果カテゴリを表す語彙である。さらに `projection_responsibility: [..., "use_selected_link_as_projection_change_trigger", ...]` の文言も同じ抽象度で書かれている。**決定的証拠:** `docs/design/runtime-orchestration-ssot.yaml` `dispatcher_contract.hub_navigation_resolution.test_proof_contract.resolution_chain` の最終行が逐語で `"frontend round-trip: NavigationSequence[].TargetManifestId -> resolveHubNavigationLinks -> ?manifest=<uuid> href -> parseProjectionEntrySelection -> resolveProjectionEntryAxes -> payload.target_ref = manifest:<uuid>:projection_entry"` と定義しており、これが hub relation navigation の **canonical completion proof 契約そのもの**である。この契約チェーンのどこにも authored Action / eventBinding record は存在しない。同 test_proof_contract は既存 proof file（`backend/tests/Topolactor.Integration.Tests/CredentialManagementHubRelationUiProjectionLiveDbTests.cs`、`frontend/tests/projectionEntry.test.ts`、共有 helper `HubRelationUiProjectionResolutionChainProof.cs`）を明示的に proof surface として指定しており、これらは将来 enum-dictionary/team-dashboard/scheduler-settings が `ui_projection` seed を持った際にも**再利用される**、manifest-identity-agnostic な契約と明記されている。以上より、`select_hub_relation_link` は topology_ui_seed の physical Action record を要求する契約ではなく、既存 generic substrate（`resolveHubNavigationLinks`）で充足される abstract 契約であることが SSOT 全体から確定した。
2. **generic substrate 充足時の canonical omission 表現の有無 — 確定：現行 SSOT/translator contract に存在しない（真正の gap）。**
   `docs/design/admin-normal-surface-projection-seed-ssot.yaml` `topology_seed_schema_contract.minimum_required_fields.hub_relation_navigation_binding`（`required_shape: list of source_surface, relation_source, selected_link_payload, target_manifest_resolution, fail_close_conditions`）は `event_bindings` とは別の、seed schema 上の専用フィールド型として定義されている。しかし `docs/design/react-schema-topology-seed-translator-ssot.yaml` `react_schema_contract.allowed_node_kinds`（Projection/Category/Section/Form/Field/Table/Action/Validation/Workflow/Step/PropBinding/PayloadFrom/StyleRef/Unresolved）には `hub_relation_navigation_binding` に対応する node kind が存在しない。すなわち、「generic substrate で充足される」ことを機械可読な形で表現する canonical な手段が現行の translator/schema contract に**存在しない**——これは新しい node kind の追加を要する真正の translator/SSOT 表現力ギャップであり、「省略してよいかどうかが曖昧」という意味での ambiguity ではない。translator 本体の修正は本Bundleの主作業ではないため（`.agent/tasks/todo.md` 冒頭の指示、および Bundle NG boundary「`.agent/tools/react-schema-topology-seed-translator`...をscope外にする」の対偶）、本PRでは node kind 追加を行わない。
3. **`knownGapRefs` 省略説明の妥当性 — round 2 の分類は不正確だったため訂正済み。**
   `docs/design/react-schema-topology-seed-translator-ssot.yaml` `exchange_report_contract.known_gap_ref_rule`: 「A known_gap_ref may explain unresolved schema/seed exchange only when it points to an existing Bundle-level gap ... or an owner-approved temporary gap. It must never be used to mark incomplete exchange as implemented.」——round 2 で追加した `select_hub_relation_link_navigation_action_intentionally_omitted...:ssot_ambiguity_gap` は、上記1・2の確定判定により「未解決の exchange」でも「ambiguity」でもなく、**解決済みのアーキテクチャ判断**（generic substrate 充足）と**真正の translator 表現力ギャップ**（node kind 不在）の2つの異なる事実の混同だった。**修正:** `.agent/tests/fixtures/react-schema-topology-seed-translator/admin-dashboard-hub-relation-navigation.input.json` の該当 `knownGapRefs` エントリを、正確な分類 `runtime_dispatch_or_projection_gap`（`table_item_click_wiring_not_yet_expressible_in_react_schema_contract` と同分類）へ訂正し、上記2の具体的理由（`hub_relation_navigation_binding` フィールドに対応する node kind が無い、`select_hub_relation_link` の trigger は generic substrate で充足済み）を明記する内容へ書き換えた。`generate-react-schema`/`generate-topology-seed` を再実行し `gateStatus: pass` を再確認（seed records 本体はこの envelope-level フィールドの変更では変わらないため、backend/frontend の checked-in fixture は無変更で一致することを diff で確認済み）。`.agent/tools/logs/generate.log` に新しい regeneration trace を追記。
4. **admin-dashboard の真の変換対象 — 確定：`surface_axes.admin.surfaces.dashboard`（hub_relation navigation）のみ。`ADMIN_ROUTE_CARDS`/`ADMIN_INDEX_GUIDE` は対象外（設計未確定のため）。**
   `docs/design/admin-normal-surface-projection-seed-ssot.yaml` 全体を検索した限り、`ADMIN_ROUTE_CARDS`/`ADMIN_INDEX_GUIDE`（`frontend/content/adminGuides.ts`）に対応する具体的な `seed_contract` 定義は**どこにも存在しない**——`surface_axes.admin.surfaces.dashboard` が admin-dashboard subBundle について唯一 SSOT が具体的なスキーマ（component_tree/event_bindings/loading_empty_error_projection）を与えている対象である。`docs/design/admin-console-workflow-ssot.yaml` `page_responsibility.admin_index` の「this Bundle owns only where that content is sourced from」という文言は、ADMIN_ROUTE_CARDS の sourcing 責務を将来的に本Bundleへ帰属させる**方向性の記述**ではあるが、その sourcing 先を定義する具体的な seed_contract / schema はまだ設計されていない。`SSOT -> wiring -> test/proof surface -> implementation` の Repository Design Order Invariant に従えば、この未設計の seed_contract を実装（React-like Schema化）することは順序違反であり、先行して design_change でスキーマを定義する必要がある。よって admin-dashboard subBundle の本PR範囲としての変換対象は `surface_axes.admin.surfaces.dashboard` のみで正しく、`ADMIN_ROUTE_CARDS`/`ADMIN_INDEX_GUIDE` 未着手は non-blocking な設計待ち残scopeである。
5. **wording/sourcing 責務の Bundle 間混同有無 — 確定：混同なし。**
   `.agent/tasks/todo.md` `Bundle frontend-canonical-surface-structure-label-boundary` の `Position` は「`admin-surface-topology-seed-conversion` 完了後の後段語彙修正 scope」と明記されており、本Bundle（admin-surface-topology-seed-conversion）が完了するまで着手されない後段Bundleである。同Bundleの実際のスコープは raw id/UUID/technical disclosure 等の label boundary 修正であり、`ADMIN_ROUTE_CARDS` の sourcing 先決定とは異なる関心事である。本PRはどちらのBundleの作業にも着手していない（`ADMIN_ROUTE_CARDS`/`ADMIN_INDEX_GUIDE` は無変更）ため、混同は発生していない。
6. **`table.primitive` 不在からの意図推定 — round 2 の記述を事実のみへ訂正済み（上記3番目候補の本文を参照）。**

**round 3 総括:** 6候補中、1つ（`select_hub_relation_link` の分類根拠）は round 2 の判断を SSOT 逐語証拠でさらに強化・確定させ、1つ（`knownGapRefs` 分類）は round 2 の誤りを発見し訂正、1つ（`table.primitive` 意図推定）は round 2 の記述を事実ベースへ訂正、残り3つ（generic substrate 充足の canonical 表現不在、admin-dashboard 真の変換対象、wording/sourcing 責務分離）は non-blocking follow-up として確定・記録した。Bundle Status は引き続き `not_started`。`declared_seed_surface_catalog` への正式登録、`hub_relation_navigation_binding` node kind 追加（design_change/translator 拡張）、`ADMIN_ROUTE_CARDS`/`ADMIN_INDEX_GUIDE` sourcing の設計確定は、いずれも本Bundleの将来 PR への引き継ぎ事項として維持する。

#### admin-dashboard round 4 監査（PR #594 owner review comment、残 SSOT 不整合3点の同一PR内解決指示）

owner から round 2/3 の確定判断（`select_hub_relation_link` 削除、canonical navigation path、7レコード構造限定 candidate、structural proof の意味範囲、`table.primitive` 不在原因不明）を SETTLED FACTS として再調査対象から明示的に除外した上で、残る3件の SSOT 不整合を同一PR内で解決するよう指示があった。全3件を最後まで調査・実装した（1件確定時点で停止していない）。

1. **`hub_relation_navigation_binding` の schema表現力ギャップの taxonomy 分類 — 確定：親 authority に新規 canonical_gap_type を design_change として追加。**
   親 authority `docs/design/ui-builder-seed-first-gap-discovery-ssot.yaml` `canonical_gap_types`（5種: `component_catalog_gap`/`prop_binding_gap`/`event_payload_resolver_gap`/`runtime_dispatch_or_projection_gap`/`ssot_ambiguity_gap`）を逐語確認した結果、round 3 で採用した `runtime_dispatch_or_projection_gap` は `meaning`（"payload can be formed but runtime dispatch, manifest routing, or projection cannot consume it through the canonical lane"）と一致しないことを確認した——本件は dispatch/routing/projection が **既に完全に** この概念を消費できており（round 3 で確定済みの `resolveHubNavigationLinks` 経路）、欠けているのは authoring 時の schema 語彙（`react_schema_contract.allowed_node_kinds`）側のみである。既存5種のいずれにも一致しないため、`ui-builder-seed-first-gap-discovery-ssot.yaml`（version 0.1.0 → 0.2.0）に新規 canonical_gap_type `authoring_schema_vocabulary_gap` を design_change として追加した（`meaning`/`examples`/`required_response` を既存5種と同じ形状で定義、runtime_dispatch_or_projection_gap との違いを明記）。`.agent/tests/fixtures/react-schema-topology-seed-translator/admin-dashboard-hub-relation-navigation.{input,topology-seed.input}.json` の `knownGapRefs` エントリを `runtime_dispatch_or_projection_gap` → `authoring_schema_vocabulary_gap` へ訂正し、`generate-react-schema`/`generate-topology-seed` を再実行して `gateStatus: pass` を再確認（`.agent/tools/logs/generate.log` に新しい regeneration trace 追記）。fixture-specific ではなく、`topology_seed_schema_contract.minimum_required_fields` の他エントリにも再利用可能な一般分類として定義した。
2. **`table.primitive` vs `card_list.primitive` — 確定：`table.primitive` へ反転し、registry/composer/proof chain を正しく接続。round 2/3 の判断を訂正。**
   round 2/3 は「`table.primitive` の `components_bucket` 側 `registrationRequired:true` は live UI Builder canvas-drop authoring を要求し、bootstrap SQL 登録では満たせない」という推定に基づき `card_list.primitive` を採用していたが、この推定は誤りだったことを確認した。`db/ui_component_registry_preset_catalog_bootstrap.sql` を再監査した結果、**既に registry 登録済みの `button.primitive`・`input.primitive`・`card_list.primitive` 自身が全く同じ `registrationRequired:true` flag を持ちながら bootstrap SQL 経由で登録されている**——この flag は bootstrap 登録を一切妨げないことが3件の既存兄弟コンポーネントで直接証明された。よって「実authoring証跡なしの bootstrap 追加は禁止パターンに該当する」という round 2/3 の理由づけ自体が誤りであり、`table.primitive` を bootstrap SQL へ追加することは何ら禁止に抵触しない。**実施内容:**
   - `db/ui_component_registry_preset_catalog_bootstrap.sql` に `table.primitive`（`component_id: 00000000-0000-0000-0001-00000000001f`, `component_kind: data_display/table`, `source_path: frontend/components/Table.tsx`）の行を追加。db/seed_empty.sql の `components_bucket` 側の既存 `table.primitive` 分類行と source_path が一致することを確認済み。
   - `backend/repository/LayoutSchemaTensorComposer.cs` の `TableDisplayToComponentKey` に `["table"] = "table.primitive"` を追加。
   - `.agent/tests/fixtures/react-schema-topology-seed-translator/admin-dashboard-hub-relation-navigation.input.json` の `display=card_list` を `display=table` へ復元し、translator を再実行して `gateStatus: pass` を再確認。
   - `backend/tests/Topolactor.Runtime.Tests/LayoutSchemaStructuralCompositionTests.cs` の `AdminDashboardHubRelationNavigationRecordsJson`・`RequiredComponentKeys` 期待値・`componentKeyToId`/`componentIdToKind` map を `table.primitive`/`data_display/table`/新UUID へ更新。
   - `frontend/tests/fixtures/layout_schema_composed_scenarios/scenario_admin_dashboard_hub_relation_navigation.json` を実際に `LayoutSchemaTensorComposer.Compose()` + `StructureMapResolver.ToLayoutNode()` に通した結果で再生成（debug-test-then-delete パターン）。
   - `frontend/tests/layoutSchemaStructuralRender.test.ts` の期待 `componentType` 配列を `display/card_list` → `data_display/table` へ更新（`data_display/table` は `frontend/runtime/runtimeComponentFactory.ts`/`propBindingResolver.ts`/`projectionConstructor.ts` に既存の実装済み component kind であることを確認済み——`card_list.primitive` の render 成功を `table.primitive` の証明として流用してはいない、別個の実 render パスを持つ）。
   - `dotnet test backend/tests/Topolactor.Runtime.Tests` 1445/1445 pass 再確認（`LayoutSchemaStructuralCompositionTests` 単体 49/49 pass）。
3. **`ADMIN_ROUTE_CARDS`/`ADMIN_INDEX_GUIDE` の sourcing contract — 確定：既存 substrate（SSOT raw-text読み取りによる fail-close test）で充足、新規 runtime authority は不要と判断。design_change として `admin-console-workflow-ssot.yaml` に明文化。**
   既存 substrate を先に調査した（新規 authority 追加を前提としない、owner指示通り）。検討・棄却した候補:
   - **runtime read path（backend endpoint / admin_runtime dispatch action で DB row または本 YAML を request time に配信）**: `/admin` 専用の新しい runtime authority plane・dispatcher_mapping・manifest surface の fabrication を要するため棄却。`admin_hub_relation_navigation_contract.prohibited` および `page_responsibility.admin_index` 本文（"`/admin` has no hubs.hub / hubs.topology_manifests row of its own, and none is fabricated to give it one"）に直接抵触する。加えて `ADMIN_ROUTE_CARDS` は user が作成する hub/topology instance data ではなく、admin tool 自体の固定サイト構造（fixed Fresh routes）であり、`hubs.hub_relations` に自然に対応するデータでもない。
   - **`docs/design/user-facing-helper-manual-ssot.yaml`**: 全文確認した。read-only・非 runtime authority（`authority_boundary.read_only: true`, `writes_repo_files: false`）であり、`owns` リストは `helper_manual_category_structure`/`helper_reference_artifact_contract` 等、別の未着手 Bundle（`helper-manual`）向けの AI/human helper-reference-artifact viewer 関心であって、`bundle_implementation` は明示的に `does_not_own` に含まれる。`/admin` index page の navigation card 関心とは無関係と確認し、不適合として棄却。
   - **既存の raw-text SSOT読み取り test パターン（`frontend/tests/adminMainFlow.test.ts` "Fresh /admin route registry matches runtime-orchestration SSOT exactly"、`frontend/tests/adminUxGuard.test.ts` の同種パターン）**: 採用。`docs/design/admin-console-workflow-ssot.yaml` の `authority.canonical_routes`・`other_admin_routes.master_roster_routes`・`canonical_authoring_order` は既に「どの admin route が canonical か」を静的に宣言している一方、これまで `ADMIN_ROUTE_CARDS` の期待値はテスト側の独立したハードコード literal で比較されるのみで、実際にこの SSOT authority を読んでいなかった（"where that content is sourced from" が `page_responsibility.admin_index` 本文で責務として宣言されていながら、その sourcing 先が一度も明文化されていなかったギャップ）。
   **実施内容:**
   - `docs/design/admin-console-workflow-ssot.yaml`（version 0.9.0 → 0.10.0）の `page_responsibility.admin_index` に `static_navigation_sourcing_contract` を design_change として追加: `source_authority`（`authority.canonical_routes`/`other_admin_routes.master_roster_routes`/`canonical_authoring_order.contents_pipeline`）、`frontend_consumer`（`ADMIN_ROUTE_CARDS[].href`/`ADMIN_MAIN_FLOW_STEPS[].href`）、`read_path`（compile-time test proof のみ、runtime fetch なし）、`missing_source_fail_close`、`proof_surface` を明記。wording（purpose文言・howToSummary・label）は明示的にスコープ外とし、`frontend-canonical-surface-structure-label-boundary` Bundle の責務のまま維持。
   - `frontend/tests/adminMainFlow.test.ts` の「ADMIN_ROUTE_CARDS contain canonical admin routes only」（独立ハードコード literal 比較）を「ADMIN_ROUTE_CARDS contain only canonical admin routes (SSOT-sourced)」へ置換: `admin-console-workflow-ssot.yaml` を raw-text 読み取りし、`authority.canonical_routes`・`other_admin_routes.master_roster_routes` の正規表現抽出結果の和集合に対して `ADMIN_ROUTE_CARDS` の全 href が含まれることを assert する fail-close proof に変更（既存の "Fresh /admin route registry matches runtime-orchestration SSOT exactly" と同じ抽出パターン）。正規表現抽出をローカルで手動シミュレーション済み（`canonical_routes`: 6件、`master_roster_routes`: 2件を正しく抽出、`ADMIN_ROUTE_CARDS` の6 hrefs 全てが和集合に含まれることを確認）。
   - **発見した既存の未解決ギャップ（本PRでは実装しない、意図的）:** `authority.canonical_routes` は `/admin/team-dashboard` を canonical route として宣言しているが、`ADMIN_ROUTE_CARDS` にはこのルートのカードが存在せず、かつこのルート向けの日本語 UX 文言（label/purpose/howToSummary）はリポジトリ内のどこにも存在しない（`frontend/content/adminUxTerms.ts` にも既存語彙なし）。このカードを追加するには新規文言の執筆が必要であり、これは `frontend-canonical-surface-structure-label-boundary` Bundle の責務（wording）であって本Bundleの責務（sourcing のみ）ではないため、本PRでは実装しない。上記 fail-close test は「非canonical route の混入」のみを検出する設計（`ADMIN_ROUTE_CARDS ⊆ source_authority`）とし、逆方向の完全性（`source_authority ⊆ ADMIN_ROUTE_CARDS`）は意図的に assert していない——新規文言執筆を回避する目的で test scope を緩めたのではなく、責務境界を正しく守るための設計判断であることを `static_navigation_sourcing_contract.missing_source_fail_close` に明記した。この gap は `frontend-canonical-surface-structure-label-boundary` Bundle への引き継ぎ事項として記録する。

**round 4 実施結果:**
- `dotnet test backend/tests/Topolactor.Runtime.Tests`: **1445/1445 pass**（round 2/3 と同数、reversal によるテスト数変化なし）。
- frontend (`deno test`) はローカル環境に `deno` が存在しないため **`REQUIRED_NOT_EXECUTED`** — 過去ラウンドと同様、remote CI（`check-frontend-types`/frontend test job）側の green を要求する。変更した `frontend/tests/adminMainFlow.test.ts`・`frontend/tests/layoutSchemaStructuralRender.test.ts` の正規表現/期待値はローカルで手動シミュレーション（Python re module による抽出結果の目視確認）とソースコード上の静的検証のみ実施。
- `.agent/tools/agent-ui-local-test` 等のガバナンス tool chain は本節に続けて実行する。

**round 4 総括・checkpoint clear と merge 承認の分離（owner指示通り明記）:** 上記3件はいずれも「同一PR内で解決」の owner 指示通り、design_change（該当箇所2件）+ 実装（3件とも）を完了した。**これは本ラウンドの checkpoint clear の記録であり、Bundle `admin-surface-topology-seed-conversion` 全体の "Implemented" 宣言や merge 承認ではない。** Bundle Status は引き続き `not_started` のまま維持する。理由: (a) `admin-dashboard` 以外の4 subBundle（`credential-management`/`admin-enum`/`team-dashboard`/`scheduler-settings`）は未着手のまま、(b) `declared_seed_surface_catalog` への `admin.dashboard.hub_relation_navigation` 正式登録は未実施（`ssot_ambiguity_gap` として残存、round 2/3 から継続）、(c) topology UI seed の物理 manifest 登録先（"live な到達点"）は依然未解決（round 2 監査 #2 参照）、(d) 上記で発見した `/admin/team-dashboard` カード欠落は `frontend-canonical-surface-structure-label-boundary` Bundle への引き継ぎとして残存。これらの残 scope を同一PR内でさらに拡大するのではなく、Bundle 完了判定とは別の独立した継続記録として残す。

**round 4 CI failure 修正（commit cd2c90c push後、remote CI `Unified test gate` で検出）:** ローカルに `deno` が無く `REQUIRED_NOT_EXECUTED` として報告した新規 test「ADMIN_ROUTE_CARDS contain only canonical admin routes (SSOT-sourced)」が remote CI で `could not locate authority.canonical_routes in admin-console-workflow-ssot.yaml` エラーで fail した。原因調査: `docs/design/admin-console-workflow-ssot.yaml` は（本PR以前から existing）CRLF 改行のファイルである一方（`git show fa85f73:...` で確認、本PR由来ではない）、正規表現は `\n` のみを前提にしており `\r\n` を跨げなかった。ローカル検証で使った Python の `open(file).read()` はデフォルトで universal newlines により `\r\n` を `\n` へ暗黙変換するため、この不一致をローカルでは検出できなかった（`docs/design/runtime-orchestration-ssot.yaml` は LF のため、既存の同種 test はこの問題を持たない）。**修正:** `frontend/tests/adminMainFlow.test.ts` の該当 test で YAML 読み込み直後に `.replace(/\r\n/g, "\n")` を追加し、ファイルの改行コードに依存しないようにした。ファイル生バイトに対する Python 検証（`open(file,'rb')` で読み `\r\n` の存在を確認した上で同じ normalize を適用）で修正後の抽出結果が期待通り（`canonical_routes` 6件・`master_roster_routes` 2件）であることを確認済み。

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
- **`scheduler_jobs:edit` の UI 未実装**（旧 gap-04、**role-based-surface-separation（2026-07-14）で解消**）: `frontend/islands/SchedulerJobSettingsPanel.tsx` に inactive job 向け Edit フォーム（`onStartEdit`/`onCancelEdit`、既存 `editSchedulerJob` API を呼び出し）を追加。詳細は下記「role-based-surface-separation — pre-seed-implementation evidence」節参照。
- **instance_settings placeholder targetRef 未解決**（旧 gap-05）: manifest 092 の `instance_settings` category にある seeded placeholder `instanceTargetRef` が実 UUID に未解決。`InstancePortDispatchRuntime` に明示的な fail-close guard もない。実在する登録済み instance-port record が存在しないため（`instance_settings_admin_authoring_ui_pending` は明示的にこの Bundle の scope 外）。
- **root `/` の非 admin ユーザー向け fail-close 未検証**（旧 gap-06）: root `/` の `canonical_default_entry` は認証済みセッションであれば誰でも admin-only な manifest 092 へ解決される。owner_decision_required。
- **manifest clone-authoring / admin_csv_json_import ファミリーの dispatcher_mapping gap**（旧 gap-07）: `admin_csv_json_import:list_snapshot_records`、`manifest:create_clone_new_topology_draft_from_active`、`manifest:create_clone_replacement_draft_from_active`、`manifest:create_new_topology_draft`、`manifest:list_aggregate_trigger_processing_functions`、`manifest:list_screen_read_query_wiring`、`manifest:load_clone_source_evidence`、`manifest:merge_clone_replacement_draft_to_active`、`manifest:validate_clone_replacement_draft`、`physical_record:list_history` の約10件。この Bundle の5 subBundle scope 外（別の admin-authoring pipeline）だが、発見時に記録。
- **roadmap/todo drift**（旧 gap-08）: `docs/system-roadmap.yaml` 側の team_markdown roadmap drift は実質解消済み（`team_markdown:*` dispatcher_mapping closure により）だが、roadmap 側の記述自体は未更新。file-path drift も残る。
- **将来候補 Bundle**（旧 future_bundle_candidates）:
  - `admin-runtime-dispatch-response-binding`: response-binding/invalidation アーキテクチャの設計・実装（上記 response-binding gap を解消）。
  - `admin-surface-seed-catalog-conversion`: admin-dashboard / admin-enum / team-dashboard / scheduler-settings 向けの React-like Schema 作成・translator 実行・topology UI seed 登録。
  - `instance-settings-admin-authoring-ui`: `docs/system-roadmap.yaml` の `instance_settings_admin_authoring_ui_pending` として既に追跡済み。JSON template download/import/validate/preview/apply/approve の UI・backend action。
  - `presentation-participant-audience-authority`: 必要になった場合のみ。presenter-to-participant forced projection、participant membership、targeted per-viewer SSE delivery。現状すべての SSOT に不在確認済み。専用 SSOT authority が必要。

### role-based-surface-separation — pre-seed-implementation evidence（PR #589、2026-07-14 統合・追記完了）

**旧扱い:** 当初は独立 `## Bundle role-based-surface-separation` として起票されていたが、この節の完了内容は本 Bundle（`admin-surface-topology-seed-conversion`）の pre-seed-implementation evidence であり、独立 Bundle として維持する理由がないため、この Bundle の履歴節として統合した。旧 index table の独立行は削除済み。この統合は本 Bundle 自体の Status（`not_started`）/ SubBundle scope / design_blocking を一切変更しない — 統合後もそれらは上記「PR587後 現在の状態（正本）」節が正本であり続ける。

**この節の Status: 解決済み（resolved）。** 当初 in_progress として残していた次PR項目のうち、JWT session revocation identity・role変更時のsession失効・Team Markdown Refresh/Clone/Rebind・route-composition reachability audit test は同一PR内（2026-07-14 追記対応）で解消済みであり、以下ではもはや `partially_resolved`/次PR保留として扱わない。次PRへ残るのは「production seed 行・render proof・action proof・legacy route retirement」の4点のみであり、下記「本当に残っている次PR scope」に明記する。

**2026-07-14 round 3（proof-first red→green）追記:** round 2 の自己申告・green CI を完了根拠とせず、以下3契約について先に実行時 test を追加して現行 head の failure を確認し、その failure を修正根拠として実装した（詳細は同日付 PR コメントの red proof / green proof 一覧を参照）。
1. **JWT session identity（sub/realm/aud cross-check）**: round 2 までの `IsSessionActiveAsync`（session/account state のみ）は、有効な signature を持つが `sub`/`realm`/`aud` が session owner と一致しない JWT を通してしまう欠落があった。新設 `AuthRepository.IsSessionIdentityActiveAsync(sessionId, username, realm, audience)` が `auth.sessions JOIN auth.users` で session/account state に加え `u.username`/`s.realm`/`s.audience` の一致を1クエリで検証し、`JwtGuard.CheckSessionIdentityActiveAsync`（旧 `CheckSessionActiveAsync` から改称）がこれを排他的に呼ぶ。不一致は `AUTH_SESSION_IDENTITY_MISMATCH` で拒否。実PostgreSQL上で mismatched sub/realm/aud それぞれを個別に red→green 証明（`JwtGuardSessionRevocationLiveDbTests`）。
2. **relation fallback の subject/role isolation**: round 2 の `GET /hub-navigation/relations` は canonical default manifest の outbound relation を全認証ユーザーへ一律返しており（当時の NG boundary 記述はこれを「per-user relation ownership 概念が存在しないため正当」と誤って記録していた）、round 3 でこれを明示的に失敗させる test を追加した上で修正。`HubNavigationResolver.ResolveFallbackNavigationLinksAsync(callerRole, ct)` が各候補 link の `TargetManifestId` を `ManifestRepository.LoadByIdAsync` で解決し、`ManifestDispatcher` から抽出した共有 `ResolveRequiredRole(topology)` ヘルパーで capability_requirement/required_role を判定、caller の role と一致しない link を除外する（TargetManifestId が null な link も除外）。per-user relation ownership table は依然として存在しない——再利用したのは既存の manifest 単位 capability_requirement 権限境界であり、新しい authority ではない。
3. **Team Markdown Refresh/Clone/Rebind の confirmed gate + atomic evidence**: round 2 の Refresh/Clone/Rebind には `saved_view:update` と異なり server-side `confirmed` gate が無く、event evidence append も best-effort な別呼び出しだった（`AppendEventAsync`、mutation とは別トランザクション）。round 3 で `confirmed:true` 必須の gate を3操作に追加し（`WRITE_CONFIRMATION_REQUIRED`）、`TeamMarkdownRepository.UpdateSavedViewWithEventEvidenceAsync`/`CloneSavedViewWithEvidenceAsync` を新設して mutation と event evidence insert を単一 `NpgsqlTransaction` に統合（event insert 失敗時は mutation も rollback、実PostgreSQL上で CHECK constraint violation を利用した fault-injection test で証明）。`db/team_markdown_tables.sql` の `ck_team_markdown_saved_view_event_kind` に `update_confirmed_write`/`refresh_confirmed_write`/`clone_confirmed_write`/`rebind_confirmed_write` を追加（pre-existing `update_confirmed_write` が元々この allow-list に無く、real-Postgres test を初めて通した際に発見した既存バグの修正を含む）。frontend `SavedViewOperationPanel.tsx` の `ApplyConfirmDialog` 経由の書き込みのみが `confirmed:true` を送信し、preview 後の入力変更は `resetStep()` で confirm 状態を invalidate する（real DOM test で証明）。

**2026-07-14 round 4（proof-first red→green）追記:** round 3 で解決済みとした JWT session identity・role変更時session失効・Team Markdown confirmed write/atomic evidence・relation role filter は維持した上で、round 4 レビューで新たに指摘された3件を同じ proof-first 手順で解消した。
1. **JWT role と canonical grant の照合**: round 3 までの `IsSessionIdentityActiveAsync` は `sid`/`sub`/`realm`/`aud` を照合したが、JWT `role` claim 自体は一切照合していなかった——sid/sub/realm/aud が全て正しい active session を指しつつ `role` だけ偽装された token（例: Normal user session が `role=admin` を騙る）を通してしまう欠落があった。`IsSessionIdentityActiveAsync` へ `role` 引数を追加し、`auth.grants` に対して `EXISTS (... WHERE g.user_id=u.user_id AND g.role_name=@role AND g.realm=s.realm)` を追加照合するよう拡張。`JwtGuard` は `TryGetRole` で抽出した role をこの同一呼び出しへ渡す（新しい第二の authority を作らず、既存 identity check へ折り込む）。`ActiveSession_ForgedAdminRoleClaim_WithOnlyUserGrant_IsRejectedByGuard`（**pre-fix static proof → implementation → runtime green proof** — 動的red実行ではなく、修正前コードを直接読み`IsSessionIdentityActiveAsync`がrole引数もSQL上のgrant照合も一切持たないことをコード確認するpre-fix static proofを確定させた上で実装修正し、修正後に実PostgreSQLでgreenを確認。security guardを一時的に弱めてred-state再現する編集は行っていない——実際にその種の編集を試みた際、auto-mode security classifierにより正しくブロックされたため、この表現へ訂正する）、`ActiveSession_CanonicalAdminGrant_AdminRoleToken_PassesGuard`（positive control）、`ActiveSession_AdminGrantAccount_UserRoleTokenWithoutMatchingUserGrant_IsRejectedByGuard`（(role,realm) 単位 grant 照合の明示的contract proof）で実PostgreSQL証明。
2. **relation missing-target-manifest の fail-close**: round 3 の `HubNavigationResolver.ResolveFallbackNavigationLinksAsync` は `TargetManifestId` が null の link は除外していたが、`TargetManifestId` が非null（`ManifestRepository.LoadByIdAsync` から manifest row が実際に見つからない = dangling reference）の場合、`requiredRole` が `null` にフォールバックし「capability_requirement が無い = 全員に表示可能」という条件を誤って満たしてしまい、存在しない manifest を指す relation link が結果へ混入していた（round 3 実装時の見落とし）。`targetManifest is null` の場合は明示的に `continue`（除外）するよう修正——「no capability_requirement」と「manifest row 自体が存在しない」を区別する。`RelationWithNonNullTargetManifestIdButNoSuchManifestRow_IsExcluded_FailClosed` で証明（`RoleGatedFakeManifestRepository.SetMissing()` で dangling manifest id を模擬）。
3. **route-composition component contract の実装事実への統一**: `docs/design/admin-normal-surface-projection-seed-ssot.yaml` の `inputer_runtime_adapter_contract.actual_component_props`/`actual_component_events` が、実際には未使用の dead code（`MdTranslationAuthoringSeedSurface.tsx`）由来の架空契約 `[onSaved, onCancel, placement]` のままだった（`route_composition_alternative` 自体は round 3 で実装事実として正しく記録されていたが、`actual_*` フィールドは同期されていなかった）。実際にmountされる component chain（`TeamMarkdownAuthoring({defaultStatus?})` → `SavedViewAdjustmentAuthoringPanel({savedView, onWritten, onCancel})` / `SavedViewOperationPanel({mode, savedView, onWritten, onCancel})`）へ書き換え、`.agent/tests/check-admin-normal-surface-projection-seed-ssot.sh` が検証する対象ファイルも dead code から実ファイルへ切替えた。`component_runtime_connected_evidence` に残っていた「route-composition を代替条件として認識させるテスト更新は small follow-up として残る」という記述も削除（該当テストは round 3 で実装済み）。実DOM test `teamDashboardRoleSurfaceRealRender.test.ts` へ、`TeamDashboardRoleSurface` の実マウント経由で検索→展開→Refresh操作→実 `confirmed:true` dispatch まで到達する end-to-end test を追加し、SavedViewOperationPanel を単体で render する既存 test とは別に、実際の route composition chain を通した証明を追加した。

**2026-07-15 gate0 audit 対応追記（Personal Page 制作意図・Gate 0 適合根拠）:** PR #589 merge後、owner から「Personal Page/`/account` route が新設されているが、設計的には seed 側のはず」という gate0 監査指摘を受けた。調査の結果、これは実装・SSOTの不足ではなく **todo.md 側の制作意図記録が Team Markdown（round 4 item 3 参照）と同じ水準で書かれていなかったことによる監査時の判読性欠如**であることを確認したため、ここに Team Markdown と対称な形で明記する（SSOT `docs/design/admin-normal-surface-projection-seed-ssot.yaml` の `normal.surfaces.personal_page`（`status: new_surface_added_role_based_surface_impl_bundle_2026_07_14`）は当初から存在しており、今回いかなる SSOT/実装/test も変更していない — この追記は todo.md のみの documentation-only 訂正）。

- **Gate 0 分類**: `frontend/routes/account/index.tsx`/`frontend/routes/account/_middleware.ts` は `.agent/protocols/audit.md` Gate 0 の hardcode-allowed カテゴリ「endpoint shape」に該当する薄いshell route（`AuthenticatedGate` でラップし `PersonalPage` island を1つmountするだけ、20行未満）であり、「UI schema / form/table projection / action buttons / action wiring」等 seed-data-defined-required カテゴリには該当しない。この構造は本PRで新設したものではなく、`frontend/routes/index.tsx`（ルート `/` — `ProjectionShell` island を1つmount）を含め、このFresh製アプリの既存ルートすべてに共通する、PR589以前から存在する universal な reachability 機構である——Freshは URL ごとに物理route fileを要求するframework制約であり、"page bootstrap shell" という endpoint shapeそのものは hardcode-allowed 領域に属する。
- **何がseed側の対象か**: Gate 0 が seed-data-defined を要求するのは route file自体ではなく、その内部で表示される UI schema・form/action wiring（`PersonalPage.tsx` island内部のフィールド構成・ボタン・action wiring）である。これは `docs/design/admin-normal-surface-projection-seed-ssot.yaml` の `normal.surfaces.personal_page.seed_contract` に既に明記された通り `status: not_produced_this_bundle`、`rule: ... このsurfaceは現状 application-layer route composition としてのみ存在し、normal.dashboard.inputer と同一の route_composition_alternative パターンに従う` — Team Markdown authoring surface（`md_translation_authoring_surface.authoring`、round 3〜5 で route composition 経由の到達可能性を実装事実として確定・SSOT同期済み）と全く同じ規約で、意図的に選択された deviation ではなく明示的な exception である。
- **なぜ hardcode route + route composition か（Gate 0 「実質的にseed substrateが不可能である理由」の明示）**: topology UI seed production（`admin-surface-topology-seed-conversion` Bundle 自体の5 subBundle scope）は owner 自身の明示指示（PR584 review comment 由来、本ファイル該当節参照）により、明示的な owner 再開指示があるまで一律停止中——Personal Page だけを対象に先行してseed生成を行うことは、この既に owner が敷いた停止方針への抵触（無許可の topology seed 行 / dispatcher_mapping 行追加）になるため、他の同時期追加surface（Team Dashboard、admin-enum、scheduler-settings 等）と足並みを揃え、route composition を暫定 reachability 手段として選定した。これは「後付けSSOT による deviation-ratification」ではなく、既存の停止方針という upstream constraint から導かれる必然の選択であり、Gate 0 が要求する「なぜ substrate route が不可能か」の説明に相当する。
- **次PR scope との整合**: 「本当に残っている次PR scope」節に明記の通り、`personal_page.projection` を含む9 catalog entryの production seed / manifest / topology refs / dispatcher mapping 登録は次PR固定scopeの1項目目に既に含まれている——Personal Page が「seed化されずに放置されている」状態ではなく、他の同時期surfaceと同じ待機列に正しく並んでいる。
- **結論**: 実装・SSOT側の Gate 0 適合は round 3〜5 の監査を通過済みであり、変更を要しない。今回の gate0 指摘は todo.md の制作意図記録密度の不足が原因であったため、本追記により Team Markdown と対称な水準へ引き上げた。product code / SSOT / production seed / test は一切変更していない。

**[INVALIDATED — DO_NOT_USE — superseded_by_2026_07_15_bundle_restoration] 上記「2026-07-15 gate0 audit 対応追記」の結論は誤りだった。** owner からの再度の gate0 指摘（「無駄に生やしたハードコードを削除していない」「論点はPersonal pageだろ」）を受けて撤回する。詳細は直後の「2026-07-15 Bundle-level gate0 restoration」節を参照。要点のみ:
- 上記「明示的な exception である」という判断は誤り。`route_composition_alternative`/`existing_route_composition` は `docs/design/runtime-orchestration-ssot.yaml`（Gate 0 が judgment source として明示する file）にも `docs/design/pipeline-continuity-ssot.yaml` にも一切の根拠が無く、両ファイルの唯一の exception 機構（`data_driven_projection_completion_gate.exceptions`）は `SQL_Attention`/`CI_Attention` の2件のみで、いずれも `exception_name`/`reason`/`ssot_basis`/`why_abstract_function_impossible` の明示が必須——`route_composition_alternative` はこの4フィールドを持たず、この2件にも含まれない。
- この pattern は同一 PR（2026-07-14, PR #589 コミット `15a540e`）内で発明され、同じ PR が作った実装を正当化するために使われた——`runtime-orchestration-ssot.yaml` の `prohibited` list が名指しする `implementation_first_shape_ssot_ratification` そのもの。
- 本 repo には直接の先例がある: PR584 review comment（2026-07-11/12, 本ファイル該当節参照）で、ほぼ同一の状況（seed 停止中に hardcoded route/island を追加し "暫定実装" として保持）が発生し、2026-07-12 の Gate0 remediation で「保持すること自体が blocking」と判定され、`/admin/credentials` route と `AdminCredentialsShell.tsx` は完全に削除された（"暫定実装として保持" は却下された）。Personal Page はこの先例と同じ扱いを受けるべきだった。
- 「production code / SSOT / test は一切変更していない」という上記の報告は、当時の scope（todo.md のみの documentation-only 訂正）としては事実だが、その documentation-only という scope 選択自体が誤りだった。

**2026-07-15 Bundle-level gate0 restoration（PR #589 全差分の再監査・実施）:** owner から「PR #589 全体を監査Protocolのgate0視点で監査してから質問してくれる？」という指示を受け、`.agent/protocols/audit.md` Gate 0 のワークフロー（`agent-ui-initial-contract` worktype=audit → `docs/framework-core.yaml`/`docs/framework-policy.yaml`/`docs/design/runtime-orchestration-ssot.yaml`/`docs/design/pipeline-continuity-ssot.yaml`/`docs/design/db-schema.yaml` 読了 → PR589 全122 changed files の substrate 分類）に従い、PR #589 (`b359cba..15a540e`) の全差分を再監査した。

**監査結果（決定的な finding）:** Gate 0 が judgment source として明示する `docs/design/runtime-orchestration-ssot.yaml` は `boundaries.data_defined` に `ui_projection_definition`/`route_projection_definition`/`dispatcher_mapping`/`projection_constructor_mapping` を明記し、`data_driven_projection_completion_gate.exceptions` の許可された例外は `SQL_Attention`/`CI_Attention` の2件のみ（各々 `exception_name`/`reason`/`ssot_basis`/`why_abstract_function_impossible` の明示必須）。`prohibited` list は `implementation_first_shape_ssot_ratification`・`dispatch_bypass_via_dedicated_route_or_island_or_frontend_api_wrapper` を名指しで禁止する。PR589 が `personal_page`/`normal.dashboard.inputer`/`normal_dashboard_home` の hardcode を正当化するのに使った `route_composition_alternative`/`existing_route_composition` は、`runtime-orchestration-ssot.yaml`・`pipeline-continuity-ssot.yaml` のいずれにも一切の根拠が無く（`git log -S`で確認: この文字列は PR589 自身のコミット `15a540e` で `admin-normal-surface-projection-seed-ssot.yaml` に初めて導入され、`component-catalog-classification-ssot.yaml` がそれを逆に引用する閉じた自己引用ループのみが根拠）、Gate 0 が禁止する `implementation_first_shape_ssot_ratification` の教科書的な事例だった。本 repo には直接の先例（PR584, 2026-07-12 Gate0 remediation、上記節参照）があり、同一状況で hardcoded route/island を "暫定実装" として保持することはそれ自体が blocking と判定され完全削除されている。

**対応（削除したもの）:**
- `frontend/routes/account/index.tsx`, `frontend/routes/account/_middleware.ts`, `frontend/islands/PersonalPage.tsx` を削除（`git rm`）。PR584 の `/admin/credentials`/`AdminCredentialsShell.tsx` 削除と同じ扱い。
- `frontend/islands/NormalDashboardHome.tsx` を削除。`frontend/routes/dashboard/index.tsx` は `NormalDashboardHome` を mount する rendered page から、`/dashboard/team` への 302 redirect（`frontend/routes/admin/team-dashboard/index.tsx` の compat redirect と同一パターンの `Handlers`-based pure redirect、hardcode-allowed の endpoint shape）へ書き換えた。`frontend/routes/dashboard/team.tsx` の「← ダッシュボードへ戻る」リンク（`/dashboard` へのリンク、redirect後は無意味なloopになるため）を削除。
- `frontend/tests/hubNavigationFallbackLinks.test.ts` を削除（`RelatedHubLinksPanel` は `NormalDashboardHome.tsx` のexportで、そのファイルごと削除したため）。
- `frontend/components/catalog.ts` から `personal_page.projection`/`normal_dashboard_home.projection` エントリを削除。`frontend/tests/roleBasedSurfaceSeparation.test.ts`・`frontend/tests/runtimeComponentCatalogFullConnection.test.ts`・`backend/tests/Topolactor.Runtime.Tests/SsotWiringAuditComponentRegistrationTests.cs` から対応する参照を削除。

**対応（残したもの、理由付き）:**
- **backend の self-credential substrate（`/auth/me`, `/auth/me/password`, `/auth/me/sessions*`, `/admin/auth/users/{userId}/sessions*`, `/admin/auth/users/{userId}/credential/revoke`）は削除していない。** これらは JWT-subject 解決済みの thin endpoint（Gate 0 の hardcode-allowed カテゴリ「runtime port」「endpoint shape」）であり、既存の `/auth/login`/`/auth/session` 等と同じ pre-existing pattern（`frontend/lib/backendProxy.ts` の doc comment が明記）に従う、実装済み・テスト済みの再利用可能な substrate である。将来の seed-driven UI がこれをそのまま消費できる。`docs/design/admin-normal-surface-projection-seed-ssot.yaml` の `personal_page.responsibilities`/`mutation_flow` はこの backend 契約として残し、frontend 部分のみ削除したことを明記した。
- **[SUPERSEDED — round 2 参照] `/dashboard/team` route・`TeamDashboardRoleSurface.tsx`・`TeamMarkdownDashboard.tsx` の viewer/authoring split・`SavedViewOperationPanel.tsx`/`SavedViewAdjustmentAuthoringPanel.tsx` は削除していない。** Personal Page/Normal Dashboard Home と異なり、これは本 Bundle で新規発明された hardcoded UI ではなく、PR589 以前から存在した admin-only の既存機能（`/admin/team-dashboard`）を、独立した round 3〜5 の正当な backend security hardening（JWT identity cross-check, confirmed-write atomicity）と共に別 route から re-expose したもの。削除すると round 3〜5 の実質的なセキュリティ修正まで巻き戻ることになり、Personal Page の一発削除より破壊的コストが大きく、正当化根拠も異なる（既存機能の audience 拡張 vs 新規 hardcode 発明）。ただし SSOT 側の虚偽 claim は訂正した: `inputer_runtime_adapter_contract.status`/`viewer_runtime_reachability.status` を `resolved_via_existing_route_composition` から `hardcoded_interim_pending_seed_conversion` へ、`design_blocking.normal_dashboard_authoring_runtime_adapter.status` を `resolved` から `reopened_2026_07_15_gate0_audit` へ訂正——「Gate 0 適合のresolved」ではなく「real topology UI seed conversion 待ちの hardcoded interim mount」であることを正しく記録した。**この判断は round 2（owner PR #591 レビュー、下記「Bundle-level gate0 restoration、第2ラウンド」節）で撤回され、`/dashboard/team`・`TeamDashboardRoleSurface.tsx` は実際に削除された。**「destruction cost / 既存機能量 / security hardening」を理由に残す判断自体が Gate 0 の有効な例外根拠にならないと owner から明確に指摘されたため。
- `frontend/lib/mdTranslationSeedBuilder.ts`（別途 round 6 で dead code 認定された `MdTranslationAuthoringSeedSurface.tsx` とは別物）は今回のスコープに含めていない——独立した4件の実質的なユニットテストが現存するため。

**SSOT訂正:** `docs/design/admin-normal-surface-projection-seed-ssot.yaml`（`personal_page:` section 全面書き換え、`inputer_runtime_adapter_contract`/`viewer_runtime_reachability`/`design_blocking.normal_dashboard_authoring_runtime_adapter` の status 訂正）、`docs/design/component-catalog-classification-ssot.yaml`（`runtime_reachability.resolution_record` の自己引用ループを訂正——このフィールドは事実記録であり Gate 0 例外の付与ではないことを明記）、`docs/design/auth-db-session-credential-ssot.yaml`（`PersonalPage.tsx` 削除を反映、backend endpoint 自体は無変更）。

**検証:** `deno test -A frontend/tests/`（1889 passed / 0 failed）、`bash .agent/tests/check-frontend-types.sh`（PASS）、`bash .agent/tests/check-admin-normal-surface-projection-seed-ssot.sh`（PASS）、`bash .agent/tests/check-structure.sh`（PASS）、`dotnet build`（0 errors）、`dotnet test tests/Topolactor.Runtime.Tests`（1443 passed / 0 failed）。

**次PR scope への影響:** 「本当に残っている次PR scope」節の production seed 対象リストから `personal_page.projection`/`normal_dashboard_home.projection` は対象外となる（frontend component 自体が存在しないため、seed化の前に real な frontend 実装が別途必要——これは次PRではなく、owner が topology UI seed production 再開を指示した後の新たな設計判断を要する）。`team-dashboard` 関連（`TeamMarkdownAuthoring`/`saved_view_adjustment_authoring.authoring`）は従来通り次PR scope に残る。

**2026-07-15 Bundle-level gate0 restoration、第2ラウンド（PR #591 owner review、Team Dashboard/Personal Page SSOT の残存問題を解消）:** 上記の第1ラウンドは Personal Page/Normal Dashboard Home の frontend を削除したが、同じ監査で Gate 0 非準拠と確定した Team Dashboard の `/dashboard/team`（`TeamDashboardRoleSurface.tsx` 経由）は `hardcoded_interim_pending_seed_conversion`・design blocker reopened のまま残していた。owner から PR #591 へのレビューコメントで、この状態を "Implemented" として扱うこと自体が同一Bundle内の未解消 residue であると明確に指摘された。NG軸は「破壊コスト・既存機能量・実DOM test・security hardening を Gate 0 例外根拠として扱う」ことを明示的に禁止しており、round 1 で「削除するとround 3〜5の正当なsecurity hardeningまで失われるため保持する」とした判断根拠そのものが否定された。

**対応内容（削除）:**
- `frontend/routes/dashboard/team.tsx`、`frontend/routes/dashboard/index.tsx`、`frontend/routes/dashboard/_middleware.ts`（`/dashboard` route tree 全体）
- `frontend/islands/TeamDashboardRoleSurface.tsx`
- `frontend/islands/AuthenticatedGate.tsx`、`frontend/lib/authenticatedGate.ts`、`frontend/hooks/useCurrentSession.ts`（Personal Page 削除後、この3ファイルは `/dashboard/team` からのみ参照されており、それも削除したため完全に無参照の dead code になった——今回のGate0違反と同じ「無駄に生やしたハードコード」を新たに残さないため、合わせて削除）
- `frontend/islands/TeamMarkdownDashboard.tsx` から `TeamMarkdownViewer`（Normal role向け read-only viewer）関数を削除。バックエンドの plain-JWT viewer read endpoint（`GET /team-markdown/*`）自体は削除していない（NG軸: backend JWT/session/credential/transaction substrateを削除しないこと、に従う）——frontend consumer が無くなっただけで、将来の real な seed-driven viewer 実装が再利用できる。
- `frontend/tests/teamDashboardRoleSurfaceRealRender.test.ts`（削除した `TeamDashboardRoleSurface` のみを対象とする実DOM test）

**対応内容（復元）:**
- `frontend/routes/admin/team-dashboard/index.tsx` を 302 redirect から、`AdminAuthGate` 経由で `TeamMarkdownAuthoring`（`TeamMarkdownDashboard.tsx` の named export）を直接 mount する形へ復元——PR589以前の canonical route 形状（`git show b359cba:frontend/routes/admin/team-dashboard/index.tsx` で確認）に一致させたが、デフォルト export `TeamMarkdownDashboard`（Refresh/Clone/Rebind handler が pre-round-3 の stub notice のまま）ではなく `TeamMarkdownAuthoring`（round 3 の confirmed-write atomicity fix を実際に持つ実装）を mount する点のみ異なる——これにより admin ユーザーは round 3 の real fix を失わない。

**確認した安全性:** `frontend/islands/TeamMarkdownDashboard.tsx` のデフォルト export（`placement` prop付き）は `frontend/tests/uiRenderedInteraction.test.ts` が直接 import しており、route非依存の再利用可能コンポーネントとして現在も有効なため、削除していない。`viewerSearchSavedViews`/`viewerGetSavedView`（`frontend/api/teamMarkdownApi.ts`）は `TeamMarkdownViewer` 削除後に呼び出し元が無くなったが、対応する backend endpoint 自体を維持する方針（self-credential API と同じ扱い）に合わせ、client-side wrapper 関数も維持した。

**SSOT訂正（第2ラウンド）:**
- `docs/design/admin-normal-surface-projection-seed-ssot.yaml`: `inputer_runtime_adapter_contract.status` を `hardcoded_interim_pending_seed_conversion` から `reverted_to_canonical_admin_route_2026_07_15_gate0_audit` へ、`viewer_runtime_reachability.status` を同様に `not_implemented_reverted_2026_07_15_gate0_audit` へ訂正。`design_blocking.normal_dashboard_authoring_runtime_adapter` は reopened のまま維持しつつ、resolution_record に「round 1 で reopen したが interim mount 自体は残していた→round 2 でその interim mount も撤去した」という2段階の経緯を明記。
- 同ファイルの `personal_page:` section を `self_account_capability:` へリネームし、再構成。owner 指示（「固定pageではないself-scoped projection capabilityとして必要性を再判定する」）に従い、「将来 Personal Page という名の page surface が作られる」という前提を排除——frontend実装の有無・形状（専用page/既存surfaceへの埋め込み/その他）は topology UI seed production 再開後の未決定の設計判断であり、本entryはそれを先取りしないことを明記した。`docs/design/auth-db-session-credential-ssot.yaml` の参照パスもこのリネームに追従。
- `docs/design/team-markdown-dashboard-saved-view-ssot.yaml` の `dashboard_surface_contract.entry_surface` を `/admin/team-dashboard` が `preferred`/`implemented` である状態へ復元し、`/dashboard/team` を `removed:` へ移動。`route_change_2026_07_14` を `route_change_2026_07_14_REVERTED_2026_07_15` へ改名し、往復の経緯（導入理由→ラウンド1監査での無効化理由→ラウンド2での物理撤去）を記録。
- `frontend/components/catalog.ts`: `team_markdown_dashboard.viewer` エントリを削除（`TeamMarkdownViewer` 消滅のため）。`md_translation_authoring_surface.authoring`/`md_viewer.projection` の `routeCompositionFile`/notes を `/admin/team-dashboard` 基準へ訂正。
- `backend/tests/Topolactor.Runtime.Tests/SsotWiringAuditComponentRegistrationTests.cs`、`frontend/tests/runtimeComponentCatalogFullConnection.test.ts`、`frontend/tests/roleBasedSurfaceSeparation.test.ts`、`frontend/tests/teamMarkdownSavedView.test.ts` から対応する参照・test を削除/訂正。

**backend への影響:** production backend code（`.cs`）は一切変更していない（NG軸を遵守）。変更したのは backend test 1ファイルから、削除済み frontend component 名を指す2行の lookup table entry を除去しただけ。

**検証:** `deno test -A frontend/tests/`（1871 passed / 0 failed）、`bash .agent/tests/check-frontend-types.sh`（PASS）、`bash .agent/tests/check-admin-normal-surface-projection-seed-ssot.sh`（PASS）、`bash .agent/tests/check-structure.sh`（PASS）、`dotnet build`（0 errors）、`dotnet test tests/Topolactor.Runtime.Tests`（1443 passed / 0 failed）。diff hygiene: `git diff --stat` と `--ignore-space-at-eol --stat` が完全一致、`db/*.sql` への変更なし、production `backend/*.cs` への変更なし。

**2026-07-15 Bundle-level gate0 restoration、第3ラウンド（PR #591 owner review、frontend bypass residue の解消）:** round 2 は削除済み surface（Personal Page/Normal Dashboard Home/`/dashboard/team`）の route・island・component を撤去したが、それら専用の frontend API wrapper 関数と Fresh proxy route（backend endpoint への薄い転送のみで、他に呼び出し元が無いもの）が未参照のまま残っていた。owner から、これも同じ「無駄に生やしたハードコード」の一種であり、"将来の再利用" という推測だけでは残す理由にならないと指摘された。

**削除（全て呼び出し元ゼロを事前に確認した上で実施）:**
- `frontend/api/teamMarkdownApi.ts` の `viewerFetch`/`viewerListTemplates`/`viewerSearchSavedViews`/`viewerGetSavedView`（削除済み `TeamMarkdownViewer` 専用）。
- `frontend/routes/api/team-markdown/templates/{index.ts,[templateId].ts}`、`frontend/routes/api/team-markdown/saved-views/{index.ts,[savedViewId].ts}`（上記4関数専用の Fresh proxy route、4ファイルとも doc comment に明示的に "viewer read" と記載されており admin dispatch lane とは別物）。
- `frontend/api/hubNavigationApi.ts`（`fetchHubRelationNavigationLinks` のみを export する1ファイル、削除済み `NormalDashboardHome.tsx`/`RelatedHubLinksPanel` 専用）と `frontend/routes/api/hub-navigation/relations.ts`。
- `frontend/api/authApi.ts` の「Self-service credential/session lifecycle」section 全体（`getCurrentAccount`/`changeOwnPassword`/`listOwnSessions`/`revokeOwnSession`/`revokeOtherSessions`、関連 type `CurrentAccountResponse`/`SessionSummary`/`ListSessionsResponse`、内部 helper `authFetch`、削除済み `PersonalPage.tsx` 専用）と `frontend/routes/api/auth/me.ts`、`frontend/routes/api/auth/me/password.ts`、`frontend/routes/api/auth/me/sessions/{index.ts,revoke.ts,revoke-others.ts}`。

**backend への影響: なし。** 上記は全て frontend 側の wrapper/proxy のみ。対応する backend endpoint（`GET /auth/me`、`POST /auth/me/password`、`GET /auth/me/sessions`、`POST /auth/me/sessions/revoke`、`POST /auth/me/sessions/revoke-others`、`GET /team-markdown/templates`、`GET /team-markdown/saved-views`、`GET /team-markdown/saved-views/{id}`、`GET /hub-navigation/relations`）とそれを実装する repository/runtime/test は一切変更していない——NG軸の「backend の再利用可能な認証・viewer-read・transaction substrate を削除する」ことへの明示的禁止に従った。これらの backend endpoint は API/test から直接到達可能なまま残っており、将来 real な seed-driven UI が作られる際は新たな thin proxy route を追加すれば足りる。

**SSOT/script訂正:** `docs/design/admin-normal-surface-projection-seed-ssot.yaml` の `prop_bindings.markdown_authoring_input.status`（`resolved_via_realized_route_composition_2026_07_14_round_4` → `hardcoded_interim_pending_seed_conversion_2026_07_15_gate0_audit`）と `event_bindings.authoring_write_completion.backend_boundary`（"via the realized route composition" → "via the current hardcoded interim mount"）を、round 2 で訂正済みの `inputer_runtime_adapter_contract.status` と整合する語彙へ訂正。`.agent/tests/check-admin-normal-surface-projection-seed-ssot.sh` のコメント内 status 名を同期。round 1 の「`/dashboard/team` 関連は削除していない」という記述（本ファイル上記、round 2 セクション直前）に `[SUPERSEDED — round 2 参照]` marker を追加。

**検証:** `deno test -A frontend/tests/`（1871 passed / 0 failed、削除前と同数——orphan だったことの裏付け）、`bash .agent/tests/check-frontend-types.sh`（PASS）、`bash .agent/tests/check-admin-normal-surface-projection-seed-ssot.sh`（PASS）、`bash .agent/tests/check-structure.sh`（PASS）、`dotnet build`（0 errors）、`dotnet test tests/Topolactor.Runtime.Tests`（1443 passed / 0 failed）。diff hygiene: `git diff origin/main --stat` と `--ignore-space-at-eol --stat` が完全一致（42 files changed）、`db/*.sql` への変更なし、production `backend/*.cs` への変更なし（backend test file 1件から dead lookup entry 2行を除去のみ、round 2 の commit）。

**2026-07-15 Bundle-level gate0 restoration、第4ラウンド（PR #591 owner review、`existing_route_composition` 機構自体の自己正当化語彙を撤去）:** round 1〜3 は個々の surface（Personal Page, Normal Dashboard Home, `/dashboard/team`）の hardcode 判断を訂正したが、それらが依拠していた共通機構——`ComponentRuntimeReachability` 型（`frontend/components/types.ts`）、`isRuntimeReachable()` helper（`frontend/components/catalog.ts`）、`frontend/tests/runtimeComponentCatalogFullConnection.test.ts`、`backend/tests/Topolactor.Runtime.Tests/SsotWiringAuditComponentRegistrationTests.cs`——自体のdoc commentが、hardcoded route composition を一般的に「SSOT-sanctioned」「is this seed-usable at all」「an alternate valid runtime-reachability condition」「the OTHER real reachability condition」と表現しており、これは round 1〜3 で個別 surface について否定した `route_composition_alternative` と同種の一般化された自己正当化語彙だった。owner から、将来の Agent がこの語彙を「hardcode を認める設計 precedent」として再利用できてしまう状態を残すべきではないと指摘された。

**対応内容（語彙訂正、機構自体は削除せず維持）:** `routeCompositionFile` が実ファイルへ実際に mount されているかを検証するテスト機構自体は、catalog の虚偽記載を防ぐ正当な anti-drift 検証であるため削除しなかった（削除するとNG軸が禁止する「機構削除」に該当する可能性があり、かつ有用な検証を失う）。代わりに、以下の箇所全てから「SSOT-sanctioned」「seed-usable」「alternate valid」「OTHER real reachability」という正当化語彙を除去し、明示的な non-authority 宣言に置き換えた:
- `frontend/components/types.ts`（`ComponentRuntimeReachability` 型の doc comment）
- `frontend/components/catalog.ts`（`isRuntimeReachable()` の doc comment）
- `frontend/tests/runtimeComponentCatalogFullConnection.test.ts`（section header comment、invariant test の assertion message）
- `backend/tests/Topolactor.Runtime.Tests/SsotWiringAuditComponentRegistrationTests.cs`（同等の comment）
- `docs/design/admin-normal-surface-projection-seed-ssot.yaml`（`component_runtime_connected_evidence` の「this closed 2026-07-14 and is not a follow-up」を、「test-verification mechanism 自体は follow-up 不要だが、これは test の正しさの話であって Gate 0 completion の話ではない」という訂正へ）
- `docs/design/component-catalog-classification-ssot.yaml`（`prohibited:` list へ4項目追加: `existing_route_composition`/`isRuntimeReachable` を Gate 0 例外・seed利用可否・Bundle completion の根拠として扱うこと、および正当化語彙を使うことの明示的禁止）

いずれも「物理的に mount されているという観測事実」自体は真実として残し、それが Gate 0 適合・seed 利用可否・completion の根拠にならないことを明記した。既存の `/admin/team-dashboard` 等の物理 route、backend security/transaction substrate は変更していない。`design_blocking.normal_dashboard_authoring_runtime_adapter` は reopened のまま維持。

**検証:** `deno test -A frontend/tests/`（1871 passed / 0 failed）、`bash .agent/tests/check-frontend-types.sh`（PASS）、`bash .agent/tests/check-admin-normal-surface-projection-seed-ssot.sh`（PASS）、`bash .agent/tests/check-structure.sh`（PASS）、`dotnet build`（0 errors）、`dotnet test tests/Topolactor.Runtime.Tests`（1443 passed / 0 failed）。diff hygiene: `git diff origin/main --stat` と `--ignore-space-at-eol --stat` が完全一致（43 files changed）、`db/*.sql` への変更なし、production `backend/*.cs` への変更なし（backend test file 1件の comment 訂正のみ）。

**2026-07-15 Bundle-level gate0 restoration、第5ラウンド（PR #591 owner review、design_change のみ——次Bundle scope の admin/Normal relation所属を固定）:** owner から、PR #591 close前に、cleanup 後の次Bundle境界（admin側の credential-management projection seed と既存 admin hub relation authority の接続、Normal側の Team Dashboard/`self_account_capability` projection seed 所属先の新規 Normal authority relation 設計）を SSOT/TODO へ明記するよう指示された。worktype は design_change——production code・DB row・frontend実装は一切追加していない。

**対応内容（SSOT design contract の追加、いずれも live relation row や具体 seed は含まない）:**
- `docs/design/admin-normal-surface-projection-seed-ssot.yaml` の `surface_axes.normal` 配下に、新規 `normal_hub_relation_navigation_contract` を追加。既存の `admin_hub_relation_navigation_contract`（`docs/design/admin-console-workflow-ssot.yaml`）と同一の runtime mechanism（`hubs.hub_relations`/`HubNavigationResolver`/`GET /hub-navigation/relations`）を再利用しつつ、authoring authority（Normal-role は relation を author しない——既存 `/admin/manifests` 経由のみ）・source/target eligibility・target_manifest_resolution・capability_requirement（navigation自体はno-admin-capability）・projection_axis・現在の target readiness（`dashboard`=team-dashboard・`self_account_capability` 共に blocked）・`prohibited`（fake manifest 禁止、新規 Normal 向け authoring UI/dispatch禁止、admin contractの単純renameとして扱うこと禁止、live connection state のSSOT固定化禁止、`self_account_capability`の未来frontend形状の先取り禁止、seed production再開前の実relation authoring禁止）を明記した、admin側とは区別される独立した design contract として記述。
- `normal.surfaces.dashboard`（Team Dashboard）・`normal.surfaces.self_account_capability` それぞれに `hub_relation_navigation_binding_ref` を追加し、上記 contract への参照と、現時点でのtarget非存在理由を明記。
- `docs/design/admin-console-workflow-ssot.yaml` の `subbundle_target_readiness.credential-management` に、「将来 topology UI seed 取得後は既存 `admin_hub_relation_navigation_contract` にそのまま接続し、専用 relation mechanism・fake source は作らない」という明示的な reinforcement を追記（既存文言への軽微な補強、既存の design方針自体は変更なし）。
- `docs/design/auth-db-session-credential-ssot.yaml`・`docs/design/team-markdown-dashboard-saved-view-ssot.yaml` それぞれに、将来の real topology UI seed 取得後は上記 `normal_hub_relation_navigation_contract` の candidate source/target になる、という次Bundle lineage note を追加（design記録のみ、seed row・manifest・relation は一切作成していない）。

**検証:** `bash .agent/tests/check-admin-normal-surface-projection-seed-ssot.sh`（PASS）、`bash .agent/tests/check-structure.sh`（PASS）。design_change のため frontend/backend test・build は対象外（production code変更なし）。

**Primary SSOT:** `docs/design/admin-normal-surface-projection-seed-ssot.yaml`, `docs/design/auth-db-session-credential-ssot.yaml`, `docs/design/team-markdown-dashboard-saved-view-ssot.yaml`, `docs/design/component-catalog-classification-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`

#### 問題点（初回実装時点）

PR574/577/578/584/587/588 の続きとして、role別（Normal / admin）surface separation の component・runtime・backend実装が未着手だった。Team Dashboard は `/admin/team-dashboard` 配下で admin 専用のまま、Personal Page は存在せず、self password change / session revoke operation は backend に一切存在せず、admin credential management には role 表示・session revoke・credential revoke がなく、scheduler edit backend は実装済みだが UI がなく、これら component は component catalog 上で runtime 未接続だった。加えて `OperationVectorResolver` が JWT 検証済み subject を `OperationVector` へ伝搬しておらず、admin 監査 actor (`ResolveAuditActor`) がクライアント供給の `ContextUserId` を信頼する spoofable な状態だった。

#### 問題点（PR #589 レビューコメントによる追加指摘、2026-07-14）

初回実装は署名+`exp`のみを検証する既存 `JwtGuard.Validate`/`ValidateForContext` に依存しており、session revoke・password変更・credential revoke・account無効化・role変更のいずれも、既に発行済みの access JWT を即座に失効させる仕組みが無かった（refresh token 失効だけでは access JWT 自体は exp まで有効なまま）。role変更は grant 更新のみで旧 role の JWT がそのまま使え続けた。component catalog の `runtimeConnected` と route-composition reachability の関係が prose (`notes` 自由記述) のみで、test-verified な形になっていなかった。Normal Dashboard Home のナビゲーションリンクは frontend hardcode かつ JWT role claim だけで判定していた。Team Markdown の Refresh/Clone/Rebind は `stubNotice()` のみで実操作に到達しなかった。

#### 目的

既存 JWT / role / repository / operation を再利用しながら、Team Dashboard の viewer(Normal+admin) / authoring(admin) 分離、Personal Page、self credential lifecycle、admin credential management 補完、scheduler edit UI、component catalog/adapter 接続、JWT session revocation identity、hub-relation link-list fallback、Team Markdown Refresh/Clone/Rebind を実装し、次PRが production seed 登録・render proof・action proof・legacy route retirement だけに集中できる状態にする。topology UI seed 生成・登録（本 Bundle 自体の 5 subBundle scope）は今回行わない。

#### 改善方針・実装内容（全て解決済み）

- 新規 backend operation は既存 auth_runtime（login/refresh/logout と同じ thin HTTP boundary パターン、manifest dispatch を経由しない）に統一し、topology seed / dispatcher_mapping への依存を作らない。
- Team Dashboard の viewer read（search/get）は新設の JWT-only read endpoint（`GET /team-markdown/*`）経由とし、admin_runtime dispatch lane（manifest capability_requirement 推論が action 単位ではなく manifest 単位で admin 一律適用される）はこれまで通り admin-only mutation にのみ使う。
- `OperationVector` に `AuthenticatedUserId`/`AuthenticatedRole`（JWT 検証済み、client 供給不可）を追加し、`AdminRuntimeMasterRoster.ResolveAuditActor` と `AdminRuntime.TeamMarkdown.ExecuteTeamMarkdownAsync` の admin-role 明示チェックに使う。
- Team Markdown の write（`saved_view:update`）に `dryRun`（非破壊 preview/validate）と `confirmed`（明示確認必須）payload flag を追加し、write と diff evidence（`team_markdown_saved_view_event` insert）を1トランザクションにまとめる（`UpdateSavedViewWithDiffEvidenceAsync`）。
- component catalog は「runtime factory 登録」だけでなく `admin-normal-surface-projection-seed-ssot.yaml` が既に認めている `existing_route_composition`（route composition による adapter）も正当な reachability 経路として扱う。**（2026-07-14 追記）** これを prose だけでなく構造化フィールド化: `ComponentCatalogClassification.runtimeReachability`/`routeCompositionFile` を追加し、`isRuntimeReachable()` ヘルパーと、backend (`SsotWiringAuditComponentRegistrationTests.ComponentRegistrationLane_ExistingRouteCompositionEntries_MustBeMountedInClaimedFile`) / frontend (`runtimeComponentCatalogFullConnection.test.ts`) 双方の新規テストが、各 `routeCompositionFile` が実際に該当 component を import/mount していることをファイル内容照合で検証する。`runtimeConnected` 自体の意味（factory registration のみ）は変更していない。
- **（2026-07-14 追記）JWT session revocation identity:** `JwtTokenIssuer.IssueAccessToken` が `sid`（session id）claim を login/refresh の両方で発行する access JWT へ埋め込むよう変更。`AuthRepository.IsSessionActiveAsync(sessionId)` を新設し、`auth.sessions` の revoked_at/expires_at と `auth.users` の active/approve/status/suspension window を1クエリで検証する（`EvaluateLoginState` と同じ判定条件を再利用、新しい authority を作らない）。`JwtGuard.ValidateActiveSessionAsync`/`ValidateForContextActiveSessionAsync` を新設し、`backend/Program.cs` の JWT 認証を要求する全endpoint（`/dispatch`、`/component-events/append`、`/intake/legacy-change`、`/auth/session`、`/auth/me*`、`/admin/auth/users/{userId}/*`、`/team-markdown/*`、`/draft-preview/*`、`/sql-attention/topology-projection`、`/sse`）をこの DB 検証付き検証へ切り替えた（署名+`exp`のみの `Validate`/`ValidateForContext` は直接呼ばれなくなった）。`sid` claim 欠如は明示的に fail-close（`AUTH_TOKEN_SID_MISSING`）。
- **（2026-07-14 追記）role変更時の session 失効:** `NpgsqlAuthMasterRepository.UpdateUserAsync` が admin grant の有無を変更前後で比較し、実際に変わった場合のみ、grant 更新と同一トランザクションでその user の active session を全て revoke する（no-op の role 指定では revoke しない）。
- **（2026-07-14 追記）自己パスワード変更後のクライアント側失効:** `PersonalPage.tsx` のパスワード変更成功時に `logoutUser()`（httpOnly refresh cookie の server-side clear）と `clearSessionToken()`（sessionStorage/cookie の client-side clear）を呼び、`/auth` へ redirect する。backend 側の全 session revoke（`ChangeOwnPasswordAsync`）は初回実装から既に完了済みだった。
- **（2026-07-14 追記）hub-relation link-list fallback:** 新規 `GET /hub-navigation/relations`（any authenticated JWT、admin-gate なし）が既存 `HubNavigationResolver`（`ResolveCanonicalDefaultEntryManifestIdAsync` + `ResolveAsync`、`ContentBundleRepository` 経由、新しい authority ではない）から read-only navigation link list を導出し、何も解決しない場合は明示的な空配列を返す。`NormalDashboardHome.tsx` の「関連ハブ」panel（`RelatedHubLinksPanel`）がこれを実際に表示し、以前の JWT role claim ベースの frontend hardcode ナビゲーションを置き換えた。hub_navigation の mutation（create/update/deprecate/reorder）は既存の admin-gated `/dispatch` レーンのみに残り、この新規 read boundary からは一切到達不可能（コード上、mutation メソッドへの呼び出し経路が存在しない）。
- **（2026-07-14 追記）Team Markdown Refresh/Clone/Rebind の実装:** 新規 `SavedViewOperationPanel`（`frontend/components/SavedViewOperationPanel.tsx`）が実際の input 収集（templateMarkdown/sourceRecordJson、clone の target refs、rebind の bindingJson/completedPresetSeedJson）、client-side no-op-not-a-backend-call preview（これら3 action には `saved_view:update` と異なりserver-side dryRun がないため）、`ApplyConfirmDialog` による明示確認、実際の write（既存 `team_markdown:saved_view:refresh/clone/rebind` action、`refreshSavedViewFromSource` を新規追加）、write後の refetch を実装。`TeamMarkdownAuthoring` の `stubNotice()` 呼び出しはすべて置き換えた。

#### 対応資料

- `docs/design/admin-normal-surface-projection-seed-ssot.yaml`（personal_page surface 追加、inputer_runtime_adapter_contract 解決、design_blocking 更新、credentials.users.operations 訂正）
- `docs/design/auth-db-session-credential-ssot.yaml`（self_credential_and_session_lifecycle 節追加）
- `docs/design/team-markdown-dashboard-saved-view-ssot.yaml`（実装は既存 mutation_flow 契約に準拠、SSOT 本文は無改変）
- `docs/design/component-catalog-classification-ssot.yaml`（vocabulary 変更なし、既存語彙のみ使用）

#### 対象ファイル名（backend）

- `backend/schema/Contracts.cs`（`OperationVector.AuthenticatedUserId`/`AuthenticatedRole`）
- `backend/runtime/OperationVectorResolver.cs`（JWT 検証済み context key の読取）
- `backend/runtime/AdminRuntimeMasterRoster.cs`（`ResolveAuditActor`、role field 読み書き、role validation）
- `backend/repository/AuthMasterRepository.cs` / `backend/repository/NpgsqlAuthMasterRepository.cs`（`role` 列、`UpdateUserAsync(roleName:)`、role変更時 session revoke）
- `backend/schema/AuthMasterContracts.cs`（`AuthUserRosterDto.Role`、`AuthUsersUpdateRequestDto.RoleName`）
- `backend/repository/AuthRepository.cs` / `backend/repository/NpgsqlAuthRepository.cs`（`ChangeOwnPasswordAsync`, `ListActiveSessionsByUserAsync`, `RevokeOwnedSessionAsync`, `RevokeSessionsForUserAsync`, `FindActiveSessionIdByRefreshTokenHashAsync`, `RevokeCredentialAsync`, `IsSessionActiveAsync`）
- `backend/service/AuthService.cs`（self/admin credential・session operation family、`IssueAccessToken` sessionId伝搬）
- `backend/service/JwtTokenIssuer.cs`（`sid` claim 発行）
- `backend/guard/JwtGuard.cs`（`ValidateActiveSessionAsync`/`ValidateForContextActiveSessionAsync`/`TryGetSessionId`）
- `backend/runtime/AuthRuntime.cs` / `backend/endpoint/AuthEndpoint.cs`（thin wrapper 追加）
- `backend/schema/AuthContracts.cs`（self/admin credential DTO 追加）
- `backend/schema/ContentBundleContracts.cs`（`HubRelationNavigationLinksResponseDto`）
- `backend/runtime/HubNavigationResolver.cs`（`ResolveFallbackNavigationLinksAsync`）
- `backend/Program.cs`（`/auth/me*`, `/admin/auth/users/{userId}/*`, `/team-markdown/*`, `/hub-navigation/relations` route 追加、全 JWT-guarded endpoint の `ValidateActiveSessionAsync` 切替）
- `backend/repository/TeamMarkdownRepository.cs` / `backend/repository/NpgsqlTeamMarkdownRepository.cs`（`UpdateSavedViewWithDiffEvidenceAsync`）
- `backend/runtime/AdminRuntime.TeamMarkdown.cs`（dryRun/confirmed、admin-role 明示チェック）
- `.github/workflows/backend-tests.yml`（`db/auth_tables.sql` をこの CI レーンの schema apply list へ追加 — 元々このレーンに欠落していた）

#### 対象ファイル名（frontend）

- `frontend/lib/backendProxy.ts`, `frontend/routes/api/auth/me*.ts`, `frontend/routes/api/admin/auth/users/[userId]/**`, `frontend/routes/api/team-markdown/**`, `frontend/routes/api/hub-navigation/relations.ts`（thin proxy）
- `frontend/api/authApi.ts`（self credential client）, `frontend/api/adminApi.ts`（role field、admin session/credential revoke client）, `frontend/api/teamMarkdownApi.ts`（viewer read client、preview/write split、`refreshSavedViewFromSource`）, `frontend/api/hubNavigationApi.ts`（新規）
- `frontend/lib/authenticatedGate.ts`, `frontend/lib/demoSession.ts`, `frontend/lib/demoSessionValidate.ts`（authenticated(not admin-only) gate）
- `frontend/hooks/useCurrentSession.ts`, `frontend/islands/AuthenticatedGate.tsx`
- `frontend/routes/dashboard/_middleware.ts`, `frontend/routes/dashboard/index.tsx`, `frontend/routes/dashboard/team.tsx`, `frontend/routes/account/_middleware.ts`, `frontend/routes/account/index.tsx`
- `frontend/routes/admin/team-dashboard/index.tsx`（`/dashboard/team` への 302 redirect へ変更、削除はしていない）
- `frontend/islands/NormalDashboardHome.tsx`（`RelatedHubLinksPanel` 追加）, `frontend/islands/PersonalPage.tsx`（password change後の client token clear + `/auth` redirect）, `frontend/islands/TeamDashboardRoleSurface.tsx`
- `frontend/islands/TeamMarkdownDashboard.tsx`（`TeamMarkdownViewer`/`TeamMarkdownAuthoring` export、Refresh/Clone/Rebind の実操作化）
- `frontend/components/MdViewer.tsx`（`authoringEnabled` prop）, `frontend/components/SavedViewAdjustmentAuthoringPanel.tsx`, `frontend/components/SavedViewOperationPanel.tsx`（新規）
- `frontend/islands/AdminUsersRoster.tsx`（role 表示/変更、session/credential revoke UI）
- `frontend/islands/SchedulerJobSettingsPanel.tsx`（Edit UI）
- `frontend/components/catalog.ts`（新規/更新 catalog entry 9件、`runtimeReachability`/`routeCompositionFile` フィールド、`isRuntimeReachable()`）
- `frontend/components/types.ts`（`ComponentRuntimeReachability` 型、`ComponentCatalogClassification` フィールド追加）

#### 対象関数名

`ChangeOwnPasswordAsync` (Service/Repository 両方), `AdminRevokeCredentialAsync`, `AdminRevokeSessionsAsync`, `AdminListSessionsAsync`, `GetCurrentAccountAsync`, `ListOwnSessionsAsync`, `RevokeOwnSessionAsync`, `RevokeOtherSessionsAsync`, `UpdateSavedViewWithDiffEvidenceAsync`, `DataTeamMarkdownSavedViewUpdateAsync`, `ExecuteTeamMarkdownAsync`, `ResolveAuditActor`, `UpdateUserAsync`(roleName overload), `previewSavedViewUpdate`, `writeSavedViewUpdate`, `viewerSearchSavedViews`, `viewerGetSavedView`, `useCurrentSession`, `authenticatedGateHandler`, `onStartEdit`/`onCancelEdit`(SchedulerJobSettingsPanel), `IssueAccessToken`(sessionId overload), `IsSessionActiveAsync`, `ValidateActiveSessionAsync`, `ValidateForContextActiveSessionAsync`, `ResolveFallbackNavigationLinksAsync`, `refreshSavedViewFromSource`, `isRuntimeReachable`。

#### 本当に残っている次PR scope（これ以外は全て解決済み）

- production seed 行の登録: team-dashboard / admin-enum / scheduler-settings 向けの `ui_projection` topology manifest（本 Bundle 自体の既存 blocker、変更なし）。`TeamMarkdownAuthoring` / `saved_view_adjustment_authoring.authoring` / `credential_management.admin_operation` / `admin_enum_roster.admin_operation` / `scheduler_job_settings.admin_operation` / `hub_navigation_admin.admin_operation` の production seed / manifest / topology refs / dispatcher mapping 登録。**2026-07-15 gate0 audit 訂正: `personal_page.projection`/`normal_dashboard_home.projection` はこのリストから除外した**——両者の frontend component (`PersonalPage.tsx`/`NormalDashboardHome.tsx`) 自体を Gate0 audit で削除したため（「2026-07-15 Bundle-level gate0 restoration」節参照）、seed化すべき frontend component がもう存在しない。backend substrate（self-credential operations, hub-navigation relations endpoint）は残っており、real な frontend 実装を作る場合はそちらが前提になる。**2026-07-15 gate0 audit 訂正（第2ラウンド）: 「route composition は runtime reachability の canonical 方式として round 3〜5 で確定済み」「owner 判断事項として残っていない」という上記の記述も撤回し、さらに team-dashboard の到達経路自体を PR589 以前の canonical `/admin/team-dashboard`（admin-only、`TeamMarkdownAuthoring` 直接 mount）へ物理的に戻した**——`route_composition_alternative`/`existing_route_composition` は Gate 0 の judgment source (`docs/design/runtime-orchestration-ssot.yaml`) に根拠が無い自己引用パターンであり（詳細は「2026-07-15 Bundle-level gate0 restoration」節）、`RUNTIME_COMPONENT_FACTORIES` への正式登録可否は依然として owner 判断事項として残っている。`/dashboard/team`・`TeamDashboardRoleSurface.tsx` は削除済みで、もはや「hardcoded interim mount を残置している」状態ではない——`/admin/team-dashboard` は本 Bundle が始まる前から存在した canonical route であり、real な topology UI seed conversion 待ちである点は変わらないが、それは新しい暫定状態ではなく元々の状態への復帰である。
- render proof: 上記 seed 登録後の実 canonical mechanism（manifest dispatch）経由での実描画確認。
- action proof: 上記 seed 登録後の実 dispatch action 経由での実操作確認。
- ~~legacy route retirement: `frontend/routes/admin/team-dashboard/index.tsx` の 302 redirect 削除は、上記 render proof + action proof（Normal viewer / admin authoring 双方）完了後のみ。~~ **2026-07-15 gate0 audit（第2ラウンド）により消滅: `/admin/team-dashboard` は 302 redirect ではなくなり、再び直接 `TeamMarkdownAuthoring` を mount する canonical route に戻ったため、この項目自体が対象外になった。**
- **2026-07-15 gate0 audit（第5ラウンド）追加: `self_account_capability`（旧 personal_page）の topology UI seed / ui_projection manifest 登録**——`normal.dashboard`（team-dashboard）と同じ「owner の topology UI seed production 再開指示待ち」scope に、`self_account_capability` も正式に加わる。ただしこちらは他surfaceと異なり、seed生成の**前提として**「専用page/既存surfaceへの埋め込み/その他のいずれの frontend 形状を取るか」という未決定の設計判断も別途必要（`docs/design/admin-normal-surface-projection-seed-ssot.yaml` `surface_axes.normal.surfaces.self_account_capability.resolution_record` 参照）。
- **2026-07-15 gate0 audit（第5ラウンド）追加: `normal_hub_relation_navigation_contract` の実 seed / live relation row 化**——`docs/design/admin-normal-surface-projection-seed-ssot.yaml` `surface_axes.normal.normal_hub_relation_navigation_contract` として design contract は本ラウンドで確定したが、実際の relation row・seed は上記2項目（team-dashboard・self_account_capability の ui_projection manifest）が両方揃い、かつ owner の topology UI seed production 再開指示が出た後でなければ着手できない。credential-management 等 admin 側の surface は既存 `admin_hub_relation_navigation_contract`（`/admin/manifests` 経由）にそのまま接続すればよく、新規 relation mechanism は不要（`docs/design/admin-console-workflow-ssot.yaml` `subbundle_target_readiness.credential-management` 参照）。

#### 副次発見（historical note — PR589完了条件・次PR scope・owner判断のいずれにも含まれない）

- **2026-07-15 追記: 削除済み。** `frontend/components/MdTranslationAuthoringSeedSurface.tsx`（32KB、`MdTranslationAuthoringSeedSurfaceProps`/`MdTranslationAuthoringSeedSurface` を export）が存在したが、リポジトリ全体で `import`/`from` 参照が一件も無い dead code だった（実際に使われているのは `frontend/islands/TeamMarkdownDashboard.tsx` 内の同名ローカル関数——別実装、重複）。round 6 では「PR589完了条件・次PR scope・owner判断のいずれにも含まれない non-blocking な任意 cleanup」として削除を先送りしたが、owner から「無駄に生やしたハードコードを削除していない」と gate0 監査の一環として明示指摘を受け、削除が正しい対応であることを確認した。削除に伴い、この dead file のソースを直接 `Deno.readTextFile` して検証していた `frontend/tests/teamMarkdownSavedView.test.ts` の8個のテスト（dead file 自体の内容にのみ依存し、生きているコンポーネント／振る舞いには対応するものが存在しない項目）を削除し、`docs/system-roadmap.yaml` の evidence_ref からもこのパスを除去し、`docs/design/admin-normal-surface-projection-seed-ssot.yaml`・`.agent/tests/check-admin-normal-surface-projection-seed-ssot.sh` の該当コメントを「is unreferenced dead code」から「was unreferenced dead code and has been deleted」へ訂正した。`frontend/lib/mdTranslationSeedBuilder.ts`（このdead fileと test からのみ import されていたビルダー関数群）は、`buildMdTranslationAuthoringSeedCandidate` を直接ユニットテストする4件の独立した実質的なテスト（`frontend/tests/teamMarkdownSavedView.test.ts` L1038〜）が現在も存在し、round 6 の finding が dead 認定していたのは `MdTranslationAuthoringSeedSurface.tsx` のみだったため、今回のスコープには含めていない（別途 owner 判断が必要であれば次回)。「どちらを正本にするか」という判断自体、実装事実として route composition 側（`TeamMarkdownAuthoring`/`SavedViewOperationPanel`/`SavedViewAdjustmentAuthoringPanel`）が正本であることは round 3〜4 で既に確定していた。
- real-HTTP（Kestrel/TestServer 経由）での JWT revocation proof は、`Topolactor.Integration.Tests` が `backend/Program.cs`（top-level statements、DI wiring全体）を参照せず個別ソースファイルを `<Compile Include>` で直接リンクする既存アーキテクチャのため、`WebApplicationFactory<Program>` 相当のテストを安全に追加できなかった（`Program.cs` を含める場合、同一ファイルの二重コンパイルや型重複が発生する構造的制約）。代わりに、実際の `JwtTokenIssuer`/`JwtGuard`/`NpgsqlAuthRepository`/`NpgsqlAuthMasterRepository` クラスを実 Postgres に対して直接使う `JwtGuardSessionRevocationLiveDbTests`/`AuthSessionRevocationLiveDbTests` で、HTTP 層を除く全ての実クラス・実DBパスを証明した。`backend/Program.cs` の該当16 endpoint すべてが `ValidateActiveSessionAsync`/`ValidateForContextActiveSessionAsync` を呼ぶことは手動で確認済み。real-HTTP round-trip proof の追加は、Integration.Tests のアーキテクチャ変更（`Topolactor.Host.csproj` への ProjectReference 化）を伴う別スコープの作業として次回判断が必要。

#### Governance NG boundary

- 本 Bundle自体（`admin-surface-topology-seed-conversion`）の Status / SubBundle scope / design_blocking 以外の記述は、この節の統合以外の目的では変更しない。
- topology UI seed 行・dispatcher_mapping 行・hub_relation 行を追加しない（`db/seed_empty.sql` 等の topology seed content は無変更。`db/auth_tables.sql` の CI schema-apply list への追加はテストインフラであり production seed content ではない）。
- fake/empty manifest を hub_relation source 目的で作らない（PR584 で一度発生し reverted 済みの anti-pattern、再発させていないことを確認済み）。
- admin による他人の password 値の指定・変更・閲覧を実装しない（確認済み: `AdminRevokeCredentialAsync` は credential 行を削除するのみで新しい password を一切受け取らない）。
- self credential operation の request body に任意 user id を含めない（確認済み: `/auth/me/*` は全て JWT subject から target を解決）。
- JWT 署名と `exp` だけを検証し session revoke 後も access JWT を有効なまま残すことをしない（`ValidateActiveSessionAsync` が全 endpoint で必須）。
- refresh token だけを失効させて access JWT 失効完了と報告しない（`sid` claim + `IsSessionActiveAsync` が access JWT 自体を即座に無効化する）。
- role 変更時に grant だけを変更し旧 role の JWT を有効なまま残さない（`UpdateUserAsync` が role 変更時に session を revoke する）。
- frontend の token 削除だけで backend authority 失効を代替しない（`ChangeOwnPasswordAsync` の server-side session revoke が既に完了済みの上で、frontend の token clear を追加している）。
- JWT body・request body・query parameter から session authority を信頼しない（`sid` は発行時に server が埋め込む値のみを信頼する）。
- session authority と無関係な第二の revocation authority を新設しない（`IsSessionActiveAsync` は既存 `auth.sessions`/`auth.users` のみを参照する）。
- route composition で到達可能な component を、runtime factory 未登録だけを理由に seed 利用不能と判定しない、また `runtimeConnected=false` component を seed から直接 runtime binding しない（`runtimeReachability`/`isRuntimeReachable()` は新しい reachability 概念であり、`runtimeConnected` の意味は変えていない。seed からの直接 binding はまだ行っていない — production seed 登録は次PR scope）。
- production seed へ React component path や hardcoded route を直接埋め込まない（今回のいかなる新規コードも production seed ファイルへ触れていない）。
- relation fallback を frontend hardcode で実装しない、また全 relation・admin relation・他ユーザー relation を無条件に含めない、JWT role claim だけで relation 所属を判定しない（`GET /hub-navigation/relations` は既存 `HubNavigationResolver` の canonical navigation sequence のみを返す。per-user relation ownership table は依然としてこのデータモデルに存在しない — hub_relations は manifest-scoped navigation graph であり user-scoped ではない — が、round 3 で各候補 link の target manifest に対し既存 `ManifestDispatcher` capability_requirement/required_role 権限境界を再利用した role-based filtering を追加し、caller の role が満たさない target を除外するようにした。canonical default manifest の outbound relation を全認証ユーザーへ一律返す旧実装は、これを明示的に失敗させる test（`AdminOnlyTargetRelation_ExcludedForUserRoleCaller_IncludedForAdminRoleCaller`）で red 確認した上で修正済み）。
- mutation authority denial を link list response へ変換して成功扱いにしない（`GET /hub-navigation/relations` のコードは hub_navigation の mutation メソッドを一切呼ばない — 別々の repository メソッドを使うため、denial を成功へ変換する経路自体が存在しない）。
- Refresh / Clone / Rebind の stub・disabled placeholder・通知だけの操作を残さない（`SavedViewOperationPanel` が実際の preview → confirm → write を実装）。
- source file 文字列 test だけで role visibility・JWT 失効・runtime reachability・relation fallback を証明しない（`teamDashboardRoleSurfaceRealRender.test.ts`・`hubNavigationFallbackLinks.test.ts`・`savedViewOperationPanel.test.ts` が real DOM render で、`JwtGuardSessionRevocationLiveDbTests`/`AuthSessionRevocationLiveDbTests` が real Postgres でそれぞれ証明する）。
- PostgreSQL transaction を通さず mock だけで password / session / diff evidence completion を証明しない（上記 LiveDb tests が実 Postgres に対して実行される）。
- backend が `confirmed` なしの Refresh/Clone/Rebind direct dispatch を受理しない（`WRITE_CONFIRMATION_REQUIRED`、frontend dialog を経由しない直接 dispatch でも拒否されることを `AdminRuntimeRefreshCloneRebind_RejectsWriteWithoutExplicitConfirmed` で証明）。Normal role JWT は `confirmed:true` を送っても拒否される（admin-role gate が confirmed check より先に走る、`NormalRoleJwt_RefreshCloneRebind_RejectedEvenWithConfirmedTrue` で証明）。
- Refresh/Clone/Rebind の mutation 後に event evidence を best-effort で追記し失敗しても write を commit することをしない（`UpdateSavedViewWithEventEvidenceAsync`/`CloneSavedViewWithEvidenceAsync` は単一 transaction、event insert 失敗時は実 Postgres CHECK constraint violation を用いた fault-injection test で mutation の rollback を証明済み）。
- JWT `role` claim を canonical grant 照合なしで authoritative capability として使わない（`IsSessionIdentityActiveAsync` が `auth.grants` への EXISTS 照合を role/realm 単位で行う。`sid`/`sub`/`realm`/`aud` が全て正しくても `role` だけ偽装された token は `ActiveSession_ForgedAdminRoleClaim_WithOnlyUserGrant_IsRejectedByGuard` で拒否証明済み）。
- target manifest が存在しない relation を `requiredRole=null`（＝全員に表示可能）として公開しない（`HubNavigationResolver` は `ManifestRepository.LoadByIdAsync` が null を返す＝dangling reference の場合を明示的に除外する。`RelationWithNonNullTargetManifestIdButNoSuchManifestRow_IsExcluded_FailClosed` で証明。`TargetManifestId=null` の場合との区別を怠らない）。
- SSOT の `actual_component_props`/`actual_component_events` へ実装と一致しない架空契約を残さない（`inputer_runtime_adapter_contract` は実際に mount される `TeamMarkdownAuthoring`/`SavedViewAdjustmentAuthoringPanel`/`SavedViewOperationPanel` の実 props/events へ同期済み。`.agent/tests/check-admin-normal-surface-projection-seed-ssot.sh` も dead code ではなく実ファイルを検証するよう同期済み）。
- 実装済み route-composition proof を `small follow-up` として SSOT に残さない（`component_runtime_connected_evidence` の該当記述を削除し、実装済みの事実として記録）。

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
