# Agent UI Protocol Implementation Todo

## Bundle `agent-ui-protocol-tooling`

**Status:** not_started  
**Worktype:** implementation_change  
**Primary SSOT:** `docs/governance/agent-ui-protocol-ssot.yaml`  
**Reference:** `docs/governance/reference/agent-ui-tool-output-reference.yaml`, `docs/governance/reference/agent-ui-senario-tmp-reference.yaml`, `docs/governance/reference/agent-ui-negative-boundary-reference.yaml`  
**Proof surface:** `.agent/tests/check-agent-ui-protocol-ssot.sh`

### 問題点

- `.agent` prompt / protocol / checklist は存在するが、Agent が作業開始時に implementation name / worktype / SSOT section / senario contract / local test / checklist interview へ一貫して入る tool-first 導線がまだ無い。
- AI が `uuid` / `datetime` / `worktype` metadata / `tool.log` record まで手書きできる余地が残っており、AI-authored content と tool-generated metadata の境界が runtime tool 上で未実装。
- `senario-tmp.md` は local temporary contract として設計済みだが、作成・読込・checklist interview への接続 tool が未実装。
- `docs/governance/logs/tool.log` は空ファイルとして接続済みだが、append-only / human cleanup / compact metadata only の tool runtime が未実装。
- PR #560 で SSOT / Reference / proof surface は設計・配線されたが、既存 `AGENTS.md` / `.agent/rules/rule.md` / `.agent/README.md` / worktype route は tool-first 導線へまだ置換されていない。

### 目的

PR #560 で確定した Agent UI protocol SSOT / Reference に従い、Agent 作業を tool-first に管制する。

目的は、Agent にすべての資料を読ませることではなく、許可された scope / SSOT section / checklist / proof surface だけを通過させること。

AI は意味入力と実装を担当し、tool は metadata / log / route output / check execution を担当する。human は cleanup / judgment / governance を担当する。

### 改善方針

- `initial contract` 用 tool と `local test` 用 tool の2本を実装する。
- Tool output は `docs/governance/reference/agent-ui-tool-output-reference.yaml` に従い、compact structured output とする。
- `senario-tmp.md` は `docs/governance/reference/agent-ui-senario-tmp-reference.yaml` に従って local temporary file として作成し、commit 対象にしない。
- NG boundary は `docs/governance/reference/agent-ui-negative-boundary-reference.yaml` に従い、prompt NG axis 由来の top boundary として扱う。
- `tool.log` は `docs/governance/logs/tool.log` へ append-only で記録する。1 usage = 1 line。cleanup / truncate / rewrite は tool が行わない。
- `AGENTS.md` / `.agent/rules/rule.md` / `.agent/README.md` / `.agent/routes/worktype-required-protocols.yaml` / worktype prompt 導線は tool-first に置換する。ただし既存 prompt / protocols / checklists は tool 非対応 Agent 向け fallback として残す。
- Reference に不足・表記ズレがあれば最小差分で補正する。SSOT 側へ template 詳細や長い checklist wording を戻さない。

---

## SubBundle `agent-ui-initial-contract-tool`

**Status:** not_started

### 目的

Agent 作業開始時の route / SSOT selection / senario contract / usage metadata を tool で固定する。

### 対応資料

- `AGENTS.md`
- `.agent/rules/rule.md`
- `.agent/README.md`
- `.agent/routes/worktype-required-protocols.yaml`
- `.agent/prompt/implementation-change.md`
- `.agent/prompt/design-change.md`
- `.agent/prompt/existing-pr-update.md`
- `docs/governance/agent-ui-protocol-ssot.yaml`
- `docs/governance/reference/agent-ui-tool-output-reference.yaml`
- `docs/governance/reference/agent-ui-senario-tmp-reference.yaml`
- `docs/governance/reference/agent-ui-negative-boundary-reference.yaml`
- `.agent/tests/check-agent-ui-protocol-ssot.sh`

### 対象ファイル名候補

- `.agent/tools/agent-ui-initial-contract` or equivalent thin entrypoint
- `.agent/scripts/agent_tools/agent_ui_initial_contract.py` or equivalent implementation body
- `.agent/scripts/agent_tools/readonly_observation.py` only if existing dispatch model requires integration
- `.agent/tests/check-agent-ui-protocol-ssot.sh`
- `.agent/tests/check-agent-ui-initial-contract.sh` or equivalent new proof surface if needed
- `docs/governance/logs/tool.log`
- local temporary `senario-tmp.md` output

### 対象関数名 / command 候補

- `initial_contract`
- `input_implementation_name`
- `select_worktype`
- `emit_worktype_prompt`
- `resolve_target_ssot`
- `list_ssot_sections`
- `select_ssot_sections`
- `emit_selected_section_subtrees`
- `continue_or_quit`
- `write_senario_tmp`
- `generate_usage_metadata`
- `append_tool_log`

### 実装scope

- [ ] `implementation_name` を AI-authored input として受ける。
- [ ] `worktype` は AI に番号選択させ、tool が canonical worktype metadata へ解決する。
- [ ] worktype に応じた prompt path / short excerpt / required reads / protocol trigger hints を compact に出す。
- [ ] target SSOT 名を受け取り、repo-relative path へ解決する。
- [ ] target SSOT の section 一覧を出す。本文全体dumpはしない。
- [ ] `[section_a,section_b]` 形式で複数 section を選択できる。
- [ ] 選択 section 配下だけを subtree として出力する。
- [ ] SSOT選択へ戻る / quit を選べる。
- [ ] quit 時に senario contract を要求し、`senario-tmp.md` を作成する。
- [ ] `uuid` / `datetime` / `worktype` metadata は tool が生成する。
- [ ] `docs/governance/logs/tool.log` へ `<datetime> <uuid> <implementation_name> <worktype>` を append する。
- [ ] `tool.log` に senario body / checklist answers / verbose execution logs / full prompt dumps を書かない。

### OK軸

- [ ] initial contract tool が、SSOT の `flow_order.initial_contract` と Reference の `initial_contract_outputs` に沿って動作する。
- [ ] AI が `uuid` / `datetime` / `worktype` metadata / `tool.log` record を手書きできない。
- [ ] output は compact で、full prompt / full protocol / full checklist dump をしない。
- [ ] `senario-tmp.md` は local temporary file として作成され、git追跡されない。
- [ ] `tool.log` は append-only compact metadata だけを持つ。
- [ ] tool は implemented / partial / not_started 判定をしない。

### NG軸

- [ ] AI-authored input として `uuid` / `datetime` / `worktype` metadata / `tool.log` record を受ける。
- [ ] `tool.log` に senario body / checklist answer / raw execution log を書く。
- [ ] SSOT全体や legacy prompt/protocol/checklist 全文を output へ流す。
- [ ] `senario-tmp.md` を commit 対象にする。
- [ ] tool が semantic completion / implemented / partial / not_started を主張する。

---

## SubBundle `agent-ui-local-test-tool`

**Status:** not_started

### 目的

Agent 実装後に、worktype tests / senario-tmp output / checklist interview / required checks を tool で実行・要約する。

### 対応資料

- `.agent/rules/rule.md`
- `.agent/README.md`
- `.agent/routes/worktype-required-protocols.yaml`
- `.agent/protocols/implementation-change.md`
- `.agent/protocols/design-change.md`
- `.agent/protocols/existing-pr-update.md`
- `.agent/protocols/scenario-contract.md`
- `.agent/checklists/policy-judgment.md`
- `.agent/checklists/boundary-identity.md`
- `.agent/tests/check-structure.sh`
- `docs/governance/agent-ui-protocol-ssot.yaml`
- `docs/governance/reference/agent-ui-tool-output-reference.yaml`
- `docs/governance/reference/agent-ui-senario-tmp-reference.yaml`

### 対象ファイル名候補

- `.agent/tools/agent-ui-local-test` or equivalent thin entrypoint
- `.agent/scripts/agent_tools/agent_ui_local_test.py` or equivalent implementation body
- `.agent/tests/check-agent-ui-local-test.sh` or equivalent proof surface if needed
- `.agent/tests/check-agent-ui-protocol-ssot.sh`
- `senario-tmp.md` local temporary input

### 対象関数名 / command 候補

- `run_worktype_tests`
- `read_senario_tmp`
- `output_senario_tmp_md`
- `checklist_interview`
- `run_required_checks`
- `summarize_check_result`
- `emit_missing_evidence`
- `emit_pass_or_fail`

### 実装scope

- [ ] worktype に対応する required tests / checks を解決する。
- [ ] test command を実行し、結果を compact summary として出す。
- [ ] raw logs を Agent context へ流さない。必要なら失敗要点のみ出す。
- [ ] `senario-tmp.md` を出力する。
- [ ] `senario-tmp.md` が読めない場合は Error を出し、checklist interview を曖昧に進めない。
- [ ] checklist interview は既存 checklists / protocols を利用する。
- [ ] missing checklist answers / missing evidence を明示する。
- [ ] final output は `pass_or_fail` / `check_result` / `checklist_result` / `missing_evidence_if_any` を持つ。
- [ ] pass は required checks 通過を表すだけで、Implemented 判定ではない。

### OK軸

- [ ] local test tool が、SSOT の `flow_order.local_test` と Reference の `local_test_outputs` に沿って動作する。
- [ ] `senario-tmp.md` を checklist interview 前に出力する。
- [ ] `senario-tmp.md` が読めない場合に Error で止まる。
- [ ] check result と checklist result を分離して出す。
- [ ] missing evidence がある場合に明示する。
- [ ] tool は semantic completion / implemented / partial / not_started 判定をしない。

### NG軸

- [ ] raw execution log を大量に output する。
- [ ] checklist answer を `tool.log` へ書く。
- [ ] `pass` を Implemented と同義に扱う。
- [ ] `senario-tmp.md` が無いのに checklist interview を完了扱いする。
- [ ] worktype route を無視して固定 test だけ実行する。

---

## SubBundle `agent-ui-reference-and-route-replacement`

**Status:** not_started

### 目的

PR #560 で設計・配線した Agent UI protocol を、既存 Agent 導線へ tool-first として接続する。legacy prompt / protocols / checklists は削除せず fallback として残す。

### 対応資料

- `AGENTS.md`
- `.agent/rules/rule.md`
- `.agent/README.md`
- `.agent/routes/worktype-required-protocols.yaml`
- `.agent/prompt/*.md`
- `.agent/protocols/*.md`
- `.agent/checklists/*.md`
- `docs/governance/agent-ui-protocol-ssot.yaml`
- `docs/governance/reference/agent-ui-tool-output-reference.yaml`
- `docs/governance/reference/agent-ui-senario-tmp-reference.yaml`
- `docs/governance/reference/agent-ui-negative-boundary-reference.yaml`
- `.agent/tests/check-agent-ui-protocol-ssot.sh`
- `.agent/docs/ssot-map.yaml`
- `.agent/docs/required-paths.yaml`
- `.agent/docs/test-bundles.yaml`

### 対象ファイル名候補

- `AGENTS.md`
- `.agent/rules/rule.md`
- `.agent/README.md`
- `.agent/routes/worktype-required-protocols.yaml`
- `.agent/prompt/implementation-change.md`
- `.agent/prompt/design-change.md`
- `.agent/prompt/existing-pr-update.md`
- `docs/governance/reference/agent-ui-tool-output-reference.yaml`
- `docs/governance/reference/agent-ui-senario-tmp-reference.yaml`
- `docs/governance/reference/agent-ui-negative-boundary-reference.yaml`
- `.agent/tests/check-agent-ui-protocol-ssot.sh`

### 対象関数名 / route 候補

- `READ_ENTRY`
- `READ_TASK_MATERIALS`
- `DEFINE_SCOPE`
- `SCENARIO_CONTRACT`
- `FILL_CHECKLISTS`
- `STRUCTURE_CHECK`
- `agent_ui_initial_contract`
- `agent_ui_local_test`

### 実装scope

- [ ] `AGENTS.md` の Entry Route を tool-first に寄せる。ただし tool 非対応 Agent 向け fallback を残す。
- [ ] `.agent/rules/rule.md` に Agent UI protocol の trigger / prohibition / authority split を最小差分で反映する。
- [ ] `.agent/README.md` の directory map に `.agent/tools` の Agent UI tool role を追加する。
- [ ] `.agent/routes/worktype-required-protocols.yaml` へ必要なら Agent UI tool route を接続する。
- [ ] worktype prompt は tool が使える場合の入口を Agent UI protocol 優先に寄せる。
- [ ] Reference 内の `task_name` / `implementation_name` など命名ズレがあれば最小差分で整える。
- [ ] `senario` 綴りは既存設計に合わせ、`senario-tmp.md` を正として扱う。既存 legacy `scenario-contract.md` ファイル名は安易に変更しない。
- [ ] `.agent/tests/check-agent-ui-protocol-ssot.sh` は将来 Reference 増加に備え、配列 + loop 構成を維持・拡張する。

### OK軸

- [ ] Agent が tool 利用可能な場合、Agent UI protocol を先に通る導線になっている。
- [ ] tool 非対応 Agent 向け legacy fallback が残っている。
- [ ] SSOT は軽量な契約 / 順序 / Reference 配線のまま。
- [ ] Reference が output fields / templates / checklist interview wording を持つ。
- [ ] `.agent/tasks/todo.md` は触っていない。
- [ ] 既存 prompt / protocols / checklists を削除していない。
- [ ] Structure Check が pass する。

### NG軸

- [ ] 既存 rule / README / prompt を全文置換する。
- [ ] legacy fallback を削除する。
- [ ] SSOT 側へ template 詳細や checklist wording を戻す。
- [ ] `.agent/tasks/todo.md` を更新する。
- [ ] tool output を SSOT authority / proof completion / semantic completion として扱う。
- [ ] tool が product runtime/source を ordinary use で mutate する。

---

## Bundle-level acceptance

- [ ] `agent-ui-initial-contract-tool` が実装され、usage metadata / selected SSOT section / senario-tmp / tool.log append まで到達する。
- [ ] `agent-ui-local-test-tool` が実装され、worktype tests / senario-tmp output / checklist interview / required checks を compact に実行・要約する。
- [ ] Reference 不足・命名ズレが最小差分で整理されている。
- [ ] `AGENTS.md` / `.agent/rules/rule.md` / `.agent/README.md` / worktype route が tool-first に置換されている。
- [ ] legacy fallback が残っている。
- [ ] `docs/governance/logs/tool.log` は append-only compact metadata であり、cleanup は human periodic operation のまま。
- [ ] `senario-tmp.md` は gitignore 済み local temporary file のまま。
- [ ] `bash .agent/tests/check-structure.sh` が pass する。

## Bundle-level NG

- [ ] SubBundle 1つだけの小粒実装で完了扱いする。
- [ ] tool 2本が揃わないまま導線置換だけ行う。
- [ ] Reference 整理だけで tool 実装を先送りする。
- [ ] rule 導線置換だけで runtime tool が無い。
- [ ] AI に metadata / log / judgment authority を戻す。
- [ ] Implemented 判定を tool の pass/fail と混同する。
