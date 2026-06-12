# Topolactor phase model note

This note is conceptual guidance, not an executable SSOT contract.
It does not define CI pass/fail criteria, runtime behavior, implementation completion, or governance authority.

## Horizontal projection and attractor convergence

Topolactor distinguishes horizontal projection surfaces from attractor-direction convergence surfaces.

- Frontend is the horizontal projection surface.
  It expands user input, symptoms, candidates, draft operations, and visible choices.
  It does not own convergence judgment or persistence authority.

- Backend is the attractor-direction convergence surface.
  It receives horizontally expanded intent and converges it through validation, dispatch,
  apply, promotion, audit judgment, and explicit runtime authority boundaries.

- Physical DB tables are convergence points.
  They are where state, evidence, history, constraints, and concrete records finally settle.

- Hub and registry are maps and guidance surfaces.
  They provide relation maps, candidate axes, topology directions, and navigational context.
  They are not themselves governance.

- SSOT is an abstract mapping.
  It describes how the real system should be interpreted, constrained, and projected.

- Implementation is the real.
  It is the actual running materialization of the SSOT mapping.

## Governance boundary

A reference graph can improve observability and navigation, but the graph itself is not governance.

Governance exists in convergence boundaries:

- SSOT contracts
- backend validation
- runtime authority
- preview / validate / apply gates
- promotion boundaries
- audit protocols
- physical persistence constraints

The graph can show where things are related.
It cannot decide whether a candidate should converge, be persisted, promoted, or rejected.

## Phase-origin observation and downstream convergence flow

Phase, attention, logs, registry tensor, hub, and topology are upstream observation and guidance axes.
They observe system state, pressures, and candidate directions.
They do not themselves decide convergence or mutate canonical state.

The canonical flow is:

1. Phase-origin observation: phase / attention / logs / registry tensor / hub / topology observe candidate directions and pressures.
2. Downstream projection and authoring: UIBuilder and frontend surfaces receive and display candidates for author review.
3. Convergence boundary: backend validation, runtime dispatch, preview / validate / apply, and promotion boundaries judge convergence.
4. Persistence point: physical DB tables record the settled state as concrete, auditable evidence.

SQL Attention, recommendation pressure, and logs observation are observation and recommendation basis layers.
They must not automatically execute fixed-route mutations, topology mutations, or registry mutations.
Any candidate that becomes actionable requires explicit user action or explicit admin-approved apply.

## Cross-SSOT reference map and UIBuilder reference graph boundary

A cross-SSOT reference map or UIBuilder reference graph improves observability and navigation across surfaces.
It helps identify which surfaces are related and which candidate expansions are available.
It does not judge whether a candidate should converge, be persisted, promoted, or rejected.
The map or graph itself holds no governance authority.

Governance authority over convergence decisions exists exclusively in:

- SSOT contracts (allowed vocabulary, shape, relation)
- backend validation and runtime dispatch
- preview / validate / apply gates
- promotion boundaries
- audit protocols
- physical DB persistence constraints

The reference graph can show where things are related.
It cannot decide whether a candidate should converge, be persisted, promoted, or rejected.

## Japanese note

このノートは概念補助であり、実行可能な SSOT 契約ではありません。
Frontend は候補や選択肢を水平展開する投影面であり、収束判断や永続化権限は持ちません。
Backend は validation / dispatch / apply / promotion / audit judgment を通じてアトラクタ方向へ収束させる面です。
物理 DB テーブルは状態・証跡・履歴・制約・具体レコードが最終的に定着する収束点です。
Hub / registry は地図・指針・候補軸であり、統治権限そのものではありません。
参照グラフは観測・参照・展開を助けますが、governance ではありません。
Governance は SSOT 契約、backend validation、runtime authority、preview / validate / apply、promotion boundary、audit protocol、DB 制約にあります。
Phase / attention / logs / registry tensor / hub / topology は上流の観測・指針・参照軸であり、収束判断・永続化権限・topology mutation authority を持ちません。
SQL Attention / recommendation / logs pressure は観測と推薦の根拠層であり、fixed route / topology / registry の自動変更を実行してはなりません。
cross-SSOT reference map や UIBuilder reference graph は観測・参照・navigation を助けますが、それ自体が収束判断を下す governance authority ではありません。
