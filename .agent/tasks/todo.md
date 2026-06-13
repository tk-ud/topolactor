# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `auth-projection-dispatch-claim-boundary` | Auth / 投影 / dispatch claim 境界 | not_started | 1 | `product.auth_projection_dispatch_claim_boundary` | `docs/design/auth-db-session-credential-ssot.yaml` |
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `product.external_optional_surface_bundle_gate` | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 1 | `product.dynamic_support_nocode_loop` | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |

---
---

## Bundle `auth-projection-dispatch-claim-boundary`

**Status:** not_started  
**SSOT:** `docs/design/auth-db-session-credential-ssot.yaml`  
**Supporting SSOT:** `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml`

問題点: projection login surface、JWT carrier lifecycle、refresh/probe、dispatch Authorization / claim authorization boundary が user realm 前提で結合されている。その結果、admin capability を持つ JWT が投影 top page で user mismatch / refresh mismatch により無効扱い・削除対象になり、投影 dispatch に Bearer が渡らず、backend dispatch 側の claim / capability authorization も不十分になる。

目的: login surface 分離と authority / capability 判定を直交させる。frontend は JWT carrier を保持し、API 呼び出し時に Bearer へ埋め込むだけにし、backend dispatch が JWT claim を正本として role / capability を判定する。

改善方針:
- [ ] `/auth` を user 固定 login ではなく projection login surface として扱い、admin user が `/auth` 経由でも admin capability を保持できるようにする
- [ ] top page / `ProjectionShell` の `expected=user` 固定 probe を廃止し、realm / audience mismatch を token 自体の失効として扱って JWT を削除しない
- [ ] refresh 失敗時の JWT 削除は invalid / expired / revoked など token 自体の失効に限定し、surface mismatch / capability mismatch と分離する
- [ ] `ProjectionShell` の初回 dispatch、SSE refresh dispatch、component dispatch で Bearer token を必ず渡す
- [ ] backend `/dispatch` は全 dispatch で JWT claim を読み、frontend request body の `role` を信用せず token claim で authoritative request role / capability を上書きする
- [ ] admin 判定を `operationType` / `target` 文字列 heuristic だけにせず、dispatch destination / manifest route / operation policy に基づいて required capability を判定する

対応資料:
- `docs/design/auth-db-session-credential-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`

対象ファイル名:
- `frontend/islands/ProjectionShell.tsx`
- `frontend/lib/demoSession.ts`
- `frontend/api/authApi.ts`
- `frontend/runtime/frontendScheduler.ts`
- `frontend/api/dispatch.ts`
- `frontend/routes/api/dispatch.ts`
- `backend/Program.cs`
- `backend/guard/JwtGuard.cs`
- `backend/endpoint/DispatchEndpoint.cs`
- `backend/runtime/ManifestDispatcher.cs`
- `backend/runtime/RuntimeExecutor.cs`
- related auth / dispatch tests

対象関数名:
- `ProjectionShell` `useEffect`
- `ensureValidClientSession`
- `clearSessionToken`
- `refreshUserSession`
- `probeSessionToken`
- `queueClientCommand`
- `dispatchOperation`
- `JwtGuard.Validate`
- `JwtGuard.ValidateForContext`
- backend `/dispatch` handler
- `DispatchEndpoint.HandleAsync`

---

## Bundle `future-external-bundle-gate`

**Status:** not_started
**SSOT:** `docs/design/extended-runtime-bundle-registry-ssot.yaml`

- [ ] Notion / Google Sheets / Slack / GitHub Issues / generic webhooks / external REST API connectors は、個別 SSOT と connector adapter contract が揃うまで optional external surface として実装しない（CSV/JSON admin import と M6 self-hosted no-code loop とは別 bundle）

---

## Bundle `helper-manual`

**Status:** not_started
**SSOT:** `docs/design/user-facing-helper-manual-ssot.yaml`

SSOT 上、helper/manual category candidates は実装ではなく方針整理。site page / UI component / help screen component 実装は explicitly out of scope。

- [ ] helper/manual category candidates を user promise / safety boundary / onboarding policy として整理する（ページ・コンポーネント実装はしない）
- [ ] Desktop AI / CLI / MCP Reader 向けに、plain business language と approval boundary のライティング方針を整理する

---

## Bundle `product-nocode-loop-acceptance`

**Status:** acceptance_pending  
**Roadmap/status SSOT:** `docs/system-roadmap.yaml`

実装 bundle ではなく、統合 UX の手動受入 / hand-debug evidence gap。runtime dispatch loop、ProjectionShell SSE refresh、recommend child island、SQL Attention feedback projection、admin CSV/JSON import、admin authoring routes は実装済みとして扱い、未実装扱いに戻さない。

- [ ] `product.dynamic_support_nocode_loop` の combined UX を、authoring guidance → SQL Attention feedback → M6 admin loop の通し手動受入 / hand-debug で確認する
