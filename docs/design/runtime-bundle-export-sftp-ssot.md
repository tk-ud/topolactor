# Runtime Bundle Export SFTP SSOT

## Purpose

Export/SFTP BundleのSSOT。

export_job → package → manifest → checksum → transfer の順序を定義する。SFTP pushはpermitted export jobの外部搬出として扱う。transfer log / retry / explicit failure を定義する。SFTP host / user / key実体はこのSSOTに含めない。

---

## Authority Boundary

| 権威 | 対象 |
|------|------|
| sftp_push_authority | authorized_export_job_only |
| 禁止権威 | cli_mcp_direct_sftp_push_outside_export_job / ai_autonomous_sftp_push |

---

## Trigger Kind

**許可:**
- `export_job_trigger_with_sftp_transfer_config` — SFTP設定付きexport job

**禁止:**
- `cli_mcp_direct_sftp_trigger`
- `ai_autonomous_sftp_trigger`
- `unauthenticated_sftp_trigger`

---

## Export Transfer Flow

```
export_job (prior_approval必須)
  → package (file artifact生成)
  → manifest (必須 — 欠如は explicit rejection)
  → checksum (転送前・転送後の両方で検証)
  → transfer (SFTP push)
  → transfer_log (runtime_event_log)
```

- **export_job**: 承認済みoperationから生成
- **package**: ZIP / manifest付きディレクトリ構造
- **manifest**: 転送前に必須。欠如は転送拒否
- **checksum**: 転送前・転送後の両方で検証。不一致は explicit rejection
- **transfer**: SFTP credential実体はruntime secret store管理

---

## Retry Policy

転送失敗時は明示的retryをscheduler経由で行う。`silent_auto_retry` / `infinite_retry_without_failure_record` は禁止。

---

## Responsibility Split (vs File Storage Bundle)

| 責務 | 担当Bundle |
|------|-----------|
| file生成 / checksum計算 / manifest生成 | File Storage Bundle |
| SFTP転送 / transfer log / retry | Export/SFTP Bundle (本SSOT) |

参照: `docs/design/runtime-bundle-file-storage-ssot.yaml`

---

## Prohibited Operations

| 禁止 | 説明 |
|------|------|
| sftp_push_outside_export_job | export_job外のSFTP push禁止 |
| sftp_push_without_manifest | manifest未整備でのSFTP push禁止 |
| sftp_push_without_checksum | checksum未検証でのSFTP push禁止 |
| silent_fallback_on_transfer_failure | 転送失敗のサイレントフォールバック禁止 |
| sftp_credential_in_public_ssot | credential公開SSOT記載禁止 |

---

## Secret Credential Boundary

SFTP host / user / key実体は公開SSOTに記載しない。runtime環境変数またはsecret store経由で注入する。

---

## Approval Boundary

SFTP転送はapproval済みexport_jobの副作用として実行する。未承認operationからのSFTP pushは禁止。

---

## Scheduler Boundary

SFTP転送はscheduler経由で実行する。`webhook_direct_runtime_execution_without_scheduler`は禁止。

---

## Audit Log Boundary

`runtime_event_log` への記録が必須。記録対象:
- export_job_initiated / package_generated / manifest_generated / checksum_computed / transfer_initiated / transfer_completed / transfer_failed / retry_initiated

---

## Failure Policy

- manifest欠如: explicit rejection + runtime_event_log記録
- checksum失敗: explicit rejection + runtime_event_log記録
- 転送失敗: explicit記録 + silent fallback禁止 + policy範囲内でretry schedule

---

## Relation to Extended Runtime Bundle Registry

- registry_entry: `export_sftp_bundle`
- registry_path: `docs/design/extended-runtime-bundle-registry-ssot.yaml`
- owner_status: `assigned_to_design_ssot`

---

## Relation to CLI/MCP Port

`docs/design/cli-model-context-protocols-port-ssot.yaml` でexport_job経由のfile exportが許可されている。このBundleはその許可経路の外部搬出（SFTP）部分を担う。export_job外のdirect SFTP pushは許可しない。

---

## Future Implementation Requirements

後段実装SSOTで設計する対象:
- export_job_schema_design
- package_artifact_schema_design
- sftp_transfer_service_design
- transfer_log_schema_design
- retry_policy_implementation_design
- credential_injection_design

実装はこのSSOTの境界定義に基づいて別途実装SSOTを作成してから行う。SFTP credential / host実体はこのSSOTに含めない。
