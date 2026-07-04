# Audit Tool Evidence Todo

## Bundle `audit-tool-evidence-observation`

**Status:** implemented  
**Worktype:** design_change  
**Primary SSOT:** `docs/governance/agent-ui-protocol-ssot.yaml`  
**Proof surface:** `.agent/tests/check-audit-tool-evidence.sh`, `.agent/tests/check-worktype-routing.sh`, `.agent/tests/check-structure.sh`

### 問題点

- worktype `audit` の出力契約に、対象PR / summary 上の既存 Agent UI tool 証跡を観測する欄が無い。
- 証跡が無い場合に、監査結果へ absent / not_applicable を分離して書く欄が無い。
- 監査が証跡生成者ではなく観測者である境界が明記されていない。

### 目的

Audit worktype のPR監査で、対象PR / summary に既存の Agent UI tool evidence があるかを観測結果として出力させる。

### 改善方針

- `.agent/prompt/audit.md` に `Agent UI tool evidence observed` 欄を追加する。
- `.agent/protocols/audit-tool-evidence.md` を観測専用protocolとして追加する。
- `.agent/routes/worktype-required-protocols.yaml` の audit required_protocols / required_checks に接続する。
- 監査は対象PRの証跡を生成・append・backfillしないことを明記する。

### 対応資料

- `AGENTS.md`
- `.agent/prompt/audit.md`
- `.agent/protocols/audit.md`
- `.agent/protocols/audit-tool-evidence.md`
- `.agent/routes/worktype-required-protocols.yaml`
- `docs/governance/agent-ui-protocol-ssot.yaml`

### 対象ファイル名

- `.agent/prompt/audit.md`
- `.agent/protocols/audit-tool-evidence.md`
- `.agent/routes/worktype-required-protocols.yaml`
- `.agent/tests/check-audit-tool-evidence.sh`

### 対象関数名 / route

- worktype `audit`
- `Agent UI tool evidence observed`
- `required_protocols.audit`
- `required_checks.audit`

### 実装scope

- [x] audit prompt required_reads に target-side Agent UI tool evidence observation を追加する。
- [x] audit output_shape に `Agent UI tool evidence observed` block を追加する。
- [x] audit tool evidence observation protocol を追加する。
- [x] audit routeへ新protocol/checkを接続する。
- [x] audit-time generation / append / backfill 禁止を明記する。

### OK軸

- [x] audit outputで evidence_present / observed_source / observed_fields / missing_fields / absence_reason が確認できる。
- [x] 証跡なしの場合も absent / not_applicable として観測結果を書ける。
- [x] 監査自身のtool使用を対象PR証跡として扱わない。
- [x] route mapから新protocol/checkへ到達できる。

### NG軸

- [ ] auditが対象PR証跡を生成・append・backfillできるように読める。
- [ ] audit Agent自身のtool使用を対象PR証跡として扱う。
- [ ] target-side evidence absence を semantic implemented / merge judgment と混同する。
- [ ] route mapへ接続せず protocol file だけ追加する。
