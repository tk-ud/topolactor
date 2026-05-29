import { defaultPackage } from "../../package/defaultPackage.ts";
import { defaultSchema } from "../../schema/defaultSchema.ts";
import { defaultStructureMap } from "../../structure_map.ts";
import AdminAuthGate from "../../islands/AdminAuthGate.tsx";
import AdminHowTo from "../../components/AdminHowTo.tsx";
import AdminHelpPanel from "../../components/AdminHelpPanel.tsx";
import { ADMIN_INDEX_GUIDE, ADMIN_ROUTE_CARDS } from "../../content/adminGuides.ts";

export default function AdminIndex() {
  const structureMapEntries = Object.values(defaultStructureMap);

  return (
    <AdminAuthGate>
      <main class="page-main-wide font-mono">
        <h1 class="page-title">topolactor — 管理 / トポロジー検査</h1>

        <AdminHowTo
          steps={ADMIN_INDEX_GUIDE.howToSteps}
          prerequisites={ADMIN_INDEX_GUIDE.prerequisites}
        />
        <AdminHelpPanel {...ADMIN_INDEX_GUIDE} />

        <section class="mb-8">
          <h2 class="section-title">管理画面一覧（画面別 How to 要約）</h2>
          <p class="text-muted mb-4 text-sm">
            登録の推奨順: Import → Seed → UI Builder →（必要なら）Token / Vector Validate。
            登録後の動作確認は <a href="/admin/runtime" class="link">/admin/runtime</a>（dispatch 検証）。
            いずれも<strong>直接 DB エディタではなく</strong> backend 検証後に永続化します。
          </p>
          <ul class="space-y-4">
            {ADMIN_ROUTE_CARDS.map((card) => (
              <li key={card.href} class="card">
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

        <section class="mb-8">
          <h2 class="section-title">構造マップ（参照用）</h2>
          <p class="text-muted-xs mb-3">
            デモ用デフォルトの attractor → package/schema/component 解決。編集は本 UI では行いません。
          </p>
          <div class="table-wrap">
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

        <section class="mb-8">
          <h2 class="section-title">デフォルトパッケージ</h2>
          <dl class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
            <dt class="font-semibold">packageId</dt>
            <dd>{defaultPackage.packageId}</dd>
            <dt class="font-semibold">name</dt>
            <dd>{defaultPackage.name}</dd>
            <dt class="font-semibold">componentIds</dt>
            <dd>{defaultPackage.componentIds.join(", ")}</dd>
          </dl>
        </section>

        <section>
          <h2 class="section-title">デフォルトスキーマ</h2>
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

        <p class="nav-footer">
          仕様: <code>docs/registrar-admin-ui-specification.md</code>
          {" · "}
          <a href="/admin/runtime" class="link">ランタイム検証</a>
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
