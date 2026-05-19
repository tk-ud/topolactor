# Agent Task List — Remaining TODO

このファイルは agent task surface として使用する。

完了済み作業・PR修正履歴・旧方針の残骸は残さない。
未完了の task がある場合のみ、次の形式で追加する。

```md
## <Area>

- [ ] <具体的な未完了作業>
      → <理由・対象ファイル・次の判断点>
```

## Current TODO

## Registry Tensor Continuity

- [ ] [Codex] registry tensor projection surface の実装乖離点検（runtime / endpoint / scheduler / function / UI topology）
      → 問題点: SSOT と監査Policyの明文化後、実装側で surface 間の意味連続性が崩れる余地がある。
      → 目的: projection/expansion surface ごとの drift を点検し、必要なら別PRで是正する。
      → 対象ファイル候補: docs/design/*, backend/runtime/*, frontend/*, db/*（点検のみ）
      → 次の判断点: 点検チェックリスト化の要否を判断。

- [ ] [Codex] Implement package-generator runtime/endpoint wiring for ui_component_bucket -> ui_topology_tensor persistence (tracked after SSOT/schema alignment).
