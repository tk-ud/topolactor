# Error
- Error内容
  - uuid: projection-lane-seed-test-hardening-2026-07-01
  - 不具合箇所: projection lane / seed contract coverage was too thin; demo/admin seed manifests did not all carry a DB-free lane-consumable projection constructor + screen data fixture before this hardening. PR537 auditで user-facing `/demo` render surface が `projectionDefinition` を無視した場合の検出test不足も確認された。
  - 対象ファイル: `frontend/tests/projectionLaneSeedHarness.test.ts`, `frontend/tests/sseLane.test.ts`, `backend/tests/Topolactor.Runtime.Tests/RuntimeExecutorTests.cs`, `db/seed_empty.sql`, `db/demo_seed.sql`, `.github/workflows/projection-lane-seed-hardening.yml`, `.agent/tests/check-projection-lane-seed-hardening.sh`
  - 対象SSOT: `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml`, `docs/framework-core.yaml`, `docs/framework-policy.yaml`, `docs/system-roadmap.yaml`, `docs/design/db-table-connectivity-audit.md`
  - 検出test: `dotnet test backend/tests/Topolactor.Runtime.Tests/Topolactor.Runtime.Tests.csproj --filter "FullyQualifiedName~ManifestDispatcherManifestDrivenTests|FullyQualifiedName~ScreenDataShapeQueryRuntimeTests" --nologo --verbosity minimal`; `deno test frontend/tests/uiRenderedInteraction.test.ts --allow-read --filter "projectionDefinition"`; `bash .agent/tests/check-projection-lane-seed-hardening.sh`
  - lane: backend ManifestDispatcher projectionDefinition injection; RuntimeExecutor/ScreenDataShapeQueryRuntime emission.data; frontend SSE receiver/dispatcher/projection runtime/render helper; frontend user-facing render (`ProjectionView`, `UserDemoStepper`, `UserDemoResultCard`); DB-free seed static parse -> lane harness -> projection assertion.
  - seed id / manifest id / mapping id: `db/seed_empty.sql` admin manifest seeds `00000000-0000-0000-0000-00000000005e` through `00000000-0000-0000-0000-000000000064` and `00000000-0000-0000-0000-00000000007d` through `00000000-0000-0000-0000-00000000007f`; `db/demo_seed.sql` demo manifest seeds `00000000-0000-0000-0000-000000000080` through `00000000-0000-0000-0000-000000000084`; mapping types `dispatcher_mapping`, `runtime_mapping`, `projection_constructor_mapping`, `screen_data_shape`, `db_notify_projection_mapping`; tableRef `seed.projection_lane`.

## 暫定対応方針

- Test hardening scopeとして、production runtime / frontend render 本体の挙動変更は行わず、DBなし fast lane のテスト面と seed fixture を補強した。
- `projectionLaneSeedHarness.test.ts` で SQL seed を静的 parse し、demo/admin 対象 manifest ごとに mapping contract を検査し、抽出した `initialDataRows[0].values` を lane harness に流して `projectionFromEmission` と `projectionRuntime` の projection assertion まで到達させる。
- `seedData ?? null` は使わず、seed 欠落は NG test で明示 failure として扱う。literal fixture は `explicitLiteralFixture` という名前で保持し、seedData present path と混同しない assertion に限定した。
- GitHub Actions に backend lane / frontend lane / frontend user-facing render / seed contract / seed-to-lane integration を分離した job 名で追加し、落ちた場所を CI 上で切り分けられるようにした。
- 追加テストの error message は `lane`, `seed`, `manifest`, `target/layer/action`, `mapping` を含むようにした。

## issue 実装時のok軸

- Backend lane: `ManifestDispatcher` が manifest topology の `projection_constructor_mapping` から `Emission.ProjectionDefinition` を注入することをテストで検出可能にした。
- Frontend lane: SSE receiver / dispatcher / projection runtime / render helper が projectionDefinition を消費する経路を既存 `sseLane.test.ts` と workflow job で実行する。
- Seed contract: `db/seed_empty.sql` / `db/demo_seed.sql` を DB 接続なしで静的 parse し、必要 mapping と `screen_data_shape.tableRef` / physical table / wiring seed の整合を確認する。
- Seed-to-lane integration: seedData を loop で lane harness に投入し、projection assertion まで到達する。
- PR537 auditで user-facing render 未検査が追加 Error として確認されたため、`ProjectionView` と `/demo` の `UserDemoStepper` → `UserDemoResultCard` が `emission.projectionDefinition` を消費しない場合に fail する DOM/SSR test を追加した。現時点で追加 test は pass しており、今後この test が runtime read-source / frontend render / canonical projection consumer の断線を検出した場合は、本bundle内で雑に直さず、次 bundle / issue として分離する。
