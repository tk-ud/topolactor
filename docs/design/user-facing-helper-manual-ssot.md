# User-facing Helper / Manual SSOT

## Purpose

Topolactor のユーザー向け説明、ヘルパー、マニュアル、サイト内ガイドの **UX-facing policy** を定義する。

canonical runtime SSOT の外向き projection として機能し、以下を非技術ユーザーが理解できるように説明できることを保証する:

- Desktop AI / CLI / MCP で何ができるか
- 月次処理・分析自動化・外部出力の考え方
- どこで人間承認が必要か
- AI が自動実行しないこと

> 実装より先にマニュアル方針を置くことで、ユーザー体験・安全境界・プロダクト説明のズレを防ぐ。

---

## Authority Boundary

user-facing helper / manual は **runtime authority を持たない**。

| 対象 | 正本 |
|------|------|
| runtime dispatch | `docs/design/runtime-orchestration-ssot.yaml` |
| DB schema | `docs/design/db-schema.yaml` |
| permission enforcement | `docs/framework-policy.yaml` |
| CLI/MCP port | `docs/design/cli-model-context-protocols-port-ssot.yaml` |
| Bundle分類 | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |

**本 SSOT が所有するもの:**
- user promise（ユーザーへの約束）
- onboarding policy
- safe operation boundary の説明方針
- helper / manual category 構造
- UX-facing message policy

---

## User-facing Message Policy

### Topolactor でできること

- コードなしで業務アプリを作れる
- フォーム・一覧・PDF・CSV を基本として使える
- 月次処理の自動化に拡張できる
- Desktop AI で業務データの検索・集計・分析を補助できる
- CSV / PDF / ZIP で業務データを搬出できる

### AI / CLI / MCP でできること

| 操作 | ユーザー向け表現 |
|------|-----------------|
| read | 許可されたスコープ内でデータを読む |
| search | 業務データを検索する |
| aggregate | 集計・月次サマリを生成する |
| analyze | パターンや傾向を分析する |
| export | CSV / PDF / ZIP に出力する |
| stream_file | 搬出ジョブのファイルを取得する |

### AI / CLI / MCP がしないこと

| 禁止操作 | ユーザーへの説明 |
|----------|-----------------|
| email send | メール送信はUI上で人間が承認する |
| payment approval | 請求確定はUI上で人間が承認する |
| record delete | 削除はUI上で人間が実行する |
| final register | 最終登録はUI上で人間が確認する |
| DB 直接操作 | AI/CLIはデータを直接書き換えない |

### 言語方針

内部システム用語（topology / manifest / attractor など）をユーザー向け説明に使わない。

| 内部用語 | ユーザー向け表現 |
|----------|-----------------|
| topology | 業務アプリの構造 |
| manifest | 業務コンテンツ設定 |
| export_job | 搬出処理 |
| cli_reader_port | AI/CLI へのデータ読み取り窓口 |

---

## Helper / Manual Category Candidates

実装しない。将来作成するカテゴリの方針として整理する。

| カテゴリ | 対象ユーザー | 関連 SSOT |
|----------|-------------|-----------|
| はじめての業務アプリ作成 | 非技術ユーザー | — |
| フォーム・一覧・PDF・CSVの基本 | 非技術ユーザー | — |
| 月次処理を自動化する | 業務担当者・管理者 | — |
| Desktop AIで業務データを分析する | 業務担当者 | CLI MCP Port SSOT |
| CLI / MCP Reader Port を使う | 技術ユーザー | CLI MCP Port SSOT |
| CSV / PDF / ZIP を出力する | 業務担当者・管理者 | CLI MCP Port SSOT |
| Email送信はUIで承認する | 業務担当者 | Extended Bundle Registry |
| Stripe決済はWebhookで確定する | 管理者・連携担当 | Extended Bundle Registry |
| 管理者向け: 権限・搬出・監査設定 | 管理者 | CLI MCP Port SSOT, Admin Console SSOT |
| 外部Bundle連携の考え方 | 管理者・連携担当 | Extended Bundle Registry |

---

## Safety Boundary

ユーザー向け説明にも必ず反映する安全境界の方針。

### ユーザーへの約束（User Promises）

1. **AI はメール送信を自動実行しない** — UI 上の人間承認が必要
2. **AI は請求を自動確定しない** — UI 上の人間承認が必要
3. **AI はレコードを自動削除しない**
4. **CLI/MCP はDBを直接操作しない** — Context API / Data Reader 経由のみ
5. **Stripe の支払済み判定は Webhook 検証後** — 自動確定なし
6. **外部サービスは Topolactor の runtime SSOT ではない** — intake/preview/apply 経由

### 人間承認が必要な操作

- email_send
- payment_approval
- final_register
- record_delete
- 承認スコープを超えた bulk_export

---

## Out of Scope

| 除外対象 |
|----------|
| サイトページ実装 |
| UI コンポーネント実装 |
| ヘルプ画面実装 |
| MCP tool 実装 |
| CLI 実装 |
| Email runtime 実装 |
| Stripe runtime 実装 |
| 文言ライティングの大量追加 |
| README 全面改稿 |

---

## Related SSOT

| SSOT | 関係 |
|------|------|
| `docs/design/cli-model-context-protocols-port-ssot.yaml` | CLI/MCP read/export port runtime 設計正本 |
| `docs/design/extended-runtime-bundle-registry-ssot.yaml` | Bundle 分類正本（Email/Stripe/外部） |
| `docs/design/runtime-orchestration-ssot.yaml` | canonical runtime route 正本 |
| `docs/design/admin-console-workflow-ssot.yaml` | admin UI surface 正本 |
| `docs/system-roadmap.yaml` | 実装状態・マイルストーン参照点 |
| `.agent/tasks/todo.md` | 未実装作業キュー |
