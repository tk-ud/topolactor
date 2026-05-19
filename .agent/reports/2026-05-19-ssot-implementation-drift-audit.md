# SSOT Implementation Drift Audit (2026-05-19)

## Scope

Read-only governance audit to compare SSOT semantics and current implementation around Context Route Recommendation and Topology Vector Runtime boundaries.

In-scope:
- SSOT semantic alignment (subject/ownership boundaries, canonical runtime route, fail-close intent, SQL Attention semantics)
- Implementation drift / meaning drift / stale-spec residue detection
- Governance gap classification for Claude follow-up implementation readiness

Out-of-scope:
- Runtime/backend/frontend/db implementation modification
- `.agent/tasks/todo.md` edits
- CI structural conformance judgments beyond explicit required declaration

## Required Check Scope Declaration

Changed scope in this audit PR: report-only (`.agent/reports/` new file).

Check inventory:
- `bash .agent/checklists/check-policy-judgment.sh` → NOT_REQUIRED (report-only drift audit; no runtime/persistence mutation and no policy checklist answer-set update in this task)
- runtime/backend/frontend/db tests (`dotnet test`, integration checks) → OUT_OF_SCOPE (no implementation file changes)
- `bash .agent/tests/check-structure.sh` → REQUIRED_EXECUTED (required gate; executed last)

Failure triage:
- No failed commands observed in this audit execution.

## SSOT Sources Reviewed

- `AGENTS.md`
- `README.md`
- `.agent/rules/rule.md`
- `.agent/protocols/completion.md`
- `.agent/protocols/reports-and-todos.md`
- `.agent/tasks/todo.md`
- `docs/design/context-route-recommendation.md`
- `docs/design/context-route-recommendation.yaml`
- `docs/design/topology-recommendation-ci-runtime.md`
- `docs/registrar-admin-ui-specification.md`
- `docs/framework-policy.yaml`
- `docs/file-structure.yaml`
- `.agent/reports/2026-05-19-claude-boundary-audit-reaudit.md`

## Implementation Files Reviewed

- `backend/runtime/ContextRouteRecommendationResolver.cs`
- `backend/runtime/TopologyVectorRuntime.cs`
- `backend/repository/ContextRouteRepository.cs`
- `backend/repository/NpgsqlContextRouteRepository.cs`
- `backend/schema/*` (catalog review)
- `backend/tests/Topolactor.Runtime.Tests/ContextRouteRecommendationResolverTests.cs`
- `db/context_route_tables.sql`
- `db/topology_tables.sql`
- `db/seed_empty.sql`
- `frontend/routes/*`
- `frontend/islands/*`

## Drift Findings

### F1. fail-closeとLogError継続の境界が未確定（継続）
- Evidence: resolver内で append/transition stats/TVR extension の例外を `LogError` して継続する分岐が残る。
- Semantic risk: fail-close / ExplicitError 方針と「非致命継続」方針の公開ルール面が未統一。
- Classification: **BLOCKING**（Claude実装PR前提の境界仕様不確定）

### F2. function_parameters駆動方針は概ね維持されるが、一部値カテゴリのDB固定化が残る
- Evidence: policy JSON は `function_parameters` から解決される一方、`scope_limit` / `candidate_kind` / `feedback_kind` がDB CHECKで列挙固定。
- Semantic risk: SSOTの可変policy境界と schema migration 前提が衝突。
- Classification: **GAP**

### F3. SQL Attention の主語は実装上保持されている
- Evidence: TopologyVectorRuntime は cosine + relation/stat/EMA/feedback 合成、evidence/mlp feature保存、hub attention current 更新を実装。
- Note: 推薦UIだけに矮小化せず、evidence軸/重み軸を保持。
- Classification: **PASS**

### F4. append-only と materialized current の意味境界は維持
- Evidence: `context_hub_feedback_event` append-only、`context_hub_recommendation_current` rebuildable current/非正本コメント・処理が一致。
- Classification: **PASS**

### F5. currentをSoT化していない点は整合
- Evidence: SQLコメント/設計文脈ともに current 非正本が明記され、append log 由来再構築を前提化。
- Classification: **PASS**

### F6. enabled=false / policy missing / policy invalid の明示化は維持
- Evidence: policy not found/invalid/state_policy invalid/ref invalid で explicit error を返す経路あり。enabled=falseはno-op分岐。
- Caveat: no-opの観測可能性（status metadata）は薄い。
- Classification: **GAP**

### F7. frontendへの topology/cosine/MLP/feedback 判定漏れは原則なし
- Evidence: frontendは投影/UIと検査画面が中心で、推論本体判定はbackend側。
- Classification: **PASS**

### F8. optional/future extension 誤記の顕著な逆転は限定的
- Evidence: docs上の optional/future 宣言は概ね保たれる。
- Caveat: 実装済み/未実装の境界の可視化が分散し監査コストが高い。
- Classification: **TODO**

### F9. registry validation と duplicate key validation の混同は限定
- Evidence: registry vector validation は cosine判定種別を分離して返却。
- Caveat: 用語上「duplicate」の意味（完全一致 vs 近接）の運用ガイド補強余地あり。
- Classification: **GAP**

### F10. Topology MLP / Feedback update の意味誤解リスク
- Evidence: 実装は feature crossing + weighted transform / delta補正でありNN/BP未実装。
- Risk: コメント・命名から「MLP=NN学習」と読まれる余地が残る。
- Classification: **TODO**

### F11. 旧仕様残骸・二重方針
- Evidence: 「no silent fallback」主張と一部 log-and-continue 実装が並存し、仕様読解時に衝突。
- Classification: **BLOCKING**

## Governance Gaps

1. Recoverable boundary（非致命継続）を規定する正式ガバナンス面が不足。
2. fail-close/ExplicitError基準の適用粒度（必須停止点と継続許容点）が規約化不足。
3. policy可変値のDB CHECK固定化がSSOTの可変設計と衝突しうる。
4. 実装済み/将来拡張の境界表示が散在し、監査時に意味ドリフト判定コストが高い。

## Proposed Governance Improvements

1. Recoverable boundary 定義を protocol surface に追加し、operation別に「fail-close必須 / non-fatal許容」を明示。
2. ContextRouteRecommendationResult または runtime status metadata に non-fatal side-effect failure の観測面を追加し、log-only依存を脱却。
3. `scope_limit` / `candidate_kind` / `feedback_kind` の可変方針について、DB制約レイヤとfunction_parametersレイヤの責務分離指針を設計ノート化。
4. SSOTに「implemented / optional / future」マトリクスを単一表で追加し、誤読余地を減らす。

## Claude Follow-up Prompt Suggestions

あり（以下を推奨）:

1. 「fail-close原則とnon-fatal side-effectの整合を取るため、ContextRouteRecommendationResolverの append/TVR extension 失敗を explicit status metadata へ昇格するか、recoverable-boundary protocolに正式化してください。実装とテストを同時更新し、log-only継続を監査可能にしてください。」
2. 「`context_hub_*` テーブルのCHECK制約とfunction_parameters可変方針の衝突を評価し、制約維持/緩和/分離の移行案を提示してください。少なくとも scope_limit と candidate_kind の拡張時運用を明文化してください。」
3. 「ContextRouteRecommendationResolverTests に persistence failure / repository unavailable / TVR extension failure の境界テストを追加し、期待結果（ExplicitErrorかnon-fatal継続か）を仕様として固定してください。」

## Remaining TODO Suggestions

- [ ] Recoverable boundary の正式プロトコル化（fail-close適用境界の明文化）
- [ ] non-fatal side-effect failure の explicit observable surface 追加
- [ ] DB CHECK固定値と policy variability の整合設計（移行指針作成）
- [ ] failure matrix（特に persistence constraint / backend unavailable）の実テスト拡張
- [ ] optional/future/implemented 状態のSSOT横断一覧化

## Completion Eligibility

- この監査は static governance audit（read-only semantic alignment review）。
- 実装修正は未実施（要求どおり）。
- 監査レポート作成と required structure check 実行により、今回タスクスコープとしては completion-eligible。
- ただし実装側の意味乖離（BLOCKING/GAP）は後続実装で解消が必要。

## Classification

**総合判定: BLOCKING**

理由:
- fail-close原則とlog-and-continue境界の未確定が、Claudeの次実装PR前提としてリスク。
- 一方でSQL Attention主語・append-only/current境界など中核SSOT整合は維持されるため、全面崩壊ではなく「境界規約の未収束」が主因。
