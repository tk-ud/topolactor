# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | 主 SSOT |
|-----------|------|--------|------|---------|
| `auth-users-update-state-action-alignment` | Auth users update_state action 整合 gap | not_started | 1 | `docs/design/admin-master-roster-management-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml` |
| `auth-refresh-cookie-secure-policy` | Auth refresh cookie Secure policy gap | not_started | 1 | `docs/design/auth-db-session-credential-ssot.yaml` |
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 1 | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |

---

## Bundle `auth-users-update-state-action-alignment`

**Status:** not_started  
**SSOT:** `docs/design/admin-master-roster-management-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`  
**Source:** reclassified from `owner-decision-required-sso-audit` OD-2 on 2026-06-07

`auth_users:update_state` は SSOT / runtime orchestration の action vocabulary に存在するが、現 frontend API / AdminUsersRoster は `auth_users:update` のみを呼び、backend dispatch も `update_state` を `DataAuthUsersUpdateAsync` に alias している。Owner 判断待ちではなく、未使用または曖昧な admin action vocabulary と runtime dispatch の SSOT 整合 gap として扱う。

- [ ] active manifest / frontend / tests の実使用を確認し、`auth_users:update_state` が不要なら `admin-master-roster-management-ssot.yaml` と `runtime-orchestration-ssot.yaml` の action vocabulary、および `backend/runtime/AdminRuntime.cs` の dispatch から削除する
- [ ] `auth_users:update_state` を残す必要がある場合は、SSOT に alias contract ではなく state-only contract を明記し、`username` 変更不可の dedicated handler / DTO / regression test を実装する

---

## Bundle `auth-refresh-cookie-secure-policy`

**Status:** not_started  
**SSOT:** `docs/design/auth-db-session-credential-ssot.yaml`  
**Source:** reclassified from `owner-decision-required-sso-audit` OD-3 on 2026-06-07

refresh token cookie は `HttpOnly; SameSite=Lax` まで実装・SSOT記載されているが、`Secure` policy が SSOT 未定義。現 backend は HTTP bind を前提に起動しているため、単純な `Secure` 固定実装はローカル/デモ HTTP の refresh cookie を壊す。Owner 判断待ちではなく、環境別 cookie policy を SSOT と実装で明文化する gap として扱う。

- [ ] `docs/design/auth-db-session-credential-ssot.yaml` の `refresh_token_cookie` に `secure` policy を追加し、HTTPS 環境では `Secure` 必須、local/demo HTTP では明示的な例外扱いであることを記述する
- [ ] `backend/Program.cs#AppendRefreshCookie` / `ClearRefreshCookie` は SSOT policy に合わせ、環境設定または request scheme に基づいて `Secure` 付与有無を明示的に分岐する
- [ ] refresh cookie policy の回帰テストまたは構造チェックを追加し、`HttpOnly` / `SameSite=Lax` / `Secure` policy が SSOT と実装で drift しないようにする

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
