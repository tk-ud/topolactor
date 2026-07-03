# Audit Tool Evidence Todo

## Bundle `audit-tool-evidence-gate`

**Status:** implemented  
**Worktype:** design_change  
**Primary SSOT:** `docs/governance/agent-ui-protocol-ssot.yaml`  
**Proof surface:** `.agent/tests/check-audit-tool-evidence.sh`, `.agent/tests/check-worktype-routing.sh`, `.agent/tests/check-structure.sh`

### 問題点

- worktype `audit` の出力契約に Agent UI tool 使用証跡確認欄が無い。
- tool未使用時の fallback reason が audit contract で必須化されていない。

### 目的

Audit worktype のPR監査で Agent UI tool route/provenance evidence を確認・出力させる。

### 改善方針

- `.agent/prompt/audit.md` に Agent UI tool evidence 欄を追加する。
- `.agent/protocols/audit-tool-evidence.md` を追加する。
- `.agent/routes/worktype-required-protocols.yaml` の audit required_protocols / required_checks に接続する。

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
- `Agent UI tool evidence checked`
- `required_protocols.audit`
- `required_checks.audit`

### 実装scope

- [x] audit prompt required_reads に Agent UI tool evidence を追加する。
- [x] audit output_shape に `Agent UI tool evidence checked` block を追加する。
- [x] audit tool evidence protocol を追加する。
- [x] audit routeへ新protocol/checkを接続する。
- [x] fallback reason を必須出力対象にする。

### OK軸

- [x] audit outputで tool_used / metadata / reference_basis / tool_log_entry_checked / fallback_reason が確認できる。
- [x] tool証跡なしの場合も fallback reason を明示させる。
- [x] route mapから新protocol/checkへ到達できる。

### NG軸

- [ ] audit outputから tool evidence block を省略できる。
- [ ] fallback reason なしで tool未使用を許す。
- [ ] route mapへ接続せず protocol file だけ追加する。
