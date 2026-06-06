# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` のみ。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | 主 SSOT |
|-----------|------|--------|------|---------|
| `owner-decision-required-sso-audit` | SSO/Auth 監査 owner 判断待ち | partial | 3 | `docs/design/auth-db-session-credential-ssot.yaml`, `docs/design/admin-master-roster-management-ssot.yaml` |
| `sso-audit-fixes` | SSO/Auth 監査 follow-up 修正 | not_started | 5 | `docs/design/auth-db-session-credential-ssot.yaml`, `docs/design/admin-master-roster-management-ssot.yaml` |
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | not_started | 1 | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |

---

## Bundle `owner-decision-required-sso-audit`

**Status:** partial  
**SSOT:** `docs/design/auth-db-session-credential-ssot.yaml`, `docs/design/admin-master-roster-management-ssot.yaml`  
**Audit date:** 2026-06-06

以下は設計判断が必要なため、Owner 方針が確定するまで実装 bundle に移さない。

### OD-1: `RefreshAsync` でのログイン状態再検証
- **ファイル:** `backend/service/AuthService.cs#RefreshAsync`
- **現状:** refresh token の realm/audience のみ検証。`EvaluateLoginState` を呼ばないため、管理者が suspend/inactive にした後も既存 refresh token（7日）が有効な限り新 JWT を発行し続ける。
- **判断待ち:** 即時ブロックが必要なら `RefreshAsync` 内で `EvaluateLoginState` を呼ぶ。デモ範囲で許容するなら SSOT に「refresh では state 再検証しない」旨を明記する。

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

## Bundle `sso-audit-fixes`

**Status:** not_started  
**SSOT:** `docs/design/auth-db-session-credential-ssot.yaml`, `docs/design/admin-master-roster-management-ssot.yaml`  
**Audit date:** 2026-06-06

### A: `AdminMasterRosterAudit.AppendAsync` — after フィールドにフル envelope を書き込むバグ
- **ファイル:** `backend/runtime/AdminMasterRosterAudit.cs`
- **不整合:** `after is null ? "{}" : json` の `json` は envelope 全体（actor / target_table / timestamp 等を含む）をシリアライズしたもの。`AppendLogsDiffAsync` の `after_state_or_diff_json` 引数には after オブジェクト単体を渡す必要がある。
- **修正:** `after is null ? "{}" : json` → `after is null ? "{}" : JsonSerializer.Serialize(after)`
- [ ] `AdminMasterRosterAudit.AppendAsync` の after 引数を `JsonSerializer.Serialize(after)` に修正

### B: `authApi.ts` に `logoutUser()` が存在しない
- **ファイル:** `frontend/api/authApi.ts`
- **不整合:** SSOT `auth_runtime_actions.logout` は定義済み。プロキシ `POST /api/auth/logout` も存在するが、フロントエンド API クライアント関数がない。
- **境界:** admin 専用 logout route は現行 SSOT で独立定義されていないため、`logoutAdmin()` は owner 判断なしに追加しない。
- [ ] `authApi.ts` に `logoutUser()` 関数を追加（`POST /api/auth/logout`）

### C: `authApi.ts` に `refreshAdminSession()` が存在しない
- **ファイル:** `frontend/api/authApi.ts`
- **不整合:** `refreshUserSession()` は実装済みだが、admin 向けの `refreshAdminSession()` がない。プロキシ `POST /api/super_auth/refresh` は存在する。
- [ ] `authApi.ts` に `refreshAdminSession()` 関数を追加（`POST /api/super_auth/refresh`）

### D: `DEMO_JWT_EXPIRY_HOURS` が事実上必須なのに `.env.example` では optional と記載
- **ファイル:** `backend/service/JwtTokenIssuer.cs`, `infra/.env.example`, `docs/design/auth-db-session-credential-ssot.yaml`
- **不整合:** `ValidateConfiguration()` は `DEMO_JWT_EXPIRY_HOURS` が未設定 or 非正整数の場合に `AUTH_JWT_EXPIRY_NOT_CONFIGURED` を返す（ログイン不能）。一方 `.env.example` は `# Backend — optional (defaults shown)` と記載しており矛盾している。SSOT の `signing_key` セクションにもこの変数の記載がない。
- [ ] `.env.example` の `DEMO_JWT_EXPIRY_HOURS` コメントを required 側へ移す
- [ ] `auth-db-session-credential-ssot.yaml` の `signing_key` セクションに `DEMO_JWT_EXPIRY_HOURS` を必須として追記

### E: JWT session cookie の `Max-Age` と `DEMO_JWT_EXPIRY_HOURS` の関係が未定義
- **ファイル:** `frontend/lib/demoSession.ts`, `backend/schema/AuthContracts.cs`, `docs/design/auth-db-session-credential-ssot.yaml`
- **不整合:** `DEFAULT_MAX_AGE_SEC = 60 * 60 * 24`（ハードコード24h）。`DEMO_JWT_EXPIRY_HOURS` を変更してもフロントエンドの cookie 存続期間は変わらない。一方、現行 `LoginResponseDto` / `RefreshResponseDto` は expiry を返していないため、ログインレスポンスに合わせる実装方針は未確定。
- **設計選択:** 次のいずれかを選ぶ。1) backend response に `expiresAt` / `expiryHours` を追加して cookie `Max-Age` と連動する。2) frontend が JWT `exp` を読み cookie `Max-Age` を導出する。3) SSOT に「JWT session cookie `Max-Age` は access JWT expiry と独立」と明記する。
- [ ] cookie `Max-Age` と JWT expiry の関係を SSOT で確定し、その方針に合わせて実装または明記する

---

## Bundle `future-external-bundle-gate`

**Status:** not_started  
**SSOT:** `docs/design/extended-runtime-bundle-registry-ssot.yaml`

- [ ] Notion / Google Sheets / Slack / GitHub Issues / generic webhooks / external REST API connectors / `dynamic_support_nocode_loop_bundle` は、個別 SSOT が揃うまで実装しない

---

## Bundle `helper-manual`

**Status:** not_started  
**SSOT:** `docs/design/user-facing-helper-manual-ssot.yaml`

SSOT 上、helper/manual category candidates は実装ではなく方針整理。site page / UI component / help screen component 実装は explicitly out of scope。

- [ ] helper/manual category candidates を user promise / safety boundary / onboarding policy として整理する（ページ・コンポーネント実装はしない）
- [ ] Desktop AI / CLI / MCP Reader 向けに、plain business language と approval boundary のライティング方針を整理する

---

## Bundle `product-nocode-loop-acceptance`

**Status:** not_started  
**Roadmap/status SSOT:** `docs/system-roadmap.yaml`

- [ ] `product.dynamic_support_nocode_loop` の `manual_acceptance_confirms_authoring_guidance_sql_attention_feedback_and_m6_admin_loop_as_one_ux` を手動受入 / hand-debug で確認する
