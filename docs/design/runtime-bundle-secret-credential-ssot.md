# Runtime Bundle Secret/Credential SSOT

## Purpose

Secret/Credential Bundleの設計SSOT。

外部連携に必要な認証情報の管理境界を定義する。公開SSOTに実credential・endpoint・secret値を記載しない。admin_config / api triggerによるcredential管理境界を本SSOTで定義する。

---

## Authority Boundary

| 権威 | 対象 |
|------|------|
| credential_management_authority | admin_config_and_runtime_secret_store |
| 禁止権威 | plaintext_credential_in_public_ssot / cli_mcp_credential_read |

---

## Trigger Kind

**許可:**
- `admin_config_credential_registration` — admin操作によるcredential参照登録
- `api_credential_rotation` — credential rotation
- `system_credential_validation` — credential有効性検証

**禁止:**
- `cli_mcp_credential_read_trigger`
- `ai_autonomous_credential_mutation`
- `plaintext_credential_api_exposure`

---

## Credential Management Flow

```
registration (admin操作 → secret store経由管理)
  → validation (登録・rotation・runtime起動時に必須)
  → rotation (明示的admin操作 / audit logには参照のみ記録)
  → runtime_injection (環境変数またはsecret manager API経由)
```

- **registration**: 実値はsecret store経由。plaintext保存禁止
- **rotation**: audit logには参照識別子のみ（実値記録禁止）
- **runtime_injection**: config fileへのplaintext記載禁止

---

## Prohibited Operations

| 禁止 | 説明 |
|------|------|
| real_credential_in_public_ssot | credential実値の公開SSOT記載禁止 |
| credential_in_runtime_event_log | audit logへのcredential実値記録禁止 |
| credential_in_audit_log_plaintext | plaintext audit記録禁止 |
| silent_credential_fallback | サイレントフォールバック禁止 |
| cli_mcp_credential_read | CLI/MCP経由のcredential読み取り禁止 |

---

## Secret Credential Boundary

全てのcredential実体（API key / secret key / connection string / private key / endpoint URL等）は公開SSOTに記載しない。runtime環境変数またはsecret store経由で注入する。このSSOT自体もcredential実体を含まない。

---

## Scheduler Boundary

credential rotation / validationのバックグラウンド実行はscheduler経由で行う。`direct_runtime_execution_without_scheduler`は禁止。

---

## Approval Boundary

credentialのrotationは明示的admin操作または承認済みautomation jobとして実行する。AI単独判断・暗黙的rotationは禁止。

---

## Audit Log Boundary

`runtime_event_log` への記録が必須。**audit logにはcredential参照識別子のみ記録する（実値は絶対に記録しない）。**

記録対象:
- credential_registered_reference_only / credential_rotated_reference_only
- credential_validation_success / credential_validation_failure
- credential_rotation_failed

---

## Failure Policy

- credential validation失敗: 明示的failure + runtime_event_log記録（credential実値なし） + silent fallback禁止
- secret store unavailable: fail_close（明示的error） + cached credential silent使用禁止
- rotation失敗: 明示的failure + runtime_event_log（参照のみ）

---

## Relation to Extended Runtime Bundle Registry

- registry_entry: `secret_credential_bundle`
- registry_path: `docs/design/extended-runtime-bundle-registry-ssot.yaml`
- owner_status: `assigned_to_design_ssot`

---

## Relation to CLI/MCP Port

`docs/design/cli-model-context-protocols-port-ssot.yaml` でcredential readはout_of_scopeとして扱う。このBundleはCLI/MCP経由のcredential read/export経路を提供しない。

---

## Future Implementation Requirements

後段実装SSOTで設計する対象:
- credential_reference_schema_design
- secret_store_adapter_design
- credential_registration_ui_design
- credential_rotation_service_design
- credential_validation_service_design
- credential_injection_pattern_design
- audit_log_schema_for_credential_events

実装はこのSSOTの境界定義に基づいて別途実装SSOTを作成してから行う。credential実体はこのSSOTを含む公開SSOTに含めない。
