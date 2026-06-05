# CLI Model Context Protocols Port SSOT

## Purpose

CLI Model Context Protocols Port は、Topolactorの業務データを AI / shell / CLI から安全に検索・集計・搬出するための **read/export port surface** である。

- UI操作自動化ではない
- DB直接接続ではない
- commit / delete / payment approval / email send を行わない

---

## Core Invariant

以下の責務境界は不変条件として維持する。

### AI / CLI / MCP が実行できる操作

| 操作 | 説明 |
|------|------|
| read | 業務データの読み取り |
| search | 業務トポロジ内の検索 |
| aggregate | 集計・月次サマリ |
| analyze | 分析・パターン抽出 |
| validate | export前バリデーション |
| export | CSV / JSON / PDF / ZIP 生成 |
| stream_file | 許可されたexport jobのファイル取得 |
| create_export_job | 搬出ジョブの登録 |

### UI / Human のみが実行できる操作

| 操作 | 説明 |
|------|------|
| approve | 承認 |
| commit | 確定 |
| delete | 削除 |
| send_email | メール送信 |
| final_register | 最終登録 |
| payment_approval | 支払承認 |

> **Email send について:**
> email send は後段の別設計とし、本SSOTではout of scopeとする。
> 将来、UI catalog / backend dispatch / runtime の別SSOTまたは別設計で扱う。
> 現時点では CLI/MCP から email send を実行しない。

---

## Admin/UI Configuration

admin/contents 設計時に、物理テーブルまたは業務コンテンツ単位で CLI reader port をOpenできる設計にする。

admin/contents は「どの業務コンテンツを、どの粒度で、AI/shell/CLIに読ませるか」を設定する入口である。

### 設定項目

```yaml
cli_reader_port_enabled: true/false
allowed_roles: [role_id, ...]
allowed_users: [user_id, ...]
allowed_tables: [table_id, ...]
allowed_columns: [column_id, ...]
allowed_filters: {field: expression, ...}
allowed_periods: {from: date, to: date}
aggregation_windows: [month, quarter, year]
export_formats: [csv, json, pdf, zip]
file_stream_enabled: true/false
requires_ui_approval: true/false
expires_at: datetime
rate_limit: {requests_per_minute: n}
audit_required: true/false
```

---

## API Responsibility

API は以下を担う。CLI / MCP はDBを直接読まず、Context API / Data Reader のみを経由する。

- authentication
- authorization
- user_role_scope
- port_enabled チェック
- period_scope
- table_scope / column_scope / row_scope
- deadline / expires_at
- export_format
- file_stream_permission
- export_job_id / idempotency_key handling
- audit log write

---

## Data Reader Responsibility

Data Reader は、SSOTからCLI/MCP用の安全なread modelを生成する。

必須責務:
- query validation
- search result shaping
- aggregation
- monthly snapshot
- export package generation
- CSV / JSON / PDF / ZIP generation
- manifest generation
- checksum generation
- source_record_ids capture

---

## File Stream

ファイルストリームは、APIが許可したexport jobに対してのみ開放する。

**対象:**
- invoice_pdf
- receipt_image
- csv_export
- json_export
- monthly_zip_bundle
- manifest_json

**必須メタデータ:**

```yaml
export_job_id: uuid
source_record_ids: [uuid, ...]
generated_by: user_id
generated_at: datetime
period: {from: date, to: date}
checksum: sha256
manifest_version: semver
```

---

## Export Job / Manifest

CLI / MCPによる搬出処理は必ず export_job として記録する。

### export_job 必須項目

```yaml
export_job_id: uuid
port_id: uuid
requested_by: user_id
requested_at: datetime
period: {from: date, to: date}
target_scope: {tables: [...], filters: {...}}
export_format: csv|json|pdf|zip
status: pending|processing|completed|failed
source_record_ids: [uuid, ...]
generated_files: [path, ...]
checksum: sha256
manifest_path: path
completed_at: datetime|null
```

### manifest 必須項目

```yaml
manifest_version: semver
export_job_id: uuid
generated_at: datetime
generated_by: user_id
period: {from: date, to: date}
source_tables: [table_id, ...]
source_record_ids: [uuid, ...]
files: [{name: str, size: int, checksum: sha256}]
checksum: sha256
```

---

## MCP Surface

MCPはadmin configから公開可能なtools/resourcesを生成する。

> MCP surface は UI操作の自動化ではなく、AI/shell/CLIがTopolactorの業務トポロジを安全に読むための **read/export port surface** である。

### tools

| tool | 説明 |
|------|------|
| get_monthly_context | 月次コンテキスト取得 |
| search_records | レコード検索 |
| aggregate_records | 集計 |
| validate_export | export前バリデーション |
| create_export_job | 搬出ジョブ作成 |
| download_export_file | ファイルダウンロード |
| get_export_status | ジョブステータス確認 |

### resources

- `topolactor://monthly/{period}/context.json`
- `topolactor://exports/{export_job_id}/manifest.json`
- `topolactor://exports/{export_job_id}/file`

---

## Explicitly Out of Scope

以下は明示的に禁止境界として扱う:

| 禁止操作 | 理由 |
|----------|------|
| DB direct connection | API / Data Reader 経由必須 |
| direct SQL execution | query validation 必須 |
| record commit | UI/Human 境界 |
| delete operation | UI/Human 境界 |
| payment approval | UI/Human 境界 |
| email send | 後段別設計（out of scope） |
| browser UI automation | 本SSOTの対象外 |
| unauthorized bulk export | rate_limit / audit_required で制御 |
| permission bypass | 認証・認可は API 責務 |

---

## Audit Log

すべてのCLI/MCP accessを監査ログへ記録する。audit logは read/export 履歴を後から追跡できる設計とする。

| フィールド | 説明 |
|-----------|------|
| user_id | 実行ユーザー |
| role | ロール |
| client_type | ai / shell / cli / mcp |
| tool_name | 実行ツール名 |
| resource_uri | アクセスリソース |
| query_hash | クエリハッシュ（内容非保存） |
| period | 対象期間 |
| target_table | 対象テーブル |
| target_scope | スコープ |
| result_count | 結果件数 |
| export_job_id | 搬出ジョブID（搬出時） |
| executed_at | 実行日時 |
| ip_address | IPアドレス |
| user_agent | クライアント情報 |

---

## Parent SSOT

- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/db-schema.yaml`
