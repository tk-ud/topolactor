# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` のみ。

---

<!-- =========================================================
  OWNER DECISION REQUIRED — sso-implementation-audit (2026-06-06)
  以下 3 点は設計判断が必要なため実装待ち。Owner が方針を確定してから bundle に移すこと。
  =========================================================

  [OD-1] RefreshAsync でのログイン状態再検証 (未実装)
    ファイル: backend/service/AuthService.cs#RefreshAsync
    現状: refresh token の realm/audience のみ検証。
          EvaluateLoginState を呼ばないため、管理者が suspend/inactive にした後も
          既存 refresh token (7 日) が有効な限り新 JWT を発行し続ける。
    判断: 即時ブロックが必要なら RefreshAsync 内で EvaluateLoginState を呼ぶ。
          デモ範囲で許容するなら SSOT に「refresh では state 再検証しない」旨を明記する。

  [OD-2] auth_users:update_state が auth_users:update と同一ハンドラに dispatch (設計乖離)
    ファイル: backend/runtime/AdminRuntime.cs:273
    現状: "auth_users:update_state" => DataAuthUsersUpdateAsync(vector, ct) — update と同じ。
    SSOT: admin-master-roster-management-ssot.yaml は update と update_state を別アクションとして列挙。
    判断: 意図的 alias なら SSOT の admin_runtime_actions から update_state を削除するか
          「alias to update」と注記する。
          別実装（state 列のみ変更可・username/password 変更不可）が必要なら分離する。

  [OD-3] refresh token cookie に Secure フラグ未設定
    ファイル: backend/Program.cs AppendRefreshCookie()
    現状: HttpOnly; SameSite=Lax のみ。Secure フラグなし。
    SSOT: auth-db-session-credential-ssot.yaml の refresh_token_cookie に secure 指定なし。
    判断: デモ (HTTP) 前提なら SSOT に secure: demo_http_only と明記する。
          HTTPS 化を見据えるなら Secure を追加し SSOT も更新する。
========================================================= -->

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | 主 SSOT |
|-----------|------|--------|------|---------|
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル | not_started | 3 | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | not_started | 1 | `docs/system-roadmap.yaml`（参照のみ・正本ではない） |

---

## Bundle `sso-audit-fixes`

**Status:** not_started  
**SSOT:** `docs/design/auth-db-session-credential-ssot.yaml`, `docs/design/admin-master-roster-management-ssot.yaml`  
**Audit date:** 2026-06-06

### A: `AdminMasterRosterAudit.AppendAsync` — after フィールドにフル envelope を書き込むバグ
- **ファイル:** `backend/runtime/AdminMasterRosterAudit.cs:44-46`
- **不整合:** `after is null ? "{}" : json` の `json` は envelope 全体（actor / target_table / timestamp 等を含む）をシリアライズしたもの。`AppendLogsDiffAsync` の `after_state_or_diff_json` 引数には after オブジェクト単体を渡す必要がある。
- **修正:** `after is null ? "{}" : json` → `after is null ? "{}" : JsonSerializer.Serialize(after)`
- [ ] `AdminMasterRosterAudit.AppendAsync` の after 引数を `JsonSerializer.Serialize(after)` に修正

### B: `authApi.ts` に `logoutUser()` / `logoutAdmin()` が存在しない
- **ファイル:** `frontend/api/authApi.ts`
- **不整合:** SSOT `auth_runtime_actions.logout` は定義済み。プロキシ `POST /api/auth/logout` も存在するが、フロントエンド API クライアント関数がない。Island が直接 fetch を書くか、関数を追加する必要がある。
- [ ] `authApi.ts` に `logoutUser()` 関数を追加（`POST /api/auth/logout`）

### C: `authApi.ts` に `refreshAdminSession()` が存在しない
- **ファイル:** `frontend/api/authApi.ts`
- **不整合:** `refreshUserSession()` は実装済みだが、admin 向けの `refreshAdminSession()` がない。プロキシ `POST /api/super_auth/refresh` は存在する。
- [ ] `authApi.ts` に `refreshAdminSession()` 関数を追加（`POST /api/super_auth/refresh`）

### D: `DEMO_JWT_EXPIRY_HOURS` が事実上必須なのに `.env.example` では optional と記載
- **ファイル:** `backend/service/JwtTokenIssuer.cs:21-24`, `infra/.env.example:11`
- **不整合:** `ValidateConfiguration()` は `DEMO_JWT_EXPIRY_HOURS` が未設定 or 非正整数の場合に `AUTH_JWT_EXPIRY_NOT_CONFIGURED` を返す（ログイン不能）。一方 `.env.example` は `# Backend — optional (defaults shown)` と記載しており矛盾している。SSOT の `signing_key` セクションにもこの変数の記載がない。
- [ ] `.env.example` の `DEMO_JWT_EXPIRY_HOURS` コメントを `# required` に修正
- [ ] `auth-db-session-credential-ssot.yaml` の `signing_key` セクションに `DEMO_JWT_EXPIRY_HOURS` を必須として追記

### E: JWT session cookie の `Max-Age` が `DEMO_JWT_EXPIRY_HOURS` と連動していない
- **ファイル:** `frontend/lib/demoSession.ts:18`
- **不整合:** `DEFAULT_MAX_AGE_SEC = 60 * 60 * 24`（ハードコード 24h）。`DEMO_JWT_EXPIRY_HOURS` を変更してもフロントエンドの cookie 存続期間は変わらず、期限切れ JWT が cookie に残り続ける。SSR 有効性は `/auth/session` プローブで防いでいるため セキュリティ上の穴ではないが、設定の一貫性が損なわれる。
- [ ] `demoSession.ts` の `sessionTokenSetCookieHeader` に `expiryHours` 引数を追加し、`SuperAuthPanel` / `LoginManifestPanel` 側でログインレスポンスの expiry に合わせた値を渡せるようにする（または SSOT に「cookie max-age は JWT expiry と独立」を明記する）

---

## Bundle `future-external-bundle-gate`

**Status:** not_started  
**SSOT:** `docs/design/extended-runtime-bundle-registry-ssot.yaml`

- [ ] Notion/Sheets/Slack/GitHub/Webhook/REST-API-Connector/NoCode-Loop — 個別 SSOT 揃うまで実装しない

---

## Bundle `helper-manual`

**Status:** not_started  
**SSOT:** `docs/design/user-facing-helper-manual-ssot.yaml`

- [ ] helper/manual category 候補の実装設計
- [ ] Desktop AI / CLI / MCP Reader 向けライティング方針
- [ ] ヘルプコンポーネント実装（SSOT カテゴリ構造ゲート）

---

## Bundle `product-nocode-loop-acceptance`

**Status:** not_started

- [ ] `product.dynamic_support_nocode_loop` 手動受入（roadmap 追従）
