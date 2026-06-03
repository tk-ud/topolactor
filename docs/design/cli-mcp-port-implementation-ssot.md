# CLI/MCP Port Implementation SSOT

## Purpose

CLI/MCP Port implementation SSOT。Data Reader / Context API / export job DB schema / MCP tool-resource wiring / audit log persistence の実装境界を固定する。

[cli-model-context-protocols-port-ssot.yaml](./cli-model-context-protocols-port-ssot.yaml) が定義する設計境界を、実装単位に分解したSSOT。

---

## CLI/MCP の許可操作と禁止操作

### 許可操作

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

### 禁止操作

| 禁止操作 | 理由 |
|----------|------|
| direct DB connection | Context API / Data Reader 経由必須 |
| direct SQL execution | query validation 必須 |
| record commit | UI/Human 境界 |
| delete operation | UI/Human 境界 |
| payment approval | UI/Human 境界 |
| email send | 後段別設計（out of scope） |
| browser UI automation | 本SSOTの対象外 |
| unauthorized bulk export | rate_limit / audit_required で制御 |
| permission bypass | 認証・認可は Context API 責務 |
| credential read/export | Secret/Credential Bundle が管理 |
| approval execution | Audit/Approval Bundle / UI/Human のみ |

---

## Data Reader 境界

Data Reader は SSOT / manifest / registry / schema から CLI/MCP 用の安全な read model を生成する。DB を直接読む抜け道ではない。Context API / authorized read model 経由に限定する。

### 責務

- query validation
- search result shaping
- aggregation
- monthly snapshot
- export package generation
- CSV / JSON / PDF / ZIP generation
- manifest generation
- checksum generation
- source_record_ids capture

### 禁止

- direct DB connection
- direct SQL execution
- Context API 認可バイパス
- manifest なしの export

---

## Context API 境界

Context API は認証・認可・scope制御を所有する。CLI / MCP は Context API のみを経由してデータにアクセスする。

### 責務

- authentication
- authorization
- user_role_scope
- port_enabled チェック
- テーブル / カラム / 行 / 期間 scope
- export format 検証
- file stream permission
- export_job idempotency
- audit log write

### 禁止

- CLI/MCP direct DB connection
- CLI/MCP direct SQL execution
- 暗黙的アクセス許可
- サイレントスコープバイパス

---

## export job DB Schema 境界

export job は CLI/MCP の搬出操作を必ず記録する永続境界。

### 必須フィールド

```yaml
export_job_id: uuid
port_id: uuid
requested_by: user_id
requested_at: datetime
period: {from: date, to: date}
target_scope: {tables: [...], filters: {...}}
export_format: csv|json|pdf|zip
status: pending|processing|awaiting_approval|approved|rejected|completed|failed
source_record_ids: [uuid, ...]
generated_files: [path, ...]
checksum: sha256
manifest_path: path
completed_at: datetime|null
idempotency_key: uuid
approval_required: boolean
approval_status: not_required|pending|approved|rejected
```

### approval 境界

approval_required=true の場合、approval は Audit/Approval Bundle 経由の UI/Human explicit action のみ。CLI/MCP から approval を実行することは禁止。CLI/MCP は create_export_job まで。

---

## File Stream 境界

ファイルストリームは、Context API が許可した export job に対してのみ開放する。

**対象ファイル種別:**
- invoice_pdf
- receipt_image
- csv_export
- json_export
- monthly_zip_bundle
- manifest_json

**必須メタデータ:**
- export_job_id
- source_record_ids
- generated_by
- generated_at
- period
- checksum
- manifest_version

export_job 外の direct file stream は禁止。

---

## MCP Surface

MCP surface は UI automation ではない。Topolactorの業務トポロジを安全に読むための **read/export port surface** である。

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

すべての tool / resource は Context API 認証・audit log 記録が必須。

---

## Audit Log 境界

すべての CLI/MCP access を監査ログへ記録する。audit log は read/export 履歴を後から追跡できる永続境界。audit log write は Context API が所有する。

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

audit log write 失敗は fail close。サイレントスキップ禁止。

---

## Bundle 境界

### File/Storage Bundle

CLI/MCP file stream は File/Storage Bundle が担う authorized export job 経由の経路を使う。export_job 外の direct file stream は禁止。checksum / manifest は File/Storage Bundle が管理する。

### Export/SFTP Bundle

CLI/MCP は export_job の作成・ファイル取得まで。SFTP push / 外部搬出は Export/SFTP Bundle の責務。CLI/MCP は直接 SFTP push を実行しない。

### Audit/Approval Bundle

approval は Audit/Approval Bundle 経由の UI/Human explicit action のみ。CLI/MCP は create_export_job まで。approval 実行は UI/Human 境界。

### Secret/Credential Bundle

credential 実体は CLI/MCP から読ませない。Secret/Credential Bundle が credential 管理を担い、CLI/MCP への経路を提供しない。

---

## Out of Scope

- approval execution（CLI/MCP ポートの対象外）
- record commit
- delete operation
- payment approval
- email send
- credential read/export
- UI automation
- direct DB connection
- direct SQL execution

---

## Parent SSOT

- `docs/design/cli-model-context-protocols-port-ssot.yaml` （設計境界の親SSOT）
- `docs/design/runtime-bundle-file-storage-ssot.yaml`
- `docs/design/runtime-bundle-export-sftp-ssot.yaml`
- `docs/design/runtime-bundle-audit-approval-ssot.yaml`
- `docs/design/runtime-bundle-secret-credential-ssot.yaml`
