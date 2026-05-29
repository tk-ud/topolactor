---
title: "GPTに監査、Codexに小修正、Claudeにbundle実装を任せて12日でproduction-ready coreまで到達した話"
emoji: "🚀"
type: "tech"
topics: ["ai", "開発", "architecture", "ci", "governance"]
published: false
---

# GPTに監査、Codexに小修正、Claudeにbundle実装を任せて12日でproduction-ready coreまで到達した話

topolactor という data-driven topology runtime framework を、2026-05-17 に開発開始して、2026-05-29 に production-ready core まで持っていった。

期間は12日。
AIコストは月額固定で $40。
API従量課金は $0。

ただし、これは「AIに全部丸投げした」という話ではない。
むしろ逆で、AIを役割ごとに分離し、SSOT・roadmap・todo・CIを判断面として置いた開発ガバナンスの話である。

## 何を production-ready と呼んだか

この記事でいう production-ready は、リポジトリ内の全 future 構想が完成したという意味ではない。

ここでの production-ready core は、M6/M7 core runtime boundaries が CI green で閉じた状態を指す。

具体的には、以下のような境界である。

- canonical dispatch pipeline
- runtime scheduling / dispatch surfaces
- self-hosted admin authoring loop
- DB/JSONB → DocumentCanvas → PDF snapshot boundary
- SQL Attention observation runtime
- live DB end-to-end evidence
- roadmap / todo / test-bundles の整合
- README 上の production-ready core 宣言

future optional external connector は含めていない。

## 使ったAIと月額コスト

使ったのは、実質 GPT / Codex / Claude だけ。

- ChatGPT Plus / Codex: $20/month
- Claude Pro / Claude Code: $20/month
- API usage billing: $0
- Pay-as-you-go agent billing: not used

合計 $40/month fixed subscription run-rate。

OpenAI の pricing では ChatGPT Plus に Codex usage が含まれ、Business Codex は pay-as-you-go として分離されている。
Anthropic の pricing では Claude Pro は月払い $20 で、Claude Code を含む。

価格は当然変わりうるので、この記事で扱うのは「production-ready core 到達時点の運用コスト」である。

## 役割分担

AIを全部同じ役割で使うと、責務が溶ける。

そこで topolactor では、ざっくり以下の役割に分けた。

### GPT: 監査役

GPTには、主にPR監査と判断を任せた。

- PR特定
- リポジトリの現状確認
- PR差分との突き合わせ
- SSOTとの意味整合確認
- roadmap / todo の更新要否判断
- follow-up prompt の発行

GPTには、基本的にコードを直接書かせない。
役割は、実装者ではなく監査役に寄せた。

### Codex: 小規模修正と管理面整理

Codexには、小さく閉じた修正を任せた。

- CI red の最小修正
- README 更新
- todo 整理
- roadmap 文言整理
- 記事ファイルの移動
- structure-map の軽微な更新

Codexに向いているのは、境界が明確で、差分が小さく、判断軸がすでに決まっている作業である。

### Claude: bundle単位の大規模実装

Claudeには、bundle単位の大きな実装を任せた。

- 大規模リファクタ
- runtime bundle 実装
- SQL function 修正
- live DB end-to-end test
- 複数ファイルをまたぐ閉ループ実装

Claudeが bundle 実装をして、GPTが監査し、Codexが小さく整える。
この三角形がかなり効いた。

## SSOTがないとAIは速くならない

topolactor では、設計思想を Markdown に置き、構造定義やroadmapを YAML に置き、CIで整合性をチェックする。

```text
md思想
→ yaml構造
→ 実装
→ test evidence
→ CI監査
→ GPT監査
→ todo/roadmap正規化
```

この流れがあると、AIに「いい感じに直して」と言わなくて済む。

何がSSOTか。
何が実装済みか。
何がpartialか。
何がfuture optionalか。
どのCIが証跡か。

これらをファイル上で持てる。
AIは、その地図の上で動く。

## partial扱いのズレも監査対象にする

今回おもしろかったのは、実装不足よりも「管理面の表現ズレ」が多かったことだ。

SQL Attention 系は、実装実態としてはかなり閉じていた。
それでも roadmap / todo 側が partial を引きずっていた。

これは実装 gap ではなく、管理面 gap である。

そこで、partial を「実装不足」ではなく「manual acceptance / hand-debug verification pending」に正規化した。

AI開発では、実装と管理面のズレがそのまま次のAIの誤読になる。

## CIは最後の面

AIに任せるなら、CIは必須である。

実装としては正しくても、CIの禁止マーカーに当たることがある。
また、runtime 実装不足ではなく、CI環境の setup mismatch で落ちることもある。

こういうズレを CI が拾い、GPT が原因を切り分け、Codex が小さく直す。

この流れが回ると、手戻りはかなり小さくなる。

## まとめ

AI駆動開発で大事なのは、AIに丸投げすることではない。

AIが誤読しにくい構造を作ることだ。

今回は、GPT / Codex / Claude を役割分担し、SSOT / roadmap / todo / CI を判断面として置くことで、12日・月額 $40・従量課金なしで production-ready core まで到達できた。

たぶん再現性の核心は、モデル選びよりも、AIガバナンスの設計にある。

## References

- OpenAI ChatGPT pricing: https://chatgpt.com/pricing/
- Anthropic Claude pricing: https://claude.com/pricing
- topolactor: https://github.com/tk-ud/topolactor
