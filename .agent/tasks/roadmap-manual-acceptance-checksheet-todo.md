# Roadmap Manual Debug Confirmation TODO

目的: `docs/system-roadmap.yaml` の feature bundle 全文を材料に、テストではなく、実画面・実操作で確認する手動デバッグ用チェックシートを作る。

このファイルは `.agent/tasks/todo.md` の canonical unresolved bundle queue を置き換えない。Roadmap status / implemented 判定の正本でもない。既に test で解決している内容を再証明するためではなく、`product-nocode-loop-acceptance` の hand-debug 確認を、ユーザーが実操作しやすい単位へ分解するための作業メモ。

---

## Scope

対象repo: `github.com/tk-ud/topolactor`

Worktype: `todo_maintenance` / manual debug planning

正本:
- `docs/system-roadmap.yaml`

必ず読む:
- `AGENTS.md`
- `.agent/rules/rule.md`
- `.agent/README.md`
- `.agent/prompt/todo-maintenance.md`
- `.agent/protocols/todo-carry-over.md`

最低限読む SSOT:
- `docs/framework-core.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/sql-attention-logs-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/team-markdown-dashboard-saved-view-ssot.yaml`

---

## Boundary

- これは自動テスト追加 TODO ではない。
- これは実装不足の洗い出し TODO ではない。
- これは production_ready 昇格 TODO ではない。
- これは Roadmap status 変更 TODO ではない。
- implemented 済み bundle を未実装扱いへ戻さない。
- 確認対象は「ユーザーが触った時に、UX・状態遷移・投影・失敗表示が破綻しないか」。
- test / CI / evidence_ref は参照資料に留め、チェック項目の主役にしない。

---

## Debug Sheet Output

作成先候補:
- `.agent/checklists/check-roadmap-manual-debug.md`

チェックシートの列:
- Debug scenario
- Related Roadmap bundle
- Screen / route
- User operation
- Expected visible result
- State / projection to watch
- Failure case to try
- NG symptom
- Debug memo

不要な列:
- test file
- CI check
- evidence_ref 中心の証跡欄
- implemented 判定欄
- production_ready 判定欄

---

## Roadmap Feature Classification for Debug

### A. 起動・基本導線 smoke debug

関連 bundle:
- `product.core_runtime_route`
- `product.projection_and_output_lanes`
- `product.frontend_projection_surface_ux_acceptance`

確認目的:
初回アクセス、画面遷移、基本投影、SSE / refetch 系が、実操作上で破綻していないかを見る。

TODO:
- [ ] app 起動後、通常画面と admin 画面へ到達できる。
- [ ] route 遷移後に白画面・hydration error・console fatal が出ない。
- [ ] refresh / reload 後に画面状態が不自然に消えない。
- [ ] 操作後の loading / success / failed 表示が見える。
- [ ] 同じ操作を連打しても UI が壊れない。
- [ ] SSE または refetch 更新後に古い表示が残り続けない。

NG 症状:
- ボタンを押しても無反応。
- 成功したのか失敗したのか分からない。
- 画面は変わるが状態が戻る。
- console error が出続ける。
- reload 後に直前の保存状態が見えない。

### B. Admin authoring guidance debug

関連 bundle:
- `product.admin_topology_authoring`
- `product.dynamic_support_nocode_loop`
- `product.frontend_projection_surface_ux_acceptance`

確認目的:
admin authoring の入口から、ユーザーが迷わず次の操作に進めるかを見る。

TODO:
- [ ] `/admin/contents` で作成・編集・import 系の入口が分かる。
- [ ] Step 表示や guidance が、現在位置と次操作を説明している。
- [ ] draft / preview / validate / apply の状態差が見える。
- [ ] 入力不足や不正値で、どこを直すべきか分かる。
- [ ] cancel / reset / back / retry の逃げ道がある。
- [ ] 失敗後に同じ画面で復帰できる。

NG 症状:
- 何を押せばいいか分からない。
- Step が進んだのか分からない。
- validation error が内部語だけ。
- 失敗後に画面をリロードしないと復帰できない。
- frontend が勝手に成功扱いへ進む。

### C. CSV / JSON import debug

関連 bundle:
- `product.dynamic_support_nocode_loop`
- `product.admin_topology_authoring`

確認目的:
self-hosted no-code loop の import 操作が、実データ投入として使えるかを見る。

TODO:
- [ ] CSV import 入口が見つかる。
- [ ] JSON import 入口が見つかる。
- [ ] file 選択後に preview が表示される。
- [ ] preview で対象列・対象項目・件数が分かる。
- [ ] validate 結果が成功 / 失敗で分かる。
- [ ] apply 前に「何が登録されるか」が分かる。
- [ ] apply 後に一覧・投影・関連画面へ反映される。
- [ ] import 失敗後に同じファイルを修正して再試行できる。

Failure case:
- [ ] 空ファイル。
- [ ] 必須列不足。
- [ ] 型不一致。
- [ ] 重複行。
- [ ] JSON 構造不正。

NG 症状:
- preview なしで apply できる。
- validate failure でも apply できる。
- apply 後にどこへ反映されたか分からない。
- エラーが raw exception / stack trace だけ。
- import した件数と画面表示が合わない。

### D. Authoring -> projection refresh debug

関連 bundle:
- `product.projection_and_output_lanes`
- `product.dynamic_support_nocode_loop`
- `product.frontend_projection_surface_ux_acceptance`

確認目的:
admin 操作後、ProjectionShell / child island / list / card 表示が自然に更新されるかを見る。

TODO:
- [ ] apply 後、対象 projection が更新される。
- [ ] 更新中表示が見える。
- [ ] 古い projection と新しい projection が混ざらない。
- [ ] 画面遷移して戻っても保存済み状態が見える。
- [ ] reload 後も persisted state が見える。
- [ ] 同時に複数操作した時に表示順が破綻しない。

NG 症状:
- DBには入ったらしいが画面に出ない。
- reload しないと反映されないのに、その案内がない。
- 反映された後に古い表示へ戻る。
- child island だけ古い。
- 成功 toast が出たが保存されていない。

### E. SQL Attention feedback debug

関連 bundle:
- `product.sql_attention_observation_runtime`
- `product.dynamic_support_nocode_loop`

確認目的:
SQL Attention feedback projection が、推薦・観察・採用候補として理解でき、勝手に route / topology を変えないことを見る。

TODO:
- [ ] feedback / recommendation 表示に到達できる。
- [ ] 候補の理由・対象・信頼度または根拠らしき情報が見える。
- [ ] feedback は projection-only として見える。
- [ ] candidate adoption が明示操作になっている。
- [ ] 採用しない限り fixed route / current state が変わらない。
- [ ] feedback 後、関連 projection が自然に更新される。

Failure case:
- [ ] 候補ゼロ。
- [ ] 古い候補。
- [ ] 対象 record が削除済み。
- [ ] relation / hub context 不足。

NG 症状:
- 推薦が勝手に適用される。
- 何の候補か分からない。
- 採用ボタンと preview / inspect の区別がない。
- candidate 表示が route 状態と混ざる。
- attention score が semantic authority のように見える。

### F. M6 combined UX 通し debug

関連 bundle:
- `product.dynamic_support_nocode_loop`
- `product.admin_topology_authoring`
- `product.sql_attention_observation_runtime`
- `product.projection_and_output_lanes`

確認目的:
`authoring guidance -> import / authoring -> validate -> apply -> projection refresh -> SQL Attention feedback -> user action` の一連操作が、製品体験として破綻しないかを見る。

TODO:
- [ ] admin authoring guidance から開始する。
- [ ] CSV / JSON または手入力でデータを入れる。
- [ ] preview を見る。
- [ ] validate を見る。
- [ ] apply する。
- [ ] projection 更新を見る。
- [ ] SQL Attention feedback / recommendation を見る。
- [ ] candidate を採用しない場合、状態が変わらないことを見る。
- [ ] candidate を明示採用する場合、次状態が分かることを見る。
- [ ] reload 後に persisted state が維持されることを見る。

NG 症状:
- 途中で文脈が切れて、次に何をすべきか分からない。
- preview / validate / apply / feedback が別物に見えて一連の流れに見えない。
- 成功・失敗・保留の区別が曖昧。
- 推薦候補が勝手に反映される。
- 操作後にどの bundle / route / projection が関係したか追えない。

### G. Markdown / saved view debug

関連 bundle:
- `product.component_markdown_authoring_projection`
- `product.md_viewer_projection_component`
- `product.completed_preset_seed_projection_gate`

確認目的:
Markdown saved view が、表示・保存・refresh / clone / rebind の実操作で混乱しないかを見る。

TODO:
- [ ] `/admin/team-dashboard` に到達できる。
- [ ] saved view を表示できる。
- [ ] rendered Markdown と source / binding / seed summary が区別できる。
- [ ] refresh / clone / rebind が seed valid / invalid 状態で適切に有効・無効になる。
- [ ] invalid seed の時、何が足りないか分かる。
- [ ] Markdown body が正本のように見えない。

NG 症状:
- Markdown を直接編集すれば正本が変わるように見える。
- seed invalid なのに refresh / clone / rebind できる。
- missing props が空表示になる。
- source record と rendered projection の関係が分からない。

### H. External port consumer demo debug

関連 bundle:
- `product.external_port_substrate`
- `product.file_storage_port_consumer`
- `product.email_port_consumer`
- `product.audit_approval_port_consumer`
- `product.webhook_inbox_port_consumer`
- `product.stripe_port_consumer`
- `product.export_sftp_port_consumer`

確認目的:
外部連携系を provider 実接続の証明ではなく、human approval / preview / explicit failure / sanitized projection の製品表示として確認する。

TODO:
- [ ] 外部連携系の操作入口が分かる。
- [ ] credential / secret / raw payload が画面に出ない。
- [ ] approval required な操作は明示承認なしで進まない。
- [ ] failure 時に明示的な拒否・失敗表示が見える。
- [ ] runtime_event_log / evidence 的な履歴表示がユーザーに追える形で見える、または確認手段が分かる。
- [ ] provider-specific runtime を触っているように見えない。

NG 症状:
- secret / endpoint / raw provider response が見える。
- approval 前に送信済みになる。
- 失敗が成功風に見える。
- 外部サービスが system SSOT のように見える。
- provider 実接続できないことを product failure と誤認する UI になっている。

### I. Future / out-of-scope 表示 debug

関連 bundle:
- `product.external_optional_surface_bundle_gate`
- `product.helper_manual_policy`

確認目的:
未実装・future・helper policy が、現在の M6 manual debug の失敗として混入しないようにする。

TODO:
- [ ] optional external connector は future として扱う。
- [ ] helper manual policy は別 TODO / 別設計として扱う。
- [ ] 現行画面で未提供機能がある場合、coming soon / unavailable / out of scope が分かる。
- [ ] future scope を product-nocode-loop-acceptance の NG にしない。

NG 症状:
- future 機能が壊れている扱いになる。
- helper 未実装が M6 loop failure として混ざる。
- optional external connector が必須導線に見える。

---

## Debug Session Template

各 debug session で残すメモ:

```md
### Debug session: <scenario name>

Date:
Branch / commit:
Startup command:
Seed / fixture:
Browser:

Steps:
1.
2.
3.

Observed:
-

NG / suspicious behavior:
-

Screenshot / log memo:
-

Follow-up candidate:
- none / implementation_change / design_change / wording / fixture / unknown
```

---

## Completion Criteria

- [ ] Roadmap 全文を、テスト証跡ではなく実操作 debug scenario に分類している。
- [ ] `product.dynamic_support_nocode_loop` combined UX を中心にしている。
- [ ] authoring guidance -> import / authoring -> validate -> apply -> projection refresh -> SQL Attention feedback -> explicit user action の通し確認がある。
- [ ] implemented 済み bundle を未実装へ戻していない。
- [ ] test / CI / evidence_ref を主チェック項目にしていない。
- [ ] NG 症状が、ユーザーが実画面で見て判断できる言葉になっている。
- [ ] follow-up は、debug で実際に見つかった違和感だけを implementation_change / design_change / wording / fixture に分類する。
