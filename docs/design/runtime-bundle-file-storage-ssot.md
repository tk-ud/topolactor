# Runtime Bundle File Storage SSOT

## Purpose

File/Storage BundleのSSOT。

PDF / CSV / JSON / ZIP / receipt image / manifest などの保存・取得境界を定義する。signed download / checksum / manifest / export_job との関係を定義する。CLI/MCP file streamはpermitted export_job経由のみ許可する。

---

## Authority Boundary

| 権威 | 対象 |
|------|------|
| file_read_authority | export_job_or_authorized_api |
| file_write_authority | export_job_or_authorized_backend_dispatch |
| 禁止権威 | cli_mcp_direct_file_stream_outside_export_job / unauthenticated_download |

---

## Trigger Kind

**許可:**
- `export_job_trigger` — 認可済みexport job
- `authorized_api_trigger` — 認可済みAPI経由
- `signed_download_request` — signed URL発行済みのダウンロード

**禁止:**
- `cli_mcp_direct_stream_outside_export_job`
- `unauthenticated_file_access`

---

## Supported File Types

| カテゴリ | 対象 |
|----------|------|
| document_export | pdf / csv / json |
| archive | zip |
| receipt_and_manifest | receipt_image / manifest_json |

---

## File Flow Boundary

```
export_job (manifest + checksum必須)
  → file_artifact (生成)
  → checksum_record (integrity検証)
  → manifest_record (package管理)
  → signed_download (一時URL発行 — 認証済みのみ)
  → runtime_event_log
```

- **export_job**: authorized + manifest_definedが前提
- **signed_download**: 有効期限付き。公開/unsigned downloadは禁止
- **checksum**: 生成・取得時に検証。失敗はexplicit rejection
- **manifest**: export packageに必須

---

## CLI/MCP File Stream Policy

CLI/MCP file streamは `export_job_authorized_stream_only`。export_job外のdirect streamは禁止。参照: `docs/design/cli-model-context-protocols-port-ssot.yaml`

---

## Prohibited Operations

| 禁止 | 説明 |
|------|------|
| direct_file_stream_from_cli_mcp_outside_export_job | export_job外のCLI/MCP direct stream禁止 |
| file_write_without_checksum | checksum未計算でのfile write禁止 |
| silent_fallback_on_storage_failure | storage失敗のサイレントフォールバック禁止 |
| bucket_name_in_public_ssot | bucket名の公開SSOT記載禁止 |

---

## Secret Credential Boundary

object storage provider credential（access key / secret key / bucket name / endpoint）は公開SSOTに記載しない。runtime環境変数またはsecret store経由で注入する。

---

## Approval Boundary

export_jobはapproval済みoperationの副作用として実行する。未承認operationからのfile writeは禁止。

---

## Scheduler Boundary

大容量exportなどの非同期処理はscheduler経由で行う。`direct_runtime_execution_without_scheduler`は禁止。

---

## Audit Log Boundary

`runtime_event_log` への記録が必須。記録対象:
- export_job_initiated / file_write_completed / checksum_verified / signed_url_generated / download_completed

---

## Failure Policy

- storage failure: explicit expose + runtime_event_log記録。silent fallback禁止
- checksum failure: explicit rejection + runtime_event_log記録

---

## Relation to Extended Runtime Bundle Registry

- registry_entry: `file_storage_bundle`
- registry_path: `docs/design/extended-runtime-bundle-registry-ssot.yaml`
- owner_status: `assigned_to_design_ssot`

---

## Relation to CLI/MCP Port

`docs/design/cli-model-context-protocols-port-ssot.yaml` で export_job経由のfile streamが許可されている。このBundleはその許可経路を担うsurface。export_job外のdirect file streamは許可しない。

---

## Future Implementation Requirements

後段実装SSOTで設計する対象:
- export_job_schema_design
- file_artifact_storage_schema_design
- checksum_record_schema_design
- signed_url_generation_service_design
- manifest_schema_design
- storage_provider_adapter_design

実装はこのSSOTの境界定義に基づいて別途実装SSOTを作成してから行う。credential / bucket実体はこのSSOTに含めない。
