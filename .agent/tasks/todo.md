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

## Demo Runtime Dispatch

- [ ] Public scaffold demo のログイン→dispatch→backend runtime emission 導線を閉じる
      → 現状は /demo の frontend-only 構造デモと、backend /dispatch /auth/login 実装は存在するが、JWT保存・Authorization送信・Fresh単体時の /api/dispatch proxy・推薦cold-start表示の扱いが未整理。
      → 目的: demoを「見るだけ」ではなく、ログイン後に dispatch panel からDB seeded runtime emission を確認できる操作デモへ寄せる。
      → 対象ファイル: frontend/api/dispatch.ts, frontend/islands/LoginPanel.tsx, frontend/islands/OperationPanel.tsx, frontend/routes/api/dispatch.ts, frontend/fresh.gen.ts, docs/demo-walkthrough.md
      → 対象関数: loginDemo, dispatchOperation, LoginPanel, OperationPanel, ContextRouteRecommendationResolver.ResolveAsync
      → 次の判断点: nginx経由を正とするか、Fresh単体 localhost:8000 でも /api/dispatch proxy を提供するか。recommendation は prefix cache 生成まで閉じるか、cold-start 表示を正式なdemo状態として明記するか。
