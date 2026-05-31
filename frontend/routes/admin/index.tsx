import { defaultPackage } from "../../package/defaultPackage.ts";
import { defaultSchema } from "../../schema/defaultSchema.ts";
import { defaultStructureMap } from "../../structure_map.ts";
import AdminAuthGate from "../../islands/AdminAuthGate.tsx";
import AdminHowTo from "../../components/AdminHowTo.tsx";
import AdminHelpPanel from "../../components/AdminHelpPanel.tsx";
import AdminMainFlowStepper from "../../components/AdminMainFlowStepper.tsx";
import {
  ADMIN_INDEX_GUIDE,
  ADMIN_ROUTE_CARDS,
  ACCEPTANCE_FLOW_STEPS,
  ACCEPTANCE_CHECKLIST,
} from "../../content/adminGuides.ts";

export default function AdminIndex() {
  const structureMapEntries = Object.values(defaultStructureMap);

  return (
    <AdminAuthGate>
      <main class="page-main-wide font-sans">
        <h1 class="page-title">topolactor — 管理</h1>

        <p class="mb-4 text-sm leading-relaxed text-gray-700">
          マニフェスト → インポート → UI Builder → Runtime確認の順で進めてください。
          利用には<strong>ログイン</strong>が必要です（未ログイン時はログイン画面が表示されます）。
        </p>

        <AdminMainFlowStepper />

        <AdminHowTo
          steps={ADMIN_INDEX_GUIDE.howToSteps}
          prerequisites={ADMIN_INDEX_GUIDE.prerequisites}
        />
        <AdminHelpPanel {...ADMIN_INDEX_GUIDE} />

        <section class="mb-8">
          <h2 class="section-title">各ステップの詳細</h2>
          <p class="text-muted mb-4 text-sm">
            手動検証時の推奨順序です。各ステップの完了サインを確認してから次へ進んでください。
          </p>
          <ol class="space-y-3">
            {ACCEPTANCE_FLOW_STEPS.map((step) => (
              <li key={step.step} class="card font-mono">
                <div class="flex flex-wrap items-baseline gap-2">
                  <span class="badge-info text-xs font-mono">Step {step.step}</span>
                  <a href={step.href} class="link font-semibold">{step.label}</a>
                </div>
                <p class="mt-1 text-sm">{step.purpose}</p>
                <p class="text-muted-xs mt-1">
                  <strong>完了サイン:</strong> {step.completionSign}
                </p>
                {step.boundaryNote && (
                  <details class="text-muted-xs mt-1">
                    <summary class="cursor-pointer italic">技術情報（境界）</summary>
                    <p class="mt-1 pl-2">{step.boundaryNote}</p>
                  </details>
                )}
                {step.nextLabel && (
                  <p class="text-muted-xs mt-1">→ 次: {step.nextLabel}</p>
                )}
              </li>
            ))}
          </ol>
        </section>

        <section class="mb-8">
          <h2 class="section-title">確認チェックリスト</h2>
          <p class="text-muted mb-3 text-sm">
            手動受入時の確認観点です。完了判定の正本ではありません。
          </p>
          <div class="space-y-3">
            {ACCEPTANCE_CHECKLIST.map((item) => (
              <div key={item.href} class="card font-mono">
                <a href={item.href} class="link font-semibold text-sm">{item.label}</a>
                <ul class="mt-2 space-y-0.5">
                  {item.checks.map((check, i) => (
                    <li key={i} class="flex items-start gap-2 text-sm">
                      <span class="text-muted-xs mt-0.5 shrink-0">□</span>
                      <span>{check}</span>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
        </section>

        <section class="mb-8">
          <h2 class="section-title">管理画面一覧</h2>
          <p class="text-muted mb-4 text-sm">
            主導線: <strong>マニフェスト → インポート → UI Builder → Runtime確認</strong>。
            シード・コンテンツ・トークン辞書は必要に応じて利用してください。
          </p>
          <ul class="space-y-4">
            {ADMIN_ROUTE_CARDS.map((card) => (
              <li key={card.href} class="card font-mono">
                <a href={card.href} class="link font-semibold">{card.label}</a>
                <p class="mt-1 text-sm">{card.purpose}</p>
                <ol class="text-muted-xs mt-2 list-decimal list-inside space-y-0.5">
                  {card.howToSummary.map((step, i) => (
                    <li key={i}>{step}</li>
                  ))}
                </ol>
                <p class="text-muted-xs mt-1">関係: {card.relation}</p>
                {card.caution && (
                  <p class="text-muted-xs mt-1 text-yellow-800">⚠ {card.caution}</p>
                )}
              </li>
            ))}
          </ul>
        </section>

        <details class="mb-8 rounded border border-gray-200 bg-gray-50 p-4 text-sm">
          <summary class="cursor-pointer font-semibold">技術情報（開発者向け・参照データ）</summary>
          <section class="mt-4">
            <h3 class="section-title text-base">構造マップ（参照用）</h3>
            <p class="text-muted-xs mb-3">
              デモ用デフォルトの attractor → package/schema/component 解決。編集は本 UI では行いません。
            </p>
            <div class="table-wrap font-mono">
              <table class="table">
                <thead>
                  <tr>
                    <th>attractorKey</th>
                    <th>packageId</th>
                    <th>schemaId</th>
                    <th>componentIds</th>
                  </tr>
                </thead>
                <tbody>
                  {structureMapEntries.map((entry) => (
                    <tr key={entry.attractorKey}>
                      <td>{entry.attractorKey}</td>
                      <td class="text-muted-xs">{entry.packageId}</td>
                      <td class="text-muted-xs">{entry.schemaId}</td>
                      <td class="text-muted-xs">{entry.componentIds.join(", ")}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
          <section class="mt-6 font-mono">
            <h3 class="section-title text-base">デフォルトパッケージ</h3>
            <dl class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
              <dt class="font-semibold">packageId</dt>
              <dd>{defaultPackage.packageId}</dd>
              <dt class="font-semibold">name</dt>
              <dd>{defaultPackage.name}</dd>
              <dt class="font-semibold">componentIds</dt>
              <dd>{defaultPackage.componentIds.join(", ")}</dd>
            </dl>
          </section>
          <section class="mt-6 font-mono">
            <h3 class="section-title text-base">デフォルトスキーマ</h3>
            <dl class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
              <dt class="font-semibold">schemaId</dt>
              <dd>{defaultSchema.schemaId}</dd>
              <dt class="font-semibold">name</dt>
              <dd>{defaultSchema.name}</dd>
              <dt class="font-semibold">fields</dt>
              <dd>
                <ul class="list-inside list-disc">
                  {defaultSchema.fields.map((f) => (
                    <li key={f.key}>
                      {f.key} ({f.type}) — {f.label}
                      {f.required ? " *" : ""}
                    </li>
                  ))}
                </ul>
              </dd>
            </dl>
          </section>
          <p class="text-muted-xs mt-4">
            仕様: <code>docs/registrar-admin-ui-specification.md</code>
            {" · "}
            Frontend = projection / intent submission。topology 正本は DB + backend runtime。
          </p>
        </details>

        <p class="nav-footer font-mono">
          <a href="/admin/runtime" class="link">Runtime確認</a>
          {" · "}
          <a href="/auth" class="link">ログイン</a>
          {" · "}
          <a href="/" class="link">トップ</a>
          {" · "}
          <a href="/demo" class="link">デモ</a>
        </p>
      </main>
    </AdminAuthGate>
  );
}
