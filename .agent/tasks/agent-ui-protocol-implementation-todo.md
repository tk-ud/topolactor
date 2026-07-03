# Agent UI Protocol Implementation Todo

対象repo: `github.com/tk-ud/topolactor`

worktype: `design_change` → `implementation_change`

このファイルは `.agent/tasks/todo.md` とは別管理の専用todo。既存todo索引へは追記しない。

---

## Bundle `agent-ui-protocolization`

**Status:** not_started  
**Primary SSOT:** `docs/governance/agent-ui-protocol-ssot.yaml`  
**Related governance SSOT:** `docs/governance/agent-governance-routing-ssot.yaml`, `docs/governance/agent-governance-routing-ssot.md`

### 問題点

Agent は `./AGENTS.md` / `.agent/rules/rule.md` を読んでも、toolを作業UIとして踏む導線になっていない。

また、tool使用時の uuid / datetime / task_name / worktype 証跡を `docs/governance/logs/tool.log` に残し、PR / summary に同じmetadataを書かせる契約がない。

### 目的

`docs/governance/agent-ui-protocol-ssot.yaml` に従い、Agentが使う protocol UI tool を構築する。

tool は以下を満たす。

- Agentに task_name を入力させる。
- Agentに worktype 番号を選ばせる。
- worktype に応じた prompt を出す。
- 対象SSOT名を入力させる。
- SSOT section一覧を出す。
- `[abc,def]` のように複数section指定を許可する。
- 指定section配下だけを結合して出す。
- SSOT選択に戻るか quit できる。
- quit 時に scenario-contract を書かせる。
- tool使用idを発行し、`docs/governance/logs/tool.log` に datetime / uuid / task_name / worktype を記録する。
- PR / summary に datetime / uuid / task_name / worktype を書かせる。
- agent実装終了後、worktype別test / checklist interview / check を実行し、合格なら pass を出す。

### 改善方針

1. `docs/governance/agent-ui-protocol-ssot.yaml` を正本として読む。
2. `.agent/tools` に Agent-facing CLI を置く。
3. tool出力は必要最低限にし、実行log垂れ流しを禁止する。
4. prompt/protocol全文を無制限に出さず、worktype prompt と指定SSOT sectionのみ出す。
5. `docs/governance/logs/tool.log` は compact one-line evidence log とし、checklist本文やscenario-contract本文は保存しない。
6. tool は source mutation をしない。ただし usage log append はSSOT上の明示例外として扱う。
7. local test flow は worktype別test → checklist interview → check → pass の順にする。

### 対応資料

必ず読む:

- `AGENTS.md`
- `.agent/rules/rule.md`
- `.agent/README.md`
- `.agent/routes/worktype-required-protocols.yaml`
- `.agent/prompt/audit.md`
- `.agent/prompt/specific.md`
- `.agent/prompt/implementation-change.md`
- `.agent/prompt/design-change.md`
- `.agent/prompt/todo-maintenance.md`
- `.agent/prompt/existing-pr-update.md`
- `.agent/protocols/scenario-contract.md`
- `.agent/checklists/README.md`
- `.agent/docs/ssot-map.yaml`
- `docs/governance/agent-governance-routing-ssot.yaml`
- `docs/governance/agent-governance-routing-ssot.md`
- `docs/governance/agent-ui-protocol-ssot.yaml`

関連箇所は Agent 判断で追加調査・追加修正すること。

### 対象ファイル名

新規候補:

- `.agent/tools/agent-ui`
- `.agent/tools/agent_ui.py`
- `.agent/tests/check-agent-ui-route.sh`
- `.agent/tests/fixtures/agent-ui-route/*.txt` or `.yaml`
- `docs/governance/logs/tool.log`

変更候補:

- `.agent/docs/required-paths.yaml`
- `.agent/routes/worktype-required-protocols.yaml`
- `.agent/README.md`
- `AGENTS.md`
- `.agent/rules/rule.md`

既存 `.agent/tasks/todo.md` は触らない。

### 対象関数名 / コマンド名候補

- `main`
- `issue_usage_id`
- `append_tool_log`
- `select_worktype`
- `emit_worktype_prompt`
- `select_target_ssot`
- `list_ssot_sections`
- `emit_selected_sections`
- `parse_section_selection`
- `request_scenario_contract_on_quit`
- `run_worktype_tests`
- `run_checklist_interview`
- `run_checks`
- future command: `.agent/tools/agent-ui initial-contract`
- future command: `.agent/tools/agent-ui local-test`

### 実装scope

Bundle単位で処理する。小粒PR化は禁止。

このBundleで最低限完了させること:

- [ ] `AGENTS.md` / `.agent/rules/rule.md` から Agent が tool を使う導線を最小差分で追加する。
- [ ] `.agent/tools/agent-ui` を追加する。
- [ ] tool使用時に task_name を入力させる。
- [ ] tool使用時に worktype 番号を選ばせる。
- [ ] worktypeに応じた prompt を出す。
- [ ] 対象SSOT名を入力させる。
- [ ] 対象SSOTのsection一覧を出す。
- [ ] `[abc,def]` のような複数section指定を受け付ける。
- [ ] 指定section配下のみを結合して出す。
- [ ] SSOT選択へ戻る / quit を選べる。
- [ ] quit時に scenario-contract を書かせる。
- [ ] datetime / uuid / task_name / worktype を `docs/governance/logs/tool.log` にcompact記録する。
- [ ] PR / summary に datetime / uuid / task_name / worktype を書かせる出力契約を追加する。
- [ ] local-test flow として worktype別test → checklist interview → check → pass を実装する。
- [ ] 実行log垂れ流しを避け、tool出力を必要最低限にする。
- [ ] tool.log に checklist本文 / scenario-contract本文 / verbose実行logを保存しない。
- [ ] `.agent/tests/check-agent-ui-route.sh` または同等checkを追加する。

### OK軸

- Agent が `AGENTS.md` / `.agent/rules/rule.md` の導線から tool を使う。
- tool が uuid を発行し、`docs/governance/logs/tool.log` に datetime / uuid / task_name / worktype を残す。
- PR / summary に datetime / uuid / task_name / worktype が出る。
- tool出力が最小限で、実行logを垂れ流さない。
- worktype prompt と指定SSOT sectionだけを出せる。
- quit時に scenario-contract を要求する。
- local-test が worktype別test → checklist interview → check → pass の順で動く。
- `docs/governance/agent-ui-protocol-ssot.yaml` と矛盾しない。

### NG軸

- toolを使わせる導線が `AGENTS.md` / `.agent/rules/rule.md` にない。
- uuid / datetime / task_name / worktype の証跡が残らない。
- PR / summary に tool usage metadata が出ない。
- toolが実行logを長々とAgentに読ませる。
- SSOT全体を毎回dumpする。
- checklist本文やscenario-contract本文を `tool.log` に保存する。
- toolが implemented / partial / not_started 判定を行う。
- 既存 `.agent/tasks/todo.md` を更新する。

### Agent向け実行指示

以下を厳守すること。

1. `AGENTS.md` を読む。
2. `.agent/rules/rule.md` を読む。
3. `.agent/README.md` を読む。
4. `docs/governance/agent-ui-protocol-ssot.yaml` を正本として読む。
5. `.agent/tasks/agent-ui-protocol-implementation-todo.md` の Bundle `agent-ui-protocolization` を処理する。
6. 既存 `.agent/tasks/todo.md` は触らない。
7. 実装後、追加checkerと既存 required checks を実行する。
8. completion output に datetime / uuid / task_name / worktype / local-test結果 / checklist結果 / check結果を出す。
