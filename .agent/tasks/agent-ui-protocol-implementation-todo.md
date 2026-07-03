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

現状の implementation protocol は、prompt / protocol / checklist / scenario-contract / required checks が個別に存在するが、Agent が 0→N の作業UIとして順序立てて踏む形になっていない。

特に以下が弱い。

- Scenario Contract は「作れ」「diff照合しろ」と書かれているが、Agent-facing route として必須UI化されていない。
- CheckLists は viewpoint template と checker script があるが、implementation_change の標準実行経路に gate として接続されていない。
- `.agent/tools` は read-only observation surface として定義済みだが、prompt/protocol/ssot-map/checklist から条件解決して route card を吐く Agent UI tool がない。
- Agent が protocol を読んで短期記憶で判断する形になっており、scope を route card に従って傾ける UI projection がない。

### 目的

prompt / protocol / checklist / ssot-map を正本として残したまま、`.agent/tools` に Agent UI CLI を構築し、条件に応じて以下を route card として吐けるようにする。

- worktype
- required reads
- target surfaces
- triggered protocols
- triggered checklists
- scenario-contract 要否
- substrate plan template
- required checks
- completion evidence skeleton
- missing evidence

Agent は tool が吐いた route card に従って scope を決め、実装・検証・completion output を行う。

### 改善方針

1. `docs/governance/agent-ui-protocol-ssot.yaml` を正本として読む。
2. Agent UI CLI は `.agent/tools` に置く。
3. tool は read-only とし、repo mutation / completion judgment / implemented 判定を行わない。
4. prompt / protocol 本文を tool に複製しない。tool は参照・条件解決・route card projection に限定する。
5. scripts/checkers の実体は `.agent/scripts` / `.agent/tests` / `.agent/checklists/check-*.sh` に残し、tool は必要に応じて command を提示または薄く呼び出す。
6. Scenario Contract / CheckLists / Required Checks を route card 上の evidence gate として表示する。
7. missing evidence がある場合、completion output に blocking として出す。

### 対応資料

必ず読む:

- `AGENTS.md`
- `.agent/rules/rule.md`
- `.agent/README.md`
- `.agent/prompt/design-change.md`
- `.agent/protocols/design-change.md`
- `.agent/prompt/implementation-change.md`
- `.agent/protocols/implementation-change.md`
- `.agent/protocols/scenario-contract.md`
- `.agent/protocols/policy-judgment.md`
- `.agent/checklists/README.md`
- `.agent/checklists/policy-judgment.md`
- `.agent/checklists/boundary-identity.md`
- `.agent/checklists/registry-tensor-projection-continuity.md`
- `.agent/docs/ssot-map.yaml`
- `.agent/routes/worktype-required-protocols.yaml`
- `docs/governance/agent-governance-routing-ssot.yaml`
- `docs/governance/agent-governance-routing-ssot.md`
- `docs/governance/agent-ui-protocol-ssot.yaml`

関連箇所は Agent 判断で追加調査・追加修正すること。

### 対象ファイル名

新規候補:

- `docs/governance/agent-ui-protocol-ssot.yaml`
- `.agent/tools/agent-ui`
- `.agent/tools/agent_ui.py`
- `.agent/tests/check-agent-ui-route.sh`
- `.agent/tests/fixtures/agent-ui-route/*.json` or `.yaml`

変更候補:

- `.agent/docs/required-paths.yaml`
- `.agent/routes/worktype-required-protocols.yaml`
- `.agent/prompt/implementation-change.md`
- `.agent/protocols/implementation-change.md`
- `.agent/protocols/scenario-contract.md`
- `.agent/checklists/README.md`
- `.agent/README.md`

既存 `.agent/tasks/todo.md` は触らない。

### 対象関数名 / コマンド名候補

- `main`
- `load_yaml_like`
- `load_route_map`
- `resolve_worktype`
- `discover_target_surfaces`
- `resolve_required_reads`
- `resolve_triggered_protocols`
- `resolve_triggered_checklists`
- `resolve_scenario_contract_gate`
- `emit_route_card`
- `emit_substrate_plan_template`
- `emit_completion_template`
- `validate_route_card`
- future command: `.agent/tools/agent-ui route`
- future command: `.agent/tools/agent-ui read-set`
- future command: `.agent/tools/agent-ui checklist`
- future command: `.agent/tools/agent-ui verify`

### 実装scope

Bundle単位で処理する。小粒PR化は禁止。

このBundleで最低限完了させること:

- [ ] `docs/governance/agent-ui-protocol-ssot.yaml` の内容に従い、Agent UI route card の schema / phase / evidence state を実装へ写像する。
- [ ] `.agent/tools/agent-ui` を追加し、Agent が実行できるCLI入口を用意する。
- [ ] CLI は `route` subcommand または同等の入口で、task text / optional changed files から route card を stdout に出す。
- [ ] CLI は `.agent/routes/worktype-required-protocols.yaml`, `.agent/docs/ssot-map.yaml`, prompt/protocol/checklist index を参照して、required reads / triggered protocols / triggered checklists / required checks を出す。
- [ ] CLI は repository mutation を行わない。
- [ ] CLI output は SSOT authority / proof authority / completion judgment / implemented status evidence ではない旨を route card に含める。
- [ ] Scenario Contract trigger が成立する場合、route card に `scenario_contract.required=true` と required fields / verification slot を出す。
- [ ] CheckList trigger が成立する場合、route card に checklist file / trigger reason / checker command / not_required reason slot を出す。
- [ ] implementation_change の route card で Substrate Plan template を出す。
- [ ] required checks は structure check last を維持して出す。
- [ ] missing evidence がある場合、completion skeleton に blocking として出す。
- [ ] `.agent/tests/check-agent-ui-route.sh` または同等の executable check を追加し、route card の必須キー欠落を検出する。
- [ ] `.agent/docs/required-paths.yaml` / `.agent/routes/worktype-required-protocols.yaml` / README 等、必要な governance route index を最小差分で更新する。

### OK軸

- Agent UI tool が prompt/protocol/checklist/ssot-map を条件解決し、Agent-facing route card として作業UIを投影している。
- tool は read-only で、repo mutation / semantic implemented 判定 / proof completion 判定を行わない。
- Scenario Contract / CheckLists / Required Checks が route card 上で evidence gate として明示される。
- Agent は route card に従って scope を傾け、missing evidence を completion output に出せる。
- protocol/prompt の本文複製ではなく、参照・条件解決・projection に留まっている。
- tests/checkers が route card の最低構造と必須keyを検査できる。
- `docs/governance/agent-ui-protocol-ssot.yaml` と `docs/governance/agent-governance-routing-ssot.yaml` の責務境界が矛盾しない。

### NG軸

- tool が SSOT authority / proof authority / implemented 判定を持つ。
- tool が repository file mutation を行う。
- prompt/protocol本文を tool 内へ長文複製する。
- Scenario Contract / CheckLists が route card に出ない。
- route card が単なる説明文で、required reads / triggers / evidence state / missing evidence を構造化していない。
- `.agent/tasks/todo.md` を更新する。
- 小粒の route 表示だけで checklist / scenario / required checks 接続を残す。
- structure check last を崩す。

### Agent向け実行指示

以下を厳守すること。

1. `AGENTS.md` を読む。
2. `.agent/rules/rule.md` を読む。
3. `.agent/README.md` を読む。
4. `docs/governance/agent-ui-protocol-ssot.yaml` を正本として読む。
5. `.agent/tasks/agent-ui-protocol-implementation-todo.md` の Bundle `agent-ui-protocolization` を処理する。
6. 既存 `.agent/tasks/todo.md` は触らない。
7. tool は read-only Agent UI projection として設計し、mutation / completion judgment / implemented 判定を持たせない。
8. 実装後、追加した checker と既存 required checks を実行する。
9. completion output に route card summary / missing evidence / check results を出す。
