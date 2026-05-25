---
title: "CRUDを増やさずに業務アプリを拡張する：AIガバナンスとSQL/CI Attentionで作るhub & pipeline"
emoji: "🧭"
type: "tech"
topics: ["architecture", "ai", "postgresql", "csharp", "deno"]
published: false
---

## はじめに

業務アプリは、最初はCRUDで十分に見えます。

しかし機能が増えるたびに、画面、API、権限、validation、例外処理、テストが増えていきます。

```text
新しい業務機能
  -> 新しい画面
  -> 新しいAPI
  -> 新しい権限境界
  -> 新しいvalidation
  -> 新しい例外処理
```

この構造は、人間が書いてもつらいですが、AIエージェントに実装させるとさらに差分が散りやすくなります。

そこで、CRUD単位で機能を増やすのではなく、hubとpipelineに寄せて業務アプリを拡張する実験として `topolactor` を作っています。

Repository: https://github.com/tk-ud/topolactor

開発開始は 2026-05-17 です。

## 作っているもの

`topolactor` は、data-driven topology runtime と AI-Driven Development OS を組み合わせた開発実験です。

目指しているのは、次のような構造です。

```text
definition
  -> registry / policy / manifest
  -> runtime route
  -> diagnostics / CI
  -> projection
```

画面やAPIを個別に増やすのではなく、新しい対象を共通pipelineに流します。

## 旧来CRUDとの違い

| 観点 | 従来CRUD | topolactor |
|---|---|---|
| 拡張単位 | 画面/API | pipelineに流す定義 |
| UI | code component中心 | catalog -> DB registration -> projection |
| 境界 | surfaceごとに実装 | route / policy / registryへ集約 |
| AI実装 | 差分が散りやすい | SSOT / CIで境界を固定 |
| 失敗処理 | fallbackで隠れがち | statusとして露出 |

CRUDはsurfaceごとに閉じる設計になりがちです。

topolactorでは、route / policy / registry を通すことで、拡張時の判断点をpipeline側へ集約します。

## AIガバナンス

AIエージェントには自由に実装させるのではなく、読むべき正本と作業境界を固定します。

```text
AGENTS.md
  -> .agent/rules
  -> worktype prompt
  -> protocol / checklist
  -> target SSOT
  -> implementation
  -> CI
```

AIの実装速度を使いつつ、意味の散らばりはSSOTとCIで止めます。

## SQL Attention

SQL Attentionは、DB上の運用信号を観測する仕組みです。

LLMのattentionをSQLで再現する、という意味ではありません。

runtimeが何に注目すべきかを判断するために、DB側にcurrent、hub current、attention evidenceのような観測面を残します。

## CI Attention

CI Attentionは、CIをpass/failだけでなく、運用statusを返すcheckerとして扱う考え方です。

```text
implementation diff / system event / runtime state / SSOT contract
  -> CI
  -> pass / gap / blocking / drift / not-covered
  -> follow-up action
```

失敗をfallbackで隠さず、次の行動へ接続できる状態として出します。

## UI topology

UI componentも、code-onlyのままではruntime上の正本にしません。

```text
component catalog
  -> ui_component_bucket
  -> package generator
  -> component / package / layout / wiring id
  -> ui_topology_tensor
  -> runtime projection
```

componentをDB登録し、IDを発行して、projection runtimeへ流せるようにします。

## いま何ができるか

現時点では開発中ですが、次の基盤が入っています。

- runtime route skeleton
- manifest-driven dispatch boundary
- frontend runtime projection lane
- UI component catalog / classification
- component bucket / package generator / topology tensorのDB設計
- AI agent governance files
- SSOT / roadmap / TODO / protocol / checklist
- CI Attention的なstatus branching設計
- SQL Attention observation runtimeの一部

## まとめ

CRUDを増やして業務アプリを拡張すると、画面/API/境界/validation/例外処理がsurfaceごとに増えます。

topolactorでは、それらをhubとpipelineへ集約します。

```text
AI governance = AI実装の意味境界を閉じる
SQL Attention = DB上の運用信号を観測する
CI Attention = 失敗をstatusとして次アクションへつなぐ
hub & pipeline = 実行・表示・監査の通り道を集約する
```

AIエージェント時代の開発では、実装速度よりも「どこを通して、どこで落とすか」が重要になると思っています。
