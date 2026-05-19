# Optional / Future / Implemented Boundary Index

このドキュメントは、実装境界（implemented）と拡張境界（optional / future）を横断的に確認するための SSOT 補助インデックス。

## Context Route Recommendation

- Source: `docs/design/context-route-recommendation.md`
- Source: `docs/design/context-route-recommendation.yaml`

### optional
- クラスタリング（optional）。
- exploration slot（optional）。
- Bollinger band drift/spike 検出（optional）。
- shell script adapter を recommendation CI の必須主体にはしない（optional allowlisted adapter）。

### future / not yet implemented
- topology edit diff を SQL Attention query source に使う拡張は `topology_edit_log` / `entity_edit_log` 実装後。
- topology 連続性シナリオの CI 監査自動ゲート化は設計方針として定義済みだが未完。
- optional extension のうち `status: future_extension` / `status: not_yet_implemented` の項目。

### implemented boundary notes
- recommendation runtime は DB topology observation を意味 SoT とし、optional/future を実装済み扱いしない。
- 未実装 policy は TODO 明示が必要で、完了宣言禁止。

## Topology Recommendation CI Runtime

- Source: `docs/design/topology-recommendation-ci-runtime.md`
- Source: `docs/design/topology-recommendation-ci-runtime.yaml`

### optional
- shell script external check adapter（allowlisted）

### future / not yet implemented
- `backend/runtime/TopologyRecommendationCIRunner.cs` は「対応実装予定（未実装）」。

### implemented boundary notes
- CI runtime 文書は「方針定義」と「実装完了」を分離して扱う。

## Relation Registry FK Audit Migration

- Source: `docs/design/relation-registry-fk-audit-and-abstract-migration.md`

### future / not yet implemented
- 当該設計文書は policy/wiring scope のみで runtime 実装は future work。

## File Structure Semantics

- Source: `docs/file-structure.yaml`

### optional
- `optional_text_input` は optional capability。
- operation log note の manual text は optional。

## Governance Rule

- optional / future / implemented 境界を更新する際は、対象ドキュメントに加えて本インデックスも更新する。
- optional/future を implemented 扱いする記述は禁止。
