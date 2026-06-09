import { useState } from "preact/hooks";
import { JSX } from "preact";
import AdminHowTo from "../components/AdminHowTo.tsx";
import AdminHelpPanel from "../components/AdminHelpPanel.tsx";
import { ADMIN_CONTENTS_GUIDE } from "../content/adminGuides.ts";
import { UX_CONTENTS, UX_HUB_MANIFESTS_PAGE } from "../content/adminUxTerms.ts";
import type { AdminManifestListItem } from "../api/adminApi.ts";
import ContentsScreenDesignPanel from "./ContentsScreenDesignPanel.tsx";

/** Canonical single-page manifest authoring surface (SSOT pipeline steps 1–3). */
export default function ContentsAdmin(): JSX.Element {
  const [sharedManifestId, setSharedManifestId] = useState("");
  const [manifests, setManifests] = useState<AdminManifestListItem[]>([]);
  const [manifestsVersion, setManifestsVersion] = useState(0);

  const bumpManifests = () => setManifestsVersion((v) => v + 1);

  return (
    <main class="page-main font-mono">
      <h1 class="page-title">topolactor — 管理 / {UX_CONTENTS}</h1>
      <p class="mb-4">
        <a href="/admin" class="link">&larr; 管理インデックス</a>
      </p>

      <section class="mb-4 rounded-lg border border-blue-200 bg-blue-50 p-4 text-sm text-blue-900">
        <p class="font-semibold">この画面でできること: 新しいページを作る（step 1 → 3）</p>
        <p class="mt-1 text-xs">
          正規フローは step 1（空登録）→ 2（テーブル定義）→ 2.5（関連）→ 3（ページ設定）です。
          完了後は
          <a href="/admin/ui-builder" class="link font-semibold"> 画面づくり </a>
          へ進みます。ページ間のつながりは
          <a href="/admin/manifests" class="link font-semibold">
            {UX_HUB_MANIFESTS_PAGE}
          </a>
          で管理します。
        </p>
      </section>

      <AdminHowTo steps={ADMIN_CONTENTS_GUIDE.howToSteps} />
      <AdminHelpPanel {...ADMIN_CONTENTS_GUIDE} />

      <ContentsScreenDesignPanel
        sharedManifestId={sharedManifestId}
        onSharedManifestIdChange={setSharedManifestId}
        manifests={manifests}
        onManifestsChange={setManifests}
        manifestsVersion={manifestsVersion}
        onManifestsReload={bumpManifests}
      />
    </main>
  );
}
