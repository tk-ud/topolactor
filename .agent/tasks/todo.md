# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | 主 SSOT |
|-----------|------|--------|------|---------|
| `owner-decision-required-sso-audit` | SSO/Auth 監査 owner 判断待ち | partial | 2 | `docs/design/auth-db-session-credential-ssot.yaml`, `docs/design/admin-master-roster-management-ssot.yaml` |
| `auth-refresh-state-revalidation` | Auth refresh 状態再検証 gap | not_started | 1 | `docs/design/auth-db-session-credential-ssot.yaml`, `docs/design/admin-master-roster-management-ssot.yaml` |
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 1 | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |

---

## Bundle `owner-decision-required-sso-audit`

**Status:** partial  
**SSOT:** `docs/design/auth-db-session-credential-ssot.yaml`, `docs/design/admin-master-roster-management-ssot.yaml`  
**Audit date:** 2026-06-06

以下は設計判断が必要なため、Owner 方針が確定するまで実装 bundle に移さない。

### OD-2: `auth_users:update_state` が `auth_users:update` と同一ハンドラに dispatch
- **ファイル:** `backend/runtime/AdminRuntime.cs`
- **現状:** `"auth_users:update_state" => DataAuthUsersUpdateAsync(vector, ct)` で update と同じ。
- **SSOT 不整合:** `admin-master-roster-management-ssot.yaml` は `update` と `update_state` を別アクションとして列挙している。
- **判断待ち:** 意図的 alias なら SSOT の `admin_runtime_actions` から `update_state` を削除するか「alias to update」と注記する。別実装が必要なら state 列のみ変更可・username/password 変更不可の dedicated handler に分離する。

### OD-3: refresh token cookie の Secure フラグ方針
- **ファイル:** `backend/Program.cs#AppendRefreshCookie`
- **現状:** `HttpOnly; SameSite=Lax` のみ。`Secure` フラグなし。
- **SSOT 不整合:** `auth-db-session-credential-ssot.yaml` の `refresh_token_cookie` に `secure` 指定がない。
- **判断待ち:** デモ HTTP 前提なら SSOT に `secure: demo_http_only` を明記する。HTTPS 化を見据えるなら `Secure` を追加し SSOT も更新する。

---

## Bundle `auth-refresh-state-revalidation`

**Status:** not_started  
**SSOT:** `docs/design/auth-db-session-credential-ssot.yaml`, `docs/design/admin-master-roster-management-ssot.yaml`  
**Source:** reclassified from `owner-decision-required-sso-audit` OD-1 on 2026-06-07

SSOT 上、`active=false` / `approve=false` / `suspended` / suspension window は login denial condition であり、refresh token による新 JWT 発行経路で例外扱いする設計根拠はない。Owner 判断待ちではなく、SSOT に対する実装不足として扱う。

- [ ] `backend/service/AuthService.cs#RefreshAsync` は refresh token の realm/audience 検証後、新 access JWT 発行前に現在の `auth.users` 状態を再取得し、`EvaluateLoginState` 相当で fail-close する
- [ ] `backend/repository/AuthRepository.cs` / `backend/repository/NpgsqlAuthRepository.cs` / test fake repositories は refresh record から再検証に必要な user state を取得できるようにする
- [ ] state 再検証で拒否した場合は新 refresh token を発行せず、可能なら該当 refresh token/session を revoke する
- [ ] refresh state denial の回帰テストを追加する

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
