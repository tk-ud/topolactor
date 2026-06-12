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

## Japanese note

このノートは概念補助であり、実行可能な SSOT 契約ではありません。
Frontend は候補や選択肢を水平展開する投影面であり、収束判断や永続化権限は持ちません。
Backend は validation / dispatch / apply / promotion / audit judgment を通じてアトラクタ方向へ収束させる面です。
物理 DB テーブルは状態・証跡・履歴・制約・具体レコードが最終的に定着する収束点です。
Hub / registry は地図・指針・候補軸であり、統治権限そのものではありません。
参照グラフは観測・参照・展開を助けますが、governance ではありません。
Governance は SSOT 契約、backend validation、runtime authority、preview / validate / apply、promotion boundary、audit protocol、DB 制約にあります。
