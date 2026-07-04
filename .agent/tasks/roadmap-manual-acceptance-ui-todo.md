# Roadmap Manual Acceptance UI TODO

目的: `docs/system-roadmap.yaml` の Roadmap feature bundle を基準に、手動受入で見るべき admin / projection UI の振る舞いを粗く列挙する。

このファイルは `.agent/tasks/todo.md` の canonical unresolved bundle queue ではない。実装完了判定、backend runtime 判定、CI / test 証跡確認の正本でもない。まずは Roadmap 基準で手動受入候補を広めに置き、後続監査でコードを読めば分かる項目や test で確認済みの項目を削除する。

---

## Scope

対象repo: `github.com/tk-ud/topolactor`

Worktype: `todo_maintenance` / manual_acceptance_planning

必ず読む:
- `AGENTS.md`
- `.agent/rules/rule.md`
- `.agent/README.md`
- `.agent/prompt/todo-maintenance.md`
- `.agent/protocols/todo-carry-over.md`

Roadmap:
- `docs/system-roadmap.yaml`

最低限読む SSOT:
- `docs/framework-core.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`

機能別に読む候補:
- `docs/design/sql-attention-logs-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/scheduler-job-manifest-ssot.yaml`
- `docs/design/team-markdown-dashboard-saved-view-ssot.yaml`
- `docs/design/user-facing-helper-manual-ssot.yaml`

---

## Boundary

- 手動受入対象は admin UI と projection UI。
- backend runtime / scheduler / dispatcher / repository / DB write の正しさは test / 監査で確認する。
- チェックシートは test の二重化にしない。
- コードを読めば分かる静的項目は後続監査で削除する。
- 手動受入では、画面を触った時の迷い、文脈喪失、状態誤認、反映先不明、失敗時復帰不能を主に見る。
- Roadmap bundle を未実装扱いへ戻すためのファイルではない。

---

## Work Order

1. `docs/system-roadmap.yaml` の `implementation_registry` 全文から機能一覧を抽出する。
2. 各機能の `detail_ref` / SSOT / 実装ファイル候補を読む。
3. admin UI / projection UI に出るべき振る舞いを抽出する。
4. UI 起点、cron 設定起点、webhook 設定起点、projection 観測に粗く分類する。
5. コード監査で分かる項目、test で確認済みの項目は後続で削除する。
6. 残った項目を `.agent/checklists/check-roadmap-manual-acceptance-ui.md` 候補へ整理する。

---

## Circulation Audit Rules

巡回監査 Agent は、未処理チェック項目を上から順に処理する。プロンプト側に細かい分岐を書かず、この節を正として判定する。

判定種別:
- `audit_resolved`: SSOT / 実装 / test を読めば確認でき、手動受入に残す必要がない項目。該当チェック項目を削除するか、完了扱いへ変更する。
- `manual_acceptance_required`: 実画面で触らないと判断できない項目。手動受入 TODO として残し、必要なら route / surface 単位に短く整える。
- `implementation_changes_request`: UI 実装不足により、手動受入以前に実装変更が必要な項目。同じ Roadmap 節内に `Implementation changes request` を追記する。
- `owner_decision_required`: SSOT / Roadmap / UI方針の設計判断が必要な項目。実装指示に進めず、同じ Roadmap 節内に `Owner decision required` を追記する。
- `out_of_scope`: admin UI / projection UI の手動受入対象ではない項目。TODO から削除する。

処理済み項目の扱い:
- `巡回監査メモ` で `manual_acceptance_required` と明記されたチェック項目は、手動受入待ちとして残すが、巡回監査では処理済みとして扱う。
- `manual_acceptance_required` は手動受入 checklist の未実施状態を表すため、チェックボックス `[ ]` のままでよい。
- 巡回監査 Agent は、処理済みメモが付いた項目を再監査せず、次の未処理チェック項目へ進む。
- ただし、関連 SSOT / 実装 / Roadmap が更新された場合、または owner が再監査を指示した場合は再処理してよい。

`Implementation changes request` に書く内容:
- 対象 route / surface
- 問題
- 期待する UI 振る舞い
- 関連 SSOT
- 対象ファイル / 関数候補
- 手動受入で確認したい NG 症状

`Owner decision required` に書く内容:
- 判断が必要な論点
- SSOT上の不明点または矛盾
- 選択肢
- 推奨しない作業
- owner 判断後に更新すべき TODO / SSOT 候補

修正ルール:
- このファイルのみ最小差分で修正する。
- 全置換や Roadmap 再分類の大規模整理は禁止。
- implemented 済み bundle を未実装扱いへ戻さない。
- backend runtime / DB / scheduler / dispatcher / repository の correctness gap は、この手動受入 TODO に残さない。
- 監査で処理した項目は、削除 / 残す / 修正 / 完了扱い / implementation request / owner report のいずれかに必ず分類する。

---

## Common Manual Acceptance Viewpoints

admin UI:

巡回監査メモ:
- 2026-06-29 audit: 共通 UX 観点は `manual_acceptance_required` として残す。ただし route 固有の未実装混入は Roadmap-based TODO 側で切り分ける。

- [ ] 初見で現在位置と次操作を誤認しない。
- [ ] 操作途中で draft / preview / validate / apply / saved / failed の文脈を失わない。
- [ ] 設定・登録・適用の結果がどの projection に出るか追える。
- [ ] 失敗後に、作業文脈を維持したまま修正へ戻れる。
- [ ] 内部実装語が、ユーザーの操作判断を邪魔しない。

projection UI:
- [ ] admin 操作後の反映先が自然に分かる。
- [ ] 更新中 / 成功 / 失敗 / 保留の状態差が見える。
- [ ] reload / 再訪問後も、保存済み状態を自然に理解できる。
- [ ] cron / webhook など非同期起点の結果が、未実行・成功・失敗・保留として誤認なく見える。
- [ ] 古い projection と新しい projection が混ざって見えない。

---

## Roadmap-based Rough TODO

### 1. Admin topology authoring / UI Builder

Roadmap bundle:
- `product.admin_topology_authoring`
- `product.frontend_projection_surface_ux_acceptance`

主な画面候補:
- `/admin/contents`
- `/admin/ui-builder`
- `/admin/manifests`

手動受入 TODO:

巡回監査メモ:
- 2026-06-30 audit: 先頭3項目（現在位置誤認 / Step境界理解 / apply前変更追跡）は `manual_acceptance_required`。SSOT・実装で `/admin/contents` stepper / 開始モード / 保存状態表示と `/admin/ui-builder` 保存前チェック / preview / validate / apply 境界は確認できる。一方、既存testは語彙・静的guard中心で、初見誤認・体感理解・変更追跡は実画面受入判断が必要なため残す。
- 2026-07-04 audit: 次5項目（apply後反映先 / validation失敗復帰 / UI Builder編集中状態 / modal・drawer・preview違和感 / advanced語彙）は `manual_acceptance_required`。SSOT・実装・testで lifecycle / status / validation error / handoff / normal-view vocabulary の基板は確認できるが、projection追跡の自然さ・画面離脱なしの復帰体感・canvas阻害感・通常操作時の語彙負荷は実画面受入判断が必要なため残す。

- [ ] `/admin/contents` で、作成・編集・import など実装済み入口を触った時に、作業の現在位置を誤認しない。
- [ ] Step 遷移中に、draft / preview / validate / apply の関係が体感で分かる。
- [ ] apply 前に、何が変わるかを画面上で追える。
- [ ] apply 後に、どの projection / list / screen に反映されたか辿れる。
- [ ] validation 失敗後、画面を離れずに修正へ戻れる。
- [ ] UI Builder で配置・style・binding を触った時に、編集中状態と反映済み状態を混同しない。
- [ ] modal / drawer / preview が、canvas 操作を妨げる違和感を起こさない。
- [ ] advanced / internal vocabulary が通常操作の判断を邪魔しない。

監査で削除候補:
- 単に選択式かどうか。
- placeholder / autocomplete の有無だけで判断できる項目。
- test で covered の layout serialization / factory connection。

### 2. Dynamic support no-code loop

Roadmap bundle:
- `product.dynamic_support_nocode_loop`

主な画面候補:
- `/admin/contents`
- projection shell / child island / recommendation panel

手動受入 TODO:

巡回監査メモ:
- 2026-07-04 audit: 6項目すべて `manual_acceptance_required`。Roadmap上も実装済み範囲とは別に combined UX acceptance が非コード確認 gap として残る。実装・testで AdminImport の preview / apply / Step3 editor merge、projection-only SQL Attention candidate、dispatch/result summary の基板は確認できるが、import→authoring→apply の一連体験、apply後の反映先理解、recommendation と通常状態の視覚的混同、candidate未採用時に状態変化したように見えないか、M6 loop全体の文脈連続性は実画面受入判断が必要なため残す。

- [ ] authoring guidance から import / authoring / apply まで、次操作に迷わない。
- [ ] CSV / JSON import 後、preview / validate / apply の流れが一連の体験として見える。
- [ ] apply 後、projection がどこに出たか自然に分かる。
- [ ] recommendation / SQL Attention feedback がある場合、通常の状態表示と混同しない。
- [ ] feedback candidate を採用しない限り、画面上で勝手に状態が変わったように見えない。
- [ ] M6 loop 全体で、操作文脈が途中で切れない。

監査で削除候補:
- backend dispatch loop が scheduler / dispatcher / runtime を通るか。
- DB write / runtime_event_log の存在確認。
- frontend test で確定できる simple render 項目。

### 3. Scheduler job / cron 設定 UI

Roadmap bundle:
- `product.scheduler_job_manifest_substrate`
- `product.core_runtime_route`
- `product.projection_and_output_lanes`

主な画面候補:
- scheduler job settings panel
- `/admin/contents` scheduler authoring surface
- projection / status display

手動受入 TODO:

巡回監査メモ:
- 2026-07-04 audit: 6項目すべて `implementation_changes_request`。SSOTは `admin.contents` で scheduler job manifest の create / edit / disable と settings/status projection を要求し、backend AdminRuntime / repository / tests は存在する。一方、frontend/APIで `scheduler_jobs` authoring/projection helperや画面surfaceが見つからず、手動受入以前に UI 実装導線が不足している。

Implementation changes request:
- 対象 route / surface: `/admin/contents` scheduler job authoring surface、scheduler job settings/status projection panel。
- 問題: backend `scheduler_jobs` create / edit / disable / list_settings authority はあるが、admin UI 側で cron / interval / manual-only / active / disable / status projection を操作・確認する導線が見つからない。
- 期待する UI 振る舞い: seed/admin-authored scheduler job manifest を data-defined form として作成・編集・disable でき、active / schedule policy / next or last run status / failure status / credential/external port references を secret無しで追える。
- 関連 SSOT: `docs/design/scheduler-job-manifest-ssot.yaml`、`docs/design/admin-console-workflow-ssot.yaml`、`docs/design/runtime-orchestration-ssot.yaml`。
- 対象ファイル / 関数候補: `frontend/api/adminApi.ts` に scheduler_jobs helpers 追加、`frontend/islands/ContentsScreenDesignPanel.tsx` または専用 admin contents subpanel、`backend/runtime/AdminRuntime.SchedulerSettings.cs` の `create` / `edit` / `disable` / `list_settings`。
- 手動受入で確認したい NG 症状: cron設定対象が分からない、有効/無効と手動実行許可を誤認する、失敗/保留/未実行が同じ表示に見える、reload後に設定済み状態を追えない、disable/edit/manual run が通常cron実行と混同される。

- [ ] admin UI で cron / scheduler job の設定対象、周期、有効/無効の意味が分かる。
- [ ] 設定後、次回実行予定や有効状態を UI 上で追える。
- [ ] 実行後、projection / status 表示上で、最終実行結果を人間が理解できる。
- [ ] 失敗した場合、未実行・保留・失敗・無効化を誤認しない。
- [ ] disable / edit / manual run 相当の操作がある場合、通常スケジュール実行と混同しない。
- [ ] reload / 再訪問後に、設定済み cron の状態を自然に把握できる。

監査で削除候補:
- SchedulerJobRunner の内部 lease / result_binding の正しさ。
- on_error policy の unit test で分かる項目。
- DB repository の guard。

### 4. Webhook / hook 設定 UI

Roadmap bundle:
- `product.external_port_substrate`
- `product.webhook_inbox_port_consumer`
- `product.stripe_port_consumer`

主な画面候補:
- external port / hook port admin surface
- webhook projection / intake history / status display

手動受入 TODO:

巡回監査メモ:
- 2026-07-04 audit: 6項目すべて `manual_acceptance_required`。SSOT / Roadmap / test で hook_port は external_port_substrate の generic DB/seed/projection lane、Stripe/Webhook は hook_port consumer として provider-specific admin UI を持たない方針が確認できる。Stripe は production_ready で verification failure / payment state projection / runtime_event_log / SSE lane まで Roadmap上実装済み。手動受入では、DB-driven projection 上で hook_path / route_key / credential requirement reference、受信/拒否/成功/失敗、反映先が人間に自然に追えるかだけを見る。

- [ ] admin UI 上で webhook / hook の設定対象、route、credential requirement の関係を誤認しない。
- [ ] secret / credential は不用意に見えず、設定済み状態だけが分かる。
- [ ] webhook 受信後、projection 上で受信・拒否・成功・失敗を区別できる。
- [ ] 署名失敗、route 未解決、payload 不正が同じ失敗表示に潰れて見えない。
- [ ] 成功した webhook の反映先が projection 上で追える。
- [ ] 失敗した webhook が成功済みデータとして見えない。

監査で削除候補:
- endpoint が AlignAndDispatchAsync を通るか。
- signature verification が fail-close するか。
- raw payload / credential projection deny の静的確認。

### 5. External port consumer UI / projection

Roadmap bundle:
- `product.external_port_substrate`
- `product.file_storage_port_consumer`
- `product.email_port_consumer`
- `product.audit_approval_port_consumer`
- `product.export_sftp_port_consumer`

主な画面候補:
- UI Builder preset surface
- file export / email approval / audit approval / transfer projection

手動受入 TODO:

巡回監査メモ:
- 2026-07-04 audit: 5項目すべて `manual_acceptance_required`。Roadmap / frontend tests / backend tests で file_storage・email・audit_approval・export_sftp は generic external_port lane、UI Builder portTargetRef wiring、safe projection、projection_deny_keys、provider-specific client/runtime不在が確認できる。残るのは、外部サービスを system SSOT と誤認しないか、approval required の承認前/後/拒否後、file export / transfer / email / approval の成功・失敗・保留、provider未接続/future scope の体感表示が実画面で自然に読めるかの受入判断。

- [ ] 外部連携操作が、外部サービスを system SSOT と誤認させない表示になっている。
- [ ] approval required な操作は、UI 上で承認前・承認後・拒否後を誤認しない。
- [ ] file export / transfer / email / audit approval の結果 projection が、成功・失敗・保留として追える。
- [ ] 失敗時に、再試行すべきか設定を直すべきか判断できる表示になっている。
- [ ] provider 未接続や future scope が product failure に見えない。

監査で削除候補:
- provider-specific runtime / client が無いこと。
- credential / raw response が projection されないこと。
- portTargetRef seed wiring の静的確認。

### 6. SQL Attention feedback projection

Roadmap bundle:
- `product.sql_attention_observation_runtime`

主な画面候補:
- SQL Attention feedback panel
- recommendation / candidate projection

手動受入 TODO:

巡回監査メモ:
- 2026-07-04 audit: 5項目すべて `manual_acceptance_required`。SSOT / 実装 / test で SQL Attention projection は append-only evidence / read-only recommendation guidance / admin dispatch pipeline 経由であり、候補は Draft や adopted topology state ではなく、component module から mutation helper も export されていない。OperationPanel の現surfaceは Advanced 手入力 sourceSetId で candidate一覧API待ちのため、通常状態との視覚混同、candidate目的理解、未採用時に状態変化したように見えないか、採用操作が無い/将来追加される場合の前後差、古い/弱い候補の圧力表現は実画面受入判断が必要なため残す。

- [ ] SQL Attention feedback が、現在状態そのものではなく候補・観察結果として見える。
- [ ] candidate を見た時に、何のための候補か分かる。
- [ ] candidate を採用しない限り、route / topology が変わったように見えない。
- [ ] 採用操作がある場合、採用前後の状態差が UI 上で追える。
- [ ] candidate が古い、対象が無い、根拠が弱い場合に、ユーザーが無理に採用すべき表示に見えない。

監査で削除候補:
- append-only evidence / phase vector generation の正しさ。
- topology projection runtime の read-only 境界。
- live DB E2E confirmation。

### 7. Markdown / saved view projection

Roadmap bundle:
- `product.component_markdown_authoring_projection`
- `product.md_viewer_projection_component`
- `product.completed_preset_seed_projection_gate`
- `product.preset_db_seed_registration`

主な画面候補:
- `/admin/team-dashboard`
- md_viewer projection
- UI Builder dashboard candidate surface

手動受入 TODO:

巡回監査メモ:
- 2026-07-04 audit: 5項目すべて `manual_acceptance_required`。SSOT / Roadmap / 実装 / test で `/admin/team-dashboard`、saved view search/card/drawer、MdViewer、completed_preset_seed_json gate、seed invalid explicit error、refresh/clone/rebind seed gate、Markdown body is not runtime SSOT、md_viewer read projection boundary、UIBuilder dashboard candidate separation は確認できる。残るのは、saved view / rendered Markdown / source / binding / seed summary の関係、Markdown body の権威誤認、refresh/clone/rebind 可否、seed invalid 表示、md_viewer mutation authority の有無が実画面上で自然に読めるかの受入判断。

- [ ] saved view / rendered Markdown / source / binding / seed summary の関係を誤認しない。
- [ ] Markdown body が runtime SSOT のように見えない。
- [ ] refresh / clone / rebind が可能な時と不可な時の違いが UI 上で分かる。
- [ ] seed invalid 時に、壊れた空表示ではなく、ユーザーが状態を理解できる。
- [ ] dashboard 上の md_viewer が read projection として見え、mutation authority と混同しない。

監査で削除候補:
- completed_preset_seed_json validation の静的・test 確認。
- runtime factory connection。
- DB migration / repository wiring。

### 8. Helper / manual policy

Roadmap bundle:
- `product.helper_manual_policy`

主な画面候補:
- helper viewer / admin header / manual projection が存在する場合のみ

手動受入 TODO:

巡回監査メモ:
- 2026-07-04 audit: 4項目すべて処理済み。helper/manual は `.agent/tasks/todo.md` の canonical Bundle `helper-manual` として `not_started` で管理済みのため、この manual acceptance TODO から新規 main TODO は生やさない。SSOTは helper reference artifact / admin helper projection を runtime authority ではなく projection-only viewer と定義し、admin submit / apply / promote / approval / merge target decision / active mutation authority を禁止している。現時点では helper未実装を M6 loop failure と扱わず、`helper-manual` 実装後に画面受入で再確認する。

- [ ] helper がある場合、現在の作業文脈に対応した説明として見える。
- [ ] helper が admin action / apply / promote authority を持つように見えない。
- [ ] internal vocabulary と user-facing vocabulary の対応が、操作判断を助ける形になっている。
- [ ] helper 未実装の場合、M6 loop の失敗として扱わない。

監査で削除候補:
- helper schema / seed artifact の存在確認。
- MCP payload reference の構造確認。

---

## Output Candidate

後続作成候補:
- `.agent/checklists/check-roadmap-manual-acceptance-ui.md`

チェックシート化する時は、次の列に圧縮する。

- Route / surface
- Roadmap source bundle
- Manual acceptance viewpoint
- Expected UI behavior
- NG symptom
- Audit-removable note

---

## Completion Criteria

- [ ] Roadmap `implementation_registry` から admin / projection UI に関係する機能を抽出している。
- [ ] backend runtime / DB / test で確認できることを主チェック項目にしていない。
- [ ] 手動受入前提で、画面上の迷い・状態誤認・反映先不明・失敗時復帰を中心にしている。
- [ ] 監査で潰せる項目は後から削除できるよう、`監査で削除候補` を併記している。
- [ ] implemented 済み bundle を未実装へ戻していない。
- [ ] optional / future / helper policy を M6 manual acceptance の必須 NG にしていない。
