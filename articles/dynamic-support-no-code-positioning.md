---
title: "動的サポート付きノーコードという考え方"
emoji: "🌱"
type: "tech"
topics: ["ai", "nocode", "architecture", "postgresql", "ci"]
published: false
---

# 動的サポート付きノーコードという考え方

**キャッチ**  
topolactor は、CI Attention が作成時の不足入力・有効候補・壊れる境界を案内し、SQL Attention が蓄積ログを次のUXへ還流する、オンプレフォーク可能な動的サポート付きノーコード基盤です。

## 先に結論

topolactor は「作れるノーコード」ではなく、**「作っても壊れにくく、使うほど育つノーコード」**です。

従来ノーコードとの違いは、画面が作れるかどうかではありません。  
違いは、**自由に作ったあとに壊れにくい運用構造**と、**使ったログが次のUXに戻る構造**を最初から持っていることです。

---

## 従来ノーコードと topolactor のUX差分

### 従来ノーコード

- ユーザーが画面・フォーム・ワークフローを作る。
- 自由度は高いが、正しく作るための知識はユーザー側に寄りやすい。
- 壊れた時は、フロー/API/権限/型/設定をユーザーが追う必要がある。
- ログは分析・監査の後処理になりやすい。
- SaaS依存が強く、組織固有の学習済み業務UXを持ち帰りにくい。

### topolactor

- ユーザーは通常業務を進めながら、必要に応じてUIや導線を作れる。
- **CI Attention** が不足入力・有効候補・構造違反・壊れる境界を案内する。
- そのため、自由に作っても壊れにくい。
- **蓄積ログが hub / SQL Attention に還流し、次の候補・導線・UI projection を育てる。**
- ログそのものが、次のユーザー体験へつながる。
- オンプレで閉じられる。
- オンプレごと fork できる。
- fork した環境を、組織固有の業務環境へ埋め込める（※ここでの埋め込みは、vector embedding ではなく、業務環境・DB・運用フロー・SSOTへの統合を指す）。
- 学習済み業務UXを、組織資産として継続運用できる。

短く言えば:

- 従来ノーコード: 作れる。
- topolactor: 作っても壊れにくい。使うほど育つ。オンプレで持てる。forkして組織に埋め込める。

---

## 責務境界（混ぜないための整理）

### CI Attention の責務

CI Attention は、**SSOT / YAML / schema / current DB state** に基づくユーザ入力補助層であり、backend/runtime 側の validation/error handling 膨張を防ぐために以下を案内する層です。

- 不足入力
- 有効候補
- 構造違反
- 壊れる境界

つまり、ユーザーがUIや導線を自由に作るときに、壊れやすいポイントを早期に見える化する役割を持ちます。

### SQL Attention の責務

SQL Attention は、**hub / hub construction / hub relation / attractor current / logs evidence** から、次の体験に向けた hub候補・接続候補・導線・projection候補を育てる層です。

- 次に有効になりやすい文脈候補
- 導線の方向性
- UI projection の重み付け

SQL Attention は「不足入力を直接教える層」ではありません。  
不足入力の案内は CI Attention の責務です。

### scenario-contract の責務

scenario-contract は、**agent / 実装作業 / 監査プロトコル側の契約**です。  
ユーザーUXの動的サポート層ではなく、CI Attention の入力源や呼び出し先として扱いません。

### frontend / backend / SSOT の責務

- frontend は projection / input lens。正本や判断を持たない。
- backend は dispatcher / executor / boundary guard。業務意味の正本を持たない。
- SSOT / DB / hub が語彙・経路・制約・文脈・projection の正本。

---

## 「ログは後処理」ではなく「ログが次のUX入力」

topolactor の重要な違いは、ログを分析・監査専用の終点として閉じないことです。

- 使われた導線
- 選ばれた候補
- 途中で詰まった境界
- UI操作の文脈

こうした蓄積ログが hub / SQL Attention に還流し、次のユーザーに対する候補提示や導線設計、UI projection の改善に反映されます。

そのため、topolactor では「利用履歴」がそのまま「次の業務UXの改善入力」になります。

---


## 木の枝とブドウの房として見る topolactor

topolactor のUX構造は、業務データ・導線・ログ・UI projection が、木の枝に実るブドウの房のように育っていく体験として捉えると直感的です。

対応関係は次のとおりです。

- SSOT / schema = 幹
- DB topology / route / relation = 枝
- hub = 節
- logs = 実
- SQL Attention = どのブドウの房が重く育っているかを見る重み
- CI Attention = 折れそうな枝や足りない支柱を知らせる仕組み
- UI projection = いま見えているブドウの房
- on-prem fork = 育った木を組織環境ごと接ぎ木・分岐できること

従来ノーコードが「部品を並べて画面やフローを作る」体験に寄りやすいのに対し、topolactor は、業務UXを一度作って終えるのではなく、使われたログを実として蓄積し、次の導線やUI projectionへ戻しながら育てる構造を持ちます。

つまりログは足跡ではなく、次の道を作る材料です。topolactor は画面を作る道具にとどまらず、業務UXが育つ土壌として機能します。さらに on-prem fork により、育った木を組織環境ごと接ぎ木・分岐し、継続運用できます。

---

## オンプレ運用・オンプレフォーク・組織埋め込みがUX価値になる理由

topolactor はオンプレ運用可能であり、オンプレ環境ごと fork できます。  
このとき保持したいのはコードだけでなく、次の運用文脈です。

- SSOT
- current DB state
- hub / logs
- CI Attention で案内された境界知見

これらの文脈を維持したまま fork できるため、蓄積ログから育った業務UXを SaaS 側に吸われず、組織資産として持ち帰って分岐・継続運用できます。

ここでいう「埋め込み」は、vector embedding のことではありません。  
組織固有の業務環境・DB・運用フロー・SSOT へ統合し、日常運用の中で再利用可能にすることを指します。

---

## Positioning message

topolactor の価値は、ノーコードの「作成自由度」を増やすことだけではありません。

- 作成時: CI Attention が壊れやすさを先回りして案内する。
- 利用後: SQL Attention が蓄積ログを次のUXへ還流し、文脈候補・導線・UI projection を育てる。
- 運用: オンプレで閉じ、オンプレフォークし、組織環境へ埋め込んで継続できる。

したがって topolactor は、

> 作れるノーコードから、育つノーコードへ

というUXポジションを取ります。

---

## 実装状態

本記事のポジションは topolactor の設計方針とロードマップ方向性を示しています。
現時点の実装状態は以下のとおりです（詳細は `docs/system-roadmap.yaml` を参照）。

**CI Attention（現在の実装）:** `backend.system_operation_ci_runtime`（`SystemOperationCiRuntime` / `SystemOperationCiScheduler`）の runtime system CI subpath は実装済みです。cron 実行でHub Attention 整合性・current rebuildability・registry continuity を検査し、`SystemCiStatus`（Pass / Gap / Blocking）で診断結果を structured log に出力します。診断結果は現時点では DB 永続化されておらず、自動 follow-up アクションも未実装です。動的サポート付きノーコード loop における CI Attention authoring guidance 境界は現在の M6/M7 core 判定上 closed として扱います。

**SQL Attention（現在の実装）:** `backend.sql_attention_observation_runtime` は M7 core runtime production-ready です。`logs.current`（L2 norm watch）・`logs.hub_current`（attractor current）・`logs.attention`（evidence 永続化）を分離し、scheduler / exploration / repository / projection read の閉ループは live DB E2E で証跡化されています。`product.sql_attention_recommendation_feedback_ux` も production-ready 証跡維持の対象です。ただし topology recommendation current は child projection consumer であり、SQL Attention parent observation body そのものではありません。

**動的サポート付きノーコード UX:** manifest-driven dispatch・admin registry surface・component topology・M6 self-hosted admin authoring loop・SQL Attention recommendation feedback UX の実装境界は閉じています。`product.dynamic_support_nocode_loop` は manual acceptance / hand-debug verification のため partial を維持しており、残理由は implementation gap ではありません。
